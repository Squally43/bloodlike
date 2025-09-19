using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WH.Gameplay.Enemies;
using WH.Gameplay.Systems;

namespace WH.UI
{
    /// <summary>
    /// World-space enemy UI: HP/Block readout + text intent.
    /// Non-invasive: reads from TurnManager.EnemyState via reflection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyWorldHUD : MonoBehaviour
    {
        [Header("Scene Services")]
        [SerializeField] private TurnManager turns;
        [SerializeField] private EnemyData enemyData;   // optional: for intent preview

        [Header("UI")]
        [SerializeField] private Slider enemyHpSlider;
        [SerializeField] private TMP_Text enemyHpText;
        [SerializeField] private TMP_Text enemyIntentText;

        private int roundIndex = 1;
        private bool firstPlayerTurnSeen = false;

        private void OnEnable()
        {
            if (turns == null)
                turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);

            if (turns != null)
            {
                turns.OnPlayerTurnStarted += OnPlayerTurnStarted;
                turns.OnPlayerTurnEnded += OnPlayerTurnEnded;
                turns.OnEnemyTurnStarted += OnEnemyTurnStarted;
                turns.OnEnemyTurnEnded += OnEnemyTurnEnded;
                turns.OnBattleEnded += OnBattleEnded;
            }

            RefreshHP();          // immediate draw
            UpdateIntentPreview(); // immediate intent text

            // Lightweight safety refresh in case HP changes between events.
            InvokeRepeating(nameof(RefreshHP), 0.25f, 0.25f);
        }

        private void OnDisable()
        {
            if (turns != null)
            {
                turns.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                turns.OnPlayerTurnEnded -= OnPlayerTurnEnded;
                turns.OnEnemyTurnStarted -= OnEnemyTurnStarted;
                turns.OnEnemyTurnEnded -= OnEnemyTurnEnded;
                turns.OnBattleEnded -= OnBattleEnded;
            }
            CancelInvoke(nameof(RefreshHP));
        }

        // --- Turn hooks ---
        private void OnPlayerTurnStarted()
        {
            if (!firstPlayerTurnSeen)
            {
                firstPlayerTurnSeen = true;
                roundIndex = 1;
            }
            UpdateIntentPreview();
            RefreshHP();
        }

        private void OnPlayerTurnEnded()
        {
            RefreshHP();
        }

        private void OnEnemyTurnStarted()
        {
            if (enemyIntentText) enemyIntentText.text = "…resolving";
        }

        private void OnEnemyTurnEnded()
        {
            roundIndex++;
            RefreshHP();
            UpdateIntentPreview();
        }

        private void OnBattleEnded(bool playerWon)
        {
            // leave final values visible
        }

        // --- UI updates ---
        private void RefreshHP()
        {
            var enemyState = turns != null ? turns.EnemyState : null;
            if (enemyState == null)
            {
                if (enemyHpText) enemyHpText.text = "HP --/-- | Block --";
                if (enemyHpSlider)
                {
                    enemyHpSlider.maxValue = 1;
                    enemyHpSlider.value = 0;
                }
                return;
            }

            int cur = GetInt(enemyState, "CurrentHp");
            int max = Mathf.Max(1, GetInt(enemyState, "MaxHp"));
            int blk = Mathf.Max(0, GetInt(enemyState, "Block"));

            if (enemyHpText) enemyHpText.text = $"HP {cur}/{max} | Block {blk}";
            if (enemyHpSlider)
            {
                if (Math.Abs(enemyHpSlider.maxValue - max) > 0.01f) enemyHpSlider.maxValue = max;
                enemyHpSlider.value = Mathf.Clamp(cur, 0, max);
            }
        }

        private void UpdateIntentPreview()
        {
            if (!enemyIntentText) return;
            enemyIntentText.text = BuildIntentString(enemyData, roundIndex);
        }

        // --- Intent formatting (no enum dependency) ---
        public static string BuildIntentString(EnemyData data, int roundIdx)
        {
            if (data == null || data.Intents == null || data.Intents.Count == 0) return "";

            int idx = Mathf.Clamp((roundIdx - 1) % data.Intents.Count, 0, data.Intents.Count - 1);
            object step = data.Intents[idx];

            string type = GetEnumName(step, "type"); // e.g., Attack, Block, ApplyStatus
            int value = GetInt(step, "value");
            int times = Mathf.Max(1, GetInt(step, "times"));
            string status = GetString(step, "status");

            switch (type)
            {
                case "Attack":
                case "MultiAttack": return $"Attack {value}×{times}";
                case "Block": return $"Block {value}";
                case "ApplyStatus": return $"{status} ×{value}";
                default: return type ?? "";
            }
        }

        // --- tiny reflection helpers ---
        private static int GetInt(object obj, string name)
        {
            if (obj == null) return 0;
            var p = obj.GetType().GetProperty(name);
            if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
            var f = obj.GetType().GetField(name);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
            return 0;
        }

        private static string GetString(object obj, string name)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(name);
            if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
            var f = obj.GetType().GetField(name);
            if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
            return null;
        }

        private static string GetEnumName(object obj, string name)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(name);
            if (p != null && p.PropertyType.IsEnum) return Enum.GetName(p.PropertyType, p.GetValue(obj));
            var f = obj.GetType().GetField(name);
            if (f != null && f.FieldType.IsEnum) return Enum.GetName(f.FieldType, f.GetValue(obj));
            return null;
        }
    }
}

