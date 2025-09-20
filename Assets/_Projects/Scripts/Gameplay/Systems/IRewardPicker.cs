using System;
using System.Collections.Generic;
using WH.Gameplay;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// Gameplay-facing contract for any reward pickerSS UI.
    /// Lives in Gameplay so UI can depend on it without circular refs.
    /// </summary>
    public interface IRewardPicker
    {
        event Action<CardData> OnChosen;

        /// <summary>Show N options drawn from the given families (Skin/Eye). If empty, show mixed.</summary>
        void Show(IReadOnlyList<CardFamily> families, int count = 3);

        /// <summary>Hide/teardown the picker UI.</summary>
        void Hide();
    }
}

