namespace WH.Gameplay.Cards
{
    /// <summary>Integer opcodes carried by CardEffectDef when type == Custom.</summary>
    public static class CardCustomOp
    {
        public const int RevealIntent = 1;           // Glare
        public const int MarkEnemyEye = 2;           // Glare (Phase 4 will read this)
        public const int ExhaustOneCurseFromHand = 3;// Stitch
        public const int EndTurnLoseHp = 4;          // Parasite (value = hp loss)
        public const int BleedBonusNext = 5;         // Clot Shield (value = +stacks next Bleed)
        public const int Unplayable = 9;             // Wound/Parasite: UI/resolver should block
    }
}

