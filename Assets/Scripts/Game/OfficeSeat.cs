using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The guard's chair in the office, the only seat in the building that can be used. E sits
    /// down facing the monitors: the view drops to a seated height and can look around within a
    /// half turn; E, a click or a movement key stands up again. During the phone call, with the
    /// office locked, it is the one thing to do besides listening.
    /// </summary>
    public class OfficeSeat : MonoBehaviour, IInteractable, IFocusTask
    {
        private const float EyeHeight = 1.18f;
        private const float LookDown = 14f;
        private const float MaxYaw = 75f;
        private const float MaxPitch = 35f;

        private HuntDirector director;
        private float yaw;
        private float pitch;
        private float seatedAt;

        public bool HoldToUse => false;
        public Transform FocusView { get; private set; }
        public string FocusHint => "Sitting at the monitors   [E] stand up";
        public string FocusControls => "[MOUSE] look around";

        public static OfficeSeat Create(HuntDirector director, Transform parent, Pose chair)
        {
            var go = new GameObject("OfficeSeat");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(chair.position, chair.rotation);
            var seat = go.AddComponent<OfficeSeat>();
            seat.director = director;
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.6f, 0f);
            trigger.size = new Vector3(0.6f, 1.2f, 0.6f);
            seat.FocusView = new GameObject("SeatedEyes").transform;
            seat.FocusView.SetParent(go.transform, false);
            seat.FocusView.localPosition = new Vector3(0f, EyeHeight, 0.05f);
            seat.Look(0f, 0f);
            return seat;
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || player.InFocus || player.Cell.Carrying)
            {
                return "";
            }
            return director.InCall ? "[E] Sit down and listen" : "[E] Sit down";
        }

        public void Use(PlayerController player)
        {
            if (player.HiddenIn != null || player.InFocus || player.Cell.Carrying)
            {
                return;
            }
            Look(0f, 0f);
            seatedAt = Time.time;
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.25f, 0.45f);
            player.BeginFocus(this);
        }

        public void FocusUpdate(FocusInput input)
        {
            Vector2 look = Controls.Look;
            Look(Mathf.Clamp(yaw + look.x, -MaxYaw, MaxYaw), Mathf.Clamp(pitch - look.y, -MaxPitch, MaxPitch));
            // Standing up: the use key, a click or a step (not the press that sat down).
            if ((input.Pressed || input.Step != Vector2Int.zero) && Time.time - seatedAt > 0.4f)
            {
                director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.2f, 0.5f);
                director.Player.EndFocus();
            }
        }

        public void FocusEnded()
        {
        }

        private void Look(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = newPitch;
            FocusView.localRotation = Quaternion.Euler(LookDown + pitch, yaw, 0f);
        }
    }
}
