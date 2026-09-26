namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, PASS GROUP P2: search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels. Mendon asked for the remaining unwired cards of Before the Dawn, Seeds of Decay,
// Embers of War, Second City, Forgotten Legacy, The Shadow's Embrace, Emperor Edition and Honor and Treachery to be evaluated and wired where possible. Every card was read letter by letter against
// its scan (the scan is authoritative; where it differs from the catalogue's Oracle text the scan is published). Nothing here conflicts with docs/rules/EMPEROR_EDITION_CORE_RULES.md.
//
// New primitives (this file, EmperorP2Duel.cs, EmperorP2Hand.cs and EmperorP2Search.cs):
//   * ATTACK STRENGTH SCOPED TO A PLAYER (P2AttackStrengthBonus, read by EffectiveAttackStrength and by ResolveOrDeferAttack's override branch): a bonus to every Ranged Attack a player's card effects
//     make -- while a condition holds (Ikoma Yamahatsu: "While you control a Terrain, your Ranged Attacks have +1 strength", one per Yamahatsu in play) or for the rest of one battle (Surrounded!: "Ranged
//     Attacks from your card effects during this battle have +2 strength", MatchState.P2.BattleRangedBonuses keyed by the battle's number). A defender-side modifier needs no new code: "Melee Attacks
//     targeting cards in this unit have -2 strength" (Armor of the Uruwashii) is CardDefinition.AttackDefenseKind/Amount/Scope "Unit", the Destrier family's data.
//   * CONDITIONAL ONGOING EFFECTS JUDGED LIVE (P2Force, P2StaticGrantedAbilityIds): an effect that lasts until the end of the turn and applies only while a condition on the army holds, re-judged every
//     time the stat or the ability list is read -- Kaiu Nakano's +3F "while he is in an army and while all Personalities in that army are Siege" and Brothers in Battle's +1F and granted "Battle:
//     Ranged 4 Attack" "while in an army with one or more of the others".
//   * A PROVINCE STRENGTH SET TO A VALUE (LowerProvinceStrengthToZero): the core rules' Set, "give a stat a bonus/penalty sufficient to reach the stated value", as a timed province strength penalty
//     (until the end of the turn: Mendon's R3 durations ruling) of exactly the current strength (Shrine to Nimuro).
//   * A HOLDING GOLD COST MINIMUM (P2GoldCostMinimum, read by CardGoldCost): Growing Hostility's "Until the game ends, Holdings in and out of play have a minimum Gold Cost of 1" -- a stat minimum laid on
//     top of every other modifier, for every Holding wherever it is.
//   * A RAISED HONOR REQUIREMENT (KazutoshiHonorRequirementIncrease, read by QuotePurchase): "Other players' Personalities in and out of play have their Honor Requirements increased by 10" -- +10 per
//     Bayushi Kazutoshi another player controls, on any Personality a player recruits (a dash, no requirement, stays none).
//   * A CONDITIONAL HOLDING PRODUCTION BY THE CONTROLLER'S BOARD (ProductionCondition.ControllerCondition): Shrine to Nimuro's "Produce 2 Gold, or 3 Gold if you control a Tactician".
// ======================================================================================================================================================================================

/// <summary>What the P2 group keeps between commands (issue #503).</summary>
public sealed class EmperorP2State
{
    /// <summary>Surrounded!: "Ranged Attacks from your card effects during this battle have +2 strength" -- one entry per play (player, battle number, amount).</summary>
    public List<P2BattleBonus> BattleRangedBonuses { get; set; } = [];
    /// <summary>Kaiu Nakano's "+3F while he is in an army and while all Personalities in that army are Siege": the Personality and the turn it lasts.</summary>
    public List<P2TurnMark> NakanoBonuses { get; set; } = [];
    /// <summary>Brothers in Battle: the two or three Samurai of one play and the turn it lasts.</summary>
    public List<P2BrothersGroup> BrothersGroups { get; set; } = [];
    /// <summary>Growing Hostility: "Until the game ends, Holdings in and out of play have a minimum Gold Cost of 1."</summary>
    public bool HoldingGoldCostMinimum { get; set; }
    /// <summary>Kakita Gonji's "Once per duel": "duel id|Gonji id" of each use.</summary>
    public List<string> GonjiUses { get; set; } = [];
    /// <summary>How many times each player replaced a focus this duel (Gonji): counted against the four-focus cap (core rules: "traits/actions that substitute 'instead of focusing' count against this cap").</summary>
    public Dictionary<string, int> FocusSubstitutions { get; set; } = [];
    /// <summary>The ordered Focus Effect being asked before the duel's totals (EmperorP2Duel.cs).</summary>
    public int PreFocusIndex { get; set; }
    /// <summary>Killing Intent: "duel id|Personality id" of each Personality using his base Force as his duel stat.</summary>
    public List<string> DuelStatBaseForce { get; set; } = [];
    /// <summary>The performers of each action whose effects are resolving ("ability id|player id"), read when it is done (The Agony of Her Gaze).</summary>
    public Dictionary<string, List<string>> Performers { get; set; } = [];
}

