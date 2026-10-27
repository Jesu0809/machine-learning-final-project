using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NightShift.Game;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.EditorTools
{
    /// <summary>
    /// "Night Shift/Create Game Scene": sets up the 3D render pipeline, the GameAssets references
    /// (Freddy, the trained model, font, sounds) and the Game scene, whose only object is the
    /// <see cref="HuntDirector"/> that builds everything at runtime.
    /// </summary>
    public static class GameSceneMenu
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";
        public const string AssetsPath = "Assets/Resources/GameAssets.asset";
        public const string PipelinePath = "Assets/Settings/URP-3D.asset";
        public const string RendererPath = "Assets/Settings/URP-3D-Renderer.asset";
        public const string ModelsFolder = "Assets/ML/Models";

        /// <summary>Model used by the game; the newest freddy_v*.onnx if this one does not exist.</summary>
        public const string PreferredModel = "freddy_v7";

        [MenuItem("Night Shift/Create Game Scene")]
        public static void CreateGameScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            SetUpRenderPipeline();
            FreddySetup.Setup();
            UpdateGameAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("HuntDirector").AddComponent<HuntDirector>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // The game first, then the headless training scene.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists(HuntTrainingMenu.ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(HuntTrainingMenu.ScenePath, false));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            PlayerSettings.productName = "Night Shift";
            PlayerSettings.companyName = "UdeC Machine Learning";
            AssetDatabase.SaveAssets();
            Debug.Log("Game scene created at " + ScenePath);
        }

        [MenuItem("Night Shift/Update Game Assets (model, sounds)")]
        public static void UpdateGameAssets()
        {
            Directory.CreateDirectory("Assets/Resources");
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(AssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }
            assets.FreddyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FreddySetup.PrefabPath);
            assets.FreddyModel = FindModel();
            assets.Font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/VT323-Regular.ttf");
            assets.ControlsKeyboard = UiTexture("Assets/Art/UI/controls-keyboard.png");
            assets.ControlsGamepad = UiTexture("Assets/Art/UI/controls-gamepad.png");
            assets.SignMain = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Exterior/sign-main.png");
            assets.SignPole = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Exterior/sign-pole.png");
            assets.PolicyWeights = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/FreddyPolicy.bytes");
            // The menu shows the real training curve and evaluation: copy them from the website data.
            Directory.CreateDirectory("Assets/Resources/Data");
            string webData = Path.GetFullPath(Path.Combine(Application.dataPath, "../../web/data"));
            var copies = new Dictionary<string, string>
            {
                { Path.Combine(webData, "curves", PreferredModel + ".csv"), "training_curves.csv" },
                { Path.Combine(webData, "results.json"), "results.json" }
            };
            foreach (KeyValuePair<string, string> copy in copies)
            {
                if (File.Exists(copy.Key))
                {
                    File.Copy(copy.Key, "Assets/Resources/Data/" + copy.Value, true);
                    AssetDatabase.ImportAsset("Assets/Resources/Data/" + copy.Value);
                }
            }
            assets.TrainingCurve = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/training_curves.csv");
            assets.EvaluationResults = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/results.json");
            assets.MaterialTemplates = CreateMaterialTemplates();
            assets.Surfaces = SurfaceImporter.ImportAll();
            assets.Props = PropImporter.ImportAll();
            assets.Signs = SignImporter.ImportAll();
            assets.AmbientOcclusion = RenderingSetup.Apply();
            assets.AmbientLoop = Clip("ambient-hum-loop");
            assets.MenuMusic = Clip("menu-music-loop");
            assets.Scream = Clip("jumpscare-scream");
            assets.LockerDoor = Clip("door-slam");
            assets.ExitDoor = Clip("door-slide");
            assets.FusePickup = Clip("power-out");
            assets.RainLoop = Clip("rain-loop");
            assets.Thunder = Clip("thunder");
            assets.CarEngine = Clip("car-engine-loop");
            assets.VoicemailBeep = Clip("voicemail-beep");
            assets.IntroLines = StoryClips("intro_");
            assets.Calls = new NightCall[NightShift.Game.StoryData.Calls.Length];
            for (int night = 0; night < assets.Calls.Length; night++)
            {
                assets.Calls[night] = new NightCall { Lines = StoryClips("night" + (night + 1) + "_") };
            }
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            Debug.Log("Game assets: model = " + (assets.FreddyModel != null ? assets.FreddyModel.name : "none (scripted Freddy)"));
        }

        /// <summary>
        /// One saved material per shader setup the level uses at runtime (lit plain, lit with normal
        /// map, lit with glow, unlit additive). Referencing them keeps those shader variants in builds.
        /// </summary>
        private static Material[] CreateMaterialTemplates()
        {
            const string folder = "Assets/Art/Materials/Templates";
            Directory.CreateDirectory(folder);
            Material Template(string name, string shader, System.Action<Material> setUp)
            {
                string path = folder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find(shader));
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = Shader.Find(shader);
                setUp(material);
                EditorUtility.SetDirty(material);
                return material;
            }

            Texture2D white = Texture2D.whiteTexture;
            return new[]
            {
                Template("LitPlain", "Universal Render Pipeline/Lit", m => { }),
                Template("LitNormal", "Universal Render Pipeline/Lit", m => { m.EnableKeyword("_NORMALMAP"); m.SetTexture("_BumpMap", Texture2D.normalTexture); }),
                Template("LitEmission", "Universal Render Pipeline/Lit", m =>
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.white);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }),
                Template("UnlitAdditive", "Universal Render Pipeline/Unlit", NightShift.Game.BuildKit.MakeAdditive),
                Template("UnlitAlpha", "Universal Render Pipeline/Unlit", NightShift.Game.BuildKit.MakeAlpha),
                Template("LitAlpha", "Universal Render Pipeline/Lit", NightShift.Game.BuildKit.MakeLitAlpha)
            };
        }

        private static ModelAsset FindModel()
        {
            // Only a brain trained on the current map: v1–v3 learned the old one-floor, 9-room graph
            // (54 inputs) and cannot drive Freddy here. Without it the game uses the scripted hunter.
            return AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelsFolder + "/" + PreferredModel + ".onnx");
        }

        /// <summary>Assets/Audio/Story/{prefix}01.wav, 02.wav … in order, until one is missing.</summary>
        private static AudioClip[] StoryClips(string prefix)
        {
            var clips = new List<AudioClip>();
            for (int i = 1; i <= 30; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Story/" + prefix + i.ToString("00") + ".wav");
                if (clip == null)
                {
                    break;
                }
                clips.Add(clip);
            }
            return clips.ToArray();
        }

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".ogg");

        /// <summary>A UI picture with transparency: no mipmaps, no compression artifacts on the thin lines.</summary>
        private static Texture2D UiTexture(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool changed = importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                               || importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Replaces the 2D renderer of the project template with a 3D Universal Renderer: per-pixel
        /// point and spot lights with shadows (the flashlight), and post-processing.
        /// </summary>
        [MenuItem("Night Shift/Set Up 3D Render Pipeline")]
        public static void SetUpRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                MethodInfo create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
                var renderer = (ScriptableRendererData)create.Invoke(null, new object[] { RendererPath, RendererType.UniversalRenderer, false, "Renderer" });
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 2048;
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = false;
            serialized.FindProperty("m_ShadowDistance").floatValue = 25f;
            serialized.FindProperty("m_SupportsHDR").boolValue = true;
            serialized.FindProperty("m_MSAA").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            // The 2D template's renderer is no longer used.
            AssetDatabase.DeleteAsset("Assets/Settings/UniversalRP.asset");
            AssetDatabase.DeleteAsset("Assets/Settings/Renderer2D.asset");
            AssetDatabase.DeleteAsset("Assets/Settings/Scenes");
            AssetDatabase.SaveAssets();
            Debug.Log("3D render pipeline ready: " + PipelinePath);
        }

        /// <summary>
        /// WebGL build straight into the website: web/game/Build/game.*. Gzip without
        /// decompression fallback; the Flask app adds the Content-Encoding headers.
        /// </summary>
        [MenuItem("Night Shift/Build WebGL (website)")]
        /// <summary>
        /// Where the WebGL build goes: "-webglOut path" on the command line, else the website's
        /// web/game in the full project repository, else Builds/WebGL in a stand-alone checkout.
        /// </summary>
        private static string WebGLOutput()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-webglOut");
            if (index >= 0 && index + 1 < args.Length)
            {
                return Path.GetFullPath(args[index + 1]);
            }
            string web = Path.GetFullPath(Path.Combine(Application.dataPath, "../../web"));
            return Directory.Exists(web)
                ? Path.Combine(web, "game")
                : Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/WebGL"));
        }

        public static void BuildWebGL()
        {
            if (!File.Exists(ScenePath))
            {
                CreateGameScene();
            }
            string output = WebGLOutput();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            // The website has its own page (web/templates/index.html); Unity's template is not used.
            File.Delete(Path.Combine(output, "index.html"));
            if (Directory.Exists(Path.Combine(output, "TemplateData")))
            {
                Directory.Delete(Path.Combine(output, "TemplateData"), true);
            }
            Debug.Log("WebGL build: " + report.summary.result + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB) at " + output);
        }

        /// <summary>Plays the game scene in the developer explore mode (see ExploreMode.cs).</summary>
        [MenuItem("Night Shift/Explore Map (developers)")]
        public static void ExploreMap()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            if (!File.Exists(ScenePath))
            {
                CreateGameScene();
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(ExploreMode.EditorFlag, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Night Shift/Build Game")]
        public static void BuildGame()
        {
            if (!File.Exists(ScenePath))
            {
                CreateGameScene();
            }
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string folder = HuntTrainingMenu.BuildsFolder();
            string output = Path.Combine(folder, windows ? "NightShift/NightShift.exe" : "NightShift.app");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = windows ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            Debug.Log("Game build: " + report.summary.result + " at " + output);
        }
    }
}
