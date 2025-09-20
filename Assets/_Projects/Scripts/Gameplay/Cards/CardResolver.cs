using System;
using UnityEngine;
using WH.Gameplay;              // SharedTypes: CardEffectDef, CardEffectType, EffectTarget, StatusKind, BodyTag
using WH.Gameplay.Systems;      // TurnManager, PulseManager

namespace WH.Gameplay.Cards
{
    /// <summary>
    /// Resolves a card play end-to-end.
    ///
    /// Responsibilities:
    ///  • Gate Unplayable cards.
    ///  • Spend Pulse (cost).
    ///  • Apply base numbers from CardData (Damage/Block/Draw/Scry).
    ///  • Walk CardEffectDef list and apply each in order:
    ///      Damage, GainBlock, Draw, Scry, GainPulse, ApplyStatus, RemoveStatus,
    ///      ExhaustSelf, RetainSelf, Custom.
    ///  • Raise events for systems we don’t own (draw/scry UI, exhaust-a-curse, reveal intent, mark enemy).
    ///
    /// Not responsible for:
    ///  • Moving the card between piles (driver decides discard/exhaust/retain using events from this resolver).
    ///  • Showing any UI — we only raise events/signals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardResolver : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private TurnManager _turns;       // provides player/enemy state + damage helpers
        [SerializeField] private PulseManager _pulse;       // action currency

        // ----------------------------- Signals -----------------------------
        // Subscribe to these from your driver/hand/controller.
        public event Action<int> OnRequestDraw;                    // draw N now
        public event Func<int, ScryOutcome> OnRequestScry;         // scry N via UI; return what was discarded
        public event Func<bool> OnRequestExhaustCurseInHand;       // exhaust any 1 Curse in the current hand; true if done
        public event Action OnRevealEnemyIntent;                   // show enemy intent
        public event Action<BodyTag> OnMarkEnemy;                  // mark an enemy body part (Eye MVP)
        public event Action OnForceExhaustThisCard;                // this play should route to Exhaust pile
        public event Action OnRetainThisCard;                      // this play should be retained this turn
        public event Action<string> OnInfo;                        // text logger for in-game debug overlay

        // Clot Shield state: next time we apply Bleed to the ENEMY, add +X stacks then consume.
        private int _bleedBonusNextStacks;

        private void Awake()
        {
            if (_turns == null)
                _turns = FindAnyObjectByType<TurnManager>(FindObjectsInactive.Include);
            if (_pulse == null)
                _pulse = FindAnyObjectByType<PulseManager>(FindObjectsInactive.Include);
        }

        // =====================================================================
        //                               API
        // =====================================================================

        /// <summary>
        /// Attempts to play the given card. Returns true if the play resolved (cost paid).
        /// The caller (driver) should still move the card to Discard/Exhaust based on events/flags.
        /// </summary>
        public bool TryPlay(CardData card)
        {
            if (card == null || _turns == null) return false;

            // 0) UNPLAYABLE guard (e.g., Wound/Parasite). This prevents cost payment.
            if (HasCustomOp(card, CardCustomOp.Unplayable))
            {
                Info($"'{card.DisplayName}' is Unplayable.");
                return false;
            }

            // 1) Pay Pulse (cost).
            var pm = _turns.Pulse != null ? _turns.Pulse : _pulse;
            if (card.CostPulse > 0)
            {
                if (pm == null) { Debug.LogWarning("[Resolver] No PulseManager set."); return false; }
                if (!pm.TrySpend(card.CostPulse)) return false; // insufficient Pulse
            }

            // 2) Apply base numbers (these exist directly on CardData for readability)
            //    Tip: Designers can leave the effect list empty and just use these.
            if (card.BaseDamage > 0) DealDamageToEnemy(card.BaseDamage);   // "Deal X"
            if (card.BaseBlock > 0) GainPlayerBlock(card.BaseBlock);      // "Gain X Block"
            if (card.DrawAmount > 0) OnRequestDraw?.Invoke(card.DrawAmount); // "Draw X"

            ScryOutcome scryOutcome = default;
            if (card.ScryAmount > 0)
            {
                // "Scry X" — we ask the driver/UI to do the interaction and tell us what happened.
                scryOutcome = RequestScry(card.ScryAmount);
            }

            // 3) Apply the data-driven effects list in order (fine-grained control).
            bool forceExhaust = false;
            bool retainCard = false;
            ResolveEffects(card, ref forceExhaust, ref retainCard, scryOutcome);

            // 4) Post-effects routing directives for the driver.
            if (retainCard) OnRetainThisCard?.Invoke();
            if (forceExhaust || card.ExhaustAfterPlay) OnForceExhaustThisCard?.Invoke();

            return true;
        }

