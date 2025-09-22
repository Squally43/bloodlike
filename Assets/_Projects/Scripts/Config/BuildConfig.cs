using UnityEngine;

namespace WH
{
    [CreateAssetMenu(menuName = "WH/Config/BuildConfig", fileName = "SO_BuildConfig")]
    public sealed class BuildConfig : ScriptableObject
    {
        [Header("Feature Flags")]
        public bool enableLying = true;
        public bool enableGlitchTint = true;

        [Header("Picker Sizes")]
        [Min(1)] public int honestCount = 3;
        [Min(1)] public int lyingCount = 2;

        [Header("LIAR Mechanics")]
        [Range(0f, 1f)] public float liarCorruptionChance = 0.15f;
        [Range(0f, 1f)] public float liarDowngradeChance = 0.25f;

        [Header("Random Seeds (optional)")]
        public int pickerSeed = 0;
        public int liarSeed = 0;
    }
}



