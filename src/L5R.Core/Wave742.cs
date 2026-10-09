namespace L5R.Core;

// Issue #765: Bonds of Hospitality and Marriage of the Emerald Champion, the two cards batches 2 and 5 deferred, wired on primitives merged since: the straighten hold tied to a card in play
// (StraightenHolds.cs), the player-level granted ability (TurnBoundaryGrants.cs, P4), the Event decision at reveal (EmperorRemainingFEvents.cs, the Jade Championship's shape), the P11 dishonor bar
// and the P14 Honor-change channel. Every legality and every negation is decided in Core. Ability ids are "w742-". Readings for Mendon to confirm or correct are in
// docs/decisions/2026-10-08-bonds-of-hospitality-and-marriage-of-the-emerald-champion.md. The wave is that of tools/content/package_wave_742.py.
//
//   Bonds of Hospitality      "Limited: Target a bowed Personality: Put this card into play. While this card remains in play, negate the target's straightening, and his controller has the ability,
//                             'Open: Pay 4 Gold: Discard a Bonds of Hospitality.'"
//                             w742-bonds-play (the Strategy stays in Zone.EventInPlay, as Uncovering Treasures does) with a StraightenHold tied to it; w742-bonds-discard, a player ability granted while it stays
//   Marriage of the Emerald   "Limited: Put this Event into play. Target your Personality. While this Event remains in play, he will not be dishonored and you will not lose Honor from other
//    Champion                 players' actions that have targeted him. After your second turn from now begins, discard this Event."
//                             an Event decision at reveal (the Personality is recorded in EventTargets); a P11 NoDishonor on him while the Event stays; an Honor-change registrant
public static class Wave742Abilities
{
    public const string BondsPlay = "w742-bonds-play", BondsDiscard = "w742-bonds-discard";

    // Target categories of this file (TargetAllows).
    public const string BowedPersonality = "w742-bowed-personality", BondsCard = "w742-bonds-card";

    // The cards (oracle ids) and the Event's effect.
    public const string Bonds = "oracle-l5r-10364", Marriage = "oracle-l5r-10821";
    public const string MarriageEffect = "w742-marriage", MarriageChoice = "w742-marriage";

    public static Dictionary<string, AbilityDefinition> AddWave742(this Dictionary<string, AbilityDefinition> all)
    {
        all[BondsPlay] = new("Limited", "Limited: Target a bowed Personality: Put this card into play. While this card remains in play, negate the target's straightening, and his controller has the ability, "
            + "\"Open: Pay 4 Gold: Discard a Bonds of Hospitality.\"", BowedPersonality);
        all[BondsDiscard] = new("Open", "Open: Pay 4 Gold: Discard a Bonds of Hospitality", BondsCard, PlayerAbility: true, Cost: GoldCostRule.Fixed(4));
        return all;
    }
}

public sealed partial class MatchEngine
{
    /// <summary>A card that stays in play after it was played: in play proper, or kept in play in the Event zone (an Event, or a Strategy put into play, such as Bonds of Hospitality).</summary>
    private static bool KeptInPlay(CardInstance card) => InPlay(card) || card.Zone == Zone.EventInPlay;