        // =====================================================================
        //                         EFFECT WALKER
        // =====================================================================

        private void ResolveEffects(CardData card, ref bool forceExhaust, ref bool retainCard, ScryOutcome scryOutcome)
        {
            var list = card.Effects;
            if (list == null || list.Count == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                var eff = list[i];

                switch (eff.type)
                {
                    case CardEffectType.Damage:
                        // Deal <value> damage. If value2 > 1, treat as multi-hit (value repeated value2 times).
                        {
                            int hits = Mathf.Max(1, eff.value2);
                            for (int h = 0; h < hits; h++)
                                DealDamageToEnemy(eff.value);
                        }
                        break;

                    case CardEffectType.GainBlock:
                        // Gain <value> Block immediately.
                        GainPlayerBlock(eff.value);
                        break;

                    case CardEffectType.Draw:
                        // Draw <value> cards immediately.
                        OnRequestDraw?.Invoke(Mathf.Max(0, eff.value));
                        break;

                    case CardEffectType.Scry:
                        // Ask UI/driver to Scry <value> cards. We keep the outcome if later effects care.
                        scryOutcome = RequestScry(Mathf.Max(0, eff.value));
                        break;

                    case CardEffectType.GainPulse:
                        // Immediately gain <value> Pulse.
                        {
                            var pm = _turns.Pulse != null ? _turns.Pulse : _pulse;
                            pm?.Gain(Mathf.Max(0, eff.value));
                        }
                        break;

                    case CardEffectType.ApplyStatus:
                        // Apply a status to either Player or Enemy.
                        // e.g., Apply Bleed xN to Enemy; Apply Weak xN to Player.
                        ApplyStatus(eff.target, eff.status, eff.value);
                        break;

                    case CardEffectType.RemoveStatus:
                        // Remove up to <value> stacks of a status from the target.
                        RemoveStatus(eff.target, eff.status, eff.value);
                        break;

                    case CardEffectType.ExhaustSelf:
                        // Force this card to go to Exhaust pile after resolution.
                        forceExhaust = true;
                        break;

                    case CardEffectType.RetainSelf:
                        // Retain this card in hand at end of turn (driver honors OnRetainThisCard).
                        retainCard = true;
                        break;

                    case CardEffectType.Custom:
                        // Project-specific opcodes that don't need a new enum entry.
                        ResolveCustomOp(card, eff.value, eff.value2, scryOutcome, ref forceExhaust, ref retainCard);
                        break;

                    default:
                        Info($"Unhandled effect type {eff.type} on '{card.DisplayName}'.");
                        break;
                }
            }
        }

        // =====================================================================
        //                           CUSTOM OPS
        // =====================================================================

