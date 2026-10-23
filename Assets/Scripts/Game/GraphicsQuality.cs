using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.Game
{
    public enum GraphicsLevel
    {
        Low,
        Medium,
        High,
        Ultra
    }

    /// <summary>
    /// The graphics quality the player picks in Settings (saved between sessions). The web build
    /// starts on Medium and the desktop build on High; a strong computer can go up to Ultra in
    /// the browser too. Each level sets the render resolution, anti-aliasing, shadow detail,
    /// which lamps cast shadows, ambient occlusion, reflections, the lamps' bounce light and light
    /// shafts, and texture resolution.
    /// </summary>
    public static class GraphicsQuality
    {
        private const string Key = "graphics_level";

        /// <summary>Lamps and effects that depend on the level, registered by the level builder.</summary>
        private static readonly List<(Light light, bool wantsShadow)> Lamps = new List<(Light, bool)>();
        private static readonly List<Light> BounceLights = new List<Light>();
        private static readonly List<Renderer> Shafts = new List<Renderer>();
        private static readonly List<ReflectionProbe> Probes = new List<ReflectionProbe>();
        private static ScriptableRendererFeature ambientOcclusion;
        private static Flashlight flashlight;
        private static Bloom bloom;
        private static MotionBlur motionBlur;
        private static ScreenSpaceLensFlare lensFlare;
        private static bool probesRendered;
        private static UniversalRenderPipelineAsset runtimePipeline;

        public static GraphicsLevel Level { get; private set; } = Default;

        private static GraphicsLevel Default => Application.platform == RuntimePlatform.WebGLPlayer ? GraphicsLevel.Medium : GraphicsLevel.High;

        public static string Name(GraphicsLevel level) => level.ToString().ToUpperInvariant();

        /// <summary>Reads the saved level (call once at start, before building the level).</summary>
        public static void Load(ScriptableRendererFeature occlusion)
        {
            ambientOcclusion = occlusion;
            int saved = PlayerPrefs.GetInt(Key, (int)Default);
            Level = (GraphicsLevel)Mathf.Clamp(saved, 0, (int)GraphicsLevel.Ultra);
            // "-quality Ultra" on the command line (captures and tests) overrides the saved choice.
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-quality");
            if (index >= 0 && index + 1 < args.Length && System.Enum.TryParse(args[index + 1], true, out GraphicsLevel forced))
            {
                Level = forced;
            }
            Lamps.Clear();
            BounceLights.Clear();
            Shafts.Clear();
            Probes.Clear();
            probesRendered = false;
        }

        public static void RegisterLamp(Light light, bool wantsShadow) => Lamps.Add((light, wantsShadow));
        public static void RegisterBounce(Light light) => BounceLights.Add(light);
        public static void RegisterShaft(Renderer shaft) => Shafts.Add(shaft);
        public static void RegisterProbe(ReflectionProbe probe) => Probes.Add(probe);
        public static void RegisterFlashlight(Flashlight light) => flashlight = light;
        public static void RegisterBloom(Bloom effect) => bloom = effect;
        public static void RegisterMotionBlur(MotionBlur effect) => motionBlur = effect;
        public static void RegisterLensFlare(ScreenSpaceLensFlare effect) => lensFlare = effect;

        /// <summary>Next level up (wraps around), saved and applied.</summary>
        public static void Cycle(int step)
        {
            int count = (int)GraphicsLevel.Ultra + 1;
            Set((GraphicsLevel)(((int)Level + step + count) % count));
        }

        public static void Set(GraphicsLevel level)
        {
            Level = level;
            PlayerPrefs.SetInt(Key, (int)level);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Applies the current level to the pipeline and everything registered.</summary>
        public static void Apply()
        {
            GraphicsLevel level = Level;
            bool low = level == GraphicsLevel.Low;
            bool high = level >= GraphicsLevel.High;
            bool ultra = level == GraphicsLevel.Ultra;

            // A copy of the project's pipeline asset, so playing in the editor never rewrites it.
            if (runtimePipeline == null && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset shared)
            {
                runtimePipeline = Object.Instantiate(shared);
                GraphicsSettings.defaultRenderPipeline = runtimePipeline;
                QualitySettings.renderPipeline = runtimePipeline;
            }
            if (runtimePipeline is UniversalRenderPipelineAsset pipeline)
            {
                pipeline.renderScale = low ? 0.75f : level == GraphicsLevel.Medium ? 0.9f : 1f;
                pipeline.msaaSampleCount = low ? 1 : level == GraphicsLevel.Medium ? 2 : 4;
                pipeline.additionalLightsShadowmapResolution = low ? 512 : level == GraphicsLevel.Medium ? 1024 : ultra ? 4096 : 2048;
                pipeline.maxAdditionalLightsCount = low ? 4 : level == GraphicsLevel.Medium ? 6 : 8;
                pipeline.shadowDistance = low ? 12f : high ? 30f : 20f;
            }
            QualitySettings.globalTextureMipmapLimit = low ? 1 : 0;
            QualitySettings.anisotropicFiltering = low ? AnisotropicFiltering.Disable : AnisotropicFiltering.ForceEnable;

            if (ambientOcclusion != null)
            {
                ambientOcclusion.SetActive(!low);
            }
            if (bloom != null)
            {
                bloom.highQualityFiltering.Override(high);
            }
            if (motionBlur != null)
            {
                motionBlur.active = high;
            }
            if (lensFlare != null)
            {
                lensFlare.active = high;
            }

            // Lamp shadows: none on Low, the main lamps from High, every lamp on Ultra.
            foreach ((Light light, bool wantsShadow) in Lamps)
            {
                if (light == null)
                {
                    continue;
                }
                bool shadow = ultra || (high && wantsShadow);
                light.shadows = shadow ? LightShadows.Soft : LightShadows.None;
            }
            foreach (Light bounce in BounceLights)
            {
                if (bounce != null)
                {
                    bounce.enabled = high;
                }
            }
            foreach (Renderer shaft in Shafts)
            {
                if (shaft != null)
                {
                    shaft.enabled = !low;
                }
            }
            foreach (ReflectionProbe probe in Probes)
            {
                if (probe != null)
                {
                    probe.enabled = high;
                    int resolution = ultra ? 256 : 128;
                    if (probe.resolution != resolution)
                    {
                        probe.resolution = resolution;
                        probesRendered = false;
                    }
                }
            }
            if (high && !probesRendered && Probes.Count > 0)
            {
                RenderProbes();
            }
            if (flashlight != null)
            {
                flashlight.SetQuality(level);
            }
        }

        /// <summary>Captures the reflections of every room once (the building does not move).</summary>
        public static void RenderProbes()
        {
            if (Level < GraphicsLevel.High)
            {
                return;
            }
            foreach (ReflectionProbe probe in Probes)
            {
                if (probe != null && probe.enabled)
                {
                    probe.RenderProbe();
                }
            }
            probesRendered = true;
        }
    }
}
