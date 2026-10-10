using System.Collections.Generic;
using NightShift.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>Editor menu that creates the Night scene, so nobody has to build it by hand.</summary>
    public static class NightSceneMenu
    {
        public const string ScenePath = "Assets/Scenes/Night.unity";

        [MenuItem("Night Shift/Create Night Scene")]
        public static void CreateNightScene()
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
            camera.orthographic = true;

            var night = new GameObject("Night");
            night.AddComponent<NightController>();
            night.AddComponent<NightBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettingsFirst(ScenePath);
            Debug.Log("Night scene created at " + ScenePath + ". Press Play to start.");
        }

        private static void AddToBuildSettingsFirst(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
