using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #734, wave batch 3 of the wirable-card plan (docs/decisions/2026-10-07-wirable-card-wave-plan.md): the stat-term and Experienced cards, wired on the merged P9 (stat terms, issue #672) and P23
// (ExperiencedCoM / Inexperienced, issue #672) primitives. Ability ids are "w734-", card Effects likewise. Readings for Mendon to confirm or correct are in
// docs/decisions/2026-10-08-wave-batch-3-stat-terms.md. Waves personality/item/spell/strategy/holding/event (tools/content/package_wave_734.py).
//
//   Mumoku no Oni, Shosuro Tanihara   "While [it] is attacking at a battlefield, its Province has a maximum strength equal to its printed strength."   ProvinceStrengthMax (TerrainStatics.cs)
//   Usagi Seki, the Untouchable        "Seki has +5F while other players' cards are resolving."                                                     PersonalityForce, against the resolving command
//   Isawa Norimichi - exp              "Your other Earth Shugenja have +1F." / "Battle: Give this Province +2 or -2 strength."                        PersonalityForce; w734-norimichi-raise / -lower
//   Kshatriya Artifacts                "Fudo and Fallen Personalities have -1F while opposing this Item."                                            PersonalityForce, read off the opposing attachments
//   Stonemason's Hammer                "This Personality has +1F while he is Siege." / "Battle, Bow this card: Give this Province +2 strength."      PersonalityForce; w734-hammer-province
//   Well-Defended Farm                 "Your Ashigaru Personalities and Followers have +1F while opposed." / "Bow this card: Produce 2 Gold."         PersonalityForce, FollowerForce; printed GoldProduction 2
//   Riding to the Rescue               "Your Cavalry Personalities and Followers have +1F while attacking and opposed (this turn)."                  an Event kept in play to its owner's next turn (ResolveEvent)
//   Armor of the Mad                   "This Personality has Fallen and a maximum Personal Honor of 0." / "Battle, Bow this card: Give one or two target enemy cards a Madness token."   AttachGrantsKeyword; a Personal Honor cap; w734-armor-madness
//   Utaku Ji-Yun - exp                 "Your dishonorable Paragons do not have a maximum Personal Honor of 0 from the rulebook." / "Reaction: After engaging at Ji-Yun's battlefield, target a Personality opposing her: He may not use his abilities."   w734-jiyun-lock
//   Bayushi Nitoshi - exp              "After Nitoshi enters play, all players lose 3 Honor." / "Ninja Battle: Melee 6 Attack."                         ApplyMandatoryEntryEffects; w734-nitoshi-attack
//   Hantei XVI - inexperienced         "You ignore Hantei XVI's Honor Requirement if you control the Imperial Favor." / "After Hantei XVI enters play, search your discard pile or Fate deck for a non-Unique Political Strategy."   P3's favor-ignores-honor-requirement; w734-hantei-search
//   Shosuro Chizuru                    "Open: Discard a card to give a target Personality a Force or Personal Honor bonus equal to the card's Focus Value. The target has a maximum Personal Honor of 4."   w734-chizuru-force / -honor
//   Ready the Defenses                 "Open: Choose your performing Personality and target a province: While at the province's battlefields, other players' Personalities and Followers have -1F and, if your performer is a Tactician, your Personalities and Followers have +1F."
//   Doji Hoturi - inexperienced        "Iaijutsu Battle: Hoturi challenges a target Personality. If the target is honorable, his controller may dishonor him and move him home to refuse the duel. Destroy the duel's loser."   w734-hoturi-duel
//   Isawa Tsuke                        "Fire Limited, Bow this card: Tsuke challenges a target Personality. Both players must focus at least once in the duel. Destroy the duel's loser."   w734-tsuke-duel
//   Iuchi Karasu - inexperienced       "Home Battle: Target two Personalities at any location controlled by the same player. Switch their locations. Straighten each one of them that is not Fallen or Nonhuman."   w734-karasu-switch
//   The Sky's Barrier                  "Battle: Destroy this card and target an attacking Personality: Give him Elite. He does not contribute Force while unbowed. If this Shugenja is Air, gain 2 Honor."   w734-barrier
//   Unseemly Alliance                  "Political Open: Bow your target unbowed Courtier. All Personalities have -1F for each Clan Alignment in their army (this turn)."
public static class Wave734Abilities
{
    public const string NorimichiRaise = "w734-norimichi-raise", NorimichiLower = "w734-norimichi-lower", HammerProvince = "w734-hammer-province", ArmorMadness = "w734-armor-madness",
        JiYunLock = "w734-jiyun-lock", NitoshiAttack = "w734-nitoshi-attack", HanteiSearch = "w734-hantei-search", ChizuruForce = "w734-chizuru-force", ChizuruHonor = "w734-chizuru-honor",
        ReadyDefenses = "w734-ready-defenses", AllianceBow = "w734-alliance-bow", HoturiDuel = "w734-hoturi-duel", TsukeDuel = "w734-tsuke-duel", KarasuSwitch = "w734-karasu-switch",
        Barrier = "w734-barrier";

