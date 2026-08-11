using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Bowling;
using WeeSpurts.Core;
using WeeSpurts.Interaction;
using WeeSpurts.Player;
using WeeSpurts.UI;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Retrofits real bowling gameplay onto lane 5 of ThunderLanesVenue.unity's
    /// "Std_Lane_Pair_5_6" art, LEFT IN ITS ORIGINAL ORIENTATION — no art
    /// rotation, unlike the first (reverted) attempt. Instead, a LaneFrame
    /// marks this lane's own down-lane/lateral axes, and BowlingBall/
    /// BowlingMatchFlow/AimPreview/ThrowerAimSlide/ThrowCamera/
    /// ThrowCameraSequence all read the lane's axes from it instead of
    /// hardcoding Unity's world Z/X. See LaneFrame's class comment for why.
    ///
    /// Coordinates below are MEASURED from the live (unrotated) scene
    /// (ThunderLanesVenueLaneDiagnostic.Dump, cross-checked on lanes 3, 5 and
    /// 7 — all parallel): lane 5's down-lane axis is world +X. Foul line at
    /// world (13.00, 0, 12.85). Headpin updated to (31.00, 0.25, 12.85) —
    /// 18.00m foul-line-to-headpin — by ThunderLanesVenueLaneLengthTool
    /// (2026-08-11, Tony's call after playing the original 9.20m/roughly-
    /// half-TestVenue's-18m lane): all 8 lanes' art and the pinsetter/back-
    /// of-house wing behind them moved +8.8m so lane 5's real distance
    /// matches LaneConfig.Length's default instead of fighting it.
    ///
    /// Reuses the EXISTING roaming rig (ThunderLanesVenueRoamingSetupTool's
    /// Player/Thrower/PlayerAvatar/PlayerCameraDirector) rather than building a
    /// second thrower — completes the fields that tool deliberately left null
    /// (throwerAimSlide, throwerModel, bowlingCamera, bowlingListener).
    ///
    /// Run via -executeMethod; not on the WeeSpurts menu — one-shot for one
    /// scene, like the (now-deleted) rotator was.
    /// </summary>
    public static class ThunderLanesVenueBowlingSetupTool
    {
        private const string ScenePath = "Assets/_Project/Scenes/ThunderLanesVenue.unity";
        private const string ProjectRoot = "Assets/_Project";

        // Measured, not guessed — see class comment. HeadpinPoint reflects
        // the +8.8m extension applied by ThunderLanesVenueLaneLengthTool;
        // was (22.20, 0, 12.85) before that pass.
        private static readonly Vector3 FoulLinePoint = new Vector3(13.00f, 0f, 12.85f);
        private static readonly Vector3 HeadpinPoint = new Vector3(31.00f, 0f, 12.85f);

        // Quaternion.Euler(0,90,0) * Vector3.forward == (1,0,0): verified
        // numerically (LaneFrameRefactorVerification), not just by hand — this
        // is what makes a LaneFrame's own Forward equal world +X, this lane's
        // real down-lane axis.
        private static readonly Quaternion LaneRotation = Quaternion.Euler(0f, 90f, 0f);

        // Same GreyboxSceneBuilder offsets (measured from ITS foul line at
        // local down-lane 0), now expressed as (downLane, lateral, height)
        // triples fed through LaneFrame.PointAt instead of raw Vector3(x,y,z).
        private const float ThrowerGroundDownLane = -0.8f;
        private const float CameraAnchorHeight = 1f;
        private const float AimViewDownLane = -3.7f;
        private const float AimViewHeight = 2.3f;
        // 16deg pitch (unchanged from GreyboxSceneBuilder), 90deg yaw so the
        // camera faces world +X (this lane's down-lane direction) instead of
        // the default +Z — see LaneRotation's comment for the same identity.
        private static readonly Vector3 AimViewEuler = new Vector3(16f, 90f, 0f);

        public static void SetAsMainMap()
        {
            var scenes = EditorBuildSettings.scenes;
            var newList = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            foreach (var s in scenes)
                if (s.path != ScenePath) newList.Add(s);
            EditorBuildSettings.scenes = newList.ToArray();
            Debug.Log($"[ThunderLanesVenue] {ScenePath} at build index 0, enabled.");
        }

        /// <summary>
        /// Targeted fix for two bugs found by playtest, without re-running the
        /// full BuildAndWire (which isn't idempotent — it unconditionally
        /// creates new GameObjects and would stack duplicates on an already-
        /// wired scene):
        ///   1. PinDeck.ResetFullRack() only runs at PLAY time (BowlingMatchFlow
        ///      calls it when a match starts) — this tool's Editor-time
        ///      BuildAndWire never triggers it, so the saved scene never had
        ///      baked pin clones to begin with. The actual bug was PinDeck's
        ///      own saved transform: rotation was left at identity (should
        ///      match the LaneFrame's, now that PinDeck.cs applies
        ///      transform.rotation to each pin offset) and position.y was
        ///      hardcoded 0 instead of this venue's real floor height, so pins
        ///      spawned buried in the lane bed's collider — see PinDeck.cs's
        ///      own comment on ResetFullRack for why a buried shaped-collider
        ///      pin gets ejected hard enough to read as permanently knocked.
        ///   2. The art's own decorative Pin_5_* meshes are still visible,
        ///      doubled up with the real physics pins.
        /// </summary>
        public static void FixPinDeckAndHideDecorPins()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[ThunderLanesVenue] Could not open {ScenePath} — aborting.");
                return;
            }

            GameObject laneGo = GameObject.Find("Lane5_LaneFrame");
            GameObject deckGo = GameObject.Find("PinDeck");
            if (laneGo == null || deckGo == null)
            {
                Debug.LogError("[ThunderLanesVenue] Missing Lane5_LaneFrame or PinDeck — run BuildAndWire first. Nothing changed.");
                return;
            }

            // Measure the REAL floor height under lane 5, live — the same
            // "measure, don't guess" rule this whole pass has followed.
            // Lane_Bed_5's own bounds top surface is where a pin's base
            // should rest, not an assumed Y=0.
            GameObject laneBed = GameObject.Find("Lane_Bed_5");
            float floorY = 0f;
            if (laneBed != null && laneBed.GetComponent<Renderer>() is Renderer laneBedRenderer)
            {
                Bounds b = laneBedRenderer.bounds;
                floorY = b.center.y + b.size.y * 0.5f;
            }
            else
            {
                Debug.LogWarning("[ThunderLanesVenue] Could not find Lane_Bed_5 to measure real floor height — leaving PinDeck.y at 0.");
            }

            Vector3 correctedHeadpin = new Vector3(HeadpinPoint.x, floorY, HeadpinPoint.z);
            deckGo.transform.SetPositionAndRotation(correctedHeadpin, laneGo.transform.rotation);
            EditorUtility.SetDirty(deckGo);

            // Hide (not destroy — reversible) the art's own decorative pins for
            // lane 5 specifically. Same row/col pattern as PinDeck.PinOffsets:
            // row 0 = 1 pin, row 1 = 2, row 2 = 3, row 3 = 4 = 10 total.
            int hidden = 0;
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col <= row; col++)
                {
                    GameObject decorPin = GameObject.Find($"Pin_5_{row}_{col}");
                    if (decorPin == null) continue;
                    decorPin.SetActive(false);
                    EditorUtility.SetDirty(decorPin);
                    hidden++;
                }
            }

            AssetDatabase.SaveAssets();
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log(
                (saved ? $"[ThunderLanesVenue] Fixed and saved {ScenePath}.\n"
                       : $"[ThunderLanesVenue] Fix applied but FAILED TO SAVE {ScenePath}.\n") +
                $"PinDeck moved to {correctedHeadpin} (real measured floor height {floorY:0.###}, was 0) and " +
                $"rotated to match Lane5_LaneFrame ({laneGo.transform.rotation.eulerAngles}, was identity).\n" +
                $"Hid {hidden}/10 decorative Pin_5_*_* art objects (SetActive(false), not destroyed — " +
                "re-enable in the Inspector if you ever want them back).\n" +
                "NOW TEST: press Play, start a match on lane 5. The pin triangle should recede AWAY from you " +
                "down the lane (not spread sideways), stand cleanly on the floor with no part embedded, and " +
                "actually fall when hit.");
        }

        public static void BuildAndWire()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[ThunderLanesVenue] Could not open {ScenePath} — aborting.");
                return;
            }

            // ----- 0. Find the existing roaming rig — this tool completes it, never builds a second one -----
            PlayerAvatar avatar = Object.FindFirstObjectByType<PlayerAvatar>();
            if (avatar == null)
            {
                Debug.LogError("[ThunderLanesVenue] No PlayerAvatar found — run " +
                                "'WeeSpurts/Thunder Lanes Venue/Set Up Roaming Player' first. Nothing changed.");
                return;
            }
            CharacterThrowReactionActor reactionActor = Object.FindFirstObjectByType<CharacterThrowReactionActor>();
            if (reactionActor == null)
            {
                Debug.LogError("[ThunderLanesVenue] No CharacterThrowReactionActor (Thrower) found — " +
                                "the roaming rig should have created one. Nothing changed.");
                return;
            }
            GameObject thrower = reactionActor.gameObject;
            PlayerCameraDirector cameraDirector = avatar.GetComponent<PlayerCameraDirector>();

            // ----- 1. The LaneFrame — the whole point of this pass -----
            GameObject laneGo = GameObject.Find("Lane5_LaneFrame");
            if (laneGo == null) laneGo = new GameObject("Lane5_LaneFrame");
            laneGo.transform.SetPositionAndRotation(FoulLinePoint, LaneRotation);
            LaneFrame lane = laneGo.GetComponent<LaneFrame>();
            if (lane == null) lane = laneGo.AddComponent<LaneFrame>();

            // ----- 2. Shared config assets (create-once, same paths GreyboxSceneBuilder uses) -----
            BallConfig ballConfig = LoadOrCreateAsset<BallConfig>(ProjectRoot + "/ScriptableObjects/BallConfig.asset");
            LaneConfig laneConfig = LoadOrCreateAsset<LaneConfig>(ProjectRoot + "/ScriptableObjects/LaneConfig.asset");
            PinConfig pinConfig = LoadOrCreateAsset<PinConfig>(ProjectRoot + "/ScriptableObjects/PinConfig.asset");

            Material ballMat = LoadOrCreateMaterial("BallMat", new Color(0.15f, 0.35f, 0.9f));
            Material pinMat = LoadOrCreateMaterial("PinMat", Color.white);

            // ----- 3. Invisible physical rails flanking lane 5 -----
            // The art's own "gutter" strips are flat decals (0.01m tall) with no
            // wall — fine for the aim-clamped straight case, but Hook/Wobble
            // apply CONTINUOUS sideways force during flight and can drift the
            // ball past that. GreyboxSceneBuilder gives its lane real 0.35m
            // rails for exactly this reason; matched here, invisible (no
            // renderer), and ORIENTED to match the LaneFrame's rotation so
            // their long localScale axis runs down-lane, not down world Z.
            float railOffset = laneConfig.Width * 0.5f + 0.55f;
            const float RailStartDownLane = -2f;   // a little behind the foul line
            const float RailEndDownLane = 18.5f;   // just past the pin deck (was 9.7 before ThunderLanesVenueLaneLengthTool's +8.8m extension)
            float railLength = RailEndDownLane - RailStartDownLane;
            float railCenterDownLane = (RailStartDownLane + RailEndDownLane) * 0.5f;
            Vector3 railScale = new Vector3(0.25f, 0.35f, railLength);

            GameObject rails = new GameObject("Lane5_Rails");
            MakeInvisibleBox(rails, "RailLeft",
                lane.PointAt(railCenterDownLane, -railOffset, 0.12f), LaneRotation, railScale);
            MakeInvisibleBox(rails, "RailRight",
                lane.PointAt(railCenterDownLane, railOffset, 0.12f), LaneRotation, railScale);

            // ----- 4. Pins -----
            GameObject deckGo = new GameObject("PinDeck");
            deckGo.transform.position = HeadpinPoint;
            PinDeck deck = deckGo.AddComponent<PinDeck>();

            GameObject pinGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pinGo.name = "PinTemplate";
            pinGo.transform.SetParent(deckGo.transform);
            pinGo.transform.localScale = new Vector3(0.12f, pinConfig.PinHeight * 0.5f, 0.12f);
            pinGo.transform.localPosition = new Vector3(0f, pinConfig.PinHeight * 0.5f, 0f);
            pinGo.GetComponent<Renderer>().sharedMaterial = pinMat;
            Object.DestroyImmediate(pinGo.GetComponent<CapsuleCollider>());
            BoxCollider pinCollider = pinGo.AddComponent<BoxCollider>();
            pinGo.AddComponent<Rigidbody>();
            Pin pinTemplate = pinGo.AddComponent<Pin>();

            var pinBounce = GetOrCreatePhysicsMaterial(ProjectRoot + "/ScriptableObjects/PinBounce.asset",
                "PinBounce", pinConfig.Bounciness, pinConfig.Friction);
            pinCollider.sharedMaterial = pinBounce;

            pinGo.SetActive(false);
            deck.Initialize(pinConfig, pinTemplate);
            EditorUtility.SetDirty(deck);

            // ----- 5. Ball -----
            GameObject ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "BowlingBall";
            float d = ballConfig.Radius * 2f;
            ballGo.transform.localScale = new Vector3(d, d, d);
            ballGo.GetComponent<Renderer>().sharedMaterial = ballMat;
            ballGo.AddComponent<Rigidbody>();
            BowlingBall ball = ballGo.AddComponent<BowlingBall>();
            ball.SetLane(lane);

            var ballBounce = GetOrCreatePhysicsMaterial(ProjectRoot + "/ScriptableObjects/BallBounce.asset",
                "BallBounce", ballConfig.Bounciness, null, maximumCombine: true);
            ballGo.GetComponent<SphereCollider>().sharedMaterial = ballBounce;

            GameObject spawn = new GameObject("BallSpawn");
            spawn.transform.position = lane.PointAt(0f, 0f, ballConfig.SpawnHeight);
            ballGo.transform.position = spawn.transform.position;

            // ----- 6. ThrowerAimSlide on the EXISTING thrower (roaming rig built it without one) -----
            ThrowerAimSlide aimSlide = thrower.GetComponent<ThrowerAimSlide>();
            bool aimSlideIsNew = aimSlide == null;
            if (aimSlideIsNew) aimSlide = thrower.AddComponent<ThrowerAimSlide>();

            // ----- 7. Camera anchor + bowling camera -----
            GameObject cameraAnchor = new GameObject("CameraAnchor");
            cameraAnchor.transform.position = lane.PointAt(ThrowerGroundDownLane, 0f, CameraAnchorHeight);
            Transform throwerCameraAnchor = cameraAnchor.transform;

            GameObject camGo = new GameObject("BowlingCamera");
            camGo.tag = "Untagged"; // the venue already has a "Main Camera" — do not steal the tag
            Camera cam = camGo.AddComponent<Camera>();
            AudioListener camListener = camGo.AddComponent<AudioListener>();
            ThrowCamera throwCam = camGo.AddComponent<ThrowCamera>();
            throwCam.SetLane(lane);
            throwCam.ConfigureAimView(lane.PointAt(AimViewDownLane, 0f, AimViewHeight), AimViewEuler, ballGo.transform);
            EditorUtility.SetDirty(throwCam);

            // ----- 8. Managers -----
            if (Object.FindFirstObjectByType<GameManager>() == null)
            {
                GameObject managers = new GameObject("GameManager");
                managers.AddComponent<GameManager>();
                managers.AddComponent<SceneLoader>();
                managers.AddComponent<AudioManager>();
            }

            // ----- 9. BowlingGame -----
            GameObject gameGo = new GameObject("BowlingGame");
            BallLauncher launcher = GetOrAdd<BallLauncher>(gameGo);
            BowlingMatchFlow matchFlow = GetOrAdd<BowlingMatchFlow>(gameGo);
            BowlingPresentation presentation = GetOrAdd<BowlingPresentation>(gameGo);
            matchFlow.Configure(ballConfig, laneConfig, ball, deck, launcher, spawn.transform);
            matchFlow.SetLane(lane);
            presentation.Configure(throwCam);
            presentation.SetThrowReactionActor(reactionActor);

            Material aimLineMat = LoadOrCreateMaterial("AimLineMat", new Color(1f, 0.9f, 0.1f));
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader != null) aimLineMat.shader = unlitShader;
            LineRenderer aimLine = ballGo.AddComponent<LineRenderer>();
            aimLine.sharedMaterial = aimLineMat;
            aimLine.startWidth = 0.03f;
            aimLine.endWidth = 0.015f;
            AimPreview aimPreview = ballGo.AddComponent<AimPreview>();
            aimPreview.Configure(launcher, matchFlow, aimLine);
            EditorUtility.SetDirty(aimPreview);

            aimSlide.Configure(launcher, matchFlow);
            EditorUtility.SetDirty(aimSlide);

            // ----- 10. Ball config switcher (same five slots as GreyboxSceneBuilder) -----
            BallConfigSwitcher switcher = gameGo.AddComponent<BallConfigSwitcher>();
            switcher.EditorClearConfigs();
            switcher.EditorAddConfig(ballConfig);
            foreach (string variantName in new[] { "BouncyBall", "Cannonball" })
            {
                var variant = AssetDatabase.LoadAssetAtPath<BallConfig>(
                    ProjectRoot + "/ScriptableObjects/" + variantName + ".asset");
                if (variant != null) switcher.EditorAddConfig(variant);
            }

            string wobblerPath = ProjectRoot + "/ScriptableObjects/Wobbler.asset";
            BallConfig wobblerConfig = AssetDatabase.LoadAssetAtPath<BallConfig>(wobblerPath);
            if (wobblerConfig != null) switcher.EditorAddConfig(wobblerConfig);

            string nukePath = ProjectRoot + "/ScriptableObjects/Nuke.asset";
            BallConfig nukeConfig = AssetDatabase.LoadAssetAtPath<BallConfig>(nukePath);
            if (nukeConfig != null) switcher.EditorAddConfig(nukeConfig);
            EditorUtility.SetDirty(switcher);

            // ----- 11. Nuke presentation layer (only if the shared Nuke config exists) -----
            if (nukeConfig != null)
            {
                Material nukeMat = LoadOrCreateMaterial("NukeMat", new Color(1f, 0.2f, 0.05f));
                GameObject nukeSphereGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nukeSphereGo.name = "NukeSphere";
                nukeSphereGo.GetComponent<Renderer>().sharedMaterial = nukeMat;
                Object.DestroyImmediate(nukeSphereGo.GetComponent<SphereCollider>());
                nukeSphereGo.SetActive(false);

                GameObject nukePoofGo = new GameObject("NukePoof");
                ParticleSystem nukePoof = nukePoofGo.AddComponent<ParticleSystem>();
                var nukePoofMain = nukePoof.main;
                nukePoofMain.playOnAwake = false;
                nukePoofMain.loop = false;
                nukePoofMain.duration = 1f;
                nukePoofMain.startLifetime = 1f;
                nukePoofMain.stopAction = ParticleSystemStopAction.None;
                nukePoof.Stop();

                Material nukePoofMat = LoadOrCreateMaterial("NukePoofMat", new Color(1f, 0.6f, 0.1f));
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
                if (particleShader != null) nukePoofMat.shader = particleShader;
                nukePoofGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = nukePoofMat;

                GameObject nukeShotGo = new GameObject("NukeShot");
                NukeShotResolver nukeResolver = nukeShotGo.AddComponent<NukeShotResolver>();
                var nukeResolverSO = new SerializedObject(nukeResolver);
                nukeResolverSO.FindProperty("nukeSphere").objectReferenceValue = nukeSphereGo.transform;
                nukeResolverSO.FindProperty("poofEffect").objectReferenceValue = nukePoof;
                nukeResolverSO.ApplyModifiedPropertiesWithoutUndo();
                presentation.SetNukeResolver(nukeResolver);
                EditorUtility.SetDirty(nukeResolver);
            }

            // ----- 12. Scripted throw camera sequence (shared config, create-once) -----
            string sequencePath = ProjectRoot + "/ScriptableObjects/ThrowCameraSequenceConfig.asset";
            ThrowCameraSequenceConfig sequenceConfig = LoadOrCreateAsset<ThrowCameraSequenceConfig>(sequencePath);

            ThrowCameraSequence throwCamSequence = camGo.AddComponent<ThrowCameraSequence>();
            var sequenceSO = new SerializedObject(throwCamSequence);
            sequenceSO.FindProperty("config").objectReferenceValue = sequenceConfig;
            sequenceSO.FindProperty("launcher").objectReferenceValue = launcher;
            sequenceSO.FindProperty("game").objectReferenceValue = matchFlow;
            sequenceSO.FindProperty("ball").objectReferenceValue = ball;
            sequenceSO.FindProperty("pinDeck").objectReferenceValue = deckGo.transform;
            sequenceSO.FindProperty("thrower").objectReferenceValue = throwerCameraAnchor;
            sequenceSO.FindProperty("laneConfig").objectReferenceValue = laneConfig;
            sequenceSO.FindProperty("sequenceCamera").objectReferenceValue = cam;
            sequenceSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(throwCamSequence);

            GetOrAdd<DebugHud>(gameGo);
            GetOrAdd<SpinSelectorHud>(gameGo);
            EditorUtility.SetDirty(matchFlow);
            EditorUtility.SetDirty(presentation);

            // ----- 13. Throwing stance marker -----
            GameObject stance = new GameObject("ThrowingStance");
            // Facing down-lane: LaneRotation IS "facing +X" by construction
            // (see its own comment), so the stance marker's forward should
            // match it exactly — the thrower faces the pins, not the wall.
            stance.transform.SetPositionAndRotation(lane.PointAt(ThrowerGroundDownLane, 0f, 0f), LaneRotation);

            var presentationSo = new SerializedObject(presentation);
            SerializedProperty autoStart = presentationSo.FindProperty("sandboxAutoStart");
            if (autoStart != null) autoStart.boolValue = false;
            Wire(presentationSo, "throwingStance", stance.transform);
            Wire(presentationSo, "sandboxThrower", avatar);
            presentationSo.ApplyModifiedPropertiesWithoutUndo();

            // ----- 14. Lane kiosk -----
            GameObject kioskGo = new GameObject("LaneKiosk_5");
            kioskGo.transform.position = lane.PointAt(-0.4f, 0.6f, 1.05f);
            LaneKioskInteractable kiosk = kioskGo.AddComponent<LaneKioskInteractable>();
            var kioskSo = new SerializedObject(kiosk);
            Wire(kioskSo, "game", presentation);
            SerializedProperty laneProperty = kioskSo.FindProperty("laneNumber");
            if (laneProperty != null) laneProperty.intValue = 5;
            kioskSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kiosk);

            // ----- 15. Finish wiring the roaming rig for bowling mode -----
            var avatarSo = new SerializedObject(avatar);
            Wire(avatarSo, "throwerAimSlide", aimSlide);
            Wire(avatarSo, "throwerModel", thrower.transform);
            avatarSo.ApplyModifiedPropertiesWithoutUndo();

            if (cameraDirector != null)
            {
                var directorSo = new SerializedObject(cameraDirector);
                Wire(directorSo, "bowlingCamera", cam);
                Wire(directorSo, "bowlingListener", camListener);
                directorSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cameraDirector);
            }
            else
            {
                Debug.LogWarning("[ThunderLanesVenue] avatar has no PlayerCameraDirector — bowling camera " +
                                  "not wired to it. Was the roaming rig built by a different tool?");
            }
            EditorUtility.SetDirty(avatar);
            EditorUtility.SetDirty(lane);

            // ----- 16. Save -----
            AssetDatabase.SaveAssets();
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log(
                (saved ? $"[ThunderLanesVenue] Bowling wired on lane 5 (ORIGINAL art orientation, no rotation) " +
                        $"and saved {ScenePath}.\n\n"
                       : $"[ThunderLanesVenue] Bowling wired on lane 5 but FAILED TO SAVE {ScenePath}.\n\n") +
                $"LANE FRAME: 'Lane5_LaneFrame' at {FoulLinePoint}, rotated so Forward = world +X (this lane's " +
                $"real down-lane axis, measured and cross-checked against lanes 3 and 7). BowlingBall, " +
                $"BowlingMatchFlow, ThrowCamera, and (transitively, via game.Lane) AimPreview/ThrowerAimSlide/" +
                $"ThrowCameraSequence all read down-lane/lateral from it instead of world Z/X.\n" +
                $"GEOMETRY: foul line at {FoulLinePoint}, headpin at {HeadpinPoint} — " +
                $"{(HeadpinPoint - FoulLinePoint).magnitude:0.00}m foul-line-to-headpin.\n" +
                "FEEL FLAG: TestVenue/BowlingTestbed's lane (and the shared BallConfig this reuses) is tuned " +
                "for LaneConfig.Length's default 18m. This lane is roughly half that. Play it before deciding " +
                "whether it needs its own tuned BallConfig variant.\n" +
                $"RAILS: invisible physical rails added, oriented to the lane's own rotation (the art's gutter " +
                "strips are flat decals with no collision height).\n" +
                $"KIOSK: 'LaneKiosk_5' at {kioskGo.transform.position}, lane number 5, wired to '{gameGo.name}'.\n" +
                (aimSlideIsNew ? "" : "NOTE: Thrower already had a ThrowerAimSlide — reused it.\n") +
                "\nNOW TEST: press Play, walk to the lane 5 kiosk, press [E] to start a match. A centred aim " +
                "should travel DOWN the lane toward the pins (not sideways into a gutter/wall), and hook/" +
                "spin should curve across the lane's WIDTH, not along its length. All 7 camera beats should " +
                "frame the shot the same way they do in TestVenue — if any beat looks visibly wrong, that " +
                "pinpoints which ThrowCameraSequence beat still has an unconverted assumption.");
        }

        // ---------- helpers ----------

        private static void MakeInvisibleBox(GameObject parent, string name, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.SetPositionAndRotation(pos, rot);
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one;
            go.transform.localScale = scale;
        }

