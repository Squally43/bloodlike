using System;
using System.Collections.Generic;
using UnityEngine;

namespace WH.Gameplay.Cards
{
    /// <summary>Scriptable definition of a playable card.</summary>
    [CreateAssetMenu(menuName = "WH/Cards/CardData", fileName = "SO_Card_")]
    public class CardData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName = "New Card";
        [SerializeField] private CardFamily family = CardFamily.Neutral;
        [SerializeField] private CardRarity rarity = CardRarity.Common;

        [Header("Costs")]
        [Min(0)][SerializeField] private int costPulse = 1;
        [SerializeField] private SideCosts sideCosts;

        [Header("Common Numbers")]
        [Min(0)][SerializeField] private int baseDamage;
        [Min(0)][SerializeField] private int baseBlock;
        [Min(0)][SerializeField] private int scryAmount;
        [Min(0)][SerializeField] private int drawAmount;

        [Header("Flags")]
        [SerializeField] private bool exhaustAfterPlay;
        [SerializeField] private bool retainInHand;

        [Header("Effects (Resolver will process in order)")]
        [SerializeField] private List<CardEffectDef> effects = new();

        [Header("FX / SFX (optional)")]
        [SerializeField] private AudioClip sfxOnPlay;
        [SerializeField] private GameObject vfxOnPlay;
        [Header("Visuals (optional)")]
        [SerializeField] private Material baseMaterial;     // e.g., your existing UI/Default clone or stylized mat
        [SerializeField] private Material overlayMaterial;  // e.g., your holographic/foil shader mat (optional)
        [SerializeField] private Color baseTint = Color.white;
        [Range(0, 1)][SerializeField] private float overlayOpacity = 0.6f;
        #region Public API
        /// <summary>Stable content identifier (auto-generated if empty).</summary>
        public string Id => id;
        public string DisplayName => displayName;
        public CardFamily Family => family;
        public CardRarity Rarity => rarity;
        public int CostPulse => costPulse;
        public SideCosts SideCosts => sideCosts;
        public int BaseDamage => baseDamage;
        public int BaseBlock => baseBlock;
        public int ScryAmount => scryAmount;
        public int DrawAmount => drawAmount;
        public bool ExhaustAfterPlay => exhaustAfterPlay;
        public bool RetainInHand => retainInHand;
        public IReadOnlyList<CardEffectDef> Effects => effects;
        public AudioClip SfxOnPlay => sfxOnPlay;
        public GameObject VfxOnPlay => vfxOnPlay;
        public Material BaseMaterial => baseMaterial;
        public Material OverlayMaterial => overlayMaterial;
        public Color BaseTint => baseTint;
        public float OverlayOpacity => overlayOpacity;
        #endregion

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;

            // Autogenerate a GUID once.
            if (string.IsNullOrEmpty(id))
                id = System.Guid.NewGuid().ToString("N");

            // Basic sanity clamps
            costPulse = Mathf.Max(0, costPulse);
            baseDamage = Mathf.Max(0, baseDamage);
            baseBlock = Mathf.Max(0, baseBlock);
            scryAmount = Mathf.Max(0, scryAmount);
            drawAmount = Mathf.Max(0, drawAmount);
        }

        /// <summary>Quick summary for debug overlays.</summary>
        public override string ToString()
            => $"{displayName} [{family}/{rarity}] Cost:{costPulse}";
    }
}

