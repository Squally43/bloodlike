using UnityEngine;
using WH.Gameplay.Systems;

namespace WH.Gameplay.Cards
{
    /// <summary>
    /// Resolves card plays. Uses TurnManager for damage and modifies Block directly.
    /// Minimal MVP: if a card has Damage > 0, hit the enemy; if Block > 0, give it to player.
    /// Costs are paid via PulseManager.
    /// </summary>
    public sealed class CardResolver : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private TurnManager _turns;   // assign in inspector or auto-find
        [SerializeField] private PulseManager _pulse;  // assign in inspector or auto-find

        private void Awake()
        {
            if (_turns == null)
                _turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);
            if (_pulse == null)
                _pulse = FindAnyObjectByType<PulseManager>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Try to play a card. Returns true if the play resolved (cost paid and effects applied).
        /// </summary>
        public bool TryPlay(CardData card)
        {
            if (card == null || _turns == null) return false;

            // Use the SAME PulseManager as TurnManager
            var pm = _turns.Pulse != null ? _turns.Pulse : _pulse;

            int cost = card.CostPulse;   // ✅ your schema
            int dmg = card.BaseDamage;  // ✅ your schema
            int block = card.BaseBlock;   // ✅ your schema

            // Gate + spend atomically
            if (cost > 0)
            {
                if (pm == null) { Debug.LogWarning("[Resolver] No PulseManager set."); return false; }
                if (!pm.TrySpend(cost)) return false;
            }

            bool didSomething = false;

            if (dmg > 0)
            {
                _turns.DealDamageToEnemy(dmg);
                didSomething = true;
            }

            if (block > 0 && _turns.PlayerState != null)
            {
                _turns.PlayerState.Block += block;
                didSomething = true;
            }

            // If you have an effects list you want to process next, you can iterate card.Effects here.

            // Even if a card has no base numbers, we still succeed so the UI flow continues.
            if (!didSomething)
                Debug.Log($"[Resolver] Played '{card.DisplayName}' – no base effects (BaseDamage/BaseBlock both 0).");

            return true;
        }


        // Helper overload to probe several names
        private static int GetInt(object obj, params string[] names)
        {
            if (obj == null || names == null) return 0;
            var t = obj.GetType();
            foreach (var n in names)
            {
                var p = t.GetProperty(n);
                if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
                var f = t.GetField(n);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
            }
            return 0;
        }

    }
}



