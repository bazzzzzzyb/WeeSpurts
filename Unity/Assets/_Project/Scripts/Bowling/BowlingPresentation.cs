using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Player;

namespace WeeSpurts.Bowling
{
    /// <summary>
    /// THE LOCAL HALF of a bowling match: what THIS machine's player sees, hears
    /// and presses. Cameras, the thrower's Body English reaction, the Nuke's
    /// fireworks, the sandbox keys, and handing an avatar to and from the foul
    /// line.
    ///
    /// WHY THIS IS SPLIT FROM <see cref="BowlingMatchFlow"/>: read that class's
    /// comment first — it has the full reasoning. The short version is that this
    /// game is online-only and host-authoritative (Docs/Networking.md), so under
    /// Mirror the match state must run on the host while everything in THIS file
    /// runs on every machine. Nothing here may ever decide a score, a pin count
    /// or whose turn it is.
    ///
    /// THE INPUT GATE LIVES HERE, for the same reason: <see cref="IsMyAvatar"/>
    /// and <see cref="ThrowInputAllowed"/> are questions about the human sitting
    /// at THIS keyboard, which is a per-machine question, not match state.
    /// BallLauncher and BallConfigSwitcher both ask before touching Input.
    ///
    /// SETUP: sits on the "BowlingGame" GameObject next to BowlingMatchFlow.
    /// GreyboxSceneBuilder creates and wires both.
    /// </summary>
    [RequireComponent(typeof(BowlingMatchFlow))]
    public class BowlingPresentation : NetworkBehaviour
    {
        [Header("Wired by GreyboxSceneBuilder")]
        [SerializeField] private ThrowCamera throwCamera;

        [Tooltip("Optional. Any MonoBehaviour implementing IThrowReactionActor — the greybox capsule now, a real rig later. Drag any matching component in; leave empty to skip (e.g. scenes with no thrower actor yet).")]
        [SerializeField] private MonoBehaviour throwReactionBehaviour;
        private IThrowReactionActor _throwReaction;

        [Tooltip("Optional. Presentation layer for the Nuke Shot powerup. Leave empty in scenes that don't have one yet — a Nuke BallConfig just won't resolve until this is wired.")]
        [SerializeField] private NukeShotResolver nukeResolver;

        [Header("Match start (control modes)")]
        [Tooltip("SANDBOX FEEL-TESTING PATH: start the match the instant the scene loads, so you can throw immediately without walking to a lane kiosk first. DEFAULT TRUE so every existing scene behaves exactly as it did before roaming existed. RoamingSetupTool switches it OFF in the walkable venue scene, where the match is meant to start diegetically.")]
        [SerializeField] private bool sandboxAutoStart = true;

        [Tooltip("Optional. Where the active thrower stands at the foul line — the avatar is snapped here on their turn. Empty just means the avatar changes mode without being moved.")]
        [SerializeField] private Transform throwingStance;

        [Tooltip("Optional. Which avatar sandboxAutoStart hands the first turn to. Leave empty in scenes with no PlayerAvatar (e.g. BowlingAlley.unity) — the match then starts with nobody's mode touched, exactly as before.")]
        [SerializeField] private PlayerAvatar sandboxThrower;

        [Tooltip("Seconds of ←/→ steering lockout at the START of a player's turn, while the 'you're up' camera beat is looking BACK at the thrower and left/right therefore read mirrored ON SCREEN. DEFAULT 0 = off, i.e. steer immediately and live with the mirrored window. Set it to ThrowCameraSequenceConfig's ReturnDuration + ADuration (0.75 + 0.9 = 1.65 at the tuned defaults) to suppress steering until the shot swings down-lane, or 2.45 to also cover the swing itself. Power and spin are never locked. NOTE: this is unrelated to a NEGATIVE HalfLaneWidth, which inverts aim permanently — see BowlingMatchFlow.HalfLaneWidth.")]
        [SerializeField] private float turnStartSteeringLockSeconds = 0f;

        // The authoritative half. Same sibling-component pattern this class is
        // itself found by. Fetched in Awake (never Start) because Unity gives NO
        // ordering guarantee between two components' Start methods — by the time
        // either side's Start runs, both cross-references must already be valid.
        private BowlingMatchFlow _matchFlow;

        // Whoever is currently at the line. Stored so the match can hand control
        // back to them (EnterRoaming) when it ends.
        private PlayerAvatar _thrower;

        // "Is this MY avatar" — identity only, no turn concept. True in every scene
        // with no PlayerAvatar wired at all (today's sandbox/test scenes keep working
        // unchanged), and true when the wired avatar is the local player's.
        private bool IsMyAvatar => _thrower == null || _thrower.isLocalPlayer;