public sealed record P2BattleBonus(int PlayerId, int Battle, int Amount);
public sealed record P2TurnMark(string CardId, int Turn);
public sealed record P2BrothersGroup(string[] CardIds, int Turn);

public static class EmperorP2Abilities
{
    public const string HonestAssessment = "p2-honest-assessment-bow";
    public const string UruwashiiNegateMove = "p2-uruwashii-negate-move";
    public const string YamahatsuAttack = "p2-yamahatsu-ranged";
    public const string SurroundedRecon = "p2-surrounded-recon";
    public const string ShrineNimuroLower = "p2-shrine-nimuro-lower";
    public const string NakanoForce = "p2-nakano-force";
    public const string BrothersInBattle = "p2-brothers-in-battle";
    public const string BrothersRanged = "p2-brothers-ranged-4";
    public const string KillingIntentFocus = "p2-killing-intent-focus";
    public const string KillingIntentStraighten = "p2-killing-intent-straighten", KillingIntentStraightenHome = "p2-killing-intent-straighten-home";
    public const string ChampionFocus = "p2-champion-court-focus", ChampionRevenge = "p2-champion-court-revenge";
    public const string TurningAwayFocus = "p2-turning-away-focus", TurningAwayRaise = "p2-turning-away-raise", TurningAwayLower = "p2-turning-away-lower";
    public const string SpiritOfTheShadows = "p2-spirit-shadows", ShadowsSwitch = "p2-shadows-switch";
    public const string MinaDestroy = "p2-mina-destroy";
    public const string DelvingSearch = "p2-delving-search";
    public const string TapestryLook = "p2-tapestry-look";
    public const string TapestryEffect = "p2-tapestry-hand-size";
    public const string AgonyEffect = "p2-agony-of-her-gaze";
    public const string GainingAdvantageEffect = "p2-gaining-advantage";

    // Card Effects.
    public const string YamahatsuEffect = "p2-yamahatsu-terrain-ranged-bonus";
    public const string ShrineNimuroGold = "p2-shrine-nimuro-gold";
    public const string KazutoshiEffect = "p2-kazutoshi-honor-requirement";
    public const string GonjiEffect = "p2-gonji-focus";
    public const string GrowingHostilityEffect = "p2-growing-hostility";

    // Target and performer categories.
    public const string EnemyPersonalityHonorLtePerformer = "p2-enemy-personality-honor-lte-performer";
    public const string OwnSamuraiPerformer = "p2-own-samurai-performer";
    public const string MinaTarget = "p2-mina-attachment-or-token-holder";

    /// <summary>The Sensei among this group's Personalities (their "After X enters play: Discard all your other Sensei Personalities").</summary>
    public static bool IsSenseiEntryEffect(string effect) => effect is KazutoshiEffect or GonjiEffect;

