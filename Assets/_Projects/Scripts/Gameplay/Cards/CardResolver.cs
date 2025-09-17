using System;
using UnityEngine;
using WH.Gameplay.Cards;
using WH.Gameplay.Cards.Piles;
using WH.Gameplay.Systems;

namespace WH.Gameplay.Cards
{
    /// <summary>Resolves CardData effects in order. No visuals yet.</summary>
    [DisallowMultipleComponent]
    public sealed class CardResolver : MonoBehaviour
    {
        [SerializeField] private PulseManager _pulse;

        // Optional scene refs. If unassigned, auto-find at Awake.
        [SerializeField] private TurnManager _turns;

        private CombatantState _player;
        private CombatantState _enemy;

        public event Action<CardData> OnCardPlayed;

        private void Awake()
        {
            if (_pulse == null)
                _pulse = FindAnyObjectByType<PulseManager>(FindObjectsInactive.Include);

            if (_turns == null)
                _turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);

            if (_turns != null)
            {
                _player = _turns.PlayerState ?? FindAnyObjectByType<CombatantState>(FindObjectsInactive.Include | FindObjectsInactive.Exclude);
                _enemy = _turns.EnemyState ?? _player; // will be corrected below
            }

            if (_player == null || _enemy == null)
            {
                var all = FindObjectsByType<CombatantState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in all)
                {
                    if (c.IsPlayer) _player = c;
                    else _enemy = c;
                }
            }
        }

        public bool TryPlay(CardData card)
        {
            if (card == null) return false;
            if (!_pulse.TrySpend(card.CostPulse))
            {
                Debug.Log("[Card] Not enough Pulse");
                return false;
            }

            ResolveEffects(card);

            OnCardPlayed?.Invoke(card);

            // After effects, check win/lose
            _turns?.GetType().GetMethod("EvaluateBattleEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                   ?.Invoke(_turns, null);

            Debug.Log($"[Card] Played {card.DisplayName}");
            return true;
        }

        private void ResolveEffects(CardData card)
        {
            foreach (var eff in card.Effects)
            {
                switch (eff.type)
                {
                    case CardEffectType.Damage:
                        ApplyDamageEffect(eff);
                        break;

                    case CardEffectType.GainBlock:
                        ApplyGainBlockEffect(eff);
                        break;

                    // Add more ops later: Draw, Scry, ApplyStatus, etc.
                    default:
                        Debug.Log($"[Card] Effect {eff.type} not implemented yet");
                        break;
                }
            }
        }

        private void ApplyDamageEffect(CardEffectDef eff)
        {
            var value = Mathf.Max(0, eff.value);
            switch (eff.target)
            {
                case EffectTarget.Enemy:
                    _enemy?.ApplyDamage(value);
                    break;
                case EffectTarget.Self:
                    _player?.ApplyDamage(value);
                    break;
                default:
                    Debug.Log("[Card] Damage target not supported in this slice");
                    break;
            }
        }

        private void ApplyGainBlockEffect(CardEffectDef eff)
        {
            var value = Mathf.Max(0, eff.value);
            switch (eff.target)
            {
                case EffectTarget.Self:
                    _player?.GainBlock(value);
                    break;
                case EffectTarget.Enemy:
                    _enemy?.GainBlock(value);
                    break;
                default:
                    Debug.Log("[Card] GainBlock target not supported in this slice");
                    break;
            }
        }
    }
}


