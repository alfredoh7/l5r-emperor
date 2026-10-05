using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-EMPEROR PASS (issue #653), the Events (event-wave-31). Every Event was read against its Promotional-Emperor scan. An Event resolves as its province is revealed in its owner's Events
// Phase (core rules; ResolveEvent), and the registered ResolveEvent hook below is where a feature resolves its own.
//
// READINGS (none is a confirmed ruling):
//   * "Limited:" on an Event is ignored (the Gates of Chaos supplement, "Aftermath ... by extension": every Event of those sets resolves as a plain triggered trait). The nine Events of
//     December 2013 - February 2014 that print it (A Great Victory, the two Festivals, Legionnaire's Appointment, Losing the Way, The Fever Breaks, Yozo's Inspiration) are the same print generation,
//     so they resolve on reveal. The Emperor-era Events print no designator and no "After you reveal ..." lead-in (the catalogue text adds it).
//   * "Until your next turn begins" is ExpiresAtTurnsTaken = the owner's TurnsTaken + 1 (the Event leaves play as that turn begins), "until your second turn from now" + 2, "three turns from now" + 3.
//   * The Festivals give their modifier to the Holdings in play as the Event resolves ("give ... Holdings"); a Holding that enters later is not changed. A Holding that prints no Gold Production has
//     none to change (the engine's producers are the cards with a printed production).
//   * Family Maneuvers' "Family Name" is the Torn Asunder rule of thumb (docs/rules/EMPEROR_EDITION_CORE_RULES.md): the first word of a two-word title that is not an ordinary English word, on a
//     Personality with a Clan alignment or a Clan keyword.
//   * Diplomacy Talks: "after each time a player creates an Attack Phase, destroy his rightmost Province" -- the player who created the phase loses his own rightmost Province that still exists; the
//     ordinary declaration and the additional Attack Phases (Retribution, The Perfect Moment, Aramasu's Legacy) all count.
//   * Expansion / Topaz Championship's "before the first time each turn another player's card's effect destroys ...": the destruction by an action (an ability, an Attack's hit, a duel) of that
//     player's Personality is negated, once per turn; the battle's own casualty sweep is not "a card's effect". Expansion negates the whole destruction (every Personality of the owner in it).
//   * Ritual of Sacrifice: "after each time a card effect destroys a Personality, the player who last controlled him discards a card" -- his controller (the card's owner id, the engine's controller)
//     discards a card of his choice, as a queued forced discard.
//   * Tales of Valor: the scan prints the End Phase ("After your next End Phase begins ..."), not the catalogue's Dynasty Phase. "Put any cards in your hand" is every card the engine reports entering
//     the hand (NoteCardIntoHand: drawing, searching, a Strategy returned to the hand).
//   * Yozo's Inspiration: "a player who declared an attack against you since your last turn ended" -- the other player created an Attack Phase in the turn just before this one.
//   * The Championships: "Choose one of your ... in play" is the owner's decision as the Event is revealed (an Event decision: PendingEventChoice kind "pe-appoint"); "permanently give him" is a
//     permanent keyword, stat modifier, granted ability or granted trait on that card, which is a new object if it leaves play and comes back.
//   * Topaz Championship's "Personalities in play without abilities": no printed or granted action, Reaction or trait (no catalogued ability, no Effect, no printed entry Honor loss, no text).
// ======================================================================================================================================================================================

/// <summary>What the Promotional-Emperor Events keep between commands.</summary>
public sealed class PromoEmperorEventState
{
    /// <summary>The turn number in which each player last created an Attack Phase (-1: never) -- Yozo's Inspiration's "declared an attack against you since your last turn ended".</summary>
    public int[] AttackPhaseCreatedTurn { get; set; } = [-1, -1];
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(1590)]
    public PromoEmperorEventState PromoEvents { get; set; } = new();
}

