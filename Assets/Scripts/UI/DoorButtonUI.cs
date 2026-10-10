using System;
using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>Button that opens and closes one office door and shows its state.</summary>
    public class DoorButtonUI : MonoBehaviour
    {
        [SerializeField] private NightController night;
        [SerializeField] private DoorSide side;
        [SerializeField] private Image doorImage;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private Sprite closedSprite;

        /// <summary>Hooked to the button's OnClick.</summary>
        public void Toggle()
        {
            throw new NotImplementedException();
        }
    }
}
