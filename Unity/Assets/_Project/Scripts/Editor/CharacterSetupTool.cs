using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK turns the raw Quaternius + Mixamo downloads into a usable
    /// player character:
    ///   1. Character FBX  -> Generic rig, avatar built from the model.
    ///   1b. Its materials -> remapped to transparent copies, so the thrower is
    ///                        see-through Wii Sports style (CharacterOpacity).
    ///   2. Mixamo FBXs    -> Generic rig, avatar COPIED from the character,
    ///                        so the clips bind to our exact skeleton, and ONE
    ///                        take imported out of the dozen each file carries.
    ///   3. Builds PlayerCharacter.controller (states + parameters).
    ///   4. Builds PlayerCharacter.prefab (model + Animator + reaction actor),
    ///      sized once at the root by CharacterDisplayScale.
    ///
    /// Menu: WeeSpurts -> Set Up Player Character
    ///
    /// WHY GENERIC AND NOT HUMANOID (this was tried first and it fails):
    /// these Quaternius FBXs export a Blender IK CONTROL rig, not a clean
    /// deform skeleton. Foot.L/Foot.R are parented to the root bone as IK
    /// targets — siblings of the leg chain, not children of LowerLeg — and
    /// UpperLeg hangs off "Body" rather than off Hips. Unity's Humanoid
    /// validates the HIERARCHY, not just bone names, and requires LeftFoot to
    /// descend from LeftLowerLeg, so the import fails with "Required human
    /// bone 'LeftFoot' not found". No amount of explicit bone mapping fixes
    /// that; the rig itself would have to be re-parented in Blender.
    ///
    /// Generic is not a downgrade here, because Humanoid's whole job is
    /// retargeting between DIFFERENT skeletons and Mixamo already did that at
    /// export time — these clips ship retargeted onto this exact skeleton
    /// (identical bone names AND hierarchy), with every bone including the
    /// detached feet fully baked. So they play correctly as-is.
    ///
    /// What Generic costs us: Humanoid-only features (Animator IK, foot IK,
    /// humanoid avatar masks) and the "drop in any rigged humanoid, zero code
    /// changes" promise in ContentPlan.md. Revisit when Tony's own characters
    /// arrive — Mixamo's auto-rigger produces a proper FK hierarchy, so those
    /// WILL import as Humanoid, and only these two lines need to change.
    ///
    /// WHY code instead of clicking through the Inspector? Same reason as
    /// GreyboxSceneBuilder: it's reproducible, it survives a fresh clone, and
    /// it means no hand-edited prefab/controller YAML (see CLAUDE.md). Swap
    /// CharacterModelPath below and re-run to try a different body.
    /// Safe to run repeatedly.
    /// </summary>
    public static class CharacterSetupTool
    {
        private const string ProjectRoot = "Assets/_Project";

        /// <summary>
        /// Which of the eight Quaternius bodies is "the" player character.
        /// Purely a look call (Tony's, per CLAUDE.md) — every variant shares
        /// one skeleton, so changing this and re-running swaps the model with
        /// zero other changes. Options live in Characters/: Male_Casual,
        /// Male_LongSleeve, Male_Shirt, Male_Suit and their Smooth_ versions
        /// (Smooth_ = soft-shaded, plain = low-poly facets visible).
        /// </summary>
        private const string CharacterModelPath = ProjectRoot + "/Characters/Smooth_Male_Casual.fbx";

        /// <summary>
        /// Uniform scale applied to the PlayerCharacter PREFAB ROOT — the one
        /// and only place the character's display size is set. Tony's knob:
        /// change the number, re-run the menu item, done.
        ///
        /// It lives on the prefab root rather than in the FBX importer on
        /// purpose. Under a Generic rig the Mixamo clips drive absolute bone
        /// LOCAL POSITIONS, baked in the animation FBX's own import units, so a
        /// model imported at a different Scale Factor than its clips gets its
        /// bones shoved to offsets the skin was never fitted to — limbs stretch
        /// away from their joints ("Slenderman"). Scaling the ROOT scales the
        /// mesh and the animated bone positions together, so proportions are
        /// preserved by construction and this number can safely be anything.
        /// </summary>
        /// (static readonly, not const, so the guard test that it stays positive
        /// is a real runtime check rather than a literal the compiler inlines.)
        public static readonly float CharacterDisplayScale = 0.4f;

        /// <summary>
        /// How opaque the thrower is, 0 (invisible) .. 1 (solid). Wii Sports
        /// keeps the thrower clearly readable but lets you see the lane through
        /// them, which is roughly 0.5-0.7 — hence 0.6. Tony's knob: change the
        /// number, re-run the menu item, done.
        ///
        /// Applied in the GENERATOR rather than by hand-editing a material in
        /// the scene, so re-running this tool or reimporting the FBX can't
        /// silently revert it back to opaque.
        /// </summary>
        public static readonly float CharacterOpacity = 0.6f;

        /// <summary>
        /// Scale Factor forced onto EVERY character and animation FBX. The value
        /// barely matters; that it is IDENTICAL across all of them is what
        /// matters (see CharacterDisplayScale). 1 = "whatever the file says",
        /// which keeps the model and its clips in the same units.
        /// </summary>
        private const float ImportScale = 1f;

        /// <summary>
        /// Mixamo names the take you actually downloaded "mixamo.com". Every
        /// other take in the file is baggage — see <see cref="SelectTakeIndex"/>.
        /// </summary>
        private const string MixamoTakeName = "mixamo.com";

        private const string CharactersFolder = ProjectRoot + "/Characters";
        private const string MaterialsFolder = ProjectRoot + "/Materials";
        private const string AnimationsFolder = ProjectRoot + "/Animations";
        private const string PrefabFolder = ProjectRoot + "/Prefabs";
        private const string ControllerPath = PrefabFolder + "/PlayerCharacter.controller";
        public const string PlayerCharacterPrefabPath = PrefabFolder + "/PlayerCharacter.prefab";

        /// <summary>
        /// The Mixamo clips we expect, and whether each one loops. Idles and
        /// locomotion loop; one-shot reactions must NOT, or the character would
        /// celebrate forever. Name = the FBX file name in Animations/.
        /// </summary>
        private static readonly (string File, bool Loop)[] Clips =
        {
            ("Idle",       true),
            ("Walking",    true),
            ("Drunk Idle", true),
            ("Excited",    false),
            ("Defeat",     false),
            ("Fall Flat",  false),
        };

        [MenuItem("WeeSpurts/Set Up Player Character")]
        public static void SetUp()
        {
            // ----- 1. Character FBX -> Generic (see class doc for why not Humanoid) -----
            ModelImporter characterImporter = AssetImporter.GetAtPath(CharacterModelPath) as ModelImporter;
            if (characterImporter == null)
            {
                Debug.LogError($"[CharacterSetup] No model found at {CharacterModelPath}. " +
                               "Check the file name, or point CharacterModelPath at a different body.");
                return;
            }

            // Fix EVERY body in Characters/, not just the one we build the
            // prefab from. They all share the same unmappable IK rig, so any
            // left on Humanoid re-log "Required human bone 'LeftFoot' not
            // found" on every reimport and bury real errors in the console.
            // It also means swapping CharacterModelPath needs no extra cleanup.
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter bodyImporter)) continue;

                // Skip a body that's already correct — SaveAndReimport is slow,
                // and this runs over eight FBXs.
                if (bodyImporter.animationType == ModelImporterAnimationType.Generic &&
                    bodyImporter.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
                    bodyImporter.useFileScale &&
                    Mathf.Approximately(bodyImporter.globalScale, ImportScale) &&
                    !bodyImporter.addCollider)
                    continue;

                bodyImporter.animationType = ModelImporterAnimationType.Generic;
                // CreateFromThisModel stores an Avatar describing this exact
                // skeleton inside the FBX. Everything else in this tool hangs
                // off that Avatar. Generic keeps the rig's real hierarchy rather
                // than forcing it onto Unity's humanoid skeleton, which is
                // precisely why it works where Humanoid doesn't.
                bodyImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                // Scale Factor is pinned here, and to the SAME value on the clips
                // below, so no hand-tweak in the Rig/Model tab can desync the two
                // and stretch the character again. Display size is the prefab
                // root's job, not the importer's (see CharacterDisplayScale).
                bodyImporter.globalScale = ImportScale;
                bodyImporter.useFileScale = true;
                // The thrower is COSMETIC and must never be a physical obstacle:
                // it stands 0.8 m BEHIND the ball spawn, so a collider here would
                // block the backward-fumble gag by putting an invisible wall in
                // the ball's path. Off by default today, but one accidental tick
                // of "Generate Colliders" in the Model tab would add a full mesh
                // collider — so pin it rather than rely on the default. Same
                // reasoning as the capsule fallback in GreyboxSceneBuilder, which
                // explicitly destroys the collider Unity's primitives come with.
                bodyImporter.addCollider = false;
                bodyImporter.SaveAndReimport();
            }

            // ----- 1b. Wii-style see-through thrower -----
            // BEFORE the Avatar is loaded, because this reimports the character
            // FBX and a reimport invalidates sub-asset references taken earlier.
            ApplyCharacterTransparency();

            // The Avatar is a sub-asset of the FBX, so it has to be dug out of
            // the model's full asset list by type.
            Avatar avatar = AssetDatabase
                .LoadAllAssetsAtPath(CharacterModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (avatar == null)
            {
                Debug.LogError($"[CharacterSetup] {CharacterModelPath} produced no Avatar. " +
                               "Check the Rig tab for import errors.");
                return;
            }
            if (!avatar.isValid)
            {
                Debug.LogError($"[CharacterSetup] The Avatar on {CharacterModelPath} is INVALID — animations will not bind. " +
                               "Check the Rig tab for import errors.");
                return;
            }

            // ----- 2. Mixamo clips -> Generic, bound to that Avatar -----
            foreach ((string file, bool loop) in Clips)
            {
                string path = $"{AnimationsFolder}/{file}.fbx";
                ModelImporter clipImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                if (clipImporter == null)
                {
                    Debug.LogWarning($"[CharacterSetup] Missing animation {path} — skipping it.");
                    continue;
                }

                clipImporter.animationType = ModelImporterAnimationType.Generic;
                // CopyFromOther + sourceAvatar binds the clip to the character's
                // skeleton rather than to the duplicate one inside the clip's own
                // FBX, so the animated transform paths are guaranteed to resolve
                // against the model we actually render.
                clipImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                clipImporter.sourceAvatar = avatar;
                // The animation FBXs carry a duplicate skin we never render;
                // importing its materials would just litter the project.
                clipImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                // Same Scale Factor as the character, for the reason spelled out
                // on CharacterDisplayScale: mismatched units here distort the rig.
                clipImporter.globalScale = ImportScale;
                clipImporter.useFileScale = true;

                // Commit the RIG settings before reading the take list, in their
                // own reimport. defaultClipAnimations reports what the last
                // COMPLETED import found in the file, not what's pending on this
                // importer — so reading it first and then bailing out below would
                // strand every write above unsaved. Worse, an FBX that imported
                // with no takes (animationType None, importAnimation off, a stray
                // import Preset) could then never be repaired: every run would
                // read an empty list, warn, skip, and never save the setting that
                // would have fixed it. Costs one extra reimport per clip on a
                // one-click tool that runs rarely — correctness wins.
                clipImporter.SaveAndReimport();

                // A Mixamo download is NOT one animation. Each of these FBXs also
                // carries every take baked into the source skin it was retargeted
                // onto — twelve in total, with the Quaternius set (Man_Clapping,
                // Man_Death, Man_Idle, ...) FIRST and the motion we actually asked
                // for LAST. Importing them all under one shared name is what made
                // every single state in the controller play Man_Clapping.
                // clipAnimations starts empty, so seed it from defaultClipAnimations
                // (what the importer found in the file).
                ModelImporterClipAnimation[] takes =
                    clipImporter.defaultClipAnimations ?? new ModelImporterClipAnimation[0];
                string[] takeNames = System.Array.ConvertAll(takes, t => t?.takeName);
                int take = SelectTakeIndex(takeNames);
                if (take < 0)
                {
                    Debug.LogWarning($"[CharacterSetup] {path} has no '{MixamoTakeName}' take and more than one " +
                                     $"take to choose between, so '{file}' was left as-is rather than guessed at. " +
                                     $"Takes found: {string.Join(", ", takeNames)}");
                    continue;
                }

                takes[take].name = file;
                takes[take].loopTime = loop;
                // Exactly ONE clip out of this FBX, named exactly `file`, so
                // AddState below has nothing left to pick wrong.
                clipImporter.clipAnimations = new[] { takes[take] };
                clipImporter.SaveAndReimport();
            }

            // ----- 3. Animator Controller -----
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(ProjectRoot, "Prefabs");

            // Delete-then-create keeps re-runs idempotent instead of piling up
            // duplicate states on an existing controller.
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter(CharacterThrowReactionActor.SpeedFloat, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterThrowReactionActor.DrunkBool, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterThrowReactionActor.ExcitedTrigger, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterThrowReactionActor.DefeatTrigger, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterThrowReactionActor.FallFlatTrigger, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(sm, "Idle", "Idle", new Vector3(300f, 0f, 0f));
            AnimatorState walking = AddState(sm, "Walking", "Walking", new Vector3(300f, 100f, 0f));
            AnimatorState drunkIdle = AddState(sm, "DrunkIdle", "Drunk Idle", new Vector3(300f, -100f, 0f));
            AnimatorState excited = AddState(sm, "Excited", "Excited", new Vector3(600f, -60f, 0f));
            AnimatorState defeat = AddState(sm, "Defeat", "Defeat", new Vector3(600f, 40f, 0f));
            AnimatorState fallFlat = AddState(sm, "FallFlat", "Fall Flat", new Vector3(600f, 140f, 0f));

            sm.defaultState = idle;

            // Locomotion: nothing drives Speed yet, but the states are wired so
            // that when movement lands it's a one-line SetSpeed call.
            AddTransition(idle, walking, AnimatorConditionMode.Greater, 0.1f, CharacterThrowReactionActor.SpeedFloat);
            AddTransition(walking, idle, AnimatorConditionMode.Less, 0.1f, CharacterThrowReactionActor.SpeedFloat);

            // Drink meter hook. Reactions always return to Idle, and if Drunk is
            // still true this transition immediately carries it on to DrunkIdle —
            // so there's no need for a reaction->DrunkIdle path as well.
            AddTransition(idle, drunkIdle, AnimatorConditionMode.If, 0f, CharacterThrowReactionActor.DrunkBool);
            AddTransition(drunkIdle, idle, AnimatorConditionMode.IfNot, 0f, CharacterThrowReactionActor.DrunkBool);

            // Body English. AnyState so a reaction interrupts whatever's playing
            // the instant the throw resolves.
            AddReaction(sm, excited, CharacterThrowReactionActor.ExcitedTrigger, idle);
            AddReaction(sm, defeat, CharacterThrowReactionActor.DefeatTrigger, idle);
            AddReaction(sm, fallFlat, CharacterThrowReactionActor.FallFlatTrigger, idle);

            EditorUtility.SetDirty(controller);

            // ----- 4. PlayerCharacter prefab -----
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            GameObject root = new GameObject("PlayerCharacter");
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            modelInstance.transform.SetParent(root.transform, false);

            // The imported model root carries an Animator once the rig has an
            // avatar, but add one if the import didn't for any reason.
            Animator animator = modelInstance.GetComponent<Animator>();
            if (animator == null) animator = modelInstance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            // Our Mixamo clips are in-place, but root motion would still creep
            // the thrower off the foul line over a match. The scene owns the
            // character's position, not the animation.
            animator.applyRootMotion = false;

            // Reaction actor on the WRAPPER root, not the model: components
            // added to the model instance would be prefab overrides that a
            // future FBX re-import can disturb.
            root.AddComponent<CharacterThrowReactionActor>();

            // The ONE place the character's display size is set. Uniform, on the
            // wrapper root, so mesh and animated bone positions scale together
            // and limb proportions hold (see CharacterDisplayScale).
            root.transform.localScale = Vector3.one * CharacterDisplayScale;
            LogCharacterHeight(root);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerCharacterPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterSetup] Done. Model: {CharacterModelPath}\n" +
                      $"Controller: {ControllerPath}\nPrefab: {PlayerCharacterPrefabPath}\n" +
                      "Now run WeeSpurts -> Build Greybox Bowling Scene to put it in the alley.");
        }

        /// <summary>
        /// Makes the thrower see-through (GameBible/ArtGuide: Wii Sports keeps
        /// the thrower readable but lets you see the lane through them).
        ///
        /// WHY MATERIAL REMAPPING AND NOT A MATERIAL SET IN THE SCENE: an FBX's
        /// materials are sub-assets generated by the importer, so anything
        /// hand-assigned in the scene or overridden on the prefab is one
        /// reimport away from being silently reverted to opaque — and a
        /// reimport happens on any Rig/Model tab tweak or a fresh clone.
        /// AssetImporter's external-object map lives in the FBX's .meta file, so
        /// the redirection IS the import setting. It survives reimports by
        /// construction, which is the whole requirement.
        ///
        /// The remap points each of the FBX's built-in material slots at a
        /// transparent COPY of that material in Materials/, so the original
        /// colours/textures are preserved and only the blending changes.
        ///
        /// Idempotent, and re-applies alpha on every run so changing
        /// CharacterOpacity and re-running actually takes effect.
        ///
        /// SELF-REPAIRING BY DESIGN. It drives off the FBX's own material slots
        /// rather than off whatever the remap map currently says, because the
        /// map can be wrong: delete one Thrower_*.mat (or lose it in a merge)
        /// and that entry resolves to null. An earlier version keyed off the map
        /// and treated "any entry present" as done, so a broken or half-finished
        /// remap could never be repaired by re-running — the tool would report
        /// "no materials to make transparent" and give up, pointing at the wrong
        /// cause. Now a missing target is simply rebuilt.
        ///
        /// API note (CLAUDE.md rule 3 — these were verified against the docs and
        /// against UnityEditor.dll in this exact Unity version, not recalled):
        /// AssetImporter.AddRemap(SourceAssetIdentifier, Object) and
        /// GetExternalObjectMap() both exist. SourceAssetIdentifier is
        /// constructed here from the source OBJECT rather than from a
        /// (Type, name) pair on purpose — Unity's own docs give that second
        /// constructor's arguments in BOTH orders on different pages, and the
        /// object overload is unambiguous. ModelImporter.materialLocation is
        /// deliberately NOT touched: the remap map alone redirects the slots,
        /// so there's no reason to also move where materials are stored.
        /// </summary>
        private static void ApplyCharacterTransparency()
        {
            if (!(AssetImporter.GetAtPath(CharacterModelPath) is ModelImporter importer)) return;

            // The FBX's own slots are the source of truth. Once a slot is
            // remapped its material is no longer a sub-asset of the FBX, so the
            // embedded list alone can't see it — the existing map fills in the
            // rest. Union of the two = every slot this model has, on a first run
            // and on any later one.
            var slots = new System.Collections.Generic.Dictionary<string, Material>();
            // Identifiers kept as Unity handed them to us, never reconstructed:
            // SourceAssetIdentifier's (Type, string) constructor is documented
            // with its arguments in BOTH orders on different Unity doc pages, so
            // building one by hand is a coin flip. Round-tripping the key we were
            // given sidesteps the question entirely.
            var existingKeys = new System.Collections.Generic
                .Dictionary<string, AssetImporter.SourceAssetIdentifier>();

            foreach (Material embedded in AssetDatabase
                         .LoadAllAssetsAtPath(CharacterModelPath).OfType<Material>())
                slots[embedded.name] = embedded;

            // Existing remaps: key.name is the ORIGINAL slot name, which is what
            // we want even when the mapped value is one of our own materials (or
            // null, if someone deleted it — that's the repair case).
            foreach (var entry in importer.GetExternalObjectMap())
            {
                if (entry.Key.type != typeof(Material)) continue;
                existingKeys[entry.Key.name] = entry.Key;
                if (!slots.ContainsKey(entry.Key.name))
                    slots[entry.Key.name] = entry.Value as Material;
            }

            if (slots.Count == 0)
            {
                Debug.LogWarning($"[CharacterSetup] {CharacterModelPath} exposes no materials to make " +
                                 "transparent, so the thrower will render opaque. Check the Materials tab " +
                                 "— Material Creation Mode must not be 'None'.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder(ProjectRoot, "Materials");

            // The model name is in the filename because all eight Quaternius
            // bodies share the SAME six slot names (Eyes, Hair, Pants, Shirt,
            // Skin, Socks). Keyed on the slot name alone, swapping
            // CharacterModelPath to another body — which this tool's doc comment
            // explicitly invites — would silently reuse the previous body's
            // colours: a suit rendered in casual-wear colours, with nothing in
            // the console to say why.
            string modelName = System.IO.Path.GetFileNameWithoutExtension(CharacterModelPath);
            bool needsReimport = false;

            foreach (var slot in slots)
            {
                string path = $"{MaterialsFolder}/Thrower_{modelName}_{slot.Key}.mat";
                Material source = slot.Value;
                Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (copy == null)
                {
                    if (source == null)
                    {
                        // Remapped to a material that no longer exists AND the
                        // FBX no longer emits the original, so there is nothing
                        // left to copy from. Drop the remap so the next reimport
                        // regenerates the embedded material, then re-run.
                        Debug.LogWarning($"[CharacterSetup] Slot '{slot.Key}' was remapped to a material " +
                                         $"that no longer exists. Clearing the remap so the FBX rebuilds it — " +
                                         "run this menu item once more to make it transparent again.");
                        if (existingKeys.TryGetValue(slot.Key, out var orphaned))
                            importer.RemoveRemap(orphaned);
                        needsReimport = true;
                        continue;
                    }

                    // new Material(source) copies the shader and every property,
                    // so the character keeps its own look and only gains alpha.
                    copy = new Material(source);
                    AssetDatabase.CreateAsset(copy, path);
                    needsReimport = true;
                }
                else if (source != null && source != copy)
                {
                    // A file at our path that came from the FBX's CURRENT slot —
                    // re-stamp it, so re-running after changing the model (or
                    // after editing the FBX's materials) can't leave stale
                    // colours behind.
                    copy.CopyPropertiesFromMaterial(source);
                }

                MaterialTransparency.Apply(copy, CharacterOpacity);
                EditorUtility.SetDirty(copy);

                if (source != null && source != copy)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), copy);
                    needsReimport = true;
                }
            }

            AssetDatabase.SaveAssets();
            if (needsReimport) importer.SaveAndReimport();

            Debug.Log($"[CharacterSetup] Thrower is {CharacterOpacity:P0} opaque " +
                      $"({slots.Count} material slot(s) on {modelName}). " +
                      "Expect to see the lane through the character — and to see the character " +
                      "through ITSELF, which is what alpha blending does to a closed mesh.");
        }

        /// <summary>
        /// Which take inside an animation FBX is the one we actually downloaded.
        /// Mixamo always names the exported motion "mixamo.com"; anything else in
        /// the file tagged along from the source skin it was retargeted onto.
        ///
        /// Pure and string-only so it can be unit-tested without an FBX — this is
        /// the exact logic that broke, so it's worth a test rather than a comment.
        /// Returns -1 when the file is genuinely ambiguous, so the caller warns
        /// loudly instead of silently binding a random animation.
        /// </summary>
        public static int SelectTakeIndex(string[] takeNames)
        {
            if (takeNames == null) return -1;

            for (int i = 0; i < takeNames.Length; i++)
            {
                if (takeNames[i] != null &&
                    takeNames[i].Trim().Equals(MixamoTakeName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            // Not a Mixamo export (a hand-made or re-exported clip): a single take
            // is unambiguous, several are not.
            return takeNames.Length == 1 ? 0 : -1;
        }

        /// <summary>
        /// Prints how tall the character actually ends up, so CharacterDisplayScale
        /// stays a measured number rather than a guess. Bounds come from the bind
        /// pose (nothing has animated yet), which is close enough to judge "does
        /// this read as an adult standing next to the lane?".
        /// </summary>
        private static void LogCharacterHeight(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[CharacterSetup] No renderers on the character — can't measure its height.");
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float scaled = bounds.size.y;
            float raw = scaled / CharacterDisplayScale;
            // Deliberately no "…and the lane is N m wide" comparison here: that
            // number lives on LaneConfig and would go stale the moment it's
            // tuned. Adult human height doesn't.
            Debug.Log($"[CharacterSetup] Character height: {raw:0.00} m unscaled -> {scaled:0.00} m at " +
                      $"CharacterDisplayScale {CharacterDisplayScale}. An adult should read about 1.7-1.8 m — " +
                      "retune CharacterDisplayScale if that looks off.");
        }

        /// <summary>
        /// Adds one state and hangs the matching clip on it. The clip is pulled
        /// out of the FBX by type — Unity keeps AnimationClips as sub-assets of
        /// the model, alongside a "__preview__" copy we must skip.
        /// </summary>
        private static AnimatorState AddState(AnimatorStateMachine sm, string stateName, string clipFile, Vector3 position)
        {
            AnimatorState state = sm.AddState(stateName, position);
            AnimationClip clip = AssetDatabase
                .LoadAllAssetsAtPath($"{AnimationsFolder}/{clipFile}.fbx")
                .OfType<AnimationClip>()
                // Match by NAME, not "first one that isn't a preview". If an FBX
                // ever yields more than one clip again, first-wins binds the
                // wrong animation SILENTLY — that bug shipped once already, and
                // it's why the thrower stood there clapping.
                .FirstOrDefault(c => c.name == clipFile);

            if (clip == null)
                Debug.LogWarning($"[CharacterSetup] No clip found in {clipFile}.fbx — state '{stateName}' will be empty.");

            state.motion = clip;
            return state;
        }

        private static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string parameter)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            // Idle/walk/drunk swaps should happen the moment the parameter
            // changes, not at the end of the current loop.
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.2f;
            t.AddCondition(mode, threshold, parameter);
        }

        /// <summary>AnyState -> reaction on a trigger, then back to idle when the clip finishes.</summary>
        private static void AddReaction(AnimatorStateMachine sm, AnimatorState state, string trigger, AnimatorState idle)
        {
            AnimatorStateTransition into = sm.AddAnyStateTransition(state);
            into.hasExitTime = false;
            into.hasFixedDuration = true;
            into.duration = 0.1f;
            // Without this, the trigger would restart the reaction from frame 0
            // while it's already playing.
            into.canTransitionToSelf = false;
            into.AddCondition(AnimatorConditionMode.If, 0f, trigger);

            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            // 0.9 = start blending out near the end of the clip so the return to
            // idle reads as a settle rather than a snap.
            back.exitTime = 0.9f;
            back.hasFixedDuration = true;
            back.duration = 0.25f;
        }
    }
}
