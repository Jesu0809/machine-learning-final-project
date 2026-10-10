using System;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>Main menu: start a night and choose the monster (random, scripted or trained).</summary>
    public class MenuUI : MonoBehaviour
    {
        [SerializeField] private string nightSceneName = "Night";

        /// <summary>Hooked to the "Start night" button.</summary>
        public void StartNight()
        {
            throw new NotImplementedException();
        }

        /// <summary>Hooked to the monster selector (0 = random, 1 = scripted, 2 = trained).</summary>
        public void SelectMonster(int kind)
        {
            throw new NotImplementedException();
        }
    }
}
