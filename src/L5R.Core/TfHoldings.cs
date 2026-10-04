namespace L5R.Core;

// Issue #622 (holding-wave-50): twelve Holdings whose Twenty Festivals label hides an earlier Emperor-era printing; each is wired from the printed text of its earliest scan.
//
//   Yukihime's Hot Springs, Coastal Pearl Bed  "Open, bow: A target Holding's abilities may not be used (this turn)" -- the action lock (ActionLock.cs): a locked Holding's printed abilities are
//                                              barred for the turn, its Gold production is not an ability (docs/decisions/2026-09-21-second-city-pass.md, Interference).
//   Fudoist Temple                             "Open, bow: Give a target Personality and each of his Followers Cavalry" -- the keyword for the turn (R-001: durations default to turn end).
//   Beautiful Host                             "Political Open: If another player's action has caused you an Honor loss this turn, choose your performing Personality: Give him a +1F Revenge token".
//   Miryoku no Shima                           "Open: Choose your two performing Personalities: A lesson is taught. One copies Commander, Duelist, ... from the other" -- the copy primitive (CardCopy.cs).
//   Colonial Temple, Suana Dojo                conditional Gold production (ConditionalProduction.cs): 4 for a single attachment; 4 while the Stronghold's Gold Production is 3 or lower.
//   Jade Mine, Slave Pits                      two ways to bow for Gold, chosen in the payment: a PAYMENT SOURCE beside the card's own bow ("<card>#alt"), exclusive with it, whose use bows the card and
//                                              applies its drawback (Jade Mine will not straighten until after your next Action Phase; Slave Pits loses 2 Honor, offered only to a player who did not go first).
//   Small Library                              "After this card enters play from a province: Refill it face-up" -- a trait, so mandatory (Purchase's entry step).
//   Temple Fortress                            "After the first time each turn you bow this card: Gain 1 Honor" -- a trait, noticed where a payment bows its producers (Pay).
//   Nexus of Lies                              "After another player loses Honor from a card he owns, increase the loss by half his Starting Family Honor, rounded down" -- applied where the loss lands (GainHonor).
public static class TfHoldingsAbilities
{
    public const string HoldingLock = "tf-holding-lock", FudoistCavalry = "tf-fudoist-cavalry", HostRevenge = "tf-beautiful-host-revenge", MiryokuLesson = "tf-miryoku-lesson";
    /// <summary>Miryoku no Shima's slots: the Personality that copies and the one it copies from ("One copies ... from the other").</summary>
    public const string LessonLearner = "learner", LessonTeacher = "teacher", LessonTeacherTarget = "tf-lesson-teacher";

    // Card Effects (data keys; never prose).
    public const string ColonialTempleEffect = "tf-colonial-temple-gold", SuanaDojoEffect = "tf-suana-dojo-gold", SmallLibraryEffect = "tf-small-library-refill",
        TempleFortressEffect = "tf-temple-fortress-honor", NexusOfLiesEffect = "tf-nexus-of-lies", JadeMineEffect = "tf-jade-mine-gold", SlavePitsEffect = "tf-slave-pits-gold";

    /// <summary>The keywords Miryoku no Shima may copy.</summary>
    public static readonly string[] LessonKeywords = ["Commander", "Duelist", "Kensai", "Magistrate", "Paragon", "Scout", "Yojimbo"];

    public static Dictionary<string, AbilityDefinition> AddTfHoldings(this Dictionary<string, AbilityDefinition> all)
    {
        // Yukihime's Hot Springs (oracle-l5r-10830), Coastal Pearl Bed (oracle-l5r-10615): "Open, bow: A target Holding's abilities may not be used." Either player's Holding.
        all[HoldingLock] = new("Open", "Bow this card: A target Holding's abilities may not be used", "any-holding", Costs: ActionCost.BowSource);
        // Fudoist Temple (oracle-l5r-10609): "Open, bow: Give a target Personality and each of his Followers Cavalry."
        all[FudoistCavalry] = new("Open", "Bow this card: Give a target Personality and each of his Followers Cavalry", "personality", Costs: ActionCost.BowSource);
        // Beautiful Host (oracle-l5r-943): the condition before the colon is judged in ValidateTfHoldings.
        all[HostRevenge] = new("Open", "Political Open: If another player's action has caused you an Honor loss this turn, choose your performing Personality: Give him a +1F Revenge token", "none",
            Performer: "own-personality", ActionKeywords: ["Political"]);
        // Miryoku no Shima (oracle-l5r-10268): "Choose your two performing Personalities: A lesson is taught. One copies ... from the other." The slot order names who copies from whom.
        all[MiryokuLesson] = new("Open", "Choose your two performing Personalities: A lesson is taught. One copies Commander, Duelist, Kensai, Magistrate, Paragon, Scout, or Yojimbo from the other", "slots",
            Slots: [new(LessonLearner, "own-personality", "Your performing Personality who copies a keyword", 1, 1, Performing: true),
                new(LessonTeacher, LessonTeacherTarget, "Your other performing Personality, whom he copies it from", 1, 1, Performing: true)]);
        return all;
    }
}