    // ---- target legality ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave742TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        switch (context.ability.Target)
        {
            case Wave742Abilities.BowedPersonality:
                return InPlay(target) && Definition(target).Type == CardType.Personality && target.Bowed;
            default:
                return false;
        }
    }

    // The granted discard names a Bonds of Hospitality kept in play (the Event zone), which the in-play default never lists. Null: not this card's category.
    private bool Wave742BondsTargetAllows(CardInstance target) => target.Zone == Zone.EventInPlay && target.DefinitionId == Wave742Abilities.Bonds;

    private string?[]? Wave742Candidates(string category) => category != Wave742Abilities.BondsCard ? null
        : _state.Cards.Values.Where(Wave742BondsTargetAllows).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    private void Wave742Validate(ValidationHookContext context)
    {
        if (context.id == Wave742Abilities.BondsPlay) Require(context.source is { Zone: Zone.Hand }, "Bonds of Hospitality is put into play from your hand.");
    }

    // ---- effects --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave742(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case Wave742Abilities.BondsPlay:
                {
                    // The Strategy stays in play; the target's straightening is negated while it does, and the target's controller can pay to discard it (a Bonds card of anyone's).
                    var name = Definition(source!).Name;
                    player.Hand.Remove(source!.Id); source.Zone = Zone.EventInPlay; source.FaceUp = true;
                    Emit("strategyEnteredPlay", $"{player.Name} puts {name} into play on {Definition(target!).Name}.", player.Id, source.Id);
                    AddStraightenHold(target!, name, null, StraightenPoint.TurnEnds, skip: int.MaxValue, whileInPlay: source.Id);
                    GrantPlayerAbility(target!.OwnerId, Wave742Abilities.BondsDiscard, TurnUntil.GameEnds(), source.Id, whileInPlay: source.Id);
                    return false;
                }
            case Wave742Abilities.BondsDiscard:
                {
                    var bonds = target!;
                    ResetCardState(bonds);
                    bonds.Zone = Zone.FateDiscard; bonds.FaceUp = true;
                    Emit("cardDiscarded", $"{player.Name} discarded {Definition(bonds).Name} from play.", player.Id, bonds.Id);
                    return false;
                }
            default:
                return null;
        }
    }

    // ---- Marriage of the Emerald Champion ---------------------------------------------------------------------------------------------------------------------------------------------

    private bool? ResolveWave742Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (Definition(card).Effect != Wave742Abilities.MarriageEffect) return null;
        var own = _state.Cards.Values.Where(p => p.OwnerId == player.Id && InPlay(p) && Definition(p).Type == CardType.Personality).Select(p => p.Id).Order(StringComparer.Ordinal).ToList();
        if (own.Count == 0) return DiscardResolvedEvent(player, province, card);
        return BeginRevealChoice(Wave742Abilities.MarriageChoice, player, card, own,
            $"{Definition(card).Name}: choose your Personality. While this Event stays in play he will not be dishonored, and {player.Name} will not lose Honor from other players' actions that have targeted him.");
    }

    // The answer to the Event decision (the default arm of ResolveEventChoiceCommand). True when this handled it.
    private bool ResolveWave742EventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind != Wave742Abilities.MarriageChoice) return false;
        Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of your Personalities in play.");
        var card = Card(pending.EventCardId); var chosen = Card(command.TargetId!); var owner = Player(card.OwnerId);
        _state.EventTargets[card.Id] = chosen.Id;
        Emit("eventChose", $"{Definition(card).Name}: {Definition(chosen).Name} will not be dishonored, and {owner.Name} will not lose Honor from other players' actions that target him.", owner.Id, chosen.Id);
        // "After your second turn from now begins, discard this Event."
        FinishRevealedEvent(card, keep: true, turnBeginsIn: 2, message: $"{Definition(card).Name} stays in play until {owner.Name}'s second turn from now begins.");
        return true;
    }

    private IEnumerable<ActionOption> Wave742EventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != Wave742Abilities.MarriageChoice) yield break;
        var name = Definition(Card(pending.EventCardId)).Name;
        foreach (var id in pending.Candidates)
            yield return new("eventChoice", $"{name}: {Definition(Card(id)).Name}", true, null, new GameCommand("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: id), id);
    }

    /// <summary>The Personalities a Marriage in play protects: the Event's owner and the Personality it names.</summary>
    private IEnumerable<(int OwnerId, string PersonalityId)> Wave742Marriages() =>
        _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && c.DefinitionId == Wave742Abilities.Marriage && _state.EventTargets.ContainsKey(c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (c.OwnerId, _state.EventTargets[c.Id]));

    // "He will not be dishonored": a live bar on the named Personality while a Marriage naming him stays in play.
    private IEnumerable<StaticRestriction> Wave742Restrictions(CardInstance card)
    {
        if (Definition(card).Type == CardType.Personality && Wave742Marriages().Any(m => m.PersonalityId == card.Id)) yield return new(RestrictionKind.NoDishonor);
    }

    // "You will not lose Honor from other players' actions that have targeted him": a loss to the Event's owner, from another player's action whose targets include the named Personality, is reduced to 0.
    private HonorChangeResult? Wave742HonorChange(HonorChangeContext context)
    {
        if (context.Stage != HonorChangeStage.Standing || context.Amount >= 0 || context.ActionKey is null || context.SourcePlayerId is not int by || by == context.PlayerId) return null;
        return Wave742Marriages().Any(m => m.OwnerId == context.PlayerId && context.TargetedCardIds.Contains(m.PersonalityId, StringComparer.Ordinal)) ? new HonorChangeResult(0) : null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave742Hooks()
    {
        EngineHooks.Abilities.Register("Wave742Abilities", 14200, static abilities => abilities.AddWave742());
        EngineHooks.PlayerGrantAbilities.Register("Wave742PlayerGrants", 14200, static () => [Wave742Abilities.BondsDiscard]);
        EngineHooks.TargetAllows.Register("Wave742TargetAllows", 14200, static (engine, context) => engine.Wave742TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave742Validate", 14200, static (engine, context) => engine.Wave742Validate(context));
        EngineHooks.RunEffects.Register("Wave742RunEffects", 14200, static (engine, context) => engine.RunWave742(context));
        EngineHooks.ResolveEvent.Register("Wave742ResolveEvent", 14200, static (engine, context) => engine.ResolveWave742Event(context.Player, context.Province, context.Card));
        EngineHooks.CardRestrictions.Register("Wave742Restrictions", 14200, static (engine, card) => engine.Wave742Restrictions(card));
        EngineHooks.HonorChange.Register("Wave742Marriage", 14200, static context => context.Engine.Wave742HonorChange(context));
    }
}
