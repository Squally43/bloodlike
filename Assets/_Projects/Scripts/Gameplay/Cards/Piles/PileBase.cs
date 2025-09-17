using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Cards.Piles
{
    /// <summary>Generic pile of cards. Logic only; no views.</summary>
    public abstract class PileBase
    {
        protected readonly List<CardData> _cards = new();

        public int Count => _cards.Count;

        public void Add(CardData card)
        {
            if (card != null) _cards.Add(card);
        }

        public CardData DrawTop()
        {
            if (_cards.Count == 0) return null;
            var idx = _cards.Count - 1;
            var c = _cards[idx];
            _cards.RemoveAt(idx);
            return c;
        }

        public void MoveAllTo(PileBase other)
        {
            if (other == null || _cards.Count == 0) return;
            other._cards.AddRange(_cards);
            _cards.Clear();
        }

        public void Shuffle(System.Random rng)
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        public IReadOnlyList<CardData> AsReadOnly() => _cards;
    }
}