    // Target categories of this file (TargetAllows).
    public const string JiYunTarget = "w734-jiyun-target", AllianceTarget = "w734-alliance-target";

    // Hantei XVI's rule is P3's "favor-ignores-honor-requirement" Effect (RecruitGates695.cs); the other cards' Effects follow.

    // The Effects the static terms are keyed on.
    public const string ProvinceCap = "w734-province-cap", Seki = "w734-seki", Norimichi = "w734-norimichi", Kshatriya = "w734-kshatriya", Hammer = "w734-hammer", Farm = "w734-farm", Riding = "w734-riding",
        Armor = "w734-armor", JiYun = "w734-jiyun", Nitoshi = "w734-nitoshi", Chizuru = "w734-chizuru", Alliance = "w734-alliance";

    /// <summary>Hantei's search is a printed "After Hantei XVI enters play" trait with no designator, so it is modelled with Reaction timing and Cherry Blossom Festival does not bar it (Mendon's R4 ruling).</summary>
    public static bool IsTrait(string abilityId) => abilityId == HanteiSearch;

    private static readonly SearchCriteria PoliticalStrategy = new("non-Unique Political Strategy", Types: [CardType.Strategy], Keywords: ["Political"], NonUnique: true);

    /// <summary>"A non-Unique Political Strategy" in the discard pile or the Fate deck.</summary>
    public static readonly SearchSpec HanteiPolitical = new("w734-hantei-search", "Search your discard pile or Fate deck for a non-Unique Political Strategy",
        [new SearchDeckOption(SearchDeck.FateDiscard, PoliticalStrategy, SearchEffect.ToHand), new SearchDeckOption(SearchDeck.Fate, PoliticalStrategy, SearchEffect.ToHand)], ChooseOne: true);

