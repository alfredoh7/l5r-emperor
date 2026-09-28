namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P3: NEW REACTION TRIGGER POINTS, ANNOUNCEMENT/INTERRUPT WINDOWS AND PRIORITY OVERRIDES. Every card was read letter by letter against its scan (the scan is authoritative).
//
// New primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md; none conflicts with it):
//   * A FORCED PERFORMER FOR A FIRST OPPORTUNITY CLAIMED IN THE ENGAGE WINDOW (Shosuro Toson - exp2): PendingEngage.FirstActionPerformerId / FirstActionPerformerPlayerId, read by CloseEngageWindow
//     exactly as Jade's Roar's keyword limit is: the limit applies only if that player actually ends up holding the first opportunity (a Defender's claim still overrides an Attacker's).
//     FirstActionRestriction.Reason names the card in the limit's message (Naval Invasion stays the default).
//   * A FOLLOW-UP AFTER A LOOK (Daigotsu Tenbatsu): a look with its own spec id (EmperorP3Looks) hands over, when it is arranged, to a "p3DarkVirtueDiscard" choice ("You may discard a Dark
//     Virtue; if you did, draw a card"), answered by "p3DiscardDarkVirtue" (TargetId = the Dark Virtue, or none to decline). A look played as a Reaction sets the window it was played in aside.
//   * "A CARD WITH A [KEYWORD] ABILITY" as a search criterion (SearchCriteria.AbilityKeyword, Daidoji Akeha): one of the card's printed actions carries the keyword (ActionCarriesKeyword: its own tag,
//     or the card's keyword that every ability inherits).
//   * "HIS MOVEMENT TO THE CURRENT BATTLEFIELD" as a negated effect kind (Request Authorization): NegatedEffects kind MoveToCurrentBattlefieldKind, read by MoveToBattlefield's negation check
//     (MovementToBattlefieldNegated) for the held action's effects only (the entries are cleared when it has resolved).
//   * THE ACTION'S COSTS ARE IGNORED (Iron Will): a before-action window's held action negated with its costs ignored (ResolveNegatedAction's ignoreCosts, Bad Kharma's), and -- should Protection stop the
//     negation -- its declared costs and Gold waived (MomokoWaivesCost's hook); in an Attack's window the costs are already paid, so that effect fails. "That player may take an additional Battle
//     action": the grant is made when the held action has finished (P3AfterHeldAction), so the negated action's own tail does not forfeit it.
//   * AN ADDITIONAL USE OF A CARD'S ABILITIES THIS TURN (Centering the Soul): a per-card counter read by EffectiveUsesPerTurn (AbilityLimits.cs), The Fires of War's mark generalised to a count.
//   * A TERRAIN'S AFTER-ACTION TRAIT (Deep Snows: "After an action resolves: Bow any Personalities it moved"): applied in FinishActionEffectTracking before any Reaction to the same moment.
// ======================================================================================================================================================================================
public static class EmperorP3Abilities
{
    public const string TosonEngage = "toson-exp2-engage";
    public const string TenbatsuEngage = "tenbatsu-engage-look";
    public const string AkehaDraw = "akeha-recon-draw";
    public const string AkehaSearch = "akeha-recon-search";
    public const string ErosionWeaken = "erosion-weaken";
    public const string ErosionEffect = "erosion-provinces";
    public const string CenteringStraighten = "centering-soul-straighten";
    public const string CenteringStraightenVoid = "centering-soul-straighten-destroy";
    public const string DeepSnowsEnter = "deep-snows-enter";
    public const string DeepSnowsEffect = "deep-snows-bow-moved";
    public const string RequestMove = "request-authorization-move";
    public const string RequestNegate = "request-authorization-negate";
    public const string IronWill = "iron-will-negate";

    public const string KentaMove = "kenta-move-home";
    public const string ForgingNegate = "forging-destiny-negate";
    public const string ForgingMove = "forging-destiny-move";
    public const string DefendersHonor = "hida-defenders-honor";
    public const string FujitaNegate = "fujita-negate-follower-destruction";
    public const string RidersNegate = "vigilant-riders-negate";
    public const string RidersEffect = "vigilant-riders-straighten";
    public const string TairuDraw = "tairu-exp-draw";
    public const string MikiEffect = "miki-events-dark-virtue";
    /// <summary>Forging Destiny's trigger: the window before an Event resolves ("p3BeforeEventReaction").</summary>
    public const string BeforeEventTrigger = "p3-before-event-resolves";
    public const string DishonorableEnemyAtBattleTarget = "p3-dishonorable-enemy-personality-at-battle";
    public const string MovedToCurrentBattlefieldTarget = "p3-moved-to-current-battlefield";
    public const string OwnRingBowedByLastActionTarget = "p3-own-ring-bowed-by-last-action";

