using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// A soft CRT look over every screen: faint scanlines and a light vignette. The pictures already
    /// have grain and a security-camera style, so this stays subtle.
    /// </summary>
    public class CrtOverlayUI : MonoBehaviour
    {
        public static CrtOverlayUI Create(Transform parent)
        {
            var holder = UiFactory.CreateArea("CrtOverlay", parent, Vector2.zero, Vector2.one);
            var overlay = holder.gameObject.AddComponent<CrtOverlayUI>();

            var lines = UiFactory.CreateRawImage("Scanlines", holder, UiFactory.ScanlineTexture, new Color(1f, 1f, 1f, 0.07f), Vector2.zero, Vector2.one);
            lines.uvRect = new Rect(0f, 0f, 1f, 270f);
            UiFactory.CreateRawImage("Vignette", holder, UiFactory.VignetteTexture, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, Vector2.one);
            return overlay;
        }
    }
}
