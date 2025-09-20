using System;
using System.Collections.Generic;
using UnityEngine;

namespace WH.Gameplay.QTE
{
    public enum QteMode { Slider, Hold, Mash }

    /// <summary>
    /// MVP QTE placeholder:
    ///  - StartQte(...) arms the QTE and waits for input.
    ///  - SPACE or Left Mouse => success.
    ///  - ESC or Backspace    => fail.
    ///  - Complete(bool)      => resolve programmatically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QTEController : MonoBehaviour
    {
        public event Action<bool, IReadOnlyList<WH.Gameplay.CardFamily>> OnQteResolved;

        private bool _active;
        private QteMode _mode;
        private float _difficulty;
        private IReadOnlyList<WH.Gameplay.CardFamily> _preferred;
        [SerializeField] private bool _log = true;

        public void StartQte(QteMode mode, float difficulty, IReadOnlyList<WH.Gameplay.CardFamily> preferred)
        {
            _mode = mode;
            _difficulty = Mathf.Clamp01(difficulty);
            _preferred = preferred;
            _active = true;

            if (_log) Debug.Log($"[QTE] START mode={_mode} diff={_difficulty}. Press SPACE/LMB=success, ESC/BACKSPACE=fail.");
        }

        public void Complete(bool success)
        {
            if (!_active) return;
            _active = false;
            OnQteResolved?.Invoke(success, _preferred);
            if (_log) Debug.Log($"[QTE] RESOLVE success={success}");
        }

        public void Cancel()
        {
            if (!_active) return;
            _active = false;
            if (_log) Debug.Log("[QTE] CANCEL");
        }

        private void Update()
        {
            if (!_active) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                Complete(true);

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                Complete(false);
        }
    }
}

