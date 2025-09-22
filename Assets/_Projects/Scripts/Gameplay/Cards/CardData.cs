using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WH.Gameplay;                // <-- CardFamily, CardRarity, CardEffectDef, SideCosts live here

namespace WH.Gameplay.Cards
{
    /// <summary>Scriptable definition of a playable card – designer-friendly.</summary>
    [CreateAssetMenu(menuName = "WH/Cards/CardData", fileName = "SO_Card_")]
    public class CardData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName = "New Card";
        [SerializeField] private CardFamily family = CardFamily.Neutral; // Neutral, Skin, Eye
        [SerializeField] private CardRarity rarity = CardRarity.Common;  // Common/Uncommon/Rare/Curse

        [Header("Costs")]
        [Min(0)][SerializeField] private int costPulse = 1;
        [SerializeField] private SideCosts sideCosts;

        [Header("Core Numbers (auto-mentioned in rules)")]
        [Min(0)][SerializeField] private int baseDamage;   // Deal X
        [Min(0)][SerializeField] private int baseBlock;    // Gain X Block
        [Min(0)][SerializeField] private int scryAmount;   // Scry X
        [Min(0)][SerializeField] private int drawAmount;   // Draw X

        [Header("Flags")]
        [SerializeField] private bool exhaustAfterPlay;
        [SerializeField] private bool retainInHand;

        // ---------------- DESIGNER-FACING EFFECTS ----------------
        [Header("Designer Effects (readable)")]
        [SerializeField] private List<DesignerEffectRow> designerEffects = new();

        [Tooltip("Optional override. If empty, we auto-compose from fields + designer effects.")]
        [TextArea(2, 6)]
        [SerializeField] private string rulesTextOverride;

        // ---------------- MECHANICAL EFFECTS ----------------
        [Header("Mechanics (Advanced) — resolver processes in order")]
        [SerializeField] private List<CardEffectDef> effects = new();

        [Header("FX (optional)")]
        [SerializeField] private AudioClip sfxOnPlay;
        [SerializeField] private GameObject vfxOnPlay;

        [Header("Visuals (optional)")]
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private Color baseTint = Color.white;
        [Range(0, 1)][SerializeField] private float overlayOpacity = 0.6f;

        // ---------------- PUBLIC API ----------------
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
        // True if this card should never be playable (Wound, etc.)
        public bool IsUnplayable
        {
            get
            {
                // Advanced effects list
                if (effects != null)
                {
                    for (int i = 0; i < effects.Count; i++)
                        if (effects[i].type == CardEffectType.Custom && effects[i].value == CardCustomOp.Unplayable)
                            return true;
                }

                // Designer effects list
                if (designerEffects != null)
                {
                    for (int i = 0; i < designerEffects.Count; i++)
                        if (designerEffects[i].customOp == DesignerCustomOp.Unplayable)
                            return true;
                }

                return false;
            }
        }

        public AudioClip SfxOnPlay => sfxOnPlay;
        public GameObject VfxOnPlay => vfxOnPlay;
        public Material BaseMaterial => baseMaterial;
        public Material OverlayMaterial => overlayMaterial;
        public Color BaseTint => baseTint;
        public float OverlayOpacity => overlayOpacity;

        /// <summary>Formatted rules line for UI.</summary>
        public string RulesLine => ComposeRulesLine();

        // -------- Designer helpers --------
        [Serializable]
        public struct DesignerEffectRow
        {
            [Tooltip("Readable custom op (maps to CardCustomOp).")]
            public DesignerCustomOp customOp;

            [Tooltip("Primary number this row uses (e.g., stacks, HP loss).")]
            public int number;

            [Tooltip("Optional extra note. Use {X} to inject the number.")]
            [TextArea(1, 3)] public string note;

            [Tooltip("If true, include in generated Rules text.")]
            public bool showInRules;

            public DesignerEffectRow(DesignerCustomOp op, int n = 0, string note = "", bool show = true)
            { customOp = op; number = n; this.note = note; showInRules = show; }
        }

        /// <summary>Readable enum for designers; mirrors CardCustomOp values.</summary>
        public enum DesignerCustomOp
        {
            None = 0,
            RevealIntent = CardCustomOp.RevealIntent,
            MarkEnemyEye = CardCustomOp.MarkEnemyEye,
            ExhaustOneCurseFromHand = CardCustomOp.ExhaustOneCurseFromHand,
            EndTurnLoseHp = CardCustomOp.EndTurnLoseHp,
            BleedBonusNext = CardCustomOp.BleedBonusNext,
            Unplayable = CardCustomOp.Unplayable
        }

        private string ComposeRulesLine()
        {
            if (!string.IsNullOrWhiteSpace(rulesTextOverride))
                return rulesTextOverride.Trim();

            var sb = new StringBuilder(64);

            // Numbers-first phrasing
            if (baseDamage > 0) sb.Append($"Deal {baseDamage}. ");
            if (baseBlock > 0) sb.Append($"Gain {baseBlock} Block. ");
            if (drawAmount > 0) sb.Append($"Draw {drawAmount}. ");
            if (scryAmount > 0) sb.Append($"Scry {scryAmount}. ");
            if (exhaustAfterPlay) sb.Append("Exhaust. ");

            foreach (var row in designerEffects)
            {
                if (!row.showInRules || row.customOp == DesignerCustomOp.None) continue;
                sb.Append(RenderDesignerRow(row));
            }
            return sb.ToString().Trim();
        }

        private static string RenderDesignerRow(DesignerEffectRow row)
        {
            switch (row.customOp)
            {
                case DesignerCustomOp.RevealIntent: return "Reveal intent. ";
                case DesignerCustomOp.MarkEnemyEye: return "Mark Eye. ";
                case DesignerCustomOp.ExhaustOneCurseFromHand: return "Exhaust a Curse in hand. ";
                case DesignerCustomOp.EndTurnLoseHp: return $"End of turn: lose {Mathf.Max(1, row.number)} HP. ";
                case DesignerCustomOp.BleedBonusNext: return $"Next time you apply Bleed this combat, +{Mathf.Max(1, row.number)} stack. ";
                case DesignerCustomOp.Unplayable: return "Unplayable. ";
                default: break;
            }
            if (!string.IsNullOrWhiteSpace(row.note))
                return row.note.Replace("{X}", row.number.ToString()) + " ";
            return string.Empty;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
            if (string.IsNullOrEmpty(id)) id = System.Guid.NewGuid().ToString("N");

            costPulse = Mathf.Max(0, costPulse);
            baseDamage = Mathf.Max(0, baseDamage);
            baseBlock = Mathf.Max(0, baseBlock);
            scryAmount = Mathf.Max(0, scryAmount);
            drawAmount = Mathf.Max(0, drawAmount);
        }

        public override string ToString() => $"{displayName} [{family}/{rarity}] Cost:{costPulse}";
    }

    /// <summary>Helper for UI tints without referencing WH.UI.</summary>
    public static class CardFamilyColors
    {
        public static Color Get(CardFamily f)
        {
            switch (f)
            {
                case CardFamily.Skin: return new Color32(245, 245, 245, 255); // soft white
                case CardFamily.Eye: return new Color32(255, 222, 125, 255); // amber
                default: return new Color32(190, 190, 190, 255); // neutral gray
            }
        }
    }
}



