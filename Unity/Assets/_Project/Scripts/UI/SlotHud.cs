using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeeSpurts.Core;
using WeeSpurts.Slop;

namespace WeeSpurts.UI
{
    /// <summary>
    /// A slot machine's real UI — the first presentation the slot engine has
    /// ever had (<see cref="SlotMachine"/> previously had no scene presence at
    /// all). A Canvas child of a <see cref="SlotStation"/>, built and wired by
    /// <c>ThunderLanesVenueStationSetupTool</c>, same [SerializeField]-
    /// everything discipline as <see cref="BlackjackHud"/>.
    ///
    /// THIS CLASS OWNS PRESENTATION ONLY — every Pull funnels through
    /// <see cref="SlotStation.Pull"/>, the only thing that ever touches
    /// <see cref="SlotMachine"/> or the ledger.
    ///
    /// THE FLICKER IS COSMETIC RANDOMNESS, NOT GAME STATE: it never influences
    /// the real result (that comes back from <see cref="SlotStation.Pull"/>
    /// in one call, same "no in-between state a disconnect could leave
    /// dangling" reasoning as the engine's own class comment), so it uses
    /// plain <see cref="UnityEngine.Random"/> rather than the seeded
    /// <c>DeterministicRng</c> — same rule this project already applies to
    /// non-throw-driven sound variation.
    ///
    /// SYMBOLS ARE PROCEDURAL PLACEHOLDERS (coloured panel + short label), no
    /// icon art exists yet — same move <see cref="BlackjackHud"/> uses for
    /// cards.
    /// </summary>
    public class SlotHud : MonoBehaviour
    {
        [Header("Wiring (set by the setup tool)")]
        [SerializeField] private SlotStation station;
        [SerializeField] private GameObject contentRoot;

        [Header("Betting")]
        [SerializeField] private Slider betSlider;
        [SerializeField] private TMP_Text betValueText;

        [Header("Pull")]
        [SerializeField] private Button pullButton;
        [SerializeField] private TMP_Text pullButtonLabel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button leaveButton;

        [Header("Reels")]
        [SerializeField] private Image reel0Background;
        [SerializeField] private TMP_Text reel0Text;
        [SerializeField] private Image reel1Background;
        [SerializeField] private TMP_Text reel1Text;
        [SerializeField] private Image reel2Background;
        [SerializeField] private TMP_Text reel2Text;

        [Header("Bonus")]
        [SerializeField] private GameObject bonusBanner;
        [SerializeField] private TMP_Text bonusText;

        [Header("Feel (Tony-tunable)")]
        [Tooltip("How long the reels flicker through random symbols before settling on the real result.")]
        [SerializeField] private float flickerDuration = 0.45f;
        [Tooltip("How often the flicker swaps symbols while it runs.")]
        [SerializeField] private float flickerStep = 0.06f;

        private static readonly SlotSymbol[] AllSymbols = (SlotSymbol[])System.Enum.GetValues(typeof(SlotSymbol));

        private bool _wasVisible;
        private bool _flickering;
        private bool _wasInBonus;
        private AudioSource _spinSource; // dedicated, not pooled — needs to loop for exactly the flicker's duration, same reasoning as BowlingBall's roll loop

        private void Awake()
        {
            if (pullButton != null) pullButton.onClick.AddListener(OnPullClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => { AudioManager.Instance?.PlaySfx(SoundId.UiClick); station.Leave(); });
            if (betSlider != null) betSlider.onValueChanged.AddListener(OnBetSliderChanged);

            _spinSource = gameObject.AddComponent<AudioSource>();
            _spinSource.loop = true;
            _spinSource.playOnAwake = false;
            _spinSource.spatialBlend = 1f;
        }

        private void Update()
        {
            bool visible = station != null && station.CanPlay;
            if (contentRoot != null) contentRoot.SetActive(visible);
            if (!visible) { _wasVisible = false; return; }

            if (!_wasVisible) OnBecameVisible();
            _wasVisible = true;

            SlotMachine machine = station.Machine;
            bool inBonus = machine.BonusPullsRemaining > 0;

            // FREE FRAME reads as an event (exec-day plan: "the whole alley
            // hears it") — PlaySfx, not PlaySfxAt, is deliberate: non-
            // positional means every player in the venue hears it, not just
            // whoever's sitting here. Fires once on the false->true edge, not
            // every frame the banner is up, and a retrigger mid-bonus (more
            // free pulls stacked on top) does NOT re-fire it — only the
            // initial trigger is the "event."
            if (inBonus && !_wasInBonus) AudioManager.Instance?.PlaySfx(SoundId.SlotJackpot);
            _wasInBonus = inBonus;

            if (bonusBanner != null) bonusBanner.SetActive(inBonus);
            if (bonusText != null && inBonus)
                bonusText.text = $"FREE FRAME!  {machine.BonusPullsRemaining} pulls left   x{machine.BonusMultiplier}";
            if (betSlider != null) betSlider.gameObject.SetActive(!inBonus);
            if (pullButtonLabel != null) pullButtonLabel.text = inBonus ? "PULL (FREE)" : "PULL";
            if (pullButton != null && !_flickering) pullButton.interactable = true;
        }

