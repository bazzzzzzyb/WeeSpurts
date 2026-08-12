using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeeSpurts.Slop;

namespace WeeSpurts.UI
{
    /// <summary>
    /// The blackjack table's real UI — replaces the old OnGUI debug box and
    /// Space/Enter keys. A Canvas child of a <see cref="BlackjackStation"/>,
    /// built and wired by <c>ThunderLanesVenueStationSetupTool</c> the same
    /// way every other station is (get-or-add, [SerializeField]-everything —
    /// see <see cref="Slop.VenueStation"/>'s class comment on why: an editor
    /// tool wires these fields, not Play-mode code).
    ///
    /// THIS CLASS OWNS PRESENTATION ONLY. Every click funnels straight
    /// through to <see cref="BlackjackStation"/>'s public methods, which are
    /// the only thing that ever touches <see cref="BlackjackTable"/> or the
    /// ledger — this HUD never does either directly.
    ///
    /// CARDS ARE BUILT AT RUNTIME, NOT BY THE EDITOR TOOL: a hand's card count
    /// varies (hits) and hand count varies (a split adds a second), so fixed
    /// pre-built slots don't fit. <see cref="CreateCardVisual"/> is a small
    /// procedural panel+text, same "build the widget's own visuals in code"
    /// move <see cref="SpinSelectorHud"/> already uses for its spin dial — no
    /// card-art dependency, easy to swap for a real texture atlas later.
    /// </summary>
    public class BlackjackHud : MonoBehaviour
    {
        [Header("Wiring (set by the setup tool)")]
        [SerializeField] private BlackjackStation station;
        [SerializeField] private GameObject contentRoot;

        [Header("Betting")]
        [SerializeField] private Slider betSlider;
        [SerializeField] private TMP_Text betValueText;
        [SerializeField] private Button dealButton;
        [SerializeField] private TMP_Text dealButtonLabel;

        [Header("Actions")]
        [SerializeField] private GameObject actionGroup;
        [SerializeField] private Button hitButton;
        [SerializeField] private Button standButton;
        [SerializeField] private Button doubleButton;
        [SerializeField] private Button splitButton;
        [SerializeField] private Button leaveButton;

        [Header("Hands")]
        [SerializeField] private RectTransform dealerHandContainer;
        [SerializeField] private TMP_Text dealerTotalText;
        [SerializeField] private RectTransform playerHand0Container;
        [SerializeField] private TMP_Text playerHand0TotalText;
        [SerializeField] private GameObject hand1Group;
        [SerializeField] private RectTransform playerHand1Container;
        [SerializeField] private TMP_Text playerHand1TotalText;

        [Header("Outcome")]
        [SerializeField] private TMP_Text outcomeText;

        [Header("Card look (Tony-tunable)")]
        [SerializeField] private Vector2 cardSize = new Vector2(56f, 78f);
        [SerializeField] private Color cardFaceColor = new Color(0.97f, 0.96f, 0.9f);
        [SerializeField] private Color cardBackColor = new Color(0.3f, 0.1f, 0.5f);
        [SerializeField] private Color redSuitColor = new Color(0.75f, 0.1f, 0.1f);
        [SerializeField] private Color blackSuitColor = Color.black;

        [Header("Dealing feel (Tony-tunable)")]
        [Tooltip("Pause between each card landing when a hand is (re)dealt.")]
        [SerializeField] private float dealStagger = 0.18f;
        [Tooltip("How long one card takes to scale in once it starts landing.")]
        [SerializeField] private float cardLandDuration = 0.15f;

        private bool _wasVisible;

        // One HandView per hand container — tracks what's actually on screen
        // right now (by CARD IDENTITY, not just count) so a same-count
        // different-content hand (stand on 2, deal again into a fresh 2) is
        // correctly detected as changed. Content is compared via PlayingCard's
        // own ToString() ("AS", "10H", ...), which is already a unique short
        // form the class provides for exactly this kind of comparison/log use.
        private class HandView
        {
            public readonly List<GameObject> Cards = new List<GameObject>();
            public readonly List<string> RenderedKeys = new List<string>();
            public bool RenderedFaceDown;
            public Coroutine RevealRoutine;
        }

        private readonly HandView _dealerView = new HandView();
        private readonly HandView _hand0View = new HandView();
        private readonly HandView _hand1View = new HandView();

        private void Awake()
        {
            if (dealButton != null) dealButton.onClick.AddListener(() => station.Deal());
            if (hitButton != null) hitButton.onClick.AddListener(() => station.Hit());
            if (standButton != null) standButton.onClick.AddListener(() => station.Stand());
            if (doubleButton != null) doubleButton.onClick.AddListener(() => station.Double());
            if (splitButton != null) splitButton.onClick.AddListener(() => station.Split());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => station.Leave());
            if (betSlider != null) betSlider.onValueChanged.AddListener(OnBetSliderChanged);
        }

