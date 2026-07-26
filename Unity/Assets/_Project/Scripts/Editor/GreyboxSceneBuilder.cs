using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Bowling;
using WeeSpurts.Core;
using WeeSpurts.UI;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK builds the entire playable greybox bowling scene:
    /// lane, rails, pins, ball, camera, light, managers — fully wired.
    ///
    /// Menu: WeeSpurts → Build Greybox Bowling Scene
    ///
    /// WHY code instead of hand-placing? (a) Beginners don't have to wire
    /// anything, (b) the scene is reproducible — change LaneConfig numbers
    /// and rebuild, (c) no hand-edited scene YAML, ever (see CLAUDE.md).
    /// Safe to run repeatedly; it creates a brand-new scene each time.
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenePath = ProjectRoot + "/Scenes/BowlingAlley.unity";

        [MenuItem("WeeSpurts/Build Greybox Bowling Scene")]
        public static void Build()
        {
            // ----- 1. Config assets (created once, then reused) -----
            BallConfig ballConfig = LoadOrCreateAsset<BallConfig>(ProjectRoot + "/ScriptableObjects/BallConfig.asset");
            LaneConfig laneConfig = LoadOrCreateAsset<LaneConfig>(ProjectRoot + "/ScriptableObjects/LaneConfig.asset");
            PinConfig pinConfig = LoadOrCreateAsset<PinConfig>(ProjectRoot + "/ScriptableObjects/PinConfig.asset");

            // ----- 2. Fresh empty scene -----
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            float width = laneConfig.Width;
            float length = laneConfig.Length;

            // ----- 3. Materials (URP if present, fallback to Standard) -----
            Material laneMat = LoadOrCreateMaterial("LaneMat", new Color(0.85f, 0.65f, 0.35f));
            Material railMat = LoadOrCreateMaterial("RailMat", new Color(0.25f, 0.25f, 0.3f));
            Material ballMat = LoadOrCreateMaterial("BallMat", new Color(0.15f, 0.35f, 0.9f));
            Material pinMat  = LoadOrCreateMaterial("PinMat", Color.white);

            // ----- 4. Environment -----
            GameObject env = new GameObject("Environment");

            // Rails sit this far either side of a lane's centre line, and their
            // box is 0.25 wide, so their OUTER face is another 0.125 beyond that.
            float railOffset = width * 0.5f + 0.55f;
            const float RailHalfThickness = 0.125f;
            Vector3 railScale = new Vector3(0.25f, 0.35f, length + 4f);

            // How wide the whole built alley is, INCLUDING the cosmetic neighbour
            // lanes below. Floor and backstop are derived from this rather than
            // from the single lane's width: otherwise the neighbour lanes would
            // float over nothing, and the backstop would visibly END inside the
            // throw camera's impact framing (which spans several lane widths at
            // the pins). When neighbour lanes are off this reduces to almost
            // exactly the old width+3 / width+2.4 numbers.
            int neighbourCount = laneConfig.BuildNeighbourLanes ? laneConfig.NeighbourLanesPerSide : 0;
            float outermostEdge = neighbourCount * laneConfig.NeighbourLaneSpacing + railOffset + RailHalfThickness;

            MakeBox(env, "Floor", new Vector3(0, -0.051f, length * 0.5f - 2f),
                    new Vector3(outermostEdge * 2f + 1.5f, 0.1f, length + 8f), railMat);
            MakeBox(env, "Lane", new Vector3(0, -0.05f, length * 0.5f),
                    new Vector3(width, 0.102f, length + 1.5f), laneMat);
            MakeBox(env, "RailLeft", new Vector3(-railOffset, 0.12f, length * 0.5f),
                    railScale, railMat);
            MakeBox(env, "RailRight", new Vector3(railOffset, 0.12f, length * 0.5f),
                    railScale, railMat);
            MakeBox(env, "Backstop", new Vector3(0, 0.5f, length + 2.5f),
                    new Vector3(outermostEdge * 2f + 1f, 1.2f, 0.25f), railMat);

            // ----- 4b. Cosmetic neighbour lanes -----
            // PURE SET DRESSING for the wide camera beats: lane surface + rails
            // only. No pins, no Rigidbody, no PinDeck registration, no scoring,
            // and every collider stripped (MakeCosmeticBox) so nothing here can
            // ever touch the ball. Parented under their own root so it is obvious
            // at a glance which geometry is real and which is scenery.
            if (neighbourCount > 0)
            {
                GameObject neighbours = new GameObject("NeighbourLanes");
                for (int side = -1; side <= 1; side += 2)   // -1 = left, +1 = right
                {
                    for (int n = 1; n <= neighbourCount; n++)
                    {
                        float centreX = side * n * laneConfig.NeighbourLaneSpacing;
                        string label = (side < 0 ? "L" : "R") + n;

                        MakeCosmeticBox(neighbours, "NeighbourLane" + label,
                                        new Vector3(centreX, -0.05f, length * 0.5f),
                                        new Vector3(width, 0.102f, length + 1.5f), laneMat);
                        MakeCosmeticBox(neighbours, "NeighbourRail" + label + "Left",
                                        new Vector3(centreX - railOffset, 0.12f, length * 0.5f),
                                        railScale, railMat);
                        MakeCosmeticBox(neighbours, "NeighbourRail" + label + "Right",
                                        new Vector3(centreX + railOffset, 0.12f, length * 0.5f),
                                        railScale, railMat);
                    }
                }
            }

            // ----- 5. Light -----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ----- 6. Pins -----
            GameObject deckGo = new GameObject("PinDeck");
            deckGo.transform.position = new Vector3(0f, 0f, length);
            PinDeck deck = deckGo.AddComponent<PinDeck>();

            GameObject pinGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pinGo.name = "PinTemplate";
            pinGo.transform.SetParent(deckGo.transform);
            // Cylinder mesh is 2 units tall at scale 1, so scale-y = height/2.
            pinGo.transform.localScale = new Vector3(0.12f, pinConfig.PinHeight * 0.5f, 0.12f);
            pinGo.transform.localPosition = new Vector3(0f, pinConfig.PinHeight * 0.5f, 0f);
            pinGo.GetComponent<Renderer>().sharedMaterial = pinMat;
            // The default capsule collider has a ROUND bottom — pins would
            // wobble over on their own. A box collider stands firm and still
            // tips hilariously.
            Object.DestroyImmediate(pinGo.GetComponent<CapsuleCollider>());
            BoxCollider pinCollider = pinGo.AddComponent<BoxCollider>();
            pinGo.AddComponent<Rigidbody>();
            Pin pinTemplate = pinGo.AddComponent<Pin>();

            // Bounce + friction live on a physics material so scatter feel is
            // tunable, same pattern as the ball's BallBounce material below.
#if UNITY_6000_0_OR_NEWER
            var pinBounce = new PhysicsMaterial("PinBounce");
#else
            var pinBounce = new PhysicMaterial("PinBounce");
#endif
            pinBounce.bounciness = pinConfig.Bounciness;
            pinBounce.dynamicFriction = pinConfig.Friction;
            pinBounce.staticFriction = pinConfig.Friction;
            AssetDatabase.CreateAsset(pinBounce, ProjectRoot + "/ScriptableObjects/PinBounce.asset");
            pinCollider.sharedMaterial = pinBounce;

            pinGo.SetActive(false);

            deck.Initialize(pinConfig, pinTemplate);
            EditorUtility.SetDirty(deck);

            // ----- 7. Ball -----
            GameObject ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "BowlingBall";
            float d = ballConfig.Radius * 2f;
            ballGo.transform.localScale = new Vector3(d, d, d);
            ballGo.GetComponent<Renderer>().sharedMaterial = ballMat;
            ballGo.AddComponent<Rigidbody>();
            BowlingBall ball = ballGo.AddComponent<BowlingBall>();

            // Bounciness lives on a physics material so the feel is tunable.
#if UNITY_6000_0_OR_NEWER
            var bounce = new PhysicsMaterial("BallBounce");
            bounce.bounceCombine = PhysicsMaterialCombine.Maximum;
#else
            var bounce = new PhysicMaterial("BallBounce");
            bounce.bounceCombine = PhysicMaterialCombine.Maximum;
#endif
            // Maximum combine: the ball's own Bounciness always wins over
            // whatever the floor/pins are set to, so bouncy ball variants
            // (power-ups) don't need a matching floor material to feel bouncy.
            bounce.bounciness = ballConfig.Bounciness;
            // ".asset" is the safe generic extension across Unity versions.
            AssetDatabase.CreateAsset(bounce, ProjectRoot + "/ScriptableObjects/BallBounce.asset");
            ballGo.GetComponent<SphereCollider>().sharedMaterial = bounce;

            // ----- 8. Ball spawn point -----
            // Y comes from BallConfig.SpawnHeight so Tony can tune it without code.
            // Default is chest height, so the ball drops onto the lane when thrown.
            GameObject spawn = new GameObject("BallSpawn");
            spawn.transform.position = new Vector3(0f, ballConfig.SpawnHeight, 0f);
            ballGo.transform.position = spawn.transform.position;

            // ----- 8b. Thrower (Body English) -----
            // Stands just behind the foul line, below the ball's spawn point.
            // Prefers the rigged PlayerCharacter prefab that CharacterSetupTool
            // builds; falls back to the original greybox capsule if that prefab
            // hasn't been generated yet, so a fresh clone still builds a working
            // scene. Both implement IThrowReactionActor, so everything
            // downstream (BowlingGameController) is identical either way.
            //
            // The character's origin is at its FEET, unlike the capsule (origin
            // at its middle), so the two need different Y — hence the separate
            // positions rather than one shared vector.
            Vector3 throwerGround = new Vector3(0f, 0f, -0.8f);
            MonoBehaviour throwReaction;
            // The thrower object itself, whichever branch produced it. Needed
            // below so ThrowerAimSlide can be attached to either one.
            GameObject throwerObject;

            // EVERY height on ThrowCameraSequenceConfig is measured relative to
            // this transform, and they were all tuned against the capsule, whose
            // pivot sat at floor+1m. The character's pivot is at its FEET, so
            // handing the camera the character's own transform would silently
            // drop all six beats by a metre and aim them at the floor. Instead
            // both branches hand the camera a pivot at the SAME floor+1m height,
            // which keeps the existing camera tuning valid as-is.
            //
            // The anchor is a STANDALONE object at the scene root, NOT a child
            // of the thrower — deliberately, now that ThrowerAimSlide moves the
            // character during aim. Parented, every camera beat would slide
            // sideways each time Tony adjusts lateral aim, and a frame that
            // moves while you aim makes it harder to judge where you're
            // pointing. Wii Sports keeps the aim camera FIXED and lets the
            // character slide within it (Tony's call); a fixed world position is
            // what implements that, and it keeps the six tuned
            // ThrowCameraSequenceConfig beats valid byte-for-byte.
            const float CameraAnchorHeight = 1f;

            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetupTool.PlayerCharacterPrefabPath);
            if (characterPrefab != null)
            {
                GameObject throwerGo = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                throwerGo.name = "Thrower";
                throwerGo.transform.position = throwerGround;
                // Face down the lane (+Z), the direction the ball travels.
                throwerGo.transform.rotation = Quaternion.identity;
                throwReaction = throwerGo.GetComponent<CharacterThrowReactionActor>();
                // A prefab generated before this component existed — or one where
                // the component ended up on the model child instead of the wrapper
                // root — returns null here, and BowlingGameController's null-safe
                // `_throwReaction?.PlayReaction(p)` would then swallow it silently:
                // no error, the thrower just never reacts again. Say so out loud.
                if (throwReaction == null)
                    Debug.LogWarning("[Greybox] The PlayerCharacter prefab has no CharacterThrowReactionActor on " +
                                     "its root, so Body English will not play. Re-run WeeSpurts -> Set Up " +
                                     "Player Character to regenerate it, then rebuild the scene.");

                throwerObject = throwerGo;
            }
            else
            {
                Debug.LogWarning("[Greybox] No PlayerCharacter prefab found — using the greybox capsule thrower. " +
                                 "Run WeeSpurts -> Set Up Player Character, then rebuild the scene.");
                Material throwerMat = LoadOrCreateMaterial("ThrowerMat", new Color(0.9f, 0.75f, 0.55f));
                // LoadOrCreateMaterial returns the ASSET AS-IS on every rebuild after
                // the first (see its "if (existing != null) return existing;" early-out),
                // so alpha/transparency can't be baked into the color argument above --
                // it would only apply the one time the asset didn't exist yet. Force it
                // unconditionally, every build, directly on the returned Material.
                MaterialTransparency.Apply(throwerMat, 0.35f);
                GameObject throwerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                throwerGo.name = "ThrowerProxy";
                // Capsule is 2m tall with a centre origin, so +1 puts its feet on
                // the floor. (It happens to sit at the camera anchor's height too,
                // but it is no longer used AS the anchor — see below.)
                throwerGo.transform.position = throwerGround + new Vector3(0f, CameraAnchorHeight, 0f);
                throwerGo.GetComponent<Renderer>().sharedMaterial = throwerMat;
                // Purely cosmetic (see CapsuleThrowReactionActor's doc comment) — it must
                // never be a physical obstacle. Without this, the default CapsuleCollider
                // that CreatePrimitive attaches sits ~0.8m behind the ball's spawn point
                // and blocks any throw sent toward -Z, e.g. the backward-fumble gag.
                Object.DestroyImmediate(throwerGo.GetComponent<CapsuleCollider>());
                throwReaction = throwerGo.AddComponent<CapsuleThrowReactionActor>();
                throwerObject = throwerGo;
            }

            // Both branches share ONE anchor, built the same way, at the scene
            // root. Previously the capsule served as its own anchor — harmless
            // while it never moved, but it would now slide with the aim and drag
            // the camera with it, so the two branches would have quietly
            // disagreed about whether the camera moves. A fixed anchor rather
            // than a real bone, too: bones move with the animation, and a camera
            // anchored to a moving chest would jitter through the reaction clips.
            GameObject cameraAnchor = new GameObject("CameraAnchor");
            cameraAnchor.transform.position = throwerGround + new Vector3(0f, CameraAnchorHeight, 0f);
            Transform throwerCameraAnchor = cameraAnchor.transform;

            // ----- 9. Camera -----
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            ThrowCamera throwCam = camGo.AddComponent<ThrowCamera>();
            // Raised + pulled back and pitched down a touch so the chest-height
            // ball (BallConfig.SpawnHeight) stays framed with the lane visible ahead.
            throwCam.ConfigureAimView(new Vector3(0f, 2.3f, -3.7f), new Vector3(16f, 0f, 0f), ballGo.transform);
            EditorUtility.SetDirty(throwCam);

            // ----- 10. Managers + game controller -----
            GameObject managers = new GameObject("GameManager");
            managers.AddComponent<GameManager>();
            managers.AddComponent<SceneLoader>();
            managers.AddComponent<AudioManager>();

            GameObject gameGo = new GameObject("BowlingGame");
            BallLauncher launcher = gameGo.AddComponent<BallLauncher>();
            BowlingGameController controller = gameGo.AddComponent<BowlingGameController>();
            controller.Configure(ballConfig, laneConfig, ball, deck, launcher, throwCam, spawn.transform);
            controller.SetThrowReactionActor(throwReaction);

            // Sandbox aim-phase preview: slides the ball to match live aim
            // input and draws a curved LineRenderer for direction + spin.
            // Purely visual (AimPreview.cs) — never touches LaunchParameters.
            Material aimLineMat = LoadOrCreateMaterial("AimLineMat", new Color(1f, 0.9f, 0.1f));
            // Prefer an unlit shader so the line reads as a flat, always-
            // visible indicator color instead of shifting with scene
            // lighting. Falls back to whatever LoadOrCreateMaterial already
            // picked (Lit/Standard) if URP's Unlit shader isn't present.
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader != null) aimLineMat.shader = unlitShader;
            LineRenderer aimLine = ballGo.AddComponent<LineRenderer>();
            aimLine.sharedMaterial = aimLineMat;
            aimLine.startWidth = 0.03f;
            aimLine.endWidth = 0.015f;
            AimPreview aimPreview = ballGo.AddComponent<AimPreview>();
            aimPreview.Configure(launcher, controller, aimLine);
            EditorUtility.SetDirty(aimPreview);

            // Thrower slides with the aim. Attached here rather than up in
            // section 8b because it needs the launcher and controller, which
            // don't exist until this section. Reads HalfLaneWidth off the same
            // controller AimPreview just got, so character, preview and resolved
            // throw are all bound to one number.
            ThrowerAimSlide aimSlide = throwerObject.AddComponent<ThrowerAimSlide>();
            aimSlide.Configure(launcher, controller);
            EditorUtility.SetDirty(aimSlide);

            // Sandbox ball switcher (press 1-9 in Play mode). A rebuild always
            // produces the SAME five slots in the SAME order — 1 default,
            // 2 BouncyBall, 3 Cannonball, 4 Wobbler, 5 Nuke — so muscle memory
            // holds between rebuilds. Clear first: this list is ordinary
            // serialized state, and merging into whatever a stale default Preset
            // or hand-edit left behind is how the plain throw went missing from
            // slot 1 while the four powerups stayed.
            BallConfigSwitcher switcher = gameGo.AddComponent<BallConfigSwitcher>();
            switcher.EditorClearConfigs();
            switcher.EditorAddConfig(ballConfig);
            foreach (string variantName in new[] { "BouncyBall", "Cannonball" })
            {
                var variant = AssetDatabase.LoadAssetAtPath<BallConfig>(
                    ProjectRoot + "/ScriptableObjects/" + variantName + ".asset");
                switcher.EditorAddConfig(variant);
            }

            // Wobbler is a ball PERSONALITY (continuous weave), not a hand-tuned
            // powerup like BouncyBall/Cannonball, so unlike those it doesn't exist
            // yet — create it here if missing. Only stamp the Wobbler-specific
            // tuning the FIRST time it's created, so re-running this builder after
            // Tony hand-tunes it in the Inspector doesn't silently stomp his values.
            string wobblerPath = ProjectRoot + "/ScriptableObjects/Wobbler.asset";
            bool wobblerAlreadyExisted = AssetDatabase.LoadAssetAtPath<BallConfig>(wobblerPath) != null;
            BallConfig wobblerConfig = LoadOrCreateAsset<BallConfig>(wobblerPath);
            if (!wobblerAlreadyExisted)
            {
                wobblerConfig.WobbleForceMagnitude = 12f;
                wobblerConfig.WobbleFrequencyHz = 0.5f;
                EditorUtility.SetDirty(wobblerConfig);
            }
            switcher.EditorAddConfig(wobblerConfig);

            // Nuke Shot (GameBible §9 powerup prototype): same create-if-missing
            // pattern as Wobbler above — only stamp the Nuke-specific tuning the
            // FIRST time it's created so a rebuild after Tony hand-tunes it
            // doesn't stomp his values.
            string nukePath = ProjectRoot + "/ScriptableObjects/Nuke.asset";
            bool nukeAlreadyExisted = AssetDatabase.LoadAssetAtPath<BallConfig>(nukePath) != null;
            BallConfig nukeConfig = LoadOrCreateAsset<BallConfig>(nukePath);
            if (!nukeAlreadyExisted)
            {
                nukeConfig.IsNuke = true;
                nukeConfig.NukeBlastRadius = 4f;
                nukeConfig.NukeExplosionForce = 14f;
                nukeConfig.NukeTweenDuration = 0.5f;
                nukeConfig.NukeLockOnPauseDuration = 0.6f;
                nukeConfig.NukeGreenZoneMin = 0.82f;
                nukeConfig.NukeGreenZoneMax = 0.85f;
                EditorUtility.SetDirty(nukeConfig);
            }
            switcher.EditorAddConfig(nukeConfig);
            // Every other configured component in this builder is marked dirty;
            // this one was relying on the scene save picking it up.
            EditorUtility.SetDirty(switcher);

            // ----- Nuke Shot presentation layer (greybox) -----
            // Pure Transform-tweened visual, NOT a physics object — see
            // NukeShotResolver's doc comment. Must NOT collide with the ball
            // or pins, so its default SphereCollider is removed immediately
            // (same lesson as ThrowerProxy above: GameObject.CreatePrimitive
            // always attaches a default collider).
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
            // A one-shot "poof", not a looping effect: AddComponent<ParticleSystem>()
            // defaults to loop = true, so without this override the very first
            // poofEffect.Play() (NukeShotResolver) would run forever. Duration/
            // startLifetime just need to read as a quick puff for this greybox
            // placeholder — not precisely tuned. stopAction = None (not Disable/
            // Destroy) because this same GameObject/ParticleSystem is reused for
            // every future nuke throw; None just means "do nothing extra when it
            // naturally finishes," which is exactly what a reusable effect needs.
            nukePoofMain.loop = false;
            nukePoofMain.duration = 1f;
            nukePoofMain.startLifetime = 1f;
            nukePoofMain.stopAction = ParticleSystemStopAction.None;
            nukePoof.Stop();

            // AddComponent<ParticleSystem>() defaults its ParticleSystemRenderer to
            // a Built-in-Render-Pipeline particle shader, which isn't part of URP's
            // shader set — in this URP project (confirmed: com.unity.render-
            // pipelines.universal in Packages/manifest.json) that renders as URP's
            // solid-magenta missing-shader fallback, not an intentional pink VFX
            // color. Explicitly assign a URP-compatible particle shader, same
            // Find-then-fallback shape LoadOrCreateMaterial already uses for
            // Lit/Standard below.
            Material nukePoofMat = LoadOrCreateMaterial("NukePoofMat", new Color(1f, 0.6f, 0.1f));
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader != null) nukePoofMat.shader = particleShader;
            nukePoofGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = nukePoofMat;

            GameObject nukeShotGo = new GameObject("NukeShot");
            NukeShotResolver nukeResolver = nukeShotGo.AddComponent<NukeShotResolver>();
            // NukeShotResolver's two references are deliberately PRIVATE
            // [SerializeField] fields (minimal public surface), so wire them via
            // SerializedObject rather than adding a public setter just for this.
            // Both fields carry [SerializeField], so — unlike a plain public
            // field assigned only in C# — this survives a scene reload; see the
            // AimPreview lesson this file already learned once (CLAUDE.md/Bible).
            var nukeResolverSO = new SerializedObject(nukeResolver);
            nukeResolverSO.FindProperty("nukeSphere").objectReferenceValue = nukeSphereGo.transform;
            nukeResolverSO.FindProperty("poofEffect").objectReferenceValue = nukePoof;
            nukeResolverSO.ApplyModifiedPropertiesWithoutUndo();
            controller.SetNukeResolver(nukeResolver);
            EditorUtility.SetDirty(nukeResolver);

            // ----- 10b. Scripted throw camera (the seven-beat cinematic move) -----
            // Same create-if-missing pattern as Wobbler/Nuke above: only stamp
            // creation-time values the FIRST time the asset appears, so re-running
            // this builder after Tony hand-tunes the camera never stomps his work.
            string sequencePath = ProjectRoot + "/ScriptableObjects/ThrowCameraSequenceConfig.asset";
            bool sequenceConfigAlreadyExisted =
                AssetDatabase.LoadAssetAtPath<ThrowCameraSequenceConfig>(sequencePath) != null;
            ThrowCameraSequenceConfig sequenceConfig = LoadOrCreateAsset<ThrowCameraSequenceConfig>(sequencePath);
            if (!sequenceConfigAlreadyExisted)
            {
                // A freshly created asset already carries every tuned default from
                // ThrowCameraSequenceConfig's own C# field initialisers, so there is
                // nothing extra to stamp here. This branch exists so that any future
                // creation-time default has an obvious home which can never run on
                // a rebuild over an existing asset.
                EditorUtility.SetDirty(sequenceConfig);
            }

            ThrowCameraSequence throwCamSequence = camGo.AddComponent<ThrowCameraSequence>();
            // Same SerializedObject pattern as NukeShotResolver above: every field
            // is a private [SerializeField], and writing them this way is what makes
            // the wiring survive a scene reload (the AimPreview lesson, again).
            var sequenceSO = new SerializedObject(throwCamSequence);
            sequenceSO.FindProperty("config").objectReferenceValue = sequenceConfig;
            sequenceSO.FindProperty("launcher").objectReferenceValue = launcher;
            sequenceSO.FindProperty("game").objectReferenceValue = controller;
            sequenceSO.FindProperty("ball").objectReferenceValue = ball;
            sequenceSO.FindProperty("pinDeck").objectReferenceValue = deckGo.transform;
            sequenceSO.FindProperty("thrower").objectReferenceValue = throwerCameraAnchor;
            sequenceSO.FindProperty("laneConfig").objectReferenceValue = laneConfig;
            sequenceSO.FindProperty("sequenceCamera").objectReferenceValue = cam;
            sequenceSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(throwCamSequence);

            gameGo.AddComponent<DebugHud>();
            // The 2D spin selector (replaces the old Q/E spin keys). Same
            // GameObject as DebugHud — both are OnGUI overlays that find the
            // controller via RequireComponent, so neither needs wiring.
            gameGo.AddComponent<SpinSelectorHud>();
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(switcher);

            // ----- 11. Save scene + register in build settings -----
            // EditorUtility.SetDirty() only flags objects dirty; it doesn't itself
            // write to disk. Newly-created assets above (e.g. Wobbler.asset) are
            // written by AssetDatabase.CreateAsset, but the SetDirty'd tuning
            // values on them need an explicit SaveAssets to be guaranteed flushed.
            AssetDatabase.SaveAssets();
            EnsureFolder(ProjectRoot + "/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log("WeeSpurts: Greybox bowling scene built and saved to " + ScenePath +
                      ". Press PLAY — arrows aim, hold SPACE for power, release to throw.");
        }

        // ---------- helpers ----------

        private static GameObject MakeBox(GameObject parent, string name, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// MakeBox for DECORATION: identical, but with the collider removed.
        ///
        /// THE LESSON THIS FILE HAS NOW LEARNED THREE TIMES (ThrowerProxy, the
        /// Nuke sphere, and now the neighbour lanes): GameObject.CreatePrimitive
        /// ALWAYS attaches a collider. Anything purely visual must have it removed
        /// or it silently becomes a physical obstacle — an invisible wall that
        /// only shows up as "why did the ball bounce off nothing?" hours later.
        /// </summary>
        private static GameObject MakeCosmeticBox(GameObject parent, string name, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = MakeBox(parent, name, position, scale, mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = ProjectRoot + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // URP projects render "Standard" as pink; pick whichever exists.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { color = color };
            EnsureFolder(ProjectRoot + "/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
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
