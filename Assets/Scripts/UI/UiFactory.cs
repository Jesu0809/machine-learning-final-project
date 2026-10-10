using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>Colors shared by every screen of the night.</summary>
    public static class NightColors
    {
        public static readonly Color Background = new Color32(10, 12, 18, 255);
        public static readonly Color Wall = new Color32(24, 27, 36, 255);
        public static readonly Color Panel = new Color32(17, 20, 28, 245);
        public static readonly Color PanelLight = new Color32(38, 44, 60, 255);
        public static readonly Color PanelHover = new Color32(56, 64, 86, 255);
        public static readonly Color Ink = new Color32(222, 226, 234, 255);
        public static readonly Color InkDim = new Color32(138, 146, 164, 255);
        public static readonly Color Accent = new Color32(224, 49, 59, 255);
        public static readonly Color Warning = new Color32(242, 163, 58, 255);
        public static readonly Color Ok = new Color32(86, 196, 122, 255);
        public static readonly Color Steel = new Color32(92, 100, 116, 255);
        public static readonly Color MonsterBody = new Color32(14, 10, 20, 255);
        public static readonly Color MonsterEyes = new Color32(255, 40, 40, 255);
    }

    /// <summary>
    /// Builds uGUI elements from code, so the Night scene needs no manual layout. Positions use
    /// anchors in [0, 1] relative to the parent, which keeps the layout correct at any resolution.
    /// </summary>
    public static class UiFactory
    {
        private static Font font;

        /// <summary>Unity's built-in font, available without importing anything.</summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors <paramref name="rect"/> to the area between <paramref name="min"/> and <paramref name="max"/> of its parent.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform CreateArea(string name, Transform parent, Vector2 min, Vector2 max)
        {
            return Place(CreateRect(name, parent), min, max);
        }

        public static Image CreateImage(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var rect = CreateArea(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateFullScreen(string name, Transform parent, Color color)
        {
            var image = CreateImage(name, parent, color, Vector2.zero, Vector2.one);
            // Blocks clicks on whatever is behind it.
            image.raycastTarget = true;
            return image;
        }

        public static Text CreateLabel(string name, Transform parent, string text, int size, Color color,
                                       TextAnchor alignment, Vector2 min, Vector2 max, FontStyle style = FontStyle.Normal)
        {
            var rect = CreateArea(name, parent, min, max);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button CreateButton(string name, Transform parent, string text, int size,
                                          Vector2 min, Vector2 max, UnityAction onClick)
        {
            var image = CreateImage(name, parent, Color.white, min, max);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            SetButtonColors(button, NightColors.PanelLight);
            button.onClick.AddListener(() =>
            {
                // Deselect, otherwise Space/Enter would "click" the last button again.
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
                onClick();
            });

            CreateLabel("Label", image.transform, text, size, NightColors.Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return button;
        }

        /// <summary>Recolors a button; used to highlight the selected option.</summary>
        public static void SetButtonColors(Button button, Color normal)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.25f);
            colors.selectedColor = normal;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        public static Text GetLabel(Button button)
        {
            return button.GetComponentInChildren<Text>();
        }

        /// <summary>Formats an in-game hour (0–6) as a clock: 0 is 12 AM.</summary>
        public static string FormatHour(int hour)
        {
            return (hour == 0 ? 12 : hour) + " AM";
        }
    }
}