    public static SearchSpec AkehaReconSearch { get; } = new("akeha-recon", "Search your Fate deck for a non-Unique card with a Recon ability",
        [new(SearchDeck.Fate, new("non-Unique card with a Recon ability", NonUnique: true, AbilityKeyword: "Recon"), SearchEffect.ToHand)]);

    public static Dictionary<string, AbilityDefinition> AddEmperorP3(this Dictionary<string, AbilityDefinition> all)
    {
        // Shosuro Toson - exp2 (oracle-l5r-7177, Before the Dawn): "Reaction: After engaging at Toson's battlefield: Discard a card from the current battlefield's province. If this discarded a
        // Personality, you have the first opportunity to take a Battle action in this battle, which must be performed by Toson."
        all[TosonEngage] = new("Reaction", "After engaging at Toson's battlefield: Discard a card from the current battlefield's province. If this discarded a Personality, you have the first opportunity to take a Battle action in this battle, which must be performed by Toson",
            "none", Trigger: "after-engaging-at-own-battlefield");
        // Daigotsu Tenbatsu (oracle-l5r-1778, Embers of War): "Reaction: After engaging at Tenbatsu's battlefield: Look at the top three cards of your Fate deck. Put them back in any order. You may
        // discard a Dark Virtue; if you did, draw a card."
        all[TenbatsuEngage] = new("Reaction", "After engaging at Tenbatsu's battlefield: Look at the top three cards of your Fate deck. Put them back in any order. You may discard a Dark Virtue; if you did, draw a card",
            "none", Trigger: "after-engaging-at-own-battlefield", Look: EmperorP3Looks.Tenbatsu);
        // Daidoji Akeha (oracle-l5r-1621, Embers of War): "Reaction: After your Recon action from a Strategy resolves: Draw a card." and "Recon Limited: Discard a card: Search your Fate deck for a
        // non-Unique card with a Recon ability, show it, and put it in your hand."
        all[AkehaDraw] = new("Reaction", "After your Recon action from a Strategy resolves: Draw a card", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        all[AkehaSearch] = new("Limited", "Recon Limited: Discard a card: Search your Fate deck for a non-Unique card with a Recon ability, show it, and put it in your hand", "hand",
            Costs: ActionCost.DiscardTarget, ActionKeywords: ["Recon"], Search: AkehaReconSearch);
        // Erosion (oracle-l5r-2363, Before the Dawn, Spell): "While this Shugenja is Water, other players' provinces have -1 strength." (its Effect, StaticProvinceBonus) and "Reaction: After the
        // resolution of an action that moved a Personality to the current battlefield: Give him -4F."
        all[ErosionWeaken] = new("Reaction", "After the resolution of an action that moved a Personality to the current battlefield: Give him -4F", MovedToCurrentBattlefieldTarget,
            Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Centering the Soul (oracle-l5r-1277, Embers of War, Spell): "Reaction: After an action resolves, bow this card and target one of your Rings the action bowed: Straighten the Ring. If this
        // Shugenja is Void, you may destroy this card; if you did, you may use the Ring's base abilities an additional time this turn." One printed action, two catalog entries sharing its use: the
        // second is the play that destroys the Spell (only while its Shugenja is Void).
        all[CenteringStraighten] = new("Reaction", "After an action resolves, bow this card and target one of your Rings the action bowed: Straighten the Ring", OwnRingBowedByLastActionTarget,
            Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "centering-the-soul", Costs: ActionCost.BowSource);
        all[CenteringStraightenVoid] = new("Reaction", "After an action resolves, bow this card and target one of your Rings the action bowed: Straighten the Ring. This Shugenja is Void: destroy this card; you may use the Ring's base abilities an additional time this turn",
            OwnRingBowedByLastActionTarget, Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "centering-the-soul", Costs: ActionCost.BowSource);
        // Deep Snows (oracle-l5r-1921, Embers of War, Winter Terrain): "After an action resolves: Bow any Personalities it moved." (a trait: its Effect) and "Reaction: After a Battle action resolves,
        // if there are no Terrains in play at the current battlefield: Put this card into play there."
        all[DeepSnowsEnter] = new("Reaction", "After a Battle action resolves, if there are no Terrains in play at the current battlefield: Put this card into play there", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Request Authorization (oracle-l5r-6265, Embers of War, Political): "Battle: Bow your performing Courtier at any location and target a Personality: Move him home." and "Reaction: Before
        // another player's Battle action resolves, target a Personality: Negate his movement to the current battlefield from the action's effects."
        all[RequestMove] = new("Battle", "Bow your performing Courtier at any location and target a Personality: Move him home", "personality-at-any-location",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowPerformer);
        all[RequestNegate] = new("Reaction", "Before another player's Battle action resolves, target a Personality: Negate his movement to the current battlefield from the action's effects", "personality",
            Trigger: "before-action-resolves");
        // Iron Will (oracle-l5r-3813, Before the Dawn): "Reaction: After another player targets your Paragon with a Battle action performed by a Personality with lower Personal Honor: The action's
        // costs are ignored and its effects are negated. That player may take an additional Battle action."
        all[IronWill] = new("Reaction", "After another player targets your Paragon with a Battle action performed by a Personality with lower Personal Honor: The action's costs are ignored and its effects are negated. That player may take an additional Battle action",
            "none", Trigger: ReactionTriggers.TargetedByAction);
        // Kakita Kenta (oracle-l5r-4189, Embers of War): "Iaijutsu Reaction: After an action resolves during which Kenta won a duel, target a dishonorable enemy Personality at the current
        // battlefield: Move him home."
        all[KentaMove] = new("Reaction", "Iaijutsu Reaction: After an action resolves during which Kenta won a duel, target a dishonorable enemy Personality at the current battlefield: Move him home",
            DishonorableEnemyAtBattleTarget, Trigger: AfterActionReactionTriggers.AfterActionResolved, ActionKeywords: ["Iaijutsu"]);
        // Forging Destiny (oracle-l5r-2652, Before the Dawn): "Reaction: Before an Event resolves: Negate its effects." and "Battle: Choose your performing Personality: Move him home.
        // Straighten his unit if he moved."
        all[ForgingNegate] = new("Reaction", "Before an Event resolves: Negate its effects", "none", Trigger: BeforeEventTrigger);
        all[ForgingMove] = new("Battle", "Choose your performing Personality: Move him home. Straighten his unit if he moved", "none", Performer: "own-personality-at-battle");
        // Hida Defenders (oracle-l5r-3083, Before the Dawn, Follower): "Reaction: After the resolution of your action that bowed, destroyed, or moved a Personality who has opposed this card at
        // the current battlefield: Gain 2 Honor."
        all[DefendersHonor] = new("Reaction", "After the resolution of your action that bowed, destroyed, or moved a Personality who has opposed this card at the current battlefield: Gain 2 Honor", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Hida Fujita (oracle-l5r-3093, Before the Dawn): "Reaction: Before the resolution of another player's action, if Fujita is at the current battlefield: Negate the destruction of your
        // Followers from its effects."
        all[FujitaNegate] = new("Reaction", "Before the resolution of another player's action, if Fujita is at the current battlefield: Negate the destruction of your Followers from its effects", "none",
            Trigger: "before-action-resolves");
        // Vigilant Riders (oracle-l5r-9158, Before the Dawn, Cavalry Follower): "After this card bows: Straighten it." (a trait: its Effect) and "Reaction: After another player's action targets
        // a card in this unit: Negate this card's destruction from the action's effects."
        all[RidersNegate] = new("Reaction", "After another player's action targets a card in this unit: Negate this card's destruction from the action's effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        // Utaku Tairu - exp (oracle-l5r-9100, Before the Dawn): "Reaction: After a Personality assigns or moves, if he is now opposing Tairu: Draw a card."
        all[TairuDraw] = new("Reaction", "After a Personality assigns or moves, if he is now opposing Tairu: Draw a card", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        return all;
    }
}

/// <summary>The looks of this group (LookCatalog.All joins them).</summary>
public static class EmperorP3Looks
{
    /// <summary>Daigotsu Tenbatsu: the top three cards of his controller's Fate deck, put back in any order (Public Records' shape, its own id so the Dark Virtue follow-up knows it).</summary>
    public static LookSpec Tenbatsu { get; } = new("tenbatsu-look", "Look at the top three cards of your Fate deck", [new("Fate")], 3, [], Reorder: true);
    public static IEnumerable<LookSpec> All => [Tenbatsu, EmperorP3PriorityAbilities.KikugoroLook];
}

/// <summary>What this group keeps between commands (issue #503).</summary>
public sealed class EmperorP3State
{
    /// <summary>Iron Will: the held action whose costs are ignored (player, source card, ability).</summary>
    public HeldActionKey? CostsIgnored { get; set; }
    /// <summary>Iron Will: "That player may take an additional Battle action", owed once the held action has finished.</summary>
    public List<HeldActionKey> OwedGrants { get; set; } = [];
    /// <summary>Tenbatsu's look, finished and waiting for the Dark Virtue decision: how to finish the Reaction afterwards.</summary>
    public PendingLook? DarkVirtueLook { get; set; }
    /// <summary>A duel's action whose after-action tracking waits behind its duel-won window (Kakita Kenta).</summary>
    public StashedDuelTracking? StashedDuel { get; set; }
    /// <summary>Forging Destiny: the Event whose window is open or was asked about, and whether its effects are negated.</summary>
    public string? EventWindowCardId { get; set; }
    public int EventWindowPlayerId { get; set; }
    public string? EventAskedCardId { get; set; }
    public string? EventNegatedCardId { get; set; }
    /// <summary>Daigotsu Miki: the Mikis whose Events Phase trait was applied ("card|turn"), and the one asking now.</summary>
    public List<string> MikiHandled { get; set; } = [];
    public string? MikiAsking { get; set; }
    /// <summary>Hida Fujita: "Negate the destruction of your Followers from its effects" -- the protected player and the held action.</summary>
    public List<FollowerShield> FollowerShields { get; set; } = [];
    /// <summary>Lockouts, Aroru's owed move, announce watchers and negated Honor gains (EmperorP3Priority.cs).</summary>
    public EmperorP3PriorityState Priority { get; set; } = new();
    /// <summary>Easy Persuasion: what was paid for each entry, and the window after it (EmperorP3Entries.cs).</summary>
    public EmperorP3EntryState Entries { get; set; } = new();
}

/// <summary>What a duel's action affected, set aside while its duel-won window is open.</summary>
public sealed class StashedDuelTracking
{
    public string AbilityId { get; set; } = "";
    public int PlayerId { get; set; }
    public string? SourceId { get; set; }
    public string? PerformerId { get; set; }
    public List<AffectedCard> Affected { get; set; } = [];
    public List<int> HonorLosers { get; set; } = [];
    public List<int> HandRecipients { get; set; } = [];
    public List<int> FavorTakers { get; set; } = [];
}

/// <summary>Hida Fujita's negation: the Followers of PlayerId are not destroyed by the effects of the held action Action (this turn).</summary>
public sealed record FollowerShield(int PlayerId, HeldActionKey Action, int Turn);

/// <summary>An announced action named by who took it, from which card and which ability.</summary>
public sealed record HeldActionKey(int PlayerId, string? CardId, string AbilityId);

public sealed partial class MatchEngine
{
    public const string MoveToCurrentBattlefieldKind = "move-to-current-battlefield";
    private const string P3ExtraUseKey = "p3:additional-use";
    private EmperorP3State P3 => _state.P3;

    private static HeldActionKey? KeyOf(GameCommand? command) => command?.AbilityId is { } id ? new(command.PlayerId, command.CardId, id) : null;

    // ---- hooks into the shared switches ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // The target categories (CardAbilities.cs, the category validation's fallback chain).
    private bool P3TargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        // Erosion: "a Personality" the resolved action moved, now at the current battlefield.
        EmperorP3Abilities.MovedToCurrentBattlefieldTarget => Definition(target).Type == CardType.Personality && InPlay(target) && _state.CurrentBattleProvince is int battle
            && target.Zone == Zone.Battlefield && target.ProvinceIndex == battle && _state.LastResolvedAction is { } moved && moved.Affected.Any(a => a.Kind == MovedKind && a.CardId == target.Id),
        // Centering the Soul: "one of your Rings the action bowed".
        EmperorP3Abilities.OwnRingBowedByLastActionTarget => Definition(target).Type == CardType.Ring && InPlay(target) && target.OwnerId == player.Id
            && _state.LastResolvedAction is { } bowed && bowed.Affected.Any(a => a.Kind == BowedRingKind && a.CardId == target.Id),
        // Kakita Kenta: "a dishonorable enemy Personality at the current battlefield".
        EmperorP3Abilities.DishonorableEnemyAtBattleTarget => Definition(target).Type == CardType.Personality && target.OwnerId != player.Id && AtBattle(target)
            && MatchesTargetStatus(target, TargetStatus.IsDishonorable),
        _ => P3PriorityTargetAllows(category, player, source, target) // EmperorP3Priority.cs
    };

    // The after-action-resolved Reactions' conditions (AfterActionResolvedApplies' hook); null when the id is not this group's.
    private bool? P3AfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        switch (abilityId)
        {
            // "After your Recon action from a Strategy resolves" -- Advanced Intelligence's classification, the action this player's own.
            case EmperorP3Abilities.AkehaDraw:
                return facts.PlayerId == playerId && AbilityCatalog.All.TryGetValue(facts.AbilityId, out var reconAction) && facts.SourceId is { } reconSourceId
                    && _state.Cards.TryGetValue(reconSourceId, out var reconSource) && Definition(reconSource).Type == CardType.Strategy && ActionCarriesKeyword(reconAction, reconSource, "Recon");
            case EmperorP3Abilities.ErosionWeaken:
                return _state.CurrentBattleProvince is int battle && facts.Affected.Any(a => a.Kind == MovedKind && Card(a.CardId) is { Zone: Zone.Battlefield } moved && moved.ProvinceIndex == battle
                    && Definition(moved).Type == CardType.Personality);
            case EmperorP3Abilities.CenteringStraighten:
            case EmperorP3Abilities.CenteringStraightenVoid:
                return facts.Affected.Any(a => a.Kind == BowedRingKind && Card(a.CardId) is { } ring && ring.OwnerId == playerId && InPlay(ring));
            case EmperorP3Abilities.DeepSnowsEnter:
                return IsBattleAction(facts.AbilityId) && _state.CurrentBattleProvince is not null;
            // "After an action resolves during which Kenta won a duel".
            case EmperorP3Abilities.KentaMove:
                return facts.Affected.Any(a => a.Kind == WonDuelKind && a.CardId == source.Id);
            case EmperorP3Abilities.DefendersHonor:
                return facts.PlayerId == playerId && DefendersApplies(source, facts);
            default:
                return P3PriorityAfterActionApplies(source, abilityId, playerId, facts, out var priorityApplies) ? priorityApplies : null; // Asako Kikugoro's Ring half
        }
    }

    // Utaku Tairu - exp: read BEFORE AfterActionResolvedApplies' assignment-sentinel check (an assignment is not a catalogued action); null when the id is not his.
    private bool? P3AssignmentOrActionApplies(CardInstance source, string abilityId, ResolvedActionFacts facts) =>
        abilityId == EmperorP3Abilities.TairuDraw ? TairuApplies(source, facts) : null;

    // "After a Personality assigns or moves, if he is now opposing Tairu": a Personality the assignment placed or the action moved stands at Tairu's battlefield on the other side.
    private bool TairuApplies(CardInstance tairu, ResolvedActionFacts facts) =>
        tairu.Zone == Zone.Battlefield && tairu.ProvinceIndex is int here
        && facts.Affected.Any(a => a.Kind is AssignedKind or MovedKind && Card(a.CardId) is { Zone: Zone.Battlefield } p && p.ProvinceIndex == here && p.OwnerId != tairu.OwnerId
            && Definition(p).Type == CardType.Personality);

    // OpenAssignedReactionWindow's hook: an assignment worth a window for this group (Tairu may be opposed by it).
    private bool P3WatchesAssignment(IEnumerable<string> assigned) =>
        _state.Cards.Values.Any(t => t.Zone == Zone.Battlefield && AbilityIdsOf(t).Contains(EmperorP3Abilities.TairuDraw)
            && assigned.Any(id => _state.Cards.TryGetValue(id, out var p) && p.Zone == Zone.Battlefield && p.ProvinceIndex == t.ProvinceIndex && p.OwnerId != t.OwnerId));

    // Hida Defenders: "your action that bowed, destroyed, or moved a Personality who has opposed this card at the current battlefield" -- the Defenders' unit is at the current battlefield, and
    // the Personality stands on the other side there now, or was destroyed or moved from there by the action.
    private bool DefendersApplies(CardInstance defenders, ResolvedActionFacts facts)
    {
        if (defenders is not { Zone: Zone.Attached, AttachedTo: { } hostId } || _state.CurrentBattleProvince is not int battle) return false;
        var host = Card(hostId);
        if (host.Zone != Zone.Battlefield || host.ProvinceIndex != battle) return false;
        return facts.Affected.Any(a => a.Kind is BowedPersonalityKind or DestroyedKind or MovedKind && Card(a.CardId) is { } p && Definition(p).Type == CardType.Personality
            && p.OwnerId != defenders.OwnerId && (p.Zone == Zone.Battlefield && p.ProvinceIndex == battle || a.FromBattlefield == battle));
    }

    // The before-action Reactions' conditions (BeforeActionReactionApplies' hook); null when the id is not this group's.
    private bool? P3BeforeActionApplies(CardInstance card, string abilityId, GameCommand command)
    {
        // Hida Fujita: another player's action, Fujita at the current battlefield, and a Follower of his controller's in play that its effects could destroy.
        if (abilityId == EmperorP3Abilities.FujitaNegate)
            return InPlay(card) && card.Zone == Zone.Battlefield && card.ProvinceIndex == _state.CurrentBattleProvince && command.PlayerId != card.OwnerId && command.AbilityId is { } fujitaActing
                && AbilityCatalog.All.TryGetValue(fujitaActing, out var fujitaAction) && IsAction(fujitaAction)
                && _state.Cards.Values.Any(f => f.OwnerId == card.OwnerId && f.Zone == Zone.Attached && Definition(f).Type == CardType.Follower);
        if (abilityId != EmperorP3Abilities.RequestNegate) return null;
        // A Strategy in its player's hand; another player's Battle action whose effects move Personalities to the battle, and whose movement may be negated.
        return card.Zone == Zone.Hand && Definition(card).Type == CardType.Strategy && command.PlayerId != card.OwnerId && command.AbilityId is { } acting
            && AbilityCatalog.All.TryGetValue(acting, out var ability) && IsAction(ability) && ability.Timing != "Reaction" && IsBattleAction(command) && ability.MovesToBattle && !MovementCannotBeNegated(command);
    }

    // The candidate targets of a before-action Reaction in the window's listing (MatchQueries); [null] when it names none.
    private string?[] P3BeforeActionTargets(string target) => target == "personality" && _state.PendingChoice?.Type == "beforeActionReaction"
        ? _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray()
        : [null];

    // The targeted-by-action Reactions' conditions (TargetedActionReactionApplies' fallback).
    private bool P3TargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        if (P3PriorityTargetedApplies(card, abilityId, subject, affected)) return true; // Shosuro Aroru - exp2 (EmperorP3Priority.cs)
        // Vigilant Riders: another player's action targets a card in this unit.
        if (abilityId == EmperorP3Abilities.RidersNegate)
            return card is { Zone: Zone.Attached } && subject.Command.PlayerId != card.OwnerId && UnitHostId(affected) == UnitHostId(card);
        if (abilityId != EmperorP3Abilities.IronWill) return false;
        var command = subject.Command;
        if (card.Zone != Zone.Hand || command.PlayerId == card.OwnerId || !IsBattleAction(command)) return false;
        if (affected.OwnerId != card.OwnerId || Definition(affected).Type != CardType.Personality || !Keyword(affected, "Paragon")) return false;
        return ActionPerformerPerRules(command) is { } performer && Definition(performer).Type == CardType.Personality && CardPersonalHonor(performer) < CardPersonalHonor(affected);
    }

    // The per-id conditions of ValidateAbility.
    private void ValidateEmperorP3(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        ValidateEmperorP3Priority(id, command, player, source); // EmperorP3Priority.cs: the Reaction lockout and its cards' conditions
        switch (id)
        {
            case EmperorP3Abilities.CenteringStraightenVoid:
                Require(source is { Zone: Zone.Attached, AttachedTo: { } host } && Keyword(Card(host), "Void"), "This Shugenja is not Void.");
                break;
            case EmperorP3Abilities.DeepSnowsEnter:
                Require(source!.Zone == Zone.Hand && player.Hand.Contains(source.Id), "Deep Snows is put into play from your hand.");
                Require(!TerrainAtCurrentBattlefield() || MarshallingFieldsWaiverAvailable(player.Id), "There is already a Terrain in play at the current battlefield."); // The Marshalling Fields (issue #501)
                break;
        }
    }

    // The execution switch's hook: null when the id is not this group's; false when its effects are done; true when a decision opened (the action finishes from there).
    private bool? RunEmperorP3(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, HashSet<string> bowedBefore, bool consumedGrant)
    {
        var name = source is null ? "an action" : Definition(source).Name;
        switch (id)
        {
            case EmperorP3Abilities.TosonEngage:
            {
                var discarded = CurrentBattlefieldProvince()?.CardId is { } cardId ? Card(cardId) : null;
                DiscardCurrentProvinceCard();
                if (discarded is not null && Definition(discarded).Type == CardType.Personality && _state.PendingEngage is { } engage)
                {
                    ClaimFirstOpportunity(player.Id, "Engage", source);
                    engage.FirstActionPerformerId = source!.Id; engage.FirstActionPerformerPlayerId = player.Id;
                }
                else Emit("firstOpportunityNotClaimed", $"{name}: no Personality was discarded, so no opportunity is claimed.", player.Id, source?.Id);
                return false;
            }
            case EmperorP3Abilities.TenbatsuEngage:
                // A Reaction's look sets the window it was played in aside, and the window is given back when the Reaction is done (ReactionsAreActions.cs).
                SuspendOpenWindow();
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBefore);
                return true;
            case EmperorP3Abilities.AkehaDraw:
                Draw(player);
                return false;
            case EmperorP3Abilities.AkehaSearch:
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBefore);
                return true;
            case EmperorP3Abilities.ErosionWeaken:
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                return false;
            case EmperorP3Abilities.CenteringStraighten:
                Straighten(target!);
                return false;
            case EmperorP3Abilities.CenteringStraightenVoid:
                Straighten(target!);
                DestroyAttachment(source!);
                if (source!.Zone != Zone.Attached) // "if you did"
                {
                    GiveAdditionalUseThisTurn(target!);
                    Emit("additionalUseGranted", $"{name}: {Player(target!.OwnerId).Name} may use {Definition(target).Name}'s abilities an additional time this turn.", player.Id, target.Id);
                }
                return false;
            case EmperorP3Abilities.DeepSnowsEnter:
                if (TerrainAtCurrentBattlefield()) SpendMarshallingFieldsWaiver(player.Id, source!); // The Marshalling Fields (issue #501): the restriction was ignored
                PutTerrainIntoPlay(player, source!);
                return false;
            case EmperorP3Abilities.RequestMove:
                MoveHome(target!, false);
                return false;
            case EmperorP3Abilities.RequestNegate:
                _state.NegatedEffects.Add(new(MoveToCurrentBattlefieldKind, target!.Id));
                Emit("effectsNegated", $"{name}: {Definition(target).Name}'s movement to the current battlefield from the action's effects is negated.", player.Id, target.Id);
                return false;
            case EmperorP3Abilities.IronWill:
                IronWillNegates(source!);
                return false;
            case EmperorP3Abilities.KentaMove:
                MoveHome(target!, false);
                return false;
            case EmperorP3Abilities.ForgingNegate:
                if (P3.EventWindowCardId is { } eventId)
                {
                    P3.EventNegatedCardId = eventId;
                    Emit("eventNegated", $"{name}: the effects of {Definition(Card(eventId)).Name} are negated.", player.Id, eventId);
                }
                return false;
            case EmperorP3Abilities.ForgingMove:
            {
                var performer = Card(command.PerformerId!);
                var wasAway = performer.Zone == Zone.Battlefield;
                MoveHome(performer, false);
                if (wasAway && performer.Zone == Zone.Home) StraightenBowedUnit(performer); // "Straighten his unit if he moved."
                return false;
            }
            case EmperorP3Abilities.DefendersHonor:
                GainHonor(player.Id, 2, name, source);
                return false;
            case EmperorP3Abilities.FujitaNegate:
                if (KeyOf(_state.PendingNegatableAbility) is { } held)
                {
                    P3.FollowerShields.RemoveAll(s => s.Turn != _state.TurnNumber);
                    P3.FollowerShields.Add(new(player.Id, held, _state.TurnNumber));
                    Emit("effectsNegated", $"{name}: the destruction of {player.Name}'s Followers from the action's effects is negated.", player.Id, source?.Id);
                }
                return false;
            case EmperorP3Abilities.RidersNegate:
                NegateEffects(source!, "destroy");
                return false;
            case EmperorP3Abilities.TairuDraw:
                Draw(player);
                return false;
            default:
                return RunEmperorP3Priority(id, ability, command, player, source, target, consumedGrant, bowedBefore); // EmperorP3Priority.cs
        }
    }

    // ---- Shosuro Toson - exp2: the forced performer of the first opportunity (CloseEngageWindow's hook) --------------------------------------------------------------------------------------

    private FirstActionRestriction? P3FirstActionPerformerLimit(PendingEngage engage, int holder) =>
        engage.FirstActionPerformerId is { } performer && engage.FirstActionPerformerPlayerId == holder && _state.Cards.ContainsKey(performer)
            ? new(holder, performer, false, Reason: Definition(Card(performer)).Name) : null;

    // ---- Daigotsu Tenbatsu: "You may discard a Dark Virtue; if you did, draw a card" after the look (FinishLook's hook) ---------------------------------------------------------------------

    private bool IsDarkVirtue(CardInstance card) => Keyword(card, "Dark Virtue");

    /// <summary>True when the look's follow-up opened (the look is finished from there).</summary>
    private bool P3AfterLook(PendingLook look)
    {
        if (look.LookId != EmperorP3Looks.Tenbatsu.Id) return false;
        var player = Player(look.PlayerId);
        if (!player.Hand.Select(Card).Any(IsDarkVirtue))
        {
            Emit("darkVirtueNone", $"{Definition(Card(look.SourceCardId)).Name}: {player.Name} has no Dark Virtue to discard.", player.Id, look.SourceCardId);
            return false;
        }
        _state.PendingLook = null;
        P3.DarkVirtueLook = look;
        _state.PendingChoice = new("p3DarkVirtueDiscard", player.Id, look.SourceCardId, $"{Definition(Card(look.SourceCardId)).Name}: you may discard a Dark Virtue; if you do, draw a card.");
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        return true;
    }

    private void ResolveP3DarkVirtueDiscard(GameCommand command)
    {
        var look = P3.DarkVirtueLook ?? throw new RuleViolation("No Dark Virtue decision is open.");
        Require(command.Type == "p3DiscardDarkVirtue" && command.PlayerId == look.PlayerId, "Discard a Dark Virtue, or decline.");
        var player = Player(look.PlayerId);
        if (command.TargetId is { } chosen)
        {
            Require(player.Hand.Contains(chosen) && IsDarkVirtue(Card(chosen)), "Choose a Dark Virtue in your hand.");
            DiscardFate(player, Card(chosen));
            Draw(player);
        }
        else Emit("darkVirtueDeclined", $"{player.Name} discards no Dark Virtue.", player.Id, look.SourceCardId);
        P3.DarkVirtueLook = null;
        FinishDeferredAction(look.PlayerId, look.Origin, look.SourceCardId, look.BowedBefore);
    }

    private IEnumerable<ActionOption> P3DarkVirtueActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "p3DarkVirtueDiscard" } pending || pending.PlayerId != playerId) yield break;
        foreach (var card in Player(playerId).Hand.Select(Card).Where(IsDarkVirtue).OrderBy(c => c.Id, StringComparer.Ordinal))
            yield return new("p3DiscardDarkVirtue", $"Discard {Definition(card).Name} and draw a card", true, null,
                new GameCommand("p3DiscardDarkVirtue", playerId, _state.Version, TargetId: card.Id), pending.CardId, [card.Id]);
        yield return new("p3DiscardDarkVirtue", "Discard no Dark Virtue", true, null, new GameCommand("p3DiscardDarkVirtue", playerId, _state.Version), pending.CardId, []);
    }

    // ---- Daidoji Akeha: "a card with a Recon ability" (SearchEligibility's hook) ----------------------------------------------------------------------------------------------------------------

    private bool HasActionWithKeyword(CardInstance card, string keyword) =>
        (Definition(card).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var ability) && IsAction(ability) && ActionCarriesKeyword(ability, card, keyword));

    // ---- Erosion's province penalty (StaticProvinceBonus' hook) -----------------------------------------------------------------------------------------------------------------------------------

    private int P3StaticProvinceBonus(CardInstance card, bool mine) =>
        Definition(card).Effect == EmperorP3Abilities.ErosionEffect && !mine && card is { Zone: Zone.Attached, AttachedTo: { } host } && Keyword(Card(host), "Water") ? -1 : 0;

    // ---- Centering the Soul: an additional use this turn (EffectiveUsesPerTurn's hook) ------------------------------------------------------------------------------------------------------------

    private void GiveAdditionalUseThisTurn(CardInstance card) =>
        card.AbilityUses[P3ExtraUseKey] = _state.TurnNumber * UseCountTurnScale + P3AdditionalUses(card.AbilityUses) + 1;

    private int P3AdditionalUses(Dictionary<string, int> uses) =>
        uses.TryGetValue(P3ExtraUseKey, out var marked) && marked / UseCountTurnScale == _state.TurnNumber ? marked % UseCountTurnScale : 0;

    // ---- Deep Snows' trait (FinishActionEffectTracking's hook): "After an action resolves: Bow any Personalities it moved." ----------------------------------------------------------------------

    private void ApplyP3AfterActionTraits(List<AffectedCard> affected)
    {
        foreach (var snows in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && Definition(c).Effect == EmperorP3Abilities.DeepSnowsEffect).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            foreach (var moved in affected.Where(a => a.Kind == MovedKind).Select(a => a.CardId).Distinct().Select(Card)
                         .Where(c => InPlay(c) && Definition(c).Type == CardType.Personality && !c.Bowed).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            {
                if (StalwartNegatesBow(moved, snows.OwnerId)) continue;
                moved.Bowed = true;
                Emit("terrainBow", $"{Definition(snows).Name}: {Definition(moved).Name} moved and bows.", snows.OwnerId, moved.Id);
            }
    }

    // ---- Request Authorization: "Negate his movement to the current battlefield from the action's effects" (MovementToBattlefieldNegated's hook) -------------------------------------------------

    private bool P3MoveToBattlefieldNegated(CardInstance card, int battlefield) =>
        battlefield == _state.CurrentBattleProvince && _state.NegatedEffects.Any(n => n.CardId == card.Id && n.Kind == MoveToCurrentBattlefieldKind);

    // ---- Iron Will ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void IronWillNegates(CardInstance source)
    {
        if (_state.PendingChoice?.Type == "attackReaction" && _state.PendingAttack is { } attack)
        {
            // An Attack's costs were paid when it was announced: "The action's costs are ignored" fails; its effects are negated.
            NegateHeldAction(source);
            if (KeyOf(attack.Command) is { } attackKey) P3.OwedGrants.Add(attackKey);
            return;
        }
        if (_state.PendingNegatableAbility is not { } held || KeyOf(held) is not { } key) return;
        P3.CostsIgnored = key;
        Emit("costsIgnored", $"{Definition(source).Name}: the costs of {Player(held.PlayerId).Name}'s action are ignored.", source.OwnerId, source.Id);
        NegateDeferredAction(source);
        P3.OwedGrants.Add(key);
    }

    /// <summary>ResolveDeferredAction's hook: the held action negated as a whole ignores its costs.</summary>
    private bool P3CostsIgnored(GameCommand command) => P3.CostsIgnored is { } key && key == KeyOf(command);

    /// <summary>Called where a held action has finished (its negated tail, its resolution's tail, a deferred Attack's end): its costs are no longer ignored, and a grant Iron Will owes its player is made.</summary>
    private void P3AfterHeldAction(GameCommand? command)
    {
        if (KeyOf(command) is not { } key) return;
        P3EndLockouts(key); // Thunder Dragon, Aroru (EmperorP3Priority.cs): the action has ended
        if (P3.CostsIgnored == key) P3.CostsIgnored = null;
        if (!P3.OwedGrants.Remove(key)) return;
        GrantAdditionalAction(key.PlayerId, null);
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP3_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorP3", 3200, static abilities => abilities.AddEmperorP3());
        EngineHooks.Looks.Register("EmperorP3Looks", 500, static () => EmperorP3Looks.All);
        EngineHooks.Searches.Register("EmperorP3Abilities", 500, static () => [EmperorP3Abilities.AkehaReconSearch]);
    }
}