    public static Dictionary<string, AbilityDefinition> AddWave734(this Dictionary<string, AbilityDefinition> all)
    {
        all[NorimichiRaise] = new("Battle", "Battle: Give this Province +2 strength", "none");
        all[NorimichiLower] = new("Battle", "Battle: Give this Province -2 strength", "none");
        all[HammerProvince] = new("Battle", "Battle, bow this card: Give this Province +2 strength", "none", Costs: ActionCost.BowSource);
        all[ArmorMadness] = new("Battle", "Battle, bow this card: Give one or two target enemy cards a Madness token", "slots", Costs: ActionCost.BowSource,
            Slots: [new("targets", "enemy-card", "One or two enemy cards to take a Madness token", 1, 2)]);
        all[JiYunLock] = new("Reaction", "Reaction: After engaging at Ji-Yun's battlefield, target a Personality opposing her: He may not use his abilities", JiYunTarget, Trigger: "after-engaging-at-own-battlefield");
        all[NitoshiAttack] = new("Battle", "Ninja Battle: Melee 6 Attack", "attack", ActionKeywords: ["Ninja"]);
        all[HanteiSearch] = new("Reaction", "After Hantei XVI enters play: search your discard pile or Fate deck for a non-Unique Political Strategy", "none", Trigger: "own-enters-play", Search: HanteiPolitical);
        all[ChizuruForce] = new("Open", "Open: Discard a card to give a target Personality a Force bonus equal to the card's Focus Value. The target has a maximum Personal Honor of 4", "slots",
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))],
            Slots: [new("target", "personality", "A Personality"), new("discard", "hand", "A card in your hand to discard")]);
        all[ChizuruHonor] = new("Open", "Open: Discard a card to give a target Personality a Personal Honor bonus equal to the card's Focus Value. The target has a maximum Personal Honor of 4", "slots",
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))],
            Slots: [new("target", "personality", "A Personality"), new("discard", "hand", "A card in your hand to discard")]);
        all[HoturiDuel] = new("Battle", "Iaijutsu Battle: Hoturi challenges a target Personality. If the target is honorable, his controller may dishonor him and move him home to refuse the duel. Destroy the duel's loser",
            "enemy-personality", IssuesDuel: true, DuelRefusable: true, ActionKeywords: ["Iaijutsu"]);
        all[TsukeDuel] = new("Limited", "Fire Limited, Bow this card: Tsuke challenges a target Personality. Both players must focus at least once in the duel. Destroy the duel's loser",
            "enemy-personality", IssuesDuel: true, Costs: ActionCost.BowSource, ActionKeywords: ["Fire"]);
        all[KarasuSwitch] = new("Battle", "Home Battle: Target two Personalities at any location controlled by the same player. Switch their locations. Straighten each one of them that is not Fallen or Nonhuman", "slots", Home: true,
            Slots: [new("first", "personality-with-switch-partner", "A Personality"), new("second", "personality-same-controller-as-first-elsewhere", "Another Personality of the same player's, at a different location")]);
        all[Barrier] = new("Battle", "Battle: Destroy this card and target an attacking Personality: Give him Elite. He does not contribute Force while unbowed. If this Shugenja is Air, gain 2 Honor", "attacking-personality",
            Costs: [new(CostVerbs.Sacrifice)]);
        all[ReadyDefenses] = new("Open", "Open: Choose your performing Personality and target a province: While at the province's battlefields, other players' Personalities and Followers have -1F and, if your performer is a Tactician, your Personalities and Followers have +1F",
            "any-province", Performer: "own-personality");
        all[AllianceBow] = new("Open", "Political Open: Bow your target unbowed Courtier. All Personalities have -1F for each Clan Alignment in their army (this turn)", AllianceTarget,
            ActionKeywords: ["Political"]);
        return all;
    }
}

/// <summary>A Personal Honor ceiling on one Personality for one turn (Shosuro Chizuru: "The target has a maximum Personal Honor of 4").</summary>
public sealed class Wave734HonorCap
{
    public string CardId { get; set; } = "";
    public int Cap { get; set; }
    public int Turn { get; set; }
}

/// <summary>Ready the Defenses, played on a turn: the province it names, who played it, and whether the performer was a Tactician.</summary>
public sealed class Wave734Defense
{
    public int ProvinceOwnerId { get; set; }
    public int ProvinceIndex { get; set; }
    public int PlayerId { get; set; }
    public bool Tactician { get; set; }
    public int Turn { get; set; }
}

