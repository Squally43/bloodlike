using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using WH.Gameplay;           // CardFamily
using WH.Gameplay.Cards;     // CardData
using WH.Gameplay.Systems;

namespace WH.UI
{
    [DisallowMultipleComponent]
    public sealed class RewardPicker : MonoBehaviour, IRewardPicker
    {
        [Header("Data")]
        [SerializeField] private CardCatalog _catalog;
        [SerializeField] private ScriptableObject _config; // assign SO_BuildConfig here

        [Header("Hand UI (DEDICATED to picker)")]
        [SerializeField] private RectTransform _handRoot;          // must NOT be your HUD Hand root
        [SerializeField] private HandLayoutController _handLayout;
        [SerializeField] private DealDiscardController _dealDiscard;
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private float _spawnStagger = 0.0f;

        [Header("Visual (optional)")]
        [SerializeField] private Graphic _glitchTint;

        public event Action<CardData> OnChosen;

        private readonly List<CardView> _spawned = new();
        private readonly List<RectTransform> _spawnedRects = new();
        private System.Random _pickerRng;
        private System.Random _liarRng;
        private bool _isOpen;

        private void Awake()
        {
            var pickerSeed = CfgInt("pickerSeed", 0);
            var liarSeed = CfgInt("liarSeed", 0);
            _pickerRng = pickerSeed != 0 ? new System.Random(pickerSeed) : null;
            _liarRng = liarSeed != 0 ? new System.Random(liarSeed) : null;
            SetGlitch(false);
        }

        public void Show(IReadOnlyList<CardFamily> families, int count = 3, bool isLying = false, bool liarInDeck = false)
        {
            if (_isOpen)
            {
                // Defensive: if Show is called twice, clear and rebuild once.
                Hide();
            }
            _isOpen = true;

            if (_catalog == null)
            {
                Debug.LogWarning("[RewardPicker] No CardCatalog assigned.");
                AutoPick(null);
                return;
            }
            if (!_handRoot || !_handLayout || !_dealDiscard || !_cardPrefab)
            {
                Debug.LogWarning("[RewardPicker] Hand UI not wired (root/layout/deal/prefab). Falling back to auto-pick.");
                var optionsFallback = BuildOptions(families, count, liarInDeck);
                AutoPick(optionsFallback.Count > 0 ? optionsFallback[0] : null);
                return;
            }

            // Hard guard: picker root must be empty. If not, nuke it (prevents “3 + leftover HUD card” visuals).
            for (int i = _handRoot.childCount - 1; i >= 0; i--)
                Destroy(_handRoot.GetChild(i).gameObject);

            gameObject.SetActive(true);
            ClearSpawned();
            SetGlitch(isLying && CfgBool("enableGlitchTint", true));

            var options = BuildOptions(families, count, liarInDeck);
            if (options.Count > count) options = options.GetRange(0, count); // belt-and-braces
            Debug.Log($"[Picker] finalCount={options.Count}");

            for (int i = 0; i < options.Count; i++)
            {
                var data = options[i];
                var view = Instantiate(_cardPrefab, _handRoot);
                _spawned.Add(view);
                var rt = (RectTransform)view.transform;
                _spawnedRects.Add(rt);

                _dealDiscard.InitCardForDeal(rt);

                var fx = view.GetComponent<CardFxController>();
                _handLayout.RegisterCard(rt, fx);

                var tint = (data.BaseTint != Color.white) ? data.BaseTint : CardFamilyColors.Get(data.Family);
                var captured = data;
                view.Bind(captured, captured.DisplayName, captured.RulesLine, captured.CostPulse, "", tint, _ => Choose(captured));
            }

            _handLayout.SetNextLayoutStagger(_spawnStagger);
            _handLayout.OnHandRebuilt();
            _dealDiscard.PlayDealStagger(_spawnedRects, this);
        }

        public void Hide()
        {
            if (_dealDiscard != null) _dealDiscard.DiscardAndDespawnMany(_spawnedRects);
            else
            {
                for (int i = 0; i < _spawned.Count; i++)
                    if (_spawned[i]) Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();
            _spawnedRects.Clear();

            SetGlitch(false);
            if (gameObject.activeSelf) gameObject.SetActive(false);
            _isOpen = false;
        }

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

        private void SetGlitch(bool on)
        {
            if (!_glitchTint) return;
            _glitchTint.gameObject.SetActive(on);
            _glitchTint.canvasRenderer.SetAlpha(on ? 1f : 0f);
        }

        private List<CardData> BuildOptions(IReadOnlyList<CardFamily> families, int count, bool liarInDeck)
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

                var pick = PickFrom(pool, used);
                if (!pick) continue;

                used.Add(pick);
                result.Add(pick);
            }

            // LIAR downgrade (per slot) when LIAR is in deck
            if (liarInDeck)
            {
                var chance = Mathf.Clamp01(CfgFloat("liarDowngradeChance", 0.25f));
                for (int i = 0; i < result.Count; i++)
                {
                    var roll = NextFloat(_liarRng);
                    if (roll < chance)
                    {
                        bool wound = NextFloat(_liarRng) < 0.5f;
                        CardData replacement = null;
                        if (wound)
                        {
                            replacement = _catalog.TryFindWoundCard();
                        }
                        else
                        {
                            if (_catalog.neutralRewards != null && _catalog.neutralRewards.Count > 0)
                                replacement = PickAny(_catalog.neutralRewards);
                            if (!replacement && result.Count > 0)
                                replacement = result[Mathf.Clamp(NextInt(_liarRng, 0, result.Count), 0, result.Count - 1)];
                        }

                        if (replacement)
                        {
                            Debug.Log($"[LIAR] downgrade rolled on slot {i} → {replacement.DisplayName}");
                            result[i] = replacement;
                        }
                    }
                }
            }

            string fams = (families == null || families.Count == 0) ? "Mixed" : string.Join(",", families);
            Debug.Log($"[Picker] pool families → {fams} | selections → {string.Join(", ", result.ConvertAll(c => c.DisplayName))}");

            return result;
        }

        private CardData PickFrom(List<CardData> pool, HashSet<CardData> used)
        {
            if (pool == null || pool.Count == 0) return null;
            for (int t = 0; t < 8; t++)
            {
                var idx = NextInt(_pickerRng, 0, pool.Count);
                var c = pool[idx];
                if (!c || used.Contains(c)) continue;
                return c;
            }
            return pool[NextInt(_pickerRng, 0, pool.Count)];
        }

        private CardData PickAny(List<CardData> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[NextInt(_pickerRng, 0, pool.Count)];
        }

        private static int NextInt(System.Random rng, int minInclusive, int maxExclusive)
        {
            if (rng == null) return UnityEngine.Random.Range(minInclusive, maxExclusive);
            return rng.Next(minInclusive, maxExclusive);
        }
        private static float NextFloat(System.Random rng)
        {
            if (rng == null) return UnityEngine.Random.value;
            return (float)rng.NextDouble();
        }

        // config helpers via reflection
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
    }
}






