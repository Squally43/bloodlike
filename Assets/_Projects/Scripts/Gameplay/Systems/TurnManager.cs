using System;
using UnityEngine;
using WH.Gameplay.Enemies;

namespace WH.Gameplay.Systems
{
    /// <summary>Controls turn order and resolves enemy intents.</summary>
    [DisallowMultipleComponent]
    public sealed class TurnManager : MonoBehaviour
    {
        [SerializeField] private PulseManager _pulse;
        [SerializeField] private EnemyAI _enemyAI;

        [Header("Runtime combatants (auto-found if unassigned)")]
        [SerializeField] private CombatantState _player;
        [SerializeField] private CombatantState _enemy;

        public event Action OnPlayerTurnStarted;
        public event Action OnPlayerTurnEnded;
        public event Action OnEnemyTurnStarted;
        public event Action OnEnemyTurnEnded;
        public event Action<bool> OnBattleEnded;

        private EnemyData _enemyData;
        private int _roundIndex;
        private bool _battleActive;

        public void StartBattle(EnemyData enemyData)
        {
            _enemyData = enemyData;
            _roundIndex = 1;
            _battleActive = true;

            // Lazy find combatants
            if (_player == null || _enemy == null)
            {
                var all = FindObjectsByType<CombatantState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in all)
                {
                    if (c.IsPlayer) _player ??= c;
                    else _enemy ??= c;
                }
            }

            if (_enemyAI == null)
                _enemyAI = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);

            // Init HP
            _player?.Init(_player.MaxHp);                     // player decides own MaxHp in inspector
            _enemy?.Init(_enemyData != null ? _enemyData.MaxHp : 30);

            if (_enemyAI != null) _enemyAI.SetData(_enemyData);

            Debug.Log($"[Turn] Battle start vs {_enemyData.DisplayName}");
            StartPlayerTurn();
        }

        public void EndPlayerTurn()
        {
            if (!_battleActive) return;

            // End of player turn hooks
            _player?.ClearBlock();

            OnPlayerTurnEnded?.Invoke();
            Debug.Log("[Turn] Player turn end");

            StartEnemyTurn();
        }

        private void StartPlayerTurn()
        {
            if (!_battleActive) return;

            _pulse.ResetForNewTurn();
            OnPlayerTurnStarted?.Invoke();
            Debug.Log($"[Turn] Player turn start - Round {_roundIndex}");
        }

        private void StartEnemyTurn()
        {
            if (!_battleActive) return;

            OnEnemyTurnStarted?.Invoke();
            Debug.Log("[Turn] Enemy turn start");

            if (_enemyAI == null)
                _enemyAI = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);

            if (_enemyAI != null)
            {
                // Select and resolve one intent
                _enemyAI.SelectNextIntent();
                ResolveEnemyIntent();
            }
            else
            {
                Debug.LogWarning("[Turn] EnemyAI not found. Skipping intent.");
            }

            // End of enemy turn hooks
            _enemy?.ClearBlock();

            OnEnemyTurnEnded?.Invoke();
            Debug.Log("[Turn] Enemy turn end");

            EvaluateBattleEnd();
            if (!_battleActive) return;

            _roundIndex++;
            StartPlayerTurn();
        }

        private void ResolveEnemyIntent()
        {
            if (_enemyData == null) return;

            if (_enemyData.Intents.Count > 0)
            {
                var i = (_roundIndex - 1) % _enemyData.Intents.Count;
                var chosen = _enemyData.Intents[i];

                switch (chosen.type)
                {
                    case IntentType.Attack:
                        for (int t = 0; t < Mathf.Max(1, chosen.times); t++)
                            _player?.ApplyDamage(chosen.value);
                        break;

                    case IntentType.MultiAttack:
                        for (int t = 0; t < Mathf.Max(1, chosen.times); t++)
                            _player?.ApplyDamage(chosen.value);
                        break;

                    case IntentType.Block:
                        _enemy?.GainBlock(chosen.value);
                        break;

                    case IntentType.ApplyStatus:
                        Debug.Log($"[Status] Enemy applies {chosen.status} x{chosen.value} (stub)");
                        break;

                    default:
                        break;
                }
            }
        }


        private void EvaluateBattleEnd()
        {
            if (!_battleActive) return;
            if (_player != null && _player.IsDead) { ForceBattleEnd(false); return; }
            if (_enemy != null && _enemy.IsDead) { ForceBattleEnd(true); return; }
        }

        public void ForceBattleEnd(bool playerWon)
        {
            if (!_battleActive) return;
            _battleActive = false;
            OnBattleEnded?.Invoke(playerWon);
            Debug.Log($"[Turn] Battle end. PlayerWon={playerWon}");
        }

        // Expose combatants for other systems
        public CombatantState PlayerState => _player;
        public CombatantState EnemyState => _enemy;
    }
}



