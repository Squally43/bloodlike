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
        [SerializeField] private HarvestGlue _harvestGlue;
        [SerializeField] private CanvasGroup _harvestOverlay;
        [Header("Temp Debug")]
        [SerializeField] private EnemyData testEnemy;

        public event Action<GamePhase> OnPhaseChanged;

        public GamePhase Phase { get; private set; } = GamePhase.Run;

        private void Start()
        {
            EnterArena();
        }
        private void ShowHarvestOverlay(bool on)
        {
            if (!_harvestOverlay) return;
            _harvestOverlay.gameObject.SetActive(true);
            _harvestOverlay.alpha = on ? 1f : 0f;
            _harvestOverlay.blocksRaycasts = on;
            _harvestOverlay.interactable = on;
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
                ShowHarvestOverlay(true);
                _harvestGlue?.SignalVictory();   // ← start QTE/placeholder → picker
            }
            else
            {
                SetPhase(GamePhase.Stitch);
                ShowHarvestOverlay(true);
                _harvestGlue?.SignalDefeat();    // ← add curse based on threat mark
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


