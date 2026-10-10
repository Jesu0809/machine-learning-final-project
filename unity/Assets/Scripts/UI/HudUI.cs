using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>Shows the in-game hour (12 AM – 6 AM) and the remaining power.</summary>
    public class HudUI : MonoBehaviour
    {
        [SerializeField] private NightController night;
        [SerializeField] private Text clockText;
        [SerializeField] private Text powerText;
        [SerializeField] private Slider powerBar;
    }
}
