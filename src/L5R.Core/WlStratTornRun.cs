namespace L5R.Core;

// Strategy wave 118 (issue #674, cluster "strat-torn"): the effects of the nineteen Torn Asunder Strategies, and the three decisions that hand a choice to a player (see WlStratTorn.cs).

/// <summary>The Either/Or and decider specs of the cluster.</summary>
public static class WlStratTornSpecs
{
    /// <summary>Courteous Gift: "He may draw a card" -- the chosen (other) player decides.</summary>
    public const string CourteousDraw = "wl-courteous-draw";
    /// <summary>The Passing of Tradition: which keyword the performer copies -- the acting player decides.</summary>
    public const string PassingKeyword = "wl-passing-keyword";
    /// <summary>Scandalous Gossip: "He must choose and bow one of his unbowed Personalities, if possible."</summary>
    public const string GossipBow = "wl-gossip-bow";

    /// <summary>"Other than Courtier, Samurai, or Shugenja" (printed); Unique is also never copied (a reading: a copied Unique would break the Unique rule).</summary>
    public static readonly string[] NotCopied = ["Courtier", "Samurai", "Shugenja", "Unique"];
}

public sealed partial class MatchEngine
{
    // ---- execution: null when the id is not this cluster's; false when its effects are done; true when a decision or a window opened ----------------------------------------------------------

    private bool? RunWlStratTorn(RunHookContext context)
    {
        var (id, ability, command, player, source, target) = (context.id, context.ability, context.command, context.player, context.source, context.target);
        var name = source is null ? "An action" : Definition(source).Name;
        switch (id)
        {
            case WlStratTornAbilities.ThreatEnacted:
                {
                    // "Destroy a non-Unique card in his unit without attachments": the acting player chooses it as the action resolves (Makito's Maho Battle's shape), then "dishonor him (if he is still in
                    // play)": both are one option's effect, so the dishonoring follows the destruction. With no such card only the dishonoring applies.
                    var options = UnitCardsWithoutAttachments(target!).Where(c => !Keyword(c, "Unique")).Select(c => new EffectOption(c.Id, $"Destroy {Definition(c).Name}", [WlThreatEffect + c.Id])).ToArray();
                    if (options.Length == 0) { WlDishonorIfInPlay(target!, player.Id); return false; }
                    if (options.Length == 1) { ApplyWlStratTornBranchEffect(options[0].Effects[0], command); return false; }
                    return BeginListedEffectChoice(command, options, $"{name}: choose the non-Unique card without attachments in {Definition(target!).Name}'s unit to destroy. He is then dishonored if he is still in play.",
                        context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants);
                }
            case WlStratTornAbilities.AmbushAttack:
            case WlStratTornAbilities.AmbushAttackMove:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, context.consumedGrant);
            case WlStratTornAbilities.BambooStalwart:
                GiveKeywordPermanently(Card(command.PerformerId!), "Stalwart", source!.Id);
                return false;
            case WlStratTornAbilities.CallingDarkness:
                {
                    var darkened = Card(command.PerformerId!);
                    for (var n = 0; n < 3; n++) CreateToken(darkened, "Force", id, "Force", 1);
                    GiveKeywordPermanently(darkened, "Shadowlands", source!.Id);
                    ScheduleDelayedEffect(darkened, DelayedKinds.Straighten, DelayUntil.EndOfTurn, player.Id, name, source.Id);
                    GainHonor(player.Id, -2, name, source);
                    return false;
                }
            case WlStratTornAbilities.CourteousGift:
                return BeginWlEitherOr(WlStratTornSpecs.CourteousDraw, context, 1 - player.Id, SlotCards(command, "gift").Single());
            case WlStratTornAbilities.ForestCoverRecon:
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                return false;
            case WlStratTornAbilities.ForestCoverAttach:
                {
                    GrantReconnaissanceAtCurrentBattlefield(player.Id);
                    // The Strategy leaves the hand as it is played: the attach may open an entering-play window, which ends the action there.
                    SpendHandPlayedCard(player, source);
                    var pending = ExecuteAttach(command, ability.Attach!, player, source, target);
                    if (target!.Zone == Zone.Attached && target.AttachedTo == command.PerformerId) QueueExtraEndPhaseDraws(player, 1, name); // "If you did"
                    return pending;
                }
            case WlStratTornAbilities.ForgottenTeachings:
                RemoveCardAbility(target!, AbilityRemovalAbilities.AllKey, name);
                if (!_state.WlStratTorn.ForgottenTeachings.Contains(target!.Id)) _state.WlStratTorn.ForgottenTeachings.Add(target.Id);
                return false;
            case WlStratTornAbilities.ImproperPapers:
                NegateEffects(target!, "move");
                return false;
            case WlStratTornAbilities.ImprudentMisstep:
                GainHonor(command.TargetPlayerId!.Value, -5, name, source);
                return false;
            case WlStratTornAbilities.InTheRight:
                foreach (var attacker in SlotCards(command, "targets").ToArray())
                    if (InPlay(attacker) && !StalwartNegatesBow(attacker, player.Id)) attacker.Bowed = true;
                return false;
            case WlStratTornAbilities.MartyrsCall:
                foreach (var ally in SlotCards(command, "targets").ToArray())
                {
                    ApplyModifier(ally, source!.Id, id, "Force", 3);
                    GrantKeyword(ally, "Stalwart", source.Id, GrantDuration.Turn);
                }
                return false;
            case WlStratTornAbilities.OverpowerOpen:
                {
                    var attacker = Player(_state.ActivePlayerId);
                    attacker.MayNotAttackThisTurn = true;
                    Emit("attackLocked", $"{name}: {attacker.Name} may not declare an attack this turn.", player.Id, source?.Id);
                    return false;
                }
            case WlStratTornAbilities.OverpowerLimited:
            case WlStratTornAbilities.PassingLimited:
                RefillProvinceWithDiscardedPersonality(player, target!, command.Province!.Value, name);
                return false;
            case WlStratTornAbilities.PassingCopy:
                return BeginWlEitherOr(WlStratTornSpecs.PassingKeyword, context, player.Id, target!);
            case WlStratTornAbilities.ScandalousGossip:
                return BeginDeciderChoice(WlStratTornSpecs.GossipBow, command, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants);
            case WlStratTornAbilities.SpoilsEnter:
                EnterPlayAsKata(player, source!);
                return false;
            case WlStratTornAbilities.TacticalSacrifice:
                if (InPlay(target!)) DestroyTargetCard(target!, player.Id);
                return false;
            case WlStratTornAbilities.LastMove:
                {
                    var enemy = SlotCards(command, "target").Single();
                    if (InPlay(enemy)) Dishonor(enemy, player.Id);
                    if (SlotCards(command, "destroy").FirstOrDefault() is { } doomed && IsUnitCardWithoutAttachments(doomed)) DestroyUnitCard(doomed, player.Id);
                    return false;
                }
            case WlStratTornAbilities.TouchOfTheNight:
                {
                    // "Each Personality and Follower opposing him": the enemy units at the battlefield, and the Followers attached to those Personalities.
                    if (_state.CurrentBattleProvince is not int battle) return false;
                    foreach (var unit in Army(1 - player.Id, battle).ToArray())
                    {
                        ApplyModifier(unit, source!.Id, id, "Force", -2);
                        foreach (var follower in Attachments(unit.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray()) ApplyModifier(follower, source.Id, id, "Force", -2);
                    }
                    return false;
                }
            default:
                return null;
        }
    }

