using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.UI
{
    /// <summary>
    /// The spin selector: a circular ball diagram with a draggable dot, shown
    /// during the AIM phase. Where you put the dot on the ball's face is the
    /// spin you throw with — centre is none.
    ///
    ///   left/right  = side spin (the hook)
    ///   up          = topspin  (grips, curves early, straighter)
    ///   down        = backspin (skids, then breaks late and hard)
    ///
    /// Drag with the mouse, or nudge with I/J/K/L and re-centre with C (that
    /// half lives in BallLauncher, since it's input state, not pixels).
    ///
    /// Deliberately OnGUI and deliberately ugly: same reasoning as DebugHud —
    /// no Canvas, no fonts, no prefab wiring, nothing to merge-conflict on. The
    /// real HUD (Roadmap [5], uGUI per Docs/UI.md) replaces this. What must NOT
    /// be thrown away with it is the interaction: round clamping, a visible
    /// centre, and a keyboard path — Docs/UI.md requires every interactive
    /// element to be controller-navigable, and this maps 1:1 onto a stick.
    ///
    /// This widget owns no state. It reads BallLauncher.CurrentSpin and writes
    /// back through SetSpin, so the launcher stays the single source of truth
    /// and the throw can never disagree with the dot.
    ///
    /// SETUP: same GameObject as BowlingGameController (GreyboxSceneBuilder
    /// adds it, alongside DebugHud).
    /// </summary>
    [RequireComponent(typeof(BowlingGameController))]
    public class SpinSelectorHud : MonoBehaviour
    {
        [Tooltip("Diameter of the spin ball on screen, in pixels.")]
        // [Min]: a zero size would make the radius 0 and ApplyMouse divide by
        // it, feeding NaN into CurrentSpin and from there into a PhysX force.
        [Min(20f)]
        [SerializeField] private float widgetSize = 150f;

        [Tooltip("Distance from the bottom-left corner of the screen, in pixels.")]
        [SerializeField] private Vector2 screenMargin = new Vector2(20f, 20f);

        private BowlingGameController _game;

        // Built once, lazily, because OnGUI has no other sane place to make a
        // texture and a 64px disc is not worth an asset file.
        private Texture2D _ballTex;
        private Texture2D _dotTex;

        // True only between a mouse-down INSIDE the circle and the matching
        // mouse-up. Without this, dragging off the widget and releasing over the
        // scorecard would leave the dot stuck to the cursor.
        private bool _dragging;

        private void Awake() => _game = GetComponent<BowlingGameController>();

        private void OnDestroy()
        {
            // Textures made with `new` aren't garbage collected with the
            // component — they leak until domain reload without this.
            if (_ballTex != null) Destroy(_ballTex);
            if (_dotTex != null) Destroy(_dotTex);
        }

        private void OnGUI()
        {
            BallLauncher launcher = _game.Launcher;
            if (launcher == null || !launcher.IsAiming)
            {
                // Cleared HERE and not only in HandleMouse, because this branch
                // returns before HandleMouse ever runs. An aim phase that ends
                // mid-drag would otherwise leave the grab latched until the next
                // mouse-up the widget happens to see.
                _dragging = false;
                return;
            }

            EnsureTextures();

            float radius = widgetSize * 0.5f;
            // GUI space has y=0 at the TOP, so "bottom-left corner" means
            // subtracting from Screen.height. Every conversion below has to
            // remember this, which is why the flip is isolated in one place.
            var rect = new Rect(screenMargin.x,
                                Screen.height - widgetSize - screenMargin.y,
                                widgetSize, widgetSize);
            Vector2 centre = rect.center;

            HandleMouse(launcher, centre, radius);

            // Greyed out once the player commits to the power meter — the throw
            // is locked to what was dialled, and showing it live-but-dead would
            // invite fiddling that does nothing.
            Color prev = GUI.color;
            GUI.color = launcher.CanEditSpin ? Color.white : new Color(1f, 1f, 1f, 0.4f);

            GUI.DrawTexture(rect, _ballTex);

            Vector2 spin = launcher.CurrentSpin;
            // +Y is UP in spin space but DOWN in GUI space: hence the minus.
            var dotPos = new Vector2(centre.x + spin.x * radius,
                                     centre.y - spin.y * radius);
            const float DotSize = 18f;
            GUI.DrawTexture(new Rect(dotPos.x - DotSize * 0.5f, dotPos.y - DotSize * 0.5f,
                                     DotSize, DotSize), _dotTex);

            GUI.color = prev;

            GUI.Box(new Rect(rect.x, rect.yMax + 2f, widgetSize, 22f),
                    launcher.CanEditSpin ? "SPIN — drag / IJKL / C" : "SPIN locked");
        }

        /// <summary>
        /// Mouse drag → spin, clamped to the circle. Only reacts to events
        /// inside the widget, so clicking anywhere else on screen is ignored.
        /// </summary>
        private void HandleMouse(BallLauncher launcher, Vector2 centre, float radius)
        {
            Event e = Event.current;
            if (e == null) return;

            if (!launcher.CanEditSpin)
            {
                _dragging = false;
                return;
            }

            // The authoritative "is the button actually still down" check.
            // EventType.MouseUp only arrives if the release happens over the
            // Game view — release after dragging onto the Inspector, or off the
            // window entirely, and the widget never hears about it. Without
            // this, the grab stays latched: the next click-and-drag ANYWHERE on
            // screen would teleport the dot to the cursor and start tracking it,
            // and the player would throw with spin they never dialled.
            if (!Input.GetMouseButton(0)) _dragging = false;

            switch (e.type)
            {
                case EventType.MouseDown:
                    // Left button only — a right- or middle-click drag across the
                    // widget should not set spin.
                    if (e.button != 0) break;

                    // Grab only if the press LANDED on the ball, so the widget
                    // doesn't hijack clicks meant for the rest of the screen.
                    if ((e.mousePosition - centre).magnitude <= radius)
                    {
                        _dragging = true;
                        ApplyMouse(launcher, e.mousePosition, centre, radius);
                        e.Use();
                    }
                    else
                    {
                        // A press that missed is an explicit "not dragging this",
                        // and is the cheapest place to unstick a latched grab.
                        _dragging = false;
                    }
                    break;

                case EventType.MouseDrag:
                    if (_dragging && e.button == 0)
                    {
                        ApplyMouse(launcher, e.mousePosition, centre, radius);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_dragging)
                    {
                        _dragging = false;
                        e.Use();
                    }
                    break;
            }
        }

        private static void ApplyMouse(BallLauncher launcher, Vector2 mouse, Vector2 centre, float radius)
        {
            Vector2 offset = mouse - centre;
            // Flip Y back out of GUI space, normalize to -1..1, and let
            // SetSpin do the circular clamp — dragging outside the ball slides
            // along the rim instead of stopping dead or reaching a square corner.
            launcher.SetSpin(new Vector2(offset.x / radius, -offset.y / radius));
        }

        /// <summary>
        /// Draws the ball diagram and the dot into small textures once. A
        /// procedural disc rather than an imported sprite keeps this file
        /// self-contained — nothing to import, nothing to wire, and no missing
        /// asset on a fresh clone.
        /// </summary>
        private void EnsureTextures()
        {
            if (_ballTex != null && _dotTex != null) return;

            _ballTex = MakeDisc(96, new Color(0.16f, 0.18f, 0.24f, 0.92f),
                                    new Color(0.85f, 0.85f, 0.9f, 1f), crosshair: true);
            _dotTex = MakeDisc(24, new Color(1f, 0.85f, 0.15f, 1f),
                                   new Color(0.2f, 0.15f, 0f, 1f), crosshair: false);
        }

        private static Texture2D MakeDisc(int size, Color fill, Color edge, bool crosshair)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color[size * size];
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r + 0.5f;
                    float dy = y - r + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    Color c;
                    if (d > r) c = Color.clear;                    // outside the ball
                    else if (d > r - 2f) c = edge;                 // rim
                    else if (crosshair && (Mathf.Abs(dx) < 0.9f || Mathf.Abs(dy) < 0.9f))
                        c = edge * 0.7f;                           // centre cross = "no spin"
                    else c = fill;

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            // Clamp, or the disc's transparent edge wraps and smears a faint
            // line across the opposite side when GUI scales the texture up.
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}
