using System.Collections.Generic;
using UnityEngine;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Systems
{
    /// <summary>Picks 1-of-3 rewards based on families and rarity weights.</summary>
    [DisallowMultipleComponent]
    public sealed class RewardPicker : MonoBehaviour
    {
        // Placeholder pools until RewardTable is added.
        [SerializeField] private List<CardData> _allCards = new();

        public List<CardData> GetRewardOptions(IReadOnlyList<WH.Gameplay.CardFamily> families, int count = 3)
        {
            // TODO: filter by family and rarity once RewardTable exists.
            var result = new List<CardData>(count);
            for (int i = 0; i < _allCards.Count && result.Count < count; i++)
                result.Add(_allCards[i]);

            Debug.Log($"[Reward] Offering {result.Count} cards");
            return result;
        }
    }
}