public sealed partial class MatchState
{
    // 22910-22919 is reserved for batch 3 of the wirable-card waves (issue #734). Each list holds effects of the current turn only and is pruned whenever one is added.
    [JsonPropertyOrder(22910)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Wave734HonorCap>? Wave734HonorCaps { get; set; }

    [JsonPropertyOrder(22911)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Wave734Defense>? Wave734Defenses { get; set; }

    /// <summary>The Personalities The Sky's Barrier was played on, by turn: they contribute no Force while unbowed that turn.</summary>
    [JsonPropertyOrder(22913)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Wave734HonorCap>? Wave734Barriers { get; set; }

    /// <summary>The turn number of each Unseemly Alliance played (one entry per copy; only this turn's count).</summary>
    [JsonPropertyOrder(22912)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Wave734AllianceTurns { get; set; }
}

public sealed partial class MatchEngine
{
    // Test seams (L5R.Core.Tests has InternalsVisibleTo).
    internal bool KeywordForTest(string cardId, string keyword) => Keyword(Card(cardId), keyword);

    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> Wave734InPlayWith(int playerId, string effect) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == effect);

    // The command whose effect is being applied right now: an ability resolving, or an Attack resolving.
    private GameCommand? Wave734ResolvingCommand() => _runningCommand ?? _attackCommandInResolution;

    // ---- province strength ceiling: Mumoku no Oni, Shosuro Tanihara -------------------------------------------------------------------------------------------------------------------------

    // "While [it] is attacking at a battlefield, its Province has a maximum strength equal to its printed strength": the Province is the Defender's at the battlefield the card is attacking at.
    // Like the other ceilings it limits what is read and never lowers the current strength.
    private bool Wave734ProvinceCapped(ProvinceState province)
    {
        var owner = _state.Players.FirstOrDefault(p => p.Provinces.Contains(province));
        if (owner is null) return false;
        return _state.Cards.Values.Any(c => c.OwnerId != owner.Id && c.Zone == Zone.Battlefield && c.ProvinceIndex == province.Index && IsAttacking(c)
            && Definition(c).Effect == Wave734Abilities.ProvinceCap);
    }

    // ---- Force terms ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private int Wave734PersonalityForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || !InPlay(card)) return 0;
        var total = 0;
        var effect = Definition(card).Effect;
        // Seki (the scan says "cards", the catalogue "actions"): another player's card is resolving -- one of its abilities or an Attack it made. Force totals outside any such resolution are his printed 2F.
        if (effect == Wave734Abilities.Seki && Wave734ResolvingCommand() is { } resolving && resolving.PlayerId != card.OwnerId) total += 5;
        // Norimichi: "Your other Earth Shugenja have +1F", once for each Norimichi (not himself).
        if (Keyword(card, "Earth") && Keyword(card, "Shugenja"))
            total += Wave734InPlayWith(card.OwnerId, Wave734Abilities.Norimichi).Count(n => n.Id != card.Id);
        // Kshatriya Artifacts: "Fudo and Fallen Personalities have -1F while opposing this Item" -- the Item is attached to an enemy of the Personality at the same battlefield.
        if (Keyword(card, "Fudo") || Keyword(card, "Fallen"))
            total -= StatTermOpposingCards(card).Count(c => c.Zone == Zone.Attached && Definition(c).Effect == Wave734Abilities.Kshatriya);
        // Stonemason's Hammer: "This Personality has +1F while he is Siege."
        if (Keyword(card, "Siege"))
            total += Attachments(card.Id).Count(c => Definition(c).Effect == Wave734Abilities.Hammer);
        total += Wave734FarmBonus(card) + Wave734RidingBonus(card, card) + Wave734DefenseForce(card) + Wave734AllianceForce(card);
        return total;
    }

    private int Wave734FollowerForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Follower || card.Zone != Zone.Attached || card.AttachedTo is not { } hostId || !_state.Cards.TryGetValue(hostId, out var host)) return 0;
        return Wave734FarmBonus(card) + Wave734RidingBonus(card, host) + Wave734DefenseForce(card);
    }

    // Ready the Defenses: a unit at the battlefield of the named province -- the Defender's province of that index -- has -1F when the Defense is another player's, +1F when it is the player's own and
    // its performer was a Tactician. A Follower stands where its Personality does.
    private int Wave734DefenseForce(CardInstance card)
    {
        if (_state.Wave734Defenses is not { Count: > 0 } defenses) return 0;
        var unit = Definition(card).Type == CardType.Follower ? WielderOf(card) : card;
        if (unit is not { Zone: Zone.Battlefield, ProvinceIndex: int battlefield }) return 0;
        var total = 0;
        foreach (var defense in defenses)
        {
            // The province is a battlefield only while its owner is the Defender; the units there are the Attacker's and the Defender's, and "other players'" are all the Defense's player does not control.
            if (defense.Turn != _state.TurnNumber || defense.ProvinceIndex != battlefield || defense.ProvinceOwnerId != 1 - _state.ActivePlayerId) continue;
            if (unit.OwnerId != defense.PlayerId) total--;
            else if (defense.Tactician) total++;
        }
        return total;
    }

