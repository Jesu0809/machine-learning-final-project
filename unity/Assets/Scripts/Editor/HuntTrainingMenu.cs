using System.Collections.Generic;
using System.IO;
using NightShift.Hunt.Training;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// Editor menu for training Freddy: creates the HuntTraining scene and builds it as a
    /// standalone player for mlagents-learn (--env=... --no-graphics).
    /// </summary>
    public static class HuntTrainingMenu
    {
        public const string ScenePath = "Assets/Scenes/HuntTraining.unity";

        [MenuItem("Night Shift/Create Hunt Training Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            new GameObject("HuntTrainingArena").AddComponent<HuntTrainingArena>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("Hunt training scene created at " + ScenePath);
        }

        /// <summary>
        /// Where builds go: training/builds next to the Unity project in the full project repository,
        /// or Builds/ inside the project when it is checked out on its own (both git-ignored).
        /// </summary>
        public static string BuildsFolder()
        {
            // Inside the main repository (unity/ next to training/config) builds go to training/builds;
            // in the standalone game repository they stay in its own Builds folder.
            string training = Path.GetFullPath(Path.Combine(Application.dataPath, "../../training"));
            return Directory.Exists(Path.Combine(training, "config"))
                ? Path.Combine(training, "builds")
                : Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
        }

        [MenuItem("Night Shift/Build Hunt Training")]
        public static void Build()
        {
            // Always a fresh scene: an old one keeps the settings it was saved with (an old exit
            // work count, for example) instead of the current defaults of HuntSettings.
            CreateScene();

            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string folder = HuntTrainingMenu.BuildsFolder();
            string output = Path.Combine(folder, windows ? "HuntTraining/HuntTraining.exe" : "HuntTraining.app");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = windows ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Hunt training built at " + output);
            }
            else
            {
                Debug.LogError("Hunt training build failed: " + report.summary.result);
            }
        }
    }
}