        private void Update()
        {
            bool visible = station != null && station.CanPlay;
            if (contentRoot != null) contentRoot.SetActive(visible);
            if (!visible) { _wasVisible = false; return; }

            if (!_wasVisible) OnBecameVisible();
            _wasVisible = true;

            BlackjackTable table = station.Table;
            bool preDeal = table.Phase == BlackjackPhase.AwaitingBet || table.Phase == BlackjackPhase.Settled;

            if (betSlider != null) betSlider.gameObject.SetActive(preDeal);
            if (dealButton != null) dealButton.gameObject.SetActive(preDeal);
            if (dealButtonLabel != null)
                dealButtonLabel.text = table.Phase == BlackjackPhase.Settled ? "DEAL AGAIN" : "DEAL";

            if (actionGroup != null) actionGroup.SetActive(table.Phase == BlackjackPhase.PlayerTurn);
            if (table.Phase == BlackjackPhase.PlayerTurn)
            {
                // station.CanDouble/CanSplit, NOT table.CanDouble/CanSplit — the
                // table can't check affordability (no ledger reference by
                // design), only the station can. See both doc comments.
                if (doubleButton != null) doubleButton.interactable = station.CanDouble;
                if (splitButton != null) splitButton.interactable = station.CanSplit;
            }

            RefreshHands(table);
            RefreshOutcome(table);
        }

        private void OnBecameVisible()
        {
            if (betSlider == null || station == null) return;
            betSlider.minValue = station.MinBet;
            betSlider.maxValue = station.MaxBet;
            betSlider.value = station.SelectedBet;
            UpdateBetValueText(station.SelectedBet);

            // Wipe every hand's tracked state (not just its GameObjects) so the
            // next RefreshHands pass treats this as a hard reset even if a
            // leftover key list from a previous sit happens to match by
            // coincidence.
            ClearHandView(_dealerView);
            ClearHandView(_hand0View);
            ClearHandView(_hand1View);
        }

        private void ClearHandView(HandView view)
        {
            if (view.RevealRoutine != null) { StopCoroutine(view.RevealRoutine); view.RevealRoutine = null; }
            foreach (GameObject go in view.Cards) if (go != null) Destroy(go);
            view.Cards.Clear();
            view.RenderedKeys.Clear();
            view.RenderedFaceDown = false;
        }

        private void OnBetSliderChanged(float v)
        {
            int bet = Mathf.RoundToInt(v);
            station.SetBet(bet);
            UpdateBetValueText(station.SelectedBet);
        }

        private void UpdateBetValueText(int bet)
        {
            if (betValueText != null) betValueText.text = $"BET: {bet}";
        }

        private void RefreshHands(BlackjackTable table)
        {
            bool dealerFaceDown = table.Phase == BlackjackPhase.PlayerTurn;
            SyncHand(dealerHandContainer, _dealerView, table.DealerHand.Cards, dealerFaceDown ? 1 : 0, dealerFaceDown);
            if (dealerTotalText != null)
                dealerTotalText.text = dealerFaceDown && table.DealerHand.Count > 0 ? "?" : table.DealerHand.Total.ToString();

            bool hasHand0 = table.PlayerHands.Count > 0;
            SyncHand(playerHand0Container, _hand0View, hasHand0 ? table.PlayerHands[0].Cards : null, 0, false);
            if (playerHand0TotalText != null)
                playerHand0TotalText.text = hasHand0 ? table.PlayerHands[0].Total.ToString() : "";

            bool split = table.PlayerHands.Count > 1;
            if (hand1Group != null) hand1Group.SetActive(split);
            SyncHand(playerHand1Container, _hand1View, split ? table.PlayerHands[1].Cards : null, 0, false);
            if (split && playerHand1TotalText != null)
                playerHand1TotalText.text = table.PlayerHands[1].Total.ToString();

            // Highlight whichever hand is currently acting, so a split table
            // reads as "this one's live" instead of two identical-looking
            // hands — the exact ambiguity the old debug OnGUI never had to
            // solve because it only ever showed one hand.
            SetHandHighlight(playerHand0Container, table.PlayerHands.Count > 1 && table.ActiveHandIndex == 0
                                                    && table.Phase == BlackjackPhase.PlayerTurn);
            SetHandHighlight(playerHand1Container, table.ActiveHandIndex == 1 && table.Phase == BlackjackPhase.PlayerTurn);
        }

