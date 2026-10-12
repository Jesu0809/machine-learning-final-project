using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>Import settings for the Freddy model and its Mixamo animations.</summary>
    public class FreddyImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(FreddySetup.Folder + "/"))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            // Keep the original material names: the setup maps them to our URP materials.
            importer.materialImportMode = assetPath == FreddySetup.SkinPath
                ? ModelImporterMaterialImportMode.ImportViaMaterialDescription
                : ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;

            if (assetPath == FreddySetup.SkinPath)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                // The animation-only files reuse the avatar of the model with skin.
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(FreddySetup.SkinPath);
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(FreddySetup.Folder + "/"))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            bool loop = !assetPath.EndsWith("freddy-attack.fbx");
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FreddySetup.Folder + "/Textures/"))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.png");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }

    /// <summary>
    /// "Night Shift/Setup Freddy": materials, animator controller and prefab for the Freddy model
    /// (Sketchfab model by 2012carlitos17, CC BY 4.0, rigged and animated with Mixamo).
    /// </summary>
    public static class FreddySetup
    {
        public const string Folder = "Assets/Art/Freddy";
        public const string SkinPath = Folder + "/freddy-walk.fbx";
        public const string PrefabPath = Folder + "/Freddy.prefab";
        public const string ControllerPath = Folder + "/Freddy.controller";
        public const float TargetHeight = 2.1f;

        private static readonly string[] AnimationFiles = { "freddy-idle", "freddy-walk", "freddy-run", "freddy-search", "freddy-attack" };

        // Material name (from the original model) -> base color texture, normal map, emission.
        private static readonly Dictionary<string, (string color, string normal, bool glow)> Materials =
            new Dictionary<string, (string, string, bool)>
            {
                { "freddy_body", ("freddy_body_baseColor", "freddy_body_normal", false) },
                { "freddy_head", ("freddy_head_baseColor", "freddy_head_normal", false) },
                { "freddy_parts", ("freddy_parts_baseColor", "freddy_parts_normal", false) },
                { "freddy_skeleton", ("freddy_skeleton_baseColor", "freddy_skeleton_normal", false) },
                { "Hat_Bow", ("Hat_Bow_baseColor", null, false) },
                { "Eyeball_l", ("Eyeball_l_baseColor", null, true) },
                { "Eyeball_r", ("Eyeball_l_baseColor", null, true) },
                { "Eye_Black", ("Eye_Black_baseColor", null, false) },
                { "EyeGlow", ("EyeGlow_baseColor", null, true) },
            };

        [MenuItem("Night Shift/Setup Freddy")]
        public static void Setup()
        {
            // The skin model first, so the animation files can copy its avatar.
            AssetDatabase.ImportAsset(SkinPath, ImportAssetOptions.ForceUpdate);
            foreach (string file in AnimationFiles)
            {
                string path = Folder + "/" + file + ".fbx";
                if (path != SkinPath)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }

            Dictionary<string, Material> materials = CreateMaterials();
            AnimatorController controller = CreateController();
            CreatePrefab(materials, controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Freddy prefab ready at " + PrefabPath);
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            string folder = Folder + "/Materials";
            Directory.CreateDirectory(folder);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<string, Material>();

            foreach (KeyValuePair<string, (string color, string normal, bool glow)> entry in Materials)
            {
                string path = folder + "/" + entry.Key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;

                var color = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + entry.Value.color + ".png");
                material.SetTexture("_BaseMap", color);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.25f);
                material.SetFloat("_Metallic", 0f);

                if (entry.Value.normal != null)
                {
                    material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + entry.Value.normal + ".png"));
                    material.EnableKeyword("_NORMALMAP");
                }
                if (entry.Value.glow)
                {
                    // The eyes glow a little in the dark, like the animatronics in the games.
                    material.EnableKeyword("_EMISSION");
                    material.SetTexture("_EmissionMap", color);
                    material.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 0.6f));
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                EditorUtility.SetDirty(material);
                result[entry.Key] = material;
            }
            return result;
        }

        private static AnimatorController CreateController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Searching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Speed 0 = idle, 1 = walk, 2 = run.
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("freddy-idle"), 0f);
            tree.AddChild(Clip("freddy-walk"), 1f);
            tree.AddChild(Clip("freddy-run"), 2f);
            machine.defaultState = locomotion;

            AnimatorState search = machine.AddState("Search");
            search.motion = Clip("freddy-search");
            AnimatorState attack = machine.AddState("Attack");
            attack.motion = Clip("freddy-attack");

            AnimatorStateTransition toSearch = locomotion.AddTransition(search);
            toSearch.AddCondition(AnimatorConditionMode.If, 0f, "Searching");
            toSearch.hasExitTime = false;
            toSearch.duration = 0.2f;
            AnimatorStateTransition fromSearch = search.AddTransition(locomotion);
            fromSearch.AddCondition(AnimatorConditionMode.IfNot, 0f, "Searching");
            fromSearch.hasExitTime = false;
            fromSearch.duration = 0.2f;

            AnimatorStateTransition toAttack = machine.AddAnyStateTransition(attack);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;
            return controller;
        }

        private static AnimationClip Clip(string file)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + file + ".fbx"))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            Debug.LogError("No animation clip in " + file);
            return null;
        }

        private static void CreatePrefab(Dictionary<string, Material> materials, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SkinPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Freddy";

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    shared[i] = Match(shared[i] != null ? shared[i].name : "", materials, i);
                }
                renderer.sharedMaterials = shared;
            }

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Normalize the height so gameplay distances do not depend on the source scale.
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }
            if (bounds.size.y > 0.01f)
            {
                instance.transform.localScale *= TargetHeight / bounds.size.y;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
        }

        private static Material Match(string name, Dictionary<string, Material> materials, int index)
        {
            foreach (KeyValuePair<string, Material> entry in materials)
            {
                if (name.StartsWith(entry.Key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }
            Debug.LogWarning("Freddy: no material for '" + name + "' (slot " + index + "), using body.");
            return materials["freddy_body"];
        }
    }
}
