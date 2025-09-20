using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WH.Gameplay.Cards;

namespace WH.UI
{
    /// <summary>Plain UGUI card. Knows only how to present data and click.</summary>
    [DisallowMultipleComponent]
    public sealed class CardView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image _bg;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _cost;
        [SerializeField] private TMP_Text _rules;
        [SerializeField] private TMP_Text _pulseReadout;
        [SerializeField] private Button _button;

        private Action<CardView> _onClick;
        public UnityEngine.Object BoundData { get; private set; }
        private CardData _data;

        private void Reset()
        {
            // Try to grab common components on the same object.
            if (_bg == null) _bg = GetComponent<Image>();
            if (_button == null) _button = GetComponent<Button>();
        }

        private void Awake()
        {
            // --------- Self-heal missing references ---------
            if (_bg == null)
            {
                // Prefer an Image on self, else first Image in children.
                _bg = GetComponent<Image>();
                if (_bg == null) _bg = GetComponentInChildren<Image>(true);
            }

            if (_button == null)
            {
                _button = GetComponent<Button>();
                if (_button == null) _button = GetComponentInChildren<Button>(true);
            }

            if (_name == null || _cost == null || _rules == null || _pulseReadout == null)
            {
                // Pick the first TMP_Texts we can find if not explicitly wired.
                var texts = GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    var n = t.gameObject.name.ToLowerInvariant();
                    if (_name == null && (n.Contains("name") || n.EndsWith("_name"))) _name = t;
                    else if (_cost == null && (n.Contains("cost") || n.Contains("pulse"))) _cost = t;
                    else if (_rules == null && (n.Contains("rules") || n.Contains("text") || n.Contains("desc"))) _rules = t;
                    else if (_pulseReadout == null && (n.Contains("readout") || n.Contains("resource"))) _pulseReadout = t;
                }
            }

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _onClick?.Invoke(this));
                _button.interactable = true;
            }
        }

        /// <summary>
        /// Bind all visible fields. Any missing UI refs are safely ignored after best-effort auto-wiring.
        /// </summary>
        public void Bind(CardData data, string displayName, string rulesLine, int cost, string pulseText, Color bgColor, Action<CardView> onClick)
        {
            _data = data;
            BoundData = data;

            if (_name) _name.text = string.IsNullOrEmpty(displayName) ? "Card" : displayName;
            if (_rules) _rules.text = string.IsNullOrEmpty(rulesLine) ? "" : rulesLine;
            if (_cost) _cost.text = cost.ToString();
            if (_pulseReadout) _pulseReadout.text = pulseText ?? "";

            // 1) Apply materials/overlay first (so they don't stomp our tint)
            var applier = GetComponent<CardStyleApplier>();
            if (applier && data != null)
                applier.ApplyFrom(data);

            // 2) Now apply the final tint we want (family or data tint)
            if (_bg)
            {
                var c = _bg.color;
                _bg.color = new Color(bgColor.r, bgColor.g, bgColor.b, c.a == 0 ? bgColor.a : c.a);
            }

            _onClick = onClick;
            SetInteractable(true);
        }

        public void SetInteractable(bool value)
        {
            if (_button) _button.interactable = value;
            if (_bg)
            {
                var c = _bg.color;
                _bg.color = value ? c : new Color(c.r, c.g, c.b, 0.4f);
            }
        }

        public void PlayFlashThenDisable()
        {
            if (!gameObject.activeInHierarchy) return;
            StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            if (_bg)
            {
                float t = 0f;
                var start = _bg.color;
                var flash = Color.white;
                while (t < 0.08f)
                {
                    t += Time.unscaledDeltaTime;
                    _bg.color = Color.Lerp(start, flash, Mathf.PingPong(t * 8f, 1f));
                    yield return null;
                }
                _bg.color = start;
            }
            SetInteractable(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_bg == null) _bg = GetComponent<Image>();
            if (_button == null) _button = GetComponent<Button>();
        }
#endif
    }
}



