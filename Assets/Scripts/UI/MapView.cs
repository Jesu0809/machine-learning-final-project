using System.Collections.Generic;
using NightShift.Game;
using NightShift.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// Top-down map of the pizzeria in the UI, drawn from <see cref="PizzeriaLayout"/>: rooms,
    /// corridors and markers placed by world position. Used by the AI inspector (live) and by the
    /// end screen (the replay of the hunt).
    /// </summary>
    public class MapView : MonoBehaviour
    {
        private RectTransform area;
        private readonly Image[] rooms = new Image[HuntMap.RoomCount];
        private readonly Text[] labels = new Text[HuntMap.RoomCount];
        private readonly Dictionary<string, List<Image>> dotGroups = new Dictionary<string, List<Image>>();
        private static Sprite disc;

        public static MapView Create(string name, Transform parent, Vector2 min, Vector2 max)
        {
            RectTransform holder = UiKit.Area(name, parent, min, max);
            var map = holder.gameObject.AddComponent<MapView>();
            map.area = UiKit.Area("Area", holder, Vector2.zero, Vector2.one);
            var fitter = map.area.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = PizzeriaLayout.Bounds.width / PizzeriaLayout.Bounds.height;

            foreach ((Vector2 start, Vector2 end, float _) in PizzeriaLayout.Corridors())
            {
                Vector2 a = Normalize(start);
                Vector2 b = Normalize(end);
                float w = PizzeriaLayout.CorridorWidth * 0.5f / PizzeriaLayout.Bounds.width;
                float h = PizzeriaLayout.CorridorWidth * 0.5f / PizzeriaLayout.Bounds.height;
                UiKit.Panel("Corridor", map.area, new Vector2(Mathf.Min(a.x, b.x) - w, Mathf.Min(a.y, b.y) - h),
                            new Vector2(Mathf.Max(a.x, b.x) + w, Mathf.Max(a.y, b.y) + h), new Color(0.12f, 0.13f, 0.13f, 1f));
            }
            for (int i = 0; i < HuntMap.RoomCount; i++)
            {
                Rect room = PizzeriaLayout.Area((Room)i);
                Vector2 min2 = Normalize(room.min);
                Vector2 max2 = Normalize(room.max);
                map.rooms[i] = UiKit.Panel(((Room)i).ToString(), map.area, min2, max2, new Color(0.1f, 0.11f, 0.11f, 1f));
                map.rooms[i].raycastTarget = false;
                Outline outline = map.rooms[i].gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.4f, 0.45f, 0.42f, 0.8f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                map.labels[i] = UiKit.Label("Label", map.rooms[i].transform, HuntMap.DisplayName((Room)i), 16, Palette.InkDim,
                                            TextAnchor.UpperLeft, new Vector2(0.04f, 0f), new Vector2(1f, 0.96f));
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

        public void Place(Graphic marker, Vector3 world)
        {
            Vector2 p = Normalize(new Vector2(world.x, world.z));
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

        private static Vector2 Normalize(Vector2 point)
        {
            Rect bounds = PizzeriaLayout.Bounds;
            return new Vector2((point.x - bounds.xMin) / bounds.width, (point.y - bounds.yMin) / bounds.height);
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
