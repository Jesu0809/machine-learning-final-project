using System;
using NightShift.Core;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>Full-screen end of the night: jumpscare if the monster got in, "6 AM" if the guard survived.</summary>
    public class JumpscareUI : MonoBehaviour
    {
        [SerializeField] private NightController night;
        [SerializeField] private GameObject jumpscareScreen;
        [SerializeField] private GameObject survivedScreen;
        [SerializeField] private AudioSource screamAudio;

        /// <summary>Shows the screen that matches <paramref name="outcome"/>.</summary>
        public void Show(NightOutcome outcome)
        {
            throw new NotImplementedException();
        }
    }
}
