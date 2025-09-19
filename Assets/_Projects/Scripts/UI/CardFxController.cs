// CardFxController.cs  — REPLACE your file content with this version
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

#if DOTWEEN_INSTALLED
using DG.Tweening;
#endif

namespace WH.UI
{
    [DisallowMultipleComponent]
    public sealed class CardFxController : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    IPointerMoveHandler 
    {
        [Header("Scale")]
        [SerializeField] private bool enableScale = true;
        [SerializeField] private float hoverScale = 1.06f;   // was 1.08
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float scaleDuration = 0.12f;

        [Header("Punch")]
        [SerializeField] private bool enablePunchOnHover = true;
        [SerializeField] private float punchDegrees = 6f;
        [SerializeField] private float punchDuration = 0.12f;

        [Header("Sorting (no layout moves)")]
        [Tooltip("Raise this card's draw order while hovered without changing sibling index.")]
        [SerializeField] private bool raiseSortingOnHover = true;
        [SerializeField] private int hoverSortingOrderBoost = 50;
        public event Action<CardFxController, bool> HoverChanged;
        RectTransform _rect;
        Transform _tr;
        Canvas _localCanvas;          // per-card canvas for safe z-ordering
        int _baseSortingOrder;
        bool _hovered, _pressed;
        [Header("Tilt")]
        [SerializeField] bool enableTilt = true;
        [SerializeField] float maxTiltX = 6f;   // tilt forward/back (deg)
        [SerializeField] float maxTiltY = 10f;  // tilt left/right (deg)
        [SerializeField] float tiltLerp = 12f;  // how fast it follows

        Vector2 _tiltTarget;     // [-1,1] x/y
        Coroutine _tiltRoutine;

        // --- Flash overlay ---
        Image _flash;            // created lazily
        [SerializeField] float flashDuration = 0.12f;
        Coroutine _scaleRoutine;
#if DOTWEEN_INSTALLED
        Tween _scaleTween, _punchTween;
#endif

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _tr = transform;

            _localCanvas = GetComponent<Canvas>();
            if (_localCanvas == null) _localCanvas = gameObject.AddComponent<Canvas>();

            // IMPORTANT: nested canvas needs its own GraphicRaycaster
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null) gameObject.AddComponent<GraphicRaycaster>();

            _localCanvas.overrideSorting = false; // we only raise on hover
            _baseSortingOrder = _localCanvas.sortingOrder;

