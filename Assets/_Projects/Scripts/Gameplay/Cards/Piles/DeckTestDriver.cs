using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay;                  // CardRarity, BodyTag, CardEffectType
using WH.Gameplay.Cards.Piles;
using WH.Gameplay.Systems;

namespace WH.Gameplay.Cards
{
    [DisallowMultipleComponent]
    public sealed class DeckTestDriver : MonoBehaviour, INextFightQueue
    {
        [Header("Scene Services")]
        [SerializeField] private TurnManager _turns;
        [SerializeField] private CardResolver _resolver;
        [SerializeField] private MarkController _marks;

        [Header("Deck Sources")]
        [SerializeField] private CardCatalog _catalog;
        [SerializeField] private bool _useCatalogStarter = true;

        [Tooltip("Fallback/manual list if no catalog or toggle disabled.")]
        [SerializeField] private List<CardData> _startingList = new();

        [Header("Rules")]
        [SerializeField] private int _startingHand = 5;

        private readonly DrawPile _draw = new();
        private readonly DiscardPile _discard = new();
        private readonly ExhaustPile _exhaust = new();
        private readonly Hand _hand = new();

        private readonly List<CardData> _pendingAddsNextFight = new();
        private System.Random _rng;
        private bool _initialized;

        // per-play flags (set by resolver)
        private bool _forceExhaustThisCard;
        private bool _retainThisCard;

        // (Phase 4) Last mark applied to enemy
        private BodyTag? _lastEnemyMark;

        // -------- cached handlers so we can unsubscribe safely --------
        private System.Action<int> _hDraw;
        private System.Func<int, ScryOutcome> _hScry;
        private System.Func<bool> _hExhaustCurse;
        private System.Action _hReveal;
        private System.Action<BodyTag> _hMark;
        private System.Action _hForceExhaust;
        private System.Action _hRetain;
        private System.Action<string> _hInfo;

        private void Awake()
        {
            _rng = new System.Random(123456);

            if (_turns == null)
                _turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);

            if (_resolver == null)
                _resolver = FindAnyObjectByType<CardResolver>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (_turns != null)
            {
                _turns.OnPlayerTurnStarted += HandlePlayerTurnStart;
                _turns.OnPlayerTurnEnded += HandlePlayerTurnEnd;
            }

            if (_resolver != null)
            {
                // create concrete delegates (not lambdas inline) so we can -= later
                _hDraw = HandleRequestDraw;
                _hScry = HandleRequestScry;
                _hExhaustCurse = HandleExhaustCurseInHand;
                _hReveal = OnRevealIntent;
                _hMark = OnMarkEnemy;
                _hForceExhaust = () => _forceExhaustThisCard = true;
                _hRetain = () => _retainThisCard = true;
                _hInfo = OnResolverInfo;
                _hMark = tag =>
                {
                    if (_marks == null) _marks = FindAnyObjectByType<MarkController>(FindObjectsInactive.Include);
                    if (_marks != null) _marks.ApplyPlayerHarvestMark(tag);
                    Debug.Log($"[Resolver] Marked enemy: {tag}");
                };
                _resolver.OnMarkEnemy += _hMark;
                _resolver.OnRequestDraw += _hDraw;
                _resolver.OnRequestScry += _hScry;
                _resolver.OnRequestExhaustCurseInHand += _hExhaustCurse;
                _resolver.OnRevealEnemyIntent += _hReveal;
                _resolver.OnMarkEnemy += _hMark;
                _resolver.OnForceExhaustThisCard += _hForceExhaust;
                _resolver.OnRetainThisCard += _hRetain;
                _resolver.OnInfo += _hInfo;
            }
        }
        public void QueueCardNextFight(CardData card)
        {
            if (card == null) return;
            _pendingAddsNextFight.Add(card);
        }

        private void OnDisable()
        {
            if (_turns != null)
            {
                _turns.OnPlayerTurnStarted -= HandlePlayerTurnStart;
                _turns.OnPlayerTurnEnded -= HandlePlayerTurnEnd;
            }

            if (_resolver != null)
            {
                // Unsubscribe using the cached delegates (no direct = null!)
                if (_hDraw != null) _resolver.OnRequestDraw -= _hDraw;
                if (_hScry != null) _resolver.OnRequestScry -= _hScry;
                if (_hExhaustCurse != null) _resolver.OnRequestExhaustCurseInHand -= _hExhaustCurse;
                if (_hReveal != null) _resolver.OnRevealEnemyIntent -= _hReveal;
                if (_hMark != null) _resolver.OnMarkEnemy -= _hMark;
                if (_hForceExhaust != null) _resolver.OnForceExhaustThisCard -= _hForceExhaust;
                if (_hRetain != null) _resolver.OnRetainThisCard -= _hRetain;
                if (_hInfo != null) _resolver.OnInfo -= _hInfo;
            }
        }

