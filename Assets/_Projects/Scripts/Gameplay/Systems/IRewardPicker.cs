using System;
using System.Collections.Generic;
using WH.Gameplay;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Systems
{
    /// <summary>Gameplay-facing contract for reward pickers.</summary>
    public interface IRewardPicker
    {
        event Action<CardData> OnChosen;

        /// <summary>
        /// Show N options drawn from the given families. If families empty → mixed pool.
        /// isLying toggles a visual glitch tint; liarInDeck enables per-slot downgrade rolls.
        /// </summary>
        void Show(IReadOnlyList<CardFamily> families, int count = 3, bool isLying = false, bool liarInDeck = false);

        void Hide();
    }
}


