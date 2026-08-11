using UnityEngine;
using WeeSpurts.Core;
using WeeSpurts.Player;

namespace WeeSpurts.UI
{
    /// <summary>
    /// Zero-setup ticket balance overlay — same OnGUI-immediate-mode
    /// philosophy as <see cref="DebugHud"/>: no Canvas, no fonts, perfect for
    /// proving the venue's economy stations actually move a number. A real
    /// HUD replaces this later (Roadmap [5]), same as DebugHud's own note.
    ///
    /// SETUP: on the Player root, next to PlayerAvatar.
    /// </summary>
    public class TicketBalanceHud : MonoBehaviour
    {
        private PlayerAvatar _avatar;

        private void Awake() => _avatar = GetComponent<PlayerAvatar>();

        private void OnGUI()
        {
            if (_avatar == null || !_avatar.IsThisMachinesPlayer) return;
            if (GameManager.Instance == null) return;

            int balance = GameManager.Instance.Tickets.BalanceOf(_avatar.EconomyPlayerId);
            GUI.Box(new Rect(Screen.width - 180, 10, 160, 26), $"TICKETS: {balance}");
        }
    }
}
