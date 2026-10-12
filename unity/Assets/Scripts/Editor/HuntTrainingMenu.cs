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

        [MenuItem("Night Shift/Build Hunt Training")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
            {
                CreateScene();
            }

            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../training/builds"));
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
