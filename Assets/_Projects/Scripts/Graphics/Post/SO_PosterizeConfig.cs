using UnityEngine;

namespace WH.Core
{
    [CreateAssetMenu(menuName = "WH/FX/Posterize Config")]
    public class SO_PosterizeConfig : ScriptableObject
    {
        [Range(2, 12)] public float levels = 5f;
        [Range(0f, 0.2f)] public float ditherAmp = 0.03f;
        [Range(0.5f, 2f)] public float contrast = 1f;
        [Range(0f, 2f)] public float saturation = 1f;
        [Range(0.5f, 2.2f)] public float gamma = 1f;
        public Texture2D ditherTex;
    }
}

