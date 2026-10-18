using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #827: six cards Mendon's rulings of 2026-10-11 unblocked (docs/decisions/2026-10-11-koan-panku-kokujin.md; rulings R-049..R-053 in docs/rules/RULINGS.md). Ability ids are "w827-".
//
//   Kokujin - inexperienced  "After Kokujin enters play, lose 6 Honor unless you are a Spider Clan player. / Battle: Kokujin challenges a target enemy Personality. Destroy the duel's loser or, if
//                            Kokujin wins, you may destroy one of your Personalities (at any location) to take control of the loser (he joins Kokujin's army)."   w827-kokujin-duel, an Iaijutsu-less
//                            duel; when Kokujin wins the engine asks "Destroy a personality to take control of the loser?" (pending type w827KokujinChoice) before the outcome applies. Not a Reaction
//                            window: nothing can negate or react to the question.
//   P'an Ku - exp            "Open, bow: Give a target card a Madness token. / Limited, bow: P'an Ku may remain bowed. Take control of a target Personality with more Madness tokens than printed
//                            Personal Honor and remove those tokens. The target will not commit seppuku until P'an Ku straightens or leaves play. After P'an Ku does, give control of the target to his
//                            owner."   a ControlReturn (EmperorMechB.cs) with two flags: RemainBowed (every straightening of P'an Ku, the Straighten Phase's or an effect's, is optional while it holds)
//                            and NoSeppuku (the controller cannot order Seppuku for the taken Personality).
//   Zansho - exp             "Zansho will not join a Spider Clan player. / Battle: Bow Zansho's target unbowed Follower. Ranged Attack with strength equal to its Force, plus 2 if the Ranged Attack targets
//                            Daigotsu Arakan."   the Follower is one attached to Zansho himself (Mendon's ruling); the bow is the cost (CostSubject.Chosen).
//   Chiyurei's Axe          "Attaches to Hida Chiyurei paying 2 less Gold. / Melee Attacks this Personality performs may be compared against the target's base Force."   AttachRequiresName, an Equip
//                            cost term; the comparison is automatic: a Melee Attack Chiyurei performs compares the lower of the target's current and printed Force (AttackComparedValue).
//   Koan's Jingasa          "This Personality has Shugenja, may not have more Spells than Koan cards attached, and he need not bow for his Spells with an odd Focus Value (0 is even)."
//   Koan's Robes            "This Personality has a Force bonus equal to the number of other Koan cards in this unit. ... need not bow for his Spells with an even Focus Value. Negate the first destruction
//                            each turn of cards in this unit from another player's card."   a WatchScope.Unit standing watch (PeriodWatches.cs).
public static class Wave827Abilities
{
    public const string KokujinDuel = "w827-kokujin-duel", PanKuMadness = "w827-panku-madness", PanKuTake = "w827-panku-take", ZanshoRanged = "w827-zansho-ranged";
    public const string KokujinType = "w827KokujinChoice", KokujinCommand = "w827KokujinChoose", StraightenType = "w827PanKuStraighten", StraightenCommand = "w827PanKuStraightenChoose",
        KoanType = "w827KoanDiscard", KoanCommand = "w827KoanDiscardChoose";
    public const string Kokujin = "oracle-l5r-10486", PanKu = "oracle-l5r-10497", Zansho = "oracle-l5r-10505", Axe = "oracle-l5r-10340", Jingasa = "oracle-l5r-10524", Robes = "oracle-l5r-10525";
    public const string KokujinEffect = "w827-kokujin", AxeEffect = "w827-chiyurei-axe", JingasaEffect = "w827-koan-jingasa", RobesEffect = "w827-koan-robes";
    public const string KokujinPrompt = "Destroy a personality to take control of the loser?";
    public const string Decline = "decline", Straighten = "straighten", Remain = "remain";

    public static Dictionary<string, AbilityDefinition> AddWave827(this Dictionary<string, AbilityDefinition> all)
    {
        all[KokujinDuel] = new("Battle", "Battle: Kokujin challenges a target enemy Personality. Destroy the duel's loser or, if Kokujin wins, you may destroy one of your Personalities (at any location) to take control of the loser",
            "enemy-personality", IssuesDuel: true);
        all[PanKuMadness] = new("Open", "Open, bow: Give a target card a Madness token", "any-card", Costs: ActionCost.BowSource);
        all[PanKuTake] = new("Limited", "Limited, bow: P'an Ku may remain bowed. Take control of a target Personality with more Madness tokens than printed Personal Honor and remove those tokens. The target will not commit seppuku until P'an Ku straightens or leaves play",
            Wave821Abilities.MadnessPersonality, Costs: ActionCost.BowSource);
        all[ZanshoRanged] = new("Battle", "Battle: Bow Zansho's target unbowed Follower. Ranged Attack with strength equal to its Force, plus 2 if the Ranged Attack targets Daigotsu Arakan", "attack",
            Costs: [new(CostVerb.Bow, CostSubject.Chosen)]);
        return all;
    }
}

