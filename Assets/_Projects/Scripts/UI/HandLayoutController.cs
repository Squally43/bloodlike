using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WH.UI
{
    /// <summary>
    /// Non-blocking, coroutine-based hand layout:
    /// - Fans cards along a shallow arc.
    /// - Widens a gap around the hovered card.
    /// - No Update loop; applies when hand changes or hover changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HandLayoutController : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] float cardSpacing = 16f;          // base X gap between cards (px)
        [SerializeField] float fanAngleMax = 6f;           // max +/- Z rotation at extremes (deg)
        [SerializeField] float arcHeight = 18f;            // vertical arc height (px)
        [SerializeField] float hoverExtraGap = 32f;        // extra X gap inserted at hovered index
        [SerializeField] float layoutDuration = 0.12f;     // lerp duration for moves
        [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] float fallbackCardWidth = 220f;
        // ...
        [SerializeField] float nextLayoutPerIndexDelay = 0f; // 0 = no stagger once
        public void SetNextLayoutStagger(float perIndexDelay) => nextLayoutPerIndexDelay = Mathf.Max(0f, perIndexDelay);

        RectTransform _root;
        HorizontalLayoutGroup _hlg;                        // we disable this in Awake
        readonly List<RectTransform> _cards = new();
        int _hoveredIndex = -1;

        // Track per-card running animations so we can cancel cleanly
        readonly Dictionary<RectTransform, Coroutine> _anim = new();

        void Awake()
        {
            _root = GetComponent<RectTransform>();
            _hlg = GetComponent<HorizontalLayoutGroup>();
            if (_hlg != null) _hlg.enabled = false;  // we take over positioning
        }

        void OnDisable()
        {
            // snap/reset to avoid stale coroutines if the hand is hidden
            foreach (var kv in _anim) if (kv.Value != null) StopCoroutine(kv.Value);
            _anim.Clear();
        }

        public void Clear()
        {
            // Called before hand rebuild if you destroy cards
            UnsubscribeAll();
            _cards.Clear();
            _hoveredIndex = -1;
        }

        public void RegisterCard(RectTransform card, CardFxController fx)
        {
            if (card == null) return;
            _cards.Add(card);

            // Ensure we control layout
            var le = card.GetComponent<LayoutElement>();
            if (le == null) le = card.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // critical: HorizontalLayoutGroup must not move these

            // Subscribe to hover
            if (fx != null)
                fx.HoverChanged += OnCardHoverChanged;
        }

        public void OnHandRebuilt()
        {
            // re-index after any spawn/remove
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] == null) { _cards.RemoveAt(i); i--; }

            ApplyLayout();
        }

        void OnCardHoverChanged(CardFxController fx, bool isHovered)
        {
            // Find index once; we rely on sibling order in _cards list
            var rt = fx.transform as RectTransform;
            _hoveredIndex = isHovered ? _cards.IndexOf(rt) : (_hoveredIndex == _cards.IndexOf(rt) ? -1 : _hoveredIndex);
            ApplyLayout();
        }

        void UnsubscribeAll()
        {
            foreach (var rt in _cards)
            {
                if (rt == null) continue;
                var fx = rt.GetComponent<CardFxController>();
                if (fx != null) fx.HoverChanged -= OnCardHoverChanged;
            }
        }

        void ApplyLayout()
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] == null) { _cards.RemoveAt(i); i--; }

            if (_cards.Count == 0) return;

            // Compute total width we’ll occupy
            float totalW = 0f;
            var widths = new float[_cards.Count];
            for (int i = 0; i < _cards.Count; i++)
            {
                var le = _cards[i].GetComponent<LayoutElement>();
                float w = (le != null && le.preferredWidth > 0f) ? le.preferredWidth : _cards[i].rect.width;
                widths[i] = w;
                totalW += (i == 0 ? 0f : cardSpacing) + w;
            }

            // Insert hover gap
            float hoverGap = (_hoveredIndex >= 0) ? hoverExtraGap : 0f;
            if (hoverGap > 0f) totalW += hoverGap;

            // Layout origin = centered in parent
            float startX = -totalW * 0.5f;

            // Place cards left → right
            float x = startX;
            for (int i = 0; i < _cards.Count; i++)
            {
                // If we’ve passed the hovered index, add the extra gap once
                if (hoverGap > 0f && _hoveredIndex >= 0 && i > _hoveredIndex)
                    x += hoverGap;

                float w = widths[i];
                float centerX = x + w * 0.5f;

                // t in [-1, +1] across the hand for fan/arc
                float t = (_cards.Count == 1) ? 0f : Mathf.Lerp(-1f, +1f, i / (float)(_cards.Count - 1));

                float rotZ = Mathf.Lerp(-fanAngleMax, +fanAngleMax, (t + 1f) * 0.5f);
                float y = -Arc(t); // downwards small arc looks “cupped”

                var targetPos = new Vector2(centerX, y);
                var targetRot = Quaternion.Euler(0, 0, rotZ);

                float delay = (nextLayoutPerIndexDelay > 0f) ? nextLayoutPerIndexDelay * i : 0f;
                StartMove(_cards[i], targetPos, targetRot, layoutDuration, delay);


                x += w + cardSpacing;
            }
            nextLayoutPerIndexDelay = 0f; // consume the one-shot stagger

        }

        float Arc(float tNormalized)
        {
            // Parabola: peak at center, zero at ends
            float u = 1f - Mathf.Abs(tNormalized); // 1 at center, 0 at ends
            return arcHeight * (u * u);
        }

        void StartMove(RectTransform rt, Vector2 targetAnchoredPos, Quaternion targetRot, float dur, float startDelay = 0f)
        {
            if (_anim.TryGetValue(rt, out var running) && running != null) StopCoroutine(running);
            _anim[rt] = StartCoroutine(MoveRoutine(rt, targetAnchoredPos, targetRot, dur, startDelay));
        }

        IEnumerator MoveRoutine(RectTransform rt, Vector2 targetPos, Quaternion targetRot, float dur, float startDelay)
        {
            if (rt == null) yield break;

            // optional stagger
            float waited = 0f;
            while (waited < startDelay)
            {
                waited += Time.unscaledDeltaTime;
                if (rt == null) yield break;
                yield return null;
            }

            Vector2 startPos = rt.anchoredPosition;
            Quaternion startRot = rt.localRotation;
            float t = 0f;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / dur));
                if (rt == null) yield break;

                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, k);
                rt.localRotation = Quaternion.SlerpUnclamped(startRot, targetRot, k);
                yield return null;
            }
            if (rt != null)
            {
                rt.anchoredPosition = targetPos;
                rt.localRotation = targetRot;
            }
            _anim[rt] = null;
        }
        public void StopAnimating(RectTransform rt)
        {
            if (rt == null) return;
            if (_anim.TryGetValue(rt, out var running) && running != null)
                StopCoroutine(running);
            _anim.Remove(rt);
        }

    }
}

