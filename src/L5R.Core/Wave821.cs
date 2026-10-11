using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #821: six cards Mendon's rulings of 2026-10-10 unblocked (docs/decisions/2026-10-10-six-ruled-cards.md; rulings R-042..R-047 in docs/rules/RULINGS.md). Ability ids are "w821-".
//
//   Hida Kaiji - exp       "Will not attack or defend with other Personalities, and other Personalities will not attack or defend with Kaiji. Kaiji has +2F while unopposed, or +1F for each
//                          Personality opposing him with 6 or lower printed Force."   a SoleUnit restriction (ArmyRestrictions.cs); a PersonalityForce term
//   Isawa Ryuzo            "After Ryuzo enters play, lose 2 Honor. Fire Open: Target two Personalities, each with more Madness tokens than printed Personal Honor. They will not attack or defend in
//                          the same army."   EntryHonorLoss; w821-ryuzo-pair (a stored NoSameArmy pair, one more per use, so pairs stack)
//   Chuda Kaito            "After Kaito enters play: Lose 2 Honor. Kaito has +3GC while the effects of a Spell action he performed are resolving."   EntryHonorLoss; +3 Gold Cost for the
//                          length of the Spell's effects (ExecuteAbilityEffects), read by CardGoldCost and by Damning Ceremony's "base Gold Cost"
//   Ikoma Satoru           "Limited: Show a card in your hand and choose another player: You offer it as a gift. He may put it in his hand; if he did, draw two cards. Otherwise, discard it, and
//                          Melee Attack with strength equal to its Focus Value targeting a Follower or Personality without Followers in his home."   w821-satoru-gift: Naoharu's Gift's offer
//                          (GiftOffer.cs), then an Attack in the other player's home (AttackTargetScope.EnemyHome)
//   Isawa Tsumaro          "Air Home Engage: Move your target Air Shugenja home. Create a 0F/3C/0PH Air - Illusion - Shugenja Personality at the current battlefield; remove him from the game after
//                          the battle ends. While the Illusion is there, abilities on the target's spells have Home."   a Home Engage (the Home flag on a Reaction, read here), the existing
//                          created-card machinery, and an IllusionLink that grants Home to the target's Spell actions while the Illusion stands at that battle's battlefield
//   The Governor's Estate  "Your face-down Dynasty cards in play are Holdings with the trait, 'Bow this card: Produce 2 Gold.' Twice per turn, after your Dynasty Phase begins: You may pay 2 Gold; if you
//                          did, turn a card in your province face-down (if needed) and put it into play bowed. / Limited: Gain 2 Honor. You may not declare an attack. / Battle: Even if this card is
//                          bowed, discard a card and target a Personality: Give him +2F or -2F."   Mendon's reading: the face-down card is not put into play; the card is removed from the game and
//                          a bowed Imperial Estate Holding (a created card: "Bow this card: Produce 2 Gold", cost 0) is created in the player's home. w821-governor-create, in a Dynasty-phase window.
public static class Wave821Abilities
{
    public const string RyuzoPair = "w821-ryuzo-pair", SatoruGift = "w821-satoru-gift", SatoruMelee = "w821-satoru-melee", TsumaroIllusion = "w821-tsumaro-illusion", GovernorCreate = "w821-governor-create", GovernorHonor = "w821-governor-honor", SadahakoHonor = "w821-sadahako-honor", SadahakoGift = "w821-sadahako-gift", GovernorUp = "w821-governor-force-up", GovernorDown = "w821-governor-force-down";
    public const string MadnessPersonality = "w821-madness-personality", OwnAirShugenja = "w821-own-air-shugenja", OwnProvinceCard = "w821-own-province-card";
    /// <summary>The trigger of the Governor's Estate's Reaction: after the owner's Dynasty Phase begins.</summary>
    public const string DynastyBegins = "w821-dynasty-begins";
    public const string DynastyWindow = "dynastyBeginReaction";
    public const string GiftSpec = "w821-satoru-gift-offer", SadahakoSpec = "w821-sadahako-gift-offer", MeleeTargetSpec = "w821-satoru-melee-target";
    public const string PairSlot = "pair";