        private void ResolveCustomOp(CardData card, int op, int arg, ScryOutcome scryOutcome, ref bool forceExhaust, ref bool retainCard)
        {
            switch (op)
            {
                case CardCustomOp.RevealIntent:
                    // Glare: reveal what the enemy intends to do this turn.
                    OnRevealEnemyIntent?.Invoke();
                    Info("Reveal enemy intent.");
                    break;

                case CardCustomOp.MarkEnemyEye:
                    // Glare: mark Eye on enemy (Phase 4: influences harvest rewards).
                    OnMarkEnemy?.Invoke(BodyTag.Eye);
                    Info("Mark Enemy: Eye.");
                    break;

                case CardCustomOp.ExhaustOneCurseFromHand:
                    // Stitch: allow player to exhaust one Curse (Rarity=Curse) in hand.
                    {
                        bool exhausted = OnRequestExhaustCurseInHand?.Invoke() ?? false;
                        Info(exhausted ? "Stitch: exhausted a Curse from hand." : "Stitch: no Curse in hand to exhaust.");
                    }
                    break;

                case CardCustomOp.EndTurnLoseHp:
                    // Parasite: if this card remains in hand at END of player turn, lose <arg> HP.
                    // Implementation detail: the driver should scan hand on OnPlayerTurnEnded and apply the loss per Parasite.
                    Info($"End-of-turn trigger armed: lose {Mathf.Max(1, arg)} HP if this remains in hand.");
                    break;

                case CardCustomOp.BleedBonusNext:
                    // Clot Shield: next time YOU apply Bleed this combat, add +arg stacks then consume.
                    _bleedBonusNextStacks += Mathf.Max(1, arg);
                    Info($"Clot Shield primed: next Bleed +{_bleedBonusNextStacks} stack(s).");
                    break;

                case CardCustomOp.Unplayable:
                    // Already gated before cost payment; noop here for completeness.
                    break;

                default:
                    Info($"Unknown CustomOp {op} on '{card.DisplayName}'.");
                    break;
            }
        }

        // =====================================================================
        //                           EFFECT HELPERS
        // =====================================================================

        private void DealDamageToEnemy(int rawDamage)
        {
            if (rawDamage <= 0) return;
            _turns.DealDamageToEnemy(rawDamage); // TurnManager should handle enemy Block/HP clamping
        }

        private void GainPlayerBlock(int amount)
        {
            if (amount <= 0) return;
            if (_turns.PlayerState == null) return;
            _turns.PlayerState.Block += amount;
        }

        private ScryOutcome RequestScry(int amount)
        {
            if (amount <= 0) return default;
            // If no UI is subscribed yet, return an empty outcome; callers should handle gracefully.
            return OnRequestScry != null ? OnRequestScry.Invoke(amount) : default;
        }

        private void ApplyStatus(EffectTarget target, StatusKind status, int stacks)
        {
            if (stacks <= 0) return;

            // Clot Shield hook: when applying Bleed to ENEMY, add the stored bonus then consume it.
            if (status == StatusKind.Bleed && target == EffectTarget.Enemy && _bleedBonusNextStacks > 0)
            {
                stacks += _bleedBonusNextStacks;
                _bleedBonusNextStacks = 0;
                Info($"Bleed bonus consumed → total stacks {stacks}.");
            }

            // MVP: log only. Later, integrate with your real status containers on Player/Enemy.
            var who = (target == EffectTarget.Enemy) ? "Enemy" : "Player";
            Info($"Apply {status} x{stacks} to {who} (stub).");
        }

        private void RemoveStatus(EffectTarget target, StatusKind status, int stacks)
        {
            // MVP: log only.
            var who = (target == EffectTarget.Enemy) ? "Enemy" : "Player";
            Info($"Remove {status} x{Mathf.Max(1, stacks)} from {who} (stub).");
        }

        // =====================================================================
        //                              UTILS
        // =====================================================================

        private bool HasCustomOp(CardData card, int opCode)
        {
            var list = card.Effects;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].type == CardEffectType.Custom && list[i].value == opCode)
                    return true;
            return false;
        }

        private void Info(string msg)
        {
            if (!string.IsNullOrEmpty(msg)) OnInfo?.Invoke(msg);
#if UNITY_EDITOR
            Debug.Log($"[Resolver] {msg}");
#endif
        }
    }

    /// <summary>
    /// Result from a Scry interaction.
    /// The driver should fill this after the player chooses cards to discard/bottom.
    /// </summary>
    public struct ScryOutcome
    {
        public int discardedTotal;     // how many cards were discarded/bottomed
        public bool discardedAnyCurse;  // true if at least one Curse was discarded
    }
}






