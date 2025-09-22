using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Systems
{
    [DisallowMultipleComponent]
    public sealed class HarvestGlue : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private MarkController _marks;
        [SerializeField] private HarvestController _harvest;
        [SerializeField] private MonoBehaviour _pickerComponent;   // RewardPicker or its GO (optional)
        [SerializeField] private CanvasGroup _overlay;             // Canvas_HarvestOverlay (optional)
        [SerializeField] private CardCatalog _catalog;

        [Header("Deck receiver (who owns next-fight deck)")]
        [SerializeField] private MonoBehaviour _deckReceiver;      // BattleHUD (preferred) or DeckTestDriver
        private INextFightQueue _queue;
        private IRewardPicker _picker;

        [Header("Debug")]
        [SerializeField] private bool _log = true;

        // prevents double-queueing when multiple signals fire
        private bool _harvestLocked;

        private void Awake()
        {
            if (!_marks) _marks = FindAnyObjectByType<MarkController>(FindObjectsInactive.Include);
            if (!_harvest) _harvest = FindAnyObjectByType<HarvestController>(FindObjectsInactive.Include);

            ResolveQueue();
            ResolvePicker();
            HideOverlay();
        }

        private void OnEnable()
        {
            WH.GameSignals.OnBattleEnded += OnBattleEnded;
            WH.GameSignals.OnPlayerTurnStarted += UnlockHarvest;
            WH.GameSignals.OnNextFightReadied += UnlockHarvest;

            if (_harvest) _harvest.OnHarvestResolved += OnHarvestResolved;
            if (_picker != null) _picker.OnChosen += OnRewardChosen;
        }

        private void OnDisable()
        {
            WH.GameSignals.OnBattleEnded -= OnBattleEnded;
            WH.GameSignals.OnPlayerTurnStarted -= UnlockHarvest;
            WH.GameSignals.OnNextFightReadied -= UnlockHarvest;

            if (_harvest) _harvest.OnHarvestResolved -= OnHarvestResolved;
            if (_picker != null) _picker.OnChosen -= OnRewardChosen;
        }

        private void OnBattleEnded(bool playerWon)
        {
            if (_harvestLocked) return;             // ← stop duplicates
            _harvestLocked = true;

            if (_log) Debug.Log($"[HarvestGlue] BattleEnded playerWon={playerWon}");
            ShowOverlay();
            if (playerWon) StartVictory(); else StartDefeat();
        }

        private void UnlockHarvest()
        {
            _harvestLocked = false;
        }

        // ----- victory -----
        private void StartVictory()
        {
            var preferred = BuildPreferredFromPlayerMark();
            if (_log) Debug.Log($"[HarvestGlue] Victory → Harvest ({string.Join(",", preferred)})");
            _harvest.BeginHarvest(preferred);
        }

        private void OnHarvestResolved(bool success, IReadOnlyList<CardFamily> preferred)
        {
            if (_log) Debug.Log($"[HarvestGlue] QTE resolved success={success}");
            if (!success) { _marks?.ClearAll(); HideOverlay(); return; }

            if (_picker != null) { _picker.Show(preferred, 3); return; }

            // fallback: auto-pick one and queue it
            var pick = PickOne(preferred);
            if (pick != null && _queue != null)
            {
                _queue.QueueCardNextFight(pick);
                if (_log) Debug.Log($"[HarvestGlue] Auto-picked {pick.DisplayName}");
            }
            _marks?.ClearAll();
            HideOverlay();
        }

        private void OnRewardChosen(CardData chosen)
        {
            if (chosen != null && _queue != null)
            {
                _queue.QueueCardNextFight(chosen);
                if (_log) Debug.Log($"[HarvestGlue] Added reward: {chosen.DisplayName}");
            }
            _marks?.ClearAll();
            HideOverlay();
        }

        // ----- defeat -----
        private void StartDefeat()
        {
            var curse = PickCurseFromThreat();
            if (curse != null && _queue != null)
            {
                _queue.QueueCardNextFight(curse);
                if (_log) Debug.Log($"[HarvestGlue] Added curse: {curse.DisplayName}");
            }
            _marks?.ClearAll();
            HideOverlay();
        }

        // ----- resolve picker / queue targets -----
        private void ResolvePicker()
        {
            _picker = _pickerComponent as IRewardPicker;
            if (_picker == null && _pickerComponent is Component c) _picker = c.GetComponent<IRewardPicker>();
            if (_picker == null)
            {
                var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mb in all) { if (mb is IRewardPicker ip) { _picker = ip; break; } }
            }
            if (_log) Debug.Log(_picker != null ? "[HarvestGlue] Picker resolved." : "[HarvestGlue] No picker found.");
        }

        private void ResolveQueue()
        {
            _queue = _deckReceiver as INextFightQueue;
            if (_queue == null && _deckReceiver is Component c) _queue = c.GetComponent<INextFightQueue>();

            if (_queue == null)
            {
                INextFightQueue hud = null, driver = null;
                var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mb in all)
                {
                    if (mb is INextFightQueue q)
                    {
                        var n = mb.GetType().Name;
                        if (n.Contains("BattleHUD")) hud = q;
                        else if (n.Contains("DeckTestDriver")) driver = q;
                    }
                }
                _queue = hud ?? driver;
            }

            if (_log) Debug.Log(_queue != null ? $"[HarvestGlue] Deck receiver → {_queue.GetType().Name}" :
                                                 "[HarvestGlue] No deck receiver.");
        }

        // ----- overlay -----
        private void ShowOverlay()
        {
            if (!_overlay) return;
            _overlay.gameObject.SetActive(true);
            _overlay.alpha = 1f;
            _overlay.blocksRaycasts = true;
            _overlay.interactable = true;
        }

        private void HideOverlay()
        {
            if (!_overlay) return;
            _overlay.blocksRaycasts = false;
            _overlay.interactable = false;
            _overlay.alpha = 0f;
            _overlay.gameObject.SetActive(false);
        }

        // ----- helpers -----
        private List<CardFamily> BuildPreferredFromPlayerMark()
        {
            var list = new List<CardFamily>(2);
            if (_marks && _marks.PlayerHarvestMarkOnEnemy.HasValue)
            {
                var t = _marks.PlayerHarvestMarkOnEnemy.Value;
                if (t == BodyTag.Skin) list.Add(CardFamily.Skin);
                if (t == BodyTag.Eye) list.Add(CardFamily.Eye);
            }
            if (list.Count == 0) { list.Add(CardFamily.Skin); list.Add(CardFamily.Eye); }
            return list;
        }

        private CardData PickCurseFromThreat()
        {
            if (_catalog == null || _catalog.negatives == null || _catalog.negatives.Count == 0) return null;
            var want = (_marks && _marks.EnemyThreatMarkOnPlayer == BodyTag.Eye) ? "Parasite" : "Wound";
            for (int i = 0; i < _catalog.negatives.Count; i++)
            {
                var c = _catalog.negatives[i];
                if (c != null && c.DisplayName.Contains(want)) return c;
            }
            return _catalog.negatives[Random.Range(0, _catalog.negatives.Count)];
        }

        private CardData PickOne(IReadOnlyList<CardFamily> fams)
        {
            if (_catalog == null) return null;
            var pool = new List<CardData>();

            if (fams != null)
            {
                foreach (var f in fams)
                {
                    if (f == CardFamily.Skin && _catalog.skinRewards != null) pool.AddRange(_catalog.skinRewards);
                    if (f == CardFamily.Eye && _catalog.eyeRewards != null) pool.AddRange(_catalog.eyeRewards);
                }
            }
            if (pool.Count == 0)
            {
                if (_catalog.skinRewards != null) pool.AddRange(_catalog.skinRewards);
                if (_catalog.eyeRewards != null) pool.AddRange(_catalog.eyeRewards);
            }
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        // dev helpers (keep keys working)
        public void SignalVictory() { if (!_harvestLocked) { _harvestLocked = true; ShowOverlay(); StartVictory(); } }
        public void SignalDefeat() { if (!_harvestLocked) { _harvestLocked = true; ShowOverlay(); StartDefeat(); } }
    }
}