    // The cards and the Effect ids their rules are keyed on.
    public const string Kaiji = "oracle-l5r-10831", Ryuzo = "oracle-l5r-10476", Kaito = "oracle-l5r-10320", Satoru = "oracle-l5r-10291", Tsumaro = "oracle-l5r-10863", Governor = "oracle-l5r-8106";
    public const string Sadahako = "oracle-l5r-10186", Illusion = "oracle-l5r-11346", ImperialEstate = "user-scan-imperial-estate";
    public const string KaijiEffect = "w821-hida-kaiji", KaitoEffect = "w821-chuda-kaito", GovernorEffect = "w821-governors-estate";

    /// <summary>Kaito's printed +3 Gold Cost.</summary>
    public const int KaitoGoldCost = 3;
    /// <summary>The Gold the Governor's Estate's Reaction costs.</summary>
    public const int GovernorPrice = 2;

    public static Dictionary<string, AbilityDefinition> AddWave821(this Dictionary<string, AbilityDefinition> all)
    {
        all[RyuzoPair] = new("Open", "Fire Open: Target two Personalities, each with more Madness tokens than printed Personal Honor. They will not attack or defend in the same army", "slots", ActionKeywords: ["Fire"],
            Slots: [new(PairSlot, MadnessPersonality, "Two Personalities, each with more Madness tokens than printed Personal Honor", 2, 2)]);
        all[SatoruGift] = new("Limited", "Limited: Show a card in your hand and choose another player: You offer it as a gift. He may put it in his hand; if he did, draw two cards. Otherwise, discard it, and Melee Attack with strength equal to its Focus Value targeting a Follower or Personality without Followers in his home", "hand");
        // The Attack itself: never printed on a card, only named by Satoru's refused gift. Its target is a card in the other player's home, chosen as the Attack is made.
        all[SatoruMelee] = new("Limited", "Melee Attack with strength equal to the discarded card's Focus Value, targeting a card in his home", "attack", Attack: new(TargetScope: AttackTargetScope.EnemyHome));
        all[TsumaroIllusion] = new("Reaction", "Air Home Engage: Move your target Air Shugenja home. Create a 0F/3C/0PH Air - Illusion - Shugenja Personality at the current battlefield; remove him from the game after the battle ends. While the Illusion is there, abilities on the target's spells have Home",
            OwnAirShugenja, Trigger: "after-engaging", ActionKeywords: ["Air"], Home: true, Move: new(MoveSpec.Target, MoveSpec.Home),
            Creates: new(CardType.Personality, Force: 0, Chi: 3, PersonalHonor: 0, Keywords: ["Air", "Illusion", "Shugenja"], ProxyId: Illusion, AtBattlefield: true, UntilBattleEnds: true));
        all[GovernorCreate] = new("Reaction", "After your Dynasty Phase begins, pay 2 Gold and target a card in your province: Remove it from the game and create a bowed Imperial Estate Holding in your home",
            OwnProvinceCard, Trigger: DynastyBegins, UsesPerTurn: 2, Cost: GoldCostRule.Fixed(GovernorPrice), Creates: new(CardType.Holding, ProxyId: ImperialEstate) { EntersBowed = true });
        all[GovernorHonor] = new("Limited", "Limited: Gain 2 Honor. You may not declare an attack", "none");
        // Sadahako's Artistry (a Fortune Celestial): the Honor half, and the gift half on the gift step Satoru shares.
        all[SadahakoHonor] = new("Limited", "Limited: If your Family Honor is 10 or lower: Gain 1 Honor", "none");
        all[SadahakoGift] = new("Limited", "Limited: Discard this card and target a card in your hand: You offer it as a gift. Show it to another player. He may accept, putting it into his hand; if he did, you gain Honor equal to its Focus Value. If he did not, discard it and draw 3 cards",
            "hand", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // "Battle: Even if this card is bowed, discard a card and target a Personality: Give him +2F or -2F": one printed action, two offered abilities that share one use.
        TargetSlot[] forceSlots = [new("target", "personality", "A Personality to give +2F or -2F"), new("discard", "hand", "A card from your hand, to discard (a cost)") { CostOnly = true }];
        all[GovernorUp] = new("Battle", "Even if this card is bowed, discard a card and target a Personality: Give him +2F", "slots", UsageKey: "w821-governor-force", Tireless: true, Slots: forceSlots, Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))]);
        all[GovernorDown] = new("Battle", "Even if this card is bowed, discard a card and target a Personality: Give him -2F", "slots", UsageKey: "w821-governor-force", Tireless: true, Slots: forceSlots, Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))]);
        return all;
    }
}

