using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The outside of the pizzeria, seen in the intro cinematic: a rainy street, the parking lot,
    /// a brick front with neon signs, street lights, a far skyline and the guard's car. The
    /// buildings around are simple boxes with lit windows, so the street never ends in a void.
    /// Lights, rain and fog are only switched on while the camera is outside (<see cref="SetOutdoors"/>).
    /// </summary>
    public class Exterior : MonoBehaviour
    {
        // The front of the building (see PizzeriaLayout): the stage and halls face the street at
        // z = -19, the exit lobby (the entrance) is set back at z = -17.
        private const float FrontZ = -19.35f;
        private const float LobbyFrontZ = -17.35f;
        private const float FrontHeight = 6.6f;
        private const float WestX = -21.75f;
        private const float EastX = 19.75f;
        private const float LobbyWestX = 8f;
        private const float SidewalkZ = -21.6f;
        public const float RoadZ = -29f;

        private static readonly Color NightSky = new Color(0.018f, 0.022f, 0.036f);
        private static readonly Color NightFog = new Color(0.02f, 0.023f, 0.034f);

        private readonly List<Light> lights = new List<Light>();
        private Material asphalt;
        private Material sidewalk;
        private readonly List<Light> signLights = new List<Light>();
        private Renderer mainSign;
        private Light moon;
        private ParticleSystem rain;
        private Transform rainAnchor;
        private bool outdoors;
        private float nextFlicker;
        private float flickerUntil;
        private float lightningUntil;

        // What the night looks like indoors, restored when the camera goes back in.
        private Color savedFog;
        private float savedFogDensity;
        private float savedReflections;
        private Color savedAmbient;
        private float savedFar;
        private CameraClearFlags savedClear;
        private Color savedBackground;
        private Camera savedCamera;

        public Car Car { get; private set; }

        /// <summary>Where the car parks (facing north) and the point in front of the entrance.</summary>
        public static readonly Vector3 ParkingSpot = new Vector3(3f, 0f, -24.5f);
        public static readonly Vector3 Entrance = new Vector3(PizzeriaLayout.ExitDoor.x, 0f, LobbyFrontZ - 0.9f);

        public static Exterior Build(Transform parent, GameAssets assets)
        {
            var exterior = new GameObject("Exterior").AddComponent<Exterior>();
            exterior.transform.SetParent(parent, false);
            Transform root = exterior.transform;

            exterior.BuildGround(root);
            exterior.BuildFront(root);
            exterior.BuildSigns(root, assets);
            exterior.BuildStreetLights(root);
            exterior.BuildSkyline(root);
            exterior.Car = Car.Create(root);
            exterior.Car.Place(ParkingSpot, 0f);
            exterior.BuildRain(root);
            exterior.BuildMoon(root);
            exterior.ApplyLights(false);
            return exterior;
        }

        // ------------------------------------------------------------------ ground

        private void BuildGround(Transform root)
        {
            asphalt = BuildKit.Surface("asphaltWet", ProceduralTextures.Asphalt, 0.72f);
            sidewalk = BuildKit.Surface("sidewalk", () => ProceduralTextures.Tiles(new Color(0.3f, 0.3f, 0.29f), new Color(0.28f, 0.28f, 0.27f), 4, false, 0.5f), 0.55f);
            Material concrete = sidewalk;
            Material paint = BuildKit.Plain("roadPaint", new Color(0.75f, 0.74f, 0.7f), 0.5f);
            Material yellow = BuildKit.Plain("roadYellow", new Color(0.8f, 0.6f, 0.1f), 0.5f);

            // The ground sits just under the floors of the building, so it never shows inside. Near the
            // pizzeria it is cut in tiles: URP lights at most 8 lights per object, and one huge plane
            // would spend them on the lamps inside the building instead of the street lights.
            BuildKit.Box("FarGround", root, new Vector3(0f, -0.09f, -20f), new Vector3(400f, 0.1f, 400f), asphalt, 6f, false);
            const float tile = 10f;
            for (float x = -60f; x < 60f; x += tile)
            {
                for (float z = -70f; z < SidewalkZ; z += tile)
                {
                    float depth = Mathf.Min(tile, SidewalkZ - z);
                    BuildKit.Box("Ground", root, new Vector3(x + tile * 0.5f, -0.07f, z + depth * 0.5f), new Vector3(tile, 0.1f, depth), asphalt, 6f, false);
                }
            }

            // Sidewalk along the front, in pieces (same reason), set back at the entrance.
            for (float x = WestX - 1f; x < EastX + 1f; x += 6f)
            {
                float width = Mathf.Min(6f, EastX + 1f - x);
                float back = x + width * 0.5f < LobbyWestX ? FrontZ : LobbyFrontZ;
                BuildKit.Box("Sidewalk", root, new Vector3(x + width * 0.5f, 0.05f, (SidewalkZ + back) * 0.5f - 0.1f),
                             new Vector3(width, 0.14f, back - SidewalkZ - 0.1f), concrete, 2f, false);
            }

            // Parking lot lines, and the road with its center line.
            for (float x = -10.6f; x <= 18f; x += 2.8f)
            {
                BuildKit.Box("ParkingLine", root, new Vector3(x, -0.015f, -24.2f), new Vector3(0.12f, 0.01f, 4.6f), paint, 1f, false);
            }
            BuildKit.Box("RoadEdgeNorth", root, new Vector3(0f, -0.015f, RoadZ + 3.9f), new Vector3(360f, 0.01f, 0.15f), paint, 1f, false);
            BuildKit.Box("RoadEdgeSouth", root, new Vector3(0f, -0.015f, RoadZ - 4.1f), new Vector3(360f, 0.01f, 0.15f), paint, 1f, false);
            for (float x = -170f; x < 170f; x += 6f)
            {
                BuildKit.Box("CenterLine", root, new Vector3(x, -0.015f, RoadZ - 0.1f), new Vector3(3f, 0.01f, 0.14f), yellow, 1f, false);
            }
        }

        // ------------------------------------------------------------------ the building's front

        private void BuildFront(Transform root)
        {
            Material brick = BuildKit.Surface("bricks", () => ProceduralTextures.Bricks(new Color(0.42f, 0.17f, 0.12f), new Color(0.32f, 0.3f, 0.27f)), 0.25f);
            Material trim = BuildKit.Surface("trim", () => ProceduralTextures.Concrete(new Color(0.5f, 0.48f, 0.44f)), 0.3f);
            Material roof = BuildKit.Plain("roof", new Color(0.04f, 0.04f, 0.045f), 0.6f);
            Material glass = BuildKit.Plain("darkGlass", new Color(0.02f, 0.025f, 0.03f), 0.96f, 0.2f);
            Material boards = BuildKit.Surface("boards", () => ProceduralTextures.Planks(new Color(0.3f, 0.22f, 0.14f)), 0.2f);
            Material steel = BuildKit.Plain("frameSteel", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.7f);
            float half = PizzeriaLayout.DoorWidth * 0.5f + 0.1f;
            float doorX = PizzeriaLayout.ExitDoor.x;
            float doorTop = PizzeriaLayout.DoorHeight + 0.2f;

            // Walls: the long front, the entrance front (with its door opening), the step between them, and the sides.
            Wall(root, "Front", WestX, LobbyWestX, FrontZ, brick);
            Wall(root, "EntranceLeft", LobbyWestX, doorX - half, LobbyFrontZ, brick);
            Wall(root, "EntranceRight", doorX + half, EastX, LobbyFrontZ, brick);
            BuildKit.Box("EntranceLintel", root, new Vector3(doorX, (doorTop + FrontHeight) * 0.5f, LobbyFrontZ), new Vector3(half * 2f, FrontHeight - doorTop, 0.3f), brick, 2f);
            BuildKit.Box("Step", root, new Vector3(LobbyWestX, FrontHeight * 0.5f, (FrontZ + LobbyFrontZ) * 0.5f), new Vector3(0.3f, FrontHeight, LobbyFrontZ - FrontZ + 0.3f), brick, 2f);
            BuildKit.Box("SideWest", root, new Vector3(WestX, FrontHeight * 0.5f, -1.5f), new Vector3(0.3f, FrontHeight, 36f), brick, 2f);
            BuildKit.Box("SideEast", root, new Vector3(EastX, FrontHeight * 0.5f, -0.5f), new Vector3(0.3f, FrontHeight, 34f), brick, 2f);
            BuildKit.Box("Roof", root, new Vector3((WestX + EastX) * 0.5f, FrontHeight - 0.3f, -1.5f), new Vector3(EastX - WestX, 0.2f, 36f), roof, 4f, false);

            // Cornice along the top, and a base course.
            BuildKit.Box("Cornice", root, new Vector3((WestX + LobbyWestX) * 0.5f, FrontHeight + 0.15f, FrontZ - 0.1f), new Vector3(LobbyWestX - WestX + 0.6f, 0.35f, 0.6f), trim, 2f, false);
            BuildKit.Box("CorniceEntrance", root, new Vector3((LobbyWestX + EastX) * 0.5f, FrontHeight + 0.15f, LobbyFrontZ - 0.1f), new Vector3(EastX - LobbyWestX + 0.6f, 0.35f, 0.6f), trim, 2f, false);
            BuildKit.Box("Base", root, new Vector3((WestX + LobbyWestX) * 0.5f, 0.45f, FrontZ - 0.18f), new Vector3(LobbyWestX - WestX, 0.6f, 0.08f), trim, 2f, false);

            // Big windows of the old dining room: dark glass, some boarded up.
            float[] windows = { -17f, -11.5f, -6f, 3.5f };
            for (int i = 0; i < windows.Length; i++)
            {
                var center = new Vector3(windows[i], 2.15f, FrontZ - 0.17f);
                BuildKit.Box("WindowFrame", root, center, new Vector3(3.4f, 2.1f, 0.05f), steel, 1f, false);
                BuildKit.Box("Window", root, center + new Vector3(0f, 0f, -0.02f), new Vector3(3.2f, 1.9f, 0.04f), glass, 1f, false);
                if (i % 2 == 1)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        var plank = BuildKit.Box("Board", root, center + new Vector3(0f, -0.6f + b * 0.6f, -0.06f), new Vector3(3.6f, 0.28f, 0.05f), boards, 1f, false);
                        plank.transform.localRotation = Quaternion.Euler(0f, 0f, (b - 1) * 7f + (i - 2) * 2f);
                    }
                }
                BuildKit.Box("Sill", root, center + new Vector3(0f, -1.1f, -0.1f), new Vector3(3.6f, 0.1f, 0.25f), trim, 1f, false);
            }

            // The entrance: a steel frame and a striped awning.
            BuildKit.Box("DoorFrameLeft", root, new Vector3(doorX - half, doorTop * 0.5f, LobbyFrontZ - 0.18f), new Vector3(0.14f, doorTop, 0.12f), steel, 1f, false);
            BuildKit.Box("DoorFrameRight", root, new Vector3(doorX + half, doorTop * 0.5f, LobbyFrontZ - 0.18f), new Vector3(0.14f, doorTop, 0.12f), steel, 1f, false);
            BuildKit.Box("DoorFrameTop", root, new Vector3(doorX, doorTop, LobbyFrontZ - 0.18f), new Vector3(half * 2f + 0.14f, 0.14f, 0.12f), steel, 1f, false);
            Material awning = BuildKit.Surface("awning", () => ProceduralTextures.Wallpaper(new Color(0.6f, 0.06f, 0.06f), new Color(0.85f, 0.82f, 0.75f)), 0.35f);
            var cover = BuildKit.Box("Awning", root, new Vector3(doorX, doorTop + 0.55f, LobbyFrontZ - 0.85f), new Vector3(4.2f, 0.06f, 1.6f), awning, 1.5f, false);
            cover.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            BuildKit.Box("AwningValance", root, new Vector3(doorX, doorTop + 0.15f, LobbyFrontZ - 1.6f), new Vector3(4.2f, 0.3f, 0.04f), awning, 1.5f, false);
            var porch = Lamp(root, new Vector3(doorX, doorTop + 0.3f, LobbyFrontZ - 0.5f), new Color(1f, 0.75f, 0.45f), 9f, 8f);
            porch.shadows = LightShadows.None;
        }

        /// <summary>A brick wall along x, in 6 m pieces so each piece gets the street lights near it.</summary>
        private static void Wall(Transform root, string name, float fromX, float toX, float z, Material material)
        {
            for (float x = fromX; x < toX - 0.01f; x += 6f)
            {
                float width = Mathf.Min(6f, toX - x);
                BuildKit.Box(name, root, new Vector3(x + width * 0.5f, FrontHeight * 0.5f, z), new Vector3(width, FrontHeight, 0.3f), material, 2f);
            }
        }

        // ------------------------------------------------------------------ signs

        private void BuildSigns(Transform root, GameAssets assets)
        {
            Texture2D main = assets != null ? assets.SignMain : null;
            Texture2D pole = assets != null ? assets.SignPole : null;

            if (main != null)
            {
                var quad = BuildKit.Quad("MainSign", root, new Vector3(-6.5f, 4.95f, FrontZ - 0.2f), new Vector2(9.6f, 3f), 180f, Neon("signMain", main));
                mainSign = quad.GetComponent<Renderer>();
                mainSign.material = new Material(mainSign.sharedMaterial);
                BuildKit.Box("SignBack", root, new Vector3(-6.5f, 4.95f, FrontZ - 0.12f), new Vector3(9.9f, 3.25f, 0.1f), BuildKit.Plain("signBack", new Color(0.05f, 0.05f, 0.06f), 0.3f, 0.5f), 1f, false);
            }
            signLights.Add(Lamp(root, new Vector3(-9f, 4.8f, FrontZ - 2.2f), new Color(1f, 0.3f, 0.2f), 14f, 14f));
            signLights.Add(Lamp(root, new Vector3(-4f, 4.4f, FrontZ - 2.2f), new Color(1f, 0.7f, 0.25f), 12f, 13f));

            // Pole sign by the road, readable from both directions.
            Material poleMetal = BuildKit.Plain("poleMetal", new Color(0.15f, 0.15f, 0.16f), 0.5f, 0.8f);
            var polePosition = new Vector3(-2.5f, 0f, RoadZ + 5.2f);
            BuildKit.Cylinder("Pole", root, polePosition + new Vector3(0f, 3.2f, 0f), 0.3f, 6.4f, poleMetal);
            if (pole != null)
            {
                Material neon = Neon("signPole", pole);
                BuildKit.Quad("PoleSignFront", root, polePosition + new Vector3(0f, 7.6f, -0.12f), new Vector2(3.2f, 3.2f), 180f, neon);
                BuildKit.Quad("PoleSignBack", root, polePosition + new Vector3(0f, 7.6f, 0.12f), new Vector2(3.2f, 3.2f), 0f, neon);
                BuildKit.Box("PoleSignBox", root, polePosition + new Vector3(0f, 7.6f, 0f), new Vector3(3.4f, 3.4f, 0.2f), poleMetal, 1f, false);
            }
            signLights.Add(Lamp(root, polePosition + new Vector3(0f, 7.4f, -1.8f), new Color(1f, 0.45f, 0.25f), 10f, 12f));
        }

        private static Material Neon(string key, Texture2D texture)
        {
            Material material = BuildKit.Picture(key, texture, 4f);
            material.SetFloat("_Smoothness", 0.5f);
            return material;
        }

        // ------------------------------------------------------------------ street lights

        private void BuildStreetLights(Transform root)
        {
            Material metal = BuildKit.Plain("lampPost", new Color(0.13f, 0.14f, 0.14f), 0.4f, 0.7f);
            Material bulb = BuildKit.Glow("sodiumBulb", new Color(1f, 0.62f, 0.3f), 6f);
            Vector3[] posts = { new Vector3(-32f, 0f, RoadZ + 4.6f), new Vector3(-12f, 0f, RoadZ + 4.6f), new Vector3(14f, 0f, RoadZ + 4.6f), new Vector3(36f, 0f, RoadZ + 4.6f), new Vector3(17.5f, 0f, -21f) };
            for (int i = 0; i < posts.Length; i++)
            {
                Vector3 p = posts[i];
                BuildKit.Cylinder("Post", root, p + new Vector3(0f, 3.4f, 0f), 0.18f, 6.8f, metal);
                float reach = p.z > RoadZ + 4f && p.z < -22f ? -1.6f : 1.6f;
                BuildKit.Box("Arm", root, p + new Vector3(0f, 6.75f, reach * 0.5f), new Vector3(0.1f, 0.1f, 1.7f), metal, 1f, false);
                Vector3 head = p + new Vector3(0f, 6.65f, reach);
                BuildKit.Box("Head", root, head, new Vector3(0.45f, 0.14f, 0.7f), metal, 1f, false);
                BuildKit.Box("Bulb", root, head + new Vector3(0f, -0.09f, 0f), new Vector3(0.35f, 0.04f, 0.55f), bulb, 1f, false);
                var light = new GameObject("StreetLight").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = head + new Vector3(0f, -0.2f, 0f);
                light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                light.type = LightType.Spot;
                light.spotAngle = 120f;
                light.innerSpotAngle = 60f;
                light.range = 20f;
                light.intensity = i == 1 ? 0f : 45f;  // one lamp near the pizzeria is dead
                light.color = new Color(1f, 0.6f, 0.32f);
                light.shadows = LightShadows.None;
                lights.Add(light);
            }
        }

        private Light Lamp(Transform root, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject("Lamp").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            lights.Add(light);
            return light;
        }

        // ------------------------------------------------------------------ the town around

        private void BuildSkyline(Transform root)
        {
            Material facade = BuildKit.Picture("skylineWindows", ProceduralTextures.CityWindows(), 1f);
            var random = new System.Random(38);
            for (int i = 0; i < 46; i++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                float distance = 95f + (float)random.NextDouble() * 110f;
                var center = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance - 10f);
                // Keep the street in front of the pizzeria open.
                if (center.z < RoadZ - 6f && center.z > RoadZ - 40f && Mathf.Abs(center.x) < 60f)
                {
                    center.z -= 40f;
                }
                float width = 12f + (float)random.NextDouble() * 18f;
                float depth = 12f + (float)random.NextDouble() * 14f;
                float height = 9f + (float)(random.NextDouble() * random.NextDouble()) * 45f;
                var block = BuildKit.Box("Building", root, center + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), facade, 6f, false);
                block.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 20f - 10f, 0f);
                // A red light on the tall ones.
                if (height > 35f)
                {
                    BuildKit.Box("Beacon", root, center + new Vector3(0f, height + 0.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f), BuildKit.Glow("beacon", new Color(1f, 0.1f, 0.05f), 8f), 1f, false);
                }
            }
        }

        // ------------------------------------------------------------------ weather

        private void BuildRain(Transform root)
        {
            rainAnchor = new GameObject("Rain").transform;
            rainAnchor.SetParent(root, false);
            rain = rainAnchor.gameObject.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = rain.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 18f;
            main.startSize = 0.025f;
            main.startColor = new Color(0.7f, 0.75f, 0.85f, 0.35f);
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.4f;
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = 2600f;
            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 40f, 0.5f);
            rainAnchor.localRotation = Quaternion.Euler(95f, 10f, 0f);   // falling down, a little slanted by the wind
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 6f;
            renderer.velocityScale = 0.02f;
            renderer.sharedMaterial = BuildKit.Additive("rainStreak", ProceduralTextures.Disc(16), new Color(0.65f, 0.7f, 0.8f, 0.5f));
        }

        private void BuildMoon(Transform root)
        {
            moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(root, false);
            moon.type = LightType.Directional;
            moon.transform.localRotation = Quaternion.Euler(28f, 25f, 0f);
            moon.color = new Color(0.55f, 0.65f, 0.95f);
            moon.intensity = 0.16f;
            moon.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ switching

        /// <summary>
        /// Outside: rain, lights, a dark blue night instead of the black interior fog, and a far
        /// camera range. Inside: everything off and the interior look restored.
        /// </summary>
        public void SetOutdoors(bool on, Camera camera)
        {
            if (on == outdoors)
            {
                return;
            }
            outdoors = on;
            if (on)
            {
                savedCamera = camera;
                savedFog = RenderSettings.fogColor;
                savedFogDensity = RenderSettings.fogDensity;
                savedAmbient = RenderSettings.ambientLight;
                savedReflections = RenderSettings.reflectionIntensity;
                savedFar = camera.farClipPlane;
                savedClear = camera.clearFlags;
                savedBackground = camera.backgroundColor;
                RenderSettings.fogColor = NightFog;
                RenderSettings.fogDensity = 0.011f;
                RenderSettings.ambientLight = new Color(0.09f, 0.095f, 0.13f);
                // Without a sky, the default reflection is a flat grey that turns wet asphalt and the
                // car white; the night has no sky to reflect, only the lights.
                RenderSettings.reflectionIntensity = 0.3f;
                camera.farClipPlane = 320f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = NightSky;
                rain.Play();
            }
            else
            {
                RenderSettings.fogColor = savedFog;
                RenderSettings.fogDensity = savedFogDensity;
                RenderSettings.ambientLight = savedAmbient;
                RenderSettings.reflectionIntensity = savedReflections;
                if (savedCamera != null)
                {
                    savedCamera.farClipPlane = savedFar;
                    savedCamera.clearFlags = savedClear;
                    savedCamera.backgroundColor = savedBackground;
                }
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            ApplyLights(on);
        }

        private void ApplyLights(bool on)
        {
            // Wet and shiny only while the camera is outside with the street lights on; from inside,
            // with no light out there, a shiny ground would just mirror the grey default sky.
            asphalt.SetFloat("_Smoothness", on ? 0.72f : 0.05f);
            sidewalk.SetFloat("_Smoothness", on ? 0.55f : 0.05f);
            foreach (Light light in lights)
            {
                light.enabled = on;
            }
            moon.enabled = on;
            Car.SetLights(on && Car.HeadlightsOn);
        }

        /// <summary>A flash of lightning (the thunder is played by the caller, a moment later).</summary>
        public void Lightning()
        {
            lightningUntil = Time.time + 0.45f;
        }

        private void LateUpdate()
        {
            if (!outdoors)
            {
                return;
            }
            Camera camera = Camera.main;
            if (camera != null)
            {
                // Rain falls around the camera wherever it goes.
                rainAnchor.position = camera.transform.position + new Vector3(0f, 12f, 0f) + camera.transform.forward * 8f;
            }

            // The sign buzzes and drops out now and then.
            if (Time.time >= nextFlicker)
            {
                nextFlicker = Time.time + Random.Range(1.5f, 6f);
                flickerUntil = Time.time + Random.Range(0.05f, 0.35f);
            }
            bool off = Time.time < flickerUntil && Random.value < 0.7f;
            if (mainSign != null)
            {
                mainSign.material.SetColor("_EmissionColor", Color.white * (off ? 0.4f : 4f));
            }
            for (int i = 0; i < signLights.Count; i++)
            {
                signLights[i].enabled = !(off && i < 2);
            }

            // Lightning: the moon flashes a couple of times.
            float flash = Time.time < lightningUntil ? (Mathf.Sin((lightningUntil - Time.time) * 40f) > 0f ? 1f : 0.2f) : 0f;
            moon.intensity = 0.16f + flash * 2.4f;
            if (camera != null)
            {
                camera.backgroundColor = Color.Lerp(NightSky, new Color(0.35f, 0.38f, 0.5f), flash * 0.6f);
            }
        }
    }

    /// <summary>The guard's old sedan, built from boxes: it drives, parks and opens its door.</summary>
    public class Car : MonoBehaviour
    {
        private readonly List<Transform> wheels = new List<Transform>();
        private readonly List<Light> headlights = new List<Light>();
        private Renderer[] lamps;
        private Transform door;
        private Vector3 lastPosition;

        public bool HeadlightsOn { get; private set; } = true;

        /// <summary>Where the driver's eyes are, in the car's space.</summary>
        public static readonly Vector3 DriverEyes = new Vector3(-0.38f, 1.12f, -0.15f);

        public static Car Create(Transform parent)
        {
            var car = new GameObject("Car").AddComponent<Car>();
            car.transform.SetParent(parent, false);
            Transform t = car.transform;
            Material paint = BuildKit.Plain("carPaint", new Color(0.18f, 0.05f, 0.05f), 0.85f, 0.6f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            // See-through, so the shot from the driver's seat shows the street.
            Material glass = BuildKit.Stain("carGlass", Texture2D.whiteTexture, new Color(0.06f, 0.08f, 0.1f, 0.5f));
            Material tire = BuildKit.Plain("tire", new Color(0.03f, 0.03f, 0.03f), 0.3f);
            Material head = BuildKit.Glow("headlight", new Color(1f, 0.95f, 0.85f), 6f);
            Material tail = BuildKit.Glow("taillight", new Color(1f, 0.05f, 0.03f), 5f);

            BuildKit.Box("Body", t, new Vector3(0f, 0.62f, 0f), new Vector3(1.8f, 0.62f, 4.5f), paint, 1f, false);
            BuildKit.Box("Hood", t, new Vector3(0f, 0.95f, 1.45f), new Vector3(1.76f, 0.06f, 1.5f), paint, 1f, false);
            BuildKit.Box("Trunk", t, new Vector3(0f, 0.95f, -1.75f), new Vector3(1.76f, 0.06f, 1f), paint, 1f, false);
            BuildKit.Box("Roof", t, new Vector3(0f, 1.42f, -0.25f), new Vector3(1.6f, 0.06f, 1.9f), paint, 1f, false);
            BuildKit.Box("Windshield", t, new Vector3(0f, 1.2f, 0.85f), new Vector3(1.58f, 0.5f, 0.04f), glass, 1f, false).transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            BuildKit.Box("RearWindow", t, new Vector3(0f, 1.2f, -1.32f), new Vector3(1.58f, 0.48f, 0.04f), glass, 1f, false).transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            BuildKit.Box("SideRight", t, new Vector3(0.79f, 1.18f, -0.25f), new Vector3(0.03f, 0.42f, 1.9f), glass, 1f, false);
            BuildKit.Box("PillarLeftFront", t, new Vector3(-0.79f, 1.18f, 0.66f), new Vector3(0.05f, 0.45f, 0.08f), paint, 1f, false);
            BuildKit.Box("PillarLeftBack", t, new Vector3(-0.79f, 1.18f, -1.15f), new Vector3(0.05f, 0.45f, 0.08f), paint, 1f, false);
            BuildKit.Box("PillarRightFront", t, new Vector3(0.79f, 1.18f, 0.66f), new Vector3(0.05f, 0.45f, 0.08f), paint, 1f, false);
            BuildKit.Box("PillarRightBack", t, new Vector3(0.79f, 1.18f, -1.15f), new Vector3(0.05f, 0.45f, 0.08f), paint, 1f, false);
            BuildKit.Box("BumperFront", t, new Vector3(0f, 0.42f, 2.28f), new Vector3(1.84f, 0.16f, 0.1f), chrome, 1f, false);
            BuildKit.Box("BumperBack", t, new Vector3(0f, 0.42f, -2.28f), new Vector3(1.84f, 0.16f, 0.1f), chrome, 1f, false);
            BuildKit.Box("Seats", t, new Vector3(0f, 0.85f, -0.4f), new Vector3(1.5f, 0.5f, 1.2f), tire, 1f, false);
            BuildKit.Box("Dashboard", t, new Vector3(0f, 0.92f, 0.72f), new Vector3(1.56f, 0.22f, 0.4f), tire, 1f, false);
            BuildKit.Box("Gauges", t, new Vector3(-0.38f, 1.04f, 0.56f), new Vector3(0.3f, 0.06f, 0.02f), BuildKit.Glow("gauges", new Color(0.3f, 0.8f, 1f), 1.5f), 1f, false);
            var wheelRim = BuildKit.Cylinder("SteeringWheel", t, new Vector3(-0.38f, 0.98f, 0.38f), 0.36f, 0.03f, tire);
            wheelRim.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

            // The driver's door: glass on top, panel below, hinged at the front.
            car.door = new GameObject("DoorHinge").transform;
            car.door.SetParent(t, false);
            car.door.localPosition = new Vector3(-0.9f, 0f, 0.6f);
            BuildKit.Box("DoorPanel", car.door, new Vector3(0f, 0.7f, -0.6f), new Vector3(0.05f, 0.62f, 1.15f), paint, 1f, false);
            BuildKit.Box("DoorGlass", car.door, new Vector3(0.02f, 1.18f, -0.62f), new Vector3(0.03f, 0.42f, 1.0f), glass, 1f, false);

            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.82f : 0.82f;
                float z = i < 2 ? 1.4f : -1.4f;
                var axle = new GameObject("Wheel").transform;
                axle.SetParent(t, false);
                axle.localPosition = new Vector3(x, 0.34f, z);
                var wheel = BuildKit.Cylinder("Tire", axle, Vector3.zero, 0.68f, 0.24f, tire);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                BuildKit.Cylinder("Hub", wheel.transform, new Vector3(0f, 1.02f, 0f), 0.6f, 0.05f, chrome);
                car.wheels.Add(axle);
            }

            var lampList = new List<Renderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                lampList.Add(BuildKit.Box("Headlight", t, new Vector3(side * 0.62f, 0.68f, 2.26f), new Vector3(0.38f, 0.16f, 0.06f), head, 1f, false).GetComponent<Renderer>());
                lampList.Add(BuildKit.Box("Taillight", t, new Vector3(side * 0.66f, 0.72f, -2.26f), new Vector3(0.32f, 0.14f, 0.06f), tail, 1f, false).GetComponent<Renderer>());
                var beam = new GameObject("Beam").AddComponent<Light>();
                beam.transform.SetParent(t, false);
                beam.transform.localPosition = new Vector3(side * 0.62f, 0.7f, 2.4f);
                beam.transform.localRotation = Quaternion.Euler(6f, side * 4f, 0f);
                beam.type = LightType.Spot;
                beam.spotAngle = 55f;
                beam.innerSpotAngle = 25f;
                beam.range = 30f;
                beam.intensity = 14f;
                beam.color = new Color(1f, 0.95f, 0.85f);
                beam.shadows = LightShadows.None;
                car.headlights.Add(beam);
            }
            car.lamps = lampList.ToArray();
            return car;
        }

        /// <summary>Puts the car somewhere, turning the wheels by the distance it moved.</summary>
        public void Place(Vector3 position, float yaw)
        {
            float moved = Vector3.Distance(position, lastPosition);
            lastPosition = position;
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float spin = moved < 5f ? moved / (0.34f * Mathf.PI * 2f) * 360f : 0f;
            foreach (Transform wheel in wheels)
            {
                wheel.Rotate(spin, 0f, 0f, Space.Self);
            }
        }

        /// <summary>0 shut, 1 wide open.</summary>
        public void SetDoor(float open)
        {
            door.localRotation = Quaternion.Euler(0f, 70f * open, 0f);
        }

        public void SetHeadlights(bool on)
        {
            HeadlightsOn = on;
            SetLights(on);
        }

        public void SetLights(bool on)
        {
            foreach (Light light in headlights)
            {
                light.enabled = on;
            }
            foreach (Renderer lamp in lamps)
            {
                lamp.enabled = on;
            }
        }
    }
}
