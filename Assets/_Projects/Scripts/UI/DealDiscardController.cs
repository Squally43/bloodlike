using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WH.UI
{
    [DisallowMultipleComponent]
    public sealed class DealDiscardController : MonoBehaviour
    {
        [Header("Anchors (same Canvas)")]
        [SerializeField] RectTransform deckAnchor;
        [SerializeField] RectTransform discardAnchor;

        [Header("Timings")]
        [SerializeField] float dealFadeDuration = 0.10f;
        [SerializeField] float dealStagger = 0.04f;
        [SerializeField] float discardDuration = 0.18f;

        [Header("Easing")]
        [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        RectTransform _root;

        void Awake() { _root = transform as RectTransform; }

        // --- DEAL ---

        /// Call immediately after you instantiate the card under Hand_Container.
        public void InitCardForDeal(RectTransform card)
        {
            var cg = card.GetComponent<CanvasGroup>() ?? card.gameObject.AddComponent<CanvasGroup>();

            if (deckAnchor != null)
            {
                card.position = deckAnchor.position;
                card.rotation = deckAnchor.rotation;
                cg.alpha = 0f; // will fade in during stagger
            }
            else
            {
                cg.alpha = 1f; // no anchor → just appear and let layout move it
            }
        }

        /// After the hand is spawned and HandLayoutController.OnHandRebuilt() was called,
        /// run this to fade cards in with a small stagger (they'll already be flying to slots).
        public Coroutine PlayDealStagger(IList<RectTransform> cards, MonoBehaviour host)
            => host.StartCoroutine(DealStaggerRoutine(cards));

        IEnumerator DealStaggerRoutine(IList<RectTransform> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var rt = cards[i];
                if (rt == null) continue;

                var cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
                StartCoroutine(FadeRoutine(cg, 0f, 1f, dealFadeDuration));

                yield return new WaitForSecondsRealtime(dealStagger);
            }
        }

        // --- DISCARD ---
        void OnDisable()
        {
            StopAllCoroutines();
        }
        /// Animate a single card to discard and destroy it at the end.
        public void DiscardAndDespawn(RectTransform card)
        {
            if (card == null) return;
            StartCoroutine(DiscardRoutine(card));
        }
        public void DiscardAndDespawnMany(IList<RectTransform> cards)
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) DiscardAndDespawn(cards[i]);
        }
        IEnumerator DiscardRoutine(RectTransform card)
        {
            var cg = card.GetComponent<CanvasGroup>() ?? card.gameObject.AddComponent<CanvasGroup>();
            var startPos = card.position;
            var startRot = card.rotation;
            var endPos = (discardAnchor != null) ? discardAnchor.position : startPos + new Vector3(0, -200f, 0);
            var endRot = (discardAnchor != null) ? discardAnchor.rotation : Quaternion.identity;

            float t = 0f;
            while (t < discardDuration && card != null)
            {
                t += Time.unscaledDeltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / discardDuration));
                card.position = Vector3.LerpUnclamped(startPos, endPos, k);
                card.rotation = Quaternion.SlerpUnclamped(startRot, endRot, k);
                cg.alpha = 1f - k;
                yield return null;
            }

            if (card != null) Destroy(card.gameObject);
        }

        // --- helpers ---

        IEnumerator FadeRoutine(CanvasGroup cg, float a, float b, float d)
        {
            float t = 0f;
            while (t < d && cg != null)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.LerpUnclamped(a, b, Mathf.Clamp01(t / d));
                yield return null;
            }
            if (cg != null) cg.alpha = b;
        }
#if UNITY_EDITOR
        void OnValidate()
        {
            if (deckAnchor && discardAnchor)
            {
                var myCanvas = GetComponentInParent<Canvas>();
                var a = deckAnchor.GetComponentInParent<Canvas>();
                var b = discardAnchor.GetComponentInParent<Canvas>();
                if (myCanvas && (a != myCanvas || b != myCanvas))
                    Debug.LogWarning("[DealDiscardController] Deck/Discard anchors should be on the SAME Canvas as the hand container.", this);
            }
        }
#endif
    }
}