        public void ResetDeck()
        {
            _draw.MoveAllTo(_discard);
            _discard.MoveAllTo(_draw);
            _exhaust.MoveAllTo(_draw);
            _hand.TakeAll();

            var source = new List<CardData>();
            if (_useCatalogStarter && _catalog != null)
                source.AddRange(_catalog.BuildStarterDeck()); // 5x Strike, 5x Guard
            else
                source.AddRange(_startingList);

            // Apply any queued reward cards
            if (_pendingAddsNextFight.Count > 0)
            {
                foreach (var add in _pendingAddsNextFight) if (add) source.Add(add);
                Debug.Log($"[Deck] +{_pendingAddsNextFight.Count} card(s) added for this fight.");
                _pendingAddsNextFight.Clear();
            }

            foreach (var c in source) _draw.Add(c);
            _draw.Shuffle(_rng);

            _initialized = true;
            Debug.Log($"[Deck] Reset with {_draw.Count} cards");
        }

        private void HandlePlayerTurnStart()
        {
            if (!_initialized) ResetDeck();

            if (_draw.Count == 0 && _discard.Count > 0)
            {
                _discard.MoveAllTo(_draw);
                _draw.Shuffle(_rng);
            }

            DrawUpTo(_startingHand);
            LogHand();
        }

        private void HandlePlayerTurnEnd()
        {
            // End-of-turn Parasite (sum all copies still in hand)
            int totalEndTurnHpLoss = 0;
            for (int i = 0; i < _hand.Count; i++)
            {
                var c = _hand.GetAt(i);
                if (c == null) continue;
                if (HasCustomOp(c, CardCustomOp.EndTurnLoseHp, out int arg))
                    totalEndTurnHpLoss += Mathf.Max(1, arg);
            }

            if (totalEndTurnHpLoss > 0)
            {
                _turns.PlayerState.CurrentHp = Mathf.Max(0, _turns.PlayerState.CurrentHp - totalEndTurnHpLoss);
                Debug.Log($"[Deck] End-of-turn Parasite: -{totalEndTurnHpLoss} HP");
            }

            foreach (var c in _hand.TakeAll())
                _discard.Add(c);

            Debug.Log($"[Deck] Discarded hand. Discard size: {_discard.Count}");
        }

