using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK turns the raw rig + animation downloads into a usable player
    /// character. As of the Meshy mascot swap that's Meshy_AI_Bowling_Mascot_
    /// Rig_biped's 11 FBXs; the pipeline shape is unchanged from the original
    /// Quaternius + Mixamo one, just pointed at a different source:
    ///   1. Rig FBX (Idle_3) -> Generic rig, avatar built from the model, no
    ///                          embedded material generated at all (see the
    ///                          note on MascotMaterialPath for why).
    ///   1b. Its look -> a transparent copy of Meshy's own shipped material,
    ///                   applied to the renderer, so the thrower is see-through
    ///                   Wii Sports style (MascotConfig.Opacity).
    ///   2. The other 10 mascot FBXs -> Generic rig, avatar COPIED from the
    ///                        rig FBX so the clips bind to our exact skeleton,
    ///                        and ONE take imported per file (Meshy ships one
    ///                        take each, unlike Mixamo's twelve). Headache_
    ///                        Relief is cut into TWO named clips (DrunkIdle,
    ///                        Defeat) from that one take. Crawl_Backward is
    ///                        intentionally skipped (unused).
    ///   3. Builds PlayerCharacter.controller (states + parameters).
    ///   4. Builds PlayerCharacter.prefab (model + Animator + reaction actor),
    ///      sized once at the root by MascotConfig.DisplayScale.
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
    /// NOTE on the Meshy mascot (the "own character" this predicted): it is
    /// deliberately kept on Generic here too, on explicit instruction, WITHOUT
    /// re-testing whether its rig would actually validate as Humanoid — this
    /// session has no Unity instance to check that against. If Meshy's biped
    /// turns out to export a clean FK hierarchy, Humanoid may well work now;
    /// that is an open follow-up, not something ruled out by this comment.
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
        /// Meshy's mascot rig folder. All 11 FBXs it ships (the rig + 10
        /// animation takes) live directly inside it — unlike the old Mixamo
        /// pipeline there is no separate Animations/ folder, so every clip
        /// path in this file is built from this constant plus the file's
        /// exact name (see <see cref="MascotClipPath"/>).
        /// </summary>
        private const string MascotFolder = ProjectRoot + "/Characters/Meshy_AI_Bowling_Mascot_Rig_biped";

        /// <summary>
        /// Every Meshy FBX in <see cref="MascotFolder"/> shares this filename
        /// shape: Meshy_AI_Bowling_Mascot_Rig_biped_Animation_&lt;Name&gt;_withSkin.fbx.
        /// <see cref="MascotClipPath"/> turns just the &lt;Name&gt; token (e.g.
        /// "Walking") into a full asset path, so the Clips table below can
        /// stay readable instead of repeating this prefix/suffix eleven times.
        /// </summary>
        private const string MascotFilePrefix = "Meshy_AI_Bowling_Mascot_Rig_biped_Animation_";
        private const string MascotFileSuffix = "_withSkin.fbx";

        /// <summary>
        /// Meshy's own material — complete and correct (URP/Lit, base color
        /// texture wired to _BaseMap, verified by reading the .mat directly).
        /// USED DIRECTLY rather than trusting Unity's FBX importer to rebuild
        /// an equivalent one: importing Idle_3 the normal way generates an
        /// EMBEDDED material with no texture bound (confirmed by playtest —
        /// the mascot rendered flat white), because whatever texture
        /// reference Meshy baked into the FBX didn't resolve through Unity's
        /// own auto texture search. Rather than fight that importer behaviour,
        /// <see cref="BuildMascotThrowerMaterial"/> just uses this asset as
        /// the source of truth and skins the FBX import entirely (see
        /// materialImportMode = None on the rig in SetUp).
        /// </summary>
        private const string MascotMaterialPath =
            MascotFolder + "/Materials/Meshy_AI_Bowling_Mascot_Rig_biped_texture_0.mat";

        /// <summary>
        /// "The" player character — Tony's own Meshy-generated mascot now,
        /// rather than a Quaternius placeholder. This same FBX also supplies
        /// the "Idle" clip (see Clips below): Meshy's Idle_3 take ships baked
        /// into the rig file itself instead of a separate animation FBX, the
        /// way Mixamo's did. The old Quaternius bodies (Male_Casual,
        /// Male_Suit, their Smooth_ versions, etc.) are left in Characters/
        /// untouched as a fallback/reference — swap this constant back to one
        /// of those and re-run to revert.
        /// </summary>
        private const string CharacterModelPath = MascotFolder + "/" + MascotFilePrefix + "Idle_3" + MascotFileSuffix;

        /// <summary>
        /// Display scale and opacity used to live here as hard constants —
        /// moved to <see cref="MascotConfig"/> (a ScriptableObject) so Tony
        /// can retune either from the Inspector with a slider, without a code
        /// edit, and without the next "Set Up Player Character" run silently
        /// overwriting a manual tweak (CLAUDE.md: "config/tunables are
        /// ScriptableObjects, not hard-coded constants"). See
        /// <see cref="LoadOrCreateMascotConfig"/> for the create-once rule.
        /// MEASURED default for DisplayScale (2026-07-27): LogCharacterHeight
        /// reported 3.14 m unscaled, so 0.56 lands the mascot at 1.76 m —
        /// mid-way through the 1.7-1.8 m adult range it checks for.
        /// </summary>
        private const string MascotConfigPath = ProjectRoot + "/ScriptableObjects/MascotConfig.asset";

        /// <summary>
        /// Animator Speed threshold (see FirstPersonController.DriveWalkAnimation)
        /// above which Walking hands off to Sprint. Speed now reads 0..1 for a
        /// walk and up to RoamConfig.SprintMultiplier (1.8 by default) while
        /// sprinting — it no longer hard-clamps to 1 always — so this needs to
        /// sit meaningfully between the two: high enough that an ordinary walk
        /// never bounces into Sprint, low enough that a real sprint always
        /// clears it. Retune alongside RoamConfig.SprintMultiplier if that
        /// number changes a lot.
        /// </summary>
        private const float SprintSpeedThreshold = 1.4f;

        /// <summary>
        /// Scale Factor forced onto EVERY character and animation FBX. The value
        /// barely matters; that it is IDENTICAL across all of them is what
        /// matters (see MascotConfig.DisplayScale). 1 = "whatever the file
        /// says", which keeps the model and its clips in the same units.
        /// </summary>
        private const float ImportScale = 1f;

        /// <summary>
        /// Mixamo names the take you actually downloaded "mixamo.com". Every
        /// other take in the file is baggage — see <see cref="SelectTakeIndex"/>.
        /// </summary>
        private const string MixamoTakeName = "mixamo.com";

        private const string CharactersFolder = ProjectRoot + "/Characters";
        private const string MaterialsFolder = ProjectRoot + "/Materials";
        // Retired with the Mixamo pipeline: nothing in this file reads from
        // it any more (see MascotFolder/MascotClipPath below). Left defined,
        // not deleted, because the old Mixamo clip FBXs still physically live
        // here as a fallback alongside the old Quaternius bodies in
        // Characters/ — same "don't delete the old option" call as those.
        private const string AnimationsFolder = ProjectRoot + "/Animations";
        private const string PrefabFolder = ProjectRoot + "/Prefabs";
        private const string ControllerPath = PrefabFolder + "/PlayerCharacter.controller";
        public const string PlayerCharacterPrefabPath = PrefabFolder + "/PlayerCharacter.prefab";

        /// <summary>
        /// Which Meshy source FBX each output clip is carved from, its final
        /// clip name, and whether it loops. Unlike the old Mixamo table, the
        /// SOURCE FILE no longer doubles as the clip name — Meshy's files
        /// carry the mascot's full descriptive name — and one file
        /// (Headache_Relief) is deliberately cut into TWO differently-named,
        /// differently-looping clips from the SAME take (see the grouped loop
        /// in SetUp below). "Idle_3" is the rig FBX itself (step 1) as well as
        /// a clip source. Crawl_Backward has no entry here on purpose — Tony's
        /// call, it's unused, and its import is left untouched entirely.
        /// </summary>
        private static readonly (string SourceFile, string ClipName, bool Loop)[] Clips =
        {
            ("Idle_3",                           "Idle",             true),
            ("Walking",                          "Walking",          true),
            ("Running",                          "Running",          true),
            ("Funky_Walk",                       "DrunkWalk",        true),
            ("Headache_Relief",                  "DrunkIdle",        true),
            ("Headache_Relief",                  "Defeat",           false),
            ("happy_jump_m",                     "Excited",          false),
            ("Fall_Down",                        "FallFlat",         false),
            ("Female_Crouch_Pick_Throw_Forward", "Throw",            false),
            ("Agree_Gesture",                    "AgreeGesture",     false),
            ("Checkout_Gesture",                 "CheckoutGesture",  false),
        };

        /// <summary>Builds a full asset path from just a Meshy &lt;Name&gt; token (see MascotFilePrefix/Suffix doc above).</summary>
        private static string MascotClipPath(string sourceFile) => MascotFolder + "/" + MascotFilePrefix + sourceFile + MascotFileSuffix;

        [MenuItem("WeeSpurts/Set Up Player Character")]
        public static void SetUp()
        {
            // ----- 0. Tony's tunables (create-once, see LoadOrCreateMascotConfig) -----
            MascotConfig mascotConfig = LoadOrCreateMascotConfig();

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
            //
            // DEVIATION FROM THE OLD SHAPE: FindAssets("t:Model", CharactersFolder)
            // recurses into MascotFolder too, and now finds the 10 Meshy ANIMATION
            // clips sitting alongside the rig — not just alternate full bodies the
            // way the old Quaternius set was. Those 10 must end up CopyFromOther
            // (step 2 below), not CreateFromThisModel, so they're explicitly
            // skipped here rather than being set one way and then flipped the
            // other by step 2 (which would still net out correct, since step 2
            // runs after and reassigns everything it touches, but would silently
            // waste a reimport pass per clip and muddy what "fix every body" means).
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool isMascotClip = path != CharacterModelPath &&
                                     path.StartsWith(MascotFolder + "/", System.StringComparison.Ordinal);
                if (isMascotClip) continue;

                if (!(AssetImporter.GetAtPath(path) is ModelImporter bodyImporter)) continue;

                bool isMascotRig = path == CharacterModelPath;

                // Skip a body that's already correct — SaveAndReimport is slow,
                // and this runs over every FBX directly in Characters/.
                if (bodyImporter.animationType == ModelImporterAnimationType.Generic &&
                    bodyImporter.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
                    bodyImporter.useFileScale &&
                    Mathf.Approximately(bodyImporter.globalScale, ImportScale) &&
                    !bodyImporter.addCollider &&
                    (!isMascotRig || bodyImporter.materialImportMode == ModelImporterMaterialImportMode.None))
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
                // root's job, not the importer's (see MascotConfig.DisplayScale).
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

                if (isMascotRig)
                {
                    // Don't bother generating an embedded material for the rig
                    // at all — Unity's FBX importer produces one with no
                    // texture bound (its internal texture reference doesn't
                    // resolve through Unity's auto search), it would just be
                    // dead weight in the project, and BuildMascotThrowerMaterial
                    // below uses Meshy's own shipped material directly instead.
                    bodyImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                }

                bodyImporter.SaveAndReimport();
            }

            // ----- 1b. Thrower material: Wii-style see-through -----
            // BEFORE the Avatar is loaded, because this reimports the character
            // FBX and a reimport invalidates sub-asset references taken earlier.
            // Quaternius bodies get a transparent copy remapped onto their
            // embedded material slots (ApplyCharacterTransparency); the mascot
            // has no embedded material to remap (see materialImportMode = None
            // above) and gets a transparent copy of Meshy's own material
            // instead, applied directly to the renderer in step 4 below.
            Material mascotThrowerMaterial = null;
            if (CharacterModelPath.StartsWith(MascotFolder + "/", System.StringComparison.Ordinal))
                mascotThrowerMaterial = BuildMascotThrowerMaterial(mascotConfig.Opacity);
            else
                ApplyCharacterTransparency(mascotConfig.Opacity);

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

            // ----- 2. Meshy clips -> Generic, bound to that Avatar -----
            // Grouped by SOURCE FILE (not iterated row-by-row) because
            // Headache_Relief supplies TWO output clips (DrunkIdle, Defeat)
            // from the same take — GroupBy preserves both the first-seen file
            // order and each file's row order within its group, so this stays
            // deterministic. System.Linq is already imported by this file.
            foreach (var clipGroup in Clips.GroupBy(c => c.SourceFile))
            {
                string sourceFile = clipGroup.Key;
                string path = MascotClipPath(sourceFile);
                ModelImporter clipImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                if (clipImporter == null)
                {
                    Debug.LogWarning($"[CharacterSetup] Missing animation {path} — skipping it.");
                    continue;
                }

                // Idle_3 IS the rig FBX (CharacterModelPath) and already got
                // Generic + CreateFromThisModel + External materials from
                // step 1 above. Re-running CopyFromOther on it here would try
                // to copy the avatar FROM ITSELF, undoing the self-authored
                // avatar everything else in this tool hangs off — so only the
                // OTHER 9 files get the rig settings below.
                bool isRig = sourceFile == "Idle_3";
                if (!isRig)
                {
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
                    // on MascotConfig.DisplayScale: mismatched units here distort the rig.
                    clipImporter.globalScale = ImportScale;
                    clipImporter.useFileScale = true;

                    // Commit the RIG settings before reading the take list, in their
                    // own reimport. defaultClipAnimations reports what the last
                    // COMPLETED import found in the file, not what's pending on this
                    // importer — so reading it first and then bailing out below would
                    // strand every write above unsaved. Costs one extra reimport per
                    // clip on a one-click tool that runs rarely — correctness wins.
                    clipImporter.SaveAndReimport();
                }

                // Meshy files are expected to carry ONE take each (unlike
                // Mixamo's twelve) — SelectTakeIndex's takeNames.Length == 1
                // fallback branch is what actually picks it here.
                // clipAnimations starts empty, so seed it from
                // defaultClipAnimations (what the importer found in the file).
                ModelImporterClipAnimation[] takes =
                    clipImporter.defaultClipAnimations ?? new ModelImporterClipAnimation[0];
                string[] takeNames = System.Array.ConvertAll(takes, t => t?.takeName);
                int take = SelectTakeIndex(takeNames);
                if (take < 0)
                {
                    Debug.LogWarning($"[CharacterSetup] {path} has more than one take and none of them is " +
                                     $"unambiguous, so it was left as-is rather than guessed at. " +
                                     $"Takes found: {string.Join(", ", takeNames)}");
                    continue;
                }

                // One row per output clip (Headache_Relief has two: DrunkIdle
                // + Defeat). Each gets its OWN ModelImporterClipAnimation
                // instance — sharing one instance between two array slots
                // would mean renaming/re-looping the second entry silently
                // rewrites the first, since it's a class (reference type),
                // not a struct. firstFrame/lastFrame/takeName are copied from
                // the SAME resolved take rather than hand-guessed, per Tony's
                // instruction — only name and loopTime differ per output.
                ModelImporterClipAnimation resolvedTake = takes[take];
                var outputClips = new System.Collections.Generic.List<ModelImporterClipAnimation>();
                foreach (var row in clipGroup)
                {
                    outputClips.Add(new ModelImporterClipAnimation
                    {
                        name = row.ClipName,
                        takeName = resolvedTake.takeName,
                        firstFrame = resolvedTake.firstFrame,
                        lastFrame = resolvedTake.lastFrame,
                        loopTime = row.Loop,
                    });
                }

                clipImporter.clipAnimations = outputClips.ToArray();
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
            controller.AddParameter(CharacterThrowReactionActor.ThrowTrigger, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(sm, "Idle", MascotClipPath("Idle_3"), "Idle", new Vector3(300f, 0f, 0f));
            AnimatorState walking = AddState(sm, "Walking", MascotClipPath("Walking"), "Walking", new Vector3(300f, 100f, 0f));
            AnimatorState sprint = AddState(sm, "Sprint", MascotClipPath("Running"), "Running", new Vector3(300f, 200f, 0f));
            AnimatorState drunkIdle = AddState(sm, "DrunkIdle", MascotClipPath("Headache_Relief"), "DrunkIdle", new Vector3(300f, -100f, 0f));
            AnimatorState drunkWalk = AddState(sm, "DrunkWalk", MascotClipPath("Funky_Walk"), "DrunkWalk", new Vector3(300f, -200f, 0f));
            AnimatorState excited = AddState(sm, "Excited", MascotClipPath("happy_jump_m"), "Excited", new Vector3(600f, -60f, 0f));
            // Defeat's clip source changed: it now comes from Headache_Relief
            // (see Clips above) instead of the old Mixamo "Defeat" FBX — same
            // state name and trigger, new source file.
            AnimatorState defeat = AddState(sm, "Defeat", MascotClipPath("Headache_Relief"), "Defeat", new Vector3(600f, 40f, 0f));
            AnimatorState fallFlat = AddState(sm, "FallFlat", MascotClipPath("Fall_Down"), "FallFlat", new Vector3(600f, 140f, 0f));
            AnimatorState throwState = AddState(sm, "Throw", MascotClipPath("Female_Crouch_Pick_Throw_Forward"), "Throw", new Vector3(600f, 240f, 0f));

            sm.defaultState = idle;

            // Locomotion: nothing drives Speed yet, but the states are wired so
            // that when movement lands it's a one-line SetSpeed call.
            AddTransition(idle, walking, AnimatorConditionMode.Greater, 0.1f, CharacterThrowReactionActor.SpeedFloat);
            AddTransition(walking, idle, AnimatorConditionMode.Less, 0.1f, CharacterThrowReactionActor.SpeedFloat);

            // Sprint. FirstPersonController.DriveWalkAnimation no longer clamps
            // Speed to 1 while sprinting, so this is the first threshold that
            // can ever tell the two apart (see SprintSpeedThreshold doc).
            AddTransition(walking, sprint, AnimatorConditionMode.Greater, SprintSpeedThreshold, CharacterThrowReactionActor.SpeedFloat);
            AddTransition(sprint, walking, AnimatorConditionMode.Less, SprintSpeedThreshold, CharacterThrowReactionActor.SpeedFloat);

            // Drink meter hook. Reactions always return to Idle, and if Drunk is
            // still true this transition immediately carries it on to DrunkIdle —
            // so there's no need for a reaction->DrunkIdle path as well.
            AddTransition(idle, drunkIdle, AnimatorConditionMode.If, 0f, CharacterThrowReactionActor.DrunkBool);
            AddTransition(drunkIdle, idle, AnimatorConditionMode.IfNot, 0f, CharacterThrowReactionActor.DrunkBool);
            // Same hook, mirrored onto the walking pair.
            AddTransition(walking, drunkWalk, AnimatorConditionMode.If, 0f, CharacterThrowReactionActor.DrunkBool);
            AddTransition(drunkWalk, walking, AnimatorConditionMode.IfNot, 0f, CharacterThrowReactionActor.DrunkBool);

            // Drunk idle/walk swap on the same Speed threshold as sober
            // idle/walking, so the drink meter doesn't change what "moving"
            // means to the Animator.
            AddTransition(drunkIdle, drunkWalk, AnimatorConditionMode.Greater, 0.1f, CharacterThrowReactionActor.SpeedFloat);
            AddTransition(drunkWalk, drunkIdle, AnimatorConditionMode.Less, 0.1f, CharacterThrowReactionActor.SpeedFloat);

            // Body English. AnyState so a reaction interrupts whatever's playing
            // the instant the throw resolves.
            AddReaction(sm, excited, CharacterThrowReactionActor.ExcitedTrigger, idle);
            AddReaction(sm, defeat, CharacterThrowReactionActor.DefeatTrigger, idle);
            AddReaction(sm, fallFlat, CharacterThrowReactionActor.FallFlatTrigger, idle);
            // The throw motion itself. CharacterThrowReactionActor.PlayReaction
            // fires this FIRST and holds the outcome trigger (Excited/Defeat/
            // FallFlat) until the Throw state's clip length has elapsed — see
            // that file's PlayOutcomeAfterThrow for why.
            AddReaction(sm, throwState, CharacterThrowReactionActor.ThrowTrigger, idle);

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

            // Mascot thrower material, applied directly to every renderer the
            // model has (there's no FBX slot to remap into — see
            // BuildMascotThrowerMaterial). No-op for a Quaternius body, which
            // already got its transparent look via ApplyCharacterTransparency's
            // FBX-level remap above.
            if (mascotThrowerMaterial != null)
            {
                foreach (Renderer renderer in modelInstance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = mascotThrowerMaterial;
            }

            // Reaction actor on the WRAPPER root, not the model: components
            // added to the model instance would be prefab overrides that a
            // future FBX re-import can disturb.
            root.AddComponent<CharacterThrowReactionActor>();

            // The ONE place the character's display size is set. Uniform, on the
            // wrapper root, so mesh and animated bone positions scale together
            // and limb proportions hold (see MascotConfig.DisplayScale).
            root.transform.localScale = Vector3.one * mascotConfig.DisplayScale;
            LogCharacterHeight(root, mascotConfig.DisplayScale);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerCharacterPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterSetup] Done. Model: {CharacterModelPath}\n" +
                      $"Controller: {ControllerPath}\nPrefab: {PlayerCharacterPrefabPath}\n" +
                      "Now run WeeSpurts -> Build Greybox Bowling Scene to put it in the alley.");
        }

        /// <summary>
        /// Loads Assets/_Project/ScriptableObjects/MascotConfig.asset, or
        /// creates it (with MascotConfig's own field-initializer defaults) if
        /// this is the first run. CREATE-ONCE: an asset that already exists is
        /// returned exactly as Tony left it — never stomped — same rule as
        /// RoamConfig/InteractionConfig in RoamingSetupTool and Wobbler/Nuke
        /// in GreyboxSceneBuilder. This is what makes the Inspector slider on
        /// MascotConfig actually stick across re-runs.
        /// </summary>
        private static MascotConfig LoadOrCreateMascotConfig()
        {
            MascotConfig existing = AssetDatabase.LoadAssetAtPath<MascotConfig>(MascotConfigPath);
            if (existing != null) return existing;

            string folder = ProjectRoot + "/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(ProjectRoot, "ScriptableObjects");

            MascotConfig created = ScriptableObject.CreateInstance<MascotConfig>();
            AssetDatabase.CreateAsset(created, MascotConfigPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>
        /// The mascot's thrower material: a transparent COPY of Meshy's own
        /// shipped material (MascotMaterialPath) — same "copy it, don't mutate
        /// the original" shape as ApplyCharacterTransparency's Thrower_*.mat
        /// convention, just built from a known-good external asset instead of
        /// an FBX's embedded sub-assets, since the mascot's embedded material
        /// isn't used at all (materialImportMode = None on the rig — see
        /// MascotMaterialPath's doc comment for why).
        ///
        /// The caller (SetUp, step 4) assigns the result directly onto the
        /// instantiated model's Renderer — there is no FBX slot to remap here,
        /// so unlike Quaternius there's nothing for a stray reimport to
        /// silently revert; SetUp already rebuilds the whole prefab from
        /// scratch every run, which re-applies this too.
        /// </summary>
        private static Material BuildMascotThrowerMaterial(float opacity)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(MascotMaterialPath);
            if (source == null)
            {
                Debug.LogWarning($"[CharacterSetup] Mascot material not found at {MascotMaterialPath} — " +
                                 "the thrower will use whatever default material the model import produced.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder(ProjectRoot, "Materials");

            string path = $"{MaterialsFolder}/Thrower_Mascot.mat";
            Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                // new Material(source) copies the shader and every property —
                // texture, tint, everything Meshy set up — so the mascot keeps
                // its own colours/clothes/hair and only gains alpha below.
                copy = new Material(source);
                AssetDatabase.CreateAsset(copy, path);
            }
            else
            {
                // Re-stamp on every run, same reasoning as
                // ApplyCharacterTransparency: keeps this in sync if Meshy's
                // source material ever changes.
                copy.CopyPropertiesFromMaterial(source);
            }

            MaterialTransparency.Apply(copy, opacity);
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
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
        /// MascotConfig.Opacity and re-running actually takes effect.
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
        private static void ApplyCharacterTransparency(float opacity)
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

                MaterialTransparency.Apply(copy, opacity);
                EditorUtility.SetDirty(copy);

                if (source != null && source != copy)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), copy);
                    needsReimport = true;
                }
            }

            AssetDatabase.SaveAssets();
            if (needsReimport) importer.SaveAndReimport();

            Debug.Log($"[CharacterSetup] Thrower is {opacity:P0} opaque " +
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
        /// Prints how tall the character actually ends up, so MascotConfig.
        /// DisplayScale stays a measured number rather than a guess. Bounds
        /// come from the bind pose (nothing has animated yet), which is close
        /// enough to judge "does this read as an adult standing next to the
        /// lane?".
        /// </summary>
        private static void LogCharacterHeight(GameObject root, float displayScale)
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
            float raw = scaled / displayScale;
            // Deliberately no "…and the lane is N m wide" comparison here: that
            // number lives on LaneConfig and would go stale the moment it's
            // tuned. Adult human height doesn't.
            Debug.Log($"[CharacterSetup] Character height: {raw:0.00} m unscaled -> {scaled:0.00} m at " +
                      $"DisplayScale {displayScale}. An adult should read about 1.7-1.8 m — " +
                      "retune MascotConfig.asset's Display Scale in the Inspector if that looks off.");
        }

        /// <summary>
        /// Adds one state and hangs the matching clip on it. The clip is pulled
        /// out of the FBX by type — Unity keeps AnimationClips as sub-assets of
        /// the model, alongside a "__preview__" copy we must skip.
        ///
        /// clipPath and clipName are now SEPARATE parameters (the old version
        /// took one "clipFile" string and used it as both, because the Mixamo
        /// pipeline's FBX filenames always matched the clip name it assigned
        /// them). That's no longer true under Meshy: DrunkWalk's clip lives in
        /// Funky_Walk_withSkin.fbx, and both DrunkIdle and Defeat live in
        /// Headache_Relief_withSkin.fbx — so the FILE to load from and the
        /// NAME to match inside it can now legitimately differ.
        /// </summary>
        private static AnimatorState AddState(AnimatorStateMachine sm, string stateName, string clipPath, string clipName, Vector3 position)
        {
            AnimatorState state = sm.AddState(stateName, position);
            AnimationClip clip = AssetDatabase
                .LoadAllAssetsAtPath(clipPath)
                .OfType<AnimationClip>()
                // Match by NAME, not "first one that isn't a preview". If an FBX
                // ever yields more than one clip again, first-wins binds the
                // wrong animation SILENTLY — that bug shipped once already, and
                // it's why the thrower stood there clapping.
                .FirstOrDefault(c => c.name == clipName);

            if (clip == null)
                Debug.LogWarning($"[CharacterSetup] No clip named '{clipName}' found in {clipPath} — state '{stateName}' will be empty.");

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
