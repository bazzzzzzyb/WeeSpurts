using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Core;
using WeeSpurts.Gameplay;
using WeeSpurts.Player;
using WeeSpurts.Slop;
using WeeSpurts.UI;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Makes the existing <see cref="Vendor"/> and <see cref="BlackjackTable"/>
    /// engines reachable in ThunderLanesVenue.unity: a <see cref="BarStation"/>
    /// near the bar art, a <see cref="BlackjackStation"/> at the casino nook's
    /// card table, and a <see cref="GameManager"/> (+ <see cref="TicketBalanceHud"/>
    /// on the Player root) so there is a ledger for either of them to talk to.
    ///
    /// Menu: WeeSpurts -> 4 Thunder Lanes Venue -> 3 Set Up Economy Stations
    ///
    /// PLACEMENT IS MEASURED, NOT GUESSED — same discipline as
    /// ThunderLanesVenueBowlingSetupTool's Lane_Bed_5 floor-height read. The
    /// bar anchor comes from 'Bar_and_Lounge' art bounds + 'Floor_Bar_Lounge'
    /// for height; the blackjack seat comes from 'Blackjack_Pit_Table' and
    /// 'Dealer_Podium' — the seat sits on the table's far side from the
    /// dealer, on the floor measured from 'Floor_Casino_RaisedPlatform', and
    /// faces the table. If any of those named objects are missing, THAT
    /// station is skipped with a loud warning rather than placed on a guess —
    /// the other station still gets built.
    ///
    /// IDEMPOTENT: every GameObject is found-by-name-or-created, every config
    /// asset is load-or-created, every component is get-or-added. Safe to run
    /// again after nudging a station by hand — it will not duplicate anything
    /// or stomp a position you already moved (see each block's own comment
    /// for exactly what re-running does and does not touch).
    ///
    /// Marks the scene DIRTY but does NOT save it — same precedent as
    /// ThunderLanesVenueRoamingSetupTool.
    /// </summary>
    public static class ThunderLanesVenueStationSetupTool
    {
        private const string ScenePath = "Assets/_Project/Scenes/ThunderLanesVenue.unity";
        private const string ProjectRoot = "Assets/_Project";

        private const string BarVendorConfigPath = ProjectRoot + "/ScriptableObjects/BarVendorConfig.asset";
        private const string BlackjackConfigPath = ProjectRoot + "/ScriptableObjects/CasinoBlackjackConfig.asset";
        private const string EconomyConfigPath = ProjectRoot + "/ScriptableObjects/EconomyConfig.asset";

        // Interaction anchors sit a little above the measured floor — roughly
        // hip/counter height — so PlayerInteractor's cone (measured from the
        // eye, not the feet) has a sensible target rather than something down
        // at ankle level.
        private const float BarAnchorHeight = 1.0f;
        private const float SeatFloorOffset = 0f;

        // How far the blackjack seat sits from the table centre, on the side
        // away from the dealer. A guess in the sense that no art object marks
        // "stand here", but a small, easily-nudged one — see class comment.
        private const float SeatDistanceFromTableCentre = 1.0f;

        /// <summary>
        /// Batch-mode entry point (`-executeMethod`) — see
        /// ThunderLanesVenueRoamingSetupTool.RunAndSave for why this exists
        /// alongside the menu item instead of replacing it.
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

        [MenuItem("WeeSpurts/4 Thunder Lanes Venue/3 Set Up Economy Stations")]
        public static void SetUp()
        {
            Scene scene = SceneManager.GetActiveScene();

            // ----- 0. The roaming rig must already exist -----
            PlayerAvatar avatar = Object.FindFirstObjectByType<PlayerAvatar>();
            if (avatar == null)
            {
                Debug.LogError("[ThunderLanesVenue] No PlayerAvatar found — run " +
                                "'WeeSpurts/4 Thunder Lanes Venue/1 Set Up Roaming Player' first. Nothing changed.");
                return;
            }

            // ----- 1. GameManager: the ledger every station below needs -----
            GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
            bool gameManagerIsNew = gameManager == null;
            if (gameManagerIsNew)
            {
                GameObject managers = new GameObject("GameManager");
                gameManager = managers.AddComponent<GameManager>();
                managers.AddComponent<SceneLoader>();
                managers.AddComponent<AudioManager>();
            }

            EconomyConfig economyConfig = LoadOrCreateAsset<EconomyConfig>(EconomyConfigPath);
            var gameManagerSo = new SerializedObject(gameManager);
            SerializedProperty economyConfigProperty = gameManagerSo.FindProperty("economyConfig");
            if (economyConfigProperty != null && economyConfigProperty.objectReferenceValue == null)
                economyConfigProperty.objectReferenceValue = economyConfig;
            gameManagerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gameManager);

            // ----- 2. Ticket balance overlay on the Player root -----
            TicketBalanceHud balanceHud = GetOrAdd<TicketBalanceHud>(avatar.gameObject);
            EditorUtility.SetDirty(balanceHud);

            // ----- 3. The bar -----
            string barReport = BuildBarStation();

            // ----- 4. The blackjack table -----
            string blackjackReport = BuildBlackjackStation();

            // ----- 5. Flush + report -----
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[ThunderLanesVenue] Economy stations pass on scene '{scene.name}'. The scene is DIRTY and NOT " +
                "saved — look at it first, then Ctrl+S to keep it (Ctrl+Z / reopen to discard).\n\n" +
                $"GAME MANAGER: {(gameManagerIsNew ? "created" : "reused existing")}, EconomyConfig " +
                $"'{EconomyConfigPath}' wired (starting balance {economyConfig.StartingTickets} tickets).\n" +
                $"TICKET HUD: TicketBalanceHud on '{avatar.name}' — top-right corner during Play.\n" +
                $"BAR: {barReport}\n" +
                $"BLACKJACK: {blackjackReport}\n\n" +
                "NOW TEST: press Play. Walk to the bar and press [E] — your ticket count (top-right) should " +
                "drop by the drink's price. Walk to the blackjack table and press [E] — you should sit down " +
                "and a hand should already be dealt (SPACE = Hit, ENTER = Stand, ESC = leave the table).");
        }

        // ---------- the bar ----------

        private static string BuildBarStation()
        {
            GameObject barArt = GameObject.Find("Bar_and_Lounge");
            if (barArt == null)
                return "SKIPPED — no 'Bar_and_Lounge' object found in the scene, so there is nothing to measure " +
                       "a placement from. Nothing else about the bar was touched.";

            if (!TryGetWorldBounds(barArt, out Bounds barBounds))
                return "SKIPPED — 'Bar_and_Lounge' has no Renderer anywhere under it, so its bounds could not " +
                       "be measured. Nothing else about the bar was touched.";

            float floorY = MeasureFloorHeight("Floor_Bar_Lounge", barBounds.min.y);
            Vector3 anchorPos = new Vector3(barBounds.center.x, floorY + BarAnchorHeight, barBounds.center.z);

            bool wasNewlyCreatedConfig = AssetDatabase.LoadAssetAtPath<VendorConfig>(BarVendorConfigPath) == null;
            VendorConfig config = LoadOrCreateAsset<VendorConfig>(BarVendorConfigPath);
            if (wasNewlyCreatedConfig)
            {
                // A fresh VendorConfig starts with an EMPTY item list on purpose
                // (VendorItem.cs: "Tony can retune every price without a
                // recompile") — but an empty bar can't be tested, and Stage 2's
                // whole point is proving a purchase actually moves a balance.
                // One placeholder item, set ONLY on first creation so a later
                // re-run never overwrites whatever Tony has since tuned.
                config.VendorName = "Bar";
                config.Items = new[]
                {
                    new VendorItem { ItemId = 1, DisplayName = "Pint", Price = 10, StockPerMatch = -1 }
                };
                EditorUtility.SetDirty(config);
            }

            GameObject stationGo = GameObject.Find("Station_Bar");
            bool stationIsNew = stationGo == null;
            if (stationIsNew)
            {
                stationGo = new GameObject("Station_Bar");
                stationGo.transform.position = anchorPos;
                TryParentUnderZone(stationGo, "WestWing_Zones");
            }
            // Re-run: position is NOT re-applied if the station already exists,
            // so nudging it by hand in the Inspector sticks across a re-run.

            BarStation station = GetOrAdd<BarStation>(stationGo);
            var stationSo = new SerializedObject(station);
            Wire(stationSo, "config", config);
            stationSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(station);

            return $"'{stationGo.name}' {(stationIsNew ? $"created at {anchorPos}" : "reused (position left as you set it)")}, " +
                   $"config '{BarVendorConfigPath}'{(wasNewlyCreatedConfig ? " (seeded with a 10-ticket Pint)" : "")}. " +
                   "If the anchor lands inside the counter or on the wrong side, drag Station_Bar in the " +
                   "Inspector — there is no art marker for 'the guest side' to measure from.";
        }

        // ---------- the blackjack table ----------

        private static string BuildBlackjackStation()
        {
            GameObject tableArt = GameObject.Find("Blackjack_Pit_Table");
            GameObject dealerArt = GameObject.Find("Dealer_Podium");
            if (tableArt == null || dealerArt == null)
                return "SKIPPED — needs both 'Blackjack_Pit_Table' and 'Dealer_Podium' in the scene to work out " +
                       $"which side is the player's side ({(tableArt == null ? "table" : "podium")} missing). " +
                       "Nothing else about the table was touched.";

            if (!TryGetWorldBounds(tableArt, out Bounds tableBounds))
                return "SKIPPED — 'Blackjack_Pit_Table' has no Renderer anywhere under it, so its bounds could " +
                       "not be measured. Nothing else about the table was touched.";

            float floorY = MeasureFloorHeight("Floor_Casino_RaisedPlatform", tableBounds.min.y);

            Vector3 awayFromDealer = tableBounds.center - dealerArt.transform.position;
            awayFromDealer.y = 0f;
            Vector3 seatDirection = awayFromDealer.sqrMagnitude > 0.0001f
                ? awayFromDealer.normalized
                : Vector3.forward; // degenerate (podium directly above/below the table) — pick something rather than NaN

            Vector3 seatPos = new Vector3(tableBounds.center.x, floorY + SeatFloorOffset, tableBounds.center.z)
                               + seatDirection * SeatDistanceFromTableCentre;
            Quaternion seatFacing = Quaternion.LookRotation(-seatDirection, Vector3.up); // face BACK toward the table

            bool wasNewlyCreatedConfig = AssetDatabase.LoadAssetAtPath<BlackjackConfig>(BlackjackConfigPath) == null;
            BlackjackConfig config = LoadOrCreateAsset<BlackjackConfig>(BlackjackConfigPath);
            // No first-creation seeding needed here, unlike the bar's item list
            // — BlackjackConfig's own class defaults (MinBet 10, MaxBet 100,
            // 6 decks, 3:2 blackjack) are already a playable table.

            GameObject stationGo = GameObject.Find("Station_Blackjack");
            bool stationIsNew = stationGo == null;
            if (stationIsNew)
            {
                stationGo = new GameObject("Station_Blackjack");
                TryParentUnderZone(stationGo, "WestWing_Zones");
            }
            // Position/rotation ARE re-applied every run, unlike the bar — this
            // station's transform IS the seat (see class comment on the single-
            // anchor design), so a stale seat facing the wrong way after an art
            // change is worse than losing a hand-nudge. Move it again by hand
            // after a re-run if you'd already adjusted it.
            stationGo.transform.SetPositionAndRotation(seatPos, seatFacing);

            BlackjackStation station = GetOrAdd<BlackjackStation>(stationGo);
            var stationSo = new SerializedObject(station);
            Wire(stationSo, "config", config);
            Wire(stationSo, "seat", stationGo.transform);
            stationSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(station);

            return $"'{stationGo.name}' at {seatPos} facing the table, config '{BlackjackConfigPath}' " +
                   $"(min bet {config.MinBet} tickets). Seat is a measured guess (opposite the dealer, " +
                   $"{SeatDistanceFromTableCentre:0.0}m from table centre) — nudge it in the Inspector if it " +
                   "clips the table or a stool.";
        }

        // ---------- helpers ----------

        /// <summary>
        /// Combines every Renderer under <paramref name="root"/> into one
        /// world-space Bounds. False if there are none — an empty anchor
        /// object with no mesh, which is a config mistake worth surfacing
        /// rather than silently placing something at the origin.
        /// </summary>
        private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        /// <summary>
        /// Top surface of a named floor object's bounds, or
        /// <paramref name="fallback"/> with a warning if it can't be found —
        /// same "measure, don't guess" rule ThunderLanesVenueBowlingSetupTool
        /// uses for Lane_Bed_5, but non-fatal here since a station can still be
        /// placed (just possibly floating or sunk) without it.
        /// </summary>
        private static float MeasureFloorHeight(string floorObjectName, float fallback)
        {
            GameObject floor = GameObject.Find(floorObjectName);
            if (floor != null && floor.TryGetComponent(out Renderer floorRenderer))
                return floorRenderer.bounds.max.y;

            Debug.LogWarning($"[ThunderLanesVenue] Could not find/measure '{floorObjectName}' for floor height — " +
                              "using the art bounds' own minimum Y instead, which may float or sink slightly.");
            return fallback;
        }

        private static void TryParentUnderZone(GameObject go, string zoneName)
        {
            GameObject zone = GameObject.Find(zoneName);
            if (zone != null) go.transform.SetParent(zone.transform, worldPositionStays: true);
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
