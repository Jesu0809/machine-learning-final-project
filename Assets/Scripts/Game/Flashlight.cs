using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The player's flashlight, made to read like a real torch in a dark building: a wide soft beam
    /// with a bright core, light bouncing off whatever it hits (so the walls around the spot are
    /// lit too), dust floating in the beam, and no blown-out white circle when you stand close to
    /// a wall (the beam dims as the surface gets near, the way your eyes adapt).
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        /// <summary>Intensity of the beam on a surface far enough away.</summary>
        public const float BeamIntensity = 40f;

        private const float BeamRange = 30f;
        private const float BounceStrength = 0.05f;

        private Light beam;
        private Light bounce;
        private ParticleSystem dust;
        private Transform view;
        private float distanceSmoothed = 10f;

        /// <summary>Light of the beam (switch it with <see cref="Light.enabled"/>).</summary>
        public Light Beam => beam;

        /// <summary>True for a moment while the flashlight sputters (fear, a bad contact).</summary>
        public bool Glitching { get; set; }

        /// <summary>Adds the flashlight to the player's camera.</summary>
        public static Flashlight Create(Transform camera)
        {
            var go = new GameObject("Flashlight");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = new Vector3(0.2f, -0.15f, 0.1f);
            var flashlight = go.AddComponent<Flashlight>();
            flashlight.view = camera;

            Light beam = go.AddComponent<Light>();
            beam.type = LightType.Spot;
            // Wide and soft: the cookie keeps a bright core in the middle of a broad halo.
            beam.spotAngle = 96f;
            beam.innerSpotAngle = 30f;
            beam.range = BeamRange;
            beam.intensity = BeamIntensity;
            beam.cookie = ProceduralTextures.FlashlightCookie();
            beam.color = new Color(1f, 0.94f, 0.82f);
            beam.shadows = LightShadows.Soft;
            beam.shadowStrength = 0.9f;
            beam.shadowNearPlane = 0.2f;
            flashlight.beam = beam;

            // The light the lit surface throws back around the room: no shadows, short range.
            var bounceObject = new GameObject("FlashlightBounce");
            bounceObject.transform.SetParent(go.transform, false);
            Light bounce = bounceObject.AddComponent<Light>();
            bounce.type = LightType.Point;
            bounce.shadows = LightShadows.None;
            bounce.color = new Color(1f, 0.9f, 0.78f);
            bounce.intensity = 0f;
            flashlight.bounce = bounce;

            flashlight.dust = BeamDust(go.transform);
            return flashlight;
        }

        private void LateUpdate()
        {
            bool on = beam.enabled;
            bounce.enabled = on;
            if (dust.isPlaying != on)
            {
                if (on)
                {
                    dust.Play();
                }
                else
                {
                    dust.Stop();
                }
            }
            if (!on)
            {
                return;
            }

            // What the beam hits, and how far.
            Vector3 origin = view.position;
            Vector3 forward = view.forward;
            float distance = BeamRange;
            Vector3 point = origin + forward * BeamRange;
            Vector3 normal = -forward;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, BeamRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;
                point = hit.point;
                normal = hit.normal;
            }
            distanceSmoothed = Mathf.Lerp(distanceSmoothed, distance, 1f - Mathf.Exp(-Time.deltaTime * 12f));

            // Close to a surface the beam dims (no white disc on the wall in front of your face).
            float near = Mathf.SmoothStep(0.3f, 1f, Mathf.Clamp01(distanceSmoothed / 4f));
            float glitch = Glitching ? 0.03f : 1f;
            beam.intensity = BeamIntensity * near * glitch;

            // The bounce sits just off the lit surface; it fades with distance like the spot does.
            float falloff = 1f / (1f + distanceSmoothed * distanceSmoothed * 0.04f);
            bounce.transform.position = point + normal * 0.6f;
            bounce.range = 2.5f + distanceSmoothed * 0.25f;
            bounce.intensity = BeamIntensity * BounceStrength * near * falloff * glitch;
        }

        /// <summary>Fine dust drifting in the first meters of the beam, catching the light.</summary>
        private static ParticleSystem BeamDust(Transform parent)
        {
            var go = new GameObject("BeamDust");
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = 3.5f;
            main.startSpeed = 0.02f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.008f);
            main.startColor = new Color(1f, 0.93f, 0.8f, 0.35f);
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 30f;
            // A cone along the beam, from just in front of the lens to a few meters out.
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;
            shape.length = 3.5f;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            // Start a little ahead of the lens: motes right in front of the eye look like big blobs.
            shape.position = new Vector3(0f, 0f, 0.9f);
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.4f;
            // Fade in and out so motes never pop.
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = BuildKit.Additive("beamDust", ProceduralTextures.Disc(16), new Color(1f, 0.92f, 0.78f, 0.35f));
            return particles;
        }
    }
}
