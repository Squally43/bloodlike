using System.Collections.Generic;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Cards.Piles
{
    /// <summary>Player hand. Max size rules added later.</summary>
    public sealed class Hand : PileBase
    {
        public int MaxSize { get; set; } = 10;

        public List<CardData> TakeAll()
        {
            var copy = new List<CardData>(_cards);
            _cards.Clear();
            return copy;
        }

        public CardData GetAt(int index)
        {
            if (index < 0 || index >= _cards.Count) return null;
            return _cards[index];
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _cards.Count) return;
            _cards.RemoveAt(index);
        }
    }
}


