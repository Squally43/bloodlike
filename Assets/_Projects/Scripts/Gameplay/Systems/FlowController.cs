using System;
using UnityEngine;
using WH.Gameplay.Enemies;

namespace WH.Gameplay.Systems
{
    public enum GamePhase { Run, Arena, Fight, Harvest, Event, Stitch, Next }

    /// <summary>Owns the high-level loop and scene entry points.</summary>
    [DisallowMultipleComponent]
    public sealed class FlowController : MonoBehaviour
    {
        [Header("Scene Systems")]
        [SerializeField] private TurnManager _turnManager;
        [SerializeField] private PulseManager _pulse;

        [Header("Temp Debug")]
        [SerializeField] private EnemyData testEnemy;

        public event Action<GamePhase> OnPhaseChanged;

        public GamePhase Phase { get; private set; } = GamePhase.Run;

        private void Start()
        {
            EnterArena();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_turnManager != null) _turnManager.EndPlayerTurn();
            }
        }

        public void EnterArena()
        {
            SetPhase(GamePhase.Arena);
            StartFight(testEnemy);
        }

        public void StartFight(EnemyData enemy)
        {
            if (enemy == null)
            {
                Debug.LogError("FlowController.StartFight called with null enemy");
                return;
            }

            SetPhase(GamePhase.Fight);

            _turnManager.OnBattleEnded -= HandleBattleEnded;
            _turnManager.OnBattleEnded += HandleBattleEnded;
            _turnManager.StartBattle(enemy);
        }

        private void HandleBattleEnded(bool playerWon)
        {
            _turnManager.OnBattleEnded -= HandleBattleEnded;

            if (playerWon)
            {
                SetPhase(GamePhase.Harvest);
            }
            else
            {
                SetPhase(GamePhase.Stitch);
            }
        }

        private void SetPhase(GamePhase next)
        {
            Phase = next;
            OnPhaseChanged?.Invoke(Phase);
            Debug.Log($"[Flow] Phase -> {Phase}");
        }
    }
}


