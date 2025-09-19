using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WH.UI
{
    [DisallowMultipleComponent, ExecuteAlways]
    public sealed class CardStyleApplier : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] Image targetImage;      // root card Image (required)
        [SerializeField] Image overlayImage;     // optional child overlay (created if missing)

        [Header("Default Fallback (optional)")]
        [SerializeField] Material defaultBaseMaterial;
        [SerializeField] Material defaultOverlayMaterial;
        [SerializeField] Color defaultBaseTint = Color.white;
        [Range(0, 1)][SerializeField] float defaultOverlayOpacity = 0.6f;

        [Header("Editor Preview")]
        [Tooltip("Drag a CardData here to preview its materials in edit mode.")]
        [SerializeField] ScriptableObject previewCard; // use CardData here
        [SerializeField] bool livePreview = true;      // auto-update in edit mode

        Material _runtimeBaseMat;
        Material _runtimeOverlayMat;

        void Reset()
        {
            if (!targetImage) targetImage = GetComponent<Image>();
            EnsureOverlay();
        }

        void OnEnable()
        {
            // In edit mode, apply preview if assigned
            if (!Application.isPlaying && livePreview && previewCard)
                ApplyFrom(previewCard);
        }

        void OnValidate()
        {
            EnsureOverlay();

            if (!Application.isPlaying && livePreview && previewCard)
                ApplyFrom(previewCard);
        }

        void OnDestroy()
        {
            if (_runtimeBaseMat) DestroyImmediate(_runtimeBaseMat);
            if (_runtimeOverlayMat) DestroyImmediate(_runtimeOverlayMat);
        }

        void EnsureOverlay()
        {
            if (overlayImage) return;
            var go = new GameObject("FX_Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            overlayImage = go.GetComponent<Image>();
            overlayImage.raycastTarget = false; // never block clicks
            overlayImage.color = new Color(1, 1, 1, 0);
        }

        /// <summary>Apply visuals from a CardData or a style ScriptableObject.</summary>
        public void ApplyFrom(ScriptableObject dataObject)
        {
            if (!targetImage || dataObject == null) return;

            // We only care about CardData fields (safe cast)
            var type = dataObject.GetType();
            var baseMat = GetField<Material>(dataObject, type, "baseMaterial") ?? defaultBaseMaterial;
            var overlayMat = GetField<Material>(dataObject, type, "overlayMaterial") ?? defaultOverlayMaterial;
            var baseTint = GetField<Color>(dataObject, type, "baseTint", defaultBaseTint);
            var overlayOp = GetField<float>(dataObject, type, "overlayOpacity", defaultOverlayOpacity);

            ApplyMaterials(baseMat, overlayMat, baseTint, overlayOp);
        }

        /// <summary>Apply visuals explicitly (e.g., from BattleHUD).</summary>
        public void ApplyMaterials(Material baseMat, Material overlayMat, Color baseTint, float overlayOpacity)
        {
            // Base
            if (_runtimeBaseMat) DestroySafe(_runtimeBaseMat);
            _runtimeBaseMat = baseMat ? new Material(baseMat) : null;
            targetImage.material = _runtimeBaseMat;
            targetImage.color = baseTint;

            // Overlay
            EnsureOverlay();
            if (_runtimeOverlayMat) DestroySafe(_runtimeOverlayMat);

            if (overlayMat)
            {
                _runtimeOverlayMat = new Material(overlayMat);
                overlayImage.material = _runtimeOverlayMat;
                overlayImage.color = new Color(1, 1, 1, overlayOpacity);
            }
            else
            {
                overlayImage.material = null;
                overlayImage.color = new Color(1, 1, 1, 0);
            }
        }

        static T GetField<T>(object obj, System.Type t, string name, T fallback = default)
        {
            var f = t.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null || !typeof(T).IsAssignableFrom(f.FieldType)) return fallback;
            var v = f.GetValue(obj);
            return v is T tv ? tv : fallback;
        }

        static void DestroySafe(Object o)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(o);
            else Destroy(o);
#else
            Destroy(o);
#endif
        }
    }
}


