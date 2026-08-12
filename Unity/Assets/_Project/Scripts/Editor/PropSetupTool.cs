using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WeeSpurts.Characters;
using WeeSpurts.Gameplay;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Turns the four re-exported MeshyDump props (Bowling_Ball, Bowling_Pin,
    /// Bowling_Pin_Hat, Mop — moved into Assets/_Project/Art/Props/ this
    /// session, geometry verified by reading the .glb binaries directly: 838
    /// / 1230 / 1559 / 1253 tris respectively) into usable prefabs, and wires
    /// two of them into each of the two existing item systems.
    ///
    /// WHY A MATERIAL BUILT HERE INSTEAD OF THE ONE glTFast GENERATES: same
    /// call CharacterSetupTool.BuildMascotThrowerMaterial makes for the
    /// mascot — glTFast's own imported material references its embedded
    /// BaseColor texture SUB-ASSET, which is a Texture2D baked in at whatever
    /// resolution the source PNG was (4096x4096 here) with NO Max Size or
    /// Compression control, because it never went through Unity's normal
    /// TextureImporter (see PropTexturePostprocessor's doc comment for why).
    /// This tool instead points a fresh material at the STANDALONE, already-
    /// extracted-and-downsized BaseColor PNG sitting in each prop's Textures/
    /// folder, which DOES get a real TextureImporter and DOES get capped by
    /// PropTexturePostprocessor. The glTFast-generated material/texture sub-
    /// assets are simply never referenced by anything.
    ///
    /// METALLIC/ROUGHNESS MAPS ARE DELIBERATELY NOT IMPORTED AT ALL — Tony's
    /// call, "flat stylized, not PBR". They still exist inside the source
    /// .glb (untouched — geometry and the rest of the file are Tony's
    /// instruction to leave alone), just never extracted or wired to
    /// anything. Whether to also strip them from the .glb itself is a
    /// separate, DESTRUCTIVE edit to the source file this tool does NOT make
    /// without Tony confirming first (see SetUp's closing log line).
    ///
    /// CODE, NOT HAND-BUILT PREFABS/MATERIALS — same reproducibility reason
    /// as every other *SetupTool here (CLAUDE.md: no hand-edited prefab
    /// YAML). Safe to re-run: prefabs/materials are rebuilt every time (nothing
    /// in them is Tony-tunable yet), catalog rows are ADD-IF-MISSING so a
    /// re-run can never renumber or stomp an id (same rule
    /// AttachmentSetupTool now follows).
    ///
    /// Menu: WeeSpurts -> 1 Assets -> Import Bowling Props
    /// </summary>
    public static class PropSetupTool
    {
        private const string PropsFolder = "Assets/_Project/Art/Props";
        private const string AttachmentCatalogPath = "Assets/_Project/ScriptableObjects/AttachmentCatalog.asset";
        private const string ItemCatalogPath = "Assets/_Project/ScriptableObjects/ItemCatalog.asset";

        /// <summary>
        /// One row per prop: the folder/file name they were moved in under,
        /// the display name used in materials/prefabs/catalog rows, and how big
        /// it should actually READ in the world, in metres.
        ///
        /// THAT LAST NUMBER IS LOAD-BEARING AND THE REASON THIS ISN'T JUST "1".
        /// Meshy normalises every export it produces into roughly the same unit
        /// box — measured across all four of these: 1.892 / 1.903 / 1.901 /
        /// 1.903 units on their longest axis, whether it's a bowling ball or a
        /// mop. So the native size carries NO information about how big the
        /// thing is meant to be, and an attachment left at ScaleOffset = 1 would
        /// render a ~1.3m bowling ball engulfing the character. The real
        /// dimension has to come from somewhere, so it comes from here.
        ///
        /// The BALL's 0.22 is not a guess — it is BallConfig.Radius (0.11) * 2,
        /// i.e. exactly the size of the physical ball that replaces it the
        /// instant the throw fires. They must agree or the handoff reads as the
        /// ball changing size mid-throw.
        /// </summary>
        private static readonly (string FileBase, string DisplayName, float TargetSizeMeters)[] Props =
        {
            ("BowlingBall",    "Bowling Ball",     0.22f),  // = BallConfig.Radius * 2
            ("BowlingPin",     "Bowling Pin",      0.38f),  // regulation pin is ~0.38m tall
            ("BowlingPinHat",  "Bowling Pin Hat",  0.30f),  // a hat, so: head-sized, judged by eye
            ("Mop",            "Mop",              1.20f),  // shoulder-height on an adult
        };

        /// <summary>
        /// REAL, PERMANENT ids — never renumbered once assigned (same rule
        /// ItemCatalog/VendorItem/AttachmentDefinition all state on their own
        /// ItemId fields). Both catalogs start their real numbering at 1;
        /// AttachmentSetupTool's still-pending placeholders live at 901+
        /// specifically so they can never collide with these.
        /// </summary>
        private const int AttachmentId_BowlingBall = 1;
        private const int AttachmentId_BowlingPinHat = 2;
        private const int ItemId_Mop = 1;
        private const int ItemId_BowlingPin = 2;

        [MenuItem("WeeSpurts/1 Assets/Import Bowling Props")]
        public static void SetUp()
        {
            var prefabs = new Dictionary<string, GameObject>();
            var attachScales = new Dictionary<string, Vector3>();
            foreach (var (fileBase, displayName, targetSize) in Props)
            {
                GameObject prefab = BuildPropPrefab(fileBase, displayName);
                if (prefab == null) continue;
                prefabs[fileBase] = prefab;
                attachScales[fileBase] = ComputeAttachmentScale(fileBase, targetSize);
            }

            // ----- AttachmentCatalog: Bowling Ball (RightHand), Bowling Pin Hat (Head) -----
            if (prefabs.TryGetValue("BowlingBall", out GameObject ball) &&
                prefabs.TryGetValue("BowlingPinHat", out GameObject pinHat))
            {
                AttachmentCatalog attachCatalog = LoadOrCreateAttachmentCatalog();
                bool addedBall = UpsertAttachment(attachCatalog, AttachmentId_BowlingBall, "Bowling Ball", ball,
                                                  HumanBodyBones.RightHand, attachScales["BowlingBall"]);
                bool addedHat = UpsertAttachment(attachCatalog, AttachmentId_BowlingPinHat, "Bowling Pin Hat", pinHat,
                                                 HumanBodyBones.Head, attachScales["BowlingPinHat"]);
                EditorUtility.SetDirty(attachCatalog);
                Debug.Log($"[PropSetup] AttachmentCatalog: Bowling Ball id={AttachmentId_BowlingBall} " +
                          $"(RightHand, ScaleOffset {attachScales["BowlingBall"].x:0.000})" +
                          $"{(addedBall ? "" : " [ALREADY PRESENT, LEFT AS-IS]")}, " +
                          $"Bowling Pin Hat id={AttachmentId_BowlingPinHat} " +
                          $"(Head, ScaleOffset {attachScales["BowlingPinHat"].x:0.000})" +
                          $"{(addedHat ? "" : " [ALREADY PRESENT, LEFT AS-IS]")}. Same id/prefab Block 6 " +
                          "should reuse for the carried ball — do not add a second mechanism for it.\n" +
                          (addedBall && addedHat ? "" :
                           "NOTE: a row marked ALREADY PRESENT keeps whatever ScaleOffset it was created with. " +
                           "Rows written before the scale computation existed have ScaleOffset 1, which renders " +
                           "a ~1.3m bowling ball. To adopt the numbers above, delete those rows from " +
                           "AttachmentCatalog.asset in the Inspector and run this again."));
            }

            // ----- ItemCatalog: Mop (LaneAction), Bowling Pin / "Loose Pin" (Throwable) -----
            ItemCatalog itemCatalog = LoadOrCreateItemCatalog();
            bool addedMop = UpsertItem(itemCatalog, ItemId_Mop, "Mop", stackable: false, maxStack: 99, ItemUseContext.LaneAction);
            bool addedPin = UpsertItem(itemCatalog, ItemId_BowlingPin, "Loose Pin", stackable: true, maxStack: 5, ItemUseContext.Throwable);
            EditorUtility.SetDirty(itemCatalog);
            Debug.Log($"[PropSetup] ItemCatalog: Mop id={ItemId_Mop} (LaneAction)" +
                      $"{(addedMop ? "" : " [already present, left as-is]")}, " +
                      $"Loose Pin id={ItemId_BowlingPin} (Throwable)" +
                      $"{(addedPin ? "" : " [already present, left as-is]")}.");

            if (prefabs.TryGetValue("BowlingPin", out GameObject pin))
                Debug.Log($"[PropSetup] {pin.name}'s prefab is the shared mesh Tony asked to reuse as a venue " +
                          "prop — drag it into the ThunderLanesVenue scene by hand wherever a decorative pin " +
                          "should sit (this tool does not touch scene files, CLAUDE.md).");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PropSetup] Done. MetallicRoughness maps were NOT imported anywhere (flat-stylized " +
                      "call) but are still sitting inside the source .glb files, untouched. Say the word if " +
                      "you want them physically stripped from the .glb too — that edits the source file and " +
                      "this tool won't do it without you confirming first.");
        }

        /// <summary>
        /// Pulls the mesh out of the imported .glb and pairs it with a fresh
        /// material pointed at the standalone, already-downsized BaseColor
        /// texture (see class doc for why not glTFast's own material).
        /// Purely visual — NO collider, same "cosmetic must never become a
        /// physical obstacle" rule GreyboxSceneBuilder enforces for anything
        /// that isn't gameplay-physical (the thrower proxy, the nuke sphere,
        /// neighbour-lane decoration all learned this the same way).
        /// </summary>
        private static GameObject BuildPropPrefab(string fileBase, string displayName)
        {
            string folder = $"{PropsFolder}/{fileBase}";
            string glbPath = $"{folder}/{fileBase}.glb";
            string texturePath = $"{folder}/Textures/{fileBase}_BaseColor.png";
            string materialPath = $"{folder}/{fileBase}.mat";
            string prefabPath = $"{folder}/{fileBase}.prefab";

            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<Mesh>().FirstOrDefault();
            if (mesh == null)
            {
                Debug.LogWarning($"[PropSetup] No mesh found in {glbPath} — has Unity imported it yet? " +
                                  "(glTFast import happens automatically once the editor notices the file; " +
                                  "if this is the first run right after the move, focus the editor once and re-run.)");
                return null;
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (baseColor == null)
                Debug.LogWarning($"[PropSetup] No BaseColor texture at {texturePath} yet — {displayName} will use the shader default (likely white/pink) until it's imported.");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            // mainTexture/color are the shader-agnostic [MainTexture]/[MainColor]
            // tagged properties both Standard and URP/Lit expose, same reason
            // GreyboxSceneBuilder sets `color` rather than a hardcoded property
            // name — works without knowing which of the two shaders resolved.
            mat.mainTexture = baseColor;
            mat.color = Color.white; // no tint; the texture IS the look
            EditorUtility.SetDirty(mat);

            GameObject root = new GameObject(displayName);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = mat;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static AttachmentCatalog LoadOrCreateAttachmentCatalog()
        {
            AttachmentCatalog existing = AssetDatabase.LoadAssetAtPath<AttachmentCatalog>(AttachmentCatalogPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/_Project/ScriptableObjects");
            AttachmentCatalog created = ScriptableObject.CreateInstance<AttachmentCatalog>();
            AssetDatabase.CreateAsset(created, AttachmentCatalogPath);
            return created;
        }

        private static ItemCatalog LoadOrCreateItemCatalog()
        {
            ItemCatalog existing = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/_Project/ScriptableObjects");
            ItemCatalog created = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(created, ItemCatalogPath);
            return created;
        }

        /// <summary>
        /// The ScaleOffset that makes a prop read at its intended real-world
        /// size once it's hanging off a bone — see the Props table for why this
        /// can't just be 1.
        ///
        /// TWO DIVISIONS, and the second one is the subtle one:
        ///   * by the mesh's own longest axis, which un-does Meshy's
        ///     normalisation and is read from the imported mesh rather than
        ///     hardcoded, so a future re-export at a different normalisation
        ///     self-corrects instead of silently resizing every prop;
        ///   * by MascotConfig.DisplayScale, because the attachment's scale is
        ///     LOCAL to a bone that has already been scaled by the character
        ///     root. Without this the ball would shrink and grow whenever Tony
        ///     retunes the character's height.
        ///
        /// KNOWN TENSION, deliberately resolved this way and worth knowing:
        /// this pins a prop to a FIXED WORLD SIZE, which is unarguably right
        /// for the carried ball (it must match the physical BowlingBall that
        /// replaces it) but arguably wrong for a hat, which you could equally
        /// argue should grow with the character wearing it. One rule for all
        /// items is the simpler thing to reason about until there's a reason
        /// not to — and every one of these is a per-row Inspector field on the
        /// catalog, so overriding any single prop costs one number, no code.
        /// If DisplayScale changes a lot, re-run this tool to recompute.
        /// </summary>
        private static Vector3 ComputeAttachmentScale(string fileBase, float targetSizeMeters)
        {
            string glbPath = $"{PropsFolder}/{fileBase}/{fileBase}.glb";
            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<Mesh>().FirstOrDefault();
            if (mesh == null) return Vector3.one;

            Vector3 size = mesh.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest <= 0.0001f) return Vector3.one;

            float displayScale = 1f;
            var mascot = AssetDatabase.LoadAssetAtPath<WeeSpurts.Bowling.MascotConfig>(
                "Assets/_Project/ScriptableObjects/MascotConfig.asset");
            if (mascot != null && mascot.DisplayScale > 0.0001f) displayScale = mascot.DisplayScale;

            return Vector3.one * (targetSizeMeters / (longest * displayScale));
        }

        /// <summary>Adds a row only if the id isn't already there. Returns whether it added one — never overwrites an existing row (Tony may have hand-tuned it).</summary>
        private static bool UpsertAttachment(AttachmentCatalog catalog, int id, string name, GameObject prefab,
                                             HumanBodyBones bone, Vector3 scaleOffset)
        {
            if (catalog.TryGetDefinition(id, out _)) return false;

            var list = new List<AttachmentDefinition>(catalog.Attachments ?? new AttachmentDefinition[0]);
            list.Add(new AttachmentDefinition
            {
                ItemId = id,
                DisplayName = name,
                Prefab = prefab,
                Bone = bone,
                ScaleOffset = scaleOffset,
            });
            catalog.Attachments = list.ToArray();
            return true;
        }

        /// <summary>Same add-if-missing rule as UpsertAttachment, for ItemCatalog rows.</summary>
        private static bool UpsertItem(ItemCatalog catalog, int id, string name, bool stackable, int maxStack, ItemUseContext context)
        {
            if (catalog.TryGetItem(id, out _)) return false;

            var list = new List<InventoryItem>(catalog.Items ?? new InventoryItem[0]);
            list.Add(new InventoryItem
            {
                ItemId = id,
                DisplayName = name,
                Stackable = stackable,
                MaxStack = maxStack,
                UseContext = context,
            });
            catalog.Items = list.ToArray();
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
