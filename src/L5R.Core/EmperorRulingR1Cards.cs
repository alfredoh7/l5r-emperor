namespace L5R.Core;

// Emperor Edition rulings pass R1 (issue #492): the cards that were blocked on a battle without a province (EmperorRulingR1.cs has the engine model).
//   * Surprise Attack (oracle-l5r-7665, strategy-wave-62) and Chikushudo's Trickery (oracle-l5r-1323, spell-wave-25) are published.
//   * Shinobi Assault (oracle-l5r-9927, The Shadow's Embrace), Two Fronts (oracle-l5r-8922, Embers of War) and The Perfect Moment (oracle-l5r-8257, Forgotten Legacy) are wired to the same model and
//     published in strategy-wave-64 (issue #494: Mendon supplied their scans, which were read letter by letter against this wiring).
public static class EmperorRulingR1Abilities
{
    public const string OwnNonhumanPersonality = "r1-own-nonhuman-personality";
    public const string EnemyPersonalityAtBattleOrHome = "r1-enemy-personality-at-battle-or-home";
    public const string OwnUnbowedNonhumanSpirit = "r1-own-unbowed-nonhuman-spirit-personality";

    public static Dictionary<string, AbilityDefinition> AddEmperorRulingR1(this Dictionary<string, AbilityDefinition> all)
    {
        // Surprise Attack (oracle-l5r-7665): "Battle: Choose one or more of your performing Nonhuman Personalities at the current battlefield and target one of the enemy leader's Personalities there or
        // at home: Create a battlefield (not associated with any province). Move all the performers and targets there." The performers are a performing slot (Dislodge the Foe's shape); the target
        // is one Personality of the other player (the enemy leader), at the current battlefield or at home. The battlefield joins the ones still to be fought this Attack Phase: no attack is declared,
        // so the Attacker and the Defender stay the current ones.
        all["surprise-attack-create"] = new("Battle", "Choose one or more of your performing Nonhuman Personalities at the current battlefield and target one of the enemy leader's Personalities there or at home: Create a battlefield (not associated with any province). Move all the performers and targets there", "slots",
            Slots: [new("performers", OwnNonhumanPersonality, "Your performing Nonhuman Personalities at the current battlefield", 1, 12, Performing: true),
                    new("target", EnemyPersonalityAtBattleOrHome, "One of the enemy leader's Personalities there or at home")]);
        // Chikushudo's Trickery (oracle-l5r-1323, an Earth Forest Spell): "Open: Choose one or two of your performing unbowed Nonhuman Spirit Personalities, target another player's unbowed Personality, bow this
        // Shugenja, and destroy this card: Create a battlefield. Negate all movement to it. Assign the target to attack there; he will not be destroyed during the battle's Combat Segment. Assign the
        // performing Spirits to defend there. Fight a battle there." Fought inside the Action Phase with the target's controller as the Attacker (Mendon: the card names the attacker).
        all["chikushudo-trickery-battle"] = new("Open", "Choose one or two of your performing unbowed Nonhuman Spirit Personalities, target another player's unbowed Personality, bow this Shugenja, and destroy this card: Create a battlefield. Negate all movement to it. Assign the target to attack there; he will not be destroyed during the battle's Combat Segment. Assign the performing Spirits to defend there. Fight a battle there", "slots",
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice)],
            Slots: [new("performers", OwnUnbowedNonhumanSpirit, "Your performing unbowed Nonhuman Spirit Personalities", 1, 2, Performing: true),
                    new("target", "enemy-unbowed-personality", "Another player's unbowed Personality")]);
        // Shinobi Assault (oracle-l5r-9927, strategy-wave-64): "Limited: Choose your performing unbowed Ninja Personality and target another player's Personality: Create a battlefield (not at any
        // province). Assign your Personality to attack there. Assign the target to defend there, even if he is bowed. Fight a battle there, in which you have the first opportunity to take a Battle
        // action or pass. Lose 5 Honor."
        all["shinobi-assault-battle"] = new("Limited", "Choose your performing unbowed Ninja Personality and target another player's Personality: Create a battlefield (not at any province). Assign your Personality to attack there. Assign the target to defend there, even if he is bowed. Fight a battle there, in which you have the first opportunity to take a Battle action or pass. Lose 5 Honor", "enemy-personality",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"]);
        // Two Fronts (oracle-l5r-8922, strategy-wave-64): "Limited: Target a province whose player has two or more provinces: During your next normal Attack Phase (this turn), create two battlefields
        // associated with it instead of one."
        all["two-fronts-mark"] = new("Limited", "Target a province whose player has two or more provinces: During your next normal Attack Phase (this turn), create two battlefields associated with it instead of one", "enemy-province");
        // The Perfect Moment (oracle-l5r-8257, strategy-wave-64): "Reaction: After the end of an Attack Phase in which you were the Defender: Create an additional Attack Phase with the previous phase's Attacker
        // as the Defender. During this Attack Phase, you will not destroy more than one of the Defender's provinces and your Personalities may not assign and will not move unless they are Paragons or Battle
        // Maidens." Retribution's window and additional Attack Phase (EmperorRemainingC.cs): its player declares it and is its Attacker.
        all["perfect-moment-declare"] = new("Reaction", "After the end of an Attack Phase in which you were the Defender: Create an additional Attack Phase with the previous phase's Attacker as the Defender. During this Attack Phase, you will not destroy more than one of the Defender's provinces and your Personalities may not assign and will not move unless they are Paragons or Battle Maidens", "none",
            Trigger: EmperorRemainingCAbilities.AttackPhaseEnd);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private static bool IsEmperorRulingR1Ability(string id) => id is "surprise-attack-create" or "chikushudo-trickery-battle" or "shinobi-assault-battle" or "two-fronts-mark" or "perfect-moment-declare";

    // The in-play Target categories of this pass (the shared category switch's fallback): false for any category that is not one of them.
    private bool RulingR1TargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        return category switch
        {
            // Surprise Attack: "your performing Nonhuman Personalities at the current battlefield" (AtBattle is asked by the shared Battle-target rule).
            EmperorRulingR1Abilities.OwnNonhumanPersonality => personality && own && Keyword(target, "Nonhuman"),
            // Surprise Attack: "one of the enemy leader's Personalities there or at home": the other player's, at the current battlefield or at home.
            EmperorRulingR1Abilities.EnemyPersonalityAtBattleOrHome => personality && !own && (AtBattle(target) || target.Zone == Zone.Home),
            // Chikushudo's Trickery: "your performing unbowed Nonhuman Spirit Personalities".
            EmperorRulingR1Abilities.OwnUnbowedNonhumanSpirit => personality && own && !target.Bowed && Keyword(target, "Nonhuman") && Keyword(target, "Spirit"),
            _ => false
        };
    }

    // Per-id conditions of this pass's abilities (ValidateAbility's hook; also run for each candidate of a slot, so a slot's card is judged against the whole ability).
    private void ValidateEmperorRulingR1(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // A battle fought inside the Action Phase cannot itself be started inside another one (the Action Phase is where Open and Limited actions are taken at all).
            case "chikushudo-trickery-battle":
            {
                Require(_state.Phase == Phase.Action && R1.Aside is null, "A battle can only be fought from the Action Phase.");
                // Every unit named must be one the effect could assign: a movement lock on assigning bars it.
                foreach (var spirit in SlotCards(command, "performers")) Require(MayBeAssignedToCreatedBattlefield(spirit, attacking: false, mayBeBowed: false), $"{Definition(spirit).Name} may not be assigned to a battlefield.");
                foreach (var target in SlotCards(command, "target")) Require(MayBeAssignedToCreatedBattlefield(target, attacking: true, mayBeBowed: false), $"{Definition(target).Name} may not be assigned to attack.");
                break;
            }
            case "shinobi-assault-battle":
            {
                Require(_state.Phase == Phase.Action && R1.Aside is null, "A battle can only be fought from the Action Phase.");
                if (command.PerformerId is not null) Require(MayBeAssignedToCreatedBattlefield(Card(command.PerformerId), attacking: true, mayBeBowed: false), "The performing Ninja may not be assigned to attack.");
                if (command.TargetId is not null) Require(MayBeAssignedToCreatedBattlefield(Card(command.TargetId), attacking: false, mayBeBowed: true), "The target may not be assigned to defend.");
                break;
            }
            case "two-fronts-mark":
                Require(Player(1 - player.Id).Provinces.Count(p => !p.Destroyed) >= 2, "The province's player must have two or more provinces.");
                break;
        }
    }

    // True when the action paused (a battle is fought inside the Action Phase) and finishes from there.
    private bool RunEmperorRulingR1Effect(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Surprise Attack: the created battlefield joins the battles still to be fought, and every performer and the target move there (each movement its own effect, negatable on its own).
            case "surprise-attack-create":
            {
                var battlefield = CreateAdditionalBattlefield(null);
                foreach (var performer in PerformingSlotCards(command, ability).ToArray()) TryMoveToBattlefield(performer, battlefield);
                if (SlotCards(command, "target").FirstOrDefault() is { } victim) TryMoveToBattlefield(victim, battlefield);
                return false;
            }
            case "chikushudo-trickery-battle":
            {
                var chosen = SlotCards(command, "target").Single();
                var spirits = PerformingSlotCards(command, ability).ToArray();
                SpendHandPlayedCard(player, source);
                var battlefield = CreateAdditionalBattlefield(null);
                R1.MovementNegated.Add(battlefield); // "Negate all movement to it."
                BeginAsideBattle(battlefield, chosen.OwnerId, command, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants, firstOpportunity: null, () =>
                {
                    if (AssignToCreatedBattlefield(chosen, battlefield, attacking: true, mayBeBowed: false)) R1.CombatShields.Add(chosen.Id); // "he will not be destroyed during the battle's Combat Segment"
                    foreach (var spirit in spirits) AssignToCreatedBattlefield(spirit, battlefield, attacking: false, mayBeBowed: false);
                });
                return true;
            }
            case "shinobi-assault-battle":
            {
                var ninja = Card(command.PerformerId!);
                SpendHandPlayedCard(player, source);
                var battlefield = CreateAdditionalBattlefield(null);
                BeginAsideBattle(battlefield, player.Id, command, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants, firstOpportunity: player.Id, () =>
                {
                    AssignToCreatedBattlefield(ninja, battlefield, attacking: true, mayBeBowed: false);
                    AssignToCreatedBattlefield(target!, battlefield, attacking: false, mayBeBowed: true); // "even if he is bowed"
                }, loseHonorAfter: 5);
                return true;
            }
            // The Perfect Moment: the additional Attack Phase, with its player's restrictions until it ends.
            case "perfect-moment-declare":
                R1.PerfectMoment = new PerfectMomentState { PlayerId = player.Id };
                DeclareAdditionalAttackPhase(player, source!);
                return true;
            case "two-fronts-mark":
                MarkTwoFronts(player.Id, command.Province ?? throw new RuleViolation("Choose a province."));
                Emit("twoFronts", $"{Definition(source!).Name}: {player.Name}'s next normal Attack Phase this turn creates two battlefields at province {command.Province + 1}.", player.Id, source!.Id);
                return false;
            default:
                throw new InvalidOperationException($"Unknown rulings pass R1 ability '{id}'.");
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRulingR1Cards_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorRulingR1", 1400, static abilities => abilities.AddEmperorRulingR1());
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRulingR1Cards_Rung4b2()
    {
        EngineHooks.TargetAllows.Register("RulingR1TargetAllows", 600, static (engine, context) => engine.RulingR1TargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRulingR1Cards_Rung4b3()
    {
        EngineHooks.ValidateEarly.Register("ValidateEmperorRulingR1", 1000, static (engine, context) => engine.ValidateEmperorRulingR1(context.id, context.ability, context.command, context.player, context.source));
    }
}