        private void OnBecameVisible()
        {
            if (station == null) return;
            if (betSlider != null)
            {
                betSlider.minValue = station.MinStake;
                betSlider.maxValue = station.MaxStake;
                betSlider.value = station.SelectedBet;
            }
            UpdateBetValueText(station.SelectedBet);
            if (resultText != null) resultText.text = "";
            ResetReels();
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

        private void OnPullClicked()
        {
            if (_flickering || station == null) return;
            AudioManager.Instance?.PlaySfxAt(SoundId.SlotLeverPull, station.transform.position);
            StartCoroutine(PullRoutine());
        }

        private IEnumerator PullRoutine()
        {
            _flickering = true;
            if (pullButton != null) pullButton.interactable = false;
            if (resultText != null) resultText.text = "";

            AudioClip spinClip = AudioManager.Instance != null ? AudioManager.Instance.GetClip(SoundId.SlotSpinning) : null;
            if (spinClip != null)
            {
                _spinSource.clip = spinClip;
                _spinSource.Play();
            }

            float elapsed = 0f;
            while (elapsed < flickerDuration)
            {
                SetReelSymbol(0, RandomSymbol());
                SetReelSymbol(1, RandomSymbol());
                SetReelSymbol(2, RandomSymbol());
                yield return new WaitForSeconds(flickerStep);
                elapsed += flickerStep;
            }

            _spinSource.Stop();
            SlotPull pull = station.Pull();
            SetReelSymbol(0, pull.Reel0);
            SetReelSymbol(1, pull.Reel1);
            SetReelSymbol(2, pull.Reel2);
            AudioManager.Instance?.PlaySfxAt(SoundId.SlotReelStop, station.transform.position);

            if (resultText != null)
                resultText.text = !pull.Spun ? "—" : (pull.Returned > 0 ? $"+{pull.Returned}!" : "nothing");

            _flickering = false;
            // Update() re-enables pullButton next frame once it re-reads CanPlay.
        }

        private void ResetReels()
        {
            SetReelSymbol(0, AllSymbols[0]);
            SetReelSymbol(1, AllSymbols[0]);
            SetReelSymbol(2, AllSymbols[0]);
        }

        private void SetReelSymbol(int index, SlotSymbol symbol)
        {
            (Image bg, TMP_Text text) = index switch
            {
                0 => (reel0Background, reel0Text),
                1 => (reel1Background, reel1Text),
                _ => (reel2Background, reel2Text)
            };

            if (bg != null) bg.color = SymbolColor(symbol);
            if (text != null) text.text = SymbolLabel(symbol);
        }

        private static SlotSymbol RandomSymbol() => AllSymbols[Random.Range(0, AllSymbols.Length)];

        private static string SymbolLabel(SlotSymbol s) => s switch
        {
            SlotSymbol.Pin => "PIN",
            SlotSymbol.Beer => "BEER",
            SlotSymbol.Ball => "BALL",
            SlotSymbol.Shoe => "SHOE",
            SlotSymbol.Ticket => "TIX",
            SlotSymbol.Mascot => "MASCOT",
            _ => "?"
        };

        private static Color SymbolColor(SlotSymbol s) => s switch
        {
            SlotSymbol.Pin => new Color(0.9f, 0.9f, 0.9f),
            SlotSymbol.Beer => new Color(0.85f, 0.65f, 0.15f),
            SlotSymbol.Ball => new Color(0.15f, 0.2f, 0.35f),
            SlotSymbol.Shoe => new Color(0.5f, 0.3f, 0.15f),
            SlotSymbol.Ticket => new Color(0.8f, 0.15f, 0.2f),
            SlotSymbol.Mascot => new Color(0.95f, 0.75f, 0.1f),
            _ => Color.grey
        };
    }
}