            // IMPORTANT: Do NOT call SetAsLastSibling anywhere – that triggers layout rebuilds.
        }

        void OnDisable()
        {
            KillAnimations();
            _tr.localScale = Vector3.one;
            _tr.localEulerAngles = new Vector3(0, 0, 0);
            _hovered = _pressed = false;
            if (_tiltRoutine != null) { StopCoroutine(_tiltRoutine); _tiltRoutine = null; }
            _tiltTarget = Vector2.zero;
            if (_localCanvas != null)
            {
                _localCanvas.overrideSorting = false;
                _localCanvas.sortingOrder = _baseSortingOrder;
            }
        }

        void OnDestroy() => KillAnimations();

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            HoverChanged?.Invoke(this, true);

            if (raiseSortingOnHover && _localCanvas != null) { _localCanvas.overrideSorting = true; _localCanvas.sortingOrder = _baseSortingOrder + hoverSortingOrderBoost; }
            if (enableScale) AnimateScaleTo(hoverScale);
            if (enablePunchOnHover) Punch();

            if (enableTilt) StartTilt();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            HoverChanged?.Invoke(this, false);

            if (enableScale) AnimateScaleTo(1f);
            if (raiseSortingOnHover && _localCanvas != null && !_pressed) { _localCanvas.overrideSorting = false; _localCanvas.sortingOrder = _baseSortingOrder; }

            if (enableTilt) StopTilt();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = true;
            if (enableScale) AnimateScaleTo(pressScale);
            // TODO: SFX hook OnCardPress
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = false;
            if (enableScale) AnimateScaleTo(_hovered ? hoverScale : 1f);

            if (raiseSortingOnHover && _localCanvas != null && !_hovered)
            {
                _localCanvas.overrideSorting = false;
                _localCanvas.sortingOrder = _baseSortingOrder;
            }
        }
        public void OnPointerMove(PointerEventData eventData)
        {
            if (!enableTilt || !_hovered) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out var local))
            {
                // normalize to [-1,1] in card rect
                var half = _rect.rect.size * 0.5f;
                if (half.x > 0.1f && half.y > 0.1f)
                {
                    _tiltTarget = new Vector2(Mathf.Clamp(local.x / half.x, -1f, 1f),
                                              Mathf.Clamp(local.y / half.y, -1f, 1f));
                }
            }
        }

        void Punch()
        {
#if DOTWEEN_INSTALLED
            _punchTween?.Kill();
            _punchTween = _tr.DOPunchRotation(new Vector3(0, 0, punchDegrees), punchDuration, vibrato: 12, elasticity: 0.9f);
#else
            StartCoroutine(PunchRoutine());
#endif
        }

        void AnimateScaleTo(float target)
        {
#if DOTWEEN_INSTALLED
            _scaleTween?.Kill();
            _scaleTween = _tr.DOScale(target, scaleDuration).SetEase(DG.Tweening.Ease.OutBack, overshoot: 1.5f);
#else
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(ScaleRoutine(target, scaleDuration));
#endif
        }

        void KillAnimations()
        {
#if DOTWEEN_INSTALLED
            _scaleTween?.Kill();
            _punchTween?.Kill();
#endif
            if (_scaleRoutine != null)
            {
                StopCoroutine(_scaleRoutine);
                _scaleRoutine = null;
            }
        }
        public void PlayAcceptedFX()
        {
            AnimateScaleTo(1.0f);
            Punch();
            Flash(Color.white, flashDuration);
        }

        public void PlayDeniedFX()
        {
            StartCoroutine(DenyShakeRoutine());
        }


        System.Collections.IEnumerator DenyShakeRoutine()
        {
            float dur = 0.15f, t = 0f;
            float amp = 6f; // degrees
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Sin(t * 60f) * (1f - t / dur); // decaying
                var e = transform.localEulerAngles;
                e.z = s * amp;
                transform.localEulerAngles = e;
                yield return null;
            }
            var r = transform.localEulerAngles; r.z = 0f; transform.localEulerAngles = r;
        }
        // ------- Fallback coroutines (no DOTween) -------
        IEnumerator ScaleRoutine(float target, float duration)
        {
            Vector3 start = _tr.localScale;
            Vector3 end = new Vector3(target, target, 1f);
            float t = 0f;

            float OvershootEase(float x)
            {
                const float s = 1.5f;
                x = Mathf.Clamp01(x);
                return 1 + (s + 1) * Mathf.Pow(x - 1, 3) + s * Mathf.Pow(x - 1, 2);
            }

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = OvershootEase(t / duration);
                _tr.localScale = Vector3.LerpUnclamped(start, end, k);
                yield return null;
            }
            _tr.localScale = end;
        }

        IEnumerator PunchRoutine()
        {
            float dur = punchDuration;
            float t = 0f;
            float dir = Mathf.Sign(punchDegrees);
            float mag = Mathf.Abs(punchDegrees);

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = t / dur;
                float angle = dir * mag * Mathf.Sin(u * Mathf.PI);
                var e = _tr.localEulerAngles;
                e.z = angle;
                _tr.localEulerAngles = e;
                yield return null;
            }
            _tr.localEulerAngles = Vector3.zero;
        }

        void StartTilt()
        {
            _tiltTarget = Vector2.zero;
            if (_tiltRoutine != null) StopCoroutine(_tiltRoutine);
            _tiltRoutine = StartCoroutine(TiltRoutine());
        }
        void StopTilt()
        {
            _tiltTarget = Vector2.zero; // lerp back to flat
                                        // keep routine; it will ease back to zero and stop itself
        }
        IEnumerator TiltRoutine()
        {
            while (_hovered || _tiltTarget.sqrMagnitude > 0.0001f)
            {
                // desired x/y tilt from target, preserve whatever Z the layout sets
                float tx = -_tiltTarget.y * maxTiltX; // move mouse up → tilt “away”
                float ty = _tiltTarget.x * maxTiltY;

                // Lerp current x/y toward target, keep z from current
                var e = _tr.localEulerAngles;
                // convert to signed (-180..180) for smooth lerp
                float curX = (e.x > 180f) ? e.x - 360f : e.x;
                float curY = (e.y > 180f) ? e.y - 360f : e.y;

                curX = Mathf.Lerp(curX, tx, 1f - Mathf.Exp(-tiltLerp * Time.unscaledDeltaTime));
                curY = Mathf.Lerp(curY, ty, 1f - Mathf.Exp(-tiltLerp * Time.unscaledDeltaTime));

                _tr.localEulerAngles = new Vector3(curX, curY, e.z); // keep Z (fan/punch)

                // decays to zero after exit
                yield return null;
            }
            // snap back x/y, preserve z
            var ez = _tr.localEulerAngles; _tr.localEulerAngles = new Vector3(0, 0, ez.z);
            _tiltRoutine = null;
        }
        void EnsureFlash()
        {
            if (_flash != null) return;
            var go = new GameObject("FX_Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_rect, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _flash = go.GetComponent<Image>();
            _flash.raycastTarget = false;
            _flash.color = new Color(1, 1, 1, 0);
        }

        public void Flash(Color color, float dur)
        {
            EnsureFlash();
            StartCoroutine(FlashRoutine(color, dur));
        }
        IEnumerator FlashRoutine(Color color, float dur)
        {
            float t = 0f;
            // quick up then down
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = t / dur;
                float a = (u < 0.3f) ? (u / 0.3f) : (1f - (u - 0.3f) / 0.7f); // rise fast then fade
                if (_flash) _flash.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(a));
                yield return null;
            }
            if (_flash) _flash.color = new Color(color.r, color.g, color.b, 0f);
        }

    }
}


