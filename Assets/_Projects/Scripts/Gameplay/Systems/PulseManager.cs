using System;
using UnityEngine;

namespace WH.Gameplay.Systems
{
    /// <summary>Manages action currency per turn.</summary>
    [DisallowMultipleComponent]
    public sealed class PulseManager : MonoBehaviour
    {
        [SerializeField] private int _basePulse = 3;

        public event Action<int, int> OnPulseChanged; // current, max

        public int Current { get; private set; }
        public int Max => _basePulse;

        public void ResetForNewTurn()
        {
            Current = _basePulse;
            OnPulseChanged?.Invoke(Current, Max);
            Debug.Log($"[Pulse] Reset to {Current}");
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (Current < amount) return false;
            Current -= amount;
            OnPulseChanged?.Invoke(Current, Max);
            return true;
        }

        public void Gain(int amount)
        {
            if (amount <= 0) return;
            Current = Mathf.Min(Current + amount, Max);
            OnPulseChanged?.Invoke(Current, Max);
        }

        public void SetBasePulse(int value)
        {
            _basePulse = Mathf.Max(0, value);
            OnPulseChanged?.Invoke(Current, Max);
        }
    }
}
