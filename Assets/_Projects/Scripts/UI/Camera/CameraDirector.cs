using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using WH.Gameplay.Systems;
using WH.Gameplay.QTE;

namespace WH.UI
{
    /// <summary>Controls CM3 priorities and simple zoom/shake based on game flow.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraDirector : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private FlowController _flow;
        [SerializeField] private QTEController _qte;
        [SerializeField] private CinemachineCamera _cmCombat;
        [SerializeField] private CinemachineCamera _cmHarvest;

        [Header("Priorities")]
        [SerializeField] private int _combatPri = 10;
        [SerializeField] private int _harvestPri = 5;
        [SerializeField] private int _harvestActivePri = 20;

        private Coroutine _shakeCo;

        private void Awake()
        {
            if (_flow == null) _flow = FindAnyObjectByType<FlowController>(FindObjectsInactive.Include);
            if (_qte == null) _qte = FindAnyObjectByType<QTEController>(FindObjectsInactive.Include);

            if (_cmCombat == null || _cmHarvest == null)
            {
                var cams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in cams)
                {
                    if (c.name.Contains("Combat")) _cmCombat = c;
                    else if (c.name.Contains("Harvest")) _cmHarvest = c;
                }
            }
        }

        private void OnEnable()
        {
            if (_flow != null) _flow.OnPhaseChanged += HandlePhase;
            if (_qte != null) _qte.OnQteResolved += HandleQteResolved;
        }

        private void OnDisable()
        {
            if (_flow != null) _flow.OnPhaseChanged -= HandlePhase;
            if (_qte != null) _qte.OnQteResolved -= HandleQteResolved;
        }

        private void Start()
        {
            SetCombatView();
        }

        private void HandlePhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Fight:
                    SetCombatView();
                    break;
                case GamePhase.Harvest:
                case GamePhase.Stitch:
                case GamePhase.Event:
                    SetHarvestView();
                    break;
                default:
                    SetCombatView();
                    break;
            }
        }

        private void HandleQteResolved(bool success, System.Collections.Generic.IReadOnlyList<WH.Gameplay.CardFamily> _)
        {
            // Keep Harvest view until flow returns to Fight.
        }

        public void SetCombatView()
        {
            if (_cmCombat != null) _cmCombat.Priority = _combatPri;
            if (_cmHarvest != null) _cmHarvest.Priority = _harvestPri;
        }

        public void SetHarvestView()
        {
            if (_cmHarvest != null) _cmHarvest.Priority = _harvestActivePri;
            if (_cmCombat != null) _cmCombat.Priority = _combatPri;
        }

        // Minimal shake using the active-priority vcam. Requires CinemachineBasicMultiChannelPerlin on the vcam.
        public void NudgeShake(float amplitude = 0.6f, float duration = 0.15f)
        {
            var active = GetActiveByPriority();
            if (active == null) return;

            var noise = active.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise == null) noise = active.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();

            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeRoutine(noise, amplitude, duration));
        }

        private CinemachineCamera GetActiveByPriority()
        {
            if (_cmCombat == null && _cmHarvest == null) return null;
            if (_cmCombat == null) return _cmHarvest;
            if (_cmHarvest == null) return _cmCombat;
            return _cmCombat.Priority >= _cmHarvest.Priority ? _cmCombat : _cmHarvest;
        }

        private static IEnumerator ShakeRoutine(CinemachineBasicMultiChannelPerlin noise, float amp, float dur)
        {
            var t = 0f;
            var baseAmp = noise.AmplitudeGain;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                var k = 1f - Mathf.Clamp01(t / dur);
                noise.AmplitudeGain = baseAmp + amp * k;
                yield return null;
            }
            noise.AmplitudeGain = baseAmp;
        }
    }
}


