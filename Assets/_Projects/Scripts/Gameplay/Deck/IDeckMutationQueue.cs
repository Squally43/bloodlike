using System;

namespace WH.Gameplay
{
    /// <summary>Queued mutation applied when the next fight's deck is built.</summary>
    public interface IDeckMutationQueue
    {
        /// <summary>Return true if a card was replaced. Implementation decides eligibility (non-starter, non-curse).</summary>
        bool TryTransformRandomNonStarterNonCurseTo(string replacementCardId);

        /// <summary>Optional: inject a specific card for next fight.</summary>
        void QueueExtraForNextFight(string cardId);
    }
}

