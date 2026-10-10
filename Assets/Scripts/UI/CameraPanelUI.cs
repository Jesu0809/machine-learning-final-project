using System;
using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// Camera panel: one button per room. Shows the static image of the selected room and the
    /// monster sprite on top when the monster is there.
    /// </summary>
    public class CameraPanelUI : MonoBehaviour
    {
        [SerializeField] private NightController night;

        [Tooltip("Room images in RoomId order (Stage, Hall, WestCorridor, Kitchen, EastCorridor).")]
        [SerializeField] private Sprite[] roomSprites = new Sprite[RoomGraph.ObservableRoomCount];

        [SerializeField] private Image roomImage;
        [SerializeField] private GameObject monsterOverlay;

        /// <summary>Hooked to each room button (argument = room index 0–4).</summary>
        public void ShowRoom(int roomIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>Hooked to the "close cameras" button.</summary>
        public void ClosePanel()
        {
            throw new NotImplementedException();
        }
    }
}
