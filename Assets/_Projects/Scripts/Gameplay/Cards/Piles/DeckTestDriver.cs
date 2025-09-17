using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay.Cards.Piles;
using WH.Gameplay.Systems;

namespace WH.Gameplay.Cards
{
    /// <summary>Small driver to prove draw-play-discard without UI.</summary>
    [DisallowMultipleComponent]
    public sealed class DeckTestDriver : MonoBehaviour
    {
        [Header("Scene Services")]
        [SerializeField] private TurnManager _turns;
        [SerializeField] private CardResolver _resolver;

        [Header("Starting Deck")]
        [SerializeField] private List<CardData> _startingList = new();

        [Header("Rules")]
        [SerializeField] private int _startingHand = 5;

        private readonly DrawPile _draw = new();
        private readonly DiscardPile _discard = new();
        private readonly ExhaustPile _exhaust = new();
        private readonly Hand _hand = new();

        private System.Random _rng;

        private void Awake()
        {
            _rng = new System.Random(123456); // deterministic for tests

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
        }

        private void OnDisable()
        {
            if (_turns != null)
            {
                _turns.OnPlayerTurnStarted -= HandlePlayerTurnStart;
                _turns.OnPlayerTurnEnded -= HandlePlayerTurnEnd;
            }
        }

        public void ResetDeck()
        {
            _draw.MoveAllTo(_discard);
            _discard.MoveAllTo(_draw);
            _exhaust.MoveAllTo(_draw);
            _hand.TakeAll();

            foreach (var c in _startingList)
                _draw.Add(c);

            _draw.Shuffle(_rng);
            Debug.Log($"[Deck] Reset with {_draw.Count} cards");
        }

        private void HandlePlayerTurnStart()
        {
            if (_draw.Count == 0 && _discard.Count > 0)
            {
                _discard.MoveAllTo(_draw);
                _draw.Shuffle(_rng);
            }

            if (_draw.Count == 0 && _startingList.Count > 0)
                ResetDeck();

            DrawUpTo(_startingHand);
            LogHand();
        }

        private void HandlePlayerTurnEnd()
        {
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

            if (Input.GetKeyDown(KeyCode.D))
            {
                DrawUpTo(_hand.Count + 1);
                LogHand();
            }
        }

        /// <summary>
        /// Takes a 1-based index from player input and converts to 0-based hand index.
        /// </summary>
        private void TryPlayDisplayIndex(int displayIndex)
        {
            int index = displayIndex - 1; // convert to zero-based
            var card = _hand.GetAt(index);
            if (card == null)
            {
                Debug.Log($"[Deck] No card at slot {displayIndex}");
                return;
            }

            if (_resolver == null)
            {
                Debug.LogError("[Deck] CardResolver missing");
                return;
            }

            if (_resolver.TryPlay(card))
            {
                _hand.RemoveAt(index);
                if (card.ExhaustAfterPlay)
                    _exhaust.Add(card);
                else
                    _discard.Add(card);

                Debug.Log($"[Deck] Played {card.DisplayName}. Hand:{_hand.Count} Discard:{_discard.Count} Exhaust:{_exhaust.Count}");
            }
            else
            {
                Debug.Log("[Deck] Play failed");
            }
        }

        private void LogHand()
        {
            var list = _hand.AsReadOnly();
            var s = "[Hand] ";
            for (int i = 0; i < list.Count; i++)
            {
                s += $"{i + 1}:{list[i].DisplayName}  "; // show as 1-based for readability
            }
            Debug.Log(s.Trim());
        }
    }
}


