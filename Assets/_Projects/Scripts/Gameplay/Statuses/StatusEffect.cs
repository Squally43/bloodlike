using UnityEngine;

namespace WH.Gameplay.Statuses
{
    /// <summary>Runtime status base. Concrete effects will extend this.</summary>
    public abstract class StatusEffect
    {
        public WH.Gameplay.StatusKind Kind { get; }
        public int Stacks { get; private set; }

        protected StatusEffect(WH.Gameplay.StatusKind kind, int stacks)
        {
            Kind = kind;
            Stacks = Mathf.Max(1, stacks);
        }

        public virtual void OnApply() { }
        public virtual void OnExpire() { }
        public virtual void OnOwnerTurnStart() { }
        public virtual void OnOwnerTurnEnd() { }

        public void AddStacks(int amount)
        {
            Stacks = Mathf.Max(1, Stacks + amount);
        }
    }
}

