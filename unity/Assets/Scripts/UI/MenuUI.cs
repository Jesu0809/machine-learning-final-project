using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// Title screen: Barnaby "breathing" (his idle poses alternate with a soft fade), the monster
    /// selector (random, scripted or trained) and the start button.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private const float PoseSeconds = 0.9f;
        private const float PoseFade = 0.35f;

        private static readonly string[] MonsterNames = { "RANDOM", "SCRIPTED", "TRAINED (PPO)" };
        private static readonly string[] MonsterDescriptions =
        {
            "Moves to a random connected room every few seconds. Baseline.",
            "Always takes the shortest path to the office and ignores the cameras. Baseline.",
            "Neural network trained with reinforcement learning. Uses your camera habits."
        };
        // Breathing cycle over the three idle poses.
        private static readonly int[] PoseOrder = { 0, 1, 2, 1 };

        private NightController night;
        private NightArt art;
        private GameObject content;
        private Button[] monsterButtons;
        private Text[] monsterLabels;
        private Text description;
        private Image poseBack;
        private Image poseFront;
        private int poseStep;
        private float poseChangedAt;
        private RawImage glitch;
        private float glitchUntil;
        private float nextGlitchAt;

        public static MenuUI Create(Transform parent, NightController night, NightArt art)
        {
            var holder = UiFactory.CreateArea("Menu", parent, Vector2.zero, Vector2.one);
            var menu = holder.gameObject.AddComponent<MenuUI>();
            menu.night = night;
            menu.art = art;

            var content = UiFactory.CreateFullScreen("Content", holder, new Color32(6, 6, 9, 255));
            menu.content = content.gameObject;
            Transform c = content.transform;

            // Barnaby on the right, fading into the dark at his feet.
            menu.poseBack = UiFactory.CreateSprite("PoseBack", c, art.monsterIdle[0], new Vector2(0.56f, 0f), new Vector2(0.97f, 0.97f), keepAspect: true);
            menu.poseFront = UiFactory.CreateSprite("PoseFront", c, art.monsterIdle[0], new Vector2(0.56f, 0f), new Vector2(0.97f, 0.97f), keepAspect: true);
            menu.poseBack.color = new Color(0.8f, 0.8f, 0.85f, 1f);
            UiFactory.CreateRawImage("FeetShade", c, UiFactory.VerticalFadeTexture, Color.white, new Vector2(0.5f, 0f), new Vector2(1f, 0.32f));
            menu.glitch = UiFactory.CreateRawImage("Glitch", c, UiFactory.NoiseTexture, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.one);
            menu.nextGlitchAt = Time.unscaledTime + 2.5f;

            UiFactory.CreateLabel("Title", c, "NIGHT SHIFT", 190, NightColors.Ink, TextAnchor.LowerLeft,
                                  new Vector2(0.05f, 0.74f), new Vector2(0.7f, 0.95f));
            UiFactory.CreateLabel("Subtitle", c, "Survive until 6 AM. Barnaby learns where you don't look.", 40,
                                  NightColors.InkDim, TextAnchor.UpperLeft, new Vector2(0.055f, 0.66f), new Vector2(0.6f, 0.74f));

            UiFactory.CreateLabel("MonsterTitle", c, "MONSTER", 34, NightColors.InkDim, TextAnchor.LowerLeft,
                                  new Vector2(0.055f, 0.58f), new Vector2(0.4f, 0.63f));
            menu.monsterButtons = new Button[MonsterNames.Length];
            menu.monsterLabels = new Text[MonsterNames.Length];
            for (int i = 0; i < MonsterNames.Length; i++)
            {
                MonsterKind kind = (MonsterKind)i;
                float y = 0.5f - i * 0.085f;
                menu.monsterButtons[i] = UiFactory.CreateButton("Monster" + i, c, MonsterNames[i], 40,
                                                                new Vector2(0.055f, y), new Vector2(0.33f, y + 0.07f),
                                                                () => night.SelectedMonster = kind);
                menu.monsterLabels[i] = UiFactory.GetLabel(menu.monsterButtons[i]);
                menu.monsterLabels[i].alignment = TextAnchor.MiddleLeft;
                menu.monsterLabels[i].rectTransform.offsetMin = new Vector2(24f, 0f);
            }
            menu.description = UiFactory.CreateLabel("Description", c, "", 32, NightColors.InkDim, TextAnchor.UpperLeft,
                                                     new Vector2(0.055f, 0.23f), new Vector2(0.52f, 0.32f));

            var start = UiFactory.CreateButton("Start", c, "START NIGHT  [ENTER]", 48, new Vector2(0.055f, 0.12f), new Vector2(0.38f, 0.21f), night.StartNight);
            UiFactory.SetButtonColors(start, NightColors.Accent);

            UiFactory.CreateLabel("Controls", c,
                                  "Mouse: look around    1–5: cameras    Space: open/close cameras    A / D: doors\n" +
                                  "Doors and cameras drain power. If the power runs out, the doors open.",
                                  28, NightColors.InkDim, TextAnchor.LowerLeft, new Vector2(0.055f, 0.02f), new Vector2(0.75f, 0.1f));
            return menu;
        }

        private void Update()
        {
            if (night.Phase == NightPhase.Menu && KeyboardInput.WasPressed(GameKey.Confirm))
            {
                night.StartNight();
            }
            Refresh();
        }

        public void Refresh()
        {
            bool visible = night.Phase == NightPhase.Menu;
            content.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (night.SelectedMonster == MonsterKind.Trained && !night.HasTrainedModel)
            {
                night.SelectedMonster = MonsterKind.Random;
            }

            for (int i = 0; i < monsterButtons.Length; i++)
            {
                bool selected = (int)night.SelectedMonster == i;
                monsterButtons[i].interactable = (MonsterKind)i != MonsterKind.Trained || night.HasTrainedModel;
                UiFactory.SetButtonColors(monsterButtons[i], selected ? NightColors.Accent : new Color(0.1f, 0.11f, 0.14f, 1f));
                monsterLabels[i].text = (selected ? "> " : "  ") + MonsterNames[i];
            }

            description.text = MonsterDescriptions[(int)night.SelectedMonster];
            if (!night.HasTrainedModel)
            {
                description.text += "\nThe trained monster unlocks once its model is assigned.";
            }

            AnimatePose();
            AnimateGlitch();
        }

        private void AnimateGlitch()
        {
            float now = Time.unscaledTime;
            if (now >= nextGlitchAt)
            {
                glitchUntil = now + Random.Range(0.06f, 0.16f);
                nextGlitchAt = now + Random.Range(3f, 7f);
            }
            glitch.color = new Color(1f, 1f, 1f, now < glitchUntil ? 0.08f : 0f);
            UiFactory.AnimateNoise(glitch, 2f);
        }

        private void AnimatePose()
        {
            float now = Time.unscaledTime;
            if (now - poseChangedAt >= PoseSeconds)
            {
                poseChangedAt = now;
                poseBack.sprite = poseFront.sprite;
                poseStep = (poseStep + 1) % PoseOrder.Length;
                poseFront.sprite = art.monsterIdle[PoseOrder[poseStep]];
            }
            float alpha = Mathf.Clamp01((now - poseChangedAt) / PoseFade);
            poseFront.color = new Color(0.8f, 0.8f, 0.85f, alpha);
        }
    }
}
