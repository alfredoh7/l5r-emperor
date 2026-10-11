namespace L5R.Core;

internal static partial class EngineHooks
{
    private static readonly Lazy<bool> Ready = new(Build);

    private static bool Build()
    {
        _ = EitherOrPrompt.Items;
        _ = EitherOrOptions.Items;
        _ = ApplyEitherOr.Items;
        _ = FinishEitherOr.Items;
        _ = DeciderPrompt.Items;
        _ = DeciderOptions.Items;
        _ = ApplyDecider.Items;
        _ = DeciderVerb.Items;
        _ = DeciderIdBefore.Items;
        _ = DeciderIdAfter.Items;
        _ = AttackStrengthBefore.Items;
        _ = AttackStrengthAfter.Items;
        _ = AttackKind.Items;
        _ = PerformerAllows.Items;
        _ = TargetAllows.Items;
        _ = PerformerCandidates.Items;
        _ = DestroysTarget.Items;
        _ = BowsTarget.Items;
        _ = PersonalityForce.Items;
        _ = ProvinceBonus.Items;
        _ = TargetedReaction.Items;
        _ = ExtraUsesPerTurn.Items;
        _ = RulebookModifier.Items; // P19 (issue #717): rulebook-action modifiers
        _ = ValidateEarly.Items;
        _ = ValidateMiddle.Items;
        _ = ValidateLate.Items;
        _ = RunEffects.Items;
        _ = DecisionState.Items;
        _ = NonDecisionState.Items;
        _ = Abilities.Items;
        _ = Looks.Items;
        _ = Searches.Items;
        _ = PendingChoice.Items;
        _ = FollowerForce.Items;
        _ = PersonalityChi.Items;
        _ = ArmyForce.Items;
        _ = StatTerms.Items;
        _ = CardRestrictions.Items;
        _ = CardTargetBars.Items;
        _ = ForceCaps.Items; // issue #768: the Force cap channel
        _ = AfterActionTraits.Items;
        _ = EntryCostTerms.Items;
        _ = StandingImmunities.Items;
        _ = AbilityCostTerms.Items;
        _ = RefillFaceUp.Items;
        _ = AfterProvincesRevealed.Items; // P5 (issue #711)
        _ = HonorChange.Items;
        _ = FactTraits.Items;
        _ = FactConditions.Items;
        _ = CelestialEntryChoices.Items;
        _ = AnnounceRegistrants.Items;
        _ = ResolveEvent.Items; // P6 (issue #705): the only channel the startup seal had forgotten (a survey finding of #668)
        _ = StandingWatches.Items;
        _ = DuelRulesFor.Items;
        _ = DuelRefusableWhen.Items;
        _ = TurnBegins.Items;
        _ = PhaseBegins.Items; // issue #816: "after each phase begins" traits
        _ = TurnBeginChoices.Items;
        _ = PlayerGrantAbilities.Items;
        _ = CostWaivers.Items;
        _ = PaymentTokens.Items;
        _ = ProductionCondition.Items;
        _ = ConditionalProductions.Items;
        _ = ProductionAdjustment.Items;
        _ = AfterPayment.Items;
        _ = RecruitGate.Items;
        _ = KeywordRule.Items;
        _ = ActionEffectWatchFired.Items;
        _ = ActionMoveGates.Items;
        _ = UntargetedReaction.Items; // issue #737 (P8): an Interrupt to an action that names no card
        _ = PendingActions.Items; // P25 (issue #723): decision infrastructure
        _ = BranchEffects.Items;
        _ = EffectChoiceContinuations.Items;
        _ = ShownChoiceApply.Items;
        return true;
    }

    // All callbacks have registered before the first engine is constructed.
    internal static void Initialize() => _ = Ready.Value;
}
