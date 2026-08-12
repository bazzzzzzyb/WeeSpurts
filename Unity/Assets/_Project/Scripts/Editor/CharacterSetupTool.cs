using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WeeSpurts.Bowling;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK turns the raw rig + animation downloads into a usable player
    /// character. The RIG and the CLIPS now come from different files — the
    /// T-posed Meshy_AI_Character_output.fbx is the body, and the older
    /// Meshy_AI_Bowling_Mascot_Rig_biped folder's 11 FBXs are kept purely as
    /// the clip source (see CharacterModelPath for why the swap happened, and
    /// why the old one is still on disk):
    ///   1. Rig FBX (CharacterModelPath) -> HUMANOID rig, avatar built from the
    ///                          model, optimizeGameObjects OFF, materials per
    ///                          RigShipsItsOwnMaterialAsset.
    ///   1b. Its look -> a transparent copy of its material, so the thrower is
    ///                   see-through Wii Sports style (MascotConfig.Opacity).
    ///   1c. Reports which human bones Unity's auto-mapper actually resolved,
    ///                   and STOPS if a REQUIRED one is missing — see
    ///                   ReportHumanoidMapping for why this is a hard stop.
    ///   2. All 11 mascot FBXs -> Humanoid, EACH BUILDING ITS OWN AVATAR (not
    ///                        copied from the rig — see the "CRITICAL" note
    ///                        below for why that changed this session), and
    ///                        ONE take imported per file (Meshy ships one take
    ///                        each, unlike Mixamo's twelve). Idle_3 is one of
    ///                        these now rather than being the rig itself — see
    ///                        the isRig note in step 2. Headache_
    ///                        Relief is cut into TWO named clips (DrunkIdle,
    ///                        Defeat) from that one take. Crawl_Backward is
    ///                        intentionally skipped (unused).
    ///   2c. Every FBX in Animations/Mixamo/ -> Humanoid, avatar built from
    ///                        ITS OWN model (NOT copied — see below), in-place
    ///                        root motion, looping if it reads as locomotion.
    ///   3. Builds PlayerCharacter.controller (states + parameters).
    ///   4. Builds PlayerCharacter.prefab (model + Animator + reaction actor),
    ///      sized once at the root by MascotConfig.DisplayScale.
    ///
    /// Menu: WeeSpurts -> 1 Assets -> Set Up Player Character
    ///
    /// WHY HUMANOID NOW (2026-08-11 — this file used to argue the opposite):
    /// the old comment here explained why the QUATERNIUS bodies cannot be
    /// Humanoid, and it is still correct about them. They export a Blender IK
    /// CONTROL rig: Foot.L/Foot.R are parented to the root bone as IK targets
    /// — siblings of the leg chain, not children of LowerLeg. Unity's Humanoid
    /// validates the HIERARCHY, not just bone names, so the import fails with
    /// "Required human bone 'LeftFoot' not found". That is why every body in
    /// Characters/ EXCEPT the mascot is still pinned to Generic below.
    ///
    /// The MESHY mascot is a different rig and the old comment explicitly left
    /// it as an open question. It is now answered: its bones are
    /// Hips / Spine / Spine01 / Spine02 / LeftShoulder / LeftArm / LeftForeArm
    /// / LeftHand / LeftUpLeg / LeftLeg / LeftFoot / LeftToeBase / neck / Head
    /// — clean Mixamo-convention FK naming with no IK targets. It has no
    /// finger bones, which is fine: Unity treats fingers as OPTIONAL.
    ///
    /// What Humanoid buys us, and the reason for the whole change: Mixamo
    /// clips retarget onto him. Humanoid stores a clip in a NORMALIZED muscle
    /// space rather than as raw bone transforms, so a clip authored on any
    /// humanoid skeleton plays on any other. That is what lets us use stock
    /// animation instead of being limited to the ten takes Meshy generated.
    ///
    /// CRITICAL, AND THE EASIEST THING TO GET WRONG HERE — every clip file,
    /// Meshy or Mixamo, gets CreateFromThisModel: its OWN avatar, from its OWN
    /// skeleton. Retargeting alone is what carries the motion onto the rig.
    ///
    /// THIS REVERSES WHAT THIS COMMENT SAID EARLIER THIS SESSION, and the
    /// reversal is worth understanding rather than just trusting: "same
    /// skeleton?" is NOT the same question as "same bone NAMES?". The ten old-
    /// mascot clip FBXs share bone names with the rig, character for
    /// character — but the RIG is now the T-posed regeneration (arms straight
    /// out) and the CLIPS are the OLD arms-down export, so their actual bind-
    /// pose proportions differ a lot (confirmed live: up to 317mm of position
    /// error on Spine02 alone). CopyFromOther doesn't just match bone names —
    /// it also expects the target file's bone POSITIONS to roughly match the
    /// copied avatar's skeleton, and logs "Rig Configuration mis-match" per
    /// bone when they don't. CopyFromOther was correct back when the rig WAS
    /// one of these same eleven files (same export, same bind pose, only the
    /// clip differed) — it stopped being correct the moment the rig became a
    /// separate regeneration. CreateFromThisModel sidesteps the whole
    /// question: retargeting normalises into muscle space specifically so a
    /// clip carries across DIFFERING proportions, which is exactly this case.
    /// MIXAMO clips ship their own skeleton entirely (`mixamorig:Hips`, ...)
    /// and always needed CreateFromThisModel for the same underlying reason —
    /// they were just never at risk of looking like a CopyFromOther candidate
    /// in the first place. Both silent-failure modes are worth naming: a bone-
    /// NAME mismatch under CopyFromOther binds nothing and T-poses with no
    /// import error; a bone-POSITION mismatch (this one) at least logs a Rig
    /// Error, but easy to miss in a wall of console output on a one-click tool.
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
        /// Meshy's newer "Mascot Base Rig" — a skinned biped (48 bones,
        /// Mixamo-convention names, no fingers/neck, both of which Unity treats
        /// as OPTIONAL humanoid bones) that ships its colour map as a LOOSE
        /// PNG beside the FBX.
        ///
        /// That last detail is the whole reason it's the body now. The previous
        /// candidate (Meshy_AI_Character, 2026-08-11) kept its only texture
        /// EMBEDDED in the FBX, where it never landed on disk — so every
        /// material route produced an empty _BaseMap and he rendered as a flat
        /// white shell. A loose PNG is bindable, which is what
        /// <see cref="BuildLooseTextureThrowerMaterial"/> does.
        /// </summary>
        private const string BaseRigFolder = ProjectRoot + "/Characters/Meshy_AI_Mascot_Base_Rig_biped";

        private const string BaseRigModelPath =
            BaseRigFolder + "/Meshy_AI_Mascot_Base_Rig_biped_Animation_Walking_withSkin.fbx";

        /// <summary>
        /// The base rig's colour map. The _metallic and _roughness maps beside
        /// it are DELIBERATELY unused: the art direction is flat stylised, not
        /// PBR (Docs/ArtGuide.md), the same call already made for the props.
        /// They're left on disk rather than deleted — Tony's standing
        /// instruction is to confirm before removing supplied source art.
        /// </summary>
        private const string BaseRigBaseColorPath =
            BaseRigFolder + "/Meshy_AI_Mascot_Base_Rig_biped_texture_0.png";

        /// <summary>
        /// The body the game actually uses. Swap this between
        /// <see cref="BaseRigModelPath"/>, <see cref="OldMascotModelPath"/>, or
        /// a Quaternius body and re-run — everything downstream reads it.
        /// </summary>
        private const string CharacterModelPath = BaseRigModelPath;

        /// <summary>
        /// The previous mascot's rig FBX. No longer the character — but still
        /// the "Idle_3" CLIP source (see Clips), so it is a normal animation
        /// file now rather than the rig. Kept as the one-line revert target
        /// for <see cref="CharacterModelPath"/>.
        /// </summary>
        private const string OldMascotModelPath = MascotFolder + "/" + MascotFilePrefix + "Idle_3" + MascotFileSuffix;

        /// <summary>
        /// Whether the rig gets its look from a material ASSET shipped
        /// alongside it (the old Meshy mascot) or from a material the FBX
        /// importer builds itself (everything else).
        ///
        /// THIS IS THE ONE PLACE THE TWO MASCOTS GENUINELY DIFFER, and getting
        /// it wrong renders the character flat white. The OLD mascot's FBX
        /// references its texture by a path Unity's auto-search cannot resolve,
        /// so its embedded material imports untextured and the tool bypasses
        /// the importer entirely (materialImportMode = None +
        /// <see cref="BuildMascotThrowerMaterial"/> off <see cref="MascotMaterialPath"/>).
        /// The NEW mascot EMBEDS its texture in the FBX — a 13 MB PNG in a
        /// Video/Content node, which is essentially the whole file size — and
        /// Unity extracts embedded media into a .fbm folder and binds it on
        /// import without help. So None would now be throwing away the only
        /// copy of the texture there is. It gets ImportStandard and the normal
        /// <see cref="ApplyCharacterTransparency"/> remap path instead, which
        /// is the same path the Quaternius bodies have always used.
        /// </summary>
        private static bool RigShipsItsOwnMaterialAsset =>
            CharacterModelPath.StartsWith(MascotFolder + "/", System.StringComparison.Ordinal);

        /// <summary>
        /// True when the rig ships its colour map as a loose texture FILE next
        /// to the FBX (the Mascot Base Rig). Those get a material built from
        /// that texture — see <see cref="BuildLooseTextureThrowerMaterial"/>.
        /// </summary>
        private static bool RigShipsLooseTextures =>
            CharacterModelPath.StartsWith(BaseRigFolder + "/", System.StringComparison.Ordinal);

        /// <summary>
        /// True when THIS TOOL supplies the thrower's material rather than
        /// letting the FBX importer generate one. Both such rigs get
        /// materialImportMode = None, because in both cases the importer's own
        /// material is the thing that renders untextured.
        /// </summary>
        private static bool ToolSuppliesThrowerMaterial =>
            RigShipsItsOwnMaterialAsset || RigShipsLooseTextures;

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
        ///
        /// That 0.56 is the FIELD DEFAULT in MascotConfig.cs, not necessarily
        /// what is on disk. Because of the create-once rule below, the shipped
        /// asset currently reads 0.7 (Tony's own tweak), which puts him at
        /// roughly 2.2 m — deliberate or not, the tool does not touch it. See
        /// LogCharacterHeight, which now prints the scale that WOULD hit 1.75 m
        /// rather than applying it.
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
        /// <summary>
        /// The OLD Quaternius-era clip folder. Nothing in this file reads
        /// from it, and nothing should: the six FBXs sitting directly in it
        /// (Idle, Walking, Defeat, Drunk Idle, Excited, Fall Flat) are Mixamo
        /// downloads retargeted onto the QUATERNIUS skeleton — Foot.L,
        /// UpperArm.L, Shoulder.L, MiddleHand.L. That is the IK-control rig
        /// described in the class doc, so they will not import as Humanoid
        /// AND they are a different skeleton from the mascot besides. Left on
        /// disk, not deleted, same "don't delete the old option" call as the
        /// Quaternius bodies in Characters/.
        /// </summary>
        private const string AnimationsFolder = ProjectRoot + "/Animations";

        /// <summary>
        /// Where Tony's Mixamo downloads land. Scanned as a FOLDER rather
        /// than listed file-by-file, because which clips exist changes every
        /// time he grabs another one — the import settings below are correct
        /// for any Mixamo FBX, so there is nothing to hand-maintain here.
        /// Missing folder is not an error: it just means none have arrived
        /// yet (see ImportMixamoClips).
        /// </summary>
        private const string MixamoFolder = AnimationsFolder + "/Mixamo";
        /// <summary>
        /// The one shared attachment catalog, assigned onto the character's
        /// <see cref="WeeSpurts.Characters.AttachmentSlots"/> at prefab-build
        /// time. Built by PropSetupTool / AttachmentSetupTool, not by this file
        /// — so it may legitimately not exist yet on a fresh clone (handled
        /// with a warning rather than a hard stop, since the character is
        /// perfectly usable without any cosmetics on it).
        /// </summary>
        private const string AttachmentCatalogPath = ProjectRoot + "/ScriptableObjects/AttachmentCatalog.asset";

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
        ///
        /// STILL HERE, NO LONGER USED FOR DrunkIdle/Defeat AT RUNTIME
        /// (2026-08-11, later): the two rows below remain the SAME
        /// Headache_Relief take, on purpose — they are the fallback
        /// AddStatePreferMixamo uses when Tony's Mixamo "drunk idle"/"Defeat"
        /// downloads AREN'T on disk (a fresh clone, or before he's grabbed
        /// them). Once those two files exist in Animations/Mixamo/ — they do,
        /// as of this session — SetUp's state-building step prefers them and
        /// this table's Headache_Relief rows go unused. Left in rather than
        /// deleted so the controller still builds something sane without the
        /// Mixamo folder at all.
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

        /// <summary>
        /// Builds a full asset path from a Mixamo download's filename (no
        /// extension). ImportMixamoClips names the imported clip after the
        /// FILE (see its own doc comment), so the clip inside always shares
        /// this same name — unlike MascotClipPath there is no separate
        /// prefix/suffix to strip, Tony's own filename IS the path fragment.
        /// </summary>
        private static string MixamoClipPath(string fileName) => MixamoFolder + "/" + fileName + ".fbx";

        [MenuItem("WeeSpurts/1 Assets/Set Up Player Character")]
        public static void SetUp()
        {
            // ----- 0. Tony's tunables (create-once, see LoadOrCreateMascotConfig) -----
            MascotConfig mascotConfig = LoadOrCreateMascotConfig();

            // ----- 1. Character FBX -> Humanoid (Quaternius bodies stay Generic; see class doc) -----
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

                // See RigShipsItsOwnMaterialAsset — None on the old mascot
                // (its embedded material imports untextured, so the tool uses
                // Meshy's shipped .mat instead), ImportStandard on the new one
                // (its texture is embedded IN the FBX, so None would throw away
                // the only copy of it and render him flat white).
                ModelImporterMaterialImportMode wantedRigMaterials = ToolSuppliesThrowerMaterial
                    ? ModelImporterMaterialImportMode.None
                    : ModelImporterMaterialImportMode.ImportStandard;

                // THE ONE LINE THIS WHOLE SESSION IS ABOUT. Only the mascot
                // becomes Humanoid; every other body in Characters/ stays
                // Generic because their IK-control rig genuinely cannot
                // validate as Humanoid (class doc). Leaving them on Humanoid
                // would re-log "Required human bone 'LeftFoot' not found" on
                // every reimport and bury real errors in the console — which
                // is the same reason the old code forced them all to Generic.
                ModelImporterAnimationType wantedType = isMascotRig
                    ? ModelImporterAnimationType.Human
                    : ModelImporterAnimationType.Generic;

                // Skip a body that's already correct — SaveAndReimport is slow,
                // and this runs over every FBX directly in Characters/.
                if (bodyImporter.animationType == wantedType &&
                    bodyImporter.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
                    bodyImporter.useFileScale &&
                    Mathf.Approximately(bodyImporter.globalScale, ImportScale) &&
                    !bodyImporter.addCollider &&
                    !bodyImporter.optimizeGameObjects &&
                    (!isMascotRig || bodyImporter.materialImportMode == wantedRigMaterials))
                    continue;

                bodyImporter.animationType = wantedType;
                // CreateFromThisModel stores an Avatar describing this exact
                // skeleton inside the FBX. Everything else in this tool hangs
                // off that Avatar. On the mascot it is now a HUMAN avatar, so
                // Unity also runs its automatic bone mapper here — which is the
                // step that can quietly half-succeed, hence ReportHumanoidMapping
                // immediately after this loop.
                bodyImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                // Belt and braces for the mapping: ask Unity to auto-map when
                // nothing else specifies the setup. Harmless alongside
                // CreateFromThisModel, and it means a future avatarSetup change
                // can't silently leave the rig unmapped.
                bodyImporter.autoGenerateAvatarMappingIfUnspecified = true;
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
                // "Optimize Game Objects" strips the bone Transforms out of the
                // imported hierarchy and drives the skin from the Animator
                // instead. It is a real runtime win and it is also exactly what
                // would break the next two things on the roadmap: the bone
                // report below needs GetBoneTransform to return something, and
                // Block 6 needs a real hand bone to parent a carried item to.
                // Off by default today — pinned so a stray Inspector tick can't
                // remove the bones and leave both looking like code bugs.
                bodyImporter.optimizeGameObjects = false;

                if (isMascotRig)
                    bodyImporter.materialImportMode = wantedRigMaterials;

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
            if (RigShipsItsOwnMaterialAsset)
                mascotThrowerMaterial = BuildMascotThrowerMaterial(mascotConfig.Opacity);
            else if (RigShipsLooseTextures)
                mascotThrowerMaterial = BuildLooseTextureThrowerMaterial(mascotConfig.Opacity);
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

            // ----- 1c. Did the humanoid auto-mapping actually work? -----
            // A HARD STOP, not a warning. An avatar that is valid-but-not-human,
            // or human-but-missing-a-required-bone, still imports without an
            // error and still produces a prefab — it just plays every retargeted
            // clip as a T-pose. Continuing past this point would hand Tony a
            // broken character and no console message pointing at the cause,
            // which is the single failure mode this whole conversion has to
            // avoid. Fixing it is a manual pass in Rig > Configure, so the tool
            // stops and names the bones rather than guessing at a mapping.
            if (!avatar.isHuman)
            {
                Debug.LogError($"[CharacterSetup] The Avatar on {CharacterModelPath} is valid but is NOT a HUMAN " +
                               "avatar, so no Mixamo clip will retarget onto it. Open the FBX > Rig tab and " +
                               "confirm Animation Type is set to Humanoid.");
                return;
            }
            if (!ReportHumanoidMapping(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath), avatar))
                return;

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

                // Skip the rig settings for a file that IS the rig: it already
                // got CreateFromThisModel in step 1, and CopyFromOther here
                // would try to copy the avatar FROM ITSELF, undoing the
                // self-authored avatar everything else in this tool hangs off.
                //
                // COMPARES PATHS, not the source-file token. This used to read
                // `sourceFile == "Idle_3"`, which was true only while the OLD
                // mascot's Idle_3 FBX was also CharacterModelPath. Now that the
                // rig is the separate T-posed model, Idle_3 is an ordinary clip
                // file — and the hardcoded token would have wrongly exempted it
                // from the settings every other clip gets, leaving it on
                // CreateFromThisModel with its own avatar. The failure mode is
                // the silent one this file keeps warning about: a clip bound to
                // the wrong skeleton logs nothing and plays as a T-pose.
                // Deriving it from the path means swapping CharacterModelPath
                // to ANY of these files stays correct with no second edit.
                bool isRig = path == CharacterModelPath;
                if (!isRig)
                {
                    clipImporter.animationType = ModelImporterAnimationType.Human;
                    // CreateFromThisModel, NOT CopyFromOther — REVERSED FROM THE
                    // PREVIOUS SESSION'S REASONING, and worth spelling out why.
                    // These ten FBXs' bone NAMES match the rig's exactly, which
                    // is what made CopyFromOther look correct — but CopyFromOther
                    // isn't a bone-name check, it's a bone-name check PLUS a
                    // proportions check: Unity compares each bone's actual
                    // position in THIS file against the copied avatar's skeleton
                    // and logs "Rig Configuration mis-match" (confirmed live —
                    // 21 bones, up to 317mm off on Spine02) when they don't
                    // roughly agree. That was fine while the rig and these clips
                    // were the SAME Meshy export (identical bind pose). It is no
                    // longer true: the rig is now the T-posed regeneration (arms
                    // straight out) and these clips are the OLD arms-down export
                    // — same names, same hierarchy shape, very different bind-
                    // pose proportions. CreateFromThisModel builds each clip's
                    // OWN avatar from ITS OWN skeleton instead (identical
                    // treatment to the Mixamo clips below), and Humanoid
                    // retargeting — which normalises into muscle space
                    // specifically so animation carries across DIFFERING
                    // proportions — is what actually gets the motion onto the
                    // new rig, not a shared avatar reference. See the class doc's
                    // "CRITICAL, AND THE EASIEST THING TO GET WRONG HERE" note:
                    // this is the same trap in a new shape, not a new trap.
                    clipImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    clipImporter.autoGenerateAvatarMappingIfUnspecified = true;
                    clipImporter.optimizeGameObjects = false;
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
                    var output = new ModelImporterClipAnimation
                    {
                        name = row.ClipName,
                        takeName = resolvedTake.takeName,
                        firstFrame = resolvedTake.firstFrame,
                        lastFrame = resolvedTake.lastFrame,
                        loopTime = row.Loop,
                    };
                    // NOT a behaviour change smuggled in with the conversion —
                    // it is what KEEPS the behaviour. Root Transform settings are
                    // Humanoid-only and were simply inert while these clips were
                    // Generic. Now that they are Human, leaving them at their
                    // defaults would start extracting root motion that
                    // applyRootMotion = false then throws away, which reads as
                    // foot-sliding. See ApplyInPlaceRootMotion.
                    ApplyInPlaceRootMotion(output);
                    outputClips.Add(output);
                }

                clipImporter.clipAnimations = outputClips.ToArray();
                clipImporter.SaveAndReimport();
            }

            // ----- 2c. Mixamo clips -> Humanoid, each on its OWN avatar -----
            ImportMixamoClips();

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

            // Every locomotion/expression state now PREFERS the matching Mixamo
            // download when Tony has actually grabbed it, and falls back to the
            // Meshy take otherwise — see AddStatePreferMixamo. This is what
            // finally splits DrunkIdle and Defeat (both used to be carved from
            // the SAME Headache_Relief take, see the Clips table comment) now
            // that real "drunk idle" and "Defeat" clips exist. Sprint, FallFlat
            // and Throw have no Mixamo replacement in today's download batch
            // (no forward run, no fall, nothing that reads as a bowling
            // delivery) so they keep their Meshy clip unconditionally.
            // "Breathing Idle" was one of the downloads that turned out to
            // import with zero usable clips (see MixamoClipExists' doc
            // comment) — "Neutral Idle" is Tony's replacement grab, not a
            // renamed version of the same file.
            AnimatorState idle = AddStatePreferMixamo(sm, "Idle", "Neutral Idle", "Idle_3", "Idle", new Vector3(300f, 0f, 0f));
            AnimatorState walking = AddStatePreferMixamo(sm, "Walking", "Walking", "Walking", "Walking", new Vector3(300f, 100f, 0f));
            AnimatorState sprint = AddStatePreferMixamo(sm, "Sprint", null, "Running", "Running", new Vector3(300f, 200f, 0f));
            AnimatorState drunkIdle = AddStatePreferMixamo(sm, "DrunkIdle", "drunk idle", "Headache_Relief", "DrunkIdle", new Vector3(300f, -100f, 0f));
            AnimatorState drunkWalk = AddStatePreferMixamo(sm, "DrunkWalk", "drunk walk", "Funky_Walk", "DrunkWalk", new Vector3(300f, -200f, 0f));
            AnimatorState excited = AddStatePreferMixamo(sm, "Excited", "Excited", "happy_jump_m", "Excited", new Vector3(600f, -60f, 0f));
            // Defeat's clip source changed: it now PREFERS Mixamo's own
            // dedicated "Defeat" download — the first time this state has ever
            // been a different motion from DrunkIdle. Falls back to the old
            // Headache_Relief split only if that download is missing.
            AnimatorState defeat = AddStatePreferMixamo(sm, "Defeat", "Defeat", "Headache_Relief", "Defeat", new Vector3(600f, 40f, 0f));
            AnimatorState fallFlat = AddStatePreferMixamo(sm, "FallFlat", null, "Fall_Down", "FallFlat", new Vector3(600f, 140f, 0f));
            AnimatorState throwState = AddStatePreferMixamo(sm, "Throw", null, "Female_Crouch_Pick_Throw_Forward", "Throw", new Vector3(600f, 240f, 0f));

            // SitIdle / Stumble are MIXAMO-ONLY — Meshy shipped neither a
            // seated pose nor a stumble, so unlike every state above there is
            // no Meshy clip to fall back to. TryAddMixamoOnlyState returns null
            // (adds nothing) rather than a state bound to nothing when the
            // download isn't on disk, so a clone without these two files still
            // builds a valid controller. Neither has a transition into it yet
            // — that's Block 4's job (Docs/Prompts/2026-08-11-executive-day-
            // plan.md), once a Seat component and a ControlMode.Seated hook
            // exist to actually drive them. They're built now purely so the
            // clip and the state exist and are ready to be wired up then.
            AnimatorState sitIdle = TryAddMixamoOnlyState(sm, "SitIdle", "Sitting Idle", new Vector3(900f, -160f, 0f));
            AnimatorState stumble = TryAddMixamoOnlyState(sm, "Stumble", "Stumble Backwards", new Vector3(900f, -60f, 0f));
            // StandToSit/SitToStand landed 2026-08-12 — Tony's second Mixamo
            // batch included BOTH transition directions ("Stand To Sit(1)",
            // "Sit To Stand(1)"), closing the gap the 2026-08-11 session left
            // open (Meshy had a seated idle to point SitIdle at but no
            // transition clip at all). SitToStand isn't one of the states the
            // day plan named, but it's the natural complement to StandToSit
            // and the clip was already sitting there unused, so it's wired up
            // too rather than left on the shelf. Still no transitions INTO
            // any of these four — that's still Block 4's job, once a Seat
            // component and ControlMode.Seated exist to actually drive them.
            AnimatorState standToSit = TryAddMixamoOnlyState(sm, "StandToSit", "Stand To Sit(1)", new Vector3(900f, -260f, 0f));
            AnimatorState sitToStand = TryAddMixamoOnlyState(sm, "SitToStand", "Sit To Stand(1)", new Vector3(900f, -360f, 0f));

            if (sitIdle == null)
                Debug.LogWarning($"[CharacterSetup] SitIdle skipped — no '{MixamoClipPath("Sitting Idle")}' found.");
            if (stumble == null)
                Debug.LogWarning($"[CharacterSetup] Stumble skipped — no '{MixamoClipPath("Stumble Backwards")}' found.");
            if (standToSit == null)
                Debug.LogWarning($"[CharacterSetup] StandToSit skipped — no '{MixamoClipPath("Stand To Sit(1)")}' found.");
            if (sitToStand == null)
                Debug.LogWarning($"[CharacterSetup] SitToStand skipped — no '{MixamoClipPath("Sit To Stand(1)")}' found.");

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
            // The scene owns the character's position, not the animation —
            // FirstPersonController's CharacterController moves him. Note this
            // DISCARDS root motion rather than preventing it being extracted,
            // which is why ApplyInPlaceRootMotion bakes it into the pose at
            // import time instead of relying on this line alone.
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

            // Layer 3 (Docs/CharacterPipeline.md §3) — hats, held items, and the
            // carried bowling ball all hang off this one component. SAME ROOT,
            // same reasoning as the reaction actor above; it resolves the
            // Animator from its CHILDREN (see AttachmentSlots' class doc for why
            // it must not RequireComponent one here).
            var slots = root.AddComponent<WeeSpurts.Characters.AttachmentSlots>();
            slots.Catalog = AssetDatabase.LoadAssetAtPath<WeeSpurts.Characters.AttachmentCatalog>(AttachmentCatalogPath);
            if (slots.Catalog == null)
                Debug.LogWarning($"[CharacterSetup] No AttachmentCatalog at {AttachmentCatalogPath}, so the " +
                                 "character's AttachmentSlots was left unassigned — nothing will equip. Run " +
                                 "WeeSpurts > 1 Assets > Import Bowling Props (and/or Build Placeholder Attachments) " +
                                 "to create it, then run this menu item again.");

            // The ONE place the character's display size is set. Uniform, on the
            // wrapper root, so mesh and animated bone positions scale together
            // and limb proportions hold (see MascotConfig.DisplayScale).
            root.transform.localScale = Vector3.one * mascotConfig.DisplayScale;
            LogCharacterHeight(root, mascotConfig.DisplayScale);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerCharacterPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterSetup] Done — mascot is now a HUMANOID rig. Model: {CharacterModelPath}\n" +
                      $"Controller: {ControllerPath}\nPrefab: {PlayerCharacterPrefabPath}\n" +
                      "Now run WeeSpurts -> 2 Build New Scene -> Bowling Greybox to put it in the alley.\n" +
                      (sitIdle != null && stumble != null && standToSit != null && sitToStand != null
                          ? "DrunkIdle/Defeat are now DIFFERENT Mixamo clips, and SitIdle/Stumble/StandToSit/SitToStand all exist (unwired — Block 4's job). "
                          : "DrunkIdle/Defeat are now different Mixamo clips. STILL OUTSTANDING: see the SitIdle/Stumble/StandToSit/SitToStand warnings above, if any. ") +
                      "STILL OUTSTANDING regardless: Sprint/FallFlat/Throw have no Mixamo replacement in " +
                      "today's batch so they're still on their original Meshy clips.");
        }

        /// <summary>
        /// Prints exactly which human bones Unity's automatic mapper resolved
        /// on the mascot, and returns false if a REQUIRED one is missing.
        ///
        /// WHY THIS EXISTS AT ALL: converting a rig to Humanoid either works
        /// or fails SILENTLY. Unity does not error when auto-mapping comes up
        /// short — it produces an avatar anyway, the FBX imports clean, the
        /// prefab builds, and the first thing anyone notices is a character
        /// standing in a T-pose with a console full of nothing. Tony cannot
        /// review an importer setting he can't see, so the tool has to say it
        /// out loud.
        ///
        /// HOW IT CHECKS: Animator.GetBoneTransform(HumanBodyBones) is the
        /// ground truth — it returns null for a human bone that did not map.
        /// Driving off the ENUM rather than off bone-name strings means there
        /// is nothing here for a naming convention to break: no hand-typed
        /// bone names, no dependence on whether Unity spells a mapped bone
        /// "LeftUpperArm" or "Left Upper Arm" in a given API. It also proves
        /// more than a description would — the bone is genuinely reachable in
        /// the hierarchy, not merely named somewhere.
        ///
        /// HumanTrait.BoneName[i] is printed alongside the enum name purely as
        /// a self-check: the two are index-aligned, so if that ever stops
        /// being true it shows up as an obviously mismatched pair in the log
        /// rather than as a wrong answer nobody can see.
        ///
        /// Fingers, toes, eyes and jaw are OPTIONAL to Unity and the Meshy
        /// mascot has no finger bones at all, so those are reported as a
        /// single information line rather than treated as a problem.
        /// </summary>
        private static bool ReportHumanoidMapping(GameObject modelAsset, Avatar avatar)
        {
            if (modelAsset == null)
            {
                Debug.LogError($"[CharacterSetup] Could not load {CharacterModelPath} to check its bone mapping.");
                return false;
            }

            // A temporary instance, because GetBoneTransform needs a live
            // Animator bound to the avatar — the asset on disk has no
            // hierarchy to walk. Destroyed in the finally below no matter what.
            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            try
            {
                Animator probeAnimator = probe.GetComponent<Animator>();
                if (probeAnimator == null) probeAnimator = probe.AddComponent<Animator>();
                probeAnimator.avatar = avatar;

                var missingRequired = new System.Collections.Generic.List<string>();
                var missingOptional = new System.Collections.Generic.List<string>();
                int mapped = 0;

                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    var bone = (HumanBodyBones)i;
                    if (probeAnimator.GetBoneTransform(bone) != null) { mapped++; continue; }

                    // HumanTrait.BoneName is Unity's own canonical list, so the
                    // required/optional split is Unity's opinion, not ours.
                    string label = i < HumanTrait.BoneName.Length && HumanTrait.BoneName[i] != bone.ToString()
                        ? $"{bone} (Unity calls it '{HumanTrait.BoneName[i]}')"
                        : bone.ToString();

                    if (HumanTrait.RequiredBone(i)) missingRequired.Add(label);
                    else missingOptional.Add(label);
                }

                Debug.Log($"[CharacterSetup] Humanoid avatar on {System.IO.Path.GetFileName(CharacterModelPath)}: " +
                          $"isValid={avatar.isValid}, isHuman={avatar.isHuman}. " +
                          $"{mapped} of {(int)HumanBodyBones.LastBone} human bones mapped " +
                          $"({HumanTrait.RequiredBoneCount} of them are required by Unity).");

                if (missingOptional.Count > 0)
                {
                    // Not a problem. Said out loud anyway so "he has no fingers"
                    // is a known fact rather than a later surprise.
                    Debug.Log($"[CharacterSetup] Unmapped OPTIONAL bones ({missingOptional.Count}) — this is fine, " +
                              $"Unity does not need them: {string.Join(", ", missingOptional)}");
                }

                if (missingRequired.Count == 0)
                {
                    Debug.Log("[CharacterSetup] Every REQUIRED human bone mapped. Mixamo clips will retarget onto him.");
                    return true;
                }

                // Deliberately does not attempt a fix. Writing a HumanDescription
                // by hand here would be guessing at which rig bone is which
                // human bone, and a wrong guess produces a character that
                // animates subtly wrong rather than obviously wrong — far worse
                // than stopping. Rig > Configure is a human's job.
                Debug.LogError($"[CharacterSetup] STOPPED: {missingRequired.Count} REQUIRED human bone(s) did not map, " +
                               $"so retargeted clips would play as a T-pose:\n  {string.Join("\n  ", missingRequired)}\n" +
                               $"Fix by hand: select {CharacterModelPath} > Rig tab > Configure..., map those bones, " +
                               "Apply, then run WeeSpurts > 1 Assets > Set Up Player Character again. " +
                               "This tool will not guess the mapping for you.");
                return false;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Makes a clip play IN PLACE, which is what a CharacterController-driven
        /// game needs: the scene owns where the character is, the animation owns
        /// only what his body does.
        ///
        /// THE TWO SETTINGS ARE NOT THE SAME THING, and the names invite mixing
        /// them up (verified against the 6000.5 scripting reference rather than
        /// recalled, per CLAUDE.md rule 3):
        ///   * lockRootPositionXZ / lockRootHeightY / lockRootRotation are
        ///     "Bake Into Pose". Enabled = that component of root motion is
        ///     baked into the BONES, so the root never moves. This is the one
        ///     that makes a clip in-place.
        ///   * keepOriginalPositionXZ / keepOriginalPositionY /
        ///     keepOriginalOrientation are "Based Upon" — the REFERENCE frame
        ///     the bake is measured against, not whether it happens.
        ///
        /// What is chosen here and why:
        ///   XZ      lock = true, keepOriginal = false. In-place horizontally,
        ///           measured from his centre of mass so he stays centred over
        ///           his own root instead of drifting off it.
        ///   Y       lock = true, keepOriginal = TRUE. Also in-place vertically,
        ///           but measured from the clip's ORIGINAL height so his feet
        ///           stay on the floor. Measuring Y from centre of mass instead
        ///           re-grounds every clip on its own average and makes him sink
        ///           or float between states.
        ///   Rotation lock = true, keepOriginal = false. Yaw belongs to the
        ///           CharacterController; a clip that turns him would fight it.
        ///
        /// WHY BOTHER, given the prefab sets applyRootMotion = false? Because
        /// that setting DISCARDS root motion rather than preventing it. An
        /// unbaked clip still has its translation stripped out of the bones and
        /// put on the root, where it is then thrown away — so the feet cycle
        /// without the body going anywhere, which is exactly what foot-sliding
        /// is. Baking is also what makes this robust to a Mixamo download where
        /// the "In Place" box was missed.
        /// </summary>
        private static void ApplyInPlaceRootMotion(ModelImporterClipAnimation clip)
        {
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = false;

            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;

            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = false;
        }

        /// <summary>
        /// Imports every FBX in Animations/Mixamo/ as a Humanoid clip.
        ///
        /// FOLDER-DRIVEN, not table-driven, and deliberately so: which Mixamo
        /// clips exist changes every time Tony downloads another one, and the
        /// settings below are correct for ALL of them. Which animator STATE
        /// uses which clip is a separate question that lives in the Clips table
        /// and the controller — this method only makes the files usable.
        ///
        /// CreateFromThisModel, NOT CopyFromOther. This is the single most
        /// important line in the method. A Mixamo FBX carries its own skeleton
        /// named mixamorig:Hips, mixamorig:LeftArm, and so on. CopyFromOther
        /// would apply the MASCOT's bone-name mapping to it, match nothing, and
        /// import a humanoid clip bound to no bones — a T-pose, with no error.
        /// Building each file its own avatar from its own skeleton is what lets
        /// Humanoid retargeting carry the motion onto the mascot at runtime,
        /// which is the entire reason for the Humanoid conversion.
        ///
        /// A missing folder is not an error — it just means no downloads yet.
        /// </summary>
        private static void ImportMixamoClips()
        {
            if (!AssetDatabase.IsValidFolder(MixamoFolder))
            {
                Debug.Log($"[CharacterSetup] No {MixamoFolder} folder yet, so no Mixamo clips were imported. " +
                          "Drop the downloads in there and re-run this menu item.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { MixamoFolder });
            if (guids.Length == 0)
            {
                Debug.Log($"[CharacterSetup] {MixamoFolder} exists but has no FBXs in it yet.");
                return;
            }

            int imported = 0;
            var report = new System.Collections.Generic.List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.autoGenerateAvatarMappingIfUnspecified = true;
                // Downloaded "Without Skin", but a stray skinned download would
                // otherwise litter the project with materials we never render.
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.globalScale = ImportScale;
                importer.useFileScale = true;
                importer.addCollider = false;
                importer.optimizeGameObjects = false;

                // Commit the rig settings BEFORE reading the take list — same
                // reason as the Meshy loop above: defaultClipAnimations reports
                // what the last COMPLETED import found, not what is pending.
                importer.SaveAndReimport();

                // BELT AND BRACES against a fresh-file race (found live,
                // 2026-08-11): on a big batch of just-moved-in FBXs,
                // SaveAndReimport can return before Unity's asset worker has
                // actually finished writing THIS file's animation curves —
                // observed correlating with the larger "with skin" downloads,
                // but not exclusively (two small ones hit it too). The symptom
                // is not "ambiguous take", it's defaultClipAnimations coming
                // back completely EMPTY — confirmed by the .meta files
                // themselves ending up ~1 KB smaller than a clip that actually
                // imported. ForceSynchronousImport blocks until THIS asset's
                // import is genuinely done before anything below trusts its
                // output. (Verified against the 6000.5 scripting reference —
                // the real member is ForceSynchronousImport, not
                // ForceSynchronous; that wrong guess is what broke the first
                // attempt at this fix, CS0117, caught by Tony's own compile
                // before it did anything worse than fail loudly.)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[CharacterSetup] {path} lost its importer during the forced reimport — skipping it.");
                    continue;
                }

                ModelImporterClipAnimation[] takes =
                    importer.defaultClipAnimations ?? new ModelImporterClipAnimation[0];
                string[] takeNames = System.Array.ConvertAll(takes, t => t?.takeName);
                int take = SelectTakeIndex(takeNames);
                if (take < 0)
                {
                    Debug.LogWarning($"[CharacterSetup] {path} has no unambiguous take, so its clip was left " +
                                     $"as-is. Takes found: {string.Join(", ", takeNames)}");
                    continue;
                }

                // Named after the FILE, because that is what Tony named it on
                // Mixamo and it is how he will look for it in the Inspector.
                // The animator states bind by clip name (see AddState), so this
                // is the name the Clips table will reference.
                string clipName = System.IO.Path.GetFileNameWithoutExtension(path);
                bool loop = LooksLikeLocomotion(clipName);

                ModelImporterClipAnimation source = takes[take];
                var output = new ModelImporterClipAnimation
                {
                    name = clipName,
                    takeName = source.takeName,
                    firstFrame = source.firstFrame,
                    lastFrame = source.lastFrame,
                    loopTime = loop,
                };
                ApplyInPlaceRootMotion(output);

                importer.clipAnimations = new[] { output };
                importer.SaveAndReimport();

                imported++;
                report.Add($"{clipName}{(loop ? " (looping)" : "")}");
            }

            Debug.Log($"[CharacterSetup] Imported {imported} Mixamo clip(s) as Humanoid, in-place: " +
                      $"{string.Join(", ", report)}\n" +
                      "Looping was guessed from the file name — see LooksLikeLocomotion. Anything guessed " +
                      "wrong is one tick of Loop Time in the FBX's Animation tab, or a row in the Clips table.");
        }

        /// <summary>
        /// Whether a clip name reads as something that should LOOP — an idle or
        /// a locomotion cycle — rather than a one-shot like a throw or a fall.
        ///
        /// A HEURISTIC, and flagged as one in the console when it runs. It
        /// exists because Mixamo files arrive named for what they are ("Idle",
        /// "Drunk Walk", "Stagger") and getting looping right on first import is
        /// worth more to a playtest than making Tony tick twelve checkboxes. It
        /// is not authority: once a clip has a row in the Clips table, that row
        /// decides. Pure and string-only so it stays cheap to reason about.
        /// </summary>
        private static bool LooksLikeLocomotion(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return false;
            string n = clipName.ToLowerInvariant();
            string[] loopingTokens = { "idle", "walk", "run", "sprint", "jog", "stagger", "strafe", "turn" };
            foreach (string token in loopingTokens)
                if (n.Contains(token)) return true;
            return false;
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
        /// <summary>
        /// Builds the thrower's material for a rig that ships LOOSE texture
        /// files (the Mascot Base Rig) rather than a finished .mat.
        ///
        /// This is the fix for the failure that killed the previous body swap.
        /// Meshy's FBXs carry a texture reference Unity's auto-search does not
        /// resolve, so letting the importer generate the material yields one
        /// with an empty _BaseMap and a flat white character — confirmed twice
        /// by playtest now. Binding the PNG ourselves sidesteps the importer
        /// entirely, which is the same reasoning as
        /// <see cref="BuildMascotThrowerMaterial"/>; only the SOURCE differs
        /// (a texture file here, a shipped material there).
        ///
        /// Colour map only, deliberately. The _metallic and _roughness maps
        /// beside it go unused because the art direction is flat stylised, not
        /// PBR — the same call Docs/ArtGuide.md already records for props.
        ///
        /// Shader is looked up by name rather than hard-referenced so this
        /// fails LOUDLY (and returns null, leaving the import's own material in
        /// place) on a non-URP project instead of silently producing magenta.
        /// </summary>
        private static Material BuildLooseTextureThrowerMaterial(float opacity)
        {
            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseRigBaseColorPath);
            if (baseColor == null)
            {
                Debug.LogWarning($"[CharacterSetup] Base colour texture not found at {BaseRigBaseColorPath} — " +
                                 "the thrower will render untextured. Check the file actually imported.");
                return null;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[CharacterSetup] Shader 'Universal Render Pipeline/Lit' not found — is this " +
                               "still a URP project? Leaving the thrower on its imported material.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder(ProjectRoot, "Materials");

            string path = $"{MaterialsFolder}/Thrower_MascotBase.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                // Re-stamp every run so a shader/texture change upstream can't
                // leave a stale asset behind — same reasoning as the sibling
                // builder's CopyPropertiesFromMaterial.
                mat.shader = urpLit;
            }

            // _BaseMap is URP's albedo slot; _MainTex is set too so anything
            // reading the built-in name (and Material.mainTexture) agrees.
            mat.SetTexture("_BaseMap", baseColor);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseColor);

            MaterialTransparency.Apply(mat, opacity);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

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
            // The scale that WOULD land him mid-range. Printed, never applied:
            // DisplayScale is Tony's dial (create-once, see LoadOrCreateMascotConfig),
            // and a tool that silently retunes the thing it is measuring is a
            // tool you can't trust the measurement from.
            const float TargetHeight = 1.75f;
            float suggested = TargetHeight / raw;
            // Deliberately no "…and the lane is N m wide" comparison here: that
            // number lives on LaneConfig and would go stale the moment it's
            // tuned. Adult human height doesn't.
            Debug.Log($"[CharacterSetup] Character height: {raw:0.00} m unscaled -> {scaled:0.00} m at " +
                      $"DisplayScale {displayScale}. An adult should read about 1.7-1.8 m; " +
                      $"DisplayScale {suggested:0.00} would put him at {TargetHeight:0.00} m. " +
                      "Nothing was changed for you — retune MascotConfig.asset in the Inspector if you want it.\n" +
                      "NOTE: Humanoid retargeting normalises PROPORTIONS for playback, it does not resize the " +
                      "mesh, so this unscaled number is expected to be about what it was under Generic.");
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

        /// <summary>
        /// Adds a state using the Mixamo download named <paramref name="mixamoFileName"/>
        /// when it's actually on disk, falling back to the Meshy
        /// (<paramref name="meshySourceFile"/>, <paramref name="meshyClipName"/>)
        /// clip otherwise. <paramref name="mixamoFileName"/> may be null for a
        /// state with no Mixamo replacement at all (Sprint, FallFlat, Throw) —
        /// that always takes the Meshy branch.
        ///
        /// WHY A DISK CHECK AND NOT JUST "DID ImportMixamoClips RUN": missing
        /// is the NORMAL case on a fresh clone or before Tony has grabbed a
        /// given clip, exactly like MixamoFolder's own missing-folder handling
        /// above — this has to degrade to the Meshy clip, not error, so the
        /// tool stays "safe to run repeatedly" per the class doc even with an
        /// empty or partial Animations/Mixamo/ folder.
        /// </summary>
        private static AnimatorState AddStatePreferMixamo(
            AnimatorStateMachine sm, string stateName,
            string mixamoFileName, string meshySourceFile, string meshyClipName,
            Vector3 position)
        {
            if (mixamoFileName != null && MixamoClipExists(mixamoFileName))
                return AddState(sm, stateName, MixamoClipPath(mixamoFileName), mixamoFileName, position);

            return AddState(sm, stateName, MascotClipPath(meshySourceFile), meshyClipName, position);
        }

        /// <summary>
        /// Adds a state from a Mixamo download with NO Meshy fallback (SitIdle,
        /// Stumble) — returns null instead of building a state bound to
        /// nothing when the file isn't usable. Deliberately not folded into
        /// <see cref="AddStatePreferMixamo"/>: that method always returns a
        /// state (it has a fallback to fall back TO), this one legitimately
        /// might not, and callers need to tell the two apart.
        /// </summary>
        private static AnimatorState TryAddMixamoOnlyState(AnimatorStateMachine sm, string stateName, string mixamoFileName, Vector3 position)
        {
            if (!MixamoClipExists(mixamoFileName)) return null;
            return AddState(sm, stateName, MixamoClipPath(mixamoFileName), mixamoFileName, position);
        }

        /// <summary>
        /// True only when the Mixamo FBX exists AND actually produced a usable,
        /// named AnimationClip — NOT merely when the file is on disk.
        ///
        /// FOUND LIVE, 2026-08-11: some of Tony's "With Skin" Mixamo downloads
        /// import cleanly as a MODEL (so AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;
        /// happily returns one) while contributing ZERO AnimationClip
        /// sub-assets — ImportMixamoClips' own SelectTakeIndex reports "Takes
        /// found: " completely empty for them, reproducibly, even after a
        /// forced ImportAssetOptions.ForceSynchronousImport reimport from a
        /// cold Editor with no other Unity instance running. Not a timing
        /// race — confirmed by content inspection (the raw FBX bytes plainly
        /// contain "mixamo.com" and AnimStack data) that Unity's importer
        /// still doesn't surface as a clip for these specific files, for
        /// reasons this tool cannot fix from the outside. The "Without Skin"
        /// downloads in the SAME batch import perfectly.
        ///
        /// A file-existence check alone would have committed the state to a
        /// clip path that resolves to nothing — exactly the failure mode
        /// AddState's own "state will be empty" warning exists to catch, but
        /// silently worse here: it would have thrown away a known-working
        /// Meshy fallback for no benefit. Checking the actual clip is what
        /// makes AddStatePreferMixamo's fallback real instead of theoretical.
        /// </summary>
        private static bool MixamoClipExists(string fileName)
        {
            string path = MixamoClipPath(fileName);
            return AssetDatabase
                .LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Any(c => c.name == fileName);
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