    // Unseemly Alliance: "All Personalities have -1F for each Clan Alignment in their army (this turn)" -- the distinct Clan alignments among the Personalities of the army the Personality is in,
    // for each copy played this turn. A Personality at home is in no army.
    private int Wave734AllianceForce(CardInstance card)
    {
        if (_state.Wave734AllianceTurns is not { Count: > 0 } plays || Definition(card).Type != CardType.Personality || card.Zone != Zone.Battlefield || card.ProvinceIndex is not int battlefield) return 0;
        var copies = plays.Count(turn => turn == _state.TurnNumber);
        if (copies == 0) return 0;
        var alignments = Army(card.OwnerId, battlefield).SelectMany(ClanAlignments).Distinct(StringComparer.Ordinal).Count();
        return -copies * alignments;
    }

    // Well-Defended Farm: "Your Ashigaru Personalities and Followers have +1F while opposed", once for each Farm its controller has in play.
    private int Wave734FarmBonus(CardInstance card) =>
        Keyword(card, "Ashigaru") && Opposed(card) ? Wave734InPlayWith(card.OwnerId, Wave734Abilities.Farm).Count() : 0;

    // Riding to the Rescue: "Your Cavalry Personalities and Followers have +1F while attacking and opposed (this turn)", once for each copy in play. The Event is kept to its owner's next
    // turn, and a unit can only be attacking in its own controller's turn, so the bonus is the turn it resolved in.
    private int Wave734RidingBonus(CardInstance card, CardInstance unit) =>
        Keyword(card, "Cavalry") && IsAttacking(unit) && Opposed(card)
            ? _state.Cards.Values.Count(e => e.OwnerId == card.OwnerId && e.Zone == Zone.EventInPlay && Definition(e).Effect == Wave734Abilities.Riding)
            : 0;

    // ---- Personal Honor: Armor of the Mad, Shosuro Chizuru, Utaku Ji-Yun --------------------------------------------------------------------------------------------------------------------

    // The highest Personal Honor the Personality may read as, or null: Armor of the Mad's 0 while attached, Chizuru's 4 for the turn.
    private int? Wave734PersonalHonorCap(CardInstance card)
    {
        int? cap = null;
        if (Attachments(card.Id).Any(a => Definition(a).Effect == Wave734Abilities.Armor)) cap = 0;
        if (_state.Wave734HonorCaps is { } caps)
            foreach (var entry in caps)
                if (entry.CardId == card.Id && entry.Turn == _state.TurnNumber) cap = cap is int current ? Math.Min(current, entry.Cap) : entry.Cap;
        return cap;
    }

    // "Your dishonorable Paragons do not have a maximum Personal Honor of 0 from the rulebook": the Personality's controller has Utaku Ji-Yun in play.
    private bool Wave734ParagonsOverride(CardInstance card) =>
        Keyword(card, "Paragon") && Wave734InPlayWith(card.OwnerId, Wave734Abilities.JiYun).Any();

    // The Sky's Barrier: "He does not contribute Force while unbowed" -- the Personality contributes nothing to his army's total while the Spell's effect lasts (this turn). Elite, which it also gives him,
    // makes a BOWED Personality contribute, so the pair means he contributes only while bowed.
    private bool Wave734ContributesNothing(CardInstance card) =>
        !card.Bowed && _state.Wave734Barriers is { } barriers && barriers.Any(b => b.CardId == card.Id && b.Turn == _state.TurnNumber);

