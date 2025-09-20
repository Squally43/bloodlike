namespace WH.Gameplay.Systems
{
    /// <summary>Anyone that owns the deck for the next fight implements this.</summary>
    public interface INextFightQueue
    {
        void QueueCardNextFight(WH.Gameplay.Cards.CardData card);
    }
}

