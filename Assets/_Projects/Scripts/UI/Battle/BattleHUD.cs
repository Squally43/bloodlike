using System;
using System.Collections.Generic;
using TMPro;                         // ✅ TMP
using UnityEngine;
using UnityEngine.UI;               // ✅ Button
using WH.Gameplay.Cards;
using WH.Gameplay.Systems;
using WH.Gameplay.Enemies;
using WH.UI;

namespace WH.UI
{
    [DisallowMultipleComponent]
    public sealed class BattleHUD : MonoBehaviour
    {
        [Header("Scene Services")]
        [SerializeField] private TurnManager _turns;
        [SerializeField] private PulseManager _pulse;
        [SerializeField] private CardResolver _resolver;

        [Header("UI Roots (your current scene)")]
        [SerializeField] private Transform _handRoot;
        HandLayoutController _handLayout;
        DealDiscardController _dealDiscard;// UI_HandRow
        [SerializeField] private TMP_Text _txtPlayerHPBlock;     // UI_PlayerHP
        [SerializeField] private TMP_Text _txtPulse;             // UI_PulseCounter
        [SerializeField] private Button _btnEndTurn;             // UI_EndTurn

        [Header("Enemy (optional, for intent preview)")]
        [SerializeField] private EnemyData _enemyDataForIntent;  // SO if you want preview text

        [Header("Prefabs/Styling")]
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private Color _skinColor = new Color(0.945f, 0.898f, 0.816f);
        [SerializeField] private Color _eyeColor = new Color(0.961f, 0.847f, 0.541f);

        [Header("Deck Setup")]
        [SerializeField] private CardCatalog _catalog;
        [SerializeField] private bool _useCatalogStarter = true;
        [SerializeField] private List<CardData> _startingDeck = new List<CardData>();
        [SerializeField] private int _startingHand = 5;
        [SerializeField] private List<CardData> _debugAddsNextFight = new List<CardData>();
        // --- runtime piles (simple lists for this HUD slice) ---
        private readonly List<CardData> _draw = new List<CardData>();
        private readonly List<CardData> _discard = new List<CardData>();
        private readonly List<CardData> _exhaust = new List<CardData>();
        private readonly List<CardData> _hand = new List<CardData>();

        private System.Random _rng;
        private readonly List<CardView> _spawned = new List<CardView>();

        private int _roundIndex = 0;
        private bool _seenFirstPlayerTurn = false;

        private void Awake()
        {
            _rng = new System.Random(123456);
            _handLayout = _handRoot.GetComponent<HandLayoutController>();
            _dealDiscard = _handRoot.GetComponent<DealDiscardController>();
            if (_turns == null) _turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);
            if (_pulse == null) _pulse = FindAnyObjectByType<PulseManager>(FindObjectsInactive.Include);
            if (_resolver == null) _resolver = FindAnyObjectByType<CardResolver>(FindObjectsInactive.Include);

            if (_btnEndTurn) _btnEndTurn.onClick.AddListener(() => _turns?.EndPlayerTurn());
        }

        private void OnEnable()
        {
            if (_turns != null)
            {
                _turns.OnPlayerTurnStarted += HandlePlayerTurnStart;
                _turns.OnPlayerTurnEnded += HandlePlayerTurnEnd;
                _turns.OnEnemyTurnStarted += HandleEnemyTurnStart;
                _turns.OnEnemyTurnEnded += HandleEnemyTurnEnd;
                _turns.OnBattleEnded += HandleBattleEnded;
                _turns.OnPlayerTurnEnded += HandlePlayerTurnEnded;
                _turns.OnPlayerTurnStarted += HandlePlayerTurnStarted;
            }
            if (_pulse != null)
                _pulse.OnPulseChanged += HandlePulseChanged;

            RefreshAllCombatantUI();
            UpdatePulseMirror();
        }

        private void OnDisable()
        {
            if (_turns != null)
            {
                _turns.OnPlayerTurnStarted -= HandlePlayerTurnStart;
                _turns.OnPlayerTurnEnded -= HandlePlayerTurnEnd;
                _turns.OnEnemyTurnStarted -= HandleEnemyTurnStart;
                _turns.OnEnemyTurnEnded -= HandleEnemyTurnEnd;
                _turns.OnBattleEnded -= HandleBattleEnded;
                _turns.OnPlayerTurnEnded -= HandlePlayerTurnEnded;
                _turns.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
            }
            if (_pulse != null)
                _pulse.OnPulseChanged -= HandlePulseChanged;
        }

