using System;
using UnityEngine;
using WH.Gameplay; // BodyTag

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// Single source of truth for Phase 4 marks (per-fight).
    /// - PlayerHarvestMarkOnEnemy: the part YOU marked on the enemy (from Glare etc).
    /// - EnemyThreatMarkOnPlayer: the part THEY will harvest from you on defeat.
    /// Clears at fight end.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarkController : MonoBehaviour
    {
        public event Action<BodyTag?> OnPlayerHarvestMarkChanged; // emits null when cleared
        public event Action<BodyTag?> OnEnemyThreatMarkChanged;   // emits null when cleared

        public BodyTag? PlayerHarvestMarkOnEnemy { get; private set; }
        public BodyTag? EnemyThreatMarkOnPlayer { get; private set; }

        public void ApplyPlayerHarvestMark(BodyTag tag)
        {
            if (PlayerHarvestMarkOnEnemy == tag) return;
            PlayerHarvestMarkOnEnemy = tag;
            OnPlayerHarvestMarkChanged?.Invoke(PlayerHarvestMarkOnEnemy);
#if UNITY_EDITOR
            Debug.Log($"[Marks] Player marked enemy: {tag}");
#endif
        }

        public void ApplyEnemyThreatMark(BodyTag tag)
        {
            if (EnemyThreatMarkOnPlayer == tag) return;
            EnemyThreatMarkOnPlayer = tag;
            OnEnemyThreatMarkChanged?.Invoke(EnemyThreatMarkOnPlayer);
#if UNITY_EDITOR
            Debug.Log($"[Marks] Enemy threatened your: {tag}");
#endif
        }

        /// <summary>Clear both marks at fight end.</summary>
        public void ClearAll()
        {
            PlayerHarvestMarkOnEnemy = null;
            EnemyThreatMarkOnPlayer = null;
            OnPlayerHarvestMarkChanged?.Invoke(null);
            OnEnemyThreatMarkChanged?.Invoke(null);
#if UNITY_EDITOR
            Debug.Log("[Marks] Cleared (fight end).");
#endif
        }
    }
}


