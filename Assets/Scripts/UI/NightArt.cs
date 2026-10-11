using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// Every image, sound and font the Night scene uses. The asset lives in
    /// Assets/Resources/NightArt.asset and is filled by "Night Shift/Rebuild Art Catalog".
    /// </summary>
    [CreateAssetMenu(menuName = "Night Shift/Night Art", fileName = "NightArt")]
    public class NightArt : ScriptableObject
    {
        public const string ResourcePath = "NightArt";

        [Header("Cameras, in RoomId order: Stage, Main Hall, West Hall, Kitchen, East Hall")]
        public Sprite[] cameraEmpty = new Sprite[5];
        public Sprite[] cameraMonster = new Sprite[5];

        [Header("Office (21:9, panned with the mouse)")]
        public Sprite office;
        public Sprite officePowerOut;
        public Sprite leftDoorClosed;
        public Sprite rightDoorClosed;
        public Sprite monsterLeftDoor;
        public Sprite monsterRightDoor;

        [Header("Monster")]
        public Sprite[] monsterIdle = new Sprite[3];
        public Sprite[] jumpscare = new Sprite[3];

        [Header("Text")]
        public Font font;

        [Header("Audio")]
        public AudioClip staticLoop;
        public AudioClip cameraSwitch;
        public AudioClip ambientHum;
        public AudioClip doorSlide;
        public AudioClip doorSlam;
        public AudioClip powerOut;
        public AudioClip scream;

        private static NightArt cached;

        /// <summary>The catalog from Resources, or null if it has not been built yet.</summary>
        public static NightArt Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<NightArt>(ResourcePath);
            }
            return cached;
        }
    }
}