public static class PromoEmperorEventAbilities
{
    // The Events' Effect keys.
    public const string LegionnairesAppointment = "pe-legionnaires-appointment", InarisFestival = "pe-inaris-festival", FrostDragonFestival = "pe-frost-dragon-festival", FamilyManeuvers = "pe-family-maneuvers",
        YozosInspiration = "pe-yozos-inspiration", DiplomacyTalks = "pe-diplomacy-talks", Expansion = "pe-expansion", GreatVictory = "pe-a-great-victory", TalesOfValor = "pe-tales-of-valor",
        RitualOfSacrifice = "pe-ritual-of-sacrifice", FeverBreaks = "pe-the-fever-breaks", LosingTheWay = "pe-losing-the-way", Amethyst = "pe-amethyst-championship", Emerald = "pe-emerald-championship",
        Obsidian = "pe-obsidian-championship", Ruby = "pe-ruby-championship", Topaz = "pe-topaz-championship", Turquoise = "pe-turquoise-championship";

    // The abilities the Championships grant (permanently) to the Personality chosen.
    public const string AmethystCopy = "pe-amethyst-copy", EmeraldStraighten = "pe-emerald-straighten", ObsidianBow = "pe-obsidian-bow", RubyBow = "pe-ruby-bow", TopazDestroy = "pe-topaz-destroy",
        TurquoiseHonor = "pe-turquoise-honor";

    /// <summary>The trait The Topaz Championship grants (CardInstance.AbilityUses key, 1 while he has it) and the turn it last negated a destruction.</summary>
    public const string TopazTrait = "pe-trait:topaz-negate", TopazUsedTurn = "pe-trait-used:topaz-negate";

    public static bool IsChampionship(string effect) => effect is Amethyst or Emerald or Obsidian or Ruby or Topaz or Turquoise;

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorEvents(this Dictionary<string, AbilityDefinition> all)
    {
        // The Amethyst Championship (oracle-l5r-7955): "Political Open: Target another Personality: This Personality copies one of the targeted Personality's Political abilities which does not itself copy abilities."
        all[AmethystCopy] = new("Open", "Political Open: Target another Personality: This Personality copies one of the targeted Personality's Political abilities which does not itself copy abilities", "other-personality",
            ActionKeywords: ["Political"], CopiesAbilities: true);
        // The Emerald Championship: "Battle/Open: Even if this Personality is bowed, target a Personality: Straighten him." (the bowed-source list in CardAbilities.cs)
        all[EmeraldStraighten] = new("BattleOrOpen", "Even if this Personality is bowed, target a Personality: Straighten him", "personality");
        // The Obsidian Championship: "Battle: Target an enemy card with lower Force: Bow it. Destroy it if it is an attachment."
        all[ObsidianBow] = new("Battle", "Target an enemy card with lower Force: Bow it. Destroy it if it is an attachment", "enemy-force-lt-self");
        // The Ruby Championship: "Battle: Target an enemy Personality: Bow him. If he is dishonorable, destroy a card without attachments in his unit."
        all[RubyBow] = new("Battle", "Target an enemy Personality: Bow him. If he is dishonorable, destroy a card without attachments in his unit", "enemy-personality");
        // The Topaz Championship: "Battle: Target an enemy card without attachments: Destroy it."
        all[TopazDestroy] = new("Battle", "Target an enemy card without attachments: Destroy it", "enemy-card-without-attachments");
        // The Turquoise Championship: "Limited: Choose a player. He gains or loses 1 Honor." (the four answers every such card offers, EffectChoice.cs)
        all[TurquoiseHonor] = new("Limited", "Choose a player. He gains or loses 1 Honor", "none", Branch: new(BranchCondition.Always, HonorChoice.Options(1), HonorChoice.None));
        return all;
    }
}

internal static partial class EngineHooks
{
    /// <summary>An Event's own resolution as its province is revealed (ResolveEvent): null when it is not the registrant's Event, else what ResolveEvent returns (true: the reveal paused for a decision).</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.ResolveEventContext, bool?>> ResolveEvent = new();
}

public sealed partial class MatchEngine
{
    internal readonly record struct ResolveEventContext(PlayerState Player, ProvinceState Province, CardInstance Card);

    private bool? RegisteredResolveEvent(PlayerState player, ProvinceState province, CardInstance card)
    {
        var context = new ResolveEventContext(player, province, card);
        foreach (var hook in EngineHooks.ResolveEvent.Items)
            if (hook(this, context) is { } result) return result;
        return null;
    }

