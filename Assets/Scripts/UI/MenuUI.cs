using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>Title screen: choose the monster (random, scripted or trained) and start a night.</summary>
    public class MenuUI : MonoBehaviour
    {
        private static readonly string[] MonsterNames = { "RANDOM", "SCRIPTED", "TRAINED (PPO)" };
        private static readonly string[] MonsterDescriptions =
        {
            "Moves to a random connected room every few seconds. Baseline.",
            "Always takes the shortest path to the office and ignores the cameras. Baseline.",
            "Neural network trained with reinforcement learning. Uses your camera habits."
        };

        private NightController night;
        private GameObject content;
        private Button[] monsterButtons;
        private Text description;

        public static MenuUI Create(Transform parent, NightController night)
        {
            var holder = UiFactory.CreateArea("Menu", parent, Vector2.zero, Vector2.one);
            var menu = holder.gameObject.AddComponent<MenuUI>();
            menu.night = night;

            var content = UiFactory.CreateFullScreen("Content", holder, NightColors.Background);
            menu.content = content.gameObject;
            Transform c = content.transform;

            UiFactory.CreateLabel("Title", c, "NIGHT SHIFT", 120, NightColors.Ink, TextAnchor.MiddleCenter,
                                  new Vector2(0f, 0.74f), new Vector2(1f, 0.92f), FontStyle.Bold);
            UiFactory.CreateLabel("Subtitle", c, "Survive until 6 AM. The monster learns where you don't look.", 32,
                                  NightColors.InkDim, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.67f), new Vector2(0.9f, 0.74f));

            UiFactory.CreateLabel("MonsterTitle", c, "MONSTER", 26, NightColors.InkDim, TextAnchor.MiddleCenter,
                                  new Vector2(0.3f, 0.58f), new Vector2(0.7f, 0.63f), FontStyle.Bold);
            menu.monsterButtons = new Button[MonsterNames.Length];
            for (int i = 0; i < MonsterNames.Length; i++)
            {
                MonsterKind kind = (MonsterKind)i;
                float x = 0.22f + i * 0.19f;
                menu.monsterButtons[i] = UiFactory.CreateButton("Monster" + i, c, MonsterNames[i], 26,
                                                                new Vector2(x, 0.49f), new Vector2(x + 0.17f, 0.57f),
                                                                () => night.SelectedMonster = kind);
            }
            menu.description = UiFactory.CreateLabel("Description", c, "", 24, NightColors.InkDim, TextAnchor.MiddleCenter,
                                                     new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.48f));

            var start = UiFactory.CreateButton("Start", c, "START NIGHT  [Enter]", 36, new Vector2(0.36f, 0.29f), new Vector2(0.64f, 0.38f), night.StartNight);
            UiFactory.SetButtonColors(start, NightColors.Accent);

            UiFactory.CreateLabel("Controls", c,
                                  "1–5  view a camera     Space  open/close cameras     A / D  left/right door\n" +
                                  "Doors and cameras drain power faster. If the power runs out, the doors open.",
                                  24, NightColors.InkDim, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.24f));
            UiFactory.CreateLabel("Footer", c, "Machine Learning · Universidad de Cundinamarca", 20, NightColors.InkDim,
                                  TextAnchor.MiddleCenter, new Vector2(0f, 0.02f), new Vector2(1f, 0.06f));
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
                UiFactory.SetButtonColors(monsterButtons[i], selected ? NightColors.Accent : NightColors.PanelLight);
            }

            description.text = night.HasTrainedModel || night.SelectedMonster != MonsterKind.Trained
                ? MonsterDescriptions[(int)night.SelectedMonster]
                : MonsterDescriptions[0];
            if (!night.HasTrainedModel)
            {
                description.text += "\n(The trained monster becomes available once its model is assigned.)";
            }
        }
    }
}
