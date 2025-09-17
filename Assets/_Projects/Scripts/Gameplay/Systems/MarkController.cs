using System;
using UnityEngine;

namespace WH.Gameplay.Systems
{
    /// <summary>Tracks threat marks (enemy) and harvest bias (player).</summary>
    [DisallowMultipleComponent]
    public sealed class MarkController : MonoBehaviour
    {
        public event Action<WH.Gameplay.BodyTag?> OnThreatMarkChanged;
        public event Action<WH.Gameplay.CardFamily?> OnHarvestBiasChanged;

        public WH.Gameplay.BodyTag? ThreatMark { get; private set; }
        public WH.Gameplay.CardFamily? HarvestBias { get; private set; }

        public void SetThreatMark(WH.Gameplay.BodyTag? tag)
        {
            ThreatMark = tag;
            OnThreatMarkChanged?.Invoke(ThreatMark);
            Debug.Log($"[Mark] Threat → {ThreatMark}");
        }

        public void SetHarvestBias(WH.Gameplay.CardFamily? family)
        {
            HarvestBias = family;
            OnHarvestBiasChanged?.Invoke(HarvestBias);
            Debug.Log($"[Mark] HarvestBias → {HarvestBias}");
        }

        public void ClearAll()
        {
            SetThreatMark(null);
            SetHarvestBias(null);
        }
    }
}