        // "Is this MY avatar AND is it MY turn" — the gate for anything that composes
        // or affects the actual throw (aim, spin, power, ball selection, frame reset).
        // Anchored on PlayerAvatar.ControlMode (WeeSpurts.Player namespace) rather than
        // inventing a parallel "whose turn" concept: the active thrower is the only
        // avatar BowlingPresentation.StartMatch ever puts into ControlMode.Bowling.
        public bool ThrowInputAllowed => IsMyAvatar && (_thrower == null || _thrower.Mode == ControlMode.Bowling);

        /// <summary>
        /// Passthroughs to the match state, so a consumer that only wants to know
        /// "can a match be started here" (LaneKioskInteractable) needs ONE
        /// reference instead of two. Anything that reads scores or turns should
        /// go to BowlingMatchFlow directly rather than growing this list.
        /// </summary>
        public bool MatchInProgress => _matchFlow.MatchInProgress;

        /// <inheritdoc cref="MatchInProgress"/>
        public bool MatchOver => _matchFlow.MatchOver;

        /// <summary>
        /// How long ←/→ steering is ignored at the start of a NEW player's turn.
        /// BowlingMatchFlow asks for this number when it opens an aim phase; the
        /// knob lives here because it is tuned against the camera beats. See
        /// BallLauncher.SteeringLocked for the whole story.
        /// </summary>
        public float SteeringLockSecondsForNewTurn => turnStartSteeringLockSeconds;

        private void Awake()
        {
            _matchFlow = GetComponent<BowlingMatchFlow>();
            // The match can end on its own (last frame resolved), and when it
            // does the player needs their legs back. Match flow raises the
            // event; deciding what that MEANS for an avatar is our job.
            _matchFlow.OnMatchComplete += HandleMatchComplete;
        }

        private void OnDestroy()
        {
            // Belt and braces: today the two components live and die on the same
            // GameObject, so this cannot leak. It is here so the subscription has
            // a visible end — the day match flow moves (a network object, a
            // persistent manager), a handler left attached to something that
            // outlives this component is a genuinely nasty bug to find.
            // "-=" on a handler that was never added is a safe no-op.
            if (_matchFlow != null) _matchFlow.OnMatchComplete -= HandleMatchComplete;
        }

        public void Configure(ThrowCamera throwCam)
        {
            throwCamera = throwCam;
        }

        /// <summary>Wires the optional thrower reaction actor (GreyboxSceneBuilder pattern, same as SetNukeResolver).</summary>
        public void SetThrowReactionActor(MonoBehaviour actor) => throwReactionBehaviour = actor;

        /// <summary>Wires the optional Nuke Shot presentation layer (same pattern as SetThrowReactionActor).</summary>
        public void SetNukeResolver(NukeShotResolver resolver) => nukeResolver = resolver;

        private void Start()
        {
            // Unity can't serialize an interface field directly, so
            // throwReactionBehaviour is serialized as a MonoBehaviour and
            // cast here. "as" on a null reference just yields null, so an
            // unassigned slot safely resolves to no-op via the ?. below.
            _throwReaction = throwReactionBehaviour as IThrowReactionActor;

            _matchFlow.Initialize();

            // Split in two so that a walkable-alley scene can wire everything up
            // at load and only actually START the match when a player walks up
            // to the lane (Docs/OpenQuestions.md — game start is diegetic, not a
            // menu button). LaneKioskInteractable is what calls the seam.
            if (sandboxAutoStart) StartMatch(sandboxThrower);
        }

        /// <summary>
        /// Begin a match, optionally taking control of an avatar and putting
        /// them at the line. Public because the walkable alley starts matches
        /// from outside (a player walking up to the lane); the sandbox path
        /// calls it from Start.
        ///
        /// The avatar handoff happens HERE and the match state starts in
        /// BowlingMatchFlow — same order as before the split, just across two
        /// classes.
        /// </summary>
        public void StartMatch(PlayerAvatar thrower)
        {
            // Idempotency is checked HERE TOO, not only in match flow, and the
            // order matters: before the split, the guard ran BEFORE the avatar
            // handoff. Without this line a second trigger mid-match (a player
            // walking back up to the kiosk) would drag them to the foul line for
            // a match that then correctly refuses to restart — leaving them
            // stranded in bowling mode. Match flow still re-checks; it is the
            // authority, this is the local half declining to do the work.
            if (_matchFlow.MatchInProgress) return;

            _thrower = thrower;
            // The avatar owns its own mode — we ask, we don't reach in and flip
            // components ourselves (see PlayerAvatar's class comment). Null is
            // fine: scenes with no avatar just start the match as they always did.
            if (_thrower != null) _thrower.EnterBowling(throwingStance);

            _matchFlow.StartMatch();
        }

