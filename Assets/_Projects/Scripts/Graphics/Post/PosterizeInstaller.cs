using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WH.Core
{
    /// <summary>One-shot binder that applies an SO to a PosterizeFeature on the active URP Renderer.</summary>
    public class PosterizeInstaller : MonoBehaviour
    {
        [SerializeField] SO_PosterizeConfig config;
        [SerializeField] UniversalRendererData rendererData; // assign your active Renderer asset
        [SerializeField] string featureName = "PosterizeFeature"; // must match the Renderer Feature name

        void Awake()
        {
            if (config == null || rendererData == null)
            {
                Debug.LogWarning("[PosterizeInstaller] Missing config or rendererData.");
                return;
            }

            foreach (var f in rendererData.rendererFeatures)
            {
                if (f == null) continue;
                if (f.name != featureName) continue;

                var feature = f as WH.Gameplay.PosterizeFeature;
                if (feature == null)
                {
                    Debug.LogWarning($"[PosterizeInstaller] Feature '{featureName}' exists but type mismatch.");
                    return;
                }

                // Apply once at boot (no polling)
                feature.settings.levels = config.levels;
                feature.settings.ditherAmp = config.ditherAmp;
                feature.settings.contrast = config.contrast;
                feature.settings.saturation = config.saturation;
                feature.settings.gamma = config.gamma;
                feature.settings.ditherTex = config.ditherTex;

                // Force feature to recreate material/pass if needed
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(rendererData);
#endif
                Debug.Log("[PosterizeInstaller] Applied SO_PosterizeConfig to PosterizeFeature.");
                return;
            }

            Debug.LogWarning($"[PosterizeInstaller] Feature '{featureName}' not found on renderer.");
        }
    }
}