    private PromoEmperorEventState PromoEvents => _state.PromoEvents;

    internal void NoteCardIntoHandForTest(int playerId) => NoteCardIntoHand(playerId);

    // ---- the reveal ------------------------------------------------------------------------------------------------------------------------------------------------------------

    internal bool? ResolvePromoEvent(PlayerState player, ProvinceState province, CardInstance card)
    {
        var effect = Definition(card).Effect;
        switch (effect)
        {
            case PromoEmperorEventAbilities.InarisFestival:
            case PromoEmperorEventAbilities.FrostDragonFestival:
                return PromoResolveFestival(player, province, card, effect == PromoEmperorEventAbilities.InarisFestival);
            case PromoEmperorEventAbilities.FamilyManeuvers:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins.", turnBeginsIn: 1);
                return false;
            case PromoEmperorEventAbilities.DiplomacyTalks:
            case PromoEmperorEventAbilities.Expansion:
            case PromoEmperorEventAbilities.RitualOfSacrifice:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins.", turnBeginsIn: 1);
                return false;
            case PromoEmperorEventAbilities.YozosInspiration:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s second turn from now begins.", turnBeginsIn: 2);
                return false;
            case PromoEmperorEventAbilities.GreatVictory:
                card.AbilityUses["pe-event-turn"] = _state.TurnNumber; // "after each battle resolution (this turn)"
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play: after each battle resolution this turn in which {player.Name}'s army destroyed an enemy army or Province, each of his Personalities in it gets a +1F/+1C token.", turnBeginsIn: 1);
                return false;
            case PromoEmperorEventAbilities.TalesOfValor:
                card.AbilityUses["pe-tales-hand"] = 0; // set to 1 when a card is put into the owner's hand
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play: if {player.Name} puts no card in his hand before his End Phase begins, he draws two cards.", turnBeginsIn: 1);
                return false;
            case PromoEmperorEventAbilities.LosingTheWay:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s turn three turns from now begins: each player may put no more than one Ring into play per turn.", turnBeginsIn: 3);
                _state.RingSchismTurn = _state.TurnNumber;
                return false;
            case PromoEmperorEventAbilities.FeverBreaks:
                {
                    var destroyed = 0;
                    foreach (var tokenCard in _state.Cards.Values.Where(c => c.Tokens.Any(t => t.Type == "Madness")).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
                        foreach (var token in tokenCard.Tokens.Where(t => t.Type == "Madness").ToArray()) { DestroyToken(tokenCard, token); destroyed++; }
                    Emit("eventResolved", $"{Definition(card).Name}: {destroyed} Madness token(s) were destroyed.", player.Id, card.Id);
                    return DiscardResolvedEvent(player, province, card);
                }
            case PromoEmperorEventAbilities.LegionnairesAppointment:
                return PromoBeginAppointment(player, province, card, c => Definition(c).Type == CardType.Personality, "choose one of your Personalities in play: he is permanently given Conqueror");
            case PromoEmperorEventAbilities.Amethyst:
                return PromoBeginAppointment(player, province, card, c => Definition(c).Type == CardType.Personality && Keyword(c, "Courtier"), "choose one of your Courtiers in play");
            case PromoEmperorEventAbilities.Emerald or PromoEmperorEventAbilities.Obsidian or PromoEmperorEventAbilities.Ruby or PromoEmperorEventAbilities.Turquoise:
                return PromoBeginAppointment(player, province, card, c => Definition(c).Type == CardType.Personality, "choose one of your Personalities in play");
            case PromoEmperorEventAbilities.Topaz:
                return PromoBeginAppointment(player, province, card, c => Definition(c).Type == CardType.Personality && PromoHasNoAbilities(c), "choose one of your Personalities in play without abilities");
            default:
                return null;
        }
    }

    // ---- the Festivals -----------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Give your non-Unique Holdings +1 Gold Production" / "Give all other players' non-Unique Holdings -1 Gold Production" until the owner's next turn begins: the Holdings in play now.
    private bool PromoResolveFestival(PlayerState player, ProvinceState province, CardInstance card, bool inari)
    {
        var holdings = _state.Cards.Values.Where(c => c.Zone == Zone.Home && Definition(c).Type == CardType.Holding && !Keyword(c, "Unique") && (inari ? c.OwnerId == player.Id : c.OwnerId != player.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        foreach (var holding in holdings) card.AbilityUses["pe-festival:" + holding.Id] = inari ? 1 : -1;
        KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins: {holdings.Length} Holding(s) have {(inari ? "+1" : "-1")} Gold Production.", turnBeginsIn: 1);
        return false;
    }

    // GoldProduced's term: a Festival still in play changes the Gold Production of the Holdings it gave it to.
    private int PromoFestivalProduction(CardInstance holding)
    {
        var total = 0;
        foreach (var festival in _state.Cards.Values)
            if (festival.Zone == Zone.EventInPlay && festival.AbilityUses.Count > 0 && Definition(festival).Effect is PromoEmperorEventAbilities.InarisFestival or PromoEmperorEventAbilities.FrostDragonFestival
                && festival.AbilityUses.TryGetValue("pe-festival:" + holding.Id, out var amount)) total += amount;
        return total;
    }

    // ---- Family Maneuvers and Yozo's Inspiration (Personality Force) ----------------------------------------------------------------------------------------------------------

    // Family Name: PromoFamilyName (PromoEmperorItems.cs), the Torn Asunder rule of thumb shared with Family Sword.

    internal int PromoEventForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || card.Zone != Zone.Battlefield) return 0;
        var total = 0;
        foreach (var ev in _state.Cards.Values)
        {
            if (ev.Zone != Zone.EventInPlay || ev.OwnerId != card.OwnerId) continue;
            switch (Definition(ev).Effect)
            {
                // "Your Personalities have +1F while in an army with another Personality with the same Family Name."
                case PromoEmperorEventAbilities.FamilyManeuvers:
                    if (PromoFamilyName(card) is { } family && card.ProvinceIndex is int battlefield
                        && Army(card.OwnerId, battlefield).Any(other => other.Id != card.Id && PromoFamilyName(other) == family)) total += 1;
                    break;
                // "Your Personalities have +1F while attacking a player who declared an attack against you since your last turn ended."
                case PromoEmperorEventAbilities.YozosInspiration:
                    if (IsAttacking(card) && PromoEvents.AttackPhaseCreatedTurn[1 - card.OwnerId] >= _state.TurnNumber - 1) total += 1;
                    break;
            }
        }
        return total;
    }

    // ---- Attack Phases: Diplomacy Talks, Yozo's Inspiration ----------------------------------------------------------------------------------------------------------------

    // Called as a player creates an Attack Phase (the declaration and each additional Attack Phase): records it, then every Diplomacy Talks in play destroys the creator's rightmost Province.
    private void PromoAttackPhaseCreated(int creatorId)
    {
        PromoEvents.AttackPhaseCreatedTurn[creatorId] = _state.TurnNumber;
        foreach (var talks in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == PromoEmperorEventAbilities.DiplomacyTalks).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            if (_state.WinnerId is not null) return;
            var rightmost = Player(creatorId).Provinces.Where(p => !p.Destroyed).OrderByDescending(p => p.Index).FirstOrDefault();
            if (rightmost is null) return;
            Emit("eventResolved", $"{Definition(talks).Name}: {Player(creatorId).Name} created an Attack Phase, so their rightmost province is destroyed.", creatorId, talks.Id);
            DestroyProvince(creatorId, rightmost);
        }
    }