#if UNITY_6000_0_OR_NEWER
        private static PhysicsMaterial GetOrCreatePhysicsMaterial(string path, string name, float bounciness,
            float? friction, bool maximumCombine = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null) return existing;

            var mat = new PhysicsMaterial(name) { bounciness = bounciness };
            if (friction.HasValue) { mat.dynamicFriction = friction.Value; mat.staticFriction = friction.Value; }
            if (maximumCombine) mat.bounceCombine = PhysicsMaterialCombine.Maximum;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
#else
        private static PhysicMaterial GetOrCreatePhysicsMaterial(string path, string name, float bounciness,
            float? friction, bool maximumCombine = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
            if (existing != null) return existing;

            var mat = new PhysicMaterial(name) { bounciness = bounciness };
            if (friction.HasValue) { mat.dynamicFriction = friction.Value; mat.staticFriction = friction.Value; }
            if (maximumCombine) mat.bounceCombine = PhysicMaterialCombine.Maximum;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
#endif

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = ProjectRoot + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { color = color };
            EnsureFolder(ProjectRoot + "/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void Wire(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[ThunderLanesVenue] '{so.targetObject.GetType().Name}' has no serialized " +
                                $"field called '{propertyName}', so it was left unwired. Did the field get renamed?");
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
            string[] parts = path.Split('/');
            string current = parts[0];
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
