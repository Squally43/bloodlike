using System.Collections.Generic;
using System.Reflection;
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
        [SerializeField] private MonoBehaviour _pickerComponent;   // IRewardPicker implementer
        [SerializeField] private CanvasGroup _overlay;
        [SerializeField] private CardCatalog _catalog;

        // Assign SO_BuildConfig here (typed as ScriptableObject on purpose to avoid asmdef issues)
        [SerializeField] private ScriptableObject _config;

        [Header("Deck receiver (next-fight owner)")]
        [SerializeField] private MonoBehaviour _deckReceiver; // BattleHUD or driver
        private INextFightQueue _queue;
        private IRewardPicker _picker;
        private ICurrentDeckProbe _probe;
        private IDeckMutationQueue _mutations;

        [Header("UI: Hide these while harvest is active")]
        [SerializeField] private GameObject[] _hideWhileHarvest; // e.g., your HUD Hand container, HUD canvas, etc.

        [Header("Debug")]
        [SerializeField] private bool _log = true;

        private bool _harvestLocked;
        private readonly List<(GameObject go, bool wasActive)> _hidden = new();

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
            if (_harvestLocked) return;
            _harvestLocked = true;

            if (_log) Debug.Log($"[HarvestGlue] BattleEnded playerWon={playerWon}");
            ShowOverlay();
            if (playerWon) StartVictory(); else StartDefeat();
        }

        private void UnlockHarvest() => _harvestLocked = false;

        // ----- victory -----
        private void StartVictory()
        {
            if (_log)
            {
                var markStr =
    (_marks && _marks.PlayerHarvestMarkOnEnemy.HasValue)
        ? _marks.PlayerHarvestMarkOnEnemy.Value.ToString()
        : (MarkController.GetPlayerHarvestMarkGlobal()?.ToString() ?? "None");
                Debug.Log($"[HarvestGlue] Mark state → PlayerHarvestMarkOnEnemy={markStr}");

            }

            var preferred = BuildPreferredFromPlayerMark();
            if (_log) Debug.Log($"[HarvestGlue] Victory → Harvest (preferred: {string.Join(",", preferred)})");
            _harvest.BeginHarvest(preferred);
        }

        private void OnHarvestResolved(bool success, IReadOnlyList<CardFamily> preferred, CardFamily? chosen)
        {
            if (_log) Debug.Log($"[HarvestGlue] QTE resolved success={success} chosen={chosen?.ToString() ?? "null"}");
            if (!success) { _marks?.ClearAll(); HideOverlay(); return; }

            var isHonest = IsHonestHarvest(chosen);
            var isLying = !isHonest;

            // Families fed into the picker: honest = chosen family only; lying/mixed = empty → mixed pool
            var pickerFamilies = new List<CardFamily>();
            if (isHonest && chosen.HasValue) pickerFamilies.Add(chosen.Value);

            // LIAR in deck?
            bool liarInDeck = false;
            if (_catalog && _catalog.liarCard) liarInDeck = _probe != null && _probe.ContainsCardId(_catalog.liarCard.Id);

            var honestCount = Mathf.Max(1, CfgInt("honestCount", 3));
            var lyingCount = Mathf.Max(1, CfgInt("lyingCount", 2));
            int count = isLying ? lyingCount : honestCount;

            if (_picker != null)
            {
                if (_log) Debug.Log($"[Picker] {(isLying ? "lying" : "honest")}, requestedCount={count}");
                _picker.Show(pickerFamilies, count, isLying, liarInDeck);
            }
            else
            {
                var pick = PickOne(pickerFamilies);
                if (pick != null && _queue != null) _queue.QueueCardNextFight(pick);
                CompleteHarvest();
            }

            // One corruption roll after a lying harvest
            if (isLying && CfgBool("enableLying", true) && _catalog && _catalog.liarCard)
            {
                var chance = Mathf.Clamp01(CfgFloat("liarCorruptionChance", 0.15f));
                var roll = Random.value;
                if (_log) Debug.Log($"[LIAR] corruption roll={roll:F3} vs {chance:F2}");
                if (roll < chance)
                {
                    var liarId = _catalog.liarCard.Id;
                    bool transformed = false;

                    if (_mutations != null) transformed = _mutations.TryTransformRandomNonStarterNonCurseTo(liarId);

                    if (transformed)
                        Debug.Log("[LIAR] corruption hit → transformed one non-starter, non-curse into LIAR.");
                    else
                    {
                        if (_mutations != null) _mutations.QueueExtraForNextFight(liarId);
                        else if (_queue != null) _queue.QueueCardNextFight(_catalog.liarCard);
                        Debug.Log("[LIAR] (fallback) queued LIAR for next fight.");
                    }
                }
            }
        }

        private bool IsHonestHarvest(CardFamily? chosen)
        {
            if (!chosen.HasValue) return false;

            BodyTag? tag = null;
            if (_marks && _marks.PlayerHarvestMarkOnEnemy.HasValue)
                tag = _marks.PlayerHarvestMarkOnEnemy.Value;
            else
                tag = MarkController.GetPlayerHarvestMarkGlobal();

            if (!tag.HasValue) return false;

            return (tag == BodyTag.Skin && chosen.Value == CardFamily.Skin)
                || (tag == BodyTag.Eye && chosen.Value == CardFamily.Eye);
        }


        private void OnRewardChosen(CardData chosen)
        {
            if (chosen != null && _queue != null) _queue.QueueCardNextFight(chosen);
            CompleteHarvest();
        }

        private void CompleteHarvest()
        {
            _marks?.ClearAll();
            HideOverlay();
        }

        // ----- defeat -----
        private void StartDefeat()
        {
            var curse = PickCurseFromThreat();
            if (curse != null && _queue != null) _queue.QueueCardNextFight(curse);
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

            INextFightQueue hud = null, driver = null;
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb is INextFightQueue q)
                {
                    var n = mb.GetType().Name;
                    if (n.Contains("BattleHUD")) hud = q; else if (n.Contains("DeckTestDriver")) driver = q;
                }
                if (_probe == null && mb is ICurrentDeckProbe p) _probe = p;
                if (_mutations == null && mb is IDeckMutationQueue m) _mutations = m;
            }
            _queue = _queue ?? hud ?? driver;

            if (_log)
            {
                Debug.Log(_queue != null ? $"[HarvestGlue] Deck receiver → {_queue.GetType().Name}" : "[HarvestGlue] No deck receiver.");
                Debug.Log(_probe != null ? "[HarvestGlue] Deck probe found." : "[HarvestGlue] No deck probe.");
                Debug.Log(_mutations != null ? "[HarvestGlue] Deck mutation queue found." : "[HarvestGlue] No mutation queue.");
            }
        }

        // ----- overlay + HUD hide/show -----
        private void ShowOverlay()
        {
            // hide HUD bits
            _hidden.Clear();
            if (_hideWhileHarvest != null)
            {
                foreach (var go in _hideWhileHarvest)
                {
                    if (!go) continue;
                    _hidden.Add((go, go.activeSelf));
                    go.SetActive(false);
                }
            }

            if (_overlay)
            {
                _overlay.gameObject.SetActive(true);
                _overlay.alpha = 1f;
                _overlay.blocksRaycasts = true;
                _overlay.interactable = true;
            }
        }

        private void HideOverlay()
        {
            // restore HUD bits
            for (int i = 0; i < _hidden.Count; i++)
            {
                var pair = _hidden[i];
                if (pair.go) pair.go.SetActive(pair.wasActive);
            }
            _hidden.Clear();

            if (_overlay)
            {
                _overlay.blocksRaycasts = false;
                _overlay.interactable = false;
                _overlay.alpha = 0f;
                _overlay.gameObject.SetActive(false);
            }
        }

        // ----- helpers -----
        private List<CardFamily> BuildPreferredFromPlayerMark()
        {
            var list = new List<CardFamily>(2);

            BodyTag? tag = null;
            if (_marks && _marks.PlayerHarvestMarkOnEnemy.HasValue)
                tag = _marks.PlayerHarvestMarkOnEnemy.Value;
            else
                tag = MarkController.GetPlayerHarvestMarkGlobal();

            if (tag.HasValue)
            {
                if (tag.Value == BodyTag.Skin) list.Add(CardFamily.Skin);
                if (tag.Value == BodyTag.Eye) list.Add(CardFamily.Eye);
            }

            if (list.Count == 0) { list.Add(CardFamily.Skin); list.Add(CardFamily.Eye); } // mixed if no mark
            return list;
        }

        private CardData PickCurseFromThreat()
        {
            if (_catalog == null) return null;
            var preferParasite = _marks && _marks.EnemyThreatMarkOnPlayer == BodyTag.Eye;
            if (preferParasite && _catalog.negatives != null)
            {
                for (int i = 0; i < _catalog.negatives.Count; i++)
                {
                    var c = _catalog.negatives[i];
                    if (c != null && c.DisplayName.IndexOf("parasite", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return c;
                }
            }
            return _catalog.TryFindWoundCard();
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

        // ----- config via reflection (keeps compile safe) -----
        private int CfgInt(string name, int def) => TryGetField(name, def);
        private float CfgFloat(string name, float def) => TryGetField(name, def);
        private bool CfgBool(string name, bool def) => TryGetField(name, def);

        private T TryGetField<T>(string name, T def)
        {
            if (_config == null) return def;
            var fi = _config.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (fi == null || !typeof(T).IsAssignableFrom(fi.FieldType)) return def;
            return (T)fi.GetValue(_config);
        }

        // dev helpers
        public void SignalVictory() { if (!_harvestLocked) { _harvestLocked = true; ShowOverlay(); StartVictory(); } }
        public void SignalDefeat() { if (!_harvestLocked) { _harvestLocked = true; ShowOverlay(); StartDefeat(); } }
    }
}









