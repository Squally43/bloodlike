using System;
using UnityEngine;

namespace WH.Gameplay
{
    /// <summary>Families tie cards/tools/harvest parts together thematically.</summary>
    public enum CardFamily
    {
        Neutral = 0,
        Skin = 10,   // Scalpel
        Eye = 20,   // Pliers
        Blood = 30,  // (future)
        Limb = 40   // (future)
    }

    /// <summary>Rarity buckets for reward weighting.</summary>
    public enum CardRarity { Common, Uncommon, Rare, Curse }

    /// <summary>Targets an effect can apply to.</summary>
    public enum EffectTarget { Self, Enemy, AllEnemies, RandomEnemy }

    /// <summary>Canonical status kinds for typed effects.</summary>
    public enum StatusKind { Bleed, Weak, Blind, Stagger }

    /// <summary>Card effect opcodes for simple, data-driven resolution.</summary>
    public enum CardEffectType
    {
        Damage, GainBlock, Draw, Scry, GainPulse,
        ApplyStatus, RemoveStatus,
        ExhaustSelf, RetainSelf,
        Custom // escape hatch for scripted specials later
    }

    /// <summary>What body part an enemy prefers to steal on loss.</summary>
    public enum BodyTag { Skin, Eye /* expand later: Arm, Leg, Heart, etc. */ }

    /// <summary>Intent types for simple enemy scripting.</summary>
    public enum IntentType { Attack, Block, ApplyStatus, MultiAttack, DoNothing, Special }

    [Serializable]
    public struct SideCosts
    {
        [Min(0)] public int hpCost;
        [Min(0)] public int maxHpCost;
        public bool addWoundOnPlay;
        public bool HasAnyCost => hpCost > 0 || maxHpCost > 0 || addWoundOnPlay;
    }

    [Serializable]
    public struct CardEffectDef
    {
        public CardEffectType type;
        public EffectTarget target;

        [Tooltip("Primary magnitude: e.g., damage, block, stacks, scry amount.")]
        public int value;

        [Tooltip("Optional secondary value (e.g., multi-attack hits).")]
        public int value2;

        [Tooltip("For ApplyStatus/RemoveStatus.")]
        public StatusKind status;
    }

    public struct EnemyIntentStep
    {
        public IntentType type;
        [Tooltip("Primary value: damage per hit, block amount, or status stacks.")]
        public int value;
        [Tooltip("For MultiAttack: number of hits. For status: ignored.")]
        public int times;
        public StatusKind status;
    }

    [Serializable]
    public struct HarvestNode
    {
        public CardFamily family;
        [Range(0, 100)] public int rarityWeight_Common;
        [Range(0, 100)] public int rarityWeight_Uncommon;
        [Range(0, 100)] public int rarityWeight_Rare;
    }
}
