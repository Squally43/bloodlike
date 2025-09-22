using System;

namespace WH
{
    /// <summary>Cross-scene event hub. Bootstrap raises; Main listens; Main can request actions.</summary>
    public static class GameSignals
    {
        // ---- Raised by Bootstrap (TurnManager), listened by Main (HUD, Camera, Harvest) ----
        public static event Action OnPlayerTurnStarted;
        public static event Action<bool> OnBattleEnded;

        // ---- Raised by Main, consumed by Bootstrap (TurnManager) ----
        public static event Action OnNextFightReadied;
        public static event Action OnEndTurnRequested;

        public static void RaisePlayerTurnStarted() => OnPlayerTurnStarted?.Invoke();
        public static void RaiseBattleEnded(bool playerWon) => OnBattleEnded?.Invoke(playerWon);
        public static void RaiseNextFightReadied() => OnNextFightReadied?.Invoke();
        public static void RaiseEndTurnRequested() => OnEndTurnRequested?.Invoke();
    }
}