        /// <summary>
        /// Brings <paramref name="view"/>'s on-screen cards in line with
        /// <paramref name="cards"/> — by CONTENT, not count, which is what
        /// makes this correctly notice "stood on a 2-card hand, dealt again
        /// into a different 2-card hand" (same count, different cards) rather
        /// than leaving the old hand on screen because the card COUNT never
        /// changed. Three outcomes: nothing changed (do nothing), the new
        /// hand is the old one plus trailing card(s) (a Hit — reveal just the
        /// new card(s)), or anything else (a fresh deal, a face-down flip, a
        /// hand that shrank/reset — clear and reveal the whole hand fresh).
        /// </summary>
        private void SyncHand(RectTransform container, HandView view, IReadOnlyList<PlayingCard> cards, int faceDownCount, bool faceDown)
        {
            if (container == null) return;

            int newCount = cards?.Count ?? 0;
            bool isAppend = faceDown == view.RenderedFaceDown && newCount > view.RenderedKeys.Count;
            if (isAppend)
            {
                for (int i = 0; i < view.RenderedKeys.Count; i++)
                    if (cards[i].ToString() != view.RenderedKeys[i]) { isAppend = false; break; }
            }

            if (isAppend)
            {
                var newKeys = new List<string>(newCount - view.RenderedKeys.Count);
                var newCardObjects = new List<GameObject>(newKeys.Capacity);
                for (int i = view.RenderedKeys.Count; i < newCount; i++)
                {
                    bool cardFaceDown = i >= newCount - faceDownCount;
                    GameObject cardGo = CreateCardVisual(container, cards[i], cardFaceDown);
                    cardGo.transform.localScale = Vector3.zero;
                    view.Cards.Add(cardGo);
                    newCardObjects.Add(cardGo);
                    newKeys.Add(cards[i].ToString());
                }
                view.RenderedKeys.AddRange(newKeys);
                if (view.RevealRoutine != null) StopCoroutine(view.RevealRoutine);
                view.RevealRoutine = StartCoroutine(RevealCards(newCardObjects, view));
                return;
            }

            // Not a simple append — build the CURRENT key list and compare
            // whole-hand-equal before deciding this needs a rebuild, so a
            // frame where nothing changed at all doesn't restart animations
            // or touch pooled GameObjects for no reason.
            bool unchanged = newCount == view.RenderedKeys.Count && faceDown == view.RenderedFaceDown;
            for (int i = 0; unchanged && i < newCount; i++)
                if (cards[i].ToString() != view.RenderedKeys[i]) unchanged = false;
            if (unchanged) return;

            if (view.RevealRoutine != null) { StopCoroutine(view.RevealRoutine); view.RevealRoutine = null; }
            foreach (GameObject go in view.Cards) if (go != null) Destroy(go);
            view.Cards.Clear();
            view.RenderedKeys.Clear();
            view.RenderedFaceDown = faceDown;

            if (cards == null) return;

            var freshCards = new List<GameObject>(newCount);
            for (int i = 0; i < newCount; i++)
            {
                bool cardFaceDown = i >= newCount - faceDownCount;
                GameObject cardGo = CreateCardVisual(container, cards[i], cardFaceDown);
                cardGo.transform.localScale = Vector3.zero;
                view.Cards.Add(cardGo);
                freshCards.Add(cardGo);
                view.RenderedKeys.Add(cards[i].ToString());
            }
            view.RevealRoutine = StartCoroutine(RevealCards(freshCards, view));
        }

        /// <summary>Scales each card in from zero, one at a time, <see cref="dealStagger"/> apart — the "cards coming down in front of you" beat.</summary>
        private IEnumerator RevealCards(List<GameObject> cards, HandView view)
        {
            foreach (GameObject card in cards)
            {
                if (card != null) StartCoroutine(ScaleIn(card.transform));
                yield return new WaitForSeconds(dealStagger);
            }
            view.RevealRoutine = null;
        }

        private IEnumerator ScaleIn(Transform t)
        {
            float elapsed = 0f;
            while (elapsed < cardLandDuration)
            {
                if (t == null) yield break; // hand was cleared mid-animation (e.g. a fast redeal)
                t.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, elapsed / cardLandDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        private static void SetHandHighlight(RectTransform container, bool active)
        {
            if (container == null || !container.TryGetComponent(out Image bg)) return;
            bg.color = active ? new Color(1f, 0.85f, 0.2f, 0.25f) : Color.clear;
        }

        private void RefreshOutcome(BlackjackTable table)
        {
            if (outcomeText == null) return;

            if (table.Phase != BlackjackPhase.Settled || table.LastRoundResults.Count == 0)
            {
                outcomeText.text = "";
                return;
            }

            if (table.LastRoundResults.Count == 1)
            {
                outcomeText.text = FormatOutcome(table.LastRoundResults[0], null);
            }
            else
            {
                outcomeText.text = FormatOutcome(table.LastRoundResults[0], "HAND 1") + "   " +
                                    FormatOutcome(table.LastRoundResults[1], "HAND 2");
            }
        }

        private static string FormatOutcome(BlackjackRound round, string label)
        {
            string prefix = label == null ? "" : label + ": ";
            string net = round.Net > 0 ? $"+{round.Net}" : round.Net.ToString();
            return $"{prefix}{round.Outcome} ({net})";
        }

        /// <summary>Builds one small card panel: a coloured background + rank/suit text (or a plain back if face-down). No art dependency — see class comment.</summary>
        private GameObject CreateCardVisual(RectTransform parent, PlayingCard card, bool faceDown)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.sizeDelta = cardSize;

            var bg = go.GetComponent<Image>();
            bg.color = faceDown ? cardBackColor : cardFaceColor;

            if (!faceDown)
            {
                var textGo = new GameObject("Label", typeof(RectTransform));
                var textRect = (RectTransform)textGo.transform;
                textRect.SetParent(rect, worldPositionStays: false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.text = card.ToString();
                text.fontSize = 20f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds ? redSuitColor : blackSuitColor;
            }

            return go;
        }
    }
}
