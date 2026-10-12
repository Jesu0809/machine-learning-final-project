using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>Colors of the interface: dim greens of an old security terminal, with a red accent.</summary>
    public static class Palette
    {
        public static readonly Color Ink = new Color(0.86f, 0.9f, 0.82f);
        public static readonly Color InkDim = new Color(0.55f, 0.62f, 0.55f);
        public static readonly Color Accent = new Color(0.85f, 0.18f, 0.15f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
        public static readonly Color Panel = new Color(0.03f, 0.04f, 0.04f, 0.88f);
        public static readonly Color Button = new Color(0.09f, 0.11f, 0.1f, 0.95f);
    }

    /// <summary>Helpers that build uGUI elements in code (no prefabs to keep in sync).</summary>
    public static class UiKit
    {
        public static Font Font;

        public static Canvas CreateCanvas(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
        }

        public static RectTransform Area(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            RectTransform rect = Area(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Image Picture(string name, Transform parent, Vector2 min, Vector2 max, Texture2D texture, Color color)
        {
            Image image = Panel(name, parent, min, max, color);
            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor,
                                 Vector2 min, Vector2 max)
        {
            RectTransform rect = Area(name, parent, min, max);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            return label;
        }

        public static Button Button(string name, Transform parent, string text, int size, Vector2 min, Vector2 max, UnityAction action)
        {
            Image image = Panel(name, parent, min, max, Palette.Button);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(action);
            Label("Label", image.transform, text, size, Palette.Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return button;
        }

        public static void SetButtonText(Button button, string text)
        {
            button.GetComponentInChildren<Text>().text = text;
        }

        public static void Tint(Button button, Color color)
        {
            button.GetComponent<Image>().color = color;
        }
    }
}