/// <summary>Tsumaro's Illusion and the shugenja it lends Home to: for one battle, at one battlefield.</summary>
public sealed record IllusionLink(string IllusionId, string ShugenjaId, int Battlefield, int BattleNumber);

public sealed class Wave821State
{
    public List<IllusionLink> Illusions { get; set; } = [];
    /// <summary>The turn whose Dynasty-phase window the player declined (the window is not opened again that turn).</summary>
    public int DynastyWindowDeclinedTurn { get; set; } = -1;
}

public sealed partial class MatchState
{
    // 23300-23399 is reserved for issue #821.
    [JsonPropertyOrder(23300)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Wave821State? Wave821 { get; set; }
}

public sealed partial class MatchEngine
{
    private Wave821State W821 => _state.Wave821 ??= new();

    // ---- target legality ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave821TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        switch (context.ability.Target)
        {
            // "each with more Madness tokens than printed Personal Honor"
            case Wave821Abilities.MadnessPersonality:
                return InPlay(target) && Definition(target).Type == CardType.Personality && TokenCount(target, "Madness") > Definition(target).PersonalHonor;
            // "your target Air Shugenja"
            case Wave821Abilities.OwnAirShugenja:
                return InPlay(target) && Definition(target).Type == CardType.Personality && target.OwnerId == context.player.Id && Keyword(target, "Air") && Keyword(target, "Shugenja");
            default:
                return false;
        }
    }

