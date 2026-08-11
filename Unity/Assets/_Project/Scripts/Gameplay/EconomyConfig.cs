using UnityEngine;

namespace WeeSpurts.Gameplay
{
    /// <summary>
    /// Session-wide economy tunables. Deliberately tiny for now — one number —
    /// because `Docs/SlopLayerPlan.md` is explicit that the economy gets tuned
    /// with four people in the room, and inventing knobs before then is
    /// inventing answers to questions nobody has asked yet.
    ///
    /// SETUP: optional. <see cref="WeeSpurts.Core.GameManager"/> falls back to
    /// <see cref="DEFAULT_STARTING_COINS"/> if no asset is assigned, so a scene
    /// that hasn't been wired still runs with a sane economy rather than
    /// leaving every player broke and every shop mysteriously refusing them.
    /// Create one via Assets > Create > WeeSpurts > Economy Config and drop it
    /// on the GameManager when you want to tune it.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "WeeSpurts/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        /// <summary>Used when no config asset is wired up. Keep it playable, not balanced.</summary>
        public const int DEFAULT_STARTING_COINS = 500;

        [Tooltip("Coins each player starts a SESSION with (not each match — the ledger lives on GameManager and survives scene loads, so winnings carry between games). Placeholder: big enough to afford a few rounds at the bar and a couple of hands of blackjack without thinking about it, which is what you want while judging whether the casino corner is fun at all.")]
        public int StartingCoins = DEFAULT_STARTING_COINS;
    }
}
