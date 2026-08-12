using UnityEditor;
using UnityEngine;
using WeeSpurts.Characters;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Builds a placeholder cube attachment prefab and demo catalog rows for
    /// whatever cosmetics are STILL waiting on a low-poly re-export (Crown,
    /// Party_Cone_Hat, Propeller_Cap, Beer_Bottle as of 2026-08-11 — see
    /// MeshyDump/). Four others (Bowling_Ball, Bowling_Pin, Bowling_Pin_Hat,
    /// Mop) already got re-exported small and have REAL rows from
    /// <see cref="PropSetupTool"/> instead — this tool only ever touches the
    /// ids in <see cref="PlaceholderItems"/> and never those.
    ///
    /// CODE, NOT A HAND-BUILT PREFAB, for the same reason as every other
    /// *SetupTool in this folder (see CharacterSetupTool's class doc):
    /// reproducible, survives a fresh clone, no hand-edited prefab YAML
    /// (CLAUDE.md). Safe to run repeatedly — CREATE-ONCE for the catalog
    /// asset (same rule as MascotConfig: never stomps a Tony edit), rebuild-
    /// every-time for the prefab, since it has nothing for a human to tune.
    ///
    /// Menu: WeeSpurts -> 1 Assets -> Build Placeholder Attachments
    /// </summary>
    public static class AttachmentSetupTool
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string PrefabPath = ProjectRoot + "/Prefabs/Attachments/PlaceholderAttachment.prefab";
        private const string MaterialPath = ProjectRoot + "/Materials/PlaceholderAttachment.mat";
        private const string CatalogPath = ProjectRoot + "/ScriptableObjects/AttachmentCatalog.asset";

        /// <summary>
        /// Demo ids for the placeholder catalog, one per slot so Equip can be
        /// exercised against every <see cref="AttachmentSlot"/> in the editor.
        /// NOT final content — items still waiting on MeshyDump re-export
        /// (Crown, Party_Cone_Hat, Propeller_Cap, Beer_Bottle as of
        /// 2026-08-11) use these until they get real rows of their own.
        ///
        /// RANGE IS 901+ ON PURPOSE. Real, permanent catalog ids start at 1
        /// (see PropSetupTool, which landed the first two: 1 = Bowling Ball,
        /// 2 = Bowling Pin Hat). Keeping demo content in its own numeric band
        /// means a real item can never collide with a placeholder id, and a
        /// placeholder never has to be renumbered out of the way when its
        /// real replacement finally lands — it just gets a fresh low id and
        /// the placeholder row (or the whole demo slot) quietly stops being
        /// referenced by anything.
        /// </summary>
        private static readonly (int Id, string Name, HumanBodyBones Bone)[] PlaceholderItems =
        {
            (901, "Placeholder Hat",        HumanBodyBones.Head),
            (902, "Placeholder Visor",      HumanBodyBones.Head),
            (903, "Placeholder Right Prop", HumanBodyBones.RightHand),
            (904, "Placeholder Left Prop",  HumanBodyBones.LeftHand),
            (905, "Placeholder Backpack",   HumanBodyBones.Spine),
        };

        [MenuItem("WeeSpurts/1 Assets/Build Placeholder Attachments")]
        public static void SetUp()
        {
            GameObject prefab = BuildPlaceholderPrefab();
            AttachmentCatalog catalog = LoadOrCreateCatalog();

            // ADD-IF-MISSING, not overwrite: the catalog can now also hold
            // REAL rows from PropSetupTool (ids 1, 2 as of 2026-08-11), and
            // this tool must never wipe those out from under it just because
            // it ran second. Same reasoning as PropSetupTool.Upsert.
            int added = 0;
            foreach (var item in PlaceholderItems)
            {
                if (catalog.TryGetDefinition(item.Id, out _)) continue;

                var list = new System.Collections.Generic.List<AttachmentDefinition>(catalog.Attachments ?? new AttachmentDefinition[0]);
                list.Add(new AttachmentDefinition
                {
                    ItemId = item.Id,
                    DisplayName = item.Name,
                    Prefab = prefab,
                    Bone = item.Bone,
                    PositionOffset = Vector3.zero,
                    RotationOffsetEuler = Vector3.zero,
                    ScaleOffset = Vector3.one,
                });
                catalog.Attachments = list.ToArray();
                added++;
            }
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AttachmentSetup] Placeholder prefab: {PrefabPath}\n" +
                      $"Catalog: {CatalogPath} ({added} placeholder row(s) added this run, " +
                      $"{catalog.Attachments.Length} total).\n" +
                      "Assign the catalog to an AttachmentSlots component (on the same object as the " +
                      "Animator) and call Equip(slot, itemId) to test. Placeholder ids are 901+ — see " +
                      "PlaceholderItems' doc comment for why they're kept out of the real 1+ range.");
        }

        /// <summary>
        /// A small grey cube with its default collider removed — same lesson
        /// GreyboxSceneBuilder learned three times over: GameObject.
        /// CreatePrimitive always attaches one, and a purely cosmetic
        /// attachment must never become a physical obstacle (imagine a hat
        /// that blocks the ball).
        /// </summary>
        private static GameObject BuildPlaceholderPrefab()
        {
            EnsureFolder(ProjectRoot + "/Prefabs/Attachments");

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PlaceholderAttachment";
            cube.transform.localScale = Vector3.one * 0.15f;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.GetComponent<Renderer>().sharedMaterial = LoadOrCreateMaterial();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(cube, PrefabPath);
            Object.DestroyImmediate(cube);
            return saved;
        }

        private static AttachmentCatalog LoadOrCreateCatalog()
        {
            AttachmentCatalog existing = AssetDatabase.LoadAssetAtPath<AttachmentCatalog>(CatalogPath);
            if (existing != null) return existing;

            EnsureFolder(ProjectRoot + "/ScriptableObjects");
            AttachmentCatalog created = ScriptableObject.CreateInstance<AttachmentCatalog>();
            AssetDatabase.CreateAsset(created, CatalogPath);
            return created;
        }

        private static Material LoadOrCreateMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            // URP projects render "Standard" as pink; pick whichever shader
            // actually exists, same fallback GreyboxSceneBuilder uses.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader) { color = new Color(0.8f, 0.2f, 0.8f) }; // loud magenta: unmistakably a placeholder
            EnsureFolder(ProjectRoot + "/Materials");
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
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
