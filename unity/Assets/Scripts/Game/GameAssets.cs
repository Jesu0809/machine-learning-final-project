using Unity.InferenceEngine;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// References to the imported assets the game builds itself from. One instance lives in
    /// Assets/Resources/GameAssets.asset (created by "Night Shift/Create Game Scene").
    /// </summary>
    /// <summary>The voice clips of one night's phone call.</summary>
    [System.Serializable]
    public class NightCall
    {
        public AudioClip[] Lines;
    }

    [CreateAssetMenu(menuName = "Night Shift/Game Assets")]
    public class GameAssets : ScriptableObject
    {
        public GameObject FreddyPrefab;

        [Tooltip("Trained brain. Without it Freddy uses the scripted baseline.")]
        public ModelAsset FreddyModel;

        [Tooltip("Weights of the same model, so the AI inspector can show its action probabilities.")]
        public TextAsset PolicyWeights;

        [Tooltip("Material templates the level copies, so builds keep the shader variants it needs.")]
        public Material[] MaterialTemplates;

        [Tooltip("Scanned PBR surfaces (Assets/Art/Surfaces, made by Night Shift > Import Surfaces).")]
        public Material[] Surfaces;

        /// <summary>A scanned 3D prop (Assets/Art/Models, made by Night Shift > Import Models).</summary>
        [System.Serializable]
        public class Prop
        {
            public string Name;
            public GameObject Model;
            public Material Material;
        }

        [Tooltip("Scanned 3D props the level places by name.")]
        public Prop[] Props;

        /// <summary>
        /// The glass of the television_02 model, measured in the model's own space when the props
        /// are imported (see PropImporter.MeasureScreen); the monitors' live feeds are laid on it.
        /// Size zero if it was not found.
        /// </summary>
        public Bounds TvScreen;

        [Tooltip("Signs and posters drawn by tools/generate_signs.py (Assets/Art/Signs).")]
        public Texture2D[] Signs;

        [Tooltip("Screen-space ambient occlusion on the renderer, switched by the graphics quality.")]
        public UnityEngine.Rendering.Universal.ScriptableRendererFeature AmbientOcclusion;

        [Tooltip("Training curve (copied from web/static/data) shown in the menu's ML explanation.")]
        public TextAsset TrainingCurve;

        [Tooltip("Evaluation results (copied from web/static/data) shown in the menu's ML explanation.")]
        public TextAsset EvaluationResults;

        public Font Font;

        [Tooltip("Controls diagrams (tools/generate_controls_diagram.py).")]
        public Texture2D ControlsKeyboard;
        public Texture2D ControlsGamepad;

        [Tooltip("Neon signs on the front of the pizzeria (tools/generate_exterior_art.py).")]
        public Texture2D SignMain;
        public Texture2D SignPole;

        [Header("Sketchfab models (tools/prepare_models.py, imported by glTFast; credits in docs/credits.md)")]
        [Tooltip("The guard's 1984 station wagon: wheels and steering wheel are nodes pivoting on their centers.")]
        public GameObject CarModel;

        [Tooltip("A rusted sedan left in the back lot.")]
        public GameObject WreckModel;

        [Tooltip("The exit panel's lever: \"Base\" and \"Handle\" (pivot on the hub, axis x).")]
        public GameObject LeverModel;

        [Tooltip("The breaker box's main knife switch: \"Base\" and \"Blade\" (pivot on the hinge, axis x).")]
        public GameObject KnifeSwitchModel;

        [Tooltip("First-person arms, rigged.")]
        public GameObject HandsModel;

        public AudioClip AmbientLoop;
        public AudioClip MenuMusic;
        public AudioClip Scream;
        public AudioClip LockerDoor;
        public AudioClip ExitDoor;
        public AudioClip FusePickup;
        public AudioClip RainLoop;
        public AudioClip Thunder;
        public AudioClip CarEngine;
        public AudioClip VoicemailBeep;

        [Tooltip("Intro narration, one clip per line of StoryData.Intro.")]
        public AudioClip[] IntroLines;

        [Tooltip("The phone call of each night, one clip per line of StoryData.Calls.")]
        public NightCall[] Calls;

        public static GameAssets Load() => Resources.Load<GameAssets>("GameAssets");
    }
}
