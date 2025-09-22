using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay;              // BodyTag
using WH.Gameplay.Cards;        // CardResolver
using WH.Gameplay.Systems;      // MarkController

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// Bridges CardResolver signals to the MarkController so Glare actually sets the harvest mark.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class ResolverToMarksBridge : MonoBehaviour
    {
        [SerializeField] private MarkController _marks;
        [SerializeField] private bool _log = true;

        private readonly List<CardResolver> _resolvers = new();

        private void Awake()
        {
            if (_marks == null)
                _marks = FindAnyObjectByType<MarkController>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            Resubscribe();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void Resubscribe()
        {
            UnsubscribeAll();

            var found = FindObjectsByType<CardResolver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found != null)
            {
                for (int i = 0; i < found.Length; i++)
                {
                    var r = found[i];
                    if (r == null) continue;
                    r.OnMarkEnemy += HandleMarkEnemy;
                    _resolvers.Add(r);
                }
            }

            if (_log) Debug.Log($"[Bridge] Subscribed to {_resolvers.Count} CardResolver(s).");
        }

        private void UnsubscribeAll()
        {
            for (int i = 0; i < _resolvers.Count; i++)
            {
                var r = _resolvers[i];
                if (r != null) r.OnMarkEnemy -= HandleMarkEnemy;
            }
            _resolvers.Clear();
        }

        private void HandleMarkEnemy(BodyTag tag)
        {
            if (_marks == null)
            {
                Debug.LogWarning("[Bridge] No MarkController in scene; cannot apply harvest mark.");
                return;
            }

            _marks.ApplyPlayerHarvestMark(tag);
            if (_log) Debug.Log($"[Bridge] Applied PlayerHarvestMarkOnEnemy={tag}");
        }
    }
}

