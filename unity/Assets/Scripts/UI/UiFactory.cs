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
        private const int NoiseWidth = 320;
        private const int NoiseHeight = 180;

        private static Font font;
        private static Texture2D noise;
        private static Texture2D scanlines;
        private static Texture2D vignette;
        private static Texture2D verticalFade;

        /// <summary>The game font (VT323) from the art catalog, or Unity's built-in font as a fallback.</summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    NightArt art = NightArt.Load();
                    font = art != null && art.font != null ? art.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        /// <summary>An image showing <paramref name="sprite"/>; with <paramref name="keepAspect"/> it is letterboxed instead of stretched.</summary>
        public static Image CreateSprite(string name, Transform parent, Sprite sprite, Vector2 min, Vector2 max, bool keepAspect = false)
        {
            var image = CreateImage(name, parent, Color.white, min, max);
            image.sprite = sprite;
            image.preserveAspect = keepAspect;
            return image;
        }

        public static RawImage CreateRawImage(string name, Transform parent, Texture texture, Color color, Vector2 min, Vector2 max)
        {
            var rect = CreateArea(name, parent, min, max);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Gray random noise, tiled and scrolled to animate TV static.</summary>
        public static Texture2D NoiseTexture
        {
            get
            {
                if (noise == null)
                {
                    noise = new Texture2D(NoiseWidth, NoiseHeight, TextureFormat.RGBA32, false);
                    var random = new System.Random(7);
                    var pixels = new Color32[NoiseWidth * NoiseHeight];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        byte v = (byte)random.Next(256);
                        pixels[i] = new Color32(v, v, v, 255);
                    }
                    noise.SetPixels32(pixels);
                    noise.wrapMode = TextureWrapMode.Repeat;
                    noise.filterMode = FilterMode.Point;
                    noise.Apply();
                }
                return noise;
            }
        }

        /// <summary>Alternating dark and clear rows, tiled vertically for CRT scanlines.</summary>
        public static Texture2D ScanlineTexture
        {
            get
            {
                if (scanlines == null)
                {
                    scanlines = new Texture2D(1, 4, TextureFormat.RGBA32, false);
                    scanlines.SetPixels32(new[]
                    {
                        new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 160),
                        new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0)
                    });
                    scanlines.wrapMode = TextureWrapMode.Repeat;
                    scanlines.filterMode = FilterMode.Point;
                    scanlines.Apply();
                }
                return scanlines;
            }
        }

        /// <summary>Transparent center fading to black at the edges.</summary>
        public static Texture2D VignetteTexture
        {
            get
            {
                if (vignette == null)
                {
                    const int size = 256;
                    vignette = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    var pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x + 0.5f) / size * 2f - 1f;
                            float dy = (y + 0.5f) / size * 2f - 1f;
                            float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.2f);
                            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d));
                            pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255));
                        }
                    }
                    vignette.SetPixels32(pixels);
                    vignette.wrapMode = TextureWrapMode.Clamp;
                    vignette.Apply();
                }
                return vignette;
            }
        }

        /// <summary>Black at the bottom fading to transparent at the top.</summary>
        public static Texture2D VerticalFadeTexture
        {
            get
            {
                if (verticalFade == null)
                {
                    const int height = 64;
                    verticalFade = new Texture2D(1, height, TextureFormat.RGBA32, false);
                    var pixels = new Color32[height];
                    for (int y = 0; y < height; y++)
                    {
                        pixels[y] = new Color32(0, 0, 0, (byte)(255 * (1f - Mathf.SmoothStep(0f, 1f, y / (height - 1f)))));
                    }
                    verticalFade.SetPixels32(pixels);
                    verticalFade.wrapMode = TextureWrapMode.Clamp;
                    verticalFade.Apply();
                }
                return verticalFade;
            }
        }

        /// <summary>Moves a noise texture to a random offset so it looks like live static.</summary>
        public static void AnimateNoise(RawImage image, float tiles = 2f)
        {
            image.uvRect = new Rect(Random.value, Random.value, tiles, tiles * 0.5625f);
        }

        /// <summary>Converts a rectangle in image pixels (origin top-left) to anchors (origin bottom-left).</summary>
        public static void PlaceInPixels(RectTransform rect, float imageWidth, float imageHeight, float x, float y, float width, float height)
        {
            Place(rect,
                  new Vector2(x / imageWidth, 1f - (y + height) / imageHeight),
                  new Vector2((x + width) / imageWidth, 1f - y / imageHeight));
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
            colors.disabledColor = new Color(normal.r * 0.6f, normal.g * 0.6f, normal.b * 0.6f, normal.a * 0.5f);
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

        /// <summary>Formats night progress (0–1) as a camera timestamp, e.g. "02:47 AM".</summary>
        public static string FormatTimestamp(float progress)
        {
            int minutes = Mathf.Clamp(Mathf.FloorToInt(progress * 6f * 60f), 0, 6 * 60);
            int hour = minutes / 60;
            return (hour == 0 ? 12 : hour).ToString("00") + ":" + (minutes % 60).ToString("00") + " AM";
        }
    }
}