        /// <summary>
        /// SPIKE Step 4 (Docs/spikes/MirrorKcpSpikeStatus.md): the networked
        /// entry point for "which avatar is the thrower", called by the host
        /// after PlayerAvatar.CmdRequestStartBowling asks on the owning
        /// client's behalf. Runs identically on every machine — Mirror
        /// resolves the PlayerAvatar parameter to the matching spawned
        /// instance locally (WriteNetworkBehaviour/ReadNetworkBehaviour,
        /// NetworkWriterExtensions.cs:272 / NetworkReaderExtensions.cs:246),
        /// so every machine's own StartMatch(thrower) call agrees on WHO.
        /// Lives here, not on BowlingMatchFlow, because that class's own
        /// class comment makes "nothing in here knows what a PlayerAvatar is"
        /// a structural rule — this class already holds PlayerAvatar
        /// references (_thrower, sandboxThrower), so the network entry point
        /// for one belongs here too.
        /// </summary>
        [ClientRpc]
        public void RpcStartMatchWith(PlayerAvatar thrower) => StartMatch(thrower);

        private void HandleMatchComplete()
        {
            // Give the player their legs back rather than leaving them stranded
            // at the foul line with no camera control. R still reloads the scene
            // for a rematch, exactly as before.
            if (_thrower != null) _thrower.EnterRoaming();
        }

        private void Update()
        {
            // Rematch: reloading the scene is the simplest reliable reset —
            // every object comes back in a known-good state. Gated on identity
            // ONLY (IsMyAvatar), NOT the full ThrowInputAllowed gate: by the time
            // MatchOver is true, HandleMatchComplete has already called
            // _thrower.EnterRoaming(), so the avatar's Mode is back to Roaming,
            // not Bowling. There is no "turn" left once the match has ended, so
            // requiring ThrowInputAllowed here would make R permanently
            // unpressable in any scene with an avatar wired.
            if (MatchOver && IsMyAvatar && Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);

            // Sandbox: quick-reset just the CURRENT frame (re-rack, same
            // player, same frame) without ending the match. For retrying one
            // throw setup — e.g. bounce/spin feel-testing — without playing
            // back through pin counting each time. Not available once the
            // match is over; R already covers a full restart there.
            //
            // MatchInProgress is also required now that a match no longer always
            // auto-starts: without it, tapping F while walking around the venue
            // would call BeginRoll and open an aim phase behind your back, with
            // nobody at the line.
            //
            // ThrowInputAllowed (the FULL gate, identity AND turn) because this
            // resets the CURRENT throw setup for whoever is at the line right
            // now — unlike R, this is only meaningful mid-turn.
            if (!MatchOver && MatchInProgress && ThrowInputAllowed && Input.GetKeyDown(KeyCode.F))
                _matchFlow.ResetCurrentFrame();
        }

        // ---------- hooks called BY BowlingMatchFlow ----------
        // Each one is a straight lift of a line the old single class called
        // inline. They exist so match flow can say WHAT should be shown without
        // holding a reference to a camera or a resolver.

        /// <summary>
        /// Hard-resets the Nuke's visual state at the start of every roll, so an
        /// interrupted nuke sequence never bleeds a stuck sphere or a looping
        /// poof into the next one. No-ops via ?. in scenes with no resolver.
        /// </summary>
        public void ResetNukeVisualState(BowlingBall ball) => nukeResolver?.ResetVisualState(ball);

        /// <summary>Puts the camera back in the aim framing for a new roll.</summary>
        public void SnapCameraToAimView() => throwCamera.SnapToAimView();

        /// <summary>
        /// The ball has just left the hand: play the thrower's Body English and
        /// hand the camera the ball. Both are cosmetic and both must happen the
        /// instant of release, not after the pins are counted — hence one call
        /// in this order rather than two separate hooks match flow could get out
        /// of order.
        /// </summary>
        public void OnThrowLaunched(LaunchParameters p)
        {
            _throwReaction?.PlayReaction(p);
            throwCamera.FollowBall();
        }

        /// <summary>
        /// The Nuke's up/lock-on/rocket-down spectacle. Returns the resolver's
        /// own coroutine so match flow can `yield return` it and stay in step;
        /// the camera reference is supplied here rather than travelling through
        /// match flow, which holds no camera at all.
        /// </summary>
        public IEnumerator PlayNukeHitSequence(BallConfig nukeConfig, Vector3 spawnPos,
                                               Vector3 pinCenter, PinDeck pinDeck, BowlingBall ball) =>
            nukeResolver.PlayHitSequence(nukeConfig, spawnPos, pinCenter, pinDeck, ball, throwCamera);

        /// <summary>The Nuke's in-hand fizzle. Same shape as PlayNukeHitSequence.</summary>
        public IEnumerator PlayNukeMissSequence(BallConfig nukeConfig, Vector3 spawnPos, BowlingBall ball) =>
            nukeResolver.PlayMissSequence(nukeConfig, spawnPos, ball, throwCamera);
    }
}