        // ------------ deck ops (minimal) ------------
        private void ResetDeck()
        {
            _draw.Clear(); _discard.Clear(); _exhaust.Clear(); _hand.Clear();

            // Choose the source of truth for the starter
            if (_useCatalogStarter && _catalog != null)
            {
                // Same starter as the driver (5x Strike, 5x Guard unless you edit the SO)
                _draw.AddRange(_catalog.BuildStarterDeck());
            }
            else
            {
                // Legacy/manual mode (uses the inspector list on this HUD)
                _draw.AddRange(_startingDeck);
            }

            // Optional: let you inject a few cards for next fight testing
            if (_debugAddsNextFight != null && _debugAddsNextFight.Count > 0)
            {
                foreach (var add in _debugAddsNextFight) if (add) _draw.Add(add);
                _debugAddsNextFight.Clear();
            }

            Shuffle(_draw);
        }


        private void DrawUpTo(int target)
        {
            while (_hand.Count < target)
            {
                if (_draw.Count == 0)
                {
                    if (_discard.Count == 0) break;
                    _draw.AddRange(_discard);
                    _discard.Clear();
                    Shuffle(_draw);
                }
                if (_draw.Count == 0) break;
                var last = _draw[_draw.Count - 1];
                _draw.RemoveAt(_draw.Count - 1);
                _hand.Add(last);
            }
        }

        private void Shuffle(List<CardData> list)
        {
            for (int n = list.Count; n > 1; n--)
            {
                int k = _rng.Next(n);
                (list[n - 1], list[k]) = (list[k], list[n - 1]);
            }
        }

        // ------------ turn hooks ------------
        private void HandlePlayerTurnStart()
        {
            if (!_seenFirstPlayerTurn)
            {
                _seenFirstPlayerTurn = true;
                _roundIndex = 1;
                ResetDeck();
            }

            // ensure UI shows correct pulse at start
            _pulse.ResetForNewTurn();
            UpdatePulseMirror();

            DrawUpTo(_startingHand);
            RebuildHandUI();
            RefreshAllCombatantUI();
            SetHandInteractable(true);
        }
        void HandlePlayerTurnEnded()
        {
            if (_dealDiscard == null) return;

            // stop layout reacting while we clear
            if (_handLayout != null) _handLayout.Clear();

            // animate each remaining card to DiscardAnchor and despawn
            foreach (var v in _spawned)
            {
                if (v == null) continue;

                // prevent late clicks
                var btn = v.GetComponent<UnityEngine.UI.Button>();
                if (btn) btn.interactable = false;

                var rt = v.transform as RectTransform;
                if (_handLayout != null) _handLayout.StopAnimating(rt);
                _dealDiscard.DiscardAndDespawn(rt);
            }

            _spawned.Clear();
        }
        void HandlePlayerTurnStarted()
        {
            RebuildHandUI();
        }

        private void HandlePlayerTurnEnd()
        {
            // discard all remaining
            _discard.AddRange(_hand);
            _hand.Clear();
            ClearHandUI();
            SetHandInteractable(false);
        }

        private void HandleEnemyTurnStart()
        {
            SetHandInteractable(false);
        }

        private void HandleEnemyTurnEnd()
        {
            _roundIndex++;
            RefreshAllCombatantUI();
        }

        private void HandleBattleEnded(bool playerWon)
        {
            SetHandInteractable(false);
        }

        // ------------ hand UI ------------
        private void RebuildHandUI()
        {
            ClearHandUI();

            var handLayout = _handRoot.GetComponentInChildren<WH.UI.HandLayoutController>(true);
            if (handLayout != null && handLayout.transform as RectTransform != _handRoot)
            {
                // parent new cards under the controller's transform instead of _handRoot
                _handRoot = handLayout.transform as RectTransform;
            }
            handLayout.Clear();
            var dealDiscard = _handRoot.GetComponent<DealDiscardController>()
               ?? _handRoot.gameObject.AddComponent<DealDiscardController>();
            var spawnedRects = new List<RectTransform>();

            for (int i = 0; i < _hand.Count; i++)
            {
                var card = _hand[i];

                var view = Instantiate(_cardPrefab, _handRoot);     // CardView prefab
                var go = view.gameObject;
                _spawned.Add(view);

                var fx = go.GetComponent<CardFxController>() ?? go.AddComponent<CardFxController>();
                handLayout.RegisterCard(go.transform as RectTransform, fx);

                dealDiscard.InitCardForDeal(go.transform as RectTransform);
                spawnedRects.Add(go.transform as RectTransform);
                Color tint = (card.BaseTint != Color.white) ? card.BaseTint : GuessFamilyColor(card);

                int capturedIndex = i;

                
                view.Bind(
                    card,
                    card.DisplayName,
                    card.RulesLine,                            // <= show rules
                    card.CostPulse,
                    _txtPulse ? _txtPulse.text : "",
                    tint,
                    _ =>
                    {
                        var canAfford = _turns.Pulse.Current >= card.CostPulse;
                        if (canAfford) fx.PlayAcceptedFX(); else fx.PlayDeniedFX();
                        TryPlayIndex(capturedIndex);
                    });
            }
            handLayout.OnHandRebuilt(); // this triggers the fly-to-slot from the deck pose
            dealDiscard.PlayDealStagger(spawnedRects, this); // subtle staggered fade-in


            handLayout.SetNextLayoutStagger(0.04f); // 40 ms per card feels Balatro-ish
            handLayout.OnHandRebuilt();
            dealDiscard.PlayDealStagger(spawnedRects, this); // keep your fade stagger too

        }