        private void DrawUpTo(int target)
        {
            while (_hand.Count < target)
            {
                if (_draw.Count == 0)
                {
                    if (_discard.Count == 0) break;
                    _discard.MoveAllTo(_draw);
                    _draw.Shuffle(_rng);
                }

                var next = _draw.DrawTop();
                if (next == null) break;
                _hand.Add(next);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) TryPlayDisplayIndex(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TryPlayDisplayIndex(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TryPlayDisplayIndex(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) TryPlayDisplayIndex(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) TryPlayDisplayIndex(5);

            // Dev controls:
            if (Input.GetKeyDown(KeyCode.N))
            {
                Debug.Log("[Deck] Next fight");
                _initialized = false;
                if (_marks) _marks.ClearAll();
                WH.GameSignals.RaiseNextFightReadied();   // ← tells TurnManager to StartNewBattle()
            }

            // put near other dev controls
            if (Input.GetKeyDown(KeyCode.V))  // Victory
            {
                var glue = FindAnyObjectByType<WH.Gameplay.Systems.HarvestGlue>(FindObjectsInactive.Include);
                glue?.SignalVictory();
            }
            if (Input.GetKeyDown(KeyCode.B))  // Defeat
            {
                var glue = FindAnyObjectByType<WH.Gameplay.Systems.HarvestGlue>(FindObjectsInactive.Include);
                glue?.SignalDefeat();
                _initialized = false; // advance to next fight
            }

            if (Input.GetKeyDown(KeyCode.R)) QueueRandomRewardNextFight();
            if (Input.GetKeyDown(KeyCode.K)) QueueRandomFromFamilyNextFight(CardFamily.Skin); // Scalpel
            if (Input.GetKeyDown(KeyCode.L)) QueueRandomFromFamilyNextFight(CardFamily.Eye);  // Pliers
        }

        private void TryPlayDisplayIndex(int displayIndex)
        {
            int index = displayIndex - 1;
            var card = _hand.GetAt(index);
            if (card == null) { Debug.Log($"[Deck] No card at slot {displayIndex}"); return; }
            if (_resolver == null) { Debug.LogError("[Deck] CardResolver missing"); return; }

            _forceExhaustThisCard = false;
            _retainThisCard = false;

            if (_resolver.TryPlay(card))
            {
                _hand.RemoveAt(index);

                if (_forceExhaustThisCard || card.ExhaustAfterPlay) _exhaust.Add(card);
                else if (_retainThisCard) _hand.Add(card);      // simple “retain”: keep in hand
                else _discard.Add(card);

                Debug.Log($"[Deck] Played {card.DisplayName}. Hand:{_hand.Count} Discard:{_discard.Count} Exhaust:{_exhaust.Count}");
            }
            else
            {
                Debug.Log("[Deck] Play failed");
            }
        }

        // ========================= Resolver handlers =========================

        private void HandleRequestDraw(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (_draw.Count == 0)
                {
                    if (_discard.Count == 0) break;
                    _discard.MoveAllTo(_draw);
                    _draw.Shuffle(_rng);
                }
                var next = _draw.DrawTop();
                if (next == null) break;
                _hand.Add(next);
            }
            LogHand();
        }

        private ScryOutcome HandleRequestScry(int n)
        {
            // UI-free Scry MVP:
            // 1) Look at top N.
            // 2) Auto-discard all Curses.
            // 3) Put the rest back on TOP in their original order.
            int examined = Mathf.Min(n, _draw.Count);
            if (examined <= 0) return default;

            var buffer = new List<CardData>(examined);
            for (int i = 0; i < examined; i++)
                buffer.Add(_draw.DrawTop()); // top-first

            int discarded = 0;
            bool anyCurse = false;
            var kept = new List<CardData>(examined);

            foreach (var c in buffer)
            {
                if (c != null && c.Rarity == CardRarity.Curse)
                {
                    _discard.Add(c);
                    discarded++;
                    anyCurse = true;
                }
                else kept.Add(c);
            }

            // put kept back on top preserving original order
            for (int i = kept.Count - 1; i >= 0; i--)
                _draw.Add(kept[i]);

            Debug.Log($"[Scry] Looked at {examined}. Discarded {discarded} (curse? {anyCurse}).");
            return new ScryOutcome { discardedTotal = discarded, discardedAnyCurse = anyCurse };
        }

        private bool HandleExhaustCurseInHand()
        {
            for (int i = 0; i < _hand.Count; i++)
            {
                var c = _hand.GetAt(i);
                if (c != null && c.Rarity == CardRarity.Curse)
                {
                    _hand.RemoveAt(i);
                    _exhaust.Add(c);
                    return true;
                }
            }
            return false;
        }

        private void OnRevealIntent() => Debug.Log("[Resolver] Reveal Intent");
        private void OnMarkEnemy(BodyTag tag) { _lastEnemyMark = tag; Debug.Log($"[Resolver] Marked enemy: {tag}"); }
        private void OnResolverInfo(string s) { if (!string.IsNullOrEmpty(s)) Debug.Log($"[Resolver] {s}"); }

        // ========================= Rewards =========================

        private void QueueRandomRewardNextFight()
        {
            if (_catalog == null) { Debug.LogWarning("[Deck] No CardCatalog set"); return; }

            var pool = new List<CardData>();
            pool.AddRange(_catalog.skinRewards);
            pool.AddRange(_catalog.eyeRewards);
            if (pool.Count == 0) { Debug.LogWarning("[Deck] Catalog reward pools are empty"); return; }

            var pick = pool[_rng.Next(0, pool.Count)];
            _pendingAddsNextFight.Add(pick);
            Debug.Log($"[Deck] Queued reward for next fight: {pick.DisplayName}");
        }

        private void QueueRandomFromFamilyNextFight(CardFamily family)
        {
            if (_catalog == null) return;
            List<CardData> pool = family == CardFamily.Skin ? _catalog.skinRewards : _catalog.eyeRewards;
            if (pool == null || pool.Count == 0) { Debug.LogWarning($"[Deck] No rewards for {family}"); return; }
            var pick = pool[_rng.Next(0, pool.Count)];
            _pendingAddsNextFight.Add(pick);
            Debug.Log($"[Deck] Queued {family} reward for next fight: {pick.DisplayName}");
        }

        private void LogHand()
        {
            var list = _hand.AsReadOnly();
            var s = "[Hand] ";
            for (int i = 0; i < list.Count; i++) s += $"{i + 1}:{list[i].DisplayName}  ";
            Debug.Log(s.Trim());
        }

        // ---------------- Utility ----------------

        private static bool HasCustomOp(CardData card, int opCode, out int arg)
        {
            arg = 0;
            var list = card.Effects;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.type == CardEffectType.Custom && e.value == opCode)
                {
                    arg = e.value2;
                    return true;
                }
            }
            return false;
        }
    }
}

