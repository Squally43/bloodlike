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
        public List<CardData> skinRewards = new();        // Scalpel family
        public List<CardData> eyeRewards = new();        // Pliers family
        public List<CardData> neutralRewards = new();     // Neutral/non-family rewards (optional)

        [Header("Negatives/Curses")]
        public List<CardData> negatives = new();          // e.g. Wound, Parasite

        [Header("Specials")]
        public CardData liarCard;                         // LIAR [1] — Exhaust. Do nothing. (rarity = Curse)
        public CardData woundCardFallback;                // Optional direct ref if you want predictable Wound injection

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

        // ------------- tiny helpers -------------
        public CardData TryFindWoundCard()
        {
            if (woundCardFallback) return woundCardFallback;
            if (negatives == null || negatives.Count == 0) return null;

            // Name contains "Wound" wins; otherwise just first Curse
            for (int i = 0; i < negatives.Count; i++)
            {
                var c = negatives[i];
                if (!c) continue;
                if (c.DisplayName.IndexOf("wound", System.StringComparison.OrdinalIgnoreCase) >= 0) return c;
            }
            for (int i = 0; i < negatives.Count; i++)
                if (negatives[i]) return negatives[i];
            return null;
        }
    }
}



