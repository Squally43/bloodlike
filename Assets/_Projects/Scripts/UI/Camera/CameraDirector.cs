using UnityEngine;
using Unity.Cinemachine;

namespace WH.UI
{
    public sealed class CameraDirector : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _cmCombat;
        [SerializeField] private CinemachineCamera _cmHarvest;
        [SerializeField] private int _combatPri = 10, _harvestPri = 5, _harvestActivePri = 20;

        private void OnEnable()
        {
            WH.GameSignals.OnPlayerTurnStarted += SetCombatView;
            WH.GameSignals.OnBattleEnded += HandleBattleEnded;
            WH.GameSignals.OnNextFightReadied += SetCombatView;
        }
        private void OnDisable()
        {
            WH.GameSignals.OnPlayerTurnStarted -= SetCombatView;
            WH.GameSignals.OnBattleEnded -= HandleBattleEnded;
            WH.GameSignals.OnNextFightReadied -= SetCombatView;
        }

        private void Start() => SetCombatView();

        private void HandleBattleEnded(bool _playerWon) => SetHarvestView();

        public void SetCombatView()
        {
            if (_cmCombat) _cmCombat.Priority = _combatPri;
            if (_cmHarvest) _cmHarvest.Priority = _harvestPri;
        }
        public void SetHarvestView()
        {
            if (_cmHarvest) _cmHarvest.Priority = _harvestActivePri;
            if (_cmCombat) _cmCombat.Priority = _combatPri;
        }
    }
}