    // ---- destruction: Expansion, the Topaz trait, Ritual of Sacrifice -------------------------------------------------------------------------------------------------------

    // DestroyUnits' first look at a batch: the cards whose destruction is negated. Only a destruction by an action (actingPlayerId) and only of a Personality another player's effect destroys.
    private HashSet<string> PromoNegatedDestructions(CardInstance[] cards, int? actingPlayerId)
    {
        var negated = new HashSet<string>(StringComparer.Ordinal);
        if (actingPlayerId is not int actor) return negated;
        foreach (var owner in cards.Where(c => c.OwnerId != actor && Definition(c).Type == CardType.Personality).Select(c => c.OwnerId).Distinct())
        {
            // Expansion: before the first time each turn another player's card's effect destroys one or more of your Personalities, negate the destruction (all of them).
            var expansion = _state.Cards.Values.FirstOrDefault(c => c.Zone == Zone.EventInPlay && c.OwnerId == owner && Definition(c).Effect == PromoEmperorEventAbilities.Expansion
                && (!c.AbilityUses.TryGetValue("pe-event-used", out var turn) || turn != _state.TurnNumber));
            if (expansion is not null)
            {
                expansion.AbilityUses["pe-event-used"] = _state.TurnNumber;
                foreach (var card in cards.Where(c => c.OwnerId == owner && Definition(c).Type == CardType.Personality)) negated.Add(card.Id);
                Emit("destructionNegated", $"{Definition(expansion).Name}: {Player(owner).Name}'s Personalities' destruction was negated.", owner, expansion.Id);
            }
        }
        // The Topaz Championship's trait: before the first time each turn another player's card effect destroys this Personality, negate the destruction.
        foreach (var card in cards.Where(c => c.OwnerId != actor && Definition(c).Type == CardType.Personality && !negated.Contains(c.Id) && c.AbilityUses.ContainsKey(PromoEmperorEventAbilities.TopazTrait)
            && (!c.AbilityUses.TryGetValue(PromoEmperorEventAbilities.TopazUsedTurn, out var used) || used != _state.TurnNumber)))
        {
            card.AbilityUses[PromoEmperorEventAbilities.TopazUsedTurn] = _state.TurnNumber;
            negated.Add(card.Id);
            Emit("destructionNegated", $"{Definition(card).Name}: the destruction was negated (The Topaz Championship's trait).", card.OwnerId, card.Id);
        }
        return negated;
    }