    public static Dictionary<string, AbilityDefinition> AddEmperorP2(this Dictionary<string, AbilityDefinition> all)
    {
        // Honest Assessment (oracle-l5r-3415, Embers of War, Bushido Virtue * Honesty): "Battle: Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal
        // Honor: Bow him. Gain 1 Honor, or 3 Honor if your Family Honor is 9 or less." Family Honor is the player's Honor total, read as the action resolves (after the bow).
        all[HonestAssessment] = new("Battle", "Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 1 Honor, or 3 Honor if your Family Honor is 9 or less",
            EnemyPersonalityHonorLtePerformer, Performer: "own-unbowed-samurai-at-battle");
        // Armor of the Uruwashii (oracle-l5r-453, Embers of War, an Imperial Armor Item, +3F/+0C): "Melee Attacks targeting cards in this unit have -2 strength." (AttackDefense data, the packager)
        // "Reaction: Before another player's action resolves: Negate this Personality's movement from the action's effects." -- an Item's action, performed by its wielder; offered when the action
        // would move him (a Reaction that could not negate anything opens no window).
        all[UruwashiiNegateMove] = new("Reaction", "Before another player's action resolves: Negate this Personality's movement from the action's effects", "none", Trigger: "before-action-resolves");
        // Ikoma Yamahatsu (oracle-l5r-3660, Embers of War): "While you control a Terrain, your Ranged Attacks have +1 strength." (his Effect) "Battle: Ranged 4 Attack."
        all[YamahatsuAttack] = new("Battle", "Ranged 4 Attack", "attack");
        // Surrounded! (oracle-l5r-7668, Embers of War, a Recon Strategy): "Reaction: After engaging, choose your performing unbowed Scout at the current battlefield: You have Reconnaissance there.
        // Ranged Attacks from your card effects during this battle have +2 strength."
        all[SurroundedRecon] = new("Reaction", "After engaging, choose your performing unbowed Scout at the current battlefield: You have Reconnaissance there. Ranged Attacks from your card effects during this battle have +2 strength",
            "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Scout"], Trigger: "after-engaging");
        // Shrine to Nimuro (oracle-l5r-7227, Embers of War, a Unique Temple Holding; the scan): "Bow this card: Produce 2 Gold, or 3 Gold if you control a Tactician." (its production)
        // "Limited: Bow this card and your performing Lion Clan Tactician, and target a province: Lower its strength to 0." -- any player's province; until the end of the turn (R3).
        all[ShrineNimuroLower] = new("Limited", "Bow this card and your performing Lion Clan Tactician, and target a province: Lower its strength to 0", "any-province",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Lion Clan", "Tactician"], Costs: [new(CostVerb.Bow), new(CostVerb.Bow, CostSubject.Performer)]);
        // Kaiu Nakano (oracle-l5r-4127, Before the Dawn): "Tactical Battle/Open: Nakano has +3F while he is in an army and while all Personalities in that army are Siege." Performed by Nakano.
        all[NakanoForce] = new("BattleOrOpen", "Tactical Battle/Open: Nakano has +3F while he is in an army and while all Personalities in that army are Siege", "none", ActionKeywords: ["Tactical"]);
        // Brothers in Battle (oracle-l5r-1164, Before the Dawn, Bushido Virtue * Duty): "Battle/Open: Choose two or three of your performing Samurai: Each of them has +1F and the ability, "Battle:
        // Ranged 4 Attack" while in an army with one or more of the others." Two or three performing slots (no "unbowed" is printed); the effect lasts until the end of the turn.
        all[BrothersInBattle] = new("BattleOrOpen", "Choose two or three of your performing Samurai: Each of them has +1F and the ability, \"Battle: Ranged 4 Attack\" while in an army with one or more of the others",
            "slots", Slots: [new("samurai", OwnSamuraiPerformer, "Two or three of your performing Samurai", 2, 3, Performing: true)]);
        // The granted ability (StaticGrantedAbilityIds while the condition holds): performed by the Samurai who has it.
        all[BrothersRanged] = new("Battle", "Ranged 4 Attack (Brothers in Battle)", "attack");
        // Killing Intent (oracle-l5r-4352, Embers of War): "As a Focus Effect: You may choose (now) to use the base Force of your Personality in this duel as his duel stat if he is at a battlefield."
        // (EmperorP2Duel.cs) "Battle: Choose your performing Personality: Straighten him. You may move him home." -- the "may" is two ways sharing one use.
        all[KillingIntentFocus] = new("FocusEffect", "As a Focus Effect: You may choose (now) to use the base Force of your Personality in this duel as his duel stat if he is at a battlefield", "none");
        all[KillingIntentStraighten] = new("Battle", "Choose your performing Personality: Straighten him", "none", Performer: "own-personality-at-battle", UsageKey: "p2-killing-intent");
        all[KillingIntentStraightenHome] = new("Battle", "Choose your performing Personality: Straighten him. Move him home", "none", Performer: "own-personality-at-battle", UsageKey: "p2-killing-intent");
        // A Champion in Court (oracle-l5r-6, Before the Dawn; the scan): "As a Focus Effect: You may substitute one of your Yojimbo Personalities for your Personality in this duel." (EmperorP2Duel.cs)
        // "Reaction: After another player's action destroys one of your Courtier or Shugenja Personalities, choose your performing Yojimbo Personality: Give him three +1F Revenge tokens."
        all[ChampionFocus] = new("FocusEffect", "As a Focus Effect: You may substitute one of your Yojimbo Personalities for your Personality in this duel", "none");
        all[ChampionRevenge] = new("Reaction", "After another player's action destroys one of your Courtier or Shugenja Personalities, choose your performing Yojimbo Personality: Give him three +1F Revenge tokens",
            "none", Performer: "own-personality-with-keyword", RequiredKeywords: ["Yojimbo"], Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Turning Away (oracle-l5r-10173, Seeds of Decay, Kiho): "As a Focus Effect: You may discard your Personality in this duel (ending the duel without resolution)." (EmperorP2Duel.cs)
        // "Battle: Even if you control no units at the current battlefield, choose your performing Monk at any location and bow your target Ring: Give the current battlefield's province +3 or -3
        // strength." -- Absent; the +3 and -3 are two ways sharing one use; until the end of the turn (R3).
        all[TurningAwayFocus] = new("FocusEffect", "As a Focus Effect: You may discard your Personality in this duel (ending the duel without resolution)", "none");
        all[TurningAwayRaise] = new("Battle", "Even if you control no units at the current battlefield, choose your performing Monk at any location and bow your target Ring: Give the current battlefield's province +3 strength",
            "own-unbowed-ring", Performer: "own-personality-with-keyword", RequiredKeywords: ["Monk"], Absent: true, Costs: ActionCost.BowTarget, UsageKey: "p2-turning-away");
        all[TurningAwayLower] = new("Battle", "Even if you control no units at the current battlefield, choose your performing Monk at any location and bow your target Ring: Give the current battlefield's province -3 strength",
            "own-unbowed-ring", Performer: "own-personality-with-keyword", RequiredKeywords: ["Monk"], Absent: true, Costs: ActionCost.BowTarget, UsageKey: "p2-turning-away");
        // Spirit of the Shadows (oracle-l5r-7427, Before the Dawn): "Limited: Choose your performing unbowed Personality: If he is a Ninja, permanently give him the ability, "Ninja Battle: Target two
        // Personalities controlled by the same player at different locations: Switch their locations." Otherwise, permanently give him Ninja." (GrantSpirit, the Spirit Strategies' shape)
        all[SpiritOfTheShadows] = new("Limited", "Choose your performing unbowed Personality: If he is a Ninja, permanently give him the ability, \"Ninja Battle: Target two Personalities controlled by the same player at different locations: Switch their locations.\" Otherwise, permanently give him Ninja",
            "none", Performer: "own-unbowed-personality");
        // The switch is Sudden Movement's (The Shadow's Embrace pass, issue #476 batch 4): both movements or neither. Unlike that Strategy it is the Ninja's own ability, performed by him at the
        // battle with his player's presence (not Absent: a reading for Mendon).
        all[ShadowsSwitch] = new("Battle", "Ninja Battle: Target two Personalities controlled by the same player at different locations: Switch their locations", "slots", ActionKeywords: ["Ninja"],
            Slots: [new("first", "personality-with-switch-partner", "A Personality"), new("second", "personality-same-controller-as-first-elsewhere", "Another Personality of the same player's, at a different location")]);
        // Isawa Mina (oracle-l5r-3861, Embers of War): "Battle: Target an attachment or token at the current battlefield: Destroy it. If this destroyed a card or token, give Mina two +1F Fire tokens."
        // The target is a card at the current battlefield: an attachment (Choice "card") or the card holding the token (Choice "token:<id>").
        all[MinaDestroy] = new("Battle", "Target an attachment or token at the current battlefield: Destroy it. If this destroyed a card or token, give Mina two +1F Fire tokens", MinaTarget);
        // Delving into History (oracle-l5r-1945, Embers of War, a Void Spell): "Limited: Bow this Shugenja and destroy this card: Search your Fate deck for a non-Unique Spell that must share a keyword with
        // the performer unless he is Void, show it, and put it in your hand." (EmperorP2Search.cs)
        all[DelvingSearch] = new("Limited", "Bow this Shugenja and destroy this card: Search your Fate deck for a non-Unique Spell that must share a keyword with the performer unless he is Void, show it, and put it in your hand",
            "none", Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice)], Search: EmperorP2Searches.DelvingIntoHistory);
        // The Tapestry Perceived (oracle-l5r-8371, Before the Dawn, a Void Spell): "While this Shugenja is Void, your maximum hand size is increased by 1." (its Effect) "Limited: Bow this card: Look at the
        // top card of any Fate deck. You may destroy this card; if you do, discard the card you looked at." (a look whose discard destroys the Spell first: EmperorP2Search.cs)
        all[TapestryLook] = new("Limited", "Bow this card: Look at the top card of any Fate deck. You may destroy this card; if you do, discard the card you looked at", "none",
            Costs: ActionCost.BowSource, Look: EmperorP2Searches.TapestryPerceived);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private EmperorP2State P2 => _state.P2;

