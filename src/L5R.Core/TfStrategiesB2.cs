namespace L5R.Core;

// ======================================================================================================================================================================================
// TWENTY FESTIVALS STRATEGIES, BATCH B, part two (strategy-wave-110) -- issue #622: PRESERVING FORCES (Embers of War) and UNSAFE PASSAGE (Embers of War), each read from its scan.
//
// PRESERVING FORCES: "As a Focus Effect: Before your Personality in this duel is destroyed for losing it, you may destroy one of his Followers; if you did, negate his destruction.
// Open: Target one or two Followers in your discard pile: Put them into your hand."
//   * THE FOCUS EFFECT is a decision asked BEFORE the duel's outcome applies (ResolveDuel, beside the R2 duel-loss delays), of the focus card's player, and only when it can matter: his Personality
//     in the duel lost, the duel's own outcome destroys its loser (TfStrategiesBDuelDestroysLoser: a printed outcome, pinned by a test over every duel-issuing ability so a new duel is classified on
//     purpose), and the other duelist has a Follower to destroy. "His" is the OTHER duelist's: the Personality who beat the focuser (in a tie, the other loser). One Either/Or option per Follower, or
//     declining. A Follower really destroyed (a negation or delay of that destruction leaves it) negates the destruction of the focuser's Personality for losing -- To First Blood's own negation
//     ("destroy", on the duel's NegatedEffects), for that Personality only. A Focus Effect is asked once per duel (a marker on the duel's negations).
//   * THE OPEN ACTION puts one or two Followers from the player's own discard pile into his hand (Shinjo Min-Hee's category and move).
//
// UNSAFE PASSAGE: "Reaction: After an action targets your Commander: Negate his movement from its effects. Battle: Target your Follower: You may take an additional action to use one of its base
// abilities, even if it is bowed or has already used the ability its maximum number of times this turn."
//   * THE REACTION is a card in hand answering a targeted-by-action window (TargetedReaction hook): an action of either player's that targets one of the player's Commanders and would move him (an
//     action that names a movement, or one the engine lists as one), unless its movement cannot be negated.
//   * THE BATTLE ACTION grants an additional action scoped to the target Follower (AdditionalActionGrant with a Tag): spent only by one of that Follower's PRINTED abilities taken as a Battle action,
//     and, while the grant is held, those abilities may be used even though the Follower is bowed or the ability's use for the turn is spent (the two Requires of ValidateAbility). A cost the
//     ability itself prints (bowing the Follower) is still a cost. The grant is optional, as "you may" says (it may be declined like any additional action).
// ======================================================================================================================================================================================

public static class TfStrategiesBMoreAbilities
{
    public const string PreservingFocus = "preserving-forces-focus", PreservingOpen = "preserving-forces-open";
    public const string UnsafeNegate = "unsafe-passage-negate", UnsafeGrant = "unsafe-passage-grant";
    public const string PreservingSpec = "preserving-forces-destroy";
    public const string FollowersSlot = "followers";
    /// <summary>The marker a Focus Effect already asked leaves on the duel's negations (kind, with the focus card's id).</summary>
    public const string AskedKind = "tfsb-asked", UnsafeTag = "tfsb-unsafe-passage";

