using System.Collections.Generic;
using UnityEngine;

namespace WH.Gameplay.Cards
{
    [CreateAssetMenu(menuName = "WH/Cards/CardCatalog", fileName = "SO_CardCatalog")]
    public sealed class CardCatalog : ScriptableObject
    {
        [System.Serializable] public struct Entry { public CardData card; public int count; }

        [Header("Starter Deck (exact counts)")]
        public List<Entry> starterDeck = new();

        [Header("Reward Pools")]
        public List<CardData> skinRewards = new();   // Scalpel family
        public List<CardData> eyeRewards = new();   // Pliers family
        public List<CardData> negatives = new();   // Curses (Wound/Parasite)

        public List<CardData> BuildStarterDeck()
        {
            var list = new List<CardData>();
            foreach (var e in starterDeck)
            {
                if (!e.card) continue;
                for (int i = 0; i < Mathf.Max(0, e.count); i++) list.Add(e.card);
            }
            return list;
        }
    }
}


