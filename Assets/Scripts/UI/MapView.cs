using System.Collections.Generic;
using NightShift.Game;
using NightShift.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// Top-down map of the pizzeria in the UI, drawn from <see cref="PizzeriaLayout"/>: the upper
    /// floor on the left and the ground floor on the right, with rooms, corridors, staircases and
    /// the air ducts (dotted), and markers placed by world position on the floor they are on. Used
    /// by the AI inspector (live) and by the end screen (the replay of the hunt).
    /// </summary>
    public class MapView : MonoBehaviour
    {
        /// <summary>Meters between the two floor plans.</summary>
        private const float Gap = 3f;

        private RectTransform area;
        private readonly Image[] rooms = new Image[HuntMap.RoomCount];
        private readonly Text[] labels = new Text[HuntMap.RoomCount];
        private readonly Dictionary<string, List<Image>> dotGroups = new Dictionary<string, List<Image>>();
        private static Sprite disc;

        private static float LayoutWidth => PizzeriaLayout.Bounds.width * 2f + Gap;

        public static MapView Create(string name, Transform parent, Vector2 min, Vector2 max)
        {
            RectTransform holder = UiKit.Area(name, parent, min, max);
            var map = holder.gameObject.AddComponent<MapView>();
            map.area = UiKit.Area("Area", holder, Vector2.zero, Vector2.one);
            var fitter = map.area.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = LayoutWidth / PizzeriaLayout.Bounds.height;

            UiKit.Label("Upper", map.area, "UPPER FLOOR", 15, Palette.Gold, TextAnchor.UpperLeft, Normalize(new Vector2(PizzeriaLayout.Bounds.xMin, PizzeriaLayout.Bounds.yMax - 2.5f), true),
                        Normalize(new Vector2(PizzeriaLayout.Bounds.xMax, PizzeriaLayout.Bounds.yMax), true));
            UiKit.Label("Ground", map.area, "GROUND FLOOR", 15, Palette.Gold, TextAnchor.UpperLeft, Normalize(new Vector2(PizzeriaLayout.Bounds.xMin, PizzeriaLayout.Bounds.yMax - 2.5f), false),
                        Normalize(new Vector2(PizzeriaLayout.Bounds.xMax, PizzeriaLayout.Bounds.yMax), false));

            foreach ((Vector3 start, Vector3 end, float _) in PizzeriaLayout.Corridors())
            {
                bool upper = start.y > 1f;
                Vector2 a = Normalize(new Vector2(start.x, start.z), upper);
                Vector2 b = Normalize(new Vector2(end.x, end.z), upper);
                float w = PizzeriaLayout.CorridorWidth * 0.5f / LayoutWidth;
                float h = PizzeriaLayout.CorridorWidth * 0.5f / PizzeriaLayout.Bounds.height;
                UiKit.Panel("Corridor", map.area, new Vector2(Mathf.Min(a.x, b.x) - w, Mathf.Min(a.y, b.y) - h),
                            new Vector2(Mathf.Max(a.x, b.x) + w, Mathf.Max(a.y, b.y) + h), new Color(0.12f, 0.13f, 0.13f, 1f));
            }
            for (int i = 0; i < HuntMap.RoomCount; i++)
            {
                var room = (Room)i;
                Rect rect = PizzeriaLayout.Area(room);
                bool upper = HuntMap.IsUpstairs(room);
                map.rooms[i] = UiKit.Panel(room.ToString(), map.area, Normalize(rect.min, upper), Normalize(rect.max, upper), new Color(0.1f, 0.11f, 0.11f, 1f));
                map.rooms[i].raycastTarget = false;
                Outline outline = map.rooms[i].gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.4f, 0.45f, 0.42f, 0.8f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                map.labels[i] = UiKit.Label("Label", map.rooms[i].transform, HuntMap.DisplayName(room), 14, Palette.InkDim,
                                            TextAnchor.UpperLeft, new Vector2(0.04f, 0f), new Vector2(1f, 0.96f));
            }

            // Staircases: drawn on both floors, where they leave and where they arrive.
            foreach (Staircase stairs in PizzeriaLayout.Stairs)
            {
                foreach (bool upper in new[] { false, true })
                {
                    Vector3 from = upper ? stairs.Top : stairs.Bottom;
                    Vector3 to = upper ? new Vector3(stairs.Landing.center.x, 0f, stairs.Landing.center.y) : stairs.Top;
                    var a = Normalize(new Vector2(Mathf.Min(from.x, to.x), Mathf.Min(from.z, to.z)) - Vector2.one * (stairs.Width * 0.5f), upper);
                    var b = Normalize(new Vector2(Mathf.Max(from.x, to.x), Mathf.Max(from.z, to.z)) + Vector2.one * (stairs.Width * 0.5f), upper);
                    Image steps = UiKit.Panel("Stairs", map.area, a, b, new Color(0.55f, 0.42f, 0.2f, 0.55f));
                    steps.raycastTarget = false;
                    UiKit.Label("StairsLabel", steps.transform, "STAIRS", 11, Palette.Gold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
                }
            }

            // Air ducts: dotted, on the floor each stretch runs along.
            var ductColor = new Color(0.3f, 0.8f, 0.85f, 0.65f);
            foreach (DuctPath duct in PizzeriaLayout.Ducts)
            {
                var points = new List<Vector3>();
                for (int i = 0; i + 1 < duct.Points.Length; i++)
                {
                    Vector3 a = duct.Points[i];
                    Vector3 b = duct.Points[i + 1];
                    int steps = Mathf.Max(1, Mathf.RoundToInt(new Vector2(b.x - a.x, b.z - a.z).magnitude / 1.2f));
                    for (int s = 0; s <= steps; s++)
                    {
                        points.Add(Vector3.Lerp(a, b, s / (float)steps));
                    }
                }
                map.Dots("duct" + duct.A + duct.B, points, ductColor, 4f);
            }
            return map;
        }

        public void SetRoom(Room room, Color fill, string label, Color outline)
        {
            rooms[(int)room].color = fill;
            labels[(int)room].text = label;
            rooms[(int)room].GetComponent<Outline>().effectColor = outline;
        }

        /// <summary>A round marker; move it with <see cref="Place"/>.</summary>
        public Image Marker(string name, Color color, float size)
        {
            var image = UiKit.Panel(name, area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), color);
            image.sprite = Disc();
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Puts a marker over a world position, on the plan of the floor it is on.</summary>
        public void Place(Graphic marker, Vector3 world)
        {
            Vector2 p = Normalize(new Vector2(world.x, world.z), IsUpper(world));
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = p;
            marker.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>Draws a list of positions as dots (trails, planned paths), reusing the dots of the group.</summary>
        public void Dots(string group, IList<Vector3> points, Color color, float size, int step = 1)
        {
            if (!dotGroups.TryGetValue(group, out List<Image> dots))
            {
                dots = new List<Image>();
                dotGroups[group] = dots;
            }
            int used = 0;
            for (int i = 0; i < points.Count; i += step)
            {
                if (used == dots.Count)
                {
                    dots.Add(Marker(group, color, size));
                }
                Image dot = dots[used++];
                dot.gameObject.SetActive(true);
                dot.color = color;
                dot.rectTransform.sizeDelta = new Vector2(size, size);
                Place(dot, points[i]);
            }
            for (int i = used; i < dots.Count; i++)
            {
                dots[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Above the middle of the staircases counts as upstairs.</summary>
        private static bool IsUpper(Vector3 world) => world.y > PizzeriaLayout.UpperFloor * 0.6f;

        private static Vector2 Normalize(Vector2 point, bool upper)
        {
            Rect bounds = PizzeriaLayout.Bounds;
            float offset = upper ? 0f : bounds.width + Gap;
            return new Vector2((point.x - bounds.xMin + offset) / LayoutWidth, (point.y - bounds.yMin) / bounds.height);
        }

        private static Sprite Disc()
        {
            if (disc == null)
            {
                Texture2D texture = ProceduralTextures.Disc(32);
                disc = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f));
            }
            return disc;
        }
    }
}
