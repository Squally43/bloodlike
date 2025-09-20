using System;
using UnityEngine;
using WH.Gameplay.Enemies;

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// Minimal, event-driven battle loop (Bootstrap scene).
    /// Emits global signals so Main can react (camera, HUD, harvest, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurnManager : MonoBehaviour
    {
        // ---------- Local events (kept for in-asm listeners) ----------
        public event Action OnPlayerTurnStarted;
        public event Action OnPlayerTurnEnded;
        public event Action OnEnemyTurnStarted;
        public event Action OnEnemyTurnEnded;
        public event Action<bool> OnBattleEnded;

        // ---------- Config ----------
        [Header("Starting Values")]
        [SerializeField] private int playerStartingHp = 60;
        [SerializeField] private int enemyStartingHpFallback = 40;

        [Header("Services")]
        [SerializeField] private PulseManager pulse;     // shared Pulse
        public PulseManager Pulse => pulse;

        // ---------- Runtime state ----------
        [Serializable]
        public class CombatantState
        {
            public string Name = "Unknown";
            public int MaxHp;
            public int CurrentHp;
            public int Block;
        }

        [Header("Public States (for HUDs)")]
        public CombatantState PlayerState { get; private set; } = new CombatantState { Name = "Player" };
        public CombatantState EnemyState { get; private set; } = new CombatantState { Name = "Enemy" };

        public bool BattleRunning { get; private set; }
        private EnemyData activeEnemyData;
        private int roundIndex = 1; // 1-based, cycles enemy intents

        // =========================================================
        //                 Bootstrap <-> Main bridge
        // =========================================================
        private void OnEnable()
        {
            // Main tells Bootstrap to end the turn / start next fight
            WH.GameSignals.OnEndTurnRequested += EndPlayerTurn;
            WH.GameSignals.OnNextFightReadied += StartNewBattle;
        }

        private void OnDisable()
        {
            WH.GameSignals.OnEndTurnRequested -= EndPlayerTurn;
            WH.GameSignals.OnNextFightReadied -= StartNewBattle;
        }

        // =========================================================
        //                       PUBLIC API
        // =========================================================
        public void StartBattle(EnemyData enemy)
        {
            BattleRunning = true;
            activeEnemyData = enemy;
            roundIndex = 1;

            // init player
            PlayerState.MaxHp = Mathf.Max(1, playerStartingHp);
            PlayerState.CurrentHp = PlayerState.MaxHp;
            PlayerState.Block = 0;

            // init enemy
            EnemyState.Name = enemy ? (string.IsNullOrEmpty(enemy.name) ? "Enemy" : enemy.name) : "Enemy";
            int enemyMax = GetEnemyMaxHp(enemy, enemyStartingHpFallback);
            EnemyState.MaxHp = Mathf.Max(1, enemyMax);
            EnemyState.CurrentHp = EnemyState.MaxHp;
            EnemyState.Block = 0;

            if (pulse) pulse.ResetForNewTurn();

            // First player turn
            OnPlayerTurnStarted?.Invoke();
            WH.GameSignals.RaisePlayerTurnStarted();
        }

        /// <summary>Called from Main via GameSignals when tester presses 'N'.</summary>
        public void StartNewBattle()
        {
            // Reuse the current enemy data (or null -> fallback)
            StartBattle(activeEnemyData);
        }

        /// <summary>Called from Main via GameSignals when End Turn is clicked.</summary>
        public void EndPlayerTurn()
        {
            if (!BattleRunning) return;
            OnPlayerTurnEnded?.Invoke();
            BeginEnemyTurn();
        }

        // =========================================================
        //                   TURN FLOW (private)
        // =========================================================
        private void BeginPlayerTurn()
        {
            if (!BattleRunning) return;

            // MVP rule: Player block clears at the start of their own turn
            PlayerState.Block = 0;

            if (pulse != null) pulse.ResetForNewTurn();

            OnPlayerTurnStarted?.Invoke();
            WH.GameSignals.RaisePlayerTurnStarted();
        }

        private void BeginEnemyTurn()
        {
            if (!BattleRunning) return;

            // MVP rule: Enemy block clears at the start of its own turn
            EnemyState.Block = 0;

            OnEnemyTurnStarted?.Invoke();

            ResolveEnemyIntentOnce();

            ClampHp(PlayerState);
            ClampHp(EnemyState);

            if (CheckBattleEnd()) return;

            OnEnemyTurnEnded?.Invoke();
            roundIndex++;
            BeginPlayerTurn();
        }

        // =========================================================
        //                ENEMY INTENT (very simple MVP)
        // =========================================================
        private void ResolveEnemyIntentOnce()
        {
            if (activeEnemyData == null || activeEnemyData.Intents == null || activeEnemyData.Intents.Count == 0)
            {
                DealDamageToPlayer(5); // simple fallback so loop is visible
                return;
            }

            var intents = activeEnemyData.Intents;
            int idx = Mathf.Clamp((roundIndex - 1) % intents.Count, 0, intents.Count - 1);
            var step = intents[idx];

            string type = GetEnumName(step, "type");
            int value = TryGetInt(step, "value");
            int times = Mathf.Max(1, TryGetInt(step, "times"));
            // string status = GetString(step, "status"); // reserved

            switch (type)
            {
                case "Attack":
                case "MultiAttack":
                    for (int i = 0; i < times; i++) DealDamageToPlayer(value);
                    break;

                case "Block":
                    EnemyState.Block += Mathf.Max(0, value);
                    break;

                case "ApplyStatus":
                    // statuses come later
                    break;

                default:
                    DealDamageToPlayer(Mathf.Max(1, value));
                    break;
            }
        }

        // =========================================================
        //                    DAMAGE / RULES HELPERS
        // =========================================================
        public void DealDamageToEnemy(int rawDamage)
        {
            if (!BattleRunning) return;
            ApplyDamage(rawDamage, EnemyState);
            if (CheckBattleEnd()) return;
        }

        public void DealDamageToPlayer(int rawDamage)
        {
            if (!BattleRunning) return;
            ApplyDamage(rawDamage, PlayerState);
        }

        private static void ApplyDamage(int rawDamage, CombatantState target)
        {
            int d = Mathf.Max(0, rawDamage);
            int absorbed = Mathf.Min(target.Block, d);
            target.Block -= absorbed;
            d -= absorbed;
            if (d > 0) target.CurrentHp = Mathf.Max(0, target.CurrentHp - d);
        }

        private bool CheckBattleEnd()
        {
            if (EnemyState.CurrentHp <= 0)
            {
                BattleRunning = false;
                OnBattleEnded?.Invoke(true);
                WH.GameSignals.RaiseBattleEnded(true);
                return true;
            }
            if (PlayerState.CurrentHp <= 0)
            {
                BattleRunning = false;
                OnBattleEnded?.Invoke(false);
                WH.GameSignals.RaiseBattleEnded(false);
                return true;
            }
            return false;
        }

        private static void ClampHp(CombatantState s)
        {
            if (s.MaxHp <= 0) s.MaxHp = 1;
            if (s.CurrentHp > s.MaxHp) s.CurrentHp = s.MaxHp;
            if (s.CurrentHp < 0) s.CurrentHp = 0;
            if (s.Block < 0) s.Block = 0;
        }

        private static int GetEnemyMaxHp(object enemyData, int fallback)
        {
            if (enemyData == null) return fallback;
            int v = TryGetInt(enemyData, "MaxHp");
            if (v <= 0) v = TryGetInt(enemyData, "BaseHp");
            if (v <= 0) v = TryGetInt(enemyData, "Health");
            if (v <= 0) v = TryGetInt(enemyData, "Hp");
            return v > 0 ? v : fallback;
        }

        // =========================================================
        //                  Reflection helpers
        // =========================================================
        private static int TryGetInt(object obj, string name)
        {
            if (obj == null) return 0;
            var t = obj.GetType();
            var p = t.GetProperty(name);
            if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
            var f = t.GetField(name);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
            return 0;
        }

        private static string GetString(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var p = t.GetProperty(name);
            if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
            var f = t.GetField(name);
            if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
            return null;
        }

        private static string GetEnumName(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var p = t.GetProperty(name);
            if (p != null && p.PropertyType.IsEnum) return Enum.GetName(p.PropertyType, p.GetValue(obj));
            var f = t.GetField(name);
            if (f != null && f.FieldType.IsEnum) return Enum.GetName(f.FieldType, f.GetValue(obj));
            return null;
        }
    }
}






