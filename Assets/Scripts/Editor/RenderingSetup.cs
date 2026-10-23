using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NightShift.EditorTools
{
    /// <summary>
    /// Render features the game switches by graphics quality (see GraphicsQuality.cs): screen-space
    /// ambient occlusion on the renderer, reflection probes with box projection and blending, and
    /// soft shadows in the pipeline asset. Safe to run again: it only adds what is missing.
    /// </summary>
    public static class RenderingSetup
    {
        /// <summary>Configures the pipeline and returns the SSAO feature of the game's renderer.</summary>
        public static ScriptableRendererFeature Apply()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(GameSceneMenu.PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(GameSceneMenu.RendererPath);
            if (pipeline == null || renderer == null)
            {
                Debug.LogWarning("Render pipeline assets missing: run Night Shift > Create Game Scene first.");
                return null;
            }

            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_ReflectionProbeBlending").boolValue = true;
            settings.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.FindProperty("m_SupportsHDR").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            ScriptableRendererFeature ambientOcclusion = null;
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                {
                    ambientOcclusion = feature;
                }
            }
            if (ambientOcclusion == null)
            {
                ambientOcclusion = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ambientOcclusion.name = "AmbientOcclusion";
                AssetDatabase.AddObjectToAsset(ambientOcclusion, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ambientOcclusion, out string _, out long localId);
                var data = new SerializedObject(renderer);
                SerializedProperty features = data.FindProperty("m_RendererFeatures");
                SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
                features.InsertArrayElementAtIndex(features.arraySize);
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ambientOcclusion;
                map.InsertArrayElementAtIndex(map.arraySize);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            // Dark rooms: the occlusion also darkens direct light a little, or it would only touch
            // the (almost black) ambient light and nobody would see it.
            var ssao = new SerializedObject(ambientOcclusion);
            ssao.FindProperty("m_Settings.Intensity").floatValue = 3.5f;
            ssao.FindProperty("m_Settings.Radius").floatValue = 0.6f;
            ssao.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.85f;
            ssao.FindProperty("m_Settings.Falloff").floatValue = 40f;
            ssao.FindProperty("m_Settings.Downsample").boolValue = false;
            ssao.FindProperty("m_Settings.AfterOpaque").boolValue = false;
            ssao.ApplyModifiedPropertiesWithoutUndo();
            ambientOcclusion.SetActive(true);
            EditorUtility.SetDirty(ambientOcclusion);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            return ambientOcclusion;
        }
    }
}
