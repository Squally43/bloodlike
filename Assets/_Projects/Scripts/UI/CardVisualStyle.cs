using UnityEngine;

namespace WH.UI
{
    [CreateAssetMenu(menuName = "WH/UI/Card Visual Style")]
    public sealed class CardVisualStyle : ScriptableObject
    {
        [Header("Identity")]
        public string styleId = "default";          // e.g., "holo", "rare", "legendary"

        [Header("Materials")]
        public Material baseMaterial;               // usually a clone of UI/Default
        public Material overlayMaterial;            // optional (foil/holo overlay)

        [Header("Tint & Misc")]
        public Color baseTint = Color.white;        // applied to root Image
        [Range(0, 1)] public float overlayOpacity = 0.6f;

        [Header("Holo Params (matched by property name in shader)")]
        [Range(0, 2)] public float FoilStrength = 1.0f;  // _FoilStrength
        [Range(0, 5)] public float FoilSpeed = 1.2f;  // _FoilSpeed
        [Range(0, 8)] public float FoilScale = 3.0f;  // _FoilScale
    }
}

