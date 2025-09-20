using System;
using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay;           // CardFamily
using WH.Gameplay.Cards;     // CardData

namespace WH.UI
{
    /// <summary>
    /// Hand-style reward picker.
    /// - Spawns CardView prefabs under a "hand" (RectTransform).
    /// - Registers each with HandLayoutController for fan/hover.
    /// - Plays deal stagger via DealDiscardController.
    /// - Click -> raises OnChosen and tears down.
    ///
    /// Implements Gameplay-facing interface so Gameplay can call us without a UI dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardPicker : MonoBehaviour, WH.Gameplay.Systems.IRewardPicker
    {
        [Header("Data")]
        [SerializeField] private CardCatalog _catalog;

        [Header("Hand UI")]
        [SerializeField] private RectTransform _handRoot;                // set to UI_Reward_Choices
        [SerializeField] private HandLayoutController _handLayout;       // on same GO as handRoot
        [SerializeField] private DealDiscardController _dealDiscard;     // on same GO as handRoot
        [SerializeField] private CardView _cardPrefab;                   // CardView prefab
        [SerializeField] private float _spawnStagger = 0.0f;             // optional one-shot layout stagger

        public event Action<CardData> OnChosen;

        // runtime
        private readonly List<CardView> _spawned = new();
        private readonly List<RectTransform> _spawnedRects = new();

        // ---------- IRewardPicker ----------
        public void Show(IReadOnlyList<CardFamily> families, int count = 3)
        {
            if (_catalog == null) { Debug.LogWarning("[RewardPicker] No CardCatalog assigned."); AutoPick(null); return; }
            if (!_handRoot || !_handLayout || !_dealDiscard || !_cardPrefab)
            {
                Debug.LogWarning("[RewardPicker] Hand UI not wired (root/layout/deal/prefab). Falling back to auto-pick.");
                var optionsFallback = BuildOptions(families, count);
                AutoPick(optionsFallback.Count > 0 ? optionsFallback[0] : null);
                return;
            }

            gameObject.SetActive(true);
            ClearSpawned();

            var options = BuildOptions(families, count);

            // Spawn into hand root
            for (int i = 0; i < options.Count; i++)
            {
                var data = options[i];
                var view = Instantiate(_cardPrefab, _handRoot);
                _spawned.Add(view);
                var rt = (RectTransform)view.transform;
                _spawnedRects.Add(rt);

                // Prep for deal
                _dealDiscard.InitCardForDeal(rt);

                // Hook hover FX for layout (if the prefab has CardFxController)
                var fx = view.GetComponent<CardFxController>();
                _handLayout.RegisterCard(rt, fx);

                // Family tint (fallback to BaseTint if set)
                var tint = (data.BaseTint != Color.white) ? data.BaseTint : CardFamilyColors.Get(data.Family);

                // Bind + click
                var captured = data;
                view.Bind(captured, captured.DisplayName, captured.RulesLine, captured.CostPulse, "", tint, _ => Choose(captured));
            }

            // Layout & deal
            _handLayout.SetNextLayoutStagger(_spawnStagger);
            _handLayout.OnHandRebuilt();
            _dealDiscard.PlayDealStagger(_spawnedRects, this);
        }

        public void Hide()
        {
            // Nice: animate out to discard, then destroy
            if (_dealDiscard != null) _dealDiscard.DiscardAndDespawnMany(_spawnedRects);
            else
            {
                for (int i = 0; i < _spawned.Count; i++)
                    if (_spawned[i]) Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();
            _spawnedRects.Clear();

            gameObject.SetActive(false);
        }

        // ---------- internals ----------
        private void Choose(CardData data)
        {
            Debug.Log($"[RewardPicker] Clicked reward: {data?.DisplayName}");
            OnChosen?.Invoke(data);
            Hide();
        }

        private void AutoPick(CardData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[RewardPicker] No options available to auto-pick.");
                OnChosen?.Invoke(null);
                return;
            }
            Debug.Log($"[RewardPicker] Auto-picked: {data.DisplayName}");
            OnChosen?.Invoke(data);
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i]) Destroy(_spawned[i].gameObject);
            _spawned.Clear();
            _spawnedRects.Clear();
        }

        private List<CardData> BuildOptions(IReadOnlyList<CardFamily> families, int count)
        {
            var result = new List<CardData>(count);
            var pools = new List<List<CardData>>(2);

            if (families != null && families.Count > 0)
            {
                for (int i = 0; i < families.Count; i++)
                {
                    var f = families[i];
                    if (f == CardFamily.Skin) pools.Add(_catalog.skinRewards);
                    else if (f == CardFamily.Eye) pools.Add(_catalog.eyeRewards);
                }
            }
            if (pools.Count == 0) { pools.Add(_catalog.skinRewards); pools.Add(_catalog.eyeRewards); }

            var used = new HashSet<CardData>();
            int p = 0;
            while (result.Count < count && pools.Count > 0)
            {
                var pool = pools[p % pools.Count]; p++;
                if (pool == null || pool.Count == 0) continue;

                var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                if (!pick || used.Contains(pick)) continue;

                used.Add(pick);
                result.Add(pick);
            }
            return result;
        }
    }
}