    // DestroyUnits' step after a Personality was destroyed by an action: Ritual of Sacrifice, "the player who last controlled him discards a card".
    private void PromoAfterPersonalityDestroyed(CardInstance personality, int? actingPlayerId)
    {
        if (actingPlayerId is null || Definition(personality).Type != CardType.Personality) return;
        foreach (var ritual in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == PromoEmperorEventAbilities.RitualOfSacrifice).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            if (Player(personality.OwnerId).Hand.Count > 0 && !P2HandDiscardBlocked(personality.OwnerId, actingPlayerId.Value, false, Definition(ritual).Name))
            {
                _state.PendingForcedDiscards.Add(new(personality.OwnerId, Definition(ritual).Name));
                Emit("eventResolved", $"{Definition(ritual).Name}: {Player(personality.OwnerId).Name} must discard a card.", personality.OwnerId, ritual.Id);
            }
    }

    // ---- turn and phase boundaries ---------------------------------------------------------------------------------------------------------------------------------------------

    // StartTurn's hook (after the Events whose duration ended were discarded): Losing the Way holds the Ring limit for every turn it lasts.
    private void PromoTurnBegins(PlayerState player)
    {
        foreach (var losing in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == PromoEmperorEventAbilities.LosingTheWay))
            if (losing.ExpiresAtTurnsTaken is not int expires || Player(losing.OwnerId).TurnsTaken < expires) _state.RingSchismTurn = _state.TurnNumber;
    }

    // The End Phase begins (PromoPhaseEnds is called as the phase changes): Tales of Valor's two cards.
    private void PromoEndPhaseBegins()
    {
        foreach (var tales in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && c.OwnerId == _state.ActivePlayerId && Definition(c).Effect == PromoEmperorEventAbilities.TalesOfValor).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            if (tales.AbilityUses.GetValueOrDefault("pe-tales-hand") == 0 && tales.AbilityUses.GetValueOrDefault("pe-tales-done") == 0)
            {
                var owner = Player(tales.OwnerId);
                Emit("eventResolved", $"{Definition(tales).Name}: {owner.Name} put no card in his hand, so he draws two cards.", owner.Id, tales.Id);
                Draw(owner); Draw(owner);
            }
            tales.AbilityUses["pe-tales-done"] = 1;
        }
    }

    // NoteCardIntoHand's hook: a card was put into a player's hand since the Event resolved.
    private void PromoNoteCardIntoHand(int playerId)
    {
        foreach (var tales in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && c.OwnerId == playerId && Definition(c).Effect == PromoEmperorEventAbilities.TalesOfValor))
            tales.AbilityUses["pe-tales-hand"] = 1;
    }

    // ---- battle resolution: A Great Victory ---------------------------------------------------------------------------------------------------------------------------------

    private void PromoApplyEventResolution(CardInstance[] attackers, CardInstance[] defenders, int attackerId, int[] destroyedByPlayer, bool provinceDestroyed)
    {
        foreach (var victory in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == PromoEmperorEventAbilities.GreatVictory
            && c.AbilityUses.GetValueOrDefault("pe-event-turn") == _state.TurnNumber).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            var owner = victory.OwnerId;
            if (!(destroyedByPlayer[owner] > 0 || provinceDestroyed && owner == attackerId)) continue;
            foreach (var personality in (owner == attackerId ? attackers : defenders).Where(c => c.Zone == Zone.Battlefield && Definition(c).Type == CardType.Personality && c.OwnerId == owner))
                CreateTwoStatToken(personality, "A Great Victory", victory.Id, "Force", 1, "Chi", 1);
            Emit("eventResolved", $"{Definition(victory).Name}: each of {Player(owner).Name}'s Personalities in the army was given a +1F/+1C token.", owner, victory.Id);
        }
    }

    // ---- the Championships: the decision and the grants ---------------------------------------------------------------------------------------------------------------------

    private bool PromoHasNoAbilities(CardInstance personality)
    {
        var def = Definition(personality);
        var text = (def.Text ?? "").Replace("May not be included in decks.", "").Trim();
        return !AbilityIdsOf(personality).Any() && def.Effect == "none" && def.EntryHonorLoss is null && text.Length == 0 && personality.AbilityUses.Keys.All(k => !k.StartsWith("pe-trait:", StringComparison.Ordinal));
    }

    // "Choose one of your [Personalities] in play": the owner's decision as the Event is revealed. With nothing to choose the Event resolves and is discarded.
    private bool PromoBeginAppointment(PlayerState player, ProvinceState province, CardInstance card, Func<CardInstance, bool> eligible, string what)
    {
        var candidates = _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && eligible(c)).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        if (candidates.Count == 0)
        {
            Emit("eventResolved", $"{Definition(card).Name}: {player.Name} has no Personality to choose.", player.Id, card.Id);
            return DiscardResolvedEvent(player, province, card);
        }
        OpenEventChoice(new PendingEventChoice { Kind = "pe-appoint", EventCardId = card.Id, PlayerId = player.Id, Origin = "reveal", Candidates = candidates, Prompt = $"{Definition(card).Name}: {what}." });
        return true;
    }

    // EventChoice's default branch: the answer. False when it is not this feature's decision.
    private bool PromoEventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind != "pe-appoint") return false;
        Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered Personalities.");
        var card = Card(pending.EventCardId); var chosen = Card(command.TargetId!);
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        Emit("eventChose", $"{Definition(card).Name}: {Player(pending.PlayerId).Name} chooses {Definition(chosen).Name}.", pending.PlayerId, chosen.Id);
        PromoApplyAppointment(card, chosen);
        FinishRevealedEvent(card);
        return true;
    }

    private IEnumerable<ActionOption> PromoEventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != "pe-appoint") yield break;
        var eventName = Definition(Card(pending.EventCardId)).Name;
        foreach (var id in pending.Candidates)
            yield return new("eventChoice", $"{eventName}: {Definition(Card(id)).Name}", true, null, new GameCommand("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: id), id);
    }

    private void PromoApplyAppointment(CardInstance ev, CardInstance chosen)
    {
        var source = ev.Id; var name = Definition(ev).Name;
        void Keyword2(params string[] keywords) { foreach (var keyword in keywords) GrantKeyword(chosen, keyword, source, GrantDuration.Permanent); }
        void Ability(string id) { GrantAbility(chosen, id, source, GrantDuration.Permanent); }
        switch (Definition(ev).Effect)
        {
            case PromoEmperorEventAbilities.LegionnairesAppointment: Keyword2("Conqueror"); break;
            case PromoEmperorEventAbilities.Amethyst:
                AddPermanentModifier(chosen, source, name, "PersonalHonor", 1);
                Ability(PromoEmperorEventAbilities.AmethystCopy);
                break;
            case PromoEmperorEventAbilities.Emerald:
                Keyword2("Imperial", "Magistrate");
                Ability(PromoEmperorEventAbilities.EmeraldStraighten);
                break;
            case PromoEmperorEventAbilities.Obsidian:
                AddPermanentModifier(chosen, source, name, "Force", 1);
                Keyword2("Shadowlands");
                Ability(PromoEmperorEventAbilities.ObsidianBow);
                break;
            case PromoEmperorEventAbilities.Ruby:
                Keyword2("Imperial", "Magistrate");
                Ability(PromoEmperorEventAbilities.RubyBow);
                break;
            case PromoEmperorEventAbilities.Topaz:
                AddPermanentModifier(chosen, source, name, "Force", 1);
                AddPermanentModifier(chosen, source, name, "Chi", 1);
                chosen.AbilityUses[PromoEmperorEventAbilities.TopazTrait] = 1;
                Ability(PromoEmperorEventAbilities.TopazDestroy);
                break;
            case PromoEmperorEventAbilities.Turquoise:
                Keyword2("Artisan");
                Ability(PromoEmperorEventAbilities.TurquoiseHonor);
                break;
        }
        Emit("abilityGranted", $"{name}: {Definition(chosen).Name} is permanently changed.", chosen.OwnerId, chosen.Id);
    }

    // The granted traits a card shows (MatchView's GrantedEffects): The Topaz Championship's.
    private IEnumerable<GrantedEffectView> PromoGrantedEffects(CardInstance card)
    {
        if (card.AbilityUses.ContainsKey(PromoEmperorEventAbilities.TopazTrait))
            yield return new GrantedEffectView("The Topaz Championship", "Before the first time each turn another player's card effect destroys this Personality: Negate the destruction.", "The Topaz Championship", "Permanent", "Trait");
    }

    // ---- the Championships' granted abilities (execution) ----------------------------------------------------------------------------------------------------------------------

    internal bool? RunPromoEvents(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case PromoEmperorEventAbilities.AmethystCopy:
                return BeginCopy(source!, target!, id, [CopyStep.Ability], player.Id, "ability", bowedBeforeAction, GrantDuration.Turn, "Political");
            case PromoEmperorEventAbilities.EmeraldStraighten:
                Straighten(target!);
                return false;
            case PromoEmperorEventAbilities.ObsidianBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (target!.Zone == Zone.Attached && InPlay(target)) DestroyTargetCard(target, player.Id);
                return false;
            case PromoEmperorEventAbilities.RubyBow:
                {
                    if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                    if (target is null || !InPlay(target) || !target.Dishonorable) return false;
                    var options = UnitCardsWithoutAttachments(target).Select(c => new EffectOption(c.Id, $"Destroy {Definition(c).Name}", [Batch1DestroyCardEffect + c.Id])).ToArray();
                    return BeginListedEffectChoice(command, options, $"{Definition(source!).Name}: choose the card without attachments in {Definition(target).Name}'s unit to destroy.",
                        consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                }
            case PromoEmperorEventAbilities.TopazDestroy:
                DestroyTargetCard(target!, player.Id);
                return false;
            default:
                return null;
        }
    }
}

public static class PromoEmperorEventStartup
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoEmperorEventHooks()
    {
        EngineHooks.Abilities.Register("AddPromoEmperorEvents", 12110, static abilities => abilities.AddPromoEmperorEvents());
        EngineHooks.ResolveEvent.Register("ResolvePromoEmperorEvent", 12110, static (engine, context) => engine.ResolvePromoEvent(context.Player, context.Province, context.Card));
        EngineHooks.PersonalityForce.Register("PromoEmperorEventForce", 12110, static (engine, context) => engine.PromoEventForce(context.card));
        EngineHooks.RunEffects.Register("RunPromoEmperorEvents", 12110, static (engine, context) =>
            engine.RunPromoEvents(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard,
                context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
