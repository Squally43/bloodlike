using System;
using System.Collections;
using TMPro;                 // ✅ TMP
using UnityEngine;
using UnityEngine.UI;       // ✅ Image, Button

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

        private void Reset()
        {
            _bg = GetComponent<Image>();
            _button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _onClick?.Invoke(this));
        }

        public void Bind(UnityEngine.Object data, string displayName, string rulesLine, int cost, string pulseText, Color bgColor, Action<CardView> onClick)
        {
            BoundData = data;
            if (_name) _name.text = string.IsNullOrEmpty(displayName) ? "Card" : displayName;
            if (_rules) _rules.text = rulesLine ?? "";
            if (_cost) _cost.text = cost > 0 ? cost.ToString() : "0";
            if (_pulseReadout) _pulseReadout.text = pulseText ?? "";
            if (_bg) _bg.color = bgColor;

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
    }
}