    // ---- Either/Or ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The Strategy leaves the hand as it is played and the decision opens exactly as an action's own tail would have run (Unfortunate Hesitation's order).
    private bool BeginWlEitherOr(string specId, RunHookContext context, int deciderId, CardInstance affected)
    {
        SpendHandPlayedCard(context.player, context.source);
        if (!context.consumedGrant) _state.AdditionalActions.RemoveAll(context.preExistingGrants.Contains);
        return OpenEitherOr(specId, context.source!, context.player.Id, deciderId, affected, "action", context.bowedBeforeAction,
            stayWithActor: _state.AdditionalActions.Any(g => g.PlayerId == context.player.Id && Forfeitable(g)), performerId: context.command.PerformerId);
    }

    private string? WlEitherOrPrompt(PendingEitherOr p, string source, string affected) => p.SpecId switch
    {
        WlStratTornSpecs.CourteousDraw => $"{source}: you may draw a card. {affected} costs 2 less Gold this turn, or 4 less if you draw.",
        WlStratTornSpecs.PassingKeyword => $"{source}: choose the keyword {WlPassingPerformerName(p)} permanently copies from {affected}.",
        _ => null
    };

    private string WlPassingPerformerName(PendingEitherOr p) => p.PerformerId is { } id && _state.Cards.TryGetValue(id, out var performer) ? Definition(performer).Name : "the performer";

