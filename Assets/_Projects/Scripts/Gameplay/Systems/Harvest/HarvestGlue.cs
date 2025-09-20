using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// Lives in the MAIN scene.
    /// Listens to TurnManager.OnBattleEnded and runs the harvest flow locally:
    ///  - Victory  -> show overlay -> begin QTE -> open picker (honest case)
    ///  - Defeat   -> show overlay -> add curse based on threat mark
    /// Also clears marks at the end of each path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarvestGlue : MonoBehaviour
    {
        [Header("Scene Refs (Main scene)")]
        [SerializeField] private TurnManager _turns;
        [SerializeField] private MarkController _marks;
        [SerializeField] private HarvestController _harvest;

        [Tooltip("Drag the UI_Reward_Choices GameObject or the RewardPicker component here.")]
        [SerializeField] private MonoBehaviour _pickerComponent; // anything on the GO
        private IRewardPicker _picker;

        [SerializeField] private CanvasGroup _harvestOverlay;     // Canvas_HarvestOverlay (optional)
        [SerializeField] private CardCatalog _catalog;
        [SerializeField] private DeckTestDriver _deckDriver;      // temp driver for queueing next-fight cards
        [SerializeField] private MonoBehaviour _deckReceiver; // drag DeckTestDriver OR BattleHUD here
        private INextFightQueue _queue;

        [Header("Debug")]
        [SerializeField] private bool _log = true;

        private void Awake()
        {
            if (!_turns) _turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);
            if (!_marks) _marks = FindAnyObjectByType<MarkController>(FindObjectsInactive.Include);
            if (!_harvest) _harvest = FindAnyObjectByType<HarvestController>(FindObjectsInactive.Include);
            if (!_deckDriver) _deckDriver = FindAnyObjectByType<DeckTestDriver>(FindObjectsInactive.Include);
            // Try the assigned component first
            _queue = _deckReceiver as INextFightQueue;
            // If they dragged the GO or a different component on the same GO:
            if (_queue == null && _deckReceiver is Component c) _queue = c.GetComponent<INextFightQueue>();
            // Fallback: find anyone in scene
            if (_queue == null)
            {
                var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mb in all) { if (mb is INextFightQueue q) { _queue = q; break; } }
            }

            ResolvePicker();
            HideOverlay(); // ensure off on load
        }

        private void OnEnable()
        {
            WH.GameSignals.OnBattleEnded += HandleBattleEnded;   // victory/defeat entry
            if (_harvest) _harvest.OnHarvestResolved += HandleHarvestResolved;
            if (_picker != null) _picker.OnChosen += HandleRewardChosen;
        }
        private void OnDisable()
        {
            WH.GameSignals.OnBattleEnded -= HandleBattleEnded;
            if (_harvest) _harvest.OnHarvestResolved -= HandleHarvestResolved;
            if (_picker != null) _picker.OnChosen -= HandleRewardChosen;
        }


        // ------------------------------------------------------------------
        // Battle end entry point (from TurnManager in this scene)
        // ------------------------------------------------------------------
        private void HandleBattleEnded(bool playerWon)
        {
            if (_log) Debug.Log($"[HarvestGlue] BattleEnded playerWon={playerWon}");
            ShowOverlay();
            if (playerWon) RunVictoryPath();
            else RunDefeatPath();
        }

        // ------------------------------------------------------------------
        // Harvest → Picker (victory path)
        // ------------------------------------------------------------------
        private void HandleHarvestResolved(bool success, IReadOnlyList<CardFamily> preferred)
        {
            if (_log) Debug.Log($"[HarvestGlue] Harvest resolved. success={success}");

            if (!success)
            {
                _marks?.ClearAll();
                HideOverlay();
                return;
            }

            // Honest MVP: 1-of-3 from preferred family (or mixed if none)
            if (_picker != null) _picker.Show(preferred, count: 3);
            else
            {
                // Dev fallback: pick one automatically so loop progresses
                var pick = PickRandomFromFamilies(preferred);
                if (pick && _deckDriver != null)
                {
                    _queue?.QueueCardNextFight(pick);
                    if (_log) Debug.Log($"[HarvestGlue] (No picker) Auto-picked {pick.DisplayName}.");
                }
                _marks?.ClearAll();
                HideOverlay();
            }
        }

        private void HandleRewardChosen(CardData chosen)
        {
            if (chosen && _deckDriver != null)
            {
                _queue?.QueueCardNextFight(chosen);
                if (_log) Debug.Log($"[HarvestGlue] Reward chosen: {chosen.DisplayName} → queued for next fight.");
            }
            _marks?.ClearAll();
            HideOverlay();
        }

        // ------------------------------------------------------------------
        // Picker resolution + overlay helpers
        // ------------------------------------------------------------------
        private void ResolvePicker()
        {
            _picker = null;

            // If they dragged a specific component that implements the interface, good.
            _picker = _pickerComponent as IRewardPicker;

            // Else look on the same GO:
            if (_picker == null && _pickerComponent is Component cOnGo)
                _picker = cOnGo.GetComponent<IRewardPicker>();

            // Else find anything in scene:
            if (_picker == null)
            {
                var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mb in all) { if (mb is IRewardPicker ip) { _picker = ip; break; } }
            }

            if (_log) Debug.Log(_picker != null ? "[HarvestGlue] Picker resolved." : "[HarvestGlue] No IRewardPicker found.");
        }

        private void ShowOverlay()
        {
            if (!_harvestOverlay) return;
            _harvestOverlay.gameObject.SetActive(true);
            _harvestOverlay.alpha = 1f;
            _harvestOverlay.blocksRaycasts = true;
            _harvestOverlay.interactable = true;
        }

        private void HideOverlay()
        {
            if (!_harvestOverlay) return;
            _harvestOverlay.blocksRaycasts = false;
            _harvestOverlay.interactable = false;
            _harvestOverlay.alpha = 0f;
            _harvestOverlay.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Small helpers
        // ------------------------------------------------------------------
        private List<CardFamily> BuildPreferredListFromPlayerMark()
        {
            var list = new List<CardFamily>(2);
            if (_marks && _marks.PlayerHarvestMarkOnEnemy.HasValue)
            {
                var f = BodyToFamily(_marks.PlayerHarvestMarkOnEnemy.Value);
                if (f.HasValue) list.Add(f.Value);
            }
            if (list.Count == 0) { list.Add(CardFamily.Skin); list.Add(CardFamily.Eye); }
            return list;
        }

        private CardData PickCurseFromThreatMark()
        {
            if (_catalog == null || _catalog.negatives == null || _catalog.negatives.Count == 0) return null;

            string desired = (_marks && _marks.EnemyThreatMarkOnPlayer == BodyTag.Eye) ? "Parasite" : "Wound";
            for (int i = 0; i < _catalog.negatives.Count; i++)
                if (_catalog.negatives[i] && _catalog.negatives[i].DisplayName.Contains(desired))
                    return _catalog.negatives[i];

            return _catalog.negatives[Random.Range(0, _catalog.negatives.Count)];
        }

        private CardData PickRandomFromFamilies(IReadOnlyList<CardFamily> fams)
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

        private static CardFamily? BodyToFamily(BodyTag tag)
        {
            switch (tag)
            {
                case BodyTag.Skin: return CardFamily.Skin;
                case BodyTag.Eye: return CardFamily.Eye;
                default: return null;
            }
        }

        // ------------------------------------------------------------------
        // External triggers (keeps DeckTestDriver/FlowController keys working)
        // ------------------------------------------------------------------S
        public void SignalVictory()
        {
            // Use the same logic as a real victory
            ShowOverlay();
            RunVictoryPath();
        }

        public void SignalDefeat()
        {
            // Use the same logic as a real defeat
            ShowOverlay();
            RunDefeatPath();
        }

        // ---------- factor the two paths so both event and wrappers can call them ----------
        private void RunVictoryPath()
        {
            var preferred = BuildPreferredListFromPlayerMark();
            if (_log) Debug.Log($"[HarvestGlue] Victory → Harvest (preferred: {string.Join(",", preferred)})");
            _harvest.BeginHarvest(preferred);
        }

        private void RunDefeatPath()
        {
            var curse = PickCurseFromThreatMark();
            if (curse && _deckDriver) _queue?.QueueCardNextFight(curse);
            if (_log) Debug.Log($"[HarvestGlue] Defeat → Queued curse: {curse?.DisplayName ?? "None"}");
            _marks?.ClearAll();
            // Optionally hide overlay here, but I’d leave it up until flow advances
        }

    }
}




