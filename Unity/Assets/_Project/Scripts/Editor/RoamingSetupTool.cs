using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Bowling;
using WeeSpurts.Environment;
using WeeSpurts.Interaction;
using WeeSpurts.Player;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK turns the OPEN scene's static bowling thrower into a walkable
    /// first-person player, without hand-editing a single line of scene YAML
    /// (CLAUDE.md's hardest Unity rule).
    ///
    /// Menu: WeeSpurts -> Set Up Roaming Player (Current Scene)
    /// Target scene: Assets/_Project/Scenes/TestVenue.unity
    ///
    /// It builds this, reusing the character that is already in the scene:
    ///
    ///   Player                  &lt;- NEW, UNSCALED, at the Thrower's world position
    ///     |- Thrower            &lt;- the EXISTING PlayerCharacter instance, reparented
    ///     |- FirstPersonCamera  &lt;- NEW
    ///
    /// THE SINGLE MOST IMPORTANT DETAIL: the CharacterController goes on the new
    /// `Player` root and NEVER on the Thrower. The PlayerCharacter prefab root
    /// carries localScale 0.4 (CharacterSetupTool.CharacterDisplayScale), and a
    /// CharacterController's radius/height are scaled by transform.lossyScale —
    /// so putting it on the Thrower would silently give you a 0.70m-tall,
    /// 0.12m-wide controller and every collision number in the venue would be a
    /// lie. The Player root is deliberately left at scale 1.
    ///
    /// IDEMPOTENT: safe to run again after a scene change. It reuses an existing
    /// Player root, camera and stance marker rather than stacking duplicates,
    /// and never overwrites RoamConfig.asset once it exists.
    ///
    /// It marks the scene DIRTY but does NOT save it — same precedent as
    /// AlleyGreyboxBuilder.BuildIntoCurrentScene. Look at what it did, then save.
    /// </summary>
    public static class RoamingSetupTool
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string RoamConfigPath = ProjectRoot + "/ScriptableObjects/RoamConfig.asset";
        private const string InteractionConfigPath = ProjectRoot + "/ScriptableObjects/InteractionConfig.asset";
        private const string AlleyLayoutPath = ProjectRoot + "/ScriptableObjects/AlleyLayout.asset";

        private const string PlayerRootName = "Player";
        private const string FirstPersonCameraName = "FirstPersonCamera";
        private const string ThrowingStanceName = "ThrowingStance";

        /// <summary>
        /// Which lane the kiosk goes on, if AlleyLayout.asset can't be found to
        /// ask. Mirrors AlleyLayoutConfig.PlayableLaneIndex's own default — the
        /// asset is the source of truth and this is only a last resort.
        /// </summary>
        private const int FallbackPlayableLaneIndex = 6;

        /// <summary>The menu item that creates the Anchor_LaneNN markers this tool looks for.</summary>
        private const string VenueBuilderMenuItem = "WeeSpurts -> Build Alley Venue Greybox";

        /// <summary>
        /// The user layer the local player's own body lives on, so the
        /// first-person camera can cull it while every other camera still sees
        /// it. Unity has no API to CREATE a user layer, so this has to be added
        /// by hand once — the tool checks and tells you exactly what to do.
        /// </summary>
        private const string LocalPlayerModelLayer = "LocalPlayerModel";

        [MenuItem("WeeSpurts/Set Up Roaming Player (Current Scene)")]
        public static void SetUp()
        {
            Scene scene = SceneManager.GetActiveScene();

            // ----- 0. Layer check FIRST, so aborting leaves the scene untouched -----
            // (The plan listed this check later; doing it first is what makes
            // "abort cleanly" actually clean — a half-built hierarchy would be
            // worse than no hierarchy.)
            int modelLayer = LayerMask.NameToLayer(LocalPlayerModelLayer);
            if (modelLayer < 0)
            {
                Debug.LogError(
                    "[Roaming] MISSING LAYER — nothing was changed.\n" +
                    $"This scene needs a user layer called '{LocalPlayerModelLayer}'.\n" +
                    "FIX IT LIKE THIS: Edit -> Project Settings -> Tags and Layers, " +
                    $"type '{LocalPlayerModelLayer}' into User Layer 6, then run this menu item again.\n" +
                    "WHY: your own character model stands where your first-person camera is, so " +
                    "without a layer to cull you would spend the whole game looking at the inside " +
                    "of your own chest. The model itself stays visible to every OTHER camera — " +
                    "this is a camera concern, not a 'hide the player' concern.");
                return;
            }

            // ----- 1. Find the character already in the scene -----
            CharacterThrowReactionActor reactionActor = FindFirst<CharacterThrowReactionActor>();
            if (reactionActor == null)
            {
                Debug.LogError(
                    "[Roaming] No thrower found — nothing was changed.\n" +
                    "This tool needs a GameObject with a CharacterThrowReactionActor on it (the " +
                    "PlayerCharacter prefab instance, normally named 'Thrower').\n" +
                    "FIX IT LIKE THIS: run WeeSpurts -> Set Up Player Character, then " +
                    "WeeSpurts -> Build Greybox Bowling Scene, and open the scene that has the lane in it.");
                return;
            }
            GameObject thrower = reactionActor.gameObject;

            // ----- 2. Find the bowling camera, and report anything ambiguous -----
            Camera bowlingCamera = FindBowlingCameraAndReport(scene, out string cameraReport);
            if (bowlingCamera == null)
            {
                Debug.LogError(
                    "[Roaming] No bowling camera found — nothing was changed.\n" +
                    "This tool identifies it as 'the Camera that has a ThrowCamera component'.\n" +
                    "FIX IT LIKE THIS: run WeeSpurts -> Build Greybox Bowling Scene, or open the " +
                    "scene that contains the lane.\n" + cameraReport);
                return;
            }

            // ----- 3. RoamConfig asset (created once, never stomped after) -----
            bool roamConfigAlreadyExisted = AssetDatabase.LoadAssetAtPath<RoamConfig>(RoamConfigPath) != null;
            RoamConfig roamConfig = LoadOrCreateAsset<RoamConfig>(RoamConfigPath);
            if (!roamConfigAlreadyExisted)
            {
                // A freshly created asset already carries every default from
                // RoamConfig's own C# field initialisers, so there is nothing to
                // stamp. This branch exists so that any future creation-time
                // default has an obvious home that can NEVER run over an
                // existing asset and stomp Tony's tuning — the same
                // create-once rule as Wobbler/Nuke in GreyboxSceneBuilder.
                EditorUtility.SetDirty(roamConfig);
            }

            // ----- 3b. InteractionConfig asset (same create-once rule) -----
            bool interactionConfigAlreadyExisted =
                AssetDatabase.LoadAssetAtPath<InteractionConfig>(InteractionConfigPath) != null;
            InteractionConfig interactionConfig = LoadOrCreateAsset<InteractionConfig>(InteractionConfigPath);
            if (!interactionConfigAlreadyExisted) EditorUtility.SetDirty(interactionConfig);

            // ----- 4. The Player root -----
            PlayerAvatar existingAvatar = FindFirst<PlayerAvatar>();
            GameObject playerRoot;
            if (existingAvatar != null)
            {
                // Re-run: keep the root exactly where it is. Tony may have
                // dragged it somewhere deliberately.
                playerRoot = existingAvatar.gameObject;
            }
            else
            {
                playerRoot = new GameObject(PlayerRootName);
                // Inherit the thrower's pose so NOTHING visibly moves on the
                // first run — the character stays exactly where the greybox
                // builder put it, it just gains a parent.
                playerRoot.transform.SetPositionAndRotation(thrower.transform.position,
                                                            thrower.transform.rotation);
                // Explicit, even though it is the default for a new GameObject:
                // this scale being 1 is the whole reason the CharacterController
                // lives here. See the class comment.
                playerRoot.transform.localScale = Vector3.one;
            }

            // Reparent the character under the root, pinned to the origin. From
            // here on the root IS the player's position and the model just rides
            // along (except while ThrowerAimSlide is sliding it during an aim —
            // PlayerAvatar zeroes that offset again on the way back to roaming).
            if (thrower.transform.parent != playerRoot.transform)
                thrower.transform.SetParent(playerRoot.transform, worldPositionStays: false);
            thrower.transform.localPosition = Vector3.zero;
            thrower.transform.localRotation = Quaternion.identity;

            // ----- 5. The first-person camera child -----
            Transform cameraTransform = playerRoot.transform.Find(FirstPersonCameraName);
            GameObject firstPersonCameraGo;
            if (cameraTransform != null)
            {
                firstPersonCameraGo = cameraTransform.gameObject;
            }
            else
            {
                firstPersonCameraGo = new GameObject(FirstPersonCameraName);
                firstPersonCameraGo.transform.SetParent(playerRoot.transform, worldPositionStays: false);
            }
            // Eye height comes from the config, not a magic number, so Tony can
            // raise or lower the viewpoint without touching this tool.
            firstPersonCameraGo.transform.localPosition = new Vector3(0f, roamConfig.EyeHeight, 0f);
            firstPersonCameraGo.transform.localRotation = Quaternion.identity;

            Camera firstPersonCamera = GetOrAdd<Camera>(firstPersonCameraGo);
            AudioListener firstPersonListener = GetOrAdd<AudioListener>(firstPersonCameraGo);
            // Both OFF at setup time. The bowling camera stays the enabled one
            // until PlayerAvatar applies its start mode in Play, so the Scene/Game
            // view and the existing throw cinematic are unchanged while editing.
            firstPersonCamera.enabled = false;
            firstPersonListener.enabled = false;

            // ----- 6. Components on the Player root -----
            CharacterController controller = GetOrAdd<CharacterController>(playerRoot);
            controller.radius = roamConfig.ControllerRadius;
            controller.height = roamConfig.ControllerHeight;
            // Centre the capsule so its FEET sit at the root's origin. The
            // character model's pivot is at its feet too (see GreyboxSceneBuilder
            // §8b), so the two line up and the root position means "where I am
            // standing" for both.
            controller.center = new Vector3(0f, roamConfig.ControllerHeight * 0.5f, 0f);
            controller.stepOffset = roamConfig.StepOffset;
            controller.slopeLimit = roamConfig.SlopeLimit;
            // Unity's documented recommendation is a skin width around 10% of
            // the radius; the 0.08 default is too fat for a 0.3 radius and makes
            // you hover off walls. Floored so a tiny radius can't produce zero,
            // which makes a CharacterController jitter.
            controller.skinWidth = Mathf.Max(0.01f, roamConfig.ControllerRadius * 0.1f);

            PlayerAvatar avatar = GetOrAdd<PlayerAvatar>(playerRoot);
            FirstPersonController firstPerson = GetOrAdd<FirstPersonController>(playerRoot);
            PlayerCameraDirector cameraDirector = GetOrAdd<PlayerCameraDirector>(playerRoot);

            // Interaction lives on the same root as everything else the player
            // owns. The interactor is mode-owned (PlayerAvatar.ApplyMode turns
            // it on for roaming); the prompt HUD is NOT — it just reads the
            // interactor's enabled state, so it stays enabled here and goes
            // quiet on its own. See InteractionPromptHud's class comment.
            PlayerInteractor interactor = GetOrAdd<PlayerInteractor>(playerRoot);
            InteractionPromptHud promptHud = GetOrAdd<InteractionPromptHud>(playerRoot);

            ThrowerAimSlide aimSlide = thrower.GetComponent<ThrowerAimSlide>();
            if (aimSlide == null)
                Debug.LogWarning("[Roaming] The thrower has no ThrowerAimSlide, so bowling mode will not " +
                                 "slide the character with the aim. Re-run WeeSpurts -> Build Greybox " +
                                 "Bowling Scene if that is unexpected.", thrower);

            // ----- 7. The throwing stance marker -----
            // A root-level object, NOT a child of Player: it has to stay put
            // while the player walks away, because it is the place they get
            // pulled back to.
            GameObject stance = GameObject.Find(ThrowingStanceName);
            if (stance == null)
            {
                stance = new GameObject(ThrowingStanceName);
                // Wherever the character was standing when roaming was first set
                // up IS the foul-line stance, by construction.
                stance.transform.SetPositionAndRotation(playerRoot.transform.position,
                                                        playerRoot.transform.rotation);
            }

            // ----- 8. Wire everything -----
            // SerializedObject rather than plain C# assignment for every private
            // [SerializeField]: this project has been bitten TWICE by editor-time
            // wiring that vanished on the next scene reload (the AimPreview
            // lesson, Docs/GameBible.md changelog 2026-07-22).
            var avatarSo = new SerializedObject(avatar);
            Wire(avatarSo, "characterController", controller);
            Wire(avatarSo, "firstPersonController", firstPerson);
            Wire(avatarSo, "interactor", interactor);
            Wire(avatarSo, "throwerAimSlide", aimSlide);
            Wire(avatarSo, "throwerModel", thrower.transform);
            avatarSo.ApplyModifiedPropertiesWithoutUndo();

            var interactorSo = new SerializedObject(interactor);
            Wire(interactorSo, "config", interactionConfig);
            // The EYE, not the player root: "in front of me" in first person
            // means where you are LOOKING. Same transform the camera pitches.
            Wire(interactorSo, "eye", firstPersonCameraGo.transform);
            Wire(interactorSo, "avatar", avatar);
            interactorSo.ApplyModifiedPropertiesWithoutUndo();

            var promptHudSo = new SerializedObject(promptHud);
            Wire(promptHudSo, "interactor", interactor);
            promptHudSo.ApplyModifiedPropertiesWithoutUndo();

            var firstPersonSo = new SerializedObject(firstPerson);
            Wire(firstPersonSo, "config", roamConfig);
            Wire(firstPersonSo, "cameraPivot", firstPersonCameraGo.transform);
            Wire(firstPersonSo, "reactionActor", reactionActor);
            firstPersonSo.ApplyModifiedPropertiesWithoutUndo();

            AudioListener bowlingListener = bowlingCamera.GetComponent<AudioListener>();
            if (bowlingListener == null)
                Debug.LogWarning("[Roaming] The bowling camera has no AudioListener, so audio will cut out " +
                                 "when you walk to the line. Harmless today (nothing plays sound yet).",
                                 bowlingCamera);

            var directorSo = new SerializedObject(cameraDirector);
            Wire(directorSo, "avatar", avatar);
            Wire(directorSo, "firstPersonCamera", firstPersonCamera);
            Wire(directorSo, "firstPersonListener", firstPersonListener);
            Wire(directorSo, "bowlingCamera", bowlingCamera);
            Wire(directorSo, "bowlingListener", bowlingListener);
            directorSo.ApplyModifiedPropertiesWithoutUndo();

            // ----- 9. Local-model layer + culling -----
            int modelLayerBit = 1 << modelLayer;
            int movedToLayer = SetLayerRecursively(thrower, modelLayer);

            // Remove the bit from the first-person camera so you cannot see your
            // own body, and make sure the bowling camera KEEPS it so the throw
            // cinematic still frames the character.
            //
            // NOTE the renderers are never touched — they stay enabled. Hiding
            // your own model is purely a local-camera concern; when Mirror lands,
            // every other player must still see this exact model walking around.
            // Disabling renderers would make you invisible to everyone.
            firstPersonCamera.cullingMask &= ~modelLayerBit;
            bowlingCamera.cullingMask |= modelLayerBit;

            // ----- 10. Hand the match-start seam to the scene's controller -----
            BowlingGameController game = FindFirst<BowlingGameController>();
            if (game != null)
            {
                var gameSo = new SerializedObject(game);
                // The venue scene opens in ROAMING mode: you walk up to a lane to
                // start a match (step 2). Flip this back on in the Inspector any
                // time you just want to feel-test throwing.
                SerializedProperty autoStart = gameSo.FindProperty("sandboxAutoStart");
                if (autoStart != null) autoStart.boolValue = false;
                Wire(gameSo, "throwingStance", stance.transform);
                // Wired even though auto-start is off, so that ticking it back on
                // hands control to the avatar properly instead of leaving the
                // roaming controller fighting ThrowerAimSlide.
                Wire(gameSo, "sandboxThrower", avatar);
                gameSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(game);
            }
            else
            {
                Debug.LogWarning("[Roaming] No BowlingGameController in this scene, so nothing starts a " +
                                 "match here. Roaming will still work.");
            }

            // ----- 10b. The lane kiosk (the diegetic way in) -----
            // Returns a report line and never throws: if the venue anchors are
            // missing, ONLY this step is skipped. Roaming must still get set up,
            // because a scene without a venue is still a scene you want to walk
            // around in.
            string kioskReport = PlaceLaneKiosk(game);

            // ----- 11. Flush + report -----
            EditorUtility.SetDirty(playerRoot);
            EditorUtility.SetDirty(avatar);
            EditorUtility.SetDirty(firstPerson);
            EditorUtility.SetDirty(cameraDirector);
            EditorUtility.SetDirty(interactor);
            EditorUtility.SetDirty(promptHud);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(firstPersonCamera);
            EditorUtility.SetDirty(bowlingCamera);
            if (aimSlide != null) EditorUtility.SetDirty(aimSlide);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[Roaming] Set up in scene '{scene.name}'. The scene is DIRTY and NOT saved — " +
                "look at it first, then Ctrl+S to keep it (Ctrl+Z / reopen to discard).\n\n" +
                $"HIERARCHY: {playerRoot.name} (scale {playerRoot.transform.localScale.x:0.##}) " +
                $"-> {thrower.name} + {FirstPersonCameraName} at eye height {roamConfig.EyeHeight:0.00}m.\n" +
                $"CHARACTER CONTROLLER (on the unscaled root): radius {controller.radius:0.00}m, " +
                $"height {controller.height:0.00}m, step offset {controller.stepOffset:0.00}m, " +
                $"slope limit {controller.slopeLimit:0}deg, skin width {controller.skinWidth:0.000}m.\n" +
                $"  -> Docs/OpenQuestions.md asks for exactly this number: the venue's 1.2 / 2.5 / 4.0m " +
                $"corridor minimums were guesses 'until there is a character controller with a real " +
                $"radius'. It is {controller.radius:0.00}m, i.e. a body {controller.radius * 2f:0.00}m " +
                $"wide, so a 1.2m threshold is {1.2f / (controller.radius * 2f):0.0} body-widths. " +
                "REPORTED ONLY — no layout config was retuned.\n" +
                $"LOCAL MODEL LAYER: '{LocalPlayerModelLayer}' (index {modelLayer}) applied to " +
                $"{movedToLayer} object(s) under {thrower.name}; culled from {FirstPersonCameraName}, " +
                $"kept on {bowlingCamera.name}. Renderers untouched — remote players will still see you.\n" +
                $"MATCH START: sandboxAutoStart set to FALSE; stance marker '{stance.name}' at " +
                $"{stance.transform.position}.\n" +
                $"INTERACTION: PlayerInteractor + InteractionPromptHud on {playerRoot.name}, " +
                $"config '{InteractionConfigPath}' (range {interactionConfig.Range:0.00}m, cone " +
                $"+/-{interactionConfig.FacingAngleDegrees:0}deg, key {interactionConfig.InteractKey}), " +
                $"facing measured from '{FirstPersonCameraName}'.\n" +
                kioskReport + "\n" +
                cameraReport + "\n" +
                "NOW TEST: press Play. You should be standing in the venue in first person — " +
                "WASD to walk, mouse to look, Shift to sprint, and the bowling camera should NOT be active.\n" +
                "THEN: nothing auto-starts any more (sandboxAutoStart is OFF) — THE KIOSK IS THE WAY IN. " +
                "Walk to the playable lane and DOWN THE STEPS into the settee pit beside it; the kiosk is " +
                "at the pit's score console, on the recessed pit floor, not up on the approach. Look at the " +
                $"console and a '[{interactionConfig.InteractKey}] Start Game' prompt should appear at the " +
                "bottom of the screen. Press it and you should be snapped to the foul line in bowling mode " +
                "with the scorecard up. Walk away and the prompt must disappear; come back mid-match and it " +
                "must NOT reappear.");
        }

        // ---------- lane kiosk ----------

        /// <summary>
        /// Creates (or repositions) the lane kiosk the player walks up to in
        /// order to start a match, and returns one line for the console report.
        /// Never throws and never aborts the tool — a missing venue is reported
        /// and skipped.
        ///
        /// THE KIOSK LIVES ON THE SCREEN ITSELF. Tony's call, and it is the right
        /// one: the thing you walk up to and press E on should BE the object you
        /// can see, so moving the screen in the editor moves the hotspot with it
        /// and there is no second "where the interaction happens" position to
        /// keep in sync. A separate marker parked at an anchor is exactly the
        /// kind of invisible second source of truth that drifts the first time
        /// somebody drags the visible thing somewhere better.
        ///
        /// THREE WAYS IT FINDS A HOME, in priority order:
        ///
        ///   1. AN EXISTING LaneKioskInteractable ANYWHERE IN THE SCENE WINS.
        ///      It is left exactly where it is — not moved, not reparented,
        ///      not deleted — and only its references are refreshed. Once you
        ///      have put the kiosk somewhere deliberately, this tool must never
        ///      second-guess you. Dragging the component onto a different object
        ///      by hand is a fully supported way to move it.
        ///   2. The playable lane pair's ConsolePanel (the screen), found under
        ///      the pit group AlleyGreyboxBuilder names Pit{A}_{B}.
        ///   3. The Anchor_LaneNN marker, as a bare fallback if the venue
        ///      geometry has been renamed or removed.
        ///
        /// THE TRADE-OFF, stated plainly: options 2 and 3 both put the component
        /// inside the 'AlleyGreybox' hierarchy, and
        /// AlleyGreyboxBuilder.RetireExistingGeometry() destroys that whole root
        /// on a venue rebuild — so the kiosk goes with it. That is accepted
        /// rather than worked around, because the alternative (a detached marker
        /// at the root) is what caused the problem this method now solves. THE
        /// WORKFLOW IS: rebuild the venue, then re-run this tool. The report line
        /// says so every time.
        /// </summary>
        private static string PlaceLaneKiosk(BowlingGameController game)
        {
            int laneNumber = ReadPlayableLaneIndex(out string laneSource);

            // ----- 1. An existing kiosk always wins -----
            LaneKioskInteractable kioskInteractable = FindFirst<LaneKioskInteractable>();
            string placement;

            if (kioskInteractable != null)
            {
                placement = $"left on '{kioskInteractable.gameObject.name}' where it already was " +
                            "(an existing kiosk is never moved — drag the component onto a different " +
                            "object if you want it somewhere else)";
            }
            else
            {
                GameObject host = FindKioskHost(laneNumber, out string hostNote);
                if (host == null)
                {
                    Debug.LogError(
                        "[Roaming] LANE KIOSK SKIPPED — nothing to attach it to.\n" +
                        "Everything else (roaming, camera, interaction components) was still set up; only " +
                        "the kiosk is missing, so there is currently NO way to start a match in this scene.\n" +
                        $"FIX IT LIKE THIS: run {VenueBuilderMenuItem} to build the venue greybox, then run " +
                        "this menu item again. OR do it by hand: select the screen object you want, Add " +
                        "Component -> Lane Kiosk Interactable, and drag the BowlingGame object into its " +
                        "'Game' slot. A hand-placed one is respected on every future run.\n" +
                        $"Looked for the screen and anchor belonging to lane {laneNumber} ({laneSource}).");
                    return "LANE KIOSK: SKIPPED — no screen or anchor found. Nothing starts a match in " +
                           "this scene. See the error above for the two ways to fix it.";
                }

                kioskInteractable = GetOrAdd<LaneKioskInteractable>(host);
                placement = hostNote;
            }

            GameObject kiosk = kioskInteractable.gameObject;

            var kioskSo = new SerializedObject(kioskInteractable);
            Wire(kioskSo, "game", game);
            // NO interaction config on the kiosk. It reports the ACTION only
            // ("Start Game — Lane 6"); InteractionPromptHud composes the "[E]"
            // from the interactor's own binding, so the glyph can never drift
            // from the key that works. See IInteractable.GetPrompt.
            SerializedProperty laneProperty = kioskSo.FindProperty("laneNumber");
            if (laneProperty != null) laneProperty.intValue = laneNumber;
            else Debug.LogError("[Roaming] 'LaneKioskInteractable' has no serialized field called " +
                                "'laneNumber', so the lane was left unset. Did the field get renamed?");
            kioskSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(kiosk);
            EditorUtility.SetDirty(kioskInteractable);

            string gameNote = game != null
                ? $"wired to '{game.name}'"
                : "NOT WIRED — no BowlingGameController in this scene, so it will offer nothing";

            return $"LANE KIOSK: on '{kiosk.name}' at world position {kiosk.transform.position} — " +
                   $"{placement}; lane {laneNumber} ({laneSource}); {gameNote}. " +
                   "It moves with the screen, so drag the screen and the hotspot follows. " +
                   "A VENUE REBUILD DESTROYS IT along with the AlleyGreybox root — re-run this tool after " +
                   "one and it will be re-attached.";
        }

        /// <summary>
        /// Finds the object the kiosk should live on: the playable lane pair's
        /// ConsolePanel (the screen) if it exists, otherwise the Anchor_LaneNN
        /// marker. Returns null when neither is present.
        /// </summary>
        private static GameObject FindKioskHost(int laneNumber, out string note)
        {
            // AlleyGreyboxBuilder names pit groups "Pit{A}_{B}" for each lane
            // PAIR, where the pair is (odd, even) — lane 6 lives in Pit05_06.
            // Deriving the pair from the lane number rather than hard-coding it
            // keeps this correct if PlayableLaneIndex ever changes.
            int laneA = laneNumber % 2 == 0 ? laneNumber - 1 : laneNumber;
            string pitName = $"Pit{laneA:00}_{laneA + 1:00}";

            Transform pit = FindTransformByName(pitName);
            if (pit != null)
            {
                // The panel is the screen face; the post is just its stalk.
                Transform panel = pit.Find("ConsolePanel");
                if (panel != null)
                {
                    note = $"attached to the screen '{pitName}/ConsolePanel'";
                    return panel.gameObject;
                }
            }

            string anchorName = "Anchor_Lane" + laneNumber.ToString("00");
            Transform anchor = FindTransformByName(anchorName);
            if (anchor != null)
            {
                Debug.LogWarning(
                    $"[Roaming] No '{pitName}/ConsolePanel' found, so the kiosk fell back to the " +
                    $"'{anchorName}' marker. That still works, but the hotspot will not follow the screen " +
                    "if you move it. Move the LaneKioskInteractable component onto the screen object " +
                    "you actually want and this tool will respect it from then on.", anchor);
                note = $"attached to the fallback marker '{anchorName}' (no ConsolePanel found)";
                return anchor.gameObject;
            }

            note = null;
            return null;
        }

        /// <summary>
        /// Which lane is the real, playable one. Read from AlleyLayout.asset so
        /// this tool and the venue builder can never disagree about which
        /// Anchor_LaneNN to use; falls back to the same default the config
        /// itself ships with if the asset isn't there yet.
        /// </summary>
        private static int ReadPlayableLaneIndex(out string source)
        {
            var layout = AssetDatabase.LoadAssetAtPath<AlleyLayoutConfig>(AlleyLayoutPath);
            if (layout != null)
            {
                source = $"from {AlleyLayoutPath}";
                return layout.PlayableLaneIndex;
            }

            source = $"fallback — {AlleyLayoutPath} not found";
            return FallbackPlayableLaneIndex;
        }

        // ---------- camera discovery ----------

        /// <summary>
        /// The bowling camera is "the Camera with a ThrowCamera component".
        /// Everything else it finds is REPORTED, never deleted: TestVenue.unity
        /// currently carries a duplicate 'Main Camera (1)' and 'Directional
        /// Light (1)' from a scene merge, and Tony needs telling rather than a
        /// tool quietly picking one and moving on.
        /// </summary>
        private static Camera FindBowlingCameraAndReport(Scene scene, out string report)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);

            var candidates = new List<Camera>();
            var all = new List<Camera>();
            foreach (Camera cam in cameras)
            {
                // Ignore a first-person camera we built on a previous run —
                // it is ours, not a duplicate the human needs warning about.
                if (cam.gameObject.name == FirstPersonCameraName) continue;
                all.Add(cam);
                if (cam.GetComponent<ThrowCamera>() != null) candidates.Add(cam);
            }

            Camera chosen = candidates.Count > 0 ? candidates[0] : null;

            var sb = new StringBuilder();
            sb.Append($"CAMERAS in '{scene.name}': {all.Count} found");
            if (all.Count > 0)
            {
                sb.Append(" — ");
                for (int i = 0; i < all.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('\'').Append(all[i].name).Append('\'');
                    if (all[i].GetComponent<ThrowCamera>() != null) sb.Append(" [ThrowCamera]");
                }
            }
            sb.Append('.');

            if (chosen != null) sb.Append($" CHOSE '{chosen.name}' as the bowling camera.");

            if (all.Count > 1)
            {
                sb.Append("\n  !! MORE THAN ONE CAMERA. Nothing was deleted — this is a report, not a fix. " +
                          "TestVenue.unity is known to carry a duplicate 'Main Camera (1)' (and a duplicate " +
                          "'Directional Light (1)') from a scene merge. Two enabled cameras render on top of " +
                          "each other and two AudioListeners make Unity complain, so decide which one is real " +
                          "and delete the other by hand.");
            }
            if (candidates.Count > 1)
            {
                sb.Append($"\n  !! {candidates.Count} cameras carry a ThrowCamera component; the first one " +
                          "was used. That is a coin flip — fix the scene rather than trusting this choice.");
            }

            report = sb.ToString();
            return chosen;
        }

        // ---------- helpers ----------

        /// <summary>
        /// Sets a whole subtree's layer and returns how many objects moved.
        /// Recursive because Unity's layer field is per-GameObject, not
        /// inherited — setting only the root would leave every mesh child
        /// visible to the first-person camera, which is the entire bug we are
        /// avoiding.
        /// </summary>
        private static int SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            int count = 1;
            foreach (Transform child in go.transform)
                count += SetLayerRecursively(child.gameObject, layer);
            return count;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        /// <summary>
        /// FindObjectsByType (not the obsolete FindObjectOfType), including
        /// inactive objects so a temporarily-disabled thrower is still found.
        /// </summary>
        private static T FindFirst<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// Finds a Transform anywhere in the open scene by exact name.
        ///
        /// NOT GameObject.Find: that only ever sees ACTIVE objects, and an
        /// anchor sitting under a temporarily-disabled venue root would come
        /// back null — which this tool would then report as "the venue isn't
        /// built", sending Tony off to rebuild something that is already there.
        /// FindObjectsInactive.Include is the whole point of doing it the long
        /// way. Editor-time only, so the cost of walking every transform is
        /// irrelevant.
        /// </summary>
        private static Transform FindTransformByName(string name)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform t in all)
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Same, but returns the GameObject. See FindTransformByName for why not GameObject.Find.</summary>
        private static GameObject FindGameObjectByName(string name)
        {
            Transform t = FindTransformByName(name);
            return t != null ? t.gameObject : null;
        }

        /// <summary>
        /// Writes one serialized reference, and says so loudly if the field name
        /// no longer exists — a silent typo here is exactly the kind of "wired
        /// it, but nothing happened" bug that costs a beginner an evening.
        /// </summary>
        private static void Wire(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[Roaming] '{so.targetObject.GetType().Name}' has no serialized field " +
                               $"called '{propertyName}', so it was left unwired. Did the field get renamed?");
                return;
            }
            property.objectReferenceValue = value;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            // AssetDatabase needs folders created level by level.
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
