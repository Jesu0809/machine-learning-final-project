using System;
using System.IO;
using NightShift.Game;
using NightShift.Hunt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.EditorTools
{
    /// <summary>
    /// Renders still pictures of the level in batch mode, with no game window: for checking the set
    /// dressing without playing. Usage: -executeMethod NightShift.EditorTools.BatchShots.Run -shots "dir".
    /// Lit evenly (not with the game's darkness), because it is for seeing what is there.
    /// </summary>
    public static class BatchShots
    {
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-shots");
            string folder = at >= 0 ? args[at + 1] : "Shots";
            // -dark: the darkness and color grade of the game, as a player sees it.
            bool dark = Array.IndexOf(args, "-dark") >= 0;
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = dark ? new Color(0.075f, 0.062f, 0.055f) : new Color(0.45f, 0.43f, 0.42f);
            RenderSettings.fog = dark;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.016f;
            RenderSettings.fogColor = new Color(0.012f, 0.014f, 0.02f);
            var root = new GameObject("Root").transform;
            GameAssets assets = GameAssets.Load();
            LevelBuilder.Build(root, assets);
            Exterior.Build(root, assets);
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = dark ? 0f : 0.6f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            var camera = new GameObject("Shot").AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = dark ? Color.black : new Color(0.1f, 0.1f, 0.13f);
            if (dark)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var volume = new GameObject("Grade").AddComponent<Volume>();
                volume.isGlobal = true;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.saturation.Override(HuntDirector.GradeSaturation);
                color.contrast.Override(HuntDirector.GradeContrast);
                color.postExposure.Override(HuntDirector.BaseExposure);
                color.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
                profile.Add<Vignette>(true).intensity.Override(0.42f);
                profile.Add<Bloom>(true).intensity.Override(0.6f);
                volume.sharedProfile = profile;
            }
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            // The first frame in batch mode draws some materials wrong: throw it away.
            camera.Render();
            int count = 0;
            for (int i = 0; i < HuntMap.RoomCount; i++)
            {
                var room = (Room)i;
                Rect area = PizzeriaLayout.Area(room);
                float inX = Mathf.Min(1.5f, area.width * 0.25f);
                float inZ = Mathf.Min(1.5f, area.height * 0.25f);
                Vector2 corner = new Vector2(area.xMin + inX, area.yMin + inZ);
                Vector2 opposite = new Vector2(area.xMax - inX, area.yMax - inZ);
                Shoot(camera, target, folder, room + "-1", new Vector3(corner.x, 1.7f, corner.y), new Vector3(opposite.x, 1f, opposite.y));
                Shoot(camera, target, folder, room + "-2", new Vector3(opposite.x, 1.7f, opposite.y), new Vector3(corner.x, 1f, corner.y));
                count += 2;
            }
            Shoot(camera, target, folder, "Front-1", new Vector3(8f, 1.7f, -38f), new Vector3(-2f, 5f, -14f));
            Shoot(camera, target, folder, "Front-2", new Vector3(36f, 8f, -46f), new Vector3(0f, 6f, -4f));
            count += 2;
            count += ShootPockets(camera, target, folder, dark);
            var seen = new System.Collections.Generic.Dictionary<string, int>();
            foreach (Transform thing in root.GetComponentsInChildren<Transform>())
            {
                if (Array.IndexOf(new[] { "HighStriker", "RingToss", "PopcornCart", "Trunk", "CostumeRack", "ClownDummy", "Unicycle", "Wreck", "Car" }, thing.name) < 0)
                {
                    continue;
                }
                seen.TryGetValue(thing.name, out int n);
                seen[thing.name] = n + 1;
                Debug.Log("BatchShots: " + thing.name + " at " + thing.position);
                if (n == 0)
                {
                    Vector3 toward = thing.position.x > 0f ? Vector3.left : Vector3.right;
                    if (thing.name == "ClownDummy")
                    {
                        // From in front of it: it stands against a wall.
                        Shoot(camera, target, folder, "Prop-ClownDummy-Close", thing.position + thing.forward * 1.3f + Vector3.up * 1.75f, thing.position + Vector3.up * 1.7f);
                        count++;
                        toward = thing.forward;
                    }
                    Shoot(camera, target, folder, "Prop-" + thing.name, thing.position + toward * 2.2f + Vector3.up * 1.5f + Vector3.forward * 0.6f, thing.position + Vector3.up * 0.9f);
                    count++;
                }
            }
            Debug.Log("BatchShots: wrote " + count + " pictures to " + folder);
        }

        /// <summary>
        /// Twelve moments of each of his animations from the front and the side, on a bare floor, and what
        /// each clip measures. Usage: -executeMethod NightShift.EditorTools.BatchShots.Moves -shots "dir".
        /// </summary>
        public static void Moves()
        {
            const int Frames = 12;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-shots");
            string folder = at >= 0 ? args[at + 1] : "Shots";
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.48f, 0.47f);
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 5f;
            var ground = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.32f, 0.32f, 0.34f) };
            floor.GetComponent<Renderer>().sharedMaterial = ground;
            var camera = new GameObject("Shot").AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.13f);
            var target = new RenderTexture(360, 520, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            // The first frame in batch mode draws some materials wrong: throw it away. Before he is there,
            // for a skin drawn once before its animator has run keeps its limbs in the T.
            camera.Render();
            GameObject pockets = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PocketsSetup.PrefabPath));
            var animator = pockets.GetComponentInChildren<Animator>();
            var rig = pockets.GetComponent<PocketsRig>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            System.Reflection.MethodInfo hang = typeof(PocketsRig).GetMethod("HangMallet", Private);
            System.Reflection.FieldInfo loose = typeof(PocketsRig).GetField("malletLoose", Private);
            // What his Start would have remembered: nothing runs it here.
            typeof(PocketsRig).GetField("malletRest", Private).SetValue(rig, rig.Mallet.localRotation);
            foreach (string move in new[] { "idle", "walk", "run", "search", "attack", "listen", "smash", "roar", "laugh" })
            {
                string path = PocketsSetup.AnimFolder + "/pockets-" + move + ".fbx";
                AnimationClip clip = null;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip found && !found.name.StartsWith("__preview__"))
                    {
                        clip = found;
                    }
                }
                if (clip == null)
                {
                    Debug.Log("MOVE " + move + " missing");
                    continue;
                }
                Debug.Log("MOVE " + move + " length " + clip.length.ToString("F2") + " s, loop " + clip.isLooping + ", average speed " + clip.averageSpeed.ToString("F2") + ", apparent " + clip.apparentSpeed.ToString("F2") + ", human scale " + animator.humanScale.ToString("F2"));
                bool swings = move == "attack" || move == "smash";
                // Through his real controller, the way the game plays them (roar and laugh over a walk).
                bool shout = move == "roar" || move == "laugh";
                string state = move == "idle" || move == "walk" || move == "run" ? "Locomotion" : char.ToUpperInvariant(move[0]) + move.Substring(1);
                float seconds = move == "attack" ? Mathf.Min(clip.length, 2.5f) : clip.length;
                animator.Rebind();
                animator.SetFloat("Speed", move == "walk" || shout ? 1f : move == "run" ? 2f : 0f);
                for (int i = 0; i < Frames; i++)
                {
                    animator.Play(shout ? "Locomotion" : state, 0, shout ? (float)i / Frames : seconds * i / Frames / clip.length);
                    if (shout)
                    {
                        animator.Play(state, 1, seconds * i / Frames / clip.length);
                    }
                    animator.Update(0f);
                    loose.SetValue(rig, swings ? 0f : 1f);
                    hang.Invoke(rig, null);
                    Vector3 middle = new Vector3(hips.position.x, 1.05f, hips.position.z);
                    Shoot(camera, target, folder, move + "-" + i + "-front", middle + new Vector3(1.4f, 0.2f, 6.2f), middle);
                    Shoot(camera, target, folder, move + "-" + i + "-side", middle + new Vector3(6.2f, 0.2f, 0f), middle);
                }
            }
            Debug.Log("MOVES done");
        }

        /// <summary>The sculpt standing in the ring, with a flashlight in the viewer's hand, as a player meets him.</summary>
        private static int ShootPockets(Camera camera, RenderTexture target, string folder, bool dark)
        {
            GameObject prefab = PocketsSetup.Build();
            if (prefab == null)
            {
                return 0;
            }
            Rect ring = PizzeriaLayout.Area(Room.DiningHall);
            Vector3 at = new Vector3(ring.center.x, 0f, ring.center.y);
            GameObject pockets = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
            var animator = pockets.GetComponentInChildren<Animator>();
            void Pose(float speed, bool searching, float seconds)
            {
                animator.Rebind();
                animator.SetFloat("Speed", speed);
                animator.SetBool("Searching", searching);
                for (float t = 0f; t < seconds; t += 0.05f)
                {
                    animator.Update(0.05f);
                }
            }
            var torch = new GameObject("Flashlight").AddComponent<Light>();
            torch.type = LightType.Spot;
            torch.spotAngle = 55f;
            torch.innerSpotAngle = 22f;
            torch.range = 16f;
            torch.intensity = dark ? 9f : 3f;
            torch.color = new Color(1f, 0.95f, 0.85f);
            torch.shadows = LightShadows.Soft;
            void Take(string name, Vector3 eye, Vector3 look)
            {
                torch.transform.position = at + eye + new Vector3(0.22f, -0.18f, 0f);
                torch.transform.rotation = Quaternion.LookRotation(at + look - torch.transform.position);
                Shoot(camera, target, folder, name, at + eye, at + look);
            }
            Pose(0f, false, 1f);
            Take("Pockets-1", new Vector3(0f, 1.55f, 4.2f), new Vector3(0f, 1.15f, 0f));
            Take("Pockets-2", new Vector3(0.1f, 1.8f, 1.15f), new Vector3(0f, 1.92f, 0f));
            Pose(1f, false, 1.2f);
            Take("Pockets-3", new Vector3(1.8f, 1.5f, 2.6f), new Vector3(0f, 1.15f, 0f));
            Pose(2f, false, 0.9f);
            Take("Pockets-4", new Vector3(-1.6f, 1.5f, 2.8f), new Vector3(0f, 1.15f, 0f));
            return 4;
        }

        private static void Shoot(Camera camera, RenderTexture target, string folder, string name, Vector3 eye, Vector3 look)
        {
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(look - eye);
            camera.Render();
            RenderTexture.active = target;
            var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), picture.EncodeToJPG(70));
            RenderTexture.active = null;
        }
    }
}
