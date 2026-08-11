using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Interaction;
using WeeSpurts.Player;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK gets a walkable first-person player into ThunderLanesVenue.unity
    /// — the hand-built 611-mesh art scene imported in commit 38f064c from a
    /// separate scratch project. That import deliberately left out the source
    /// project's own FirstPersonController.cs, so the scene's placeholder
    /// "Player" object carries a dangling script reference (guid
    /// 6f4c0019fd90e41a8b90484093047d1d resolves to nothing in this project)
    /// plus an old-Input-System PlayerInput component this project doesn't use
    /// anywhere else. This tool replaces that placeholder with WeeSpurts' own
    /// PlayerCharacter prefab, wired the same way RoamingSetupTool wires it for
    /// TestVenue.unity — same RoamConfig/InteractionConfig assets, same
    /// component set, same SerializedObject wiring discipline.
    ///
    /// Menu: WeeSpurts -> Thunder Lanes Venue -> Set Up Roaming Player
    ///
    /// WHY NOT JUST RUN RoamingSetupTool ON THIS SCENE: that tool requires a
    /// CharacterThrowReactionActor (the thrower) AND a Camera carrying
    /// ThrowCamera to already be in the scene — both come from
    /// GreyboxSceneBuilder, and this scene has neither. ThunderLanesVenue is
    /// pure imported environment art: no lane, no pins, no ball, no
    /// BowlingPresentation. Loosening RoamingSetupTool's requirements to cover
    /// that case would change tested behaviour for TestVenue/BowlingAlley,
    /// which this tool avoids by existing separately instead.
    ///
    /// SCOPE: ROAMING ONLY. Walking, looking around and interacting works;
    /// bowling does not, because there is nothing bowling-related in this
    /// scene yet. Retrofitting real gameplay (PinDeck/pins/ball/ThrowCamera/
    /// BowlingPresentation) onto one of the scene's existing art lane pairs
    /// (e.g. Std_Lane_Pair_3_4, VIP_Lane_Pair_1_2 — named meshes only, no
    /// gameplay components) is separate follow-up work, deliberately not done
    /// here — see the report this tool prints for exactly what is and isn't
    /// wired.
    ///
    /// IDEMPOTENT: safe to run again. Reuses an existing Player root rather
    /// than stacking duplicates, and never overwrites RoamConfig/
    /// InteractionConfig once they exist.
    ///
    /// Marks the scene DIRTY but does NOT save it — same precedent as
    /// RoamingSetupTool and every other builder in this project.
    /// </summary>
    public static class ThunderLanesVenueRoamingSetupTool
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenePath = ProjectRoot + "/Scenes/ThunderLanesVenue.unity";
        private const string RoamConfigPath = ProjectRoot + "/ScriptableObjects/RoamConfig.asset";
        private const string InteractionConfigPath = ProjectRoot + "/ScriptableObjects/InteractionConfig.asset";
        private const string PlayerCharacterPrefabPath = ProjectRoot + "/Prefabs/PlayerCharacter.prefab";

        private const string PlayerRootName = "Player";
        private const string ThrowerName = "Thrower";
        private const string FirstPersonCameraName = "FirstPersonCamera";
        private const string LegacyPlaceholderName = "Player";

        /// <summary>Same user layer RoamingSetupTool requires, for the same reason (see its class comment).</summary>
        private const string LocalPlayerModelLayer = "LocalPlayerModel";

        /// <summary>
        /// Batch-mode entry point (`-executeMethod`): opens
        /// <see cref="ScenePath"/>, runs <see cref="SetUp"/>, and saves.
        /// EXACT SAME CODE PATH as the menu item — this only exists so the
        /// setup can be driven from the command line instead of by hand. Not
        /// on the menu itself; the menu item already covers the interactive
        /// case, and having both would invite them to drift apart.
        /// </summary>
        public static void RunAndSave()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[ThunderLanesVenue] Could not open {ScenePath} — aborting before anything changed.");
                return;
            }

            SetUp();

            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log(saved
                ? $"[ThunderLanesVenue] Saved {ScenePath}."
                : $"[ThunderLanesVenue] FAILED to save {ScenePath} — check the log above for why SetUp aborted.");
        }

        [MenuItem("WeeSpurts/Thunder Lanes Venue/Set Up Roaming Player")]
        public static void SetUp()
        {
            Scene scene = SceneManager.GetActiveScene();

            // ----- 0. Layer check FIRST, so aborting leaves the scene untouched -----
            int modelLayer = LayerMask.NameToLayer(LocalPlayerModelLayer);
            if (modelLayer < 0)
            {
                Debug.LogError(
                    "[ThunderLanesVenue] MISSING LAYER — nothing was changed.\n" +
                    $"This scene needs a user layer called '{LocalPlayerModelLayer}'.\n" +
                    "FIX IT LIKE THIS: Edit -> Project Settings -> Tags and Layers, " +
                    $"type '{LocalPlayerModelLayer}' into User Layer 6, then run this menu item again.\n" +
                    "(Same layer RoamingSetupTool uses for TestVenue — if that scene already works, " +
                    "this layer should already exist project-wide.)");
                return;
            }

            // ----- 1. The Player root: reuse on a re-run, else build fresh -----
            PlayerAvatar existingAvatar = FindFirst<PlayerAvatar>();
            GameObject playerRoot;
            string spawnNote;

            if (existingAvatar != null)
            {
                playerRoot = existingAvatar.gameObject;
                spawnNote = $"reused existing Player root at {playerRoot.transform.position}";
            }
            else
            {
                // The imported placeholder "Player" (foreign FirstPersonController +
                // PlayerInput + CharacterController, no PlayerAvatar) tells us where
                // the source scene meant a player to start. Capture its transform,
                // then remove it — it is dead scaffolding this project can't run,
                // and leaving it in place is what shows as a permanent Missing
                // Script warning on every scene load.
                GameObject legacy = GameObject.Find(LegacyPlaceholderName);
                Vector3 spawnPos = Vector3.zero;
                Quaternion spawnRot = Quaternion.identity;

                if (legacy != null && legacy.GetComponent<PlayerAvatar>() == null)
                {
                    spawnPos = legacy.transform.position;
                    spawnRot = legacy.transform.rotation;
                    spawnNote = $"removed the imported placeholder '{LegacyPlaceholderName}' (dangling script, " +
                                $"old Input System PlayerInput) and its child camera; spawning at its position " +
                                $"{spawnPos}";
                    Object.DestroyImmediate(legacy);
                }
                else
                {
                    spawnNote = "no placeholder 'Player' found — spawning at the world origin; move the " +
                                "Player root by hand if that lands inside geometry";
                }

                playerRoot = new GameObject(PlayerRootName);
                playerRoot.transform.SetPositionAndRotation(spawnPos, spawnRot);
                // Explicit, even though it's the default: the CharacterController
                // below depends on this root staying at scale 1 (see
                // RoamingSetupTool's class comment for why — same reasoning here).
                playerRoot.transform.localScale = Vector3.one;
            }

            // ----- 2. RoamConfig / InteractionConfig (shared, create-once) -----
            bool roamConfigAlreadyExisted = AssetDatabase.LoadAssetAtPath<RoamConfig>(RoamConfigPath) != null;
            RoamConfig roamConfig = LoadOrCreateAsset<RoamConfig>(RoamConfigPath);
            if (!roamConfigAlreadyExisted) EditorUtility.SetDirty(roamConfig);

            bool interactionConfigAlreadyExisted =
                AssetDatabase.LoadAssetAtPath<InteractionConfig>(InteractionConfigPath) != null;
            InteractionConfig interactionConfig = LoadOrCreateAsset<InteractionConfig>(InteractionConfigPath);
            if (!interactionConfigAlreadyExisted) EditorUtility.SetDirty(interactionConfig);

            // ----- 3. The character model (Thrower child) -----
            Transform throwerTransform = playerRoot.transform.Find(ThrowerName);
            GameObject thrower;
            if (throwerTransform != null)
            {
                thrower = throwerTransform.gameObject;
            }
            else
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError(
                        $"[ThunderLanesVenue] Cannot find {PlayerCharacterPrefabPath} — nothing further was " +
                        "changed (the Player root above was already created/reused).\n" +
                        "FIX IT LIKE THIS: run WeeSpurts -> Set Up Player Character first, then run this menu " +
                        "item again.");
                    return;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = ThrowerName;
                instance.transform.SetParent(playerRoot.transform, worldPositionStays: false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                thrower = instance;
            }

            CharacterThrowReactionActorRef(thrower, out var reactionActor);
            if (reactionActor == null)
            {
                Debug.LogError(
                    $"[ThunderLanesVenue] '{PlayerCharacterPrefabPath}' has no CharacterThrowReactionActor on " +
                    "its root — nothing further was wired. Did the prefab change shape?");
                return;
            }

            // ----- 4. The first-person camera child -----
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
            firstPersonCameraGo.transform.localPosition = new Vector3(0f, roamConfig.EyeHeight, 0f);
            firstPersonCameraGo.transform.localRotation = Quaternion.identity;

            Camera firstPersonCamera = GetOrAdd<Camera>(firstPersonCameraGo);
            AudioListener firstPersonListener = GetOrAdd<AudioListener>(firstPersonCameraGo);
            // OFF at setup time, same as RoamingSetupTool: PlayerAvatar.Start()
            // -> EnterRoaming() -> ApplyMode() enables both at Play time via
            // PlayerCameraDirector.Apply(). No reason to duplicate that runtime
            // path here, even though this scene only ever has one mode.
            firstPersonCamera.enabled = false;
            firstPersonListener.enabled = false;

            // ----- 5. Components on the Player root -----
            CharacterController controller = GetOrAdd<CharacterController>(playerRoot);
            controller.radius = roamConfig.ControllerRadius;
            controller.height = roamConfig.ControllerHeight;
            controller.center = new Vector3(0f, roamConfig.ControllerHeight * 0.5f, 0f);
            controller.stepOffset = roamConfig.StepOffset;
            controller.slopeLimit = roamConfig.SlopeLimit;
            controller.skinWidth = Mathf.Max(0.01f, roamConfig.ControllerRadius * 0.1f);

            PlayerAvatar avatar = GetOrAdd<PlayerAvatar>(playerRoot);
            FirstPersonController firstPerson = GetOrAdd<FirstPersonController>(playerRoot);
            PlayerCameraDirector cameraDirector = GetOrAdd<PlayerCameraDirector>(playerRoot);

            PlayerInteractor interactor = GetOrAdd<PlayerInteractor>(playerRoot);
            InteractionPromptHud promptHud = GetOrAdd<InteractionPromptHud>(playerRoot);

            // ----- 6. Wire everything -----
            // SerializedObject, not plain assignment: an editor-time reference
            // Unity doesn't serialize this way is null again after the next
            // scene reload (the AimPreview lesson, same as RoamingSetupTool).
            var avatarSo = new SerializedObject(avatar);
            Wire(avatarSo, "characterController", controller);
            Wire(avatarSo, "firstPersonController", firstPerson);
            Wire(avatarSo, "interactor", interactor);
            // No ThrowerAimSlide, no throwerModel-offset concern: this scene has
            // no bowling mode to switch away from, so those two fields are left
            // unset on purpose (bowling-only concerns — see PlayerAvatar.ApplyMode).
            avatarSo.ApplyModifiedPropertiesWithoutUndo();

            var interactorSo = new SerializedObject(interactor);
            Wire(interactorSo, "config", interactionConfig);
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

            var directorSo = new SerializedObject(cameraDirector);
            Wire(directorSo, "avatar", avatar);
            Wire(directorSo, "firstPersonCamera", firstPersonCamera);
            Wire(directorSo, "firstPersonListener", firstPersonListener);
            // bowlingCamera / bowlingListener left unwired (null): there is no
            // bowling camera in this scene. PlayerCameraDirector.Apply() already
            // null-guards both fields, so roaming still works correctly with
            // them empty — see its class comment.
            directorSo.ApplyModifiedPropertiesWithoutUndo();

            // ----- 7. Local-model layer + culling -----
            int modelLayerBit = 1 << modelLayer;
            int movedToLayer = SetLayerRecursively(thrower, modelLayer);
            firstPersonCamera.cullingMask &= ~modelLayerBit;

            // ----- 8. Flush + report -----
            EditorUtility.SetDirty(playerRoot);
            EditorUtility.SetDirty(avatar);
            EditorUtility.SetDirty(firstPerson);
            EditorUtility.SetDirty(cameraDirector);
            EditorUtility.SetDirty(interactor);
            EditorUtility.SetDirty(promptHud);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(firstPersonCamera);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[ThunderLanesVenue] Set up in scene '{scene.name}'. The scene is DIRTY and NOT saved — " +
                "look at it first, then Ctrl+S to keep it (Ctrl+Z / reopen to discard).\n\n" +
                $"PLAYER ROOT: {spawnNote}.\n" +
                $"HIERARCHY: {playerRoot.name} -> {thrower.name} + {FirstPersonCameraName} at eye height " +
                $"{roamConfig.EyeHeight:0.00}m.\n" +
                $"CHARACTER CONTROLLER: radius {controller.radius:0.00}m, height {controller.height:0.00}m, " +
                $"step offset {controller.stepOffset:0.00}m, slope limit {controller.slopeLimit:0}deg.\n" +
                $"LOCAL MODEL LAYER: '{LocalPlayerModelLayer}' applied to {movedToLayer} object(s) under " +
                $"{thrower.name}, culled from {FirstPersonCameraName}. No bowling camera exists yet to keep " +
                "it visible on, so there is nothing to un-cull it for — revisit when one exists.\n" +
                $"INTERACTION: PlayerInteractor + InteractionPromptHud on {playerRoot.name}, config " +
                $"'{InteractionConfigPath}' (range {interactionConfig.Range:0.00}m, cone " +
                $"+/-{interactionConfig.FacingAngleDegrees:0}deg, key {interactionConfig.InteractKey}) — but " +
                "nothing in this scene implements IInteractable yet, so it will find nothing to prompt for.\n" +
                "BOWLING: NOT WIRED. This scene has no lane, pins, ball, ThrowCamera or BowlingPresentation — " +
                "only art meshes named for them (Std_Lane_Pair_3_4, VIP_Lane_Pair_1_2, BowlingBall_*, Pin_*, " +
                "etc.). Roaming and looking around work; there is no way to start a match here yet. That is " +
                "separate follow-up work: pick one lane pair, retrofit real PinDeck/pins/ball/ThrowCamera/" +
                "BowlingPresentation onto it, then re-run this tool to get the lane kiosk.\n\n" +
                "NOW TEST: press Play. You should be standing in the venue in first person — WASD to walk, " +
                "mouse to look, Shift to sprint. Walk the concourse and confirm you don't clip through walls " +
                "or fall through the floor; report back anything that does, since this is the first time this " +
                "art has had a real collider-driven character in it.");
        }

        // ---------- helpers ----------

        private static void CharacterThrowReactionActorRef(GameObject thrower,
            out WeeSpurts.Bowling.CharacterThrowReactionActor reactionActor)
        {
            reactionActor = thrower.GetComponent<WeeSpurts.Bowling.CharacterThrowReactionActor>();
        }

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

        private static T FindFirst<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            return found.Length > 0 ? found[0] : null;
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
