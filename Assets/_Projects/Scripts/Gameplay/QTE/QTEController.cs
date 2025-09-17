using System;
using System.Collections.Generic;
using UnityEngine;

namespace WH.Gameplay.QTE
{
    public enum QteMode { Slider, Hold, Mash }

    /// <summary>Generic QTE entry point. UI comes later.</summary>
    [DisallowMultipleComponent]
    public sealed class QTEController : MonoBehaviour
    {
        public event Action<bool, IReadOnlyList<WH.Gameplay.CardFamily>> OnQteResolved;

        public void StartQte(QteMode mode, float difficulty, IReadOnlyList<WH.Gameplay.CardFamily> preferred)
        {
            // Placeholder. Later hook to UI overlay and input.
            bool success = UnityEngine.Random.value > difficulty;
            OnQteResolved?.Invoke(success, preferred);
            Debug.Log($"[QTE] Mode={mode} diff={difficulty} success={success}");
        }
    }
}

