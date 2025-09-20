using System;

namespace WH
{
    /// <summary>Cross-scene event hub. Bootstrap raises; Main listens; Main can request actions.</summary>
    public static class GameSignals
    {
        // ---- Raised by Bootstrap (TurnManager), listened by Main (HUD, Camera, Harvest) ----
        public static event Action OnPlayerTurnStarted;             // camera -> combat, HUD draw, etc.
        public static event Action<bool> OnBattleEnded;             // camera -> harvest, HarvestGlue, etc.

        // ---- Raised by Main, consumed by Bootstrap (TurnManager) ----
        public static event Action OnNextFightReadied;              // driver says "please start a new battle"
        public static event Action OnEndTurnRequested;              // HUD says "player clicked End Turn"

        // Safe raisers
        public static void RaisePlayerTurnStarted() => OnPlayerTurnStarted?.Invoke();
        public static void RaiseBattleEnded(bool playerWon) => OnBattleEnded?.Invoke(playerWon);
        public static void RaiseNextFightReadied() => OnNextFightReadied?.Invoke();
        public static void RaiseEndTurnRequested() => OnEndTurnRequested?.Invoke();
    }
}


