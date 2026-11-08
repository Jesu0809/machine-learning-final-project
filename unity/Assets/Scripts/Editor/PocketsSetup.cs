using System.IO;
using NightShift.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>Import settings of his Mixamo animations (Assets/Art/Pockets/Anim/pockets-*.fbx; only their motion is used).</summary>
    public class PocketsImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(PocketsSetup.AnimFolder + "/"))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            // The skeleton Mixamo gave the body, so every clip reads the same.
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(PocketsSetup.RiggedPath);
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(PocketsSetup.AnimFolder + "/"))
            {
                return;
            }
            const float AttackLastFrame = 38f;
            string move = Path.GetFileNameWithoutExtension(assetPath);
            bool once = move == "pockets-attack" || move == "pockets-smash" || move == "pockets-roar" || move == "pockets-laugh";
            bool travels = move == "pockets-walk" || move == "pockets-run";
            bool attack = move == "pockets-attack";
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = !once;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                // The walk and the run keep their ground speed as root motion (never applied): it tells
                // how fast the steps should play. Everything else stays in the pose.
                clip.lockRootPositionXZ = !travels;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                // The moves that stay on the spot are pinned by their center of mass: the attack's clip alone
                // would carry him three meters forward, through the player and the walls.
                clip.keepOriginalPositionXZ = travels;
                if (attack)
                {
                    // Only up to where he falls on his knees: then the clip dives to the floor, and the
                    // jumpscare's camera, which stays in front of his face, would end up over his head.
                    clip.lastFrame = Mathf.Min(clip.lastFrame, AttackLastFrame);
                }
            }
            importer.clipAnimations = clips;
        }
    }

    /// <summary>
    /// Sets up Pockets from Assets/Art/Pockets: the sculpt's textures (tools/sculpt_pockets.py), his
    /// material, and, once Mixamo has rigged the body (pockets_rigged.fbx), the prefab the game uses,
    /// with the jaw, the key and the mallet attached to its bones.
    /// </summary>
    public static class PocketsSetup
    {
        public const string Folder = "Assets/Art/Pockets";
        public const string RiggedPath = Folder + "/pockets_rigged.fbx";
        public const string PrefabPath = Folder + "/Pockets.prefab";
        public const string MaterialPath = Folder + "/Pockets.mat";
        public const string AnimFolder = Folder + "/Anim";
        public const string ControllerPath = Folder + "/Pockets.controller";
        private const float SculptHeight = 2.18f;

        /// <summary>
        /// Where each loose part's origin sits on him (pivots.txt, with x negated: Unity mirrors x when
        /// it imports a right-handed file, which puts the machine arm on his left).
        /// </summary>
        public static readonly Vector3 Hinge = new Vector3(0f, 1.8714f, -0.026f);
        public static readonly Vector3 KeyAt = new Vector3(0f, 1.39f, -0.16f);
        public static readonly Vector3 GripAt = new Vector3(-0.585f, 0.985f, 0.05f);

        public static Material ImportMaterial()
        {
            Texture(Folder + "/pockets_albedo.jpg", TextureImporterType.Default, true);
            // His folds and stitches survive at 1K in the browser, where he is mostly seen in the dark.
            Texture(Folder + "/pockets_normal.jpg", TextureImporterType.NormalMap, false, 1024);
            Texture(Folder + "/pockets_mask.png", TextureImporterType.Default, false, 1024);
            Texture(Folder + "/pockets_emission.jpg", TextureImporterType.Default, true, 1024);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/pockets_albedo.jpg"));
            // The sculpt was painted to be seen in a brighter viewer than the game's exposure.
            material.SetColor("_BaseColor", new Color(0.8f, 0.78f, 0.78f));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/pockets_normal.jpg"));
            material.EnableKeyword("_NORMALMAP");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/pockets_mask.png");
            material.SetTexture("_MetallicGlossMap", mask);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness", 1f);
            material.SetTexture("_OcclusionMap", mask);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/pockets_emission.jpg"));
            material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Full size on the desktop; <paramref name="webSize"/> and crunched in the web build.</summary>
        private static void Texture(string path, TextureImporterType type, bool srgb, int webSize = 2048)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = 4096;
                // The sculpt's atlases are not square to the pixel (4096 x 4038): a side that is not a
                // multiple of 4 cannot be block-compressed, and Unity stores it uncompressed.
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                WebImport.Crunch(importer, webSize, 70);
                importer.SaveAndReimport();
            }
        }

        /// <summary>The prefab of the rigged Pockets. Null without the file from Mixamo.</summary>
        [MenuItem("Night Shift/Setup Pockets")]
        public static GameObject Build()
        {
            if (!File.Exists(RiggedPath))
            {
                return null;
            }
            Material material = ImportMaterial();
            if (AssetImporter.GetAtPath(RiggedPath) is ModelImporter model && model.animationType != ModelImporterAnimationType.Human)
            {
                model.animationType = ModelImporterAnimationType.Human;
                model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                model.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Pockets";
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }
            var animator = instance.GetComponent<Animator>();
            RuntimeAnimatorController moves = BuildController(out float walkSpeed, out float runSpeed);
            if (moves == null)
            {
                Debug.LogError("Pockets: his five required animations are not in " + AnimFolder);
            }
            animator.runtimeAnimatorController = moves;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // The sculpt's height decides the scale, whatever units came back from Mixamo.
            (float low, float high) = Surface(instance);
            if (high - low > 0.01f)
            {
                instance.transform.localScale *= SculptHeight / (high - low);
            }
            (low, high) = Surface(instance);

            // Mixamo may have moved or rescaled him: report it, for the parts are placed in sculpt meters.
            Bounds placed = new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                placed.Encapsulate(renderer.bounds);
            }
            Debug.Log("POCKETS bounds " + placed.min.ToString("F3") + " to " + placed.max.ToString("F3") + " root scale " + instance.transform.localScale.ToString("F4") + " human " + animator.isHuman);
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot })
            {
                Transform at = animator.GetBoneTransform(bone);
                Debug.Log("POCKETS bone " + bone + " at " + (at != null ? at.position.ToString("F3") : "missing"));
            }

            Debug.Log("POCKETS surface from y " + low.ToString("F3") + " to " + high.ToString("F3"));
            float size = (high - low) / SculptHeight;
            Vector3 Place(Vector3 sculpted) => new Vector3(sculpted.x * size, low + sculpted.y * size, sculpted.z * size);

            var rig = instance.AddComponent<PocketsRig>();
            rig.Jaw = Attach("pockets_jaw", animator.GetBoneTransform(HumanBodyBones.Head), Place(Hinge), Quaternion.identity, size, material);
            rig.Key = Attach("pockets_key", animator.GetBoneTransform(HumanBodyBones.Spine), Place(KeyAt), Quaternion.identity, size, material);
            // Mixamo stands him in a T, so the mallet is hung along the machine arm's fingers: when the arm
            // falls in the animations, it hangs from the hand with its head by the floor.
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform finger = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            Vector3 along = finger != null ? (finger.position - hand.position).normalized : Vector3.left;
            rig.WalkClipSpeed = walkSpeed;
            rig.RunClipSpeed = runSpeed;
            rig.Mallet = Attach("pockets_mallet", hand, hand.position + along * 0.05f + Vector3.forward * 0.03f, Quaternion.FromToRotation(Vector3.down, along), size, material);
            var core = new GameObject("Core").transform;
            core.position = Place(new Vector3(0f, 1.4f, 0.112f));
            core.SetParent(animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine), true);
            rig.Core = core;

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Debug.Log("Pockets prefab ready at " + PrefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>
        /// His own moves, once the five he cannot do without are in Anim/: idle, walk, run, search and
        /// attack. Listen, smash, roar and laugh are used when they are there. Null until then.
        /// </summary>
        private static AnimatorController BuildController(out float walkSpeed, out float runSpeed)
        {
            walkSpeed = runSpeed = 0f;
            foreach (string required in new[] { "idle", "walk", "run", "search", "attack" })
            {
                if (!File.Exists(AnimFolder + "/pockets-" + required + ".fbx"))
                {
                    return null;
                }
            }
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "StepScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter("Searching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            AnimationClip walk = Move("walk");
            AnimationClip run = Move("run");
            tree.AddChild(Move("idle"), 0f);
            tree.AddChild(walk, 1f);
            tree.AddChild(run, 2f);
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = "StepScale";
            machine.defaultState = locomotion;
            walkSpeed = new Vector2(walk.averageSpeed.x, walk.averageSpeed.z).magnitude;
            runSpeed = new Vector2(run.averageSpeed.x, run.averageSpeed.z).magnitude;
            Debug.Log("POCKETS clips cover " + walkSpeed.ToString("F2") + " walking and " + runSpeed.ToString("F2") + " running");

            AnimatorState search = machine.AddState("Search");
            search.motion = Move("search");
            Link(locomotion, search, "Searching", true, 0.2f);
            Link(search, locomotion, "Searching", false, 0.2f);

            AnimatorState attack = machine.AddState("Attack");
            attack.motion = Move("attack");
            // Its 1.3 s last all of the jumpscare's close-up (HuntDirector.Jumpscare: 0.4 s + 1.5 s).
            attack.speed = 0.7f;
            AnimatorStateTransition toAttack = machine.AddAnyStateTransition(attack);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;

            if (Has("listen"))
            {
                controller.AddParameter("Listening", AnimatorControllerParameterType.Bool);
                AnimatorState listen = machine.AddState("Listen");
                listen.motion = Move("listen");
                Link(locomotion, listen, "Listening", true, 0.3f);
                Link(listen, locomotion, "Listening", false, 0.3f);
            }
            if (Has("smash"))
            {
                controller.AddParameter("Smash", AnimatorControllerParameterType.Trigger);
                AnimatorState smash = machine.AddState("Smash");
                smash.motion = Move("smash");
                // Lands 0.4 s after it starts (FreddyBody.SwingLeadSeconds): the clip's blow is at 0.7 s.
                smash.speed = 1.75f;
                AnimatorStateTransition toSmash = machine.AddAnyStateTransition(smash);
                toSmash.AddCondition(AnimatorConditionMode.If, 0f, "Smash");
                toSmash.hasExitTime = false;
                toSmash.duration = 0.1f;
                AnimatorStateTransition back = smash.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.15f;
            }
            // The roar and the laugh take the head, the chest and the arms; the legs keep walking.
            if (Has("roar") || Has("laugh"))
            {
                var upper = new AvatarMask { name = "Upper" };
                for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                {
                    var bodyPart = (AvatarMaskBodyPart)part;
                    bool legs = bodyPart == AvatarMaskBodyPart.Root || bodyPart == AvatarMaskBodyPart.LeftLeg || bodyPart == AvatarMaskBodyPart.RightLeg || bodyPart == AvatarMaskBodyPart.LeftFootIK || bodyPart == AvatarMaskBodyPart.RightFootIK;
                    upper.SetHumanoidBodyPartActive(bodyPart, !legs);
                }
                AssetDatabase.AddObjectToAsset(upper, controller);
                controller.AddLayer("Upper");
                AnimatorControllerLayer[] layers = controller.layers;
                layers[1].avatarMask = upper;
                layers[1].defaultWeight = 1f;
                controller.layers = layers;
                AnimatorStateMachine top = controller.layers[1].stateMachine;
                AnimatorState nothing = top.AddState("Nothing");
                // Writes nothing, so the walk underneath shows through.
                nothing.writeDefaultValues = false;
                top.defaultState = nothing;
                foreach (string shout in new[] { "Roar", "Laugh" })
                {
                    if (!Has(shout.ToLowerInvariant()))
                    {
                        continue;
                    }
                    controller.AddParameter(shout, AnimatorControllerParameterType.Trigger);
                    AnimatorState state = top.AddState(shout);
                    AnimationClip motion = Move(shout.ToLowerInvariant());
                    state.motion = motion;
                    state.writeDefaultValues = false;
                    AnimatorStateTransition into = nothing.AddTransition(state);
                    into.AddCondition(AnimatorConditionMode.If, 0f, shout);
                    into.hasExitTime = false;
                    into.duration = 0.12f;
                    AnimatorStateTransition done = state.AddTransition(nothing);
                    done.hasExitTime = true;
                    // Short and sharp, however long the clip is.
                    done.exitTime = Mathf.Min(0.88f, 4f / motion.length);
                    done.duration = 0.2f;
                }
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static bool Has(string move) => File.Exists(AnimFolder + "/pockets-" + move + ".fbx");

        private static void Link(AnimatorState from, AnimatorState to, string parameter, bool when, float seconds)
        {
            AnimatorStateTransition link = from.AddTransition(to);
            link.AddCondition(when ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            link.hasExitTime = false;
            link.duration = seconds;
        }

        private static AnimationClip Move(string move)
        {
            string path = AnimFolder + "/pockets-" + move + ".fbx";
            AssetDatabase.ImportAsset(path);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            Debug.LogError("POCKETS no animation clip in " + path);
            return null;
        }

        /// <summary>The light version of the sculpt, for the dummies and spare heads of the set.</summary>
        public static void SetUpProps(GameAssets assets)
        {
            assets.PropHeadMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/prop_head.obj");
            assets.PropDummyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/prop_dummy.obj");
            Texture(Folder + "/prop_albedo.jpg", TextureImporterType.Default, true, 1024);
            Texture(Folder + "/prop_normal.jpg", TextureImporterType.NormalMap, false, 1024);
            Texture(Folder + "/prop_mask.png", TextureImporterType.Default, false, 1024);
            string path = Folder + "/Prop.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/prop_albedo.jpg"));
            material.SetColor("_BaseColor", new Color(0.8f, 0.78f, 0.78f));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/prop_normal.jpg"));
            material.EnableKeyword("_NORMALMAP");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/prop_mask.png");
            material.SetTexture("_MetallicGlossMap", mask);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness", 1f);
            material.SetTexture("_OcclusionMap", mask);
            material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
            assets.PropMaterial = material;
        }

        /// <summary>The lowest and highest points of his skin as it stands (renderer bounds of a skinned mesh are not to be trusted).</summary>
        private static (float low, float high) Surface(GameObject figure)
        {
            var baked = new Mesh();
            float low = float.MaxValue, high = float.MinValue;
            foreach (SkinnedMeshRenderer skin in figure.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(baked);
                Matrix4x4 place = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                foreach (Vector3 vertex in baked.vertices)
                {
                    float y = place.MultiplyPoint3x4(vertex).y;
                    low = Mathf.Min(low, y);
                    high = Mathf.Max(high, y);
                }
            }
            Object.DestroyImmediate(baked);
            return (low, high);
        }

        /// <summary>A loose part hung on a bone, at this place and turn in the world, at this size.</summary>
        private static Transform Attach(string name, Transform bone, Vector3 at, Quaternion turn, float size, Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".obj");
            if (model == null || bone == null)
            {
                Debug.LogError("Pockets: missing " + name + " or its bone");
                return null;
            }
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(part, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            part.name = name;
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }
            part.transform.position = at;
            part.transform.rotation = turn;
            part.transform.localScale = Vector3.one * size;
            part.transform.SetParent(bone, true);
            return part.transform;
        }
    }
}