    // ---- duel outcomes: Hoturi, Tsuke --------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Destroy the duel's loser" (a tie has two losers, as every duel's does).
    private bool Wave734DuelOutcome(string abilityId, List<string> loserIds, int challengerPlayerId)
    {
        if (abilityId is not (Wave734Abilities.HoturiDuel or Wave734Abilities.TsukeDuel)) return false;
        DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId);
        return true;
    }

    // ---- entry: Bayushi Nitoshi ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "After Nitoshi enters play, all players lose 3 Honor." A trait without a designator, so mandatory like the entry Honor loss of every other card (ApplyMandatoryEntryEffects).
    private void ApplyWave734EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != Wave734Abilities.Nitoshi) return;
        foreach (var player in _state.Players.ToArray())
            GainHonor(player.Id, -3, $"{Definition(card).Name} entering play", card);
    }

    // ---- events ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? ResolveWave734Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (Definition(card).Effect != Wave734Abilities.Riding) return null;
        KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins: {player.Name}'s Cavalry Personalities and Followers have +1F while attacking and opposed.", turnBeginsIn: 1);
        return false;
    }

    // ---- target legality ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave734TargetAllows(TargetAllowsContext context)
    {
        // "Your target unbowed Courtier" (Unseemly Alliance): a Personality of the player's in play with the Courtier keyword, unbowed.
        if (context.ability.Target == Wave734Abilities.AllianceTarget)
            return Definition(context.target).Type == CardType.Personality && InPlay(context.target) && context.target.OwnerId == context.player.Id && !context.target.Bowed && Keyword(context.target, "Courtier");
        if (context.ability.Target != Wave734Abilities.JiYunTarget) return false;
        // "A Personality opposing her": an enemy Personality in play at the battlefield Ji-Yun is at.
        var target = context.target;
        return context.source is { Zone: Zone.Battlefield, ProvinceIndex: int battlefield } && Definition(target).Type == CardType.Personality && target.OwnerId != context.player.Id
            && target.Zone == Zone.Battlefield && target.ProvinceIndex == battlefield;
    }

    // ---- attacks: Nitoshi --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int? Wave734AttackStrength(string abilityId) => abilityId == Wave734Abilities.NitoshiAttack ? 6 : null;

    private static string? Wave734AttackKind(string abilityId) => abilityId == Wave734Abilities.NitoshiAttack ? "Melee" : null;

    // ---- effects -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave734(RunHookContext c)
    {
        var (id, source) = (c.id, c.source);
        switch (id)
        {
            case Wave734Abilities.NorimichiRaise or Wave734Abilities.NorimichiLower or Wave734Abilities.HammerProvince:
                var amount = id == Wave734Abilities.NorimichiLower ? -2 : 2;
                GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, _state.CurrentBattleProvince!.Value, amount, Definition(source!).Name);
                return false;
            case Wave734Abilities.ArmorMadness:
                foreach (var enemy in SlotCards(c.command, "targets").ToArray())
                    if (InPlay(enemy)) CreateToken(enemy, "Madness", id);
                return false;
            case Wave734Abilities.JiYunLock:
                LockActions(c.target!, source!.Id);
                return false;
            case Wave734Abilities.HanteiSearch:
                BeginSearch(c.ability.Search!, source!, c.player.Id, "ability", c.bowedBeforeAction);
                return true;
            case Wave734Abilities.ChizuruForce or Wave734Abilities.ChizuruHonor:
                var chizuruTarget = SlotCards(c.command, "target").First();
                var focus = FocusValueOf(SlotCards(c.command, "discard").First());
                ApplyModifier(chizuruTarget, source!.Id, id, id == Wave734Abilities.ChizuruForce ? "Force" : "PersonalHonor", focus);
                var caps = _state.Wave734HonorCaps ??= [];
                caps.RemoveAll(e => e.Turn != _state.TurnNumber);
                caps.Add(new() { CardId = chizuruTarget.Id, Cap = 4, Turn = _state.TurnNumber });
                return false;
            case Wave734Abilities.NitoshiAttack:
                return ResolveOrDeferAttack(id, c.player, c.target?.Id, source!.Id, c.consumedGrant);
            case Wave734Abilities.KarasuSwitch:
                var first = SlotCards(c.command, "first").Single();
                var second = SlotCards(c.command, "second").Single();
                SwitchLocations(first, second, Definition(source!).Name);
                // "Straighten each one of them that is not Fallen or Nonhuman" -- the Personalities, whether or not they moved.
                foreach (var switched in new[] { first, second })
                    if (InPlay(switched) && !Keyword(switched, "Fallen") && !Keyword(switched, "Nonhuman")) Straighten(switched);
                return false;
            case Wave734Abilities.Barrier:
                var barred = c.target!;
                GrantKeyword(barred, "Elite", id, GrantDuration.Turn);
                var marks = _state.Wave734Barriers ??= [];
                marks.RemoveAll(e => e.Turn != _state.TurnNumber);
                marks.Add(new() { CardId = barred.Id, Cap = 0, Turn = _state.TurnNumber });
                // "If this Shugenja is Air": the Spell was destroyed to pay for the action, so it is the host it had (LastHostId).
                if (source is { LastHostId: { } hostId } && _state.Cards.TryGetValue(hostId, out var shugenja) && Keyword(shugenja, "Air"))
                    GainHonor(c.player.Id, 2, Definition(source).Name, source);
                return false;
            case Wave734Abilities.ReadyDefenses:
                var defenses = _state.Wave734Defenses ??= [];
                defenses.RemoveAll(e => e.Turn != _state.TurnNumber);
                defenses.Add(new() { ProvinceOwnerId = c.command.TargetPlayerId!.Value, ProvinceIndex = c.command.Province!.Value, PlayerId = c.player.Id,
                    Tactician = c.command.PerformerId is not null && Keyword(Card(c.command.PerformerId), "Tactician"), Turn = _state.TurnNumber });
                return false;
            case Wave734Abilities.AllianceBow:
                c.target!.Bowed = true;
                var plays = _state.Wave734AllianceTurns ??= [];
                plays.RemoveAll(turn => turn != _state.TurnNumber);
                plays.Add(_state.TurnNumber);
                return false;
            default:
                return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave734Hooks()
    {
        EngineHooks.Abilities.Register("Wave734Abilities", 15300, static abilities => abilities.AddWave734());
        EngineHooks.PersonalityForce.Register("Wave734PersonalityForce", 15300, static (engine, context) => engine.Wave734PersonalityForce(context.card));
        EngineHooks.FollowerForce.Register("Wave734FollowerForce", 15300, static (engine, context) => engine.Wave734FollowerForce(context.card));
        EngineHooks.ResolveEvent.Register("Wave734Events", 15300, static (engine, context) => engine.ResolveWave734Event(context.Player, context.Province, context.Card));
        EngineHooks.RunEffects.Register("Wave734RunEffects", 15300, static (engine, context) => engine.RunWave734(context));
        EngineHooks.TargetAllows.Register("Wave734TargetAllows", 15300, static (engine, context) => engine.Wave734TargetAllows(context));
        EngineHooks.Searches.Register("Wave734Searches", 15300, static () => [Wave734Abilities.HanteiPolitical]);
        EngineHooks.AttackStrengthBefore.Register("Wave734AttackStrength", 15300, static (engine, context) => Wave734AttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("Wave734AttackKind", 15300, static context => Wave734AttackKind(context.abilityId));
        // Hoturi: "If the target is honorable, his controller may dishonor him and move him home to refuse the duel." Tsuke: "Both players must focus at least once in the duel."
        DuelRuleRegistry.ForAbility("Wave734HoturiDuelRules", 15300, Wave734Abilities.HoturiDuel,
            new DuelRules { RefusalCost = DuelRefusalCosts.DishonorAndMoveHome, RefusableWhen = DuelRefusableConditions.ChallengedHonorable });
        DuelRuleRegistry.ForAbility("Wave734TsukeDuelRules", 15300, Wave734Abilities.TsukeDuel, new DuelRules { MinFocusEach = 1 });
    }
}