    // A card in one of the player's own provinces, face-up or face-down (the Governor's Estate): an out-of-play category, judged here and listed by Wave821Candidates.
    internal static bool IsWave821OutOfPlayTarget(string category) => category == Wave821Abilities.OwnProvinceCard;
    private bool Wave821OwnProvinceCardAllows(PlayerState player, CardInstance card) => card.OwnerId == player.Id && card.Zone == Zone.Province && Definition(card).Type != CardType.Event;
    private string?[]? Wave821Candidates(string category, int playerId) => category != Wave821Abilities.OwnProvinceCard ? null
        : _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Province && Definition(c).Type != CardType.Event).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave821Validate(ValidationHookContext context)
    {
        var (id, ability, command, source) = (context.id, context.ability, context.command, context.source);
        switch (id)
        {
            case Wave821Abilities.RyuzoPair:
                {
                    var pair = (command.Selections?.FirstOrDefault(s => s.Slot == Wave821Abilities.PairSlot)?.CardIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
                    Require(pair.Length == 2, "Target two different Personalities.");
                    break;
                }
            case Wave821Abilities.SadahakoHonor:
                Require(Player(command.PlayerId).Honor <= 10, "Your Family Honor must be 10 or lower.");
                break;
            case Wave821Abilities.SatoruGift:
            case Wave821Abilities.SadahakoGift:
                Require(command.TargetId is not null && Card(command.TargetId) is { Zone: Zone.Hand } gift && Player(command.PlayerId).Hand.Contains(gift.Id), "Choose a card in your hand to show and offer.");
                break;
        }
        // Home on a Reaction or Engage action (Mendon's ruling): it behaves as Home does on a Battle action, at the Reaction's own timing. The source's unit may be at home, or -- as for any action --
        // at the current battlefield; a unit standing at another battlefield is neither.
        if (ability is { Home: true, Timing: "Reaction" } && source is not null && (Definition(source).Type == CardType.Personality || source.Zone == Zone.Attached) && _state.CurrentBattleProvince is not null)
            Require(HomeWaivesSourceLocation(ability, source) || AtBattle(source), $"{Definition(source).Name} must be at the current battlefield, or at home (Home).");
    }

    // ---- effects --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave821(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case Wave821Abilities.RyuzoPair:
                {
                    var pair = SlotCards(c.command, Wave821Abilities.PairSlot).DistinctBy(x => x.Id).ToArray();
                    if (pair.Length == 2)
                        AddSameArmyPair(pair[0], pair[1], note: $"{Definition(source!).Name}: {Definition(pair[0]).Name} and {Definition(pair[1]).Name} will not attack or defend in the same army this turn.");
                    return false;
                }
            case Wave821Abilities.SatoruGift:
                return BeginGift(Wave821Abilities.GiftSpec, source!, player, target!, c.bowedBeforeAction);
            case Wave821Abilities.SadahakoGift:
                return BeginGift(Wave821Abilities.SadahakoSpec, source!, player, target!, c.bowedBeforeAction);
            case Wave821Abilities.SadahakoHonor:
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            case Wave821Abilities.TsumaroIllusion:
                {
                    // "Move your target Air Shugenja home" (nothing moves when he is home already), then "Create ... at the current battlefield".
                    if (target is { Zone: Zone.Battlefield }) ExecuteMove(c.command, c.ability, source, target);
                    var before = _state.Cards.Keys.ToHashSet(StringComparer.Ordinal);
                    return ExecuteCreate(c.ability, c.ability.Creates!, c.command, player, () => Wave821LinkIllusion(target!, before));
                }
            case Wave821Abilities.GovernorCreate:
                return Wave821GovernorCreate(c);
            case Wave821Abilities.GovernorHonor:
                GainHonorFromPerformer(source!, 2, Definition(source!).Name); player.MayNotAttackThisTurn = true;
                return false;
            case Wave821Abilities.GovernorUp:
            case Wave821Abilities.GovernorDown:
                {
                    var who = SlotCards(c.command, "target").FirstOrDefault();
                    if (who is not null && InPlay(who)) ApplyModifier(who, source!.Id, id, "Force", id == Wave821Abilities.GovernorUp ? 2 : -2);
                    return false;
                }
            default:
                return null;
        }
    }

    // ---- Tsumaro: the Illusion's grant ------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave821LinkIllusion(CardInstance shugenja, HashSet<string> cardsBefore)
    {
        if (_state.CurrentBattleProvince is not int battlefield) return;
        var links = W821.Illusions;
        links.RemoveAll(l => !_state.Cards.TryGetValue(l.IllusionId, out var old) || !InPlay(old));
        foreach (var illusion in _state.Cards.Values.Where(x => !cardsBefore.Contains(x.Id) && x.DefinitionId == Wave821Abilities.Illusion))
            links.Add(new(illusion.Id, shugenja.Id, battlefield, _state.BattleNumber));
    }

    /// <summary>"While the Illusion is there, abilities on the target's spells have Home." The Illusion must stand at the battlefield of the battle it was created in, and that battle must still be the current one:
    /// destroyed, sent home or moved to another battlefield it takes the grant with it, back at the battlefield it gives it again, and when the battle resolves it is gone for good (Mendon, 2026-10-10).</summary>
    private bool Wave821GrantsHome(CardInstance source)
    {
        if (_state.Wave821 is not { Illusions.Count: > 0 } state || source.Zone != Zone.Attached || Definition(source).Type != CardType.Spell || source.AttachedTo is not { } hostId) return false;
        return state.Illusions.Any(l => l.ShugenjaId == hostId && _state.CurrentBattleProvince == l.Battlefield && _state.BattleNumber == l.BattleNumber
            && _state.Cards.TryGetValue(l.IllusionId, out var illusion) && illusion is { Zone: Zone.Battlefield } && illusion.ProvinceIndex == l.Battlefield);
    }

    // ---- Chuda Kaito: +3 Gold Cost while the effects of a Spell action he performed resolve --------------------------------------------------------------------------------------------

    private readonly List<string> _wave821Resolving = [];

    private int Wave821GoldCost(CardInstance card) => _wave821Resolving.Count > 0 && _wave821Resolving.Contains(card.Id) ? Wave821Abilities.KaitoGoldCost : 0;

    /// <summary>ExecuteAbilityEffects' bracket: when the action is a Spell's and its performer is a Chuda Kaito, he has +3 Gold Cost until the effects are done. Returns the id to hand back to End.</summary>
    private string? Wave821BeginKaito(GameCommand command)
    {
        if (!_state.Cards.Values.Any(x => x.Zone != Zone.Hand && Definition(x).Effect == Wave821Abilities.KaitoEffect)) return null;
        if (command.CardId is not { } sourceId || !_state.Cards.TryGetValue(sourceId, out var source) || Definition(source).Type != CardType.Spell || command.AbilityId is not { } abilityId
            || !AbilityCatalog.All.TryGetValue(abilityId, out var ability)) return null;
        var performer = ActionPerformer(ability, command, source);
        if (!InPlay(performer) || Definition(performer).Effect != Wave821Abilities.KaitoEffect) return null;
        _wave821Resolving.Add(performer.Id);
        return performer.Id;
    }

    private void Wave821EndKaito(string? performerId)
    {
        if (performerId is not null) _wave821Resolving.Remove(performerId);
    }

    // ---- Hida Kaiji: his Force ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "+2F while unopposed, or +1F for each Personality opposing him with 6 or lower printed Force": at a battlefield, against the enemy army there.
    private int Wave821Force(CardInstance card)
    {
        if (Definition(card).Effect != Wave821Abilities.KaijiEffect || card.Zone != Zone.Battlefield || card.ProvinceIndex is not int battlefield) return 0;
        var opposing = Army(1 - card.OwnerId, battlefield).ToArray();
        return opposing.Length == 0 ? 2 : opposing.Count(o => Definition(o).Force <= 6);
    }

    // ---- The Governor's Estate ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave821HasGovernor(int playerId) => _state.Cards.Values.Any(x => x.OwnerId == playerId && x.Zone == Zone.Stronghold && Definition(x).Effect == Wave821Abilities.GovernorEffect);

    /// <summary>SetPhase's last step for the Dynasty Phase: the owner's window, opened only when he holds an Estate with a use to spend and could take the Reaction now. True when it is open.</summary>
    private bool Wave821OpenDynastyWindow()
    {
        if (_state.PendingChoice is not null || _state.WinnerId is not null) return false;
        var playerId = _state.ActivePlayerId;
        if (!Wave821HasGovernor(playerId) || _state.Wave821?.DynastyWindowDeclinedTurn == _state.TurnNumber) return false;
        var window = new PendingChoice(Wave821Abilities.DynastyWindow, playerId, null, "After your Dynasty Phase begins: you may use the Governor's Estate, or pass.");
        _state.PendingChoice = window;
        bool usable;
        try { usable = AbilityActions(playerId).Any(o => o.Enabled); }
        finally { _state.PendingChoice = null; }
        if (!usable) return false;
        _state.PendingChoice = window;
        _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
        Emit("reactionWindow", "After-Dynasty-Phase-begins reaction window opened.", playerId);
        return true;
    }

    private bool Wave821DynastyWindowOpen(GameCommand command) =>
        _state.PendingChoice is { Type: Wave821Abilities.DynastyWindow } pending && command.PlayerId == pending.PlayerId;

    private bool HandleWave821Window(string type, GameCommand command)
    {
        if (type != Wave821Abilities.DynastyWindow) return false;
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Use the Governor's Estate, or pass.");
        if (command.Type == "useAbility")
        {
            UseAbility(command);
            // The Reaction resolved: the window stays open for the second use while one remains and can be taken, and is otherwise closed.
            if (_state.PendingChoice is { Type: Wave821Abilities.DynastyWindow } open)
            {
                var playerId = open.PlayerId;
                var usable = AbilityActions(playerId).Any(o => o.Enabled);
                if (usable) { _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0; }
                else { _state.PendingChoice = null; _state.PriorityPlayerId = _state.ActivePlayerId; _state.ConsecutivePasses = 0; }
            }
            return true;
        }
        W821.DynastyWindowDeclinedTurn = _state.TurnNumber;
        _state.PendingChoice = null;
        _state.PriorityPlayerId = _state.ActivePlayerId; _state.ConsecutivePasses = 0;
        return true;
    }

    private IEnumerable<ActionOption>? Wave821WindowActions(PendingActionsContext context)
    {
        if (context.Choice.Type != Wave821Abilities.DynastyWindow) return null;
        if (context.PlayerId != context.Choice.PlayerId) return [];
        return AbilityActions(context.PlayerId).Concat([new ActionOption("declineReaction", "Pass reaction", true, null, new GameCommand("declineReaction", context.PlayerId, _state.Version))]);
    }

    /// <summary>"Pay 2 Gold; if you did, turn a card in your province face-down (if needed) and put it into play bowed", read as Mendon rules: the card is removed from the game (it stays face-down in the
    /// removed-from-the-game pile when it was face-down, and is shown there when it had been revealed) and a bowed Imperial Estate is created in the player's home. The province refills.</summary>
    private bool? Wave821GovernorCreate(RunHookContext c)
    {
        var card = c.target!;
        var owner = Player(card.OwnerId);
        var wasFaceUp = card.FaceUp;
        var (province, slot) = FindProvinceSlot(owner, card.Id);
        ClearProvinceSlot(province, slot);
        card.Zone = Zone.RemovedFromGame; card.ProvinceIndex = null; card.AttachedTo = null; card.FaceUp = wasFaceUp; card.Tokens.Clear();
        // A face-down card's identity stays hidden, in the log as in the pile.
        Emit("removedFromGame", wasFaceUp ? $"{Definition(card).Name} was removed from the game." : $"{owner.Name}'s face-down card in province {province.Index + 1} was removed from the game.", owner.Id, wasFaceUp ? card.Id : null);
        RefillProvinceSlot(owner, province, slot);
        return ExecuteCreate(c.ability, c.ability.Creates!, c.command, c.player);
    }

    // ---- registration ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave821Hooks()
    {
        EngineHooks.Abilities.Register("Wave821Abilities", 13821, static abilities => abilities.AddWave821());
        EngineHooks.TargetAllows.Register("Wave821TargetAllows", 13821, static (engine, context) => engine.Wave821TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave821Validate", 13821, static (engine, context) => engine.Wave821Validate(context));
        EngineHooks.RunEffects.Register("Wave821RunEffects", 13821, static (engine, context) => engine.RunWave821(context));
        EngineHooks.PersonalityForce.Register("Wave821Force", 13821, static (engine, context) => engine.Wave821Force(context.card));
        EngineHooks.AttackKind.Register("Wave821AttackKind", 13821, static context => context.abilityId == Wave821Abilities.SatoruMelee ? "Melee" : null);
        EngineHooks.PendingChoice.Register("Wave821Window", 13821, static (engine, type, command) => engine.HandleWave821Window(type, command));
        EngineHooks.PendingActions.Register("Wave821WindowActions", 13821, static (engine, context) => engine.Wave821WindowActions(context));
        RegisterWave821GiftHooks();

        // Hida Kaiji: "will not attack or defend with other Personalities, and other Personalities will not attack or defend with Kaiji".
        CardRestrictionCatalog.Register(Wave821Abilities.Kaiji, new StaticRestriction(RestrictionKind.SoleUnit));
    }
}
