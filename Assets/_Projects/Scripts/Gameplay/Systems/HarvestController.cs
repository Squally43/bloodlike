using System;
using System.Collections.Generic;
using UnityEngine;

namespace WH.Gameplay.Systems
{
    /// <summary>Starts a QTE and reports the result with suggested families.</summary>
    [DisallowMultipleComponent]
    public sealed class HarvestController : MonoBehaviour
    {
        [SerializeField] private WH.Gameplay.QTE.QTEController _qte;

        public event Action<bool, IReadOnlyList<WH.Gameplay.CardFamily>> OnHarvestResolved;

        public void BeginHarvest(IReadOnlyList<WH.Gameplay.CardFamily> preferred)
        {
            if (_qte == null)
            {
                Debug.LogError("HarvestController missing QTEController");
                return;
            }

            _qte.OnQteResolved -= HandleQteResolved;
            _qte.OnQteResolved += HandleQteResolved;

            // For now always start the same mode. Difficulty tuning later.
            _qte.StartQte(WH.Gameplay.QTE.QteMode.Slider, 0.5f, preferred);
        }

        private void HandleQteResolved(bool success, IReadOnlyList<WH.Gameplay.CardFamily> preferred)
        {
            _qte.OnQteResolved -= HandleQteResolved;
            OnHarvestResolved?.Invoke(success, preferred);
            Debug.Log($"[Harvest] QTE success={success}");
        }
    }
}

