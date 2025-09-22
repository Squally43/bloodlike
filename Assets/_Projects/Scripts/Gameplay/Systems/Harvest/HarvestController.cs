using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WH.Gameplay.Systems
{
    /// <summary>
    /// Lets the player choose a body family (Skin/Eye) via key or button, then runs the QTE.
    /// Emits the QTE result + the chosen family.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarvestController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private WH.Gameplay.QTE.QTEController _qte;

        [Header("Selection (optional UI)")]
        [SerializeField] private GameObject _selectionPanel;  // simple panel with two buttons (optional)
        [SerializeField] private Button _btnSkin;
        [SerializeField] private Button _btnEye;

        [Header("Selection (keys)")]
        public bool enableKeybinds = true;
        public KeyCode keySkin = KeyCode.Alpha1;
        public KeyCode keyEye = KeyCode.Alpha2;

        public event Action<bool, IReadOnlyList<WH.Gameplay.CardFamily>, WH.Gameplay.CardFamily?> OnHarvestResolved;

        private IReadOnlyList<WH.Gameplay.CardFamily> _preferredFromMarks;
        private bool _awaitingSelection;
        private WH.Gameplay.CardFamily? _chosen;

        private void Awake()
        {
            if (_btnSkin) _btnSkin.onClick.AddListener(() => Choose(WH.Gameplay.CardFamily.Skin));
            if (_btnEye) _btnEye.onClick.AddListener(() => Choose(WH.Gameplay.CardFamily.Eye));
            HideSelection();
        }

        private void Update()
        {
            if (!_awaitingSelection || !enableKeybinds) return;
            if (Input.GetKeyDown(keySkin)) Choose(WH.Gameplay.CardFamily.Skin);
            if (Input.GetKeyDown(keyEye)) Choose(WH.Gameplay.CardFamily.Eye);
        }

        public void BeginHarvest(IReadOnlyList<WH.Gameplay.CardFamily> preferred)
        {
            if (_qte == null)
            {
                Debug.LogError("HarvestController missing QTEController");
                return;
            }

            _preferredFromMarks = preferred;
            _chosen = null;
            ShowSelection();
            _awaitingSelection = true;
            Debug.Log("[Harvest] Select a body part: [1] Skin, [2] Eye (or use UI buttons).");
        }

        private void Choose(WH.Gameplay.CardFamily fam)
        {
            if (!_awaitingSelection) return;
            _awaitingSelection = false;
            HideSelection();

            _chosen = fam;

            _qte.OnQteResolved -= HandleQteResolved;
            _qte.OnQteResolved += HandleQteResolved;

            // Keep current slider QTE; difficulty placeholder
            _qte.StartQte(WH.Gameplay.QTE.QteMode.Slider, 0.5f, _preferredFromMarks);
        }

        private void HandleQteResolved(bool success, IReadOnlyList<WH.Gameplay.CardFamily> preferred)
        {
            _qte.OnQteResolved -= HandleQteResolved;
            Debug.Log($"[Harvest] QTE success={success}");
            OnHarvestResolved?.Invoke(success, preferred, _chosen);
        }

        private void ShowSelection() { if (_selectionPanel) _selectionPanel.SetActive(true); }
        private void HideSelection() { if (_selectionPanel) _selectionPanel.SetActive(false); }
    }
}


