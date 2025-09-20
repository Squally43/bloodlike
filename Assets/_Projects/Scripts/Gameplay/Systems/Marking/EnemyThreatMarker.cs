using UnityEngine;
using WH.Gameplay;

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// MVP: enemy assigns ONE random threat mark (Skin or Eye) the first time
    /// the player's turn ends. This is intentionally predictable and light-touch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyThreatMarker : MonoBehaviour
    {
        [SerializeField] private TurnManager _turns;
        [SerializeField] private MarkController _marks;

        [Tooltip("If true, log which turn applied the threat mark.")]
        [SerializeField] private bool _log = true;

        private bool _appliedThisFight;

        private void Awake()
        {
            if (_turns == null) _turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);
            if (_marks == null) _marks = FindAnyObjectByType<MarkController>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (_turns != null)
            {
                _turns.OnPlayerTurnEnded += HandlePlayerTurnEnded;
                _turns.OnPlayerTurnStarted += HandlePlayerTurnStarted; // to reset per fight
            }
        }

        private void OnDisable()
        {
            if (_turns != null)
            {
                _turns.OnPlayerTurnEnded -= HandlePlayerTurnEnded;
                _turns.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
            }
        }

        private void HandlePlayerTurnStarted()
        {
            // New fight usually starts on Player Turn; reset our per-fight flag.
            _appliedThisFight = false;
        }

        private void HandlePlayerTurnEnded()
        {
            if (_appliedThisFight || _marks == null) return;

            // MVP rule: 50/50 Skin or Eye.
            var pick = (Random.value < 0.5f) ? BodyTag.Skin : BodyTag.Eye;
            _marks.ApplyEnemyThreatMark(pick);
            _appliedThisFight = true;

            if (_log) Debug.Log($"[ThreatMarker] Applied threat mark: {pick}");
        }
    }
}

