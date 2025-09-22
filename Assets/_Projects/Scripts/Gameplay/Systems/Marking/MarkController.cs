using System;
using UnityEngine;
using WH.Gameplay; // BodyTag

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// Single source of truth for Phase 4 marks (per-fight).
    /// Uses static backing so multiple scene instances all see the same values.
    /// - PlayerHarvestMarkOnEnemy: the part YOU marked on the enemy (from Glare etc).
    /// - EnemyThreatMarkOnPlayer : the part THEY threaten to take from you on defeat.
    /// Clears at fight end.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class MarkController : MonoBehaviour
    {
        // -------- Global state (works across scenes/instances) --------
        private static BodyTag? s_PlayerHarvestMarkOnEnemy;
        private static BodyTag? s_EnemyThreatMarkOnPlayer;

        // Instance events (optional UI hooks)
        public event Action<BodyTag?> OnPlayerHarvestMarkChanged; // emits null when cleared
        public event Action<BodyTag?> OnEnemyThreatMarkChanged;   // emits null when cleared

        // Instance readers return the global values
        public BodyTag? PlayerHarvestMarkOnEnemy => s_PlayerHarvestMarkOnEnemy;
        public BodyTag? EnemyThreatMarkOnPlayer => s_EnemyThreatMarkOnPlayer;

        // -------- Instance API (fires instance events, updates global) --------
        public void ApplyPlayerHarvestMark(BodyTag tag)
        {
            if (s_PlayerHarvestMarkOnEnemy == tag) return;
            s_PlayerHarvestMarkOnEnemy = tag;
            OnPlayerHarvestMarkChanged?.Invoke(s_PlayerHarvestMarkOnEnemy);
#if UNITY_EDITOR
            Debug.Log($"[Marks] Player marked enemy: {tag} (global)");
#endif
        }

        public void ApplyEnemyThreatMark(BodyTag tag)
        {
            if (s_EnemyThreatMarkOnPlayer == tag) return;
            s_EnemyThreatMarkOnPlayer = tag;
            OnEnemyThreatMarkChanged?.Invoke(s_EnemyThreatMarkOnPlayer);
#if UNITY_EDITOR
            Debug.Log($"[Marks] Enemy threatened your: {tag} (global)");
#endif
        }

        /// <summary>Clear both marks at fight end.</summary>
        public void ClearAll()
        {
            s_PlayerHarvestMarkOnEnemy = null;
            s_EnemyThreatMarkOnPlayer = null;
            OnPlayerHarvestMarkChanged?.Invoke(null);
            OnEnemyThreatMarkChanged?.Invoke(null);
#if UNITY_EDITOR
            Debug.Log("[Marks] Cleared (global, fight end).");
#endif
        }

        // -------- Static helpers (when you don't have an instance handy) --------
        public static void ApplyPlayerHarvestMarkGlobal(BodyTag tag)
        {
            if (s_PlayerHarvestMarkOnEnemy == tag) return;
            s_PlayerHarvestMarkOnEnemy = tag;
#if UNITY_EDITOR
            Debug.Log($"[Marks] (global) Player marked enemy: {tag}");
#endif
        }

        public static void ApplyEnemyThreatMarkGlobal(BodyTag tag)
        {
            if (s_EnemyThreatMarkOnPlayer == tag) return;
            s_EnemyThreatMarkOnPlayer = tag;
#if UNITY_EDITOR
            Debug.Log($"[Marks] (global) Enemy threatened your: {tag}");
#endif
        }

        public static BodyTag? GetPlayerHarvestMarkGlobal() => s_PlayerHarvestMarkOnEnemy;
        public static BodyTag? GetEnemyThreatMarkGlobal() => s_EnemyThreatMarkOnPlayer;

        public static void ClearAllGlobal()
        {
            s_PlayerHarvestMarkOnEnemy = null;
            s_EnemyThreatMarkOnPlayer = null;
#if UNITY_EDITOR
            Debug.Log("[Marks] (global) Cleared.");
#endif
        }
    }
}