// Beautiful Host's memory: the turn number in which another player's action last cost each player Honor (-1: never). Not a pending decision.
public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1280)]
    public int[] HonorLostToOtherActionTurn { get; set; } = [-1, -1];
}

public sealed partial class MatchEngine
{
    private const string AltProductionMark = "#alt";

    // ---- the three actions -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunTfHoldings(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, HashSet<string> bowedBeforeAction)
    {
        switch (id)
        {
            case TfHoldingsAbilities.HoldingLock:
                LockActions(target!, source!.Id);
                return false;
            case TfHoldingsAbilities.FudoistCavalry:
                GrantKeyword(target!, "Cavalry", source!.Id, GrantDuration.Turn);
                foreach (var follower in Attachments(target!.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray()) GrantKeyword(follower, "Cavalry", source.Id, GrantDuration.Turn);
                return false;
            case TfHoldingsAbilities.HostRevenge:
                CreateToken(ActionPerformer(ability, command, source), "Revenge", id, "Force", 1);
                return false;
            case TfHoldingsAbilities.MiryokuLesson:
                return TfTeachLesson(id, player, command, bowedBeforeAction);
            default:
                return null;
        }
    }

    private void ValidateTfHoldings(string id, PlayerState player)
    {
        if (id == TfHoldingsAbilities.HostRevenge)
            Require(_state.HonorLostToOtherActionTurn[player.Id] == _state.TurnNumber, "Another player's action has not caused you an Honor loss this turn.");
    }

    // Miryoku no Shima's second slot: another of the player's Personalities than the one who copies.
    private bool TfTargetAllows(string category, GameCommand command, PlayerState player, CardInstance target) =>
        category == TfHoldingsAbilities.LessonTeacherTarget && Definition(target).Type == CardType.Personality && target.OwnerId == player.Id
        && SlotCards(command, TfHoldingsAbilities.LessonLearner).All(learner => learner.Id != target.Id);

    // "One copies Commander, Duelist, Kensai, Magistrate, Paragon, Scout, or Yojimbo from the other": the keywords of the second Personality, among the seven, that the first does not have, for the
    // turn. The copy primitive (CardCopy.cs): nothing to copy is skipped, one is taken, several ask the player. The printed "copies one" names no further limit.
    private bool TfTeachLesson(string id, PlayerState player, GameCommand command, HashSet<string> bowedBeforeAction)
    {
        var learner = SlotCards(command, TfHoldingsAbilities.LessonLearner).First(); var teacher = SlotCards(command, TfHoldingsAbilities.LessonTeacher).First();
        Emit("lessonTaught", $"A lesson is taught: {Definition(learner).Name} learns from {Definition(teacher).Name}.", player.Id, learner.Id);
        var copy = new PendingCopy
        {
            PlayerId = player.Id,
            ReceiverId = learner.Id,
            ActionCardId = learner.Id,
            CopierAbilityId = id,
            Duration = GrantDuration.Turn,
            SourceDefinitionId = teacher.DefinitionId,
            SourceName = Definition(teacher).Name,
            Steps = [CopyStep.Keyword],
            Origin = "ability",
            BowedBefore = bowedBeforeAction.ToList()
        };
        copy.KeywordChoices = CopyableKeywords(teacher, learner).Where(k => TfHoldingsAbilities.LessonKeywords.Contains(k, StringComparer.Ordinal)).ToList();
        return AdvanceCopy(copy);
    }

    // ---- Beautiful Host: the Honor another player's action cost -----------------------------------------------------------------------------------------------------------------------------

    // NoteHonorLostToOtherAction's hook: the action that just resolved cost these players Honor, in any phase of this turn.
    private void TfNoteHonorLostToOtherAction(int actingPlayerId, List<int> losers)
    {
        foreach (var loser in losers)
            if (loser != actingPlayerId) _state.HonorLostToOtherActionTurn[loser] = _state.TurnNumber;
    }

    // ---- production -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ConditionalProductionOf's hook. Colonial Temple: "Bow this card: When paying for an attachment, it enters play for 4 less Gold" -- a payment for an attachment is 4 Gold of that cost (Platinum Mine's
    // condition). Suana Dojo: "Produce 3 Gold, or 4 Gold if your Stronghold has 3 or lower Gold Production", read when the Gold is produced.
    private ConditionalProduction? TfConditionalProduction(CardInstance card) => Definition(card).Effect switch
    {
        TfHoldingsAbilities.ColonialTempleEffect => new(4, ProductionCondition.PayingForAttachment),
        TfHoldingsAbilities.SuanaDojoEffect when GoldProduced(Player(card.OwnerId).StrongholdId, card.OwnerId) <= 3 => new(4, ProductionCondition.ControllerCondition),
        _ => null
    };

    // "Did not go first this game": the first turn belongs to the player who went first, and the turns alternate.
    private bool TfWentFirst(int playerId) => (_state.TurnNumber % 2 == 1 ? _state.ActivePlayerId : 1 - _state.ActivePlayerId) == playerId;

    private static bool TfIsAltToken(PaymentToken token) => token.AbilityId is TfHoldingsAbilities.JadeMineEffect or TfHoldingsAbilities.SlavePitsEffect;

    // Jade Mine and Slave Pits: the second way to bow for Gold, a payment source of its own ("<card>#alt", shown by the client as a "bow" method). It is chosen instead of the card's own bow, never with it.
    private IEnumerable<PaymentToken> TfAltProductionTokens(int playerId, PaymentSubject? paying)
    {
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield && !c.Bowed).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var effect = Definition(card).Effect; var name = Definition(card).Name;
            if (effect is not (TfHoldingsAbilities.JadeMineEffect or TfHoldingsAbilities.SlavePitsEffect)) continue;
            if (effect == TfHoldingsAbilities.SlavePitsEffect && TfWentFirst(playerId)) continue; // "If you did not go first this game"
            var amount = Math.Max(0, 3 + ShadowsEmbraceBatch6Production(card, playerId));
            var label = effect == TfHoldingsAbilities.JadeMineEffect
                ? $"Bow this card: {amount} Gold. It will not straighten until after your next Action Phase"
                : $"Bow this card: {amount} Gold. Lose 2 Honor";
            yield return new(card.Id + AltProductionMark, card, effect, amount, "bow", label, null);
        }
    }

    // The alternative is exclusive with the card's own bow (and with a bow another cost of the action already makes).
    private void RequireTfAltExclusive(string[] ids, IReadOnlySet<string>? exclude)
    {
        foreach (var id in ids.Where(i => i.EndsWith(AltProductionMark, StringComparison.Ordinal)))
        {
            var cardId = PaymentTokenCardId(id);
            Require(!ids.Contains(cardId, StringComparer.Ordinal), "A Holding bows for Gold only once in a payment: choose one of its ways to produce.");
            Require(exclude is null || !exclude.Contains(cardId), "A card bowed or destroyed as part of the action's cost cannot also be bowed for its Gold.");
        }
    }

    // The ordinary source's conflicts: its own alternative.
    private static string[]? TfAltConflicts(CardInstance card, PaymentToken[] tokens) =>
        tokens.Where(t => TfIsAltToken(t) && t.Card.Id == card.Id).Select(t => t.Id).ToArray() is { Length: > 0 } alt ? alt : null;

    // What an alternative adds beyond the card's own production in a variable quote's ceiling: the better of the two ways, not both.
    private int TfAltOverlap(IGrouping<string, PaymentToken> group, int playerId, PaymentSubject? paying) =>
        group.Any(TfIsAltToken) ? Math.Min(GoldProduced(group.Key, playerId, paying), group.Where(TfIsAltToken).Max(t => t.Amount)) : 0;

    // PaymentOptionsExcluding: each alternative joins its card's own group, so a suggestion never names both ways of one Holding.
    private static void TfMergeAltIntoGroups(List<(string Id, int Amount)[]> groups, IEnumerable<PaymentToken> tokens)
    {
        foreach (var token in tokens.Where(TfIsAltToken))
        {
            var index = groups.FindIndex(g => g.Any(item => item.Id == token.Card.Id));
            if (index >= 0) groups[index] = [.. groups[index], (token.Id, token.Amount)];
            else groups.Add([(token.Id, token.Amount)]);
        }
    }

    // UsePaymentTokens' step for an alternative: the card bows, and its drawback applies.
    private void TfUseAltToken(PaymentToken token, int playerId)
    {
        var card = token.Card; var name = Definition(card).Name;
        card.Bowed = true;
        Emit("goldSourceUsed", $"{name} bowed to produce {token.Amount} Gold.", playerId, card.Id);
        if (token.AbilityId == TfHoldingsAbilities.JadeMineEffect)
        {
            // "Until after your next Action Phase": the Action Phase in progress is not the next one.
            var skip = _state.ActivePlayerId == card.OwnerId && _state.Phase == Phase.Action ? 1 : 0;
            AddStraightenHold(card, name, card.OwnerId, StraightenPoint.ActionPhaseEnds, skip);
        }
        else GainHonor(playerId, -2, name, card);
    }

    // ---- Temple Fortress: "After the first time each turn you bow this card: Gain 1 Honor." ----------------------------------------------------------------------------------------------------

    private const string FortressTurnKey = "tf:fortress-honor-turn";

    // Pay's step after the producers it names have bowed.
    private void TfAfterProducersBowed(int playerId, string[] producerIds)
    {
        foreach (var id in producerIds.Where(i => !IsPaymentToken(i)))
        {
            var card = Card(id);
            if (Definition(card).Effect != TfHoldingsAbilities.TempleFortressEffect || card.OwnerId != playerId) continue;
            if (card.AbilityUses.TryGetValue(FortressTurnKey, out var turn) && turn == _state.TurnNumber) continue;
            card.AbilityUses[FortressTurnKey] = _state.TurnNumber;
            GainHonor(playerId, 1, Definition(card).Name, card);
        }
    }

    // ---- Small Library: "After this card enters play from a province: Refill it face-up." ------------------------------------------------------------------------------------------------------

    // Purchase's step once the province has refilled the slot the Holding left.
    private void TfAfterEnteredFromProvince(PlayerState player, ProvinceState province, int slot, CardInstance card)
    {
        if (Definition(card).Effect != TfHoldingsAbilities.SmallLibraryEffect || Definition(card).Type != CardType.Holding) return;
        if (slot == OtosanSlotPrimary) { RefillProvinceFaceUp(player, province.Index, card); return; }
        if (province.CardId2 is { } secondId && Card(secondId) is { Zone: Zone.Province, FaceUp: false } refilled)
        {
            refilled.FaceUp = true;
            Emit("provinceRefilled", $"{Definition(card).Name}: {player.Name}'s province {province.Index + 1} was refilled face up with {Definition(refilled).Name}.", player.Id, refilled.Id);
        }
    }

    // ---- Nexus of Lies -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // GainHonor's step as a loss lands: "After another player loses Honor from a card he owns, increase the loss by an amount equal to half his Starting Family Honor, rounded down." The card is the loss's
    // named source, else the card whose action is resolving. A loss that comes from no card (a battle's resolution) is not "from a card he owns".
    private int TfNexusIncreasedLoss(int playerId, int amount, CardInstance? source)
    {
        if (amount >= 0) return amount;
        var from = source ?? (_runningCommand?.CardId is { } runningId && _state.Cards.TryGetValue(runningId, out var running) ? running : null);
        if (from is null || from.OwnerId != playerId) return amount;
        var nexuses = _state.Cards.Values.Count(c => c.OwnerId != playerId && InPlay(c) && Definition(c).Effect == TfHoldingsAbilities.NexusOfLiesEffect);
        var extra = nexuses * Math.Max(0, Definition(Card(Player(playerId).StrongholdId)).StartingHonor / 2);
        if (extra == 0) return amount;
        Emit("honorLossIncreased", $"Nexus of Lies: {Player(playerId).Name}'s Honor loss from {Definition(from).Name} is increased by {extra}.", playerId, from.Id, -extra);
        return amount - extra;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTfHoldingsHooks()
    {
        EngineHooks.Abilities.Register("AddTfHoldings", 11400, static abilities => abilities.AddTfHoldings());
        EngineHooks.RunEffects.Register("RunTfHoldings", 11400, static (engine, context) => engine.RunTfHoldings(context.id, context.ability, context.command, context.player, context.source, context.target, context.bowedBeforeAction));
        EngineHooks.ValidateLate.Register("ValidateTfHoldings", 11400, static (engine, context) => engine.ValidateTfHoldings(context.id, context.player));
        EngineHooks.TargetAllows.Register("TfHoldingsTargetAllows", 11400, static (engine, context) => engine.TfTargetAllows(context.ability.Target, context.command, context.player, context.target));
    }
}