    // The keywords the performer may copy from the target: any he has now that he lacks, except the printed three (and Unique).
    private string[] WlCopyableKeywords(CardInstance performer, CardInstance target) =>
        CurrentKeywordList(target).Where(k => !WlStratTornSpecs.NotCopied.Contains(k, StringComparer.Ordinal) && !Keyword(performer, k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();

    private EitherOrOption[]? WlEitherOrOptions(PendingEitherOr p, CardInstance? affected)
    {
        switch (p.SpecId)
        {
            case WlStratTornSpecs.CourteousDraw:
                return [new("draw", "Draw a card", $"You draw a card; {(affected is null ? "the card" : Definition(affected).Name)} costs 4 less Gold this turn.", EitherOrKind.Choose, 0, true, null),
                    new("decline", "Do not draw", $"{(affected is null ? "The card" : Definition(affected).Name)} costs 2 less Gold this turn.", EitherOrKind.Choose, 0, true, null)];
            case WlStratTornSpecs.PassingKeyword:
                {
                    var performer = p.PerformerId is { } id && _state.Cards.TryGetValue(id, out var found) ? found : null;
                    var keywords = performer is null || affected is null ? [] : WlCopyableKeywords(performer, affected);
                    var options = keywords.Select(k => new EitherOrOption($"kw:{k}", $"Copy {k}", $"{WlPassingPerformerName(p)} permanently has {k}.", EitherOrKind.Choose, 0, true, null)).ToList();
                    options.Add(new("none", "No keyword to copy", "Nothing is copied.", EitherOrKind.Choose, 0, keywords.Length == 0, "A keyword is copied when there is one to copy."));
                    return options.ToArray();
                }
            default:
                return null;
        }
    }

    private bool WlApplyEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider, CardInstance? affected)
    {
        switch (p.SpecId)
        {
            case WlStratTornSpecs.CourteousDraw:
                {
                    var drew = option.Key == "draw";
                    if (drew) Draw(decider);
                    var name = Definition(source).Name;
                    Emit("eitherOrChosen", drew ? $"{decider.Name} draws a card ({name})." : $"{decider.Name} does not draw ({name}).", decider.Id, source.Id);
                    if (affected is not null) ApplyModifier(affected, source.Id, p.SpecId, "GoldCost", drew ? -4 : -2); // "(this turn)": a modifier lasts until the turn ends
                    return true;
                }
            case WlStratTornSpecs.PassingKeyword:
                if (option.Key.StartsWith("kw:", StringComparison.Ordinal) && p.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer) && InPlay(performer))
                    GiveKeywordPermanently(performer, option.Key[3..], source.Id);
                return true;
            default:
                return false;
        }
    }

    // ---- A Threat Enacted's listed choice ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string WlThreatEffect = "wl-threat-destroy:";

    private void WlDishonorIfInPlay(CardInstance personality, int actingPlayerId)
    {
        if (InPlay(personality) && Definition(personality).Type == CardType.Personality) Dishonor(personality, actingPlayerId);
    }

    // ApplyBranchEffect's arm: destroy the chosen card, then dishonor the targeted Personality if he is still in play. False for any other effect.
    private bool ApplyWlStratTornBranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith(WlThreatEffect, StringComparison.Ordinal)) return false;
        var card = Card(effect[WlThreatEffect.Length..]);
        if (InPlay(card)) DestroyUnitCard(card, command.PlayerId);
        if (command.TargetId is { } targetId && _state.Cards.TryGetValue(targetId, out var victim)) WlDishonorIfInPlay(victim, command.PlayerId);
        return true;
    }

    // ---- Eye of the Sword -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ApplyDuelOutcome's case: "Give the winner two +1F tokens. Destroy the duel's loser." (a tie has no winner; both are its losers).
    private void WlEyeDuelOutcome(string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        if (winnerId is not null && InPlay(Card(winnerId)))
            for (var n = 0; n < 2; n++) CreateToken(Card(winnerId), "Force", WlStratTornAbilities.EyeDuel, "Force", 1);
        DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId);
    }

    internal void WlEyeDuelOutcomeForTest(string? winnerId, List<string> loserIds, int challengerPlayerId) => WlEyeDuelOutcome(winnerId, loserIds, challengerPlayerId);

    // ---- Scandalous Gossip's decider ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private string WlDeciderPrompt(PendingDeciderChoice p) =>
        $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose one of your unbowed Personalities. It is bowed.";

    private (List<string> Candidates, int Min, int Max) WlDeciderOptions(int deciderId) =>
        (_state.Cards.Values.Where(c => c.OwnerId == deciderId && InPlay(c) && !c.Bowed && Definition(c).Type == CardType.Personality).Select(c => c.Id).Order(StringComparer.Ordinal).ToList(), 1, 1);

    private bool WlApplyDecider(PendingDeciderChoice p)
    {
        if (p.SpecId != WlStratTornSpecs.GossipBow) return false;
        var source = Card(p.SourceCardId);
        foreach (var id in p.Chosen)
        {
            var chosen = Card(id);
            if (InPlay(chosen) && !StalwartNegatesBow(chosen, p.ActingPlayerId)) chosen.Bowed = true;
        }
        GainHonor(p.ActingPlayerId, 1, Definition(source).Name, source); // "Gain 1 Honor": whether or not anything could be bowed
        return true;
    }
}
