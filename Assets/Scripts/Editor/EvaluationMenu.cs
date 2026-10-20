using System.Collections.Generic;
using System.IO;
using NightShift.Evaluation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// "Night Shift/Build Evaluation": an Evaluation scene with the trained model, built as a
    /// standalone player that writes training/evaluation/results.json.
    /// </summary>
    public static class EvaluationMenu
    {
        public const string ScenePath = "Assets/Scenes/Evaluation.unity";

        [MenuItem("Night Shift/Create Evaluation Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            GameSceneMenu.UpdateGameAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Loaded after NewScene, which unloads assets that no scene references.
            var assets = AssetDatabase.LoadAssetAtPath<NightShift.Game.GameAssets>(GameSceneMenu.AssetsPath);
            new GameObject("Main Camera", typeof(Camera)).tag = "MainCamera";
            var runner = new GameObject("EvaluationRunner").AddComponent<HuntEvaluationRunner>();
            runner.Configure(assets.FreddyModel, 400);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, false));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("Evaluation scene created with model " + (assets.FreddyModel != null ? assets.FreddyModel.name : "none"));
        }

        [MenuItem("Night Shift/Build Evaluation")]
        public static void Build()
        {
            CreateScene();
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string folder = HuntTrainingMenu.BuildsFolder();
            string output = Path.Combine(folder, windows ? "Evaluation/Evaluation.exe" : "Evaluation.app");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = windows ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            Debug.Log("Evaluation build: " + report.summary.result + " at " + output);
        }
    }
}
