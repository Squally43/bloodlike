using TMPro;
using UnityEngine;
using WH.Gameplay;
using WH.Gameplay.Systems;

namespace WH.UI
{
    /// <summary>
    /// Minimal readout for Phase 4: shows both marks.
    /// Assign two TMP_Texts in the inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarkIconsBinder : MonoBehaviour
    {
        [SerializeField] private MarkController _marks;
        [Header("UI")]
        [SerializeField] private TMP_Text _txtPlayerMark;  // near enemy portrait
        [SerializeField] private TMP_Text _txtThreatMark;  // near player diagram

        private void Awake()
        {
            if (_marks == null) _marks = FindAnyObjectByType<MarkController>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (_marks == null) return;
            _marks.OnPlayerHarvestMarkChanged += UpdatePlayerMark;
            _marks.OnEnemyThreatMarkChanged += UpdateThreatMark;

            // Prime
            UpdatePlayerMark(_marks.PlayerHarvestMarkOnEnemy);
            UpdateThreatMark(_marks.EnemyThreatMarkOnPlayer);
        }

        private void OnDisable()
        {
            if (_marks == null) return;
            _marks.OnPlayerHarvestMarkChanged -= UpdatePlayerMark;
            _marks.OnEnemyThreatMarkChanged -= UpdateThreatMark;
        }

        private void UpdatePlayerMark(BodyTag? tag)
        {
            if (_txtPlayerMark)
                _txtPlayerMark.text = tag.HasValue ? $"Mark: {tag.Value}" : "Mark: —";
        }

        private void UpdateThreatMark(BodyTag? tag)
        {
            if (_txtThreatMark)
                _txtThreatMark.text = tag.HasValue ? $"Threat: {tag.Value}" : "Threat: —";
        }
    }
}

