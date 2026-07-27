using UnityEngine;

namespace WeeSpurts.Interaction
{
    /// <summary>
    /// Draws the "[E] Start Game" line for whatever the player is currently
    /// looking at, and nothing at all otherwise.
    ///
    /// DELIBERATELY UGLY, and for exactly the same reasons as
    /// <see cref="WeeSpurts.UI.DebugHud"/>: immediate-mode OnGUI needs no
    /// Canvas, no EventSystem, no font asset, no prefab and no scene wiring, so
    /// it adds nothing to the two files most likely to cause a merge conflict
    /// between two people (scenes and prefabs — CLAUDE.md's "#1 two-person
    /// Unity hazard"). The real, good-looking prompt is part of the HUD pass at
    /// Roadmap [5] and will replace this whole file. Do not invest in it.
    ///
    /// SETUP: on the Player root, next to <see cref="PlayerInteractor"/>.
    /// RoamingSetupTool adds and wires it.
    ///
    /// THIS COMPONENT IS NEVER TOGGLED BY MODE. It reads the interactor's
    /// enabled state instead. That is a deliberate choice so that
    /// PlayerAvatar.ApplyMode gains exactly ONE new line rather than two:
    /// ApplyMode is the single chokepoint for mode-owned components and the
    /// fewer things it has to remember, the better it holds. The rule still
    /// holds — the interactor is mode-owned, and this HUD is just a window onto
    /// it, so it goes quiet automatically the moment the interactor does.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptHud : MonoBehaviour
    {
        [Tooltip("The interactor whose current target we draw. Normally the PlayerInteractor on this same object. When it is disabled (i.e. you are bowling, or this is a remote player) this HUD draws nothing.")]
        [SerializeField] private PlayerInteractor interactor;

        // Layout, in screen pixels. Centre-bottom: low enough to be out of the
        // way of what you are looking at, high enough not to fight the taskbar
        // or a future drink meter.
        private const float PromptWidth = 420f;
        private const float PromptHeight = 34f;
        private const float BottomMargin = 96f;

        // Built lazily inside OnGUI because GUI.skin is only valid there —
        // touching it from Awake throws. Cached because allocating a GUIStyle
        // every frame is the classic OnGUI performance mistake.
        private GUIStyle _style;

        private void OnGUI()
        {
            // isActiveAndEnabled, not just enabled: it also covers the whole
            // Player object being deactivated, which `enabled` alone would miss.
            if (interactor == null || !interactor.isActiveAndEnabled) return;

            IInteractable target = interactor.Current;

            // Re-check liveness. Current was chosen during Update; OnGUI can run
            // several times per frame and something could have been destroyed in
            // between. See PlayerInteractor.IsAlive for why a plain null check
            // through an interface reference is not enough.
            if (!PlayerInteractor.IsAlive(target)) return;

            string action = target.GetPrompt(interactor.Avatar);
            // An interactable is allowed to say "show nothing" by returning an
            // empty string, and we honour it rather than drawing an empty box.
            if (string.IsNullOrEmpty(action)) return;

            // THE KEY PREFIX IS COMPOSED HERE, not by the interactable. This is
            // the one place that knows which key actually works, so "[E]" can
            // never drift from the binding — and when rebinding or controller
            // support lands (Docs/UI.md), every prompt in the venue updates
            // because of this line, without touching a single interactable.
            KeyCode key = interactor.InteractKey;
            string prompt = key != KeyCode.None ? $"[{key}] {action}" : action;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };

            var rect = new Rect((Screen.width - PromptWidth) * 0.5f,
                                Screen.height - BottomMargin - PromptHeight,
                                PromptWidth, PromptHeight);

            GUI.Box(rect, prompt, _style);
        }
    }
}