        private void ClearHandUI()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i]) Destroy(_spawned[i].gameObject);
            _spawned.Clear();
        }

        private void SetHandInteractable(bool on)
        {
            foreach (var v in _spawned) if (v) v.SetInteractable(on);
        }

        private void TryPlayIndex(int index)
        {
            if (index < 0 || index >= _hand.Count || _resolver == null) return;

            var card = _hand[index];
            if (_resolver.TryPlay(card))
            {
                if (index >= 0 && index < _spawned.Count && _spawned[index])
                    _spawned[index].PlayFlashThenDisable();

                _hand.RemoveAt(index);
                if (card.ExhaustAfterPlay) _exhaust.Add(card);
                else _discard.Add(card);

                RebuildHandUI();
                RefreshAllCombatantUI();
                UpdatePulseMirror();
            }
            // else: insufficient Pulse → leave card in hand
        }

        // ------------ labels ------------
        private void HandlePulseChanged(int current, int max)
        {
            if (_txtPulse) _txtPulse.text = $"Pulse {current}/{max}";
        }

        private void UpdatePulseMirror()
        {
            if (_pulse != null)
                HandlePulseChanged(_pulse.Current, _pulse.Max);
        }

        private void RefreshAllCombatantUI()
        {
            if (_turns != null && _turns.PlayerState != null)
            {
                int php = GetInt(_turns.PlayerState, "CurrentHp");
                int pmax = GetInt(_turns.PlayerState, "MaxHp");
                int pblk = GetInt(_turns.PlayerState, "Block");
                if (_txtPlayerHPBlock) _txtPlayerHPBlock.text = $"HP {php}/{pmax}  |  Block {Mathf.Max(0, pblk)}";
            }

        }

        // ------------ intent preview w/o enum dependency ------------
        private void UpdateIntentPreviewTo(TMP_Text targetText)
        {
            if (targetText == null) return;
            string s = BuildIntentString(_enemyDataForIntent, _roundIndex);
            targetText.text = s;
        }

        public static string BuildIntentString(EnemyData data, int roundIndex)
        {
            if (data == null || data.Intents == null || data.Intents.Count == 0) return "";

            int idx = Mathf.Clamp((roundIndex - 1) % data.Intents.Count, 0, data.Intents.Count - 1);
            object step = data.Intents[idx];

            // Reflection: fields/properties likely exist: type (enum), value (int), times (int), status (string/enum)
            string type = GetEnumName(step, "type");
            int value = GetInt(step, "value");
            int times = Mathf.Max(1, GetInt(step, "times"));
            string status = GetString(step, "status");

            switch (type)
            {
                case "Attack":
                case "MultiAttack":
                    return $"Attack {value}×{times}";
                case "Block":
                    return $"Block {value}";
                case "ApplyStatus":
                    return $"{status} ×{value}";
                default:
                    return type ?? "";
            }
        }

        // ------------ tiny reflection helpers ------------
        private static int GetInt(object obj, string name)
        {
            if (obj == null) return 0;
            var p = obj.GetType().GetProperty(name);
            if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
            var f = obj.GetType().GetField(name);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
            return 0;
        }

        private static string GetString(object obj, string name)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(name);
            if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
            var f = obj.GetType().GetField(name);
            if (f != null && f.FieldType == typeof(String)) return (string)f.GetValue(obj);
            return null;
        }

        private static string GetEnumName(object obj, string name)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(name);
            if (p != null && p.PropertyType.IsEnum) return Enum.GetName(p.PropertyType, p.GetValue(obj));
            var f = obj.GetType().GetField(name);
            if (f != null && f.FieldType.IsEnum) return Enum.GetName(f.FieldType, f.GetValue(obj));
            return null;
        }

        private Color GuessFamilyColor(CardData c)
        {
            var fam = c.Family.ToString();
            if (fam.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0) return _eyeColor;
            return _skinColor;
        }
    }
}


