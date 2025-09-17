using UnityEngine;

namespace WH.Gameplay.Systems
{
    /// <summary>Holds HP and Block for a combatant. No visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatantState : MonoBehaviour
    {
        [SerializeField] private bool _isPlayer = true;
        [SerializeField] private int _maxHp = 40;

        public bool IsPlayer => _isPlayer;
        public int MaxHp => _maxHp;
        public int Hp { get; private set; }
        public int Block { get; private set; }
        public bool IsDead => Hp <= 0;

        public void Init(int maxHp)
        {
            _maxHp = Mathf.Max(1, maxHp);
            Hp = _maxHp;
            Block = 0;
            Debug.Log($"[HP] Init {(IsPlayer ? "Player" : "Enemy")} {Hp}/{MaxHp}");
        }

        public void GainBlock(int amount)
        {
            if (amount <= 0) return;
            Block += amount;
            Debug.Log($"[Block] {(IsPlayer ? "Player" : "Enemy")} +{amount} -> {Block}");
        }

        public int ApplyDamage(int amount)
        {
            if (amount <= 0) return 0;

            var absorbed = Mathf.Min(Block, amount);
            Block -= absorbed;
            var spill = amount - absorbed;

            if (spill > 0)
            {
                Hp = Mathf.Max(0, Hp - spill);
                Debug.Log($"[HP] {(IsPlayer ? "Player" : "Enemy")} took {spill}. {Hp}/{MaxHp}");
            }
            else
            {
                Debug.Log($"[Block] {(IsPlayer ? "Player" : "Enemy")} absorbed {absorbed}. Block {Block}");
            }

            return spill;
        }

        public void ClearBlock()
        {
            if (Block <= 0) return;
            Block = 0;
            Debug.Log($"[Block] {(IsPlayer ? "Player" : "Enemy")} cleared");
        }
    }
}