public sealed class Wave827State
{
    /// <summary>The duel whose Kokujin question has been asked (the duel resolves again once it is answered, and does not ask twice).</summary>
    public string? KokujinAskedDuel { get; set; }
    /// <summary>The Personality the answer destroys to take control of the loser; null when the controller declined.</summary>
    public string? KokujinSacrifice { get; set; }
    /// <summary>P'an Ku cards whose straightening by an effect waits for their controller's answer.</summary>
    public List<string> StraightenOffers { get; set; } = [];
    /// <summary>The priority and pass count a decision opened after a command took over, restored when it is answered.</summary>
    public int? ResumePriority { get; set; }
    public int ResumePasses { get; set; }
}

public sealed partial class MatchState
{
    // 23400-23499 is reserved for issue #827.
    [JsonPropertyOrder(23400)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Wave827State? Wave827 { get; set; }
}

public sealed partial class MatchEngine
{
    private Wave827State W827 => _state.Wave827 ??= new();
    /// <summary>True while a P'an Ku straightening that its controller allowed is being applied (it is not offered again), and while the Straighten Phase straightens (its choice was the phase's own).</summary>
    private bool _w827StraightenAllowed, _w827StraightenPhase;

    // ---- entry: Kokujin ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "After Kokujin enters play, lose 6 Honor unless you are a Spider Clan player." A trait without a designator, so mandatory like every other entry Honor loss.
    private void ApplyWave827EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != Wave827Abilities.KokujinEffect || Player(playerId).Clan == "Spider") return;
        GainHonor(playerId, -6, $"{Definition(card).Name} entering play", card);
    }

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave827Validate(ValidationHookContext context)
    {
        var (id, command, player, source) = (context.id, context.command, context.player, context.source);
        switch (id)
        {
            // "Bow Zansho's target unbowed Follower": one of the Followers attached to Zansho himself (Mendon's ruling), unbowed; it is bowed as the cost.
            case Wave827Abilities.ZanshoRanged:
                Require(ChosenCostCard(command) is { } follower && source is not null && ZanshoFollowers(source).Any(f => f.Id == follower.Id && !f.Bowed), "Choose an unbowed Follower attached to Zansho to bow.");
                break;
            // Another player's Personality: control of one's own would not change.
            case Wave827Abilities.PanKuTake:
                Require(command.TargetId is null || !_state.Cards.TryGetValue(command.TargetId, out var taken) || taken.OwnerId != player.Id, "Target another player's Personality.");
                break;
        }
    }

    private IEnumerable<CardInstance> ZanshoFollowers(CardInstance zansho) =>
        Attachments(zansho.Id).Where(a => Definition(a).Type == CardType.Follower && InPlay(a)).OrderBy(a => a.Id, StringComparer.Ordinal);

    private static bool Wave827CarriesCostCard(string abilityId) => abilityId == Wave827Abilities.ZanshoRanged;
    private string?[]? Wave827CostCardChoices(string abilityId, CardInstance? source) => abilityId != Wave827Abilities.ZanshoRanged ? null
        : source is null ? [null] : ZanshoFollowers(source).Where(f => !f.Bowed).Select(f => (string?)f.Id).DefaultIfEmpty(null).ToArray();
    private static string? Wave827CostCardVerb(string abilityId) => abilityId == Wave827Abilities.ZanshoRanged ? "bows" : null;

    // ---- effects -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave827(RunHookContext c)
    {
        var (id, command, player, source, target) = (c.id, c.command, c.player, c.source, c.target);
        switch (id)
        {
            case Wave827Abilities.PanKuMadness:
                CreateToken(target!, "Madness", id);
                return false;
            case Wave827Abilities.PanKuTake:
                {
                    var name = Definition(source!).Name;
                    if (!TransferUnitControl(target!, player.Id, name, checkLoyal: true, fromEffect: true)) return false;
                    target!.Tokens.RemoveAll(t => t.Type == "Madness");
                    MechB.ControlReturns.Add(new ControlReturn
                    {
                        Seq = ++MechB.ControlReturnSequence,
                        CardId = target.Id,
                        Trigger = ControlReturnTriggers.SourceStraightens,
                        SourceId = source!.Id,
                        Reason = $"{name}: after he straightens or leaves play",
                        SourceSeenBowed = InPlay(source) && source.Bowed,
                        RemainBowed = true,
                        NoSeppuku = true
                    });
                    Emit("panKuControl", $"{name} may remain bowed; {Definition(target).Name} will not commit seppuku until {name} straightens or leaves play.", player.Id, target.Id);
                    return false;
                }
            // The Follower was bowed as the cost: the strength is its Force, +2 against Daigotsu Arakan.
            case Wave827Abilities.ZanshoRanged:
                {
                    var follower = ChosenCostCard(command);
                    var strength = (follower is null ? 0 : CardForce(follower)) + (target is not null && Definition(target).Name == "Daigotsu Arakan" ? 2 : 0);
                    return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, c.consumedGrant, strength);
                }
            default:
                return null;
        }
    }

    // ---- P'an Ku: remaining bowed, and the seppuku bar -----------------------------------------------------------------------------------------------------------------------------------

    private bool PanKuMayRemainBowed(CardInstance card) =>
        card.Bowed && InPlay(card) && MechB.ControlReturns.Any(r => r.RemainBowed && r.SourceId == card.Id && r.Trigger == ControlReturnTriggers.SourceStraightens);

    // EligibleForOptionalStraighten's addition: the Straighten Phase's straightening of P'an Ku is the controller's choice while he holds a Personality.
    private bool PanKuMayStayBowedInPhase(CardInstance card) => PanKuMayRemainBowed(card) && card.Zone is Zone.Home or Zone.Stronghold or Zone.Attached && card.StraightenBlockedTurn != _state.TurnNumber;

    // "The target will not commit seppuku until P'an Ku straightens or leaves play": the controller may not order it.
    private bool SeppukuBarredByControl(CardInstance card) => MechB.ControlReturns.Count > 0 && MechB.ControlReturns.Any(r => r.NoSeppuku && r.CardId == card.Id);

    // Straighten's interception for an effect's straightening: queued, and offered to the controller once the command is done. False when it is not P'an Ku, or the choice was already made.
    private bool Wave827DefersStraighten(CardInstance card)
    {
        if (_w827StraightenAllowed || _w827StraightenPhase || !PanKuMayRemainBowed(card) || CurrentActionUnnegatable()) return false;
        if (!W827.StraightenOffers.Contains(card.Id)) W827.StraightenOffers.Add(card.Id);
        return true;
    }

    // ---- Kokujin: the question after the duel -----------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> KokujinCandidates(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal);

    // ResolveDuel's hook, after the delay decisions and before the outcome applies: when Kokujin wins, his controller is asked once. True when the question opened (the duel resolves again once it is answered).
    private bool Wave827BeginKokujinDecision(PendingDuel duel, string? winnerId)
    {
        if (duel.AbilityId != Wave827Abilities.KokujinDuel || winnerId is null || winnerId != duel.ChallengerId || W827.KokujinAskedDuel == duel.DuelId) return false;
        var kokujin = Card(winnerId); var loser = Card(duel.ChallengedId);
        W827.KokujinAskedDuel = duel.DuelId; W827.KokujinSacrifice = null;
        // The loser cannot be controlled by the winner (Loyal): there is nothing to ask, the loser is destroyed.
        if (!InPlay(loser) || !LoyaltyAllowsControl(loser, Player(kokujin.OwnerId).Clan, kokujin.OwnerId)) return false;
        _state.PendingChoice = new(Wave827Abilities.KokujinType, kokujin.OwnerId, kokujin.Id, Wave827Abilities.KokujinPrompt);
        _state.PriorityPlayerId = kokujin.OwnerId; _state.ConsecutivePasses = 0;
        Emit("kokujinChoice", $"{Definition(kokujin).Name} won the duel against {Definition(loser).Name}: {Player(kokujin.OwnerId).Name} may destroy one of their Personalities to take control of him.", kokujin.OwnerId, kokujin.Id);
        return true;
    }

    private IEnumerable<ActionOption> Wave827KokujinActions(int playerId)
    {
        var pending = _state.PendingChoice!;
        if (pending.PlayerId != playerId) return [];
        var duel = _state.PendingDuel;
        var loserName = duel is null ? "the loser" : Definition(Card(duel.ChallengedId)).Name;
        var options = KokujinCandidates(playerId).Select(c => new ActionOption(Wave827Abilities.KokujinCommand, $"Destroy {Definition(c).Name}", true, null,
            new GameCommand(Wave827Abilities.KokujinCommand, playerId, _state.Version, TargetId: c.Id), c.Id, DecisionCandidateIds: [c.Id], DecisionLabel: $"Destroy {Definition(c).Name} to take control of {loserName}")).ToList();
        options.Add(new ActionOption(Wave827Abilities.KokujinCommand, $"Do not destroy a Personality (destroy {loserName})", true, null,
            new GameCommand(Wave827Abilities.KokujinCommand, playerId, _state.Version, Choice: Wave827Abilities.Decline)));
        return options;
    }

    private void Wave827AnswerKokujin(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == Wave827Abilities.KokujinCommand && command.PlayerId == pending.PlayerId, "Answer the question: destroy a Personality to take control of the loser?");
        var duel = _state.PendingDuel ?? throw new RuleViolation("The duel is no longer pending.");
        if (command.Choice == Wave827Abilities.Decline)
        {
            W827.KokujinSacrifice = null;
            Emit("kokujinChosen", $"{Player(command.PlayerId).Name} will not destroy a Personality: {Definition(Card(duel.ChallengedId)).Name} is destroyed.", command.PlayerId, pending.CardId);
        }
        else
        {
            Require(command.TargetId is not null && KokujinCandidates(command.PlayerId).Any(c => c.Id == command.TargetId), "Choose one of your Personalities in play.");
            W827.KokujinSacrifice = command.TargetId;
            Emit("kokujinChosen", $"{Player(command.PlayerId).Name} will destroy {Definition(Card(command.TargetId!)).Name} to take control of {Definition(Card(duel.ChallengedId)).Name}.", command.PlayerId, pending.CardId);
        }
        _state.PendingChoice = null; _state.ConsecutivePasses = 0;
        ResolveDuel();
    }

    // ApplyDuelOutcome's case: "Destroy the duel's loser or, if Kokujin wins, you may destroy one of your Personalities to take control of the loser (he joins Kokujin's army)". A tie has two losers: both are destroyed.
    private void Wave827KokujinOutcome(string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        var sacrificeId = W827.KokujinSacrifice;
        W827.KokujinSacrifice = null; W827.KokujinAskedDuel = null;
        var kokujinWon = winnerId is not null && Card(winnerId).OwnerId == challengerPlayerId;
        if (kokujinWon && sacrificeId is not null && loserIds.Count == 1 && _state.Cards.TryGetValue(sacrificeId, out var sacrifice) && InPlay(sacrifice))
        {
            var loser = Card(loserIds[0]);
            var name = Definition(winnerId is null ? sacrifice : Card(winnerId)).Name;
            DestroyUnits([sacrifice], actingPlayerId: challengerPlayerId);
            if (InPlay(sacrifice)) { Emit("kokujinNoControl", $"{name}: {Definition(sacrifice).Name} was not destroyed, so control of {Definition(loser).Name} is not taken.", challengerPlayerId, loser.Id); return; }
            if (InPlay(loser)) TakeControlOfPersonality(loser, challengerPlayerId, name);
            return;
        }
        DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId);
    }

    // ---- Koan: the Spell limit, the Robes' bonus ---------------------------------------------------------------------------------------------------------------------------------------

    private int KoanCardsInUnit(CardInstance host) => (Keyword(host, "Koan") ? 1 : 0) + Attachments(host.Id).Count(a => Keyword(a, "Koan"));

    // "May not have more Spells than Koan cards attached": the first Personality carrying a Jingasa with more Spells attached than Koan cards.
    private (CardInstance Host, int Limit, CardInstance[] Spells)? Wave827KoanExcess()
    {
        if (!_state.Cards.Values.Any(c => c.Zone == Zone.Attached && Definition(c).Effect == Wave827Abilities.JingasaEffect)) return null; // run after every command: skip the walk when no Jingasa is in play
        foreach (var host in _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var attached = Attachments(host.Id).ToArray();
            if (!attached.Any(a => Definition(a).Effect == Wave827Abilities.JingasaEffect)) continue;
            var limit = KoanCardsInUnit(host);
            var spells = attached.Where(a => Definition(a).Type == CardType.Spell).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
            if (spells.Length > limit) return (host, limit, spells);
        }
        return null;
    }

    private int Wave827Force(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || !InPlay(card)) return 0;
        var robes = Attachments(card.Id).FirstOrDefault(a => Definition(a).Effect == Wave827Abilities.RobesEffect);
        return robes is null ? 0 : KoanCardsInUnit(card);
    }

    private IEnumerable<ActionOption> Wave827KoanActions(int playerId)
    {
        var pending = _state.PendingChoice!;
        if (pending.PlayerId != playerId) return [];
        if (Wave827KoanExcess() is not { } excess) return [];
        return excess.Spells.Select(s => new ActionOption(Wave827Abilities.KoanCommand, $"Discard {Definition(s).Name}", true, null,
            new GameCommand(Wave827Abilities.KoanCommand, playerId, _state.Version, TargetId: s.Id), s.Id, DecisionCandidateIds: [s.Id],
            DecisionLabel: $"Discard {Definition(s).Name} ({excess.Spells.Length - excess.Limit} too many)"));
    }

    private void Wave827AnswerKoan(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == Wave827Abilities.KoanCommand && command.PlayerId == pending.PlayerId, "Choose a Spell to discard.");
        var excess = Wave827KoanExcess();
        Require(excess is not null && command.TargetId is not null && excess.Value.Spells.Any(s => s.Id == command.TargetId), "Choose one of the Spells attached to the Personality.");
        var spell = Card(command.TargetId!);
        _state.PendingChoice = null;
        DiscardAttachmentFromPlay(spell, $"{Definition(excess!.Value.Host).Name} may not have more Spells than Koan cards attached");
        Wave827Resume();
    }

    // ---- P'an Ku: the straightening choice ------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<ActionOption> Wave827StraightenActions(int playerId)
    {
        var pending = _state.PendingChoice!;
        if (pending.PlayerId != playerId || pending.CardId is null) return [];
        var name = Definition(Card(pending.CardId)).Name;
        return [
            new ActionOption(Wave827Abilities.StraightenCommand, $"Straighten {name}", true, null, new GameCommand(Wave827Abilities.StraightenCommand, playerId, _state.Version, Choice: Wave827Abilities.Straighten), pending.CardId,
                DecisionCandidateIds: [pending.CardId], DecisionLabel: $"Straighten {name} (control of the Personality he took returns to its owner)"),
            new ActionOption(Wave827Abilities.StraightenCommand, $"Leave {name} bowed", true, null, new GameCommand(Wave827Abilities.StraightenCommand, playerId, _state.Version, Choice: Wave827Abilities.Remain), pending.CardId,
                DecisionCandidateIds: [pending.CardId], DecisionLabel: $"Leave {name} bowed")];
    }

    private void Wave827AnswerStraighten(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == Wave827Abilities.StraightenCommand && command.PlayerId == pending.PlayerId && command.Choice is Wave827Abilities.Straighten or Wave827Abilities.Remain, "Straighten the card or leave it bowed.");
        var card = Card(pending.CardId!);
        _state.PendingChoice = null;
        W827.StraightenOffers.Remove(card.Id);
        if (command.Choice == Wave827Abilities.Straighten)
        {
            _w827StraightenAllowed = true;
            try { Straighten(card); } finally { _w827StraightenAllowed = false; }
        }
        else Emit("straightenDeclined", $"{Definition(card).Name} was left bowed by choice.", card.OwnerId, card.Id);
        Wave827Resume();
    }

    // ---- decisions opened after a command ---------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave827Open(string type, int playerId, string cardId, string prompt)
    {
        W827.ResumePriority = _state.PriorityPlayerId; W827.ResumePasses = _state.ConsecutivePasses;
        _state.PendingChoice = new(type, playerId, cardId, prompt);
        _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
    }

    private void Wave827Resume()
    {
        if (W827.ResumePriority is int priority) { _state.PriorityPlayerId = priority; _state.ConsecutivePasses = W827.ResumePasses; }
        W827.ResumePriority = null;
    }

    // Run once after every command (MatchEngine.Execute): a Personality with more Spells than Koan cards discards down to the count, and a P'an Ku an effect straightened is offered to its controller.
    private void Wave827AfterCommand()
    {
        if (_state.WinnerId is not null || _state.PendingChoice is not null || _state.PendingDuel is not null) return;
        if (_state.Wave827 is { StraightenOffers.Count: > 0 } offers)
        {
            offers.StraightenOffers.RemoveAll(id => !_state.Cards.TryGetValue(id, out var offered) || !PanKuMayRemainBowed(offered));
            if (offers.StraightenOffers.Count > 0)
            {
                var card = Card(offers.StraightenOffers[0]);
                Wave827Open(Wave827Abilities.StraightenType, card.OwnerId, card.Id, $"{Definition(card).Name} would straighten: straighten him, or leave him bowed?");
                return;
            }
        }
        if (Wave827KoanExcess() is { } excess)
            Wave827Open(Wave827Abilities.KoanType, excess.Host.OwnerId, excess.Host.Id,
                $"{Definition(excess.Host).Name} may not have more Spells than Koan cards attached ({excess.Limit}): discard {excess.Spells.Length - excess.Limit} Spell(s).");
    }

    // ---- Chiyurei's Axe ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CostTerm> Wave827EntryCost(EntryCostContext context)
    {
        if (context.Kind != EntryCostKind.Equip || context.Card.DefinitionId != Wave827Abilities.Axe || context.Wielder is null) yield break;
        // "Attaches to Hida Chiyurei paying 2 less Gold."
        if (Definition(context.Wielder).Name == "Hida Chiyurei") yield return new(-2, "Hida Chiyurei", WielderDependent: true);
    }

    // AttackComparedValue's first question: a Melee Attack a Personality with the Axe performs is compared against the lower of the target's current and printed Force (Mendon: automatic, no choice).
    private int? Wave827AxeComparedValue(string abilityId, CardInstance target, string? sourceId)
    {
        if (sourceId is null || !_state.Cards.TryGetValue(sourceId, out var source) || AttackKind(abilityId) != "Melee") return null;
        var performer = source.Zone == Zone.Attached && source.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : source;
        if (Definition(performer).Type != CardType.Personality || !Attachments(performer.Id).Any(a => Definition(a).Effect == Wave827Abilities.AxeEffect)) return null;
        return Math.Min(CardForce(target), Definition(target).Force);
    }

    // ---- registration ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave827Hooks()
    {
        EngineHooks.Abilities.Register("Wave827Abilities", 13827, static abilities => abilities.AddWave827());
        EngineHooks.ValidateEarly.Register("Wave827Validate", 13827, static (engine, context) => engine.Wave827Validate(context));
        EngineHooks.RunEffects.Register("Wave827RunEffects", 13827, static (engine, context) => engine.RunWave827(context));
        EngineHooks.PersonalityForce.Register("Wave827Force", 13827, static (engine, context) => engine.Wave827Force(context.card));
        EngineHooks.AttackKind.Register("Wave827AttackKind", 13827, static context => context.abilityId == Wave827Abilities.ZanshoRanged ? "Ranged" : null);
        EngineHooks.EntryCostTerms.Register("Wave827EntryCost", 13827, static context => context.Engine.Wave827EntryCost(context));
        EngineHooks.CostWaivers.Register("Wave827KoanWaivers", 13827, static context =>
            context.Engine.SpellFocusParityWaives(context, Wave827Abilities.JingasaEffect, odd: true) || context.Engine.SpellFocusParityWaives(context, Wave827Abilities.RobesEffect, odd: false));
        EngineHooks.StandingWatches.Register("Wave827RobesWatch", 13827, static () => new StandingWatch("w827-robes-destroy", StandingWatch.OnEffect(Wave827Abilities.RobesEffect), WatchKind.Destroy, WatchScope.Unit));
        EngineHooks.PendingChoice.Register("Wave827Pending", 13827, static (engine, type, command) =>
        {
            switch (type)
            {
                case Wave827Abilities.KokujinType: engine.Wave827AnswerKokujin(command); return true;
                case Wave827Abilities.StraightenType: engine.Wave827AnswerStraighten(command); return true;
                case Wave827Abilities.KoanType: engine.Wave827AnswerKoan(command); return true;
                default: return false;
            }
        });
        EngineHooks.PendingActions.Register("Wave827PendingActions", 13827, static (engine, context) => context.Choice.Type switch
        {
            Wave827Abilities.KokujinType => engine.Wave827KokujinActions(context.PlayerId),
            Wave827Abilities.StraightenType => engine.Wave827StraightenActions(context.PlayerId),
            Wave827Abilities.KoanType => engine.Wave827KoanActions(context.PlayerId),
            _ => null
        });
    }
}