    public static Dictionary<string, AbilityDefinition> AddTfStrategiesBMore(this Dictionary<string, AbilityDefinition> all)
    {
        all[PreservingFocus] = new("FocusEffect", "As a Focus Effect: Before your Personality in this duel is destroyed for losing it, you may destroy one of his Followers; if you did, negate his destruction", "none");
        all[PreservingOpen] = new("Open", "Target one or two Followers in your discard pile: Put them into your hand", "slots",
            Slots: [new(FollowersSlot, "se2-own-follower-in-discard", "One or two Followers in your discard pile", 1, 2)]);
        all[UnsafeNegate] = new("Reaction", "After an action targets your Commander: Negate his movement from its effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        all[UnsafeGrant] = new("Battle", "Target your Follower: You may take an additional action to use one of its base abilities, even if it is bowed or has already used the ability its maximum number of times this turn", "own-follower");
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- Preserving Forces: the decision before the outcome ---------------------------------------------------------------------------------------------------------------------------

    // Whether the duel's own printed outcome destroys this loser. Every duel-issuing ability is classified in TfStrategiesBTests (a new duel must be added here or there on purpose). Ochiai's Bravery
    // queues its destruction until after the duel's action, which a negation of the outcome does not reach, so it is not listed.
    // Issue #740: Doji Hakuseki - exp3's duel against The Dark Naga destroys its loser too (Wave740DuelDestroysLoser).
    private bool TfStrategiesBDuelDestroysLoser(PendingDuel duel, CardInstance loser) =>
        duel.IchigikuDestroysLoser // Kakita Ichigiku: "destroy that duel's loser in addition to other consequences of losing"
        || Wave740DuelDestroysLoser(duel, loser)
        || TfStrategiesBDuelAbilityDestroysLoser(duel.AbilityId, loser.Dishonorable);

    internal static bool TfStrategiesBDuelAbilityDestroysLoser(string abilityId, bool loserDishonorable) => abilityId switch
    {
        "steel-on-steel-duel" or "sword-for-hire-create" or "ichizo-exp-duel" or "shikei-duel" or "makoto-duel" or "insult-answered-duel" or "kudome-exp-duel" or "sanctioned-duel" or "fusako-duel" or WlStratTornAbilities.EyeDuel => true, // Sanctioned Duel (issue #622, IvorySpells.cs): "Destroy the duel's loser"
        Wave734Abilities.HoturiDuel or Wave734Abilities.TsukeDuel => true, // issue #734 (Wave734.cs): "Destroy the duel's loser"
        Wave835Abilities.FoeDuel => true, // issue #835 (Wave835.cs): The Empire's Foe Exposed, "Destroy the duel's loser"
        Wave827Abilities.KokujinDuel => true, // issue #827 (Wave827.cs): "Destroy the duel's loser" (or, when Kokujin wins, a Personality of his controller's to take control of it instead)
        WlPersTornAbilities.IzaDuel or WlPersTornAbilities.KyoshiroDuel or WlPersTornAbilities.RikuDuel => true, // issue #674 (WlPersTorn.cs): "Destroy the duel's loser"
        Wave740Abilities.MagistrateDuel => true, // issue #740: A Magistrate Falls, "Destroy the duel's loser"
        "hakuseki-duel" => loserDishonorable, // "destroy him if he is dishonorable"
        _ => false
    };

    private (CardInstance Mine, CardInstance His)? PreservingDuelists(PendingDuel duel, CardInstance focus)
    {
        var mineId = duel.ChallengerPlayerId == focus.OwnerId ? duel.ChallengerId : duel.ChallengedPlayerId == focus.OwnerId ? duel.ChallengedId : null;
        if (mineId is null) return null;
        return (Card(mineId), Card(mineId == duel.ChallengerId ? duel.ChallengedId : duel.ChallengerId));
    }

    private CardInstance[] PreservingFollowers(CardInstance his) =>
        Attachments(his.Id).Where(c => Definition(c).Type == CardType.Follower && c.Zone == Zone.Attached).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();

    // ResolveDuel's hook, after the R2 delays and before the outcome applies: asks each Preserving Forces Focus Effect (in the duel's Focus Effect order) that has something to ask. True when it paused.
    private bool BeginTfStrategiesBDuelDecisions(PendingDuel duel, List<string> loserIds)
    {
        foreach (var focusId in duel.OrderedFocusEffects.ToArray())
        {
            if (FocusEffectAbilityOf(focusId) != TfStrategiesBMoreAbilities.PreservingFocus) continue;
            if (duel.NegatedEffects.Any(n => n.Kind == TfStrategiesBMoreAbilities.AskedKind && n.CardId == focusId)) continue;
            var focus = Card(focusId);
            if (PreservingDuelists(duel, focus) is not { } sides) continue;
            if (!loserIds.Contains(sides.Mine.Id) || !InPlay(sides.Mine) || !TfStrategiesBDuelDestroysLoser(duel, sides.Mine) || PreservingFollowers(sides.His).Length == 0) continue;
            duel.NegatedEffects.Add(new(TfStrategiesBMoreAbilities.AskedKind, focusId));
            if (OpenEitherOr(TfStrategiesBMoreAbilities.PreservingSpec, focus, focus.OwnerId, focus.OwnerId, sides.His, "duel")) return true;
        }
        return false;
    }

    private string? PreservingPrompt(PendingEitherOr p, string source) =>
        p.SpecId == TfStrategiesBMoreAbilities.PreservingSpec
            ? $"{source}: before your Personality is destroyed for losing this duel, you may destroy one of {Definition(Card(p.AffectedCardId)).Name}'s Followers; if you do, his destruction is negated."
            : null;

    private EitherOrOption[]? PreservingOptions(PendingEitherOr p)
    {
        if (p.SpecId != TfStrategiesBMoreAbilities.PreservingSpec) return null;
        var his = Card(p.AffectedCardId);
        var options = PreservingFollowers(his).Select(f => new EitherOrOption("destroy:" + f.Id, $"Destroy {Definition(f).Name}",
            $"{Definition(f).Name} is destroyed and your Personality's destruction for losing the duel is negated.", EitherOrKind.Avoid, 0, true, null)).ToList();
        options.Add(new("decline", "Do not use it", "Nothing happens: your Personality is destroyed as the duel says.", EitherOrKind.Accept, 0, true, null));
        return options.ToArray();
    }

    private bool ApplyPreserving(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != TfStrategiesBMoreAbilities.PreservingSpec) return false;
        if (!option.Key.StartsWith("destroy:", StringComparison.Ordinal))
        {
            Emit("eitherOrChosen", $"{decider.Name} did not use {Definition(source).Name}.", decider.Id, source.Id);
            return true;
        }
        var follower = Card(option.Key["destroy:".Length..]);
        Emit("eitherOrChosen", $"{decider.Name} destroys {Definition(follower).Name} ({Definition(source).Name}).", decider.Id, follower.Id);
        DestroyAttachment(follower);
        // "If you did": a Follower still attached was not destroyed (negated or delayed), so nothing is negated.
        if (follower.Zone != Zone.Attached && _state.PendingDuel is { } duel && PreservingDuelists(duel, source) is { } sides)
        {
            duel.NegatedEffects.Add(new("destroy", sides.Mine.Id));
            Emit("effectsNegated", $"{Definition(sides.Mine).Name}: his destruction for losing the duel is negated ({Definition(source).Name}).", decider.Id, sides.Mine.Id);
        }
        return true;
    }

    // The decision is answered: the duel resolves again, now past this Focus Effect's question (and asks the next one, if any).
    private bool FinishPreserving(PendingEitherOr p)
    {
        if (p.SpecId != TfStrategiesBMoreAbilities.PreservingSpec) return false;
        _state.PendingChoice = null;
        ResolveDuel();
        return true;
    }

    // ---- Unsafe Passage -------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The Reaction's trigger: an action targets one of the player's Commanders and would move him -- by a movement it names, or one the engine lists -- and that movement can be negated.
    private bool UnsafePassageApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        if (abilityId != TfStrategiesBMoreAbilities.UnsafeNegate) return false;
        if (affected.OwnerId != card.OwnerId || Definition(affected).Type != CardType.Personality || !Keyword(affected, "Commander")) return false;
        if (subject.Command.AbilityId is not { } actingId || !AbilityCatalog.All.TryGetValue(actingId, out var acting)) return false;
        return (acting.Move is not null || MoveOrStraightenKind(actingId) == "move") && !MovementCannotBeNegated(subject.Command);
    }

