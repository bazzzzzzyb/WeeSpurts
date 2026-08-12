using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
        private const string SlotConfigPath = ProjectRoot + "/ScriptableObjects/CasinoSlotConfig.asset";
        private const string EconomyConfigPath = ProjectRoot + "/ScriptableObjects/EconomyConfig.asset";
        private const string AudioCatalogPath = ProjectRoot + "/ScriptableObjects/AudioCatalog.asset";

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

            // Runs whether GameManager was just created above or already
            // existed (e.g. a scene built by GreyboxSceneBuilder before this
            // tool ever touched it) — GetOrAdd rather than assuming AudioManager
            // is already there, and wiring is idempotent, so reruns never
            // duplicate or overwrite a Tony hand-edit.
            AudioManager audioManager = GetOrAdd<AudioManager>(gameManager.gameObject);
            WireAudioCatalog(audioManager);

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

            // ----- 2b. EventSystem — required for any uGUI Button/Slider to receive clicks at all -----
            string eventSystemReport = EnsureEventSystem();

            // ----- 3. The bar -----
            string barReport = BuildBarStation();

            // ----- 4. The blackjack table, now with a real Canvas HUD -----
            string blackjackReport = BuildBlackjackStation();

            // ----- 5. The slot machines -----
            string slotReport = BuildSlotStations();

            // ----- 6. Flush + report -----
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"[ThunderLanesVenue] Economy stations pass on scene '{scene.name}'. The scene is DIRTY and NOT " +
                "saved — look at it first, then Ctrl+S to keep it (Ctrl+Z / reopen to discard).\n\n" +
                $"GAME MANAGER: {(gameManagerIsNew ? "created" : "reused existing")}, EconomyConfig " +
                $"'{EconomyConfigPath}' wired (starting balance {economyConfig.StartingTickets} tickets).\n" +
                $"TICKET HUD: TicketBalanceHud on '{avatar.name}' — top-right corner during Play.\n" +
                $"EVENT SYSTEM: {eventSystemReport}\n" +
                $"BAR: {barReport}\n" +
                $"BLACKJACK: {blackjackReport}\n" +
                $"SLOTS: {slotReport}\n\n" +
                "NOW TEST: press Play. Walk to the bar and press [E] — your ticket count (top-right) should " +
                "drop by the drink's price. Walk to the blackjack table and press [E] — you'll sit down with " +
                "a real bet slider and Deal button (click/drag with the mouse — Hit/Stand/Double/Split are " +
                "buttons now, Esc still leaves). Walk to a slot machine and press [E] — drag the bet slider " +
                "and click Pull.");
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

            BuildBlackjackHud(stationGo, station);

            return $"'{stationGo.name}' at {seatPos} facing the table, config '{BlackjackConfigPath}' " +
                   $"(min bet {config.MinBet} tickets). Seat is a measured guess (opposite the dealer, " +
                   $"{SeatDistanceFromTableCentre:0.0}m from table centre) — nudge it in the Inspector if it " +
                   "clips the table or a stool. Canvas HUD built/wired (bet slider, Deal/Hit/Stand/Double/" +
                   "Split/Leave).";
        }

        /// <summary>
        /// Builds/wires the real Canvas UI on <paramref name="stationGo"/> —
        /// bet slider, Deal/Hit/Stand/Double/Split/Leave buttons, dealer +
        /// player hand containers, outcome text. Card visuals themselves are
        /// NOT built here — they vary in count (hits, splits), so
        /// <see cref="BlackjackHud"/> builds those at runtime. This is the
        /// static skeleton only, same idempotent get-or-create discipline as
        /// every station above: safe to re-run, and any Tony hand-restyle
        /// (colours, sizes, positions) survives a re-run because nothing here
        /// overwrites an existing child, only fills in what's missing.
        /// </summary>
        private static void BuildBlackjackHud(GameObject stationGo, BlackjackStation station)
        {
            GameObject canvasGo = EnsureScreenSpaceCanvas(stationGo.transform, "BlackjackCanvas");
            GameObject contentRoot = GetOrCreateUIChild(canvasGo.transform, "Content");
            SetStretch(contentRoot.GetComponent<RectTransform>());
            contentRoot.SetActive(false);
            DestroyStaleChild(contentRoot.transform, "Panel"); // pre-TableArea/ControlBar layout — see that method's comment

            // TWO docked panels, not one centred box — "gamble with friends"
            // reference: a mostly see-through view of the table up top (so
            // the actual 3D table/cards read as the focus, not a UI box) and
            // a solid, rounded control bar at the very bottom, like a
            // wristband/controller in front of the seat. See EnsureDockedPanel.
            GameObject tableArea = EnsureDockedPanel(contentRoot.transform, "TableArea",
                new Color(0.02f, 0.02f, 0.04f, 0.4f), new Vector2(1000f, 380f), PanelDock.Top, 120f, rounded: true);
            var tableLayout = EnsureComponent<VerticalLayoutGroup>(tableArea);
            SetUpVerticalStack(tableLayout, TextAnchor.UpperCenter);
            tableLayout.spacing = 10f;
            tableLayout.padding = new RectOffset(20, 20, 16, 16);
            tableLayout.childForceExpandHeight = false;
            tableLayout.childForceExpandWidth = true;

            GameObject controlBar = EnsureDockedPanel(contentRoot.transform, "ControlBar",
                new Color(0.07f, 0.07f, 0.1f, 0.95f), new Vector2(1000f, 170f), PanelDock.Bottom, 24f, rounded: true);
            var controlLayout = EnsureComponent<VerticalLayoutGroup>(controlBar);
            SetUpVerticalStack(controlLayout, TextAnchor.MiddleCenter);
            controlLayout.spacing = 10f;
            controlLayout.padding = new RectOffset(20, 20, 14, 14);
            controlLayout.childForceExpandHeight = false;
            controlLayout.childForceExpandWidth = true;

            // ----- Table area: dealer row, player hand(s), outcome -----
            GameObject dealerRow = EnsureRow(tableArea.transform, "DealerRow");
            EnsureLabel(dealerRow.transform, "Label", "DEALER", 22f, FontStyles.Bold);
            GameObject dealerHandGo = EnsureHandContainer(dealerRow.transform, "DealerHand");
            TMP_Text dealerTotal = EnsureLabel(dealerRow.transform, "Total", "", 24f, FontStyles.Bold);

            GameObject handsRow = EnsureRow(tableArea.transform, "PlayerHandsRow");

            GameObject hand0Group = GetOrCreateUIChild(handsRow.transform, "Hand0Group");
            SetUpVerticalStack(EnsureComponent<VerticalLayoutGroup>(hand0Group), TextAnchor.UpperCenter);
            EnsureLabel(hand0Group.transform, "Label", "YOU", 20f, FontStyles.Bold);
            GameObject hand0HandGo = EnsureHandContainer(hand0Group.transform, "Hand");
            TMP_Text hand0Total = EnsureLabel(hand0Group.transform, "Total", "", 22f, FontStyles.Bold);

            GameObject hand1Group = GetOrCreateUIChild(handsRow.transform, "Hand1Group");
            SetUpVerticalStack(EnsureComponent<VerticalLayoutGroup>(hand1Group), TextAnchor.UpperCenter);
            EnsureLabel(hand1Group.transform, "Label", "HAND 2", 20f, FontStyles.Bold);
            GameObject hand1HandGo = EnsureHandContainer(hand1Group.transform, "Hand");
            TMP_Text hand1Total = EnsureLabel(hand1Group.transform, "Total", "", 22f, FontStyles.Bold);

            // Rebuild THESE TWO explicitly, bottom-up, before anything above
            // them (PlayerHandsRow, TableArea, Content) ever tries to read
            // their size. Verified directly against the saved scene that the
            // later whole-hierarchy rebuild (see ForceLayoutRebuild, called
            // once at the very end) does NOT reliably apply a computed size
            // this many LayoutGroup levels deep (Content > TableArea >
            // PlayerHandsRow > Hand0Group is four) in a single pass, even
            // repeated — PlayerHandsRow itself came out correctly sized
            // (it could correctly QUERY Hand0Group's reported preferred
            // size), but Hand0Group's own RectTransform never got that size
            // APPLIED back down to it. Rebuilding the deepest groups first
            // sidesteps the question of why by only ever asking one level to
            // resolve at a time.
            ForceLayoutRebuild(hand0Group.GetComponent<RectTransform>());
            ForceLayoutRebuild(hand1Group.GetComponent<RectTransform>());
            hand1Group.SetActive(false);

            TMP_Text outcomeText = EnsureLabel(tableArea.transform, "OutcomeText", "", 24f, FontStyles.Bold);
            // EnsureLabel's 200px default width fits a short "DEALER"/"YOU"
            // tag but not a two-hand outcome line ("HAND 1: WIN (+40)   HAND
            // 2: BUST (-20)") — widen just this one.
            outcomeText.GetComponent<LayoutElement>().preferredWidth = 900f;

            // ----- Control bar: bet row (pre-deal / post-settle) -----
            GameObject betRow = EnsureRow(controlBar.transform, "BetRow");
            Slider betSlider = EnsureSlider(betRow.transform, "BetSlider", new Vector2(360f, 30f));
            TMP_Text betValueText = EnsureLabel(betRow.transform, "BetValueText", "BET: 0", 22f, FontStyles.Normal);
            Button dealButton = EnsureButton(betRow.transform, "DealButton", "DEAL", new Vector2(160f, 48f));
            TMP_Text dealButtonLabel = dealButton.GetComponentInChildren<TMP_Text>();

            // ----- Control bar: actions (PlayerTurn only) -----
            GameObject actionGroup = EnsureRow(controlBar.transform, "ActionGroup");
            Button hitButton = EnsureButton(actionGroup.transform, "HitButton", "HIT", new Vector2(140f, 48f));
            Button standButton = EnsureButton(actionGroup.transform, "StandButton", "STAND", new Vector2(140f, 48f));
            Button doubleButton = EnsureButton(actionGroup.transform, "DoubleButton", "DOUBLE", new Vector2(140f, 48f));
            Button splitButton = EnsureButton(actionGroup.transform, "SplitButton", "SPLIT", new Vector2(140f, 48f));
            // Starts hidden — matches the table's own starting phase (AwaitingBet,
            // not PlayerTurn) AND matters for layout: with BOTH this and BetRow
            // active at once (their natural edit-time state, before
            // BlackjackHud.Update ever runs), controlLayout's VerticalLayoutGroup
            // sums both into the bar's required height instead of the one
            // that's actually showing at any given moment.
            actionGroup.SetActive(false);

            Button leaveButton = EnsureButton(controlBar.transform, "LeaveButton", "LEAVE TABLE", new Vector2(200f, 36f), LeaveButtonColor);

            BlackjackHud hud = GetOrAdd<BlackjackHud>(canvasGo);
            var hudSo = new SerializedObject(hud);
            Wire(hudSo, "station", station);
            Wire(hudSo, "contentRoot", contentRoot);
            Wire(hudSo, "betSlider", betSlider);
            Wire(hudSo, "betValueText", betValueText);
            Wire(hudSo, "dealButton", dealButton);
            Wire(hudSo, "dealButtonLabel", dealButtonLabel);
            Wire(hudSo, "actionGroup", actionGroup);
            Wire(hudSo, "hitButton", hitButton);
            Wire(hudSo, "standButton", standButton);
            Wire(hudSo, "doubleButton", doubleButton);
            Wire(hudSo, "splitButton", splitButton);
            Wire(hudSo, "leaveButton", leaveButton);
            Wire(hudSo, "dealerHandContainer", dealerHandGo.GetComponent<RectTransform>());
            Wire(hudSo, "dealerTotalText", dealerTotal);
            Wire(hudSo, "playerHand0Container", hand0HandGo.GetComponent<RectTransform>());
            Wire(hudSo, "playerHand0TotalText", hand0Total);
            Wire(hudSo, "hand1Group", hand1Group);
            Wire(hudSo, "playerHand1Container", hand1HandGo.GetComponent<RectTransform>());
            Wire(hudSo, "playerHand1TotalText", hand1Total);
            Wire(hudSo, "outcomeText", outcomeText);
            hudSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            ForceLayoutRebuild(contentRoot.GetComponent<RectTransform>());
        }

        // ---------- the slot machines ----------

        /// <summary>
        /// One <see cref="SlotStation"/> per SlotMachine_N art object already
        /// in the venue (SlotMachine_3/4/5 — confirmed by reading the scene),
        /// each seated at the nearest not-yet-claimed Casino_Stool. Same
        /// measure-don't-guess, skip-with-a-warning-not-a-guess discipline as
        /// <see cref="BuildBlackjackStation"/>. A shared <see cref="SlotConfig"/>
        /// asset (create-once) with a PER-MACHINE SEED OFFSET, so the three
        /// machines don't roll identical reel sequences.
        /// </summary>
        private static string BuildSlotStations()
        {
            var machineNames = new[] { "SlotMachine_3", "SlotMachine_4", "SlotMachine_5" };

            bool wasNewlyCreatedConfig = AssetDatabase.LoadAssetAtPath<SlotConfig>(SlotConfigPath) == null;
            SlotConfig config = LoadOrCreateAsset<SlotConfig>(SlotConfigPath);
            // No first-creation seeding needed — SlotConfig's own class defaults
            // (24-slot strips, 3:2:1 pin/beer/etc paytable, FREE FRAME on
            // triple pin) are already a playable machine, same reasoning as
            // BlackjackConfig's defaults above.

            var claimedStools = new HashSet<GameObject>();
            var reports = new List<string>();
            int builtCount = 0;

            for (int i = 0; i < machineNames.Length; i++)
            {
                string machineName = machineNames[i];
                GameObject machineArt = GameObject.Find(machineName);
                if (machineArt == null)
                {
                    reports.Add($"'{machineName}' SKIPPED — not found in the scene.");
                    continue;
                }

                if (!TryGetWorldBounds(machineArt, out Bounds machineBounds))
                {
                    reports.Add($"'{machineName}' SKIPPED — no Renderer anywhere under it to measure.");
                    continue;
                }

                GameObject nearestStool = FindNearestUnclaimedStool(machineBounds.center, claimedStools);
                Vector3 seatPos;
                Quaternion seatFacing;
                string seatNote;

                if (nearestStool != null)
                {
                    claimedStools.Add(nearestStool);
                    seatPos = nearestStool.transform.position;
                    Vector3 toMachine = machineBounds.center - seatPos;
                    toMachine.y = 0f;
                    seatFacing = toMachine.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(toMachine.normalized, Vector3.up)
                        : Quaternion.identity;
                    seatNote = $"seated at '{nearestStool.name}'";
                }
                else
                {
                    // No stool free — stand-in front of the machine rather than
                    // guessing a stool that isn't there. Same "skip/warn, never
                    // guess silently" rule as the bar/blackjack anchors, except
                    // here the STATION still gets built (a slot machine is
                    // usable standing up in real life) with a loud note instead.
                    Vector3 outward = new Vector3(0f, 0f, 1f);
                    seatPos = machineBounds.center + outward * 0.9f;
                    seatFacing = Quaternion.LookRotation(-outward, Vector3.up);
                    seatNote = "NO UNCLAIMED Casino_Stool FOUND — placed standing in front of the machine, nudge by hand";
                }

                string stationName = $"Station_Slot_{i}";
                GameObject stationGo = GameObject.Find(stationName);
                bool stationIsNew = stationGo == null;
                if (stationIsNew)
                {
                    stationGo = new GameObject(stationName);
                    TryParentUnderZone(stationGo, "WestWing_Zones");
                }
                stationGo.transform.SetPositionAndRotation(seatPos, seatFacing);

                SlotStation station = GetOrAdd<SlotStation>(stationGo);
                var stationSo = new SerializedObject(station);
                Wire(stationSo, "config", config);
                Wire(stationSo, "seat", stationGo.transform);
                SerializedProperty seedProperty = stationSo.FindProperty("seed");
                // Only stamp the seed on first creation — a re-run must not
                // reroll a machine Tony has already been tuning against.
                if (stationIsNew && seedProperty != null) seedProperty.intValue = 20260812 + i;
                stationSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(station);

                BuildSlotHud(stationGo, station);

                reports.Add($"'{stationName}' at '{machineName}' ({seatNote}).");
                builtCount++;
            }

            if (builtCount > 0) AssetDatabase.SaveAssets();

            string configNote = wasNewlyCreatedConfig ? $" config '{SlotConfigPath}' (min stake {config.MinStake} tickets)." : "";
            return $"{builtCount}/{machineNames.Length} machines got a station.{configNote} " + string.Join(" ", reports);
        }

        private static GameObject FindNearestUnclaimedStool(Vector3 point, HashSet<GameObject> claimed)
        {
            GameObject[] stools = FindAllNamed("Casino_Stool");

            GameObject nearest = null;
            float nearestSqrDist = float.MaxValue;
            foreach (GameObject stool in stools)
            {
                if (claimed.Contains(stool)) continue;
                float sqrDist = (stool.transform.position - point).sqrMagnitude;
                if (sqrDist < nearestSqrDist) { nearestSqrDist = sqrDist; nearest = stool; }
            }
            return nearest;
        }

        /// <summary>Every active scene object named exactly <paramref name="name"/> — GameObject.Find only ever returns one, and this venue has several Casino_Stools.</summary>
        private static GameObject[] FindAllNamed(string name)
        {
            var result = new List<GameObject>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == name) result.Add(t.gameObject);
            return result.ToArray();
        }

        /// <summary>Same shape as <see cref="BuildBlackjackHud"/> — bet slider, Pull button, three reel slots, bonus banner. See that method's class comment for the idempotency discipline.</summary>
        private static void BuildSlotHud(GameObject stationGo, SlotStation station)
        {
            GameObject canvasGo = EnsureScreenSpaceCanvas(stationGo.transform, "SlotCanvas");
            GameObject contentRoot = GetOrCreateUIChild(canvasGo.transform, "Content");
            SetStretch(contentRoot.GetComponent<RectTransform>());
            contentRoot.SetActive(false);
            DestroyStaleChild(contentRoot.transform, "Panel"); // pre-TableArea/ControlBar layout — see that method's comment

            // Same TableArea/ControlBar split as BuildBlackjackHud — see that
            // method's comment.
            GameObject tableArea = EnsureDockedPanel(contentRoot.transform, "TableArea",
                new Color(0.02f, 0.02f, 0.04f, 0.4f), new Vector2(700f, 230f), PanelDock.Top, 120f, rounded: true);
            var tableLayout = EnsureComponent<VerticalLayoutGroup>(tableArea);
            SetUpVerticalStack(tableLayout, TextAnchor.UpperCenter);
            tableLayout.spacing = 10f;
            tableLayout.padding = new RectOffset(20, 20, 16, 16);
            tableLayout.childForceExpandHeight = false;
            tableLayout.childForceExpandWidth = true;

            GameObject controlBar = EnsureDockedPanel(contentRoot.transform, "ControlBar",
                new Color(0.07f, 0.07f, 0.1f, 0.95f), new Vector2(700f, 150f), PanelDock.Bottom, 24f, rounded: true);
            var controlLayout = EnsureComponent<VerticalLayoutGroup>(controlBar);
            SetUpVerticalStack(controlLayout, TextAnchor.MiddleCenter);
            controlLayout.spacing = 10f;
            controlLayout.padding = new RectOffset(20, 20, 14, 14);
            controlLayout.childForceExpandHeight = false;
            controlLayout.childForceExpandWidth = true;

            GameObject bonusBanner = EnsureLabel(tableArea.transform, "BonusBanner", "FREE FRAME!", 26f, FontStyles.Bold).gameObject;
            TMP_Text bonusText = bonusBanner.GetComponent<TMP_Text>();
            // "FREE FRAME!  N pulls left   xM" doesn't fit EnsureLabel's 200px default.
            bonusBanner.GetComponent<LayoutElement>().preferredWidth = 600f;
            bonusBanner.SetActive(false);

            GameObject reelsRow = EnsureRow(tableArea.transform, "ReelsRow");
            (Image reel0Bg, TMP_Text reel0Text) = EnsureReelSlot(reelsRow.transform, "Reel0");
            (Image reel1Bg, TMP_Text reel1Text) = EnsureReelSlot(reelsRow.transform, "Reel1");
            (Image reel2Bg, TMP_Text reel2Text) = EnsureReelSlot(reelsRow.transform, "Reel2");

            TMP_Text resultText = EnsureLabel(tableArea.transform, "ResultText", "", 24f, FontStyles.Bold);

            GameObject betRow = EnsureRow(controlBar.transform, "BetRow");
            Slider betSlider = EnsureSlider(betRow.transform, "BetSlider", new Vector2(300f, 30f));
            TMP_Text betValueText = EnsureLabel(betRow.transform, "BetValueText", "BET: 0", 22f, FontStyles.Normal);

            Button pullButton = EnsureButton(controlBar.transform, "PullButton", "PULL", new Vector2(220f, 52f));
            TMP_Text pullButtonLabel = pullButton.GetComponentInChildren<TMP_Text>();
            Button leaveButton = EnsureButton(controlBar.transform, "LeaveButton", "LEAVE MACHINE", new Vector2(200f, 36f), LeaveButtonColor);

            SlotHud hud = GetOrAdd<SlotHud>(canvasGo);
            var hudSo = new SerializedObject(hud);
            Wire(hudSo, "station", station);
            Wire(hudSo, "contentRoot", contentRoot);
            Wire(hudSo, "betSlider", betSlider);
            Wire(hudSo, "betValueText", betValueText);
            Wire(hudSo, "pullButton", pullButton);
            Wire(hudSo, "pullButtonLabel", pullButtonLabel);
            Wire(hudSo, "resultText", resultText);
            Wire(hudSo, "leaveButton", leaveButton);
            Wire(hudSo, "reel0Background", reel0Bg);
            Wire(hudSo, "reel0Text", reel0Text);
            Wire(hudSo, "reel1Background", reel1Bg);
            Wire(hudSo, "reel1Text", reel1Text);
            Wire(hudSo, "reel2Background", reel2Bg);
            Wire(hudSo, "reel2Text", reel2Text);
            Wire(hudSo, "bonusBanner", bonusBanner);
            Wire(hudSo, "bonusText", bonusText);
            hudSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            ForceLayoutRebuild(contentRoot.GetComponent<RectTransform>());
        }

        private static (Image, TMP_Text) EnsureReelSlot(Transform parent, string name)
        {
            GameObject go = GetOrCreateUIChild(parent, name);
            RectTransform rect = go.GetComponent<RectTransform>();
            var size = new Vector2(160f, 120f);
            rect.sizeDelta = size;

            Image bg = EnsureComponent<Image>(go);
            bg.color = new Color(0.9f, 0.9f, 0.9f); // overwritten every pull by SlotHud.SetReelSymbol anyway

            LayoutElement layoutElement = EnsureComponent<LayoutElement>(go);
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;

            TMP_Text text = EnsureLabel(rect, "Label", "?", 22f, FontStyles.Bold);
            text.color = Color.black;
            SetStretch((RectTransform)text.transform);
            LayoutElement labelLayout = text.GetComponent<LayoutElement>();
            if (labelLayout != null) labelLayout.ignoreLayout = true;

            return (bg, text);
        }

        // ---------- shared uGUI construction helpers ----------
        // Every element below is GET-OR-CREATE by child name, same idempotency
        // rule as GetOrAdd<T>: a re-run fills in whatever's missing and never
        // duplicates or resets what Tony has already hand-restyled. All of
        // this builds real GameObjects in the Hierarchy (never hand-edited
        // scene YAML) — see CLAUDE.md.

        /// <summary>Get-or-create a Screen Space - Overlay Canvas as a child of <paramref name="parent"/> — one per station, so only the seated player's own station's Canvas is ever active.</summary>
        private static GameObject EnsureScreenSpaceCanvas(Transform parent, string name)
        {
            GameObject go = GetOrCreateUIChild(parent, name);
            Canvas canvas = EnsureComponent<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = EnsureComponent<CanvasScaler>(go);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureComponent<GraphicRaycaster>(go);
            return go;
        }

        /// <summary>Get-or-create an EventSystem — without one, no uGUI Button/Slider in the scene receives any input at all. StandaloneInputModule (not the new Input System's UI module) because ProjectSettings' activeInputHandler is 2 ("Both"), same as every other Input.* call in this project.</summary>
        private static string EnsureEventSystem()
        {
            GameObject existing = GameObject.Find("EventSystem");
            bool isNew = existing == null;
            GameObject go = existing != null ? existing : new GameObject("EventSystem");

            EnsureComponent<EventSystem>(go);
            EnsureComponent<StandaloneInputModule>(go);

            return isNew ? "created" : "reused existing";
        }

        /// <summary>
        /// Removes a named child if one exists — the deliberate exception to
        /// this file's usual "never delete, only get-or-create" rule. Exists
        /// because the blackjack/slot HUD layout was restructured mid-session
        /// (one centred "Panel" → a docked TableArea + ControlBar): the OLD
        /// name's GameObjects don't get touched by ANY of the code below
        /// anymore, so without this they sit in the scene forever as
        /// orphaned, un-rebuilt, invisible-until-you-look dead weight — found
        /// by hand while chasing why one nested group's computed size never
        /// seemed to land: TWO "Hand0Group"s existed, an abandoned one still
        /// reporting whatever it last had (childControlWidth still false,
        /// sizeDelta still Unity's raw 100x100 default) and a live one this
        /// file actually maintains, and a same-named grep search has no way
        /// to tell them apart. If the orphan were still active it would
        /// ALSO render, doubled up with the real HUD.
        /// </summary>
        private static void DestroyStaleChild(Transform parent, string name)
        {
            Transform stale = parent.Find(name);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);
        }

        private static GameObject GetOrCreateUIChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Runs Unity's layout system once, synchronously, right now — the
        /// ONLY way a batchmode editor script (never renders a frame, so the
        /// normal live layout rebuild that a Canvas triggers on its own
        /// never fires) ends up with correct, USABLE sizeDelta/anchoredPosition
        /// values baked into the saved scene instead of every LayoutGroup-
        /// controlled RectTransform sitting at whatever raw default it had
        /// when created. Also a real player-facing win, not just a verification
        /// convenience: without this, the very first frame after Play begins
        /// would show everything at its wrong pre-rebuild size for one frame
        /// before Unity's own layout pass corrects it — a visible "pop".
        /// Temporarily reactivates <paramref name="root"/> if it starts
        /// inactive (Content does, until a player sits down) because Unity's
        /// layout system skips inactive hierarchies. Runs SEVERAL passes, not
        /// one: verified directly against the saved scene that a single
        /// <c>ForceRebuildLayoutImmediate</c> call does not converge for
        /// deeply nested groups in one shot (Content → TableArea →
        /// PlayerHandsRow → Hand0Group is four levels — DealerRow, three
        /// levels down, resolved correctly after one pass; Hand0Group, one
        /// level deeper, was still sitting at RectTransform's raw 100x100
        /// default) — each pass can only apply sizes computed from the PREVIOUS
        /// pass's children, so a group whose own preferred size depends on a
        /// grandchild needs that grandchild settled first. A handful of
        /// passes is cheap and converges any hierarchy this project is
        /// likely to build.
        /// </summary>
        private static void ForceLayoutRebuild(RectTransform root)
        {
            if (root == null) return;
            bool wasActive = root.gameObject.activeSelf;
            if (!wasActive) root.gameObject.SetActive(true);

            for (int pass = 0; pass < 4; pass++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            }

            if (!wasActive) root.gameObject.SetActive(false);
        }

        private enum PanelDock { Top, Bottom }

        /// <summary>
        /// Unity's own built-in UI sprites — the exact same ones
        /// "GameObject > UI > Button" uses (see <c>UnityEngine.UI.DefaultControls</c>),
        /// so a rounded, 9-sliced look costs nothing: no art asset, no import
        /// settings to get right, just an editor-only lookup.
        /// </summary>
        private static Sprite GetBuiltinSprite(string resourcePath) =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>(resourcePath);

        /// <summary>
        /// A fixed-size panel docked to the top or bottom edge of its parent,
        /// with a fixed margin off that edge. Splitting the HUD into a
        /// (mostly see-through) TOP "table area" for cards/totals and a
        /// solid BOTTOM "control bar" for buttons — rather than one big
        /// centred box — is what stops the UI from just sitting over the
        /// middle of the screen hiding the table. <paramref name="rounded"/>
        /// applies the built-in rounded-corner sprite (see
        /// <see cref="GetBuiltinSprite"/>) instead of a flat rectangle.
        /// </summary>
        private static GameObject EnsureDockedPanel(Transform parent, string name, Color color, Vector2 size,
                                                     PanelDock dock, float edgeMargin, bool rounded)
        {
            GameObject go = GetOrCreateUIChild(parent, name);
            RectTransform rect = go.GetComponent<RectTransform>();
            float anchorY = dock == PanelDock.Bottom ? 0f : 1f;
            rect.anchorMin = new Vector2(0.5f, anchorY);
            rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.pivot = new Vector2(0.5f, anchorY);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, dock == PanelDock.Bottom ? edgeMargin : -edgeMargin);

            Image img = EnsureComponent<Image>(go);
            img.color = color;
            if (rounded)
            {
                img.sprite = GetBuiltinSprite("UI/Skin/Background.psd");
                img.type = Image.Type.Sliced;
            }
            return go;
        }

        /// <summary>
        /// The childControlWidth/Height=true default every VerticalLayoutGroup
        /// in this file needs (see <see cref="EnsureRow"/>'s comment on why
        /// that isn't actually the framework default) — factored out because
        /// it's set on four different vertical stacks (TableArea, ControlBar,
        /// Hand0Group, Hand1Group) and repeating the same two lines with the
        /// same justification four times is worse than naming it once.
        /// ALSO forces childForceExpandWidth/Height to false — the OTHER
        /// framework default that bit this file: a fresh VerticalLayoutGroup
        /// defaults BOTH to true, which would stretch every child (Label,
        /// the card container, Total) to fill this stack's full assigned
        /// width/height rather than each keeping its own natural size —
        /// confirmed on Hand0Group, which came out 960 wide (matching its
        /// row's full content width) instead of a narrow column, before this
        /// was added. Callers that DO want force-expand (TableArea/ControlBar
        /// want their rows to stretch full width) set it explicitly right
        /// after calling this, which simply overrides the false set here.
        /// </summary>
        private static void SetUpVerticalStack(VerticalLayoutGroup layout, TextAnchor alignment)
        {
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static GameObject EnsureRow(Transform parent, string name)
        {
            GameObject go = GetOrCreateUIChild(parent, name);
            HorizontalLayoutGroup layout = EnsureComponent<HorizontalLayoutGroup>(go);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // EXPLICIT, not left at whatever AddComponent defaults to —
            // confirmed by reading the actual saved scene YAML that a freshly
            // added HorizontalLayoutGroup's childControlWidth/Height come in
            // FALSE, not true (the opposite of what an earlier version of
            // this comment assumed). False here would leave every child that
            // doesn't set its own sizeDelta directly (EnsureLabel's text,
            // this row's own child rows) stuck at a fresh RectTransform's
            // actual default — {x: 100, y: 100}, also confirmed against the
            // scene, not zero — instead of the size its LayoutElement asks
            // for. True makes this row apply each child's LayoutElement.
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // NO ContentSizeFitter here. HorizontalLayoutGroup already
            // implements ILayoutElement itself and reports a preferred size
            // computed from this row's own children up to its parent's
            // VerticalLayoutGroup (true regardless of childControlWidth/
            // Height above — that flag is about children, this is about
            // what THIS row reports to ITS OWN parent). Adding a
            // ContentSizeFitter on the SAME RectTransform makes two
            // different layout controllers (the fitter, self-controlling;
            // the parent group, externally controlling) fight over the same
            // axis in the same rebuild pass, which is an undefined-order
            // Unity uGUI footgun and is what collapsed every row — and every
            // button/slider inside it — to zero height. (First shipped with
            // it; Tony reported "a little menu pops up but no game commands
            // visible" and this was the fix.)
            if (go.TryGetComponent(out ContentSizeFitter staleFitter)) Object.DestroyImmediate(staleFitter);

            return go;
        }

        /// <summary>The container a hand's card visuals get built into at runtime. An Image (initially clear) so BlackjackHud can tint it to highlight the active hand.</summary>
        private static GameObject EnsureHandContainer(Transform parent, string name)
        {
            GameObject go = GetOrCreateUIChild(parent, name);
            RectTransform rect = go.GetComponent<RectTransform>();
            var size = new Vector2(320f, 90f);
            rect.sizeDelta = size;

            Image bg = EnsureComponent<Image>(go);
            bg.color = Color.clear;

            LayoutElement layoutElement = EnsureComponent<LayoutElement>(go);
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;

            HorizontalLayoutGroup layout = EnsureComponent<HorizontalLayoutGroup>(go);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // Card visuals are built at RUNTIME (BlackjackHud.CreateCardVisual)
            // with no LayoutElement of their own — they set their own
            // sizeDelta directly. Without this, HorizontalLayoutGroup's
            // default childControlWidth/Height (true) would drive their size
            // from a (missing) ILayoutElement and collapse every card to 0x0.
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            return go;
        }

        private static TMP_Text EnsureLabel(Transform parent, string name, string defaultText, float fontSize, FontStyles style)
        {
            GameObject go = GetOrCreateUIChild(parent, name);
            TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(go);
            if (string.IsNullOrEmpty(text.text)) text.text = defaultText; // only seed on first creation
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            LayoutElement layoutElement = EnsureComponent<LayoutElement>(go);
            layoutElement.preferredWidth = 200f;
            layoutElement.preferredHeight = fontSize + 12f;

            return text;
        }

        /// <summary>Standard "primary action" green — Deal/Hit/Stand/Double/Split/Pull. A distinct colour for Leave (<see cref="LeaveButtonColor"/>) is what actually keeps a five-button row from reading as five equally-weighted choices.</summary>
        private static readonly Color PrimaryButtonColor = new Color(0.2f, 0.55f, 0.28f);
        private static readonly Color LeaveButtonColor = new Color(0.45f, 0.2f, 0.2f);

        private static Button EnsureButton(Transform parent, string name, string label, Vector2 size, Color? bgColor = null)
        {
            GameObject go = GetOrCreateUIChild(parent, name);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image bg = EnsureComponent<Image>(go);
            bg.color = bgColor ?? PrimaryButtonColor;
            bg.sprite = GetBuiltinSprite("UI/Skin/UISprite.psd");
            bg.type = Image.Type.Sliced;

            Button button = EnsureComponent<Button>(go);
            button.targetGraphic = bg;

            LayoutElement layoutElement = EnsureComponent<LayoutElement>(go);
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;

            TMP_Text labelText = EnsureLabel(go.transform, "Label", label, 20f, FontStyles.Bold);
            SetStretch((RectTransform)labelText.transform);
            labelText.color = Color.white;
            // The label's own LayoutElement (added by EnsureLabel) would fight
            // the stretch above inside a button this small — neutralise it.
            LayoutElement labelLayout = labelText.GetComponent<LayoutElement>();
            if (labelLayout != null) labelLayout.ignoreLayout = true;

            return button;
        }

        private static Slider EnsureSlider(Transform parent, string name, Vector2 size)
        {
            GameObject root = GetOrCreateUIChild(parent, name);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = size;

            Image rootBg = EnsureComponent<Image>(root);
            rootBg.color = new Color(0.15f, 0.15f, 0.18f);
            rootBg.sprite = GetBuiltinSprite("UI/Skin/InputFieldBackground.psd");
            rootBg.type = Image.Type.Sliced;

            LayoutElement layoutElement = EnsureComponent<LayoutElement>(root);
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;

            GameObject fillAreaGo = GetOrCreateUIChild(root.transform, "FillArea");
            RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            SetStretch(fillAreaRect);

            GameObject fillGo = GetOrCreateUIChild(fillAreaRect, "Fill");
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            SetStretch(fillRect);
            Image fillImg = EnsureComponent<Image>(fillGo);
            fillImg.color = new Color(0.9f, 0.75f, 0.2f);
            fillImg.sprite = GetBuiltinSprite("UI/Skin/UISprite.psd");
            fillImg.type = Image.Type.Sliced;

            GameObject handleAreaGo = GetOrCreateUIChild(root.transform, "HandleSlideArea");
            RectTransform handleAreaRect = handleAreaGo.GetComponent<RectTransform>();
            SetStretch(handleAreaRect);

            GameObject handleGo = GetOrCreateUIChild(handleAreaRect, "Handle");
            RectTransform handleRect = handleGo.GetComponent<RectTransform>();
            // A round knob, not a stretched bar — matches Unity's own default
            // Slider handle shape (anchored at vertical-centre, fixed square
            // size) so the Knob sprite reads as a circle instead of a smear.
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(24f, 24f);
            Image handleImg = EnsureComponent<Image>(handleGo);
            handleImg.color = Color.white;
            handleImg.sprite = GetBuiltinSprite("UI/Skin/Knob.psd");

            Slider slider = EnsureComponent<Slider>(root);
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;

            return slider;
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

        /// <summary>
        /// Loads (or creates) the one shared AudioCatalog asset, seeds it with
        /// every id in SoundId.All (additive-only — never touches a row a
        /// Tony hand-edit already filled in), and wires it onto the given
        /// AudioManager if it isn't wired already. Same idempotent "only-if-
        /// null" discipline as economyConfig's wiring above.
        /// </summary>
        private static void WireAudioCatalog(AudioManager audioManager)
        {
            AudioCatalog catalog = LoadOrCreateAsset<AudioCatalog>(AudioCatalogPath);
            catalog.EnsureIds(SoundId.All);
            EditorUtility.SetDirty(catalog);

            var so = new SerializedObject(audioManager);
            SerializedProperty catalogProperty = so.FindProperty("catalog");
            if (catalogProperty != null && catalogProperty.objectReferenceValue == null)
                catalogProperty.objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioManager);
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