    // ---- hooks into the shared switches (CardAbilities.cs, MatchEngine.cs) ------------------------------------------------------------------------------------------------------------------------

    private static int? P2AttackStrength(string abilityId) => abilityId switch
    {
        EmperorP2Abilities.YamahatsuAttack or EmperorP2Abilities.BrothersRanged => 4,
        _ => null
    };

    private static string? P2AttackKind(string abilityId) => abilityId switch
    {
        EmperorP2Abilities.YamahatsuAttack or EmperorP2Abilities.BrothersRanged => "Ranged",
        _ => null
    };

    private static bool P2BowsTarget(string abilityId) => abilityId == EmperorP2Abilities.HonestAssessment;

    /// <summary>The strength every Ranged Attack of <paramref name="playerId"/>'s card effects gets on top of its own (Ikoma Yamahatsu, Surrounded!); 0 for a Melee Attack.</summary>
    private int P2AttackStrengthBonus(string abilityId, int playerId)
    {
        if (AttackKind(abilityId) != "Ranged") return 0;
        var bonus = 0;
        // Yamahatsu: a trait of each Yamahatsu in play (bowed or not), while his controller controls a Terrain.
        if (ControlsKeyword(playerId, "Terrain"))
            bonus += _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == EmperorP2Abilities.YamahatsuEffect);
        // Surrounded!: every play of this battle.
        if (_state.Phase == Phase.Battle && _state.CurrentBattleProvince is not null)
            bonus += P2.BattleRangedBonuses.Where(b => b.PlayerId == playerId && b.Battle == _state.BattleNumber).Sum(b => b.Amount);
        return bonus;
    }

    // Target categories (ValidateAbility's default chain; slot candidates are judged through it too).
    private bool EmperorP2TargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        switch (category)
        {
            // Honest Assessment: "an enemy Personality with equal or lower Personal Honor" than the performing Samurai.
            case EmperorP2Abilities.EnemyPersonalityHonorLtePerformer:
                return personality && target.OwnerId != player.Id && InPlay(target) && command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer)
                    && CardPersonalHonor(target) <= CardPersonalHonor(performer);
            // Brothers in Battle: "your performing Samurai" -- in a battle (a Battle action) at the current battlefield, in the Action Phase anywhere.
            case EmperorP2Abilities.OwnSamuraiPerformer:
                return personality && target.OwnerId == player.Id && InPlay(target) && Keyword(target, "Samurai") && (_state.Phase != Phase.Battle || AtBattle(target));
            // Isawa Mina: an attachment at the current battlefield, or a card there holding a token.
            case EmperorP2Abilities.MinaTarget:
                return InPlay(target) && AtBattle(target) && (target.Zone == Zone.Attached || target.Tokens.Count > 0);
            default:
                return false;
        }
    }

    // The per-card conditions of taking the action (ValidateAbility).
    private void ValidateEmperorP2(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Armor of the Uruwashii: the Reaction's card is the Armor, attached to the player's Personality, and the held action is another player's that would move him.
            case EmperorP2Abilities.UruwashiiNegateMove:
                Require(source is { Zone: Zone.Attached, AttachedTo: { } } && _state.PendingNegatableAbility is { } held && UruwashiiApplies(source!, held),
                    "Armor of the Uruwashii reacts before another player's action that would move this Personality.");
                break;
            // Kaiu Nakano: a Tactical action is performed by a Tactician (he is one, printed; a Nakano who lost it may not take it).
            case EmperorP2Abilities.NakanoForce:
                Require(source is not null && Keyword(source, "Tactician"), "A Tactical action must be performed by a Tactician.");
                break;
            case EmperorP2Abilities.MinaDestroy:
                if (command.TargetId is { } minaTargetId && _state.Cards.TryGetValue(minaTargetId, out var minaTarget))
                    Require(command.Choice == MinaCardChoice ? minaTarget.Zone == Zone.Attached
                        : command.Choice is { } tokenChoice && tokenChoice.StartsWith(MinaTokenChoicePrefix, StringComparison.Ordinal) && minaTarget.Tokens.Any(t => t.Id == tokenChoice[MinaTokenChoicePrefix.Length..]),
                        "Choose the attachment, or one of the tokens on the card.");
                break;
        }
    }

    // BeforeActionReactionApplies' hook for this group's in-play before-action Reactions; null when the id is not one of them.
    private bool? P2BeforeActionApplies(CardInstance card, string abilityId, GameCommand command) => abilityId switch
    {
        EmperorP2Abilities.UruwashiiNegateMove => UruwashiiApplies(card, command),
        _ => null
    };

    // "Before another player's action resolves": the Armor is in play on a Personality of its controller's, the held action is another player's action whose movement may be negated, and it would move him.
    private bool UruwashiiApplies(CardInstance armor, GameCommand command)
    {
        if (!InPlay(armor) || armor.Zone != Zone.Attached || armor.AttachedTo is not { } wielderId || command.PlayerId == armor.OwnerId) return false;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting) || MovementCannotBeNegated(command)) return false;
        return OwnPersonalitiesActionWouldMove(armor.OwnerId, command).Any(c => c.Id == wielderId);
    }

    // The execution switch's hook: null when the id is not this group's; false when its effects are done; true when a decision or an Attack's window opened and the action waits.
    private bool? RunEmperorP2(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            case EmperorP2Abilities.HonestAssessment:
            {
                var performer = Card(command.PerformerId!);
                if (target is not null && InPlay(target)) BowByEffect(target, player.Id);
                // "Gain 1 Honor, or 3 Honor if your Family Honor is 9 or less" (a dishonorable performer is rehonored instead: core rules, Dishonor).
                var amount = player.Honor <= 9 ? 3 : 1;
                GainHonorFromPerformer(performer, amount, Definition(source!).Name);
                return false;
            }
            case EmperorP2Abilities.UruwashiiNegateMove:
                NegateEffects(Card(source!.AttachedTo!), "move");
                Emit("uruwashiiNegatesMove", $"{Definition(source).Name}: {Definition(Card(source.AttachedTo!)).Name}'s movement from the action's effects is negated.", player.Id, source.Id);
                return false;
            case EmperorP2Abilities.YamahatsuAttack:
            case EmperorP2Abilities.BrothersRanged:
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            case EmperorP2Abilities.SurroundedRecon:
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                P2.BattleRangedBonuses.Add(new(player.Id, _state.BattleNumber, 2));
                Emit("surroundedBonus", $"{Definition(source!).Name}: {player.Name} has Reconnaissance at the current battlefield, and his Ranged Attacks have +2 strength during this battle.", player.Id, source!.Id);
                return false;
            case EmperorP2Abilities.ShrineNimuroLower:
                LowerProvinceStrengthToZero(command.TargetPlayerId!.Value, command.Province!.Value, Definition(source!).Name);
                return false;
            case EmperorP2Abilities.NakanoForce:
                P2.NakanoBonuses.Add(new(source!.Id, _state.TurnNumber));
                Emit("nakanoForce", $"{Definition(source).Name} has +3F this turn while he is in an army of Siege Personalities.", player.Id, source.Id);
                return false;
            case EmperorP2Abilities.BrothersInBattle:
            {
                var brothers = SlotCards(command, "samurai").Select(c => c.Id).ToArray();
                P2.BrothersGroups.Add(new(brothers, _state.TurnNumber));
                Emit("brothersInBattle", $"{Definition(source!).Name}: {string.Join(", ", brothers.Select(b => Definition(Card(b)).Name))} each have +1F and \"Battle: Ranged 4 Attack\" this turn while in an army with one or more of the others.", player.Id, source!.Id);
                return false;
            }
            case EmperorP2Abilities.KillingIntentStraighten:
            case EmperorP2Abilities.KillingIntentStraightenHome:
            {
                var performer = Card(command.PerformerId!);
                Straighten(performer);
                if (id == EmperorP2Abilities.KillingIntentStraightenHome && InPlay(performer)) MoveHome(performer, false);
                return false;
            }
            case EmperorP2Abilities.ChampionRevenge:
            {
                var yojimbo = Card(command.PerformerId!);
                for (var i = 0; i < 3; i++) CreateToken(yojimbo, "Revenge", source!.Id, "Force", 1);
                return false;
            }
            case EmperorP2Abilities.TurningAwayRaise:
            case EmperorP2Abilities.TurningAwayLower:
                if (CurrentProvinceIndex() is int turningProvince)
                    GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, turningProvince, id == EmperorP2Abilities.TurningAwayRaise ? 3 : -3, Definition(source!).Name);
                return false;
            case EmperorP2Abilities.SpiritOfTheShadows:
                GrantSpirit(Card(command.PerformerId!), "Ninja", EmperorP2Abilities.ShadowsSwitch, source!.Id, player.Id);
                return false;
            case EmperorP2Abilities.ShadowsSwitch:
            {
                var switchPlan = SwitchPlan(command);
                if (SwitchBlocked(switchPlan, Definition(source!).Name)) return false;
                foreach (var (mover, to) in switchPlan) { if (to is int province) TryMoveToBattlefield(mover, province); else MoveHome(mover, false); }
                Emit("locationsSwitched", $"{Definition(source!).Name}: {Definition(switchPlan[0].Card).Name} and {Definition(switchPlan[1].Card).Name} switched locations.", player.Id, source!.Id);
                return false;
            }
            case EmperorP2Abilities.MinaDestroy:
                MinaDestroys(command, player, source!, target!);
                return false;
            case EmperorP2Abilities.DelvingSearch:
                BeginDelvingSearch(ability, player, source!);
                return true;
            case EmperorP2Abilities.TapestryLook:
                BeginLook(ability.Look!, source!, player.Id, "ability");
                return true;
            default:
                return null;
        }
    }

    // ---- Isawa Mina ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    public const string MinaCardChoice = "card";
    public const string MinaTokenChoicePrefix = "token:";

    // The choices a command of this group's abilities carries (QueryActions offers one action per choice, each validated with every target): Mina's attachment, or one token at the battlefield.
    private (string Key, string Label)[]? P2ChoiceOptions(string abilityId)
    {
        if (abilityId != EmperorP2Abilities.MinaDestroy) return null;
        var options = new List<(string, string)> { (MinaCardChoice, "destroy the attachment") };
        if (_state.CurrentBattleProvince is not null)
            foreach (var holder in _state.Cards.Values.Where(c => InPlay(c) && c.Tokens.Count > 0 && AtBattle(c)).OrderBy(c => c.Id, StringComparer.Ordinal))
                foreach (var token in holder.Tokens)
                    options.Add(($"{MinaTokenChoicePrefix}{token.Id}", $"destroy the {token.Type} token on {Definition(holder).Name}"));
        return [.. options];
    }

    private void MinaDestroys(GameCommand command, PlayerState player, CardInstance mina, CardInstance target)
    {
        var destroyed = false;
        if (command.Choice == MinaCardChoice)
        {
            if (InPlay(target) && target.Zone == Zone.Attached)
            {
                DestroyTargetCard(target, player.Id);
                destroyed = !InPlay(target);
            }
        }
        else if (command.Choice is { } choice && target.Tokens.FirstOrDefault(t => t.Id == choice[MinaTokenChoicePrefix.Length..]) is { } token)
        {
            target.Tokens.Remove(token);
            destroyed = true;
            Emit("tokenDestroyed", $"{Definition(mina).Name} destroyed a {token.Type} token on {Definition(target).Name}.", player.Id, target.Id);
        }
        if (destroyed && InPlay(mina)) { CreateToken(mina, "Fire", mina.Id, "Force", 1); CreateToken(mina, "Fire", mina.Id, "Force", 1); }
    }

    // ---- Shrine to Nimuro -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Lower its strength to 0": Set (core rules Glossary) -- a penalty sufficient to reach 0, until the end of the turn. A ceiling (Times of Strife) can make the current strength read lower than what
    // the penalty works on, so the penalty is taken again until the strength reads 0 (at most a few steps: each one removes everything still above 0).
    private void LowerProvinceStrengthToZero(int ownerId, int provinceIndex, string source)
    {
        var province = Player(ownerId).Provinces.FirstOrDefault(p => p.Index == provinceIndex);
        if (province is null || province.Destroyed) return;
        for (var step = 0; step < 4 && EffectiveProvinceStrength(province) > 0; step++)
            GiveProvinceStrengthThisTurn(ownerId, provinceIndex, -EffectiveProvinceStrength(province), source);
    }

    // Shrine to Nimuro's production: "Produce 2 Gold, or 3 Gold if you control a Tactician" -- the controller's board, read when the Gold is produced.
    private ConditionalProduction? P2ConditionalProduction(CardInstance card) =>
        Definition(card).Effect == EmperorP2Abilities.ShrineNimuroGold && ControlsKeyword(card.OwnerId, "Tactician") ? new(3, ProductionCondition.ControllerCondition) : null;

    // ---- conditional ongoing effects: Kaiu Nakano, Brothers in Battle ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A Personality's Force from this group's live conditional effects (PersonalityForce's term).</summary>
    private int P2Force(CardInstance card)
    {
        if (card.Zone != Zone.Battlefield || card.ProvinceIndex is not int battlefield) return 0;
        var force = 0;
        // Kaiu Nakano: "+3F while he is in an army and while all Personalities in that army are Siege".
        if (P2.NakanoBonuses.Any(m => m.CardId == card.Id && m.Turn == _state.TurnNumber) && Army(card.OwnerId, battlefield).All(p => Keyword(p, "Siege")))
            force += 3 * P2.NakanoBonuses.Count(m => m.CardId == card.Id && m.Turn == _state.TurnNumber);
        // Brothers in Battle: +1F per play whose other members include one in his army.
        force += BrothersGroupsInArmy(card).Count();
        return force;
    }

    // The Brothers in Battle plays of this turn naming this Personality with at least one of the others in the same army (same side of the same battlefield).
    private IEnumerable<P2BrothersGroup> BrothersGroupsInArmy(CardInstance card)
    {
        if (card.Zone != Zone.Battlefield || card.ProvinceIndex is not int battlefield) yield break;
        foreach (var group in P2.BrothersGroups)
            if (group.Turn == _state.TurnNumber && group.CardIds.Contains(card.Id)
                && group.CardIds.Any(other => other != card.Id && _state.Cards.TryGetValue(other, out var brother) && brother.OwnerId == card.OwnerId && brother.Zone == Zone.Battlefield && brother.ProvinceIndex == battlefield))
                yield return group;
    }

    // The abilities this group grants while a condition holds (StaticGrantedAbilityIds' hook).
    private IEnumerable<string> P2StaticGrantedAbilityIds(CardInstance card)
    {
        if (BrothersGroupsInArmy(card).Any()) yield return EmperorP2Abilities.BrothersRanged;
    }

    // ---- Growing Hostility, Bayushi Kazutoshi --------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The minimum Gold Cost of a card (CardGoldCost): 1 for every Holding once Growing Hostility has resolved, else 0.</summary>
    private int P2GoldCostMinimum(CardInstance card) => P2.HoldingGoldCostMinimum && Definition(card).Type == CardType.Holding ? 1 : 0;

    /// <summary>Bayushi Kazutoshi: "Other players' Personalities in and out of play have their Honor Requirements increased by 10" -- +10 for each Kazutoshi in play controlled by a player other than
    /// the one whose Personality it is (<paramref name="playerId"/>, the recruiting player).</summary>
    private int KazutoshiHonorRequirementIncrease(int playerId) =>
        10 * _state.Cards.Values.Count(c => c.OwnerId != playerId && InPlay(c) && Definition(c).Effect == EmperorP2Abilities.KazutoshiEffect);

    // Growing Hostility (an Event resolved as its province is revealed): "Destroy all non-Unique Holdings with a base Gold Cost of 0. Until the game ends, Holdings in and out of play have a minimum
    // Gold Cost of 1." Every player's Holdings in play.
    private void ResolveGrowingHostility(PlayerState player, CardInstance card)
    {
        var doomed = _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Holding && !Keyword(c, "Unique") && Definition(c).GoldCost == 0).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Emit("growingHostility", $"{Definition(card).Name}: every non-Unique Holding with a base Gold Cost of 0 is destroyed ({doomed.Length}), and until the game ends Holdings have a minimum Gold Cost of 1.", player.Id, card.Id);
        if (doomed.Length > 0) DestroyUnits(doomed, false, player.Id);
        P2.HoldingGoldCostMinimum = true;
    }

    private static bool IsEmperorP2Event(string effect) => effect is EmperorP2Abilities.GrowingHostilityEffect or EmperorP2Abilities.GainingAdvantageEffect or EmperorP2Abilities.AgonyEffect or ScorpionWallEffect;

    // ResolveEvent's hook: false (the reveal carries on) unless a decision opened.
    private bool ResolveEmperorP2Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            case EmperorP2Abilities.GrowingHostilityEffect:
                province.CardId = null; card.ProvinceIndex = null;
                ResolveGrowingHostility(player, card);
                card.Zone = Zone.DynastyDiscard;
                Emit("eventResolved", $"{Definition(card).Name} resolved and was discarded.", player.Id, card.Id);
                Refill(player, province);
                return false;
            case EmperorP2Abilities.GainingAdvantageEffect:
                return BeginGainingAdvantage(player, card);
            case ScorpionWallEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until the game ends: other players' actions will not discard cards from {player.Name}'s hand, and after a player has resolved two actions that put cards in his hand in one phase, no more cards will be put into his hand that phase.");
                return false;
            case EmperorP2Abilities.AgonyEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s Events Phase two turns from now begins: after each Battle action resolves, each of {player.Name}'s Nonhumans who performed it gets a +1F Frenzy token.", eventsPhaseIn: 2);
                return false;
            default:
                return false;
        }
    }
}