    // ValidateAbility's two waivers: while the player holds the grant Unsafe Passage gave for this Follower, one of its printed abilities taken as a Battle action in a battle may be used even though the
    // Follower is bowed, or the ability's use for the turn is spent.
    private bool UnsafePassageWaives(GameCommand command, CardInstance? source) =>
        source is not null && command.AbilityId is { } id && _state.Phase == Phase.Battle && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing is "Battle" or "BattleOrOpen"
        && (Definition(source).Abilities ?? []).Contains(id)
        && _state.AdditionalActions.Any(g => g.PlayerId == command.PlayerId && g.Tag == TfStrategiesBMoreAbilities.UnsafeTag && GrantUsableNow(g) && g.ScopeCardIds is { } ids && ids.Contains(source.Id));

    // PerformerGrantMatches' hook: Unsafe Passage's grant is spent only by one of its Follower's printed abilities.
    private bool UnsafePassageGrantMatches(AdditionalActionGrant grant, GameCommand? command) =>
        grant.Tag != TfStrategiesBMoreAbilities.UnsafeTag
        || command?.AbilityId is { } id && command.CardId is { } cardId && _state.Cards.TryGetValue(cardId, out var card) && (Definition(card).Abilities ?? []).Contains(id);

    // ---- execution -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunTfStrategiesBMore(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            // Preserving Forces' Open action: the chosen Followers (one or two) go from the discard pile to the hand; one that is no longer there is skipped.
            case TfStrategiesBMoreAbilities.PreservingOpen:
                foreach (var follower in SlotCards(command, TfStrategiesBMoreAbilities.FollowersSlot).ToArray())
                    if (follower.Zone == Zone.FateDiscard && follower.OwnerId == player.Id) { ReturnToHand(follower); Emit("followerReturned", $"{player.Name} put {Definition(follower).Name} into their hand.", player.Id, follower.Id); }
                return false;
            // Unsafe Passage's Reaction: the targeted Commander's movement from the held action is negated.
            case TfStrategiesBMoreAbilities.UnsafeNegate:
                NegateEffects(Card(HeldAction().TargetId!), "move");
                return false;
            // Unsafe Passage's Battle action: the additional action for the Follower's base abilities.
            case TfStrategiesBMoreAbilities.UnsafeGrant:
                GrantAdditionalAction(player.Id, [target!.Id], tag: TfStrategiesBMoreAbilities.UnsafeTag);
                return false;
        }
        return null;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTfStrategiesBMoreHooks()
    {
        EngineHooks.Abilities.Register("AddTfStrategiesBMore", 11810, static abilities => abilities.AddTfStrategiesBMore());
        EngineHooks.RunEffects.Register("RunTfStrategiesBMore", 11810, static (engine, c) => engine.RunTfStrategiesBMore(c.id, c.command, c.player, c.source, c.target));
        EngineHooks.TargetedReaction.Register("TfStrategiesBMoreTargetedReaction", 11810, static (engine, context) => engine.UnsafePassageApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.EitherOrPrompt.Register("PreservingEitherOrPrompt", 11810, static (engine, p, source, affected) => engine.PreservingPrompt(p, source));
        EngineHooks.EitherOrOptions.Register("PreservingEitherOrOptions", 11810, static (engine, p, affected, source, payable) => engine.PreservingOptions(p));
        EngineHooks.ApplyEitherOr.Register("ApplyPreserving", 11810, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyPreserving(p, option, source, decider));
        EngineHooks.FinishEitherOr.Register("FinishPreserving", 11810, static (engine, p) => engine.FinishPreserving(p));
    }
}
