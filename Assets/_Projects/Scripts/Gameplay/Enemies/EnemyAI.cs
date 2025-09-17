using System;
using UnityEngine;

namespace WH.Gameplay.Enemies
{
    /// <summary>Simple intent iterator over EnemyData.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAI : MonoBehaviour
    {
        [SerializeField] private EnemyData _data;

        public event Action<WH.Gameplay.EnemyIntentStep> OnIntentSelected;

        private int _index;

        public void SetData(EnemyData data)
        {
            _data = data;
            _index = 0;
        }

        public void SelectNextIntent()
        {
            if (_data == null || _data.Intents.Count == 0) return;

            WH.Gameplay.EnemyIntentStep step;
            if (_data.RandomizeIntents)
            {
                var r = UnityEngine.Random.Range(0, _data.Intents.Count);
                step = _data.Intents[r];
            }
            else
            {
                step = _data.Intents[_index % _data.Intents.Count];
                _index++;
            }

            OnIntentSelected?.Invoke(step);
            Debug.Log($"[EnemyAI] Intent → {step.type} {step.value}x{Mathf.Max(1, step.times)}");
        }
    }
}

