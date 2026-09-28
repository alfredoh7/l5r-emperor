namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, PASS GROUP P6: cards blocked by a missing scan, by the province model, or by a rules-lane question (Before the Dawn ... Honor and Treachery). Every card was read against its scan (the
// scan is authoritative; the Emperor-era Event scans print only the effect, not the later "After you reveal cards in your Provinces ..." wording of the catalogue).
//
// Wired here: Kitsune Haruki (his Deer, oracle-l5r-9999, was already published), Daigotsu Susaiken, Kuni Renyu, Otomo Suikihime, Shosuro Ietsuno (personality-wave-116), Perfect Sacrifice
// (strategy-wave-102), Incredible Tragedy, In Search of Guidance, Dark Experiments, Distant Expansion (event-wave-29), Mining Apparatus (item-wave-38), Illusory Defense (spell-wave-46).
//
// New primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md; none conflicts with it):
//   * "REFILL A PROVINCE FACE-UP" (Susaiken: "Once per turn, you may refill one of your provinces face-up"; Distant Expansion: "In your Dynasty Phase, one time for each of your provinces destroyed this
//     game, you may refill one of your provinces face-up"). Under the core rules (Setting Up the Game, 8) a province is refilled at once, face down, whenever it has no card, so "refill it face-up" is
//     read as the MANNER of that refill: when one of the player's provinces refills from his Dynasty deck, he may have it refilled face-up instead (Agasha Ryo's precedent: the refilled card is turned
//     face-up). The decision is asked as soon as the command that refilled it is done (a queued decision, P6.Queue, opened from Apply's tail), before anyone can act on the new card; nobody has seen
//     the card, so asking then is the same as asking during the refill. Each use is spent: Susaiken once per turn (per card), Distant Expansion once per destroyed province of the player's in each of
//     his Dynasty Phases while it lasts ("until your third turn from now ends": it leaves play as his fourth turn from now begins -- no Dynasty Phase of his lies between).
//   * A REFILL TRIGGER (Ietsuno: "After a card from a deck refills Ietsuno's battlefield's province: Look at it. You may switch it with a card owned by the same player in another province."): every
//     refill from a deck (Refill, and the face-up refills of other cards) is reported to AfterProvinceRefilledP6; Ietsuno's controller sees the card (a province sight, private) and may choose one
//     card of the same owner in another of that player's provinces to swap places with it (each keeps its facing). A trait, so mandatory and usable while bowed; one decision per refill.
//   * GAINED PROVINCES AT ANY TIME (Suikihime, Mining Apparatus, Illusory Defense, Kuni Renyu): group F's GainProvince (Benefices of the Emperor) renumbered the row only for what exists in an Events
//     Phase; ShiftProvinceKeysForGainedLeftProvince now renumbers everything that names one of that player's provinces by index -- including a battle at them when he is the Defender (the units at
//     his battlefields, the current and unresolved battlefields, the engage / before-resolution / after-resolution / Conqueror records, Two Fronts' associations, delayed moves) -- so a province may be
//     gained to the left of the leftmost one in the Action Phase or during a battle. GainProvince takes a base strength (Renyu: 4) and a province that permanently holds no Dynasty cards
//     (ProvinceState.HoldsNoDynastyCards: Refill and every face-up refill skip it). "Create a province" is read as "gain a province".
//   * A PERMANENT PROVINCE MARK (Dark Experiments: ProvinceState.DarkExperimentsMarked): a Region that attaches to the province is destroyed (AttachRegionToProvince's hook) and a Holding that enters
//     play from it (bought from it) gets a -1 Gold Production token (a CardToken of Stat "GoldProduction": StatModifier reads tokens, and a Holding's production modifier -- The Moon's Imperative's -- is read by GoldProduced).
//   * A CAVALRY MANEUVERS LOCK (Incredible Tragedy, the scan: "Personalities may not be assigned during the Cavalry Maneuvers Segment"): while the Event is in play no Personality of either player may be
//     assigned in a Cavalry Maneuvers Segment (Assign refuses it, the offered assignment lists no unit).
//   * AN AGGREGATE CONDITION ON A SLOT (Mining Apparatus: Holdings with total base Gold Cost of 10 or higher; Illusory Defense: performing Shugenja with 10 or greater total Chi), checked when the action
//     is taken; the offered default selection is the fewest cards that meet it (highest first), so a viewer without a slot picker still gets a legal command. CostVerbs.RemoveSlotCards removes every
//     card of a slot from the game as a cost.
//   * STANDING TRAITS: Suikihime's "You may not assign, and will not move, Suikihime to battlefields" (MovementLocked), "will not become dishonorable" (Dishonor), "Other players' Political actions may
//     not target Suikihime" (RequireNotBarredFromTargeting); Renyu's "After an action targets Renyu: Reduce any Honor losses from its effects to 0" (GainHonor: any Honor loss, of any player, while
//     an action whose targets include Renyu -- or a duel it issued against him -- is resolving); Illusory Defense's "Your Air Shugenja have +1C" (PersonalityChi).
//   * "AFTER THE NEXT TIME THIS GAME SUIKIHIME LEAVES PLAY, DESTROY YOUR LEFTMOST PROVINCE": a watch checked after every command (Apply's tail), the MechB control-return convention.
// ======================================================================================================================================================================================

/// <summary>What pass group P6 keeps between commands (issue #503).</summary>
public sealed class EmperorP6State
{
    /// <summary>Refill decisions waiting for the command that caused them to finish (opened one at a time from Apply's tail).</summary>
    public List<P6QueuedDecision> Queue { get; set; } = [];
    /// <summary>In Search of Guidance: the Sensei its search found, by the Event's card id, put into the Event's province face-up when the Event leaves it.</summary>
    public Dictionary<string, string> SearchedRefills { get; set; } = [];
    /// <summary>Otomo Suikihime: "After the next time this game Suikihime leaves play, destroy your leftmost province" -- her card id and the player whose province it is.</summary>
    public List<P6LeavesPlayWatch> LeavesPlayWatches { get; set; } = [];
    /// <summary>Distant Expansion's uses, one entry "player|turn" per province refilled face-up with it.</summary>
    public List<string> DistantExpansionUses { get; set; } = [];
    /// <summary>The priority and pass count the open decision interrupted, restored when it is answered.</summary>
    public int ResumePriority { get; set; }
    public int ResumePasses { get; set; }
}

/// <summary>A decision owed after a refill: Kind "faceup" (Susaiken / Distant Expansion) or "ietsuno"; the province (owner, index) and the card that refilled it; the Ietsuno whose trait it is.</summary>
public sealed record P6QueuedDecision(string Kind, int PlayerId, int OwnerId, int Province, string CardId, string SourceId);

public sealed record P6LeavesPlayWatch(string CardId, int PlayerId, string Source);

public static class EmperorP6Abilities
{
    public const string HarukiCreate = "haruki-create", PerfectSacrificeBattle = "perfect-sacrifice-battle", PerfectSacrificeLimited = "perfect-sacrifice-limited", IetsunoBow = "ietsuno-bow",
        RenyuPurge = "renyu-purge", SuikihimeLands = "suikihime-lands", MiningApparatus = "mining-apparatus-province", IllusoryDefense = "illusory-defense-province";
    public const string OwnHolding = "p6-own-holding", OwnUnbowedShugenja = "p6-own-unbowed-shugenja";

    public static Dictionary<string, AbilityDefinition> AddEmperorP6(this Dictionary<string, AbilityDefinition> all)
    {
        // Kitsune Haruki (oracle-l5r-4462): "Reaction: After Haruki enters play: Create a 2F/2C/3PH Mantis Clan · Nonhuman · Deer · Spirit Personality with the ability, 'Limited: If another player has
        // higher Family Honor than you: Gain 2 Honor.'" -- Kitsune Ohsuki's shape; the Deer is the published Proxy Haruki's Deer (oracle-l5r-9999, personality-wave-64).
        all[HarukiCreate] = new("Reaction", "After Haruki enters play: Create a 2F/2C/3PH Mantis Clan · Nonhuman · Deer · Spirit Personality with the ability, \"Limited: If another player has higher Family Honor than you: Gain 2 Honor.\"",
            "none", Trigger: "own-enters-play",
            Creates: new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3, Keywords: ["Mantis Clan", "Nonhuman", "Deer", "Spirit"], AbilityText: "If another player has higher Family Honor than you: Gain 2 Honor"));
        // Perfect Sacrifice (oracle-l5r-5923, Dark Virtue · Perfection): "Battle/Limited: Choose your performing Samurai: Give each Personality and Follower in his unit a Force bonus equal to its current
        // Force. Before this turn ends, destroy his unit." Two ids sharing one use (no Battle/Limited timing exists; Duel to First Blood's split); the bonus lasts until end of turn, the destruction
        // is the shared delay store's (In the Heart of Battle's).
        all[PerfectSacrificeBattle] = new("Battle", "Choose your performing Samurai: Give each Personality and Follower in his unit a Force bonus equal to its current Force. Before this turn ends, destroy his unit", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Samurai"], UsageKey: "perfect-sacrifice");
        all[PerfectSacrificeLimited] = new("Limited", "Choose your performing Samurai: Give each Personality and Follower in his unit a Force bonus equal to its current Force. Before this turn ends, destroy his unit", "none",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Samurai"], UsageKey: "perfect-sacrifice");
        // Shosuro Ietsuno (oracle-l5r-7110): "Battle: Target an enemy card without attachments: Bow it." (Kaiu Tojikana's shape; his refill trait is his Effect.)
        all[IetsunoBow] = new("Battle", "Target an enemy card without attachments: Bow it", "enemy-card-without-attachments");
        // Kuni Renyu (oracle-l5r-9808): "Battle: Target an enemy unit: Its controller may choose to bow it and destroy all Shadowlands cards in it. If he did not choose this, create a province with a
        // base strength of 4 to the left of your leftmost province." -- the target's controller decides (an Either/Or, Extend the Soul's shape).
        all[RenyuPurge] = new("Battle", "Target an enemy unit: Its controller may choose to bow it and destroy all Shadowlands cards in it. If he did not choose this, create a province with a base strength of 4 to the left of your leftmost province", "enemy-unit");
        // Otomo Suikihime (oracle-l5r-10094): "Reaction: Once per game, after Suikihime enters play, if no other player controls The Forgotten Temple: Suikihime grants you new lands in the Colonies.
        // Gain a province to the left of your leftmost province. After the next time this game Suikihime leaves play, destroy your leftmost province."
        all[SuikihimeLands] = new("Reaction", "Once per game, after Suikihime enters play, if no other player controls The Forgotten Temple: Gain a province to the left of your leftmost province. After the next time this game Suikihime leaves play, destroy your leftmost province",
            "none", Trigger: "own-enters-play", OncePerGame: true);
        // Mining Apparatus (oracle-l5r-10120, Unique Item): "Limited: Remove this card and any number of Holdings you control with total base Gold Cost of 10 or higher from the game: Gain a province to
        // the left of your leftmost province."
        all[MiningApparatus] = new("Limited", "Remove this card and any number of Holdings you control with total base Gold Cost of 10 or higher from the game: Gain a province to the left of your leftmost province", "slots",
            Slots: [new("holdings", OwnHolding, "Holdings you control (total base Gold Cost 10 or higher)", 1, 20)],
            Costs: [new(CostVerbs.Remove), new(CostVerbs.RemoveSlotCards, CostSubject.Slot("holdings"))]);
        // Illusory Defense (oracle-l5r-3666, Air · Ritual Spell): "Limited: Bow one or more of your performing Shugenja with 10 or greater total Chi, and remove this card from the game: Create a province to
        // the left of your leftmost province which permanently does not hold Dynasty cards." (Its "Your Air Shugenja have +1C." is its Effect.)
        all[IllusoryDefense] = new("Limited", "Bow one or more of your performing Shugenja with 10 or greater total Chi, and remove this card from the game: Create a province to the left of your leftmost province which permanently does not hold Dynasty cards", "slots",
            Slots: [new("performers", OwnUnbowedShugenja, "Your performing Shugenja (10 or greater total Chi)", 1, 20, Performing: true)],
            Costs: [new(CostVerb.Bow, EmperorRemainingCAbilities.AllPerformingSlots), new(CostVerbs.Remove)]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    internal const string SusaikenId = "oracle-l5r-1771";
    internal const string IetsunoEffect = "ietsuno-refill-look", RenyuEffect = "renyu-honor-shield", SuikihimeEffect = "suikihime-governor", IllusoryDefenseEffect = "illusory-defense-air-chi",
        IncredibleTragedyEffect = "incredible-tragedy", InSearchOfGuidanceEffect = "in-search-of-guidance", DarkExperimentsEffect = "dark-experiments", DistantExpansionEffect = "distant-expansion";
    private const string SusaikenUseKey = "p6-susaiken-refill";
    private const string ForgottenTempleTitle = "The Forgotten Temple";

    private EmperorP6State P6 => _state.P6;

    // ---- validation and target categories ---------------------------------------------------------------------------------------------------------------------

    private void ValidateEmperorP6(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case EmperorP6Abilities.SuikihimeLands:
                Require(!_state.Cards.Values.Any(c => c.OwnerId != player.Id && InPlay(c) && Definition(c).Name == ForgottenTempleTitle), $"Another player controls {ForgottenTempleTitle}.");
                break;
            // Aggregate conditions on the whole selection (a candidate alone is judged by its category: ValidateSlotCard's probe carries no Slots).
            case EmperorP6Abilities.MiningApparatus when ability.Slots is not null:
                Require(SlotCards(command, "holdings").Sum(h => Definition(h).GoldCost) >= 10, "The Holdings removed must have a total base Gold Cost of 10 or higher.");
                break;
            case EmperorP6Abilities.IllusoryDefense when ability.Slots is not null:
                Require(SlotCards(command, "performers").Sum(PersonalityChi) >= 10, "Your performing Shugenja must have 10 or greater total Chi.");
                break;
        }
    }

    private bool EmperorP6TargetAllows(string category, PlayerState player, CardInstance target) => category switch
    {
        EmperorP6Abilities.OwnHolding => target.OwnerId == player.Id && target.Zone == Zone.Home && Definition(target).Type == CardType.Holding,
        EmperorP6Abilities.OwnUnbowedShugenja => target.OwnerId == player.Id && !target.Bowed && InPlay(target) && Definition(target).Type == CardType.Personality && Keyword(target, "Shugenja"),
        _ => false
    };

    // The offered default of a slot ability whose condition is an aggregate: the fewest legal candidates, highest first, that meet it (null: not one of these abilities, or no selection meets it).
    private GameCommand? EmperorP6DefaultSelection(GameCommand command, AbilityDefinition ability, TargetSlotOption[] options)
    {
        var (slot, value) = command.AbilityId switch
        {
            EmperorP6Abilities.MiningApparatus => ("holdings", (Func<CardInstance, int>)(h => Definition(h).GoldCost)),
            EmperorP6Abilities.IllusoryDefense => ("performers", PersonalityChi),
            _ => (null, null!)
        };
        if (slot is null || options.FirstOrDefault(o => o.Slot == slot) is not { } offered) return null;
        var picked = new List<string>(); var total = 0;
        foreach (var card in offered.CardIds.Select(Card).OrderByDescending(value).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            if (total >= 10) break;
            picked.Add(card.Id); total += value(card);
        }
        return total >= 10 ? command with { Selections = [new(slot, picked.ToArray())] } : null;
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------

    // Null when the id is not this group's; false when its effects are done; true when a decision opened and the action waits.
    private bool? RunEmperorP6(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, IEnumerable<string> bowedBeforeAction)
    {
        switch (id)
        {
            case EmperorP6Abilities.PerfectSacrificeBattle: case EmperorP6Abilities.PerfectSacrificeLimited:
            {
                var samurai = Card(command.PerformerId!);
                // "a Force bonus equal to its current Force": each card's Force is read before any bonus is given, so one card's bonus never feeds another's.
                var unit = Attachments(samurai.Id).Where(c => Definition(c).Type == CardType.Follower).Prepend(samurai).Select(c => (Card: c, Force: CardForce(c))).ToArray();
                foreach (var (card, force) in unit)
                    if (force > 0) ApplyModifier(card, source!.Id, id, "Force", force);
                Emit("perfectSacrifice", $"{Definition(source!).Name}: each Personality and Follower in {Definition(samurai).Name}'s unit doubles its Force.", player.Id, samurai.Id);
                ScheduleDelayedEffect(samurai, DelayedKinds.Destroy, DelayUntil.EndOfTurn, null, Definition(source!).Name, source!.Id); // the shared delay store (issue #492, EmperorRulingR2.cs)
                Emit("destructionScheduled", $"{Definition(samurai).Name}'s unit will be destroyed before this turn ends.", player.Id, samurai.Id);
                return false;
            }
            case EmperorP6Abilities.IetsunoBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case EmperorP6Abilities.RenyuPurge:
                return OpenEitherOr(EmperorP6Specs.RenyuPurge, source!, player.Id, target!.OwnerId, target, "action", bowedBeforeAction);
            case EmperorP6Abilities.SuikihimeLands:
                GainProvince(player, left: true);
                P6.LeavesPlayWatches.Add(new(source!.Id, player.Id, Definition(source).Name));
                Emit("suikihimeLands", $"{Definition(source).Name} grants {player.Name} new lands in the Colonies: after the next time she leaves play, {player.Name}'s leftmost province is destroyed.", player.Id, source.Id);
                return false;
            case EmperorP6Abilities.MiningApparatus:
                GainProvince(player, left: true);
                return false;
            case EmperorP6Abilities.IllusoryDefense:
                GainProvince(player, left: true, holdsNoDynastyCards: true);
                Emit("provinceHoldsNoDynastyCards", $"{player.Name}'s new leftmost province permanently does not hold Dynasty cards.", player.Id);
                return false;
            default:
                return null;
        }
    }

    // ---- Kuni Renyu: the Either/Or and the Honor shield -------------------------------------------------------------------------------------------------------------

    private string? EmperorP6EitherOrPrompt(PendingEitherOr p, string source, string affected) => p.SpecId == EmperorP6Specs.RenyuPurge
        ? $"{source}: bow {affected}'s unit and destroy all Shadowlands cards in it, or {Player(p.ActingPlayerId).Name} creates a province with a base strength of 4 to the left of their leftmost province." : null;

    private EitherOrOption[]? EmperorP6EitherOrOptions(PendingEitherOr p, CardInstance? affected)
    {
        if (p.SpecId != EmperorP6Specs.RenyuPurge) return null;
        var name = Definition(affected!).Name;
        return [
            new("purge", $"Bow {name}'s unit and destroy its Shadowlands cards", $"{name}'s unit bows and every Shadowlands card in it is destroyed.", EitherOrKind.Avoid, 0, InPlay(affected!), $"{name} is no longer in play."),
            new("refuse", "Do not choose this", $"{Player(p.ActingPlayerId).Name} creates a province with a base strength of 4 to the left of their leftmost province.", EitherOrKind.Accept, 0, true, null)
        ];
    }

    private bool ApplyEmperorP6EitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != EmperorP6Specs.RenyuPurge) return false;
        var affected = Card(p.AffectedCardId!);
        if (option.Key == "purge")
        {
            Emit("eitherOrChosen", $"{decider.Name} chose to bow {Definition(affected).Name}'s unit and destroy its Shadowlands cards ({Definition(source).Name}).", decider.Id, affected.Id);
            var unit = Attachments(affected.Id).Prepend(affected).ToArray();
            foreach (var card in unit.Where(c => !c.Bowed)) if (!StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true;
            var shadowlands = unit.Where(c => InPlay(c) && (Keyword(c, "Shadowlands") || HasClanAffiliation(c, "Shadowlands"))).ToArray();
            foreach (var attachment in shadowlands.Where(c => c.Id != affected.Id)) DestroyAttachment(attachment);
            if (shadowlands.Contains(affected)) DestroyUnits([affected], false, p.ActingPlayerId);
        }
        else
        {
            Emit("eitherOrChosen", $"{decider.Name} did not choose it ({Definition(source).Name}): {Player(p.ActingPlayerId).Name} creates a province.", decider.Id, source.Id);
            GainProvince(Player(p.ActingPlayerId), left: true, baseStrength: 4);
        }
        return true;
    }

    // "After an action targets Renyu: Reduce any Honor losses from its effects to 0." The action being resolved (an ability, the Attack it made, the paused action a decision finishes, a duel it issued).
    private bool RenyuReducesLoss(int playerId, int amount, string reason)
    {
        if (amount >= 0 || _resolvingFocusEffect || !_state.Cards.Values.Any(c => Definition(c).Effect == RenyuEffect)) return false;
        var command = _runningCommand ?? _attackCommandInResolution ?? _state.PendingAttack?.Command ?? PausedActionCommand();
        var targeted = command is null ? [] : ActionTargetIds(command).ToList();
        if (command is null && _state.PendingDuel is { } duel) targeted.Add(duel.ChallengedId);
        var renyu = targeted.Where(_state.Cards.ContainsKey).Select(Card).FirstOrDefault(c => Definition(c).Effect == RenyuEffect);
        if (renyu is null) return false;
        Emit("honorLossReduced", $"{Player(playerId).Name}'s Honor loss from {reason} was reduced to 0 ({Definition(renyu).Name} was targeted by the action).", playerId, renyu.Id);
        return true;
    }

    private GameCommand? PausedActionCommand() => _state.PendingChoice?.Type switch
    {
        "eitherOr" => _state.PendingEitherOr?.Command,
        "deciderChoice" => _state.PendingDeciderChoice?.Paused.Command,
        "opponentPayment" => _state.PendingOpponentPayment?.Paused.Command,
        "effectChoice" => _state.PendingEffectChoice?.Command,
        { } type when IsSearchChoice(type) => _state.PendingSearch?.Command,
        _ => null
    };

    // ---- Otomo Suikihime: standing traits and the leaves-play watch ----------------------------------------------------------------------------------------------

    private bool IsSuikihime(CardInstance card) => Definition(card).Effect == SuikihimeEffect;

    // "You may not assign, and will not move, Suikihime to battlefields."
    private bool SuikihimeMovementLocked(CardInstance card, MovementKinds kind) =>
        (kind & (MovementKinds.Assign | MovementKinds.AssignAttack | MovementKinds.ToBattlefield)) != 0 && IsSuikihime(card);

    // "Suikihime will not become dishonorable."
    private bool SuikihimeRefusesDishonor(CardInstance card)
    {
        if (!IsSuikihime(card) || card.Dishonorable) return false;
        Emit("dishonorNegated", $"{Definition(card).Name} will not become dishonorable.", card.OwnerId, card.Id);
        return true;
    }

    // "Other players' Political actions may not target Suikihime."
    private void RequireSuikihimeNotPoliticallyTargeted(GameCommand command, AbilityDefinition ability)
    {
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        if (!ActionCarriesKeyword(ability, source, "Political")) return;
        foreach (var targetId in ActionTargetIds(command))
            if (_state.Cards.TryGetValue(targetId, out var target) && IsSuikihime(target) && target.OwnerId != command.PlayerId)
                throw new RuleViolation($"Other players' Political actions may not target {Definition(target).Name}.");
    }

    private void CheckEmperorP6LeavesPlayWatches()
    {
        foreach (var watch in P6.LeavesPlayWatches.ToArray())
        {
            if (_state.Cards.TryGetValue(watch.CardId, out var card) && InPlay(card)) continue;
            P6.LeavesPlayWatches.Remove(watch);
            var player = Player(watch.PlayerId);
            if (player.Provinces.Where(p => !p.Destroyed).OrderBy(p => p.Index).FirstOrDefault() is not { } leftmost) continue;
            Emit("leavesPlayWatch", $"{watch.Source} left play: {player.Name}'s leftmost province is destroyed.", player.Id, watch.CardId);
            DestroyProvince(player.Id, leftmost);
        }
    }

    // ---- Illusory Defense: "Your Air Shugenja have +1C." ---------------------------------------------------------------------------------------------------------------

    private int EmperorP6ChiBonus(CardInstance card) =>
        Keyword(card, "Air") && Keyword(card, "Shugenja")
            ? _state.Cards.Values.Count(c => c.OwnerId == card.OwnerId && c.Zone == Zone.Attached && Definition(c).Effect == IllusoryDefenseEffect) : 0;

    // ---- Incredible Tragedy -----------------------------------------------------------------------------------------------------------------------------------------

    // "Until your second turn from now begins, Personalities may not be assigned during the Cavalry Maneuvers Segment."
    private bool CavalryAssignmentBarredP6() => _state.Cards.Values.Any(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == IncredibleTragedyEffect);

    // ---- the Events ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsEmperorP6Event(string effect) => effect is IncredibleTragedyEffect or InSearchOfGuidanceEffect or DarkExperimentsEffect or DistantExpansionEffect;

    // Returns true when the reveal paused (a search, a choice of province).
    private bool ResolveEmperorP6Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            case IncredibleTragedyEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s second turn from now begins: Personalities may not be assigned during the Cavalry Maneuvers Segment.", turnBeginsIn: 2);
                return false;
            case DistantExpansionEffect:
                // "Until your third turn from now ends": it leaves play as his fourth turn from now begins (the trait only acts in his own Dynasty Phases, none of which lies between).
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s third turn from now ends: in their Dynasty Phase they may refill a province face-up once for each of their destroyed provinces.", turnBeginsIn: 4);
                return false;
            case InSearchOfGuidanceEffect:
                BeginSearch(EmperorP6Searches.InSearchOfGuidance, card, player.Id, "event");
                return true;
            case DarkExperimentsEffect:
            {
                var candidates = _state.Players.SelectMany(p => p.Provinces.Where(q => !q.Destroyed).Select(q => $"{p.Id}:{q.Index}")).ToList();
                if (candidates.Count == 0) { FinishRevealedEvent(card); return true; }
                OpenEventChoice(new PendingEventChoice { Kind = "p6-dark", EventCardId = card.Id, PlayerId = player.Id, Origin = "reveal", Candidates = candidates,
                    Prompt = $"{Definition(card).Name}: choose a province. It permanently has -2 strength and its Region is destroyed; until the game ends a Region attaching to it is destroyed and a Holding entering play from it gets a -1 Gold Production token." });
                return true;
            }
        }
        return false;
    }

    // In Search of Guidance: the found Sensei is held (out of the deck) until the Event leaves its province, then refills it face-up.
    private void HoldSearchedRefill(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        player.DynastyDeck.Remove(card.Id);
        P6.SearchedRefills[search.SourceCardId] = card.Id;
    }

    // ResumeEventAfterDeferral's refill: true when the province was refilled with the Sensei In Search of Guidance found.
    private bool RefillWithSearchedSensei(PlayerState player, ProvinceState province, CardInstance eventCard)
    {
        if (!P6.SearchedRefills.Remove(eventCard.Id, out var senseiId)) return false;
        var sensei = Card(senseiId);
        if (province.Destroyed || province.CardId is not null || province.HoldsNoDynastyCards)
        {
            sensei.Zone = Zone.DynastyDiscard; sensei.FaceUp = true; sensei.ProvinceIndex = null;
            Emit("provinceDiscard", $"{Definition(sensei).Name} could not refill the province and was discarded.", player.Id, sensei.Id);
            return false;
        }
        ResetCardState(sensei);
        sensei.Zone = Zone.Province; sensei.ProvinceIndex = province.Index; sensei.FaceUp = true; sensei.Bowed = false;
        province.CardId = sensei.Id;
        Emit("provinceRefilled", $"{Definition(eventCard).Name}: {player.Name} refilled province {province.Index + 1} face up with {Definition(sensei).Name}.", player.Id, sensei.Id);
        AfterProvinceRefilledP6(player, province, faceUp: true);
        return true;
    }

    // Dark Experiments: the chosen province is marked for the rest of the game.
    private void ApplyDarkExperiments(CardInstance card, string choice)
    {
        var parts = choice.Split(':');
        var owner = Player(int.Parse(parts[0])); var province = owner.Provinces.Single(p => p.Index == int.Parse(parts[1]));
        province.Strength -= 2;
        province.DarkExperimentsMarked = true;
        Emit("eventChose", $"{Definition(card).Name}: {owner.Name}'s province {province.Index + 1} permanently has -2 strength and is marked until the game ends.", card.OwnerId, card.Id);
        if (province.RegionId is { } regionId) DestroyRegion(Card(regionId));
    }

    // "After a Region attaches to it, destroy the Region."
    private void AfterRegionAttachedP6(ProvinceState province, CardInstance region)
    {
        if (!province.DarkExperimentsMarked || province.RegionId != region.Id) return;
        Emit("darkExperiments", $"{Definition(region).Name} attached to a province marked by Dark Experiments and is destroyed.", region.OwnerId, region.Id);
        DestroyRegion(region);
    }

    // "After a Holding enters play from it, give the Holding a -1GP token."
    private void AfterHoldingEnteredFromProvinceP6(ProvinceState province, CardInstance holding)
    {
        if (!province.DarkExperimentsMarked || Definition(holding).Type != CardType.Holding) return;
        CreateToken(holding, "Dark Experiments", holding.Id, "GoldProduction", -1);
        Emit("darkExperiments", $"{Definition(holding).Name} entered play from a province marked by Dark Experiments: it has a -1 Gold Production token.", holding.OwnerId, holding.Id);
    }


    // ---- refills: Susaiken, Distant Expansion, Ietsuno ----------------------------------------------------------------------------------------------------------

    // The ways a player may refill one of his provinces face-up right now: "susaiken:<card>" (once per turn each) and "distant:<card>" (in his own Dynasty Phase, one use per destroyed province of his).
    private List<string> FaceUpRefillSources(PlayerState player)
    {
        var sources = new List<string>();
        foreach (var susaiken in _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && c.DefinitionId == SusaikenId).OrderBy(c => c.Id, StringComparer.Ordinal))
            if (!susaiken.AbilityUses.TryGetValue(SusaikenUseKey, out var turn) || turn != _state.TurnNumber) sources.Add($"susaiken:{susaiken.Id}");
        if (_state.Phase == Phase.Dynasty && _state.ActivePlayerId == player.Id
            && _state.Cards.Values.FirstOrDefault(c => c.OwnerId == player.Id && c.Zone == Zone.EventInPlay && Definition(c).Effect == DistantExpansionEffect) is { } distant
            && P6.DistantExpansionUses.Count(u => u == $"{player.Id}|{_state.TurnNumber}") < player.Provinces.Count(p => p.Destroyed))
            sources.Add($"distant:{distant.Id}");
        return sources;
    }

    // Every refill of a province from a deck reports here (faceUp: the refill already put the card face-up).
    private void AfterProvinceRefilledP6(PlayerState owner, ProvinceState province, bool faceUp)
    {
        if (province.CardId is not { } cardId) return;
        if (!faceUp && FaceUpRefillSources(owner).Count > 0) P6.Queue.Add(new("faceup", owner.Id, owner.Id, province.Index, cardId, ""));
        // Ietsuno: at a battlefield at this province (the Defender's), whoever controls him.
        if (owner.Id != 1 - _state.ActivePlayerId) return;
        foreach (var ietsuno in _state.Cards.Values.Where(c => Definition(c).Effect == IetsunoEffect && c.Zone == Zone.Battlefield && c.ProvinceIndex is int b && ReferenceEquals(BattlefieldProvince(b), province))
                     .OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var viewer = Player(ietsuno.OwnerId); var card = Card(cardId);
            if (!card.FaceUp) _state.ProvinceSights.Add(new(viewer.Id, owner.Id, province.Index, card.Id, card.DefinitionId, _state.TurnNumber));
            Emit("provinceLooked", $"{viewer.Name} looks at the card that refilled {owner.Name}'s province {province.Index + 1} ({Definition(ietsuno).Name}).", viewer.Id, ietsuno.Id);
            P6.Queue.Add(new("ietsuno", viewer.Id, owner.Id, province.Index, cardId, ietsuno.Id));
        }
    }

    // Apply's tail: the first queued decision that still makes sense opens once nothing else is open.
    private void OpenEmperorP6QueuedDecision()
    {
        CheckEmperorP6LeavesPlayWatches();
        while (_state.PendingChoice is null && _state.WinnerId is null && P6.Queue.Count > 0)
        {
            var next = P6.Queue[0]; P6.Queue.RemoveAt(0);
            var owner = Player(next.OwnerId);
            var province = owner.Provinces.FirstOrDefault(p => p.Index == next.Province);
            if (province is null || province.Destroyed || province.CardId != next.CardId) continue;
            var card = Card(next.CardId);
            List<string> candidates; string prompt; string source;
            if (next.Kind == "faceup")
            {
                if (card.FaceUp) continue;
                candidates = FaceUpRefillSources(owner);
                if (candidates.Count == 0) continue;
                source = candidates[0].Split(':')[1];
                prompt = $"Province {province.Index + 1} was refilled: you may refill it face-up instead ({string.Join(" or ", candidates.Select(c => Definition(Card(c.Split(':')[1])).Name))}).";
            }
            else
            {
                candidates = owner.Provinces.Where(p => p != province && !p.Destroyed && p.CardId is { } other && Card(other).OwnerId == card.OwnerId).Select(p => p.CardId!).ToList();
                if (candidates.Count == 0) continue;
                source = next.SourceId;
                prompt = $"{Definition(Card(source)).Name}: you may switch the card that refilled {owner.Name}'s province {province.Index + 1} with a card of theirs in another province.";
            }
            P6.ResumePriority = _state.PriorityPlayerId; P6.ResumePasses = _state.ConsecutivePasses;
            OpenEventChoice(new PendingEventChoice { Kind = next.Kind == "faceup" ? "p6-faceup" : "p6-ietsuno", EventCardId = source, PlayerId = next.PlayerId, Origin = "deferred",
                ClosesTo = _state.PriorityPlayerId, Candidates = candidates, Data = [next.OwnerId.ToString(), next.Province.ToString(), next.CardId], Prompt = prompt });
        }
    }

    // The answers of this group's decisions (EventChoice's default arm); false when the kind is not this group's.
    private bool ResolveEmperorP6Choice(PendingEventChoice pending, GameCommand command)
    {
        switch (pending.Kind)
        {
            case "p6-dark":
                Require(command.Choice is not null && pending.Candidates.Contains(command.Choice), "Choose a province.");
                _state.PendingChoice = null; _state.PendingEventChoice = null;
                ApplyDarkExperiments(Card(pending.EventCardId), command.Choice!);
                FinishRevealedEvent(Card(pending.EventCardId));
                return true;
            case "p6-faceup":
            {
                Require(command.Choice == "no" || command.Choice is not null && pending.Candidates.Contains(command.Choice), "Choose whether to refill the province face-up.");
                CloseEmperorP6Choice();
                var owner = Player(int.Parse(pending.Data[0])); var province = owner.Provinces.Single(p => p.Index == int.Parse(pending.Data[1])); var card = Card(pending.Data[2]);
                if (command.Choice == "no") { Emit("refillFaceDownKept", $"{owner.Name} left province {province.Index + 1}'s refill face down.", owner.Id); return true; }
                var parts = command.Choice!.Split(':'); var source = Card(parts[1]);
                if (parts[0] == "susaiken") source.AbilityUses[SusaikenUseKey] = _state.TurnNumber;
                else P6.DistantExpansionUses.Add($"{owner.Id}|{_state.TurnNumber}");
                card.FaceUp = true;
                Emit("provinceRefilled", $"{Definition(source).Name}: {owner.Name}'s province {province.Index + 1} was refilled face up with {Definition(card).Name}.", owner.Id, card.Id);
                return true;
            }
            case "p6-ietsuno":
            {
                Require(command.Choice == "none" || command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose a card to switch with, or none.");
                CloseEmperorP6Choice();
                if (command.Choice == "none" || command.TargetId is null) { Emit("ietsunoKept", $"{Definition(Card(pending.EventCardId)).Name}: nothing was switched.", pending.PlayerId); return true; }
                var owner = Player(int.Parse(pending.Data[0])); var here = owner.Provinces.Single(p => p.Index == int.Parse(pending.Data[1]));
                var refilled = Card(pending.Data[2]); var other = Card(command.TargetId);
                var there = owner.Provinces.Single(p => p.CardId == other.Id);
                here.CardId = other.Id; other.ProvinceIndex = here.Index;
                there.CardId = refilled.Id; refilled.ProvinceIndex = there.Index;
                if (!refilled.FaceUp) _state.ProvinceSights.Add(new(pending.PlayerId, owner.Id, there.Index, refilled.Id, refilled.DefinitionId, _state.TurnNumber));
                Emit("provinceCardsSwitched", $"{Definition(Card(pending.EventCardId)).Name}: the cards in {owner.Name}'s provinces {here.Index + 1} and {there.Index + 1} were switched.", pending.PlayerId, pending.EventCardId);
                return true;
            }
        }
        return false;
    }

    private void CloseEmperorP6Choice()
    {
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        _state.PriorityPlayerId = P6.ResumePriority; _state.ConsecutivePasses = P6.ResumePasses;
    }

    private IEnumerable<ActionOption> EmperorP6ChoiceActions(PendingEventChoice pending, int playerId)
    {
        var name = Definition(Card(pending.EventCardId)).Name;
        GameCommand Answer(string? target = null, string? choice = null) => new("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: target, Choice: choice);
        switch (pending.Kind)
        {
            case "p6-dark":
                foreach (var key in pending.Candidates)
                {
                    var parts = key.Split(':'); var owner = int.Parse(parts[0]); var index = int.Parse(parts[1]);
                    yield return new("eventChoice", $"{name}: {Player(owner).Name}'s province {index + 1}", true, null, Answer(choice: key), pending.EventCardId, Provinces: [index]);
                }
                break;
            case "p6-faceup":
                foreach (var key in pending.Candidates)
                    yield return new("eventChoice", $"Refill it face-up ({Definition(Card(key.Split(':')[1])).Name})", true, null, Answer(choice: key), key.Split(':')[1]);
                yield return new("eventChoice", "Leave it face down", true, null, Answer(choice: "no"), pending.EventCardId);
                break;
            case "p6-ietsuno":
                foreach (var id in pending.Candidates)
                    yield return new("eventChoice", $"{name}: switch it with the card in province {Card(id).ProvinceIndex + 1}", true, null, Answer(target: id), id);
                yield return new("eventChoice", $"{name}: do not switch", true, null, Answer(choice: "none"), pending.EventCardId);
                break;
        }
    }

    // ---- gaining a province to the left: every other record keyed by one of the player's province indexes -----------------------------------------------------

    private void ShiftProvinceKeysForGainedLeftProvince(PlayerState player)
    {
        static int Up(int index) => index is >= 0 and < FirstAdditionalBattlefield ? index + 1 : index;
        _state.ProvinceStrengthEffects = _state.ProvinceStrengthEffects.Select(e => e.PlayerId == player.Id && e.ProvinceIndex is int i ? e with { ProvinceIndex = i + 1 } : e).ToList();
        _state.PreservingBeautyChecks = _state.PreservingBeautyChecks.Select(c => c.ProvinceOwnerId == player.Id ? c with { ProvinceIndex = c.ProvinceIndex + 1 } : c).ToList();
        _state.ConsecrationWatches = _state.ConsecrationWatches.Select(w => w.PlayerId == player.Id ? w with { Provinces = w.Provinces.Select(i => i + 1).ToArray() } : w).ToList();
        if (_state.LastProvinceEntryCardId is { } entered && _state.Cards.TryGetValue(entered, out var enteredCard) && enteredCard.OwnerId == player.Id && _state.LastProvinceEntryProvinceIndex is int last)
            _state.LastProvinceEntryProvinceIndex = last + 1;
        foreach (var queued in P6.Queue.Where(q => q.OwnerId == player.Id).ToArray()) P6.Queue[P6.Queue.IndexOf(queued)] = queued with { Province = queued.Province + 1 };
        // A battle at his provinces: he is the Defender (the Attacker is ActivePlayerId, swapped for an additional Attack Phase or a battle inside the Action Phase).
        if (player.Id != 1 - _state.ActivePlayerId) return;
        foreach (var card in _state.Cards.Values.Where(c => c.ProvinceIndex is int b && b < FirstAdditionalBattlefield && (c.Zone == Zone.Battlefield || c.Zone == Zone.Attached && c.AttachedTo is string host && Card(host).Zone == Zone.Battlefield)))
            card.ProvinceIndex = Up(card.ProvinceIndex!.Value);
        if (_state.CurrentBattleProvince is int current) _state.CurrentBattleProvince = Up(current);
        _state.UnresolvedBattlefields = _state.UnresolvedBattlefields.Select(Up).ToList();
        if (_state.ResolvedBattleProvinceIndex is int resolved) _state.ResolvedBattleProvinceIndex = Up(resolved);
        if (_state.PendingEngage is { } engage) engage.ProvinceIndex = Up(engage.ProvinceIndex);
        if (_state.PendingBeforeBattleResolve is { } before) before.ProvinceIndex = Up(before.ProvinceIndex);
        if (_state.PendingAfterResolution is { } after) after.ProvinceIndex = Up(after.ProvinceIndex);
        if (_state.PendingShadowCourtResolution is { } court) court.ProvinceIndex = Up(court.ProvinceIndex);
        if (_state.PendingConqueror is { } conqueror) conqueror.ProvinceIndex = Up(conqueror.ProvinceIndex);
        foreach (var key in R1.Additional.Keys.ToArray()) if (R1.Additional[key] >= 0) R1.Additional[key]++;
        R1.TwoFronts = R1.TwoFronts.Select(m => m with { Province = Up(m.Province) }).ToList();
        _state.TorchWatches = _state.TorchWatches.Select(w => w with { ProvinceIndex = Up(w.ProvinceIndex) }).ToList();
        _state.HiddenFallsGrants = _state.HiddenFallsGrants.Select(g => g with { Provinces = g.Provinces.Select(Up).ToArray() }).ToList();
        foreach (var delayed in _state.DelayedEffects.Where(d => d.Kind == DelayedKinds.MoveToBattlefield && int.TryParse(d.Arg, out _)))
            delayed.Arg = Up(int.Parse(delayed.Arg!)).ToString();
    }
}

public static class EmperorP6Specs
{
    /// <summary>Kuni Renyu: the target unit's controller may bow it and destroy all its Shadowlands cards; if he did not, the acting player creates a province (EmperorP6.cs).</summary>
    public const string RenyuPurge = "renyu-purge";
}

public static class EmperorP6Searches
{
    /// <summary>In Search of Guidance (oracle-l5r-3723): "Search your Dynasty deck for a Sensei Personality. Refill this province with him, face-up."</summary>
    public static SearchSpec InSearchOfGuidance { get; } = new("in-search-of-guidance", "Search your Dynasty deck for a Sensei Personality",
        [new(SearchDeck.Dynasty, new("Sensei Personality", [CardType.Personality], ["Sensei"]), SearchEffect.RefillSourceProvinceFaceUp)]);

    public static IEnumerable<SearchSpec> All => [InSearchOfGuidance];
}

// This property keeps its original JSON name and tail position in old saved MatchState payloads.
public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1006)]
    public EmperorP6State P6 { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP6Hooks()
    {
        EngineHooks.Abilities.Register("AddEmperorP6", 3100, static abilities => abilities.AddEmperorP6());
        EngineHooks.Searches.Register("EmperorP6Searches", 400, static () => EmperorP6Searches.All);
        EngineHooks.TargetAllows.Register("EmperorP6TargetAllows", 1600, static (engine, context) => engine.EmperorP6TargetAllows(context.ability.Target, context.player, context.target));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorP6", 1500, static (engine, context) => engine.ValidateEmperorP6(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorP6", 2500, static (engine, context) => engine.RunEmperorP6(context.id, context.command, context.player, context.source, context.target, context.bowedBeforeAction));
        EngineHooks.EitherOrPrompt.Register("EmperorP6EitherOrPrompt", 300, static (engine, p, source, affected) => engine.EmperorP6EitherOrPrompt(p, source, affected));
        EngineHooks.EitherOrOptions.Register("EmperorP6EitherOrOptions", 300, static (engine, p, affected, source, payable) => engine.EmperorP6EitherOrOptions(p, affected));
        EngineHooks.ApplyEitherOr.Register("ApplyEmperorP6EitherOr", 300, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyEmperorP6EitherOr(p, option, source, decider));
    }
}
