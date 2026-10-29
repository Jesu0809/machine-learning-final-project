using System.Collections.Generic;
using NightShift.Hunt;
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
        private const float FrontZ = -16.35f;
        private const float LobbyFrontZ = -17.35f;
        private const float FrontHeight = 7.2f;   // one tall storey with a false front for the sign
        private const float WestX = -22.35f;
        private const float EastX = 23.35f;
        private const float LobbyWestX = 12f;
        private const float BackZ = 22.35f;
        private const float SidewalkZ = -19.6f;
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
        private ParticleSystem splashes;
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
            exterior.BuildBackAlley(root);
            exterior.BuildBackLot(root, assets);
            exterior.BuildSigns(root, assets);
            exterior.BuildStreetLights(root);
            exterior.BuildRoadside(root);
            exterior.BuildSkyline(root);
            exterior.Car = Car.Create(root, assets);
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
            BuildKit.Box("FarGround", root, new Vector3(-80f, -0.09f, -20f), new Vector3(560f, 0.1f, 400f), asphalt, 6f, false);
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
            BuildKit.Box("RoadEdgeNorth", root, new Vector3(-80f, -0.015f, RoadZ + 3.9f), new Vector3(520f, 0.01f, 0.15f), paint, 1f, false);
            BuildKit.Box("RoadEdgeSouth", root, new Vector3(-80f, -0.015f, RoadZ - 4.1f), new Vector3(520f, 0.01f, 0.15f), paint, 1f, false);
            for (float x = -330f; x < 170f; x += 6f)
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
            // The west side, with the storage's loading door (the other way out) and a concrete pad outside it.
            Vector3 loading = PizzeriaLayout.BackExitDoor;
            float gapSouth = loading.z - half;
            float gapNorth = loading.z + half;
            BuildKit.Box("SideWest", root, new Vector3(WestX, FrontHeight * 0.5f, (FrontZ + gapSouth) * 0.5f), new Vector3(0.3f, FrontHeight, gapSouth - FrontZ), brick, 2f);
            BuildKit.Box("SideWest", root, new Vector3(WestX, FrontHeight * 0.5f, (gapNorth + BackZ) * 0.5f), new Vector3(0.3f, FrontHeight, BackZ - gapNorth), brick, 2f);
            BuildKit.Box("LoadingLintel", root, new Vector3(WestX, (doorTop + FrontHeight) * 0.5f, loading.z), new Vector3(0.3f, FrontHeight - doorTop, half * 2f), brick, 2f);
            BuildKit.Box("LoadingPad", root, new Vector3(WestX - 2.2f, 0.02f, loading.z), new Vector3(4f, 0.1f, 5f), trim, 2f);
            var dockLamp = Lamp(root, new Vector3(WestX - 0.5f, doorTop + 0.4f, loading.z), new Color(1f, 0.75f, 0.45f), 7f, 5f);
            dockLamp.shadows = LightShadows.None;
            BuildKit.Box("SideEast", root, new Vector3(EastX, FrontHeight * 0.5f, (LobbyFrontZ + BackZ) * 0.5f), new Vector3(0.3f, FrontHeight, BackZ - LobbyFrontZ), brick, 2f);
            BackWall(root, brick, trim);
            BuildKit.Box("Roof", root, new Vector3((WestX + EastX) * 0.5f, FrontHeight - 0.3f, (LobbyFrontZ + BackZ) * 0.5f), new Vector3(EastX - WestX, 0.2f, BackZ - LobbyFrontZ), roof, 4f, false);

            // Cornice along the top, and a base course.
            BuildKit.Box("Cornice", root, new Vector3((WestX + LobbyWestX) * 0.5f, FrontHeight + 0.15f, FrontZ - 0.1f), new Vector3(LobbyWestX - WestX + 0.6f, 0.35f, 0.6f), trim, 2f, false);
            BuildKit.Box("CorniceEntrance", root, new Vector3((LobbyWestX + EastX) * 0.5f, FrontHeight + 0.15f, LobbyFrontZ - 0.1f), new Vector3(EastX - LobbyWestX + 0.6f, 0.35f, 0.6f), trim, 2f, false);
            BuildKit.Box("Base", root, new Vector3((WestX + LobbyWestX) * 0.5f, 0.45f, FrontZ - 0.18f), new Vector3(LobbyWestX - WestX, 0.6f, 0.08f), trim, 2f, false);

            // Big windows of the old dining room: dark glass, some boarded up.
            float[] windows = { -19f, -11f, -4.5f, 7f };
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
            // The threshold under the doors, from the sidewalk to the doors' sill (no gap of street between them).
            BuildKit.Box("Threshold", root, new Vector3(doorX, -0.03f, (LobbyFrontZ + PizzeriaLayout.ExitDoor.z) * 0.5f - 0.1f),
                         new Vector3(half * 2f, 0.1f, PizzeriaLayout.ExitDoor.z - LobbyFrontZ + 0.3f), sidewalk, 1f, false);
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

        // ------------------------------------------------------------------ the back wall and the lot behind it

        /// <summary>
        /// The building's back wall, with the north wing's windows cut through it (the same ones
        /// as the rooms' wall, PizzeriaLayout.Windows) and a reveal joining the two walls, so
        /// looking out of a window shows the lot behind the pizzeria.
        /// </summary>
        private static void BackWall(Transform root, Material brick, Material trim)
        {
            var holes = new List<(float x, float width, float sill, float top)>();
            foreach ((Room _, float x, float width, float sill, float top) in PizzeriaLayout.Windows)
            {
                holes.Add((x, width, sill, top));
            }
            holes.Sort((a, b) => a.x.CompareTo(b.x));
            float start = WestX;
            float inner = PizzeriaLayout.NorthWallZ;
            float depth = BackZ + 0.15f - inner;
            foreach ((float x, float width, float sill, float top) in holes)
            {
                Wall(root, "Back", start, x - width * 0.5f, BackZ, brick);
                BuildKit.Box("BackUnder", root, new Vector3(x, sill * 0.5f, BackZ), new Vector3(width, sill, 0.3f), brick, 2f);
                BuildKit.Box("BackOver", root, new Vector3(x, (top + FrontHeight) * 0.5f, BackZ), new Vector3(width, FrontHeight - top, 0.3f), brick, 2f);
                // The reveal between the rooms' wall and this one, and a stone sill outside.
                float zMid = inner + depth * 0.5f;
                BuildKit.Box("RevealLeft", root, new Vector3(x - width * 0.5f - 0.04f, (sill + top) * 0.5f, zMid), new Vector3(0.08f, top - sill, depth), trim, 1f, false);
                BuildKit.Box("RevealRight", root, new Vector3(x + width * 0.5f + 0.04f, (sill + top) * 0.5f, zMid), new Vector3(0.08f, top - sill, depth), trim, 1f, false);
                BuildKit.Box("RevealTop", root, new Vector3(x, top + 0.04f, zMid), new Vector3(width + 0.16f, 0.08f, depth), trim, 1f, false);
                BuildKit.Box("RevealSill", root, new Vector3(x, sill - 0.04f, zMid), new Vector3(width + 0.16f, 0.08f, depth), trim, 1f, false);
                BuildKit.Box("OutsideSill", root, new Vector3(x, sill - 0.06f, BackZ + 0.22f), new Vector3(width + 0.3f, 0.08f, 0.2f), trim, 1f, false);
                start = x + width * 0.5f;
            }
            Wall(root, "Back", start, EastX, BackZ, brick);
        }

        /// <summary>
        /// What the north windows look out on: the wet back lot under two sodium street lamps (one
        /// stutters), an abandoned car, a dumpster, a chain-link fence with power poles and sagging
        /// wires, small houses beyond it with a few windows still lit (one flickers with a TV), the
        /// low sky, rain falling, and now and then lightning with its thunder.
        /// </summary>
        private void BuildBackLot(Transform root, GameAssets assets)
        {
            const float lotSouth = BackZ + 0.15f;
            const float fenceZ = BackZ + 11f;
            Material brick = BuildKit.Surface("bricks", () => ProceduralTextures.Bricks(new Color(0.42f, 0.17f, 0.12f), new Color(0.32f, 0.3f, 0.27f)), 0.25f);
            Material siding = BuildKit.Pbr("wood_planks", new Color(0.45f, 0.42f, 0.4f)) ?? brick;
            Material roof = BuildKit.Plain("roof", new Color(0.04f, 0.04f, 0.045f), 0.6f);
            Material steel = BuildKit.Plain("frameSteel", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.7f);
            Material wood = BuildKit.Plain("pole", new Color(0.16f, 0.12f, 0.09f), 0.2f);

            // Ground in tiles, a strip of sidewalk along the wall.
            for (float x = WestX; x < EastX; x += 10f)
            {
                float width = Mathf.Min(10f, EastX - x);
                for (float z = lotSouth; z < fenceZ + 14f; z += 10f)
                {
                    BuildKit.Box("LotGround", root, new Vector3(x + width * 0.5f, -0.07f, z + 5f), new Vector3(width, 0.1f, 10f), asphalt, 6f);
                }
                BuildKit.Box("LotSidewalk", root, new Vector3(x + width * 0.5f, 0.02f, lotSouth + 0.9f), new Vector3(width, 0.12f, 1.8f), sidewalk, 2f, false);
            }

            // Two sodium lamps on poles; the far one stutters.
            foreach ((float x, bool stutter) in new[] { (-6f, false), (14f, true) })
            {
                var pole = BuildKit.Cylinder("LampPole", root, new Vector3(x, 3.2f, lotSouth + 5f), 0.16f, 6.4f, steel);
                BuildKit.Box("LampArm", root, new Vector3(x, 6.3f, lotSouth + 4.3f), new Vector3(0.08f, 0.08f, 1.4f), steel, 1f, false);
                BuildKit.Box("LampHead", root, new Vector3(x, 6.25f, lotSouth + 3.6f), new Vector3(0.35f, 0.14f, 0.6f), steel, 1f, false);
                BuildKit.Box("LampLens", root, new Vector3(x, 6.17f, lotSouth + 3.6f), new Vector3(0.3f, 0.02f, 0.5f), BuildKit.Glow("sodium", new Color(1f, 0.62f, 0.25f), 4f), 1f, false);
                var lamp = new GameObject("LotLamp").AddComponent<Light>();
                lamp.transform.SetParent(root, false);
                lamp.transform.localPosition = new Vector3(x, 6.1f, lotSouth + 3.6f);
                lamp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                lamp.type = LightType.Spot;
                lamp.spotAngle = 130f;
                lamp.innerSpotAngle = 60f;
                lamp.range = 24f;
                // Bright: seen from inside, with the building's dark ambient, it is the lot's only light.
                lamp.intensity = 80f;
                lamp.color = new Color(1f, 0.64f, 0.3f);
                lamp.shadows = LightShadows.Soft;
                if (stutter)
                {
                    lamp.gameObject.AddComponent<SodiumFlicker>();
                }
            }

            // An abandoned car under the near lamp, a dumpster against the wall.
            Wreck(root, assets, new Vector3(-3.2f, 0f, lotSouth + 6.2f), 70f);
            Dumpster(root, new Vector3(19.2f, 0f, lotSouth + 1.1f), 90f);

            // The fence, with power poles behind it and wires sagging between them.
            Texture2D link = BuildKit.SignTexture("fence_chainlink");
            for (float x = WestX; x <= EastX; x += 3f)
            {
                BuildKit.Cylinder("FencePost", root, new Vector3(x, 1.1f, fenceZ), 0.06f, 2.2f, steel);
            }
            BuildKit.Cylinder("FenceRail", root, new Vector3((WestX + EastX) * 0.5f, 2.2f, fenceZ), 0.04f, EastX - WestX, steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (link != null)
            {
                Material mesh = BuildKit.Decal("chainLinkLot", link, Color.white);
                mesh.SetTextureScale("_BaseMap", new Vector2((EastX - WestX) / 0.8f, 2.2f / 0.8f));
                BuildKit.Quad("Fence", root, new Vector3((WestX + EastX) * 0.5f, 1.1f, fenceZ), new Vector2(EastX - WestX, 2.2f), 180f, mesh);
            }
            Material wire = BuildKit.Plain("wire", new Color(0.02f, 0.02f, 0.02f), 0.3f);
            for (float x = WestX + 2f; x < EastX; x += 11f)
            {
                BuildKit.Cylinder("PowerPole", root, new Vector3(x, 4.5f, fenceZ + 2f), 0.26f, 9f, wood);
                BuildKit.Box("CrossArm", root, new Vector3(x, 8.4f, fenceZ + 2f), new Vector3(1.8f, 0.12f, 0.12f), wood, 1f, false);
                if (x + 11f < EastX)
                {
                    // A sagging wire to the next pole, in a few straight pieces.
                    for (int wireIndex = -1; wireIndex <= 1; wireIndex += 2)
                    {
                        const int pieces = 6;
                        for (int k = 0; k < pieces; k++)
                        {
                            float a = k / (float)pieces, b = (k + 1) / (float)pieces;
                            var p0 = new Vector3(x + a * 11f, 8.45f - Mathf.Sin(a * Mathf.PI) * 0.6f, fenceZ + 2f + wireIndex * 0.7f);
                            var p1 = new Vector3(x + b * 11f, 8.45f - Mathf.Sin(b * Mathf.PI) * 0.6f, fenceZ + 2f + wireIndex * 0.7f);
                            var piece = BuildKit.Cylinder("Wire", root, (p0 + p1) * 0.5f, 0.025f, Vector3.Distance(p0, p1), wire);
                            piece.transform.localRotation = Quaternion.FromToRotation(Vector3.up, p1 - p0);
                        }
                    }
                }
            }

            // Small houses beyond the fence: a few windows lit, one with a TV flickering.
            var random = new System.Random(77);
            Material lit = BuildKit.Glow("houseWindowLit", new Color(1f, 0.72f, 0.4f), 0.9f);
            Material dark = BuildKit.Plain("houseWindowDark", new Color(0.02f, 0.025f, 0.03f), 0.9f, 0.2f);
            bool tvPlaced = false;
            for (float x = WestX + 4f; x < EastX - 3f; x += 10.5f)
            {
                float width = 7f + (float)random.NextDouble() * 2f;
                float height = 4.2f + (float)random.NextDouble() * 1.6f;
                float z = fenceZ + 9f + (float)random.NextDouble() * 3f;
                BuildKit.Box("House", root, new Vector3(x, height * 0.5f, z), new Vector3(width, height, 6f), random.NextDouble() < 0.5 ? siding : brick, 2f, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    var slope = BuildKit.Box("Roof", root, new Vector3(x, height + 0.9f, z + side * 1.55f), new Vector3(width + 0.4f, 0.15f, 3.6f), roof, 1f, false);
                    slope.transform.localRotation = Quaternion.Euler(side * -28f, 0f, 0f);
                }
                // A porch light by the back door, so the house reads against the sky.
                var porch = new GameObject("PorchLight").AddComponent<Light>();
                porch.transform.SetParent(root, false);
                porch.transform.localPosition = new Vector3(x + width * 0.3f, 2.4f, z - 3.4f);
                porch.type = LightType.Point;
                porch.range = 7f;
                porch.intensity = 6f;
                porch.color = new Color(1f, 0.75f, 0.45f);
                porch.shadows = LightShadows.None;
                BuildKit.Box("PorchLamp", root, new Vector3(x + width * 0.3f, 2.4f, z - 3.06f), new Vector3(0.14f, 0.2f, 0.08f), lit, 1f, false);
                for (int w = 0; w < 3; w++)
                {
                    float wx = x - width * 0.33f + w * width * 0.33f;
                    bool on = random.NextDouble() < 0.35;
                    GameObject window = BuildKit.Box("HouseWindow", root, new Vector3(wx, height * 0.55f, z - 3.02f), new Vector3(1f, 1.1f, 0.04f), on ? lit : dark, 1f, false);
                    if (!on && !tvPlaced && random.NextDouble() < 0.5)
                    {
                        tvPlaced = true;
                        window.AddComponent<TvGlow>();
                    }
                }
            }

            // The sky behind it all: low clouds lit by the town, flashing with the lightning.
            Texture2D skyTexture = BuildKit.SignTexture("sky_night");
            if (skyTexture != null)
            {
                Material sky = BuildKit.Unlit(skyTexture, new Color(3.2f, 3.2f, 3.5f));
                GameObject backdrop = BuildKit.Quad("Sky", root, new Vector3(0f, 12f, fenceZ + 20f), new Vector2(110f, 30f), 180f, sky);
                var storm = backdrop.AddComponent<Lightning>();
                storm.Set(sky, assets != null ? assets.Thunder : null);
            }

            // Rain over the lot, always falling (seen through the windows).
            var rainObject = new GameObject("LotRain");
            rainObject.transform.SetParent(root, false);
            rainObject.transform.localPosition = new Vector3(0f, 9f, lotSouth + 7f);
            rainObject.transform.localRotation = Quaternion.Euler(95f, 10f, 0f);
            var lotRain = rainObject.AddComponent<ParticleSystem>();
            lotRain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = lotRain.main;
            main.loop = true;
            main.startLifetime = 0.7f;
            main.startSpeed = 16f;
            main.startSize = 0.025f;
            main.startColor = new Color(0.7f, 0.75f, 0.85f, 0.35f);
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = lotRain.emission;
            emission.rateOverTime = 2200f;
            ParticleSystem.ShapeModule shape = lotRain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(EastX - WestX, 14f, 0.5f);
            var rainRenderer = lotRain.GetComponent<ParticleSystemRenderer>();
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.lengthScale = 6f;
            rainRenderer.velocityScale = 0.02f;
            rainRenderer.sharedMaterial = BuildKit.Additive("rainStreak", ProceduralTextures.Disc(16), new Color(0.65f, 0.7f, 0.8f, 0.5f));
            lotRain.Play();
        }

        /// <summary>A rusted sedan left in the lot (car_wreck.glb, or the box car with its lights off).</summary>
        private static void Wreck(Transform root, GameAssets assets, Vector3 position, float yaw)
        {
            if (assets == null || assets.WreckModel == null)
            {
                Car boxes = Car.Create(root, null);
                boxes.Place(position, yaw);
                boxes.SetLights(false);
                return;
            }
            GameObject wreck = Object.Instantiate(assets.WreckModel, root, false);
            wreck.name = "Wreck";
            // Measured while it still stands unturned at the origin, then placed.
            Renderer[] renderers = wreck.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(wreck.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(wreck.transform.InverseTransformPoint(renderer.bounds.min));
                bounds.Encapsulate(wreck.transform.InverseTransformPoint(renderer.bounds.max));
            }
            var solid = wreck.AddComponent<BoxCollider>();
            solid.center = bounds.center;
            solid.size = bounds.size;
            wreck.transform.localPosition = position;
            wreck.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>A green steel dumpster, lids half open, on four casters.</summary>
        private static void Dumpster(Transform root, Vector3 position, float yaw)
        {
            Material dumpster = BuildKit.Pbr("metal_green", new Color(0.75f, 0.85f, 0.75f)) ?? BuildKit.Plain("dumpster", new Color(0.12f, 0.25f, 0.15f), 0.4f, 0.5f);
            Material rubber = BuildKit.Plain("rubber", new Color(0.03f, 0.03f, 0.03f), 0.3f);
            var bin = new GameObject("Dumpster").transform;
            bin.SetParent(root, false);
            bin.localPosition = position;
            bin.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildKit.Box("Body", bin, new Vector3(0f, 0.7f, 0f), new Vector3(1.1f, 1.1f, 1.9f), dumpster);
            BuildKit.Box("Rim", bin, new Vector3(0f, 1.27f, 0f), new Vector3(1.16f, 0.06f, 1.96f), dumpster, 1f, false);
            for (int i = 0; i < 2; i++)
            {
                var lid = BuildKit.Box("Lid", bin, new Vector3(-0.05f, 1.32f + i * 0.08f, -0.47f + i * 0.95f), new Vector3(1.2f, 0.04f, 0.95f), BuildKit.Plain("lidBlack", new Color(0.05f, 0.05f, 0.05f), 0.4f), 1f, false);
                lid.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 4f : -22f);
            }
            foreach (Vector3 wheel in new[] { new Vector3(-0.4f, 0.08f, -0.8f), new Vector3(0.4f, 0.08f, -0.8f), new Vector3(-0.4f, 0.08f, 0.8f), new Vector3(0.4f, 0.08f, 0.8f) })
            {
                BuildKit.Cylinder("Wheel", bin, wheel, 0.16f, 0.06f, rubber).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        // ------------------------------------------------------------------ the back alley

        /// <summary>
        /// What the loading door opens onto: a wet alley between the pizzeria and the next
        /// building, lit by one buzzing sodium lamp over the door (always on, unlike the street
        /// lights). A dumpster, pallets, trash cans, puddles and a chain-link fence at the end.
        /// </summary>
        private void BuildBackAlley(Transform root)
        {
            const float alleyWestX = WestX - 9f;     // the wall of the next building
            const float southZ = SidewalkZ;
            const float fenceZ = BackZ + 5f;
            Vector3 loading = PizzeriaLayout.BackExitDoor;
            Material brick = BuildKit.Surface("bricks", () => ProceduralTextures.Bricks(new Color(0.42f, 0.17f, 0.12f), new Color(0.32f, 0.3f, 0.27f)), 0.25f);
            Material darkBrick = BuildKit.Pbr("bricks", new Color(0.55f, 0.5f, 0.5f)) ?? brick;
            Material boards = BuildKit.Surface("boards", () => ProceduralTextures.Planks(new Color(0.3f, 0.22f, 0.14f)), 0.2f);
            Material steel = BuildKit.Plain("frameSteel", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.7f);
            Material dumpster = BuildKit.Pbr("metal_green", new Color(0.75f, 0.85f, 0.75f)) ?? BuildKit.Plain("dumpster", new Color(0.12f, 0.25f, 0.15f), 0.4f, 0.5f);
            Material rubber = BuildKit.Plain("rubber", new Color(0.03f, 0.03f, 0.03f), 0.3f);

            // Ground in tiles (lights per object), the next building's wall across the alley.
            for (float z = southZ; z < fenceZ; z += 6f)
            {
                float depth = Mathf.Min(6f, fenceZ - z);
                BuildKit.Box("AlleyGround", root, new Vector3((alleyWestX + WestX) * 0.5f, -0.07f, z + depth * 0.5f), new Vector3(WestX - alleyWestX, 0.1f, depth), asphalt, 6f, false);
                BuildKit.Box("NextBuilding", root, new Vector3(alleyWestX - 0.2f, 4f, z + depth * 0.5f), new Vector3(0.4f, 8f, depth), darkBrick, 2f);
            }
            // Its windows: boarded, dark, and one with a weak light (someone still lives there).
            float[] windowZ = { -10f, -2f, 6f, 13f };
            for (int i = 0; i < windowZ.Length; i++)
            {
                var center = new Vector3(alleyWestX + 0.02f, 3.4f, windowZ[i]);
                BuildKit.Box("Window", root, center, new Vector3(0.06f, 1.6f, 1.2f),
                             i == 2 ? BuildKit.Glow("windowWarm", new Color(1f, 0.7f, 0.35f), 0.35f) : BuildKit.Plain("darkGlass", new Color(0.02f, 0.025f, 0.03f), 0.96f, 0.2f), 1f, false);
                BuildKit.Box("Sill", root, center + new Vector3(0.08f, -0.85f, 0f), new Vector3(0.2f, 0.08f, 1.4f), steel, 1f, false);
                if (i % 2 == 1)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        var plank = BuildKit.Box("Board", root, center + new Vector3(0.06f, -0.5f + b * 0.5f, 0f), new Vector3(0.04f, 0.24f, 1.4f), boards, 1f, false);
                        plank.transform.localRotation = Quaternion.Euler((b - 1) * 8f, 0f, 0f);
                    }
                }
            }
            // A fire escape ladder up the next building.
            for (float y = 1.2f; y < 7.5f; y += 0.35f)
            {
                BuildKit.Box("Rung", root, new Vector3(alleyWestX + 0.35f, y, 9.5f), new Vector3(0.04f, 0.04f, 0.5f), steel, 1f, false);
            }
            foreach (float side in new[] { -0.25f, 0.25f })
            {
                BuildKit.Box("Rail", root, new Vector3(alleyWestX + 0.35f, 4.35f, 9.5f + side), new Vector3(0.05f, 6.3f, 0.05f), steel, 1f, false);
            }

            // The sodium lamp over the loading door, and its fixture.
            BuildKit.Box("WallPack", root, new Vector3(WestX - 0.2f, 3.3f, loading.z), new Vector3(0.25f, 0.3f, 0.4f), steel, 1f, false);
            BuildKit.Box("WallPackLens", root, new Vector3(WestX - 0.34f, 3.22f, loading.z), new Vector3(0.03f, 0.18f, 0.32f), BuildKit.Glow("sodium", new Color(1f, 0.62f, 0.25f), 4f), 1f, false);
            var sodium = new GameObject("SodiumLamp").AddComponent<Light>();
            sodium.transform.SetParent(root, false);
            sodium.transform.localPosition = new Vector3(WestX - 0.45f, 3.2f, loading.z);
            sodium.transform.localRotation = Quaternion.Euler(60f, -90f, 0f);
            sodium.type = LightType.Spot;
            sodium.spotAngle = 125f;
            sodium.innerSpotAngle = 60f;
            sodium.range = 14f;
            sodium.intensity = 7f;
            sodium.color = new Color(1f, 0.66f, 0.32f);
            sodium.shadows = LightShadows.Soft;
            sodium.gameObject.AddComponent<SodiumFlicker>();

            // The dumpster against the wall, lids half open.
            var bin = new GameObject("Dumpster").transform;
            bin.SetParent(root, false);
            bin.localPosition = new Vector3(WestX - 0.75f, 0f, loading.z - 4.2f);
            BuildKit.Box("Body", bin, new Vector3(0f, 0.7f, 0f), new Vector3(1.1f, 1.1f, 1.9f), dumpster);
            BuildKit.Box("Rim", bin, new Vector3(0f, 1.27f, 0f), new Vector3(1.16f, 0.06f, 1.96f), dumpster, 1f, false);
            for (int i = 0; i < 2; i++)
            {
                var lid = BuildKit.Box("Lid", bin, new Vector3(-0.05f, 1.32f + i * 0.08f, -0.47f + i * 0.95f), new Vector3(1.2f, 0.04f, 0.95f), BuildKit.Plain("lidBlack", new Color(0.05f, 0.05f, 0.05f), 0.4f), 1f, false);
                lid.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 4f : -22f);
            }
            foreach (Vector3 wheel in new[] { new Vector3(-0.4f, 0.08f, -0.8f), new Vector3(0.4f, 0.08f, -0.8f), new Vector3(-0.4f, 0.08f, 0.8f), new Vector3(0.4f, 0.08f, 0.8f) })
            {
                BuildKit.Cylinder("Wheel", bin, wheel, 0.16f, 0.06f, rubber).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            BuildKit.Prop("cardboard_box_01", root, new Vector3(WestX - 1.6f, 0f, loading.z - 5.6f), 35f);
            BuildKit.Prop("cardboard_box_01", root, new Vector3(WestX - 1.7f, 0f, loading.z - 6.2f), -20f, 0.9f);

            // Pallets leaning on the next building, trash cans by the door.
            for (int i = 0; i < 3; i++)
            {
                var pallet = new GameObject("Pallet").transform;
                pallet.SetParent(root, false);
                pallet.localPosition = new Vector3(alleyWestX + 0.35f + i * 0.12f, 0.6f, 2f + i * 0.08f);
                pallet.localRotation = Quaternion.Euler(0f, 0f, -14f + i * 2f);
                for (int slat = 0; slat < 5; slat++)
                {
                    BuildKit.Box("Slat", pallet, new Vector3(0f, -0.5f + slat * 0.25f, 0f), new Vector3(0.025f, 0.1f, 1.2f), boards, 1f, false);
                }
                BuildKit.Box("Stringer", pallet, new Vector3(-0.05f, 0f, -0.55f), new Vector3(0.08f, 1.2f, 0.1f), boards, 1f, false);
                BuildKit.Box("Stringer", pallet, new Vector3(-0.05f, 0f, 0.55f), new Vector3(0.08f, 1.2f, 0.1f), boards, 1f, false);
            }
            BuildKit.Prop("metal_trash_can", root, new Vector3(WestX - 0.55f, 0f, loading.z + 3.1f), 20f);
            BuildKit.Prop("metal_trash_can", root, new Vector3(WestX - 0.6f, 0f, loading.z + 3.8f), -35f);
            BuildKit.Prop("Barrel_02", root, new Vector3(alleyWestX + 0.8f, 0f, 11.2f), 0f);
            BuildKit.Prop("plastic_crate_02", root, new Vector3(alleyWestX + 0.9f, 0f, 7.4f), 12f);

            // Puddles that mirror the sodium lamp.
            Texture2D wet = BuildKit.SignTexture("decal_puddle");
            if (wet != null)
            {
                Material water = BuildKit.Decal("puddle", wet, new Color(0.35f, 0.38f, 0.4f, 1f));
                water.SetFloat("_Smoothness", 0.97f);
                Vector3[] puddles = { new Vector3(WestX - 6.2f, 0f, loading.z + 1.5f), new Vector3(WestX - 5.5f, 0f, loading.z - 3f), new Vector3(WestX - 2.4f, 0f, loading.z + 6.5f) };
                for (int i = 0; i < puddles.Length; i++)
                {
                    var quad = BuildKit.Quad("Puddle", root, puddles[i] + Vector3.up * -0.015f, new Vector2(2.6f, 1.7f), 0f, water);
                    quad.transform.localRotation = Quaternion.Euler(90f, i * 70f, 0f);
                }
            }

            // Chain-link fence closing the alley at the back.
            Texture2D link = BuildKit.SignTexture("fence_chainlink");
            for (float x = alleyWestX; x < WestX - 0.1f; x += 3f)
            {
                BuildKit.Cylinder("FencePost", root, new Vector3(x, 1.1f, fenceZ), 0.06f, 2.2f, steel);
            }
            BuildKit.Cylinder("FenceRail", root, new Vector3((alleyWestX + WestX) * 0.5f, 2.2f, fenceZ), 0.04f, WestX - alleyWestX, steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (link != null)
            {
                Material mesh = BuildKit.Decal("chainLink", link, Color.white);
                mesh.SetTextureScale("_BaseMap", new Vector2((WestX - alleyWestX) / 0.8f, 2.2f / 0.8f));
                BuildKit.Quad("Fence", root, new Vector3((alleyWestX + WestX) * 0.5f, 1.1f, fenceZ), new Vector2(WestX - alleyWestX, 2.2f), 180f, mesh);
                BuildKit.Quad("FenceBack", root, new Vector3((alleyWestX + WestX) * 0.5f, 1.1f, fenceZ + 0.01f), new Vector2(WestX - alleyWestX, 2.2f), 0f, mesh);
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

        // ------------------------------------------------------------------ the road into town

        /// <summary>
        /// The road west of the pizzeria, where the guard's car comes from in the intro: sodium lamps
        /// every 30 m (their light sweeps through the car), power poles with sagging wires, small
        /// houses and a closed shop set back from the road, a few windows still lit, and puddles.
        /// </summary>
        private void BuildRoadside(Transform root)
        {
            Material metal = BuildKit.Plain("lampPost", new Color(0.13f, 0.14f, 0.14f), 0.4f, 0.7f);
            Material bulb = BuildKit.Glow("sodiumBulb", new Color(1f, 0.62f, 0.3f), 6f);
            Material wood = BuildKit.Plain("pole", new Color(0.16f, 0.12f, 0.09f), 0.2f);
            Material wire = BuildKit.Plain("wire", new Color(0.02f, 0.02f, 0.02f), 0.3f);
            Material brick = BuildKit.Surface("bricks", () => ProceduralTextures.Bricks(new Color(0.42f, 0.17f, 0.12f), new Color(0.32f, 0.3f, 0.27f)), 0.25f);
            Material siding = BuildKit.Pbr("wood_planks", new Color(0.45f, 0.42f, 0.4f)) ?? brick;
            Material roof = BuildKit.Plain("roof", new Color(0.04f, 0.04f, 0.045f), 0.6f);
            Material lit = BuildKit.Glow("houseWindowLit", new Color(1f, 0.72f, 0.4f), 0.9f);
            Material dark = BuildKit.Plain("houseWindowDark", new Color(0.02f, 0.025f, 0.03f), 0.9f, 0.2f);
            Material yard = BuildKit.Plain("yard", new Color(0.05f, 0.06f, 0.045f), 0.35f);
            var random = new System.Random(91);

            for (float x = -232f; x < -45f; x += 30f)
            {
                StreetLamp(root, new Vector3(x, 0f, RoadZ + 4.6f), -1.6f, metal, bulb, 38f);
            }

            // Power poles on the south side, the wires sagging between them.
            const float poleZ = RoadZ - 6.2f;
            for (float x = -236f; x < -40f; x += 34f)
            {
                BuildKit.Cylinder("PowerPole", root, new Vector3(x, 4.5f, poleZ), 0.26f, 9f, wood);
                BuildKit.Box("CrossArm", root, new Vector3(x, 8.4f, poleZ), new Vector3(0.12f, 0.12f, 1.8f), wood, 1f, false);
                for (int line = -1; line <= 1; line += 2)
                {
                    const int pieces = 7;
                    for (int k = 0; k < pieces; k++)
                    {
                        float a = k / (float)pieces, b = (k + 1) / (float)pieces;
                        var p0 = new Vector3(x + a * 34f, 8.45f - Mathf.Sin(a * Mathf.PI) * 0.9f, poleZ + line * 0.7f);
                        var p1 = new Vector3(x + b * 34f, 8.45f - Mathf.Sin(b * Mathf.PI) * 0.9f, poleZ + line * 0.7f);
                        BuildKit.Cylinder("Wire", root, (p0 + p1) * 0.5f, 0.025f, Vector3.Distance(p0, p1), wire).transform.localRotation = Quaternion.FromToRotation(Vector3.up, p1 - p0);
                    }
                }
            }

            // Houses on both sides, set back behind dark yards, facing the road.
            for (float x = -228f; x < -48f; x += 15f + (float)random.NextDouble() * 9f)
            {
                float side = random.NextDouble() < 0.5 ? 1f : -1f;
                float width = 7f + (float)random.NextDouble() * 3f;
                float height = 3.8f + (float)random.NextDouble() * 1.8f;
                float z = RoadZ + side * (15f + (float)random.NextDouble() * 6f);
                float front = z - side * 3f;
                BuildKit.Box("Yard", root, new Vector3(x, -0.05f, (front + RoadZ + side * 4.2f) * 0.5f), new Vector3(width + 4f, 0.1f, Mathf.Abs(front - RoadZ) - 4.2f), yard, 4f, false);
                BuildKit.Box("House", root, new Vector3(x, height * 0.5f, z), new Vector3(width, height, 6f), random.NextDouble() < 0.6 ? siding : brick, 2f, false);
                for (int slope = -1; slope <= 1; slope += 2)
                {
                    BuildKit.Box("Roof", root, new Vector3(x, height + 0.9f, z + slope * 1.55f), new Vector3(width + 0.4f, 0.15f, 3.6f), roof, 1f, false)
                        .transform.localRotation = Quaternion.Euler(slope * -28f, 0f, 0f);
                }
                for (int w = 0; w < 3; w++)
                {
                    float wx = x - width * 0.33f + w * width * 0.33f;
                    bool on = random.NextDouble() < 0.3;
                    GameObject window = BuildKit.Box("HouseWindow", root, new Vector3(wx, height * 0.55f, front - side * 0.02f), new Vector3(1f, 1.1f, 0.04f), on ? lit : dark, 1f, false);
                    if (!on && random.NextDouble() < 0.12)
                    {
                        window.AddComponent<TvGlow>();
                    }
                }
                if (random.NextDouble() < 0.4)
                {
                    // A porch light on the front, so the house shows against the dark.
                    BuildKit.Box("PorchLamp", root, new Vector3(x + width * 0.3f, 2.3f, front - side * 0.06f), new Vector3(0.14f, 0.2f, 0.08f), lit, 1f, false);
                    Lamp(root, new Vector3(x + width * 0.3f, 2.3f, front - side * 0.6f), new Color(1f, 0.72f, 0.42f), 5f, 7f);
                }
            }

            // Puddles on the road and in the lot, mirroring the lamps and the headlights.
            Texture2D wet = BuildKit.SignTexture("decal_puddle");
            if (wet != null)
            {
                Material water = BuildKit.Decal("roadPuddle", wet, new Color(0.3f, 0.33f, 0.36f, 1f));
                water.SetFloat("_Smoothness", 0.97f);
                for (int i = 0; i < 26; i++)
                {
                    bool lot = i >= 20;
                    var at = lot ? new Vector3(-9f + (float)random.NextDouble() * 24f, -0.012f, -26.5f + (float)random.NextDouble() * 5f)
                                 : new Vector3(-215f + i * 10.5f + (float)random.NextDouble() * 6f, -0.012f, RoadZ + ((float)random.NextDouble() - 0.6f) * 6f);
                    var size = new Vector2(1.6f + (float)random.NextDouble() * 2.4f, 1.1f + (float)random.NextDouble() * 1.2f);
                    BuildKit.Quad("Puddle", root, at, size, 0f, water).transform.localRotation = Quaternion.Euler(90f, (float)random.NextDouble() * 180f, 0f);
                }
            }
        }

        /// <summary>A sodium street lamp: post, arm, head, and its light (in the outdoor lights).</summary>
        private void StreetLamp(Transform root, Vector3 p, float reach, Material metal, Material bulb, float intensity)
        {
            BuildKit.Cylinder("Post", root, p + new Vector3(0f, 3.4f, 0f), 0.18f, 6.8f, metal);
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
            light.intensity = intensity;
            light.color = new Color(1f, 0.6f, 0.32f);
            light.shadows = LightShadows.None;
            lights.Add(light);
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
                float width = 12f + (float)random.NextDouble() * 18f;
                float depth = 12f + (float)random.NextDouble() * 14f;
                // Keep the road clear, all the way out west where the intro's car comes from, and the
                // street in front of the pizzeria open: blocks too close are pushed back from it.
                float clearance = Mathf.Sqrt(width * width + depth * depth) * 0.5f + 30f;
                if (Mathf.Abs(center.z - RoadZ) < clearance && center.x > -320f)
                {
                    center.z = RoadZ + (center.z >= RoadZ ? clearance : -clearance);
                }
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
            main.maxParticles = 7000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.4f;
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = 4600f;
            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 40f, 0.5f);
            rainAnchor.localRotation = Quaternion.Euler(95f, 10f, 0f);   // falling down, a little slanted by the wind
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 6f;
            renderer.velocityScale = 0.02f;
            renderer.sharedMaterial = BuildKit.Additive("rainStreak", ProceduralTextures.Disc(16), new Color(0.65f, 0.7f, 0.8f, 0.5f));

            // Where the drops land: small rings on the ground that grow and fade, around the camera.
            var splashObject = new GameObject("Splashes");
            splashObject.transform.SetParent(root, false);
            splashes = splashObject.AddComponent<ParticleSystem>();
            splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule splashMain = splashes.main;
            splashMain.loop = true;
            splashMain.startLifetime = 0.22f;
            splashMain.startSpeed = 0f;
            splashMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            splashMain.startColor = new Color(0.75f, 0.8f, 0.9f, 0.5f);
            splashMain.maxParticles = 1500;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule splashRate = splashes.emission;
            splashRate.rateOverTime = 1300f;
            ParticleSystem.ShapeModule area = splashes.shape;
            area.shapeType = ParticleSystemShapeType.Box;
            area.scale = new Vector3(26f, 0f, 26f);
            ParticleSystem.SizeOverLifetimeModule grow = splashes.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));
            ParticleSystem.ColorOverLifetimeModule fadeOut = splashes.colorOverLifetime;
            fadeOut.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fadeOut.color = gradient;
            var splashRenderer = splashes.GetComponent<ParticleSystemRenderer>();
            splashRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            splashRenderer.sharedMaterial = BuildKit.Additive("rainSplash", ProceduralTextures.Ring(64, 0.12f), new Color(0.6f, 0.65f, 0.75f, 0.35f));
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
                RenderSettings.fogDensity = 0.014f;
                RenderSettings.ambientLight = new Color(0.09f, 0.095f, 0.13f);
                // Without a sky, the default reflection is a flat grey that turns wet asphalt and the
                // car white; the night has no sky to reflect, only the lights.
                RenderSettings.reflectionIntensity = 0.3f;
                camera.farClipPlane = 320f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = NightSky;
                rain.Play();
                splashes.Play();
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
                splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
                Vector3 ahead = camera.transform.position + Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) * 9f;
                splashes.transform.position = new Vector3(ahead.x, 0.03f, ahead.z);
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

    /// <summary>
    /// The guard's car, a 1984 station wagon (car_wagon.glb, or boxes if the model is missing): it
    /// drives, parks, turns its wheels and its steering wheel, lights its lamps with beams in the
    /// rain, and its wipers sweep the beads of rain off the windshield.
    /// </summary>
    public class Car : MonoBehaviour
    {
        private const float WipeSeconds = 1.3f;

        private readonly List<Transform> wheels = new List<Transform>();
        private readonly List<Light> headlights = new List<Light>();
        private readonly List<Renderer> lamps = new List<Renderer>();
        private Transform door;            // only the car made of boxes has a door that opens
        private Transform steering;
        private Quaternion steeringRest;
        private float wheelRadius = 0.34f;
        private Vector3 lastPosition;
        private Light dashboard;
        private readonly List<ParticleSystem> spray = new List<ParticleSystem>();

        // The windshield: two halves of beads, each wiped by its blade.
        private Bounds glass;
        private Transform[] wipers;
        private Material[] drops;
        private readonly float[] wet = { 1f, 1f };
        private float wipeClock;
        private float lastSweep;

        public bool HeadlightsOn { get; private set; } = true;

        /// <summary>Where the driver's eyes are, in the car's space.</summary>
        public Vector3 DriverEyes { get; private set; } = new Vector3(-0.38f, 1.12f, -0.15f);

        /// <summary>The steering wheel's center, in the car's space.</summary>
        public Vector3 SteeringCenter { get; private set; } = new Vector3(-0.38f, 0.98f, 0.38f);

        /// <summary>The windshield's box, in the car's space.</summary>
        public Bounds Windshield => glass;

        /// <summary>Rain beading on the windshield (the wipers sweep while it is on).</summary>
        public bool Wipers { get; set; }

        public static Car Create(Transform parent, GameAssets assets)
        {
            var car = new GameObject("Car").AddComponent<Car>();
            car.transform.SetParent(parent, false);
            if (assets != null && assets.CarModel != null)
            {
                car.BuildModel(assets.CarModel);
            }
            else
            {
                car.BuildBoxes();
            }
            car.BuildSpray();
            return car;
        }

        private void BuildModel(GameObject model)
        {
            GameObject body = Instantiate(model, transform, false);
            body.name = "Wagon";
            Renderer headlightLenses = null, brakeLights = null, windshield = null;
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                string name = renderer.name;
                if (name.Contains("WheelStock"))
                {
                    wheels.Add(renderer.transform);
                    wheelRadius = LocalBounds(renderer).extents.y;
                }
                else if (name.Contains("Steering_Wheel"))
                {
                    steering = renderer.transform;
                }
                else if (name.Contains("Headlights0"))
                {
                    headlightLenses = renderer;
                }
                else if (name.Contains("Brakelights"))
                {
                    brakeLights = renderer;
                }
                else if (name.Contains("Windshield"))
                {
                    windshield = renderer;
                }
                if (name.Contains("Glass") || name.Contains("Windshield"))
                {
                    // Wet, smudged glass: no mirror-sharp highlights of the lights inside the car.
                    foreach (Material material in renderer.materials)
                    {
                        material.SetFloat("roughnessFactor", 0.85f);
                        material.SetFloat("metallicFactor", 0f);
                    }
                }
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (steering != null)
            {
                steeringRest = steering.localRotation;
                SteeringCenter = transform.InverseTransformPoint(steering.position);
                DriverEyes = SteeringCenter + new Vector3(0f, 0.3f, -0.48f);
            }
            if (windshield != null)
            {
                glass = LocalBounds(windshield);
            }

            Material head = BuildKit.Glow("headlight", new Color(1f, 0.95f, 0.85f), 6f);
            Material tail = BuildKit.Glow("taillight", new Color(1f, 0.05f, 0.03f), 4f);
            if (headlightLenses != null)
            {
                Bounds lens = LocalBounds(headlightLenses);
                for (int side = -1; side <= 1; side += 2)
                {
                    var at = new Vector3(lens.center.x + side * (lens.extents.x - 0.14f), lens.center.y, lens.max.z + 0.006f);
                    lamps.Add(BuildKit.Quad("HeadlightGlow", transform, at, new Vector2(0.22f, 0.13f), 0f, head).GetComponent<Renderer>());
                    Headlight(at + new Vector3(0f, 0f, 0.08f), side);
                }
            }
            if (brakeLights != null)
            {
                Bounds lens = LocalBounds(brakeLights);
                for (int side = -1; side <= 1; side += 2)
                {
                    var at = new Vector3(lens.center.x + side * (lens.extents.x - 0.1f), lens.center.y, lens.min.z - 0.006f);
                    lamps.Add(BuildKit.Quad("TaillightGlow", transform, at, new Vector2(0.16f, 0.12f), 180f, tail).GetComponent<Renderer>());
                }
            }

            // The gauges' faint glow on the driver's hands and face.
            dashboard = new GameObject("Dashboard").AddComponent<Light>();
            dashboard.transform.SetParent(transform, false);
            // The glow of the gauges and the street on the wheel and dashboard, from over the driver's shoulder.
            dashboard.transform.localPosition = DriverEyes + new Vector3(0.12f, 0.18f, -0.05f);
            dashboard.transform.localRotation = Quaternion.LookRotation(SteeringCenter + new Vector3(0f, 0.05f, 0.15f) - dashboard.transform.localPosition);
            dashboard.type = LightType.Spot;
            dashboard.spotAngle = 75f;
            dashboard.range = 1.8f;
            dashboard.intensity = 2.2f;
            dashboard.color = new Color(0.75f, 0.8f, 1f);
            dashboard.shadows = LightShadows.None;

            Bounds all = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                all.Encapsulate(LocalBounds(renderer));
            }
            var solid = gameObject.AddComponent<BoxCollider>();
            solid.center = all.center + new Vector3(0f, 0.1f, 0f);
            solid.size = all.size - new Vector3(0.05f, 0.2f, 0.1f);
        }

        /// <summary>A headlight's beam: a spot light and a faint cone of light in the rain.</summary>
        private void Headlight(Vector3 at, int side)
        {
            var beam = new GameObject("Beam").AddComponent<Light>();
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = at;
            beam.transform.localRotation = Quaternion.Euler(5f, side * 3f, 0f);
            beam.type = LightType.Spot;
            beam.spotAngle = 58f;
            beam.innerSpotAngle = 22f;
            beam.range = 32f;
            beam.intensity = 16f;
            beam.color = new Color(1f, 0.93f, 0.8f);
            beam.shadows = LightShadows.Soft;
            headlights.Add(beam);
            const float length = 9f;
            Vector3 forward = Quaternion.Euler(5f, side * 3f, 0f) * Vector3.forward;
            GameObject cone = BuildKit.Cone("BeamCone", transform, at + forward * length, 1.6f, length,
                                            BuildKit.Additive("headlightBeam", BeamFade(), new Color(1f, 0.93f, 0.8f, 0.07f)));
            cone.transform.localRotation = Quaternion.FromToRotation(Vector3.up, -forward);
            lamps.Add(cone.GetComponent<Renderer>());
        }

        /// <summary>Bright at the lamp, gone at the far end (the cone's texture runs from its base, v 0, to its apex, v 1).</summary>
        private static Texture2D BeamFade()
        {
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BeamFade" };
            for (int y = 0; y < 64; y++)
            {
                float v = y / 63f;
                texture.SetPixel(0, y, Color.white * (v * v * v));
            }
            texture.Apply();
            return texture;
        }

        private Bounds LocalBounds(Renderer renderer)
        {
            Bounds world = renderer.bounds;
            var local = new Bounds(transform.InverseTransformPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? world.min.x : world.max.x, (i / 2) % 2 == 0 ? world.min.y : world.max.y, i < 4 ? world.min.z : world.max.z);
                local.Encapsulate(transform.InverseTransformPoint(corner));
            }
            return local;
        }

        /// <summary>The old car made of boxes, when the model is missing.</summary>
        private void BuildBoxes()
        {
            Transform t = transform;
            Material paint = BuildKit.Plain("carPaint", new Color(0.18f, 0.05f, 0.05f), 0.85f, 0.6f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            // See-through, so the shot from the driver's seat shows the street.
            Material see = BuildKit.Stain("carGlass", Texture2D.whiteTexture, new Color(0.06f, 0.08f, 0.1f, 0.5f));
            Material tire = BuildKit.Plain("tire", new Color(0.03f, 0.03f, 0.03f), 0.3f);
            Material head = BuildKit.Glow("headlight", new Color(1f, 0.95f, 0.85f), 6f);
            Material tail = BuildKit.Glow("taillight", new Color(1f, 0.05f, 0.03f), 5f);

            BuildKit.Box("Body", t, new Vector3(0f, 0.62f, 0f), new Vector3(1.8f, 0.62f, 4.5f), paint, 1f, false);
            BuildKit.Box("Hood", t, new Vector3(0f, 0.95f, 1.45f), new Vector3(1.76f, 0.06f, 1.5f), paint, 1f, false);
            BuildKit.Box("Trunk", t, new Vector3(0f, 0.95f, -1.75f), new Vector3(1.76f, 0.06f, 1f), paint, 1f, false);
            BuildKit.Box("Roof", t, new Vector3(0f, 1.42f, -0.25f), new Vector3(1.6f, 0.06f, 1.9f), paint, 1f, false);
            BuildKit.Box("Windshield", t, new Vector3(0f, 1.2f, 0.85f), new Vector3(1.58f, 0.5f, 0.04f), see, 1f, false).transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            BuildKit.Box("RearWindow", t, new Vector3(0f, 1.2f, -1.32f), new Vector3(1.58f, 0.48f, 0.04f), see, 1f, false).transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            BuildKit.Box("SideRight", t, new Vector3(0.79f, 1.18f, -0.25f), new Vector3(0.03f, 0.42f, 1.9f), see, 1f, false);
            BuildKit.Box("BumperFront", t, new Vector3(0f, 0.42f, 2.28f), new Vector3(1.84f, 0.16f, 0.1f), chrome, 1f, false);
            BuildKit.Box("BumperBack", t, new Vector3(0f, 0.42f, -2.28f), new Vector3(1.84f, 0.16f, 0.1f), chrome, 1f, false);
            BuildKit.Box("Seats", t, new Vector3(0f, 0.85f, -0.4f), new Vector3(1.5f, 0.5f, 1.2f), tire, 1f, false);
            BuildKit.Box("Dashboard", t, new Vector3(0f, 0.92f, 0.72f), new Vector3(1.56f, 0.22f, 0.4f), tire, 1f, false);
            steering = BuildKit.Cylinder("SteeringWheel", t, SteeringCenter, 0.36f, 0.03f, tire).transform;
            steering.localRotation = Quaternion.Euler(60f, 0f, 0f);
            steeringRest = steering.localRotation;

            door = new GameObject("DoorHinge").transform;
            door.SetParent(t, false);
            door.localPosition = new Vector3(-0.9f, 0f, 0.6f);
            BuildKit.Box("DoorPanel", door, new Vector3(0f, 0.7f, -0.6f), new Vector3(0.05f, 0.62f, 1.15f), paint, 1f, false);
            BuildKit.Box("DoorGlass", door, new Vector3(0.02f, 1.18f, -0.62f), new Vector3(0.03f, 0.42f, 1.0f), see, 1f, false);

            for (int i = 0; i < 4; i++)
            {
                var axle = new GameObject("Wheel").transform;
                axle.SetParent(t, false);
                axle.localPosition = new Vector3(i % 2 == 0 ? -0.82f : 0.82f, 0.34f, i < 2 ? 1.4f : -1.4f);
                var wheel = BuildKit.Cylinder("Tire", axle, Vector3.zero, 0.68f, 0.24f, tire);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                BuildKit.Cylinder("Hub", wheel.transform, new Vector3(0f, 1.02f, 0f), 0.6f, 0.05f, chrome);
                wheels.Add(axle);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                lamps.Add(BuildKit.Box("Headlight", t, new Vector3(side * 0.62f, 0.68f, 2.26f), new Vector3(0.38f, 0.16f, 0.06f), head, 1f, false).GetComponent<Renderer>());
                lamps.Add(BuildKit.Box("Taillight", t, new Vector3(side * 0.66f, 0.72f, -2.26f), new Vector3(0.32f, 0.14f, 0.06f), tail, 1f, false).GetComponent<Renderer>());
                Headlight(new Vector3(side * 0.62f, 0.7f, 2.4f), side);
            }
        }

        /// <summary>Mist thrown up behind the rear tires on the wet road (thicker the faster it goes).</summary>
        private void BuildSpray()
        {
            Material mist = BuildKit.Additive("tireSpray", ProceduralTextures.Disc(32), new Color(0.55f, 0.58f, 0.64f, 0.16f));
            foreach (Transform wheel in wheels)
            {
                Vector3 at = transform.InverseTransformPoint(wheel.position);
                if (at.z > 0f)
                {
                    continue;
                }
                var go = new GameObject("Spray");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(at.x, 0.1f, at.z - wheelRadius * 0.8f);
                go.transform.localRotation = Quaternion.Euler(-18f, 180f, 0f);
                var mistSystem = go.AddComponent<ParticleSystem>();
                mistSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = mistSystem.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.maxParticles = 300;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.25f;
                ParticleSystem.EmissionModule emission = mistSystem.emission;
                emission.rateOverTime = 0f;
                ParticleSystem.ShapeModule shape = mistSystem.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 28f;
                shape.radius = 0.12f;
                ParticleSystem.SizeOverLifetimeModule grow = mistSystem.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
                ParticleSystem.ColorOverLifetimeModule fade = mistSystem.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                 new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mist;
                mistSystem.Play();
                spray.Add(mistSystem);
            }
        }

        /// <summary>Puts the car somewhere, turning the wheels by the distance it moved.</summary>
        public void Place(Vector3 position, float yaw)
        {
            float moved = Vector3.Distance(position, lastPosition);
            lastPosition = position;
            float speed = moved < 5f && Time.deltaTime > 0f ? moved / Time.deltaTime : 0f;
            foreach (ParticleSystem mist in spray)
            {
                ParticleSystem.EmissionModule emission = mist.emission;
                emission.rateOverTime = speed > 1.5f ? speed * 12f : 0f;
            }
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float spin = moved < 5f ? moved / (wheelRadius * Mathf.PI * 2f) * 360f : 0f;
            foreach (Transform wheel in wheels)
            {
                wheel.Rotate(spin, 0f, 0f, Space.Self);
            }
        }

        /// <summary>Turns the steering wheel (degrees, positive to the right).</summary>
        public void Steer(float degrees)
        {
            if (steering != null)
            {
                steering.localRotation = door != null ? steeringRest * Quaternion.Euler(0f, degrees, 0f) : steeringRest * Quaternion.Euler(0f, 0f, -degrees);
            }
        }

        /// <summary>0 shut, 1 wide open (the model's doors are part of its body: only the box car's opens).</summary>
        public void SetDoor(float open)
        {
            if (door != null)
            {
                door.localRotation = Quaternion.Euler(0f, 70f * open, 0f);
            }
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
            if (dashboard != null)
            {
                dashboard.enabled = on;
            }
        }

        private void Update()
        {
            if (glass.size == Vector3.zero || (!Wipers && wipers == null))
            {
                return;
            }
            if (wipers == null)
            {
                BuildWindshieldRain();
            }
            for (int i = 0; i < 2; i++)
            {
                wet[i] = Mathf.Min(1f, wet[i] + Time.deltaTime * 0.8f);
            }
            // Up and back in one stroke, then a short rest; each half is wiped as its blade crosses it.
            float sweep = 0f;
            if (Wipers || wipeClock > 0f)
            {
                wipeClock += Time.deltaTime;
                float phase = wipeClock % WipeSeconds / WipeSeconds;
                sweep = phase < 0.85f ? Mathf.Sin(phase / 0.85f * Mathf.PI) : 0f;
                if (!Wipers && phase >= 0.85f)
                {
                    wipeClock = 0f;
                }
            }
            if (lastSweep < 0.5f && sweep >= 0.5f || lastSweep > 0.5f && sweep <= 0.5f)
            {
                wet[0] = wet[1] = 0f;
            }
            lastSweep = sweep;
            foreach (Transform wiper in wipers)
            {
                wiper.localRotation = Quaternion.Euler(0f, 0f, sweep * 98f);
            }
            for (int i = 0; i < 2; i++)
            {
                drops[i].SetColor("_BaseColor", new Color(1f, 1f, 1f, Wipers ? wet[i] * 0.9f : 0.9f));
            }
        }

        /// <summary>Beads on the inside face of the windshield's plane, and the two wiper blades lying on it.</summary>
        private void BuildWindshieldRain()
        {
            // The glass runs from its bottom front edge up and back to the roof.
            var up = new Vector3(0f, glass.size.y, -glass.size.z);
            float height = up.magnitude;
            up /= height;
            Vector3 outward = Vector3.Cross(Vector3.right, up);
            Quaternion plane = Quaternion.LookRotation(outward, up);
            Texture2D beads = BuildKit.SignTexture("glass_drops");
            drops = new Material[2];
            for (int i = 0; i < 2; i++)
            {
                drops[i] = new Material(BuildKit.Stain("windshieldDrops", beads != null ? beads : Texture2D.whiteTexture, Color.white));
                drops[i].SetTextureScale("_BaseMap", new Vector2(glass.size.x * 0.5f / 0.35f, height / 0.35f));
                drops[i].SetTextureOffset("_BaseMap", new Vector2(i * 0.5f, 0f));
                GameObject half = BuildKit.Quad("Drops", transform, Vector3.zero, new Vector2(glass.size.x * 0.48f, height * 0.94f), 0f, drops[i]);
                half.transform.localRotation = plane;
                half.transform.localPosition = glass.center + Vector3.right * ((i - 0.5f) * glass.size.x * 0.48f) - outward * 0.015f;
            }
            Material rubber = BuildKit.Plain("wiperBlade", new Color(0.02f, 0.02f, 0.02f), 0.25f);
            Vector3 bottom = new Vector3(glass.center.x, glass.min.y, glass.max.z) + up * 0.05f - outward * 0.01f;
            float blade = glass.size.x * 0.4f;
            wipers = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var pivot = new GameObject("WiperPivot").transform;
                pivot.SetParent(transform, false);
                pivot.localPosition = bottom + Vector3.right * ((i == 0 ? -0.36f : 0.08f) * glass.size.x);
                var arm = new GameObject("Wiper").transform;
                arm.SetParent(pivot, false);
                arm.localRotation = Quaternion.identity;
                pivot.localRotation = plane;
                BuildKit.Box("Blade", arm, new Vector3(blade * 0.5f, 0f, 0.012f), new Vector3(blade, 0.022f, 0.016f), rubber, 1f, false);
                wipers[i] = arm;
            }
        }
    }

    /// <summary>The alley's sodium lamp: a steady orange with, now and then, a stutter.</summary>
    public class SodiumFlicker : MonoBehaviour
    {
        private Light lamp;
        private float baseIntensity;
        private float nextStutter;
        private float stutterUntil;

        private void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            nextStutter = Time.time + Random.Range(4f, 12f);
        }

        private void Update()
        {
            if (Time.time >= nextStutter)
            {
                stutterUntil = Time.time + Random.Range(0.15f, 0.5f);
                nextStutter = Time.time + Random.Range(5f, 15f);
            }
            lamp.intensity = Time.time < stutterUntil && Random.value < 0.5f ? baseIntensity * 0.25f : baseIntensity;
        }
    }

    /// <summary>A window lit by a television: cold blue, changing every moment.</summary>
    public class TvGlow : MonoBehaviour
    {
        private Material screen;
        private float next;

        private void Start()
        {
            Renderer renderer = GetComponent<Renderer>();
            screen = BuildKit.Glow("tvWindow", new Color(0.4f, 0.55f, 1f), 1f);
            screen = new Material(screen);
            renderer.sharedMaterial = screen;
        }

        private void Update()
        {
            if (Time.time < next)
            {
                return;
            }
            next = Time.time + Random.Range(0.08f, 0.5f);
            screen.SetColor("_EmissionColor", new Color(0.35f, 0.5f, 1f) * Random.Range(0.2f, 1.1f));
        }
    }

    /// <summary>
    /// Lightning now and then: the sky flashes twice, and the thunder comes a moment later, from
    /// far away (it is heard inside the building).
    /// </summary>
    public class Lightning : MonoBehaviour
    {
        private Material sky;
        private AudioClip thunder;
        private Color baseColor;
        private float nextStrike;
        private float flashAt = -10f;

        public void Set(Material material, AudioClip clip)
        {
            sky = material;
            thunder = clip;
            baseColor = material.GetColor("_BaseColor");
            nextStrike = Time.time + Random.Range(15f, 35f);
        }

        private void Update()
        {
            if (sky == null)
            {
                return;
            }
            if (Time.time >= nextStrike)
            {
                flashAt = Time.time;
                nextStrike = Time.time + Random.Range(25f, 60f);
                if (thunder != null)
                {
                    StartCoroutine(Thunder(Random.Range(0.8f, 2.5f)));
                }
            }
            float t = Time.time - flashAt;
            // Two quick flashes, then the afterglow.
            float flash = t < 0.08f ? 1f : t < 0.16f ? 0.2f : t < 0.26f ? 0.8f : Mathf.Max(0f, 0.3f - (t - 0.26f));
            sky.SetColor("_BaseColor", baseColor * (1f + flash * 4f));
        }

        private System.Collections.IEnumerator Thunder(float delay)
        {
            yield return new WaitForSeconds(delay);
            AudioSource.PlayClipAtPoint(thunder, transform.position, 0.8f);
        }
    }
}
