using System;
using UnityEngine;

namespace WH.Gameplay.Cards
{
    [Serializable]
    public struct SideCosts
    {
        [Min(0)] public int loseHp;          // optional self-HP cost (unused for MVP)
        [Min(0)] public int discardRandom;   // optional discard (unused for MVP)
        // Extend later if a card needs quirky side costs.
    }
}

