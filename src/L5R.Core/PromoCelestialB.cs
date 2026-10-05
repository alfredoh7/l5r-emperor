namespace L5R.Core;

// Promotional-Celestial group B (strategy-wave-9201 and celestial-wave-9202). Every printed text is the SCAN's, read against its receipt.
//   * A BRAVE NEW WORLD (4): "Battle: Choose your performing Personality and discard a card: Move your Personality to the current battlefield. Straighten his unit if he moved." The scan prints no "Absent"
//     and no "at any location" (Oracle's later template does): a move to the current battlefield is an Absent Battle action for either player by rule (MovesToBattle), the performer is any Personality
//     of the player's wherever he stands, and the discarded card is another card from the hand (never the Strategy being played). Indecision's move shape (EmperorMechD.cs): the shared move rule, then
//     StraightenUnit only if he was not at the battlefield and now is (a move negated or blocked straightens nothing).
//   * ECONOMIC MANIPULATION (2279): "This card has -1 Gold Cost if you control a Merchant" prices the Strategy as it is played from the hand (the one SourcePrinted term in GoldQuotes.cs); "Limited: Target a
//     Holding with a base Gold Cost of 4 or less controlled by a player who controls five or more Holdings: Destroy it." Holdings in play are counted for the target's controller, bowed ones included,
//     the target itself too; the destruction is the shared one an action inflicts on its chosen card (DestroyTargetCard), so "will not be destroyed" and negations apply.
//   * FOLKLORE (2621): "Battle/Open: Target an unaligned Personality: Give him -3F." (until the end of the turn, ruling R-001) and "Battle: Choose your performing Human Personality and target a Nonhuman
//     Personality: Move it home." (the shared move rule, negatable). A Human is a Personality without Nonhuman (MatchEngine.KeywordOrHuman); "unaligned" is Keyword(card, "Unaligned").
//   * GAINING EXPERIENCE (2760): "Open: Choose your performing Samurai: Permanently give him either Cavalry or Tactician." Kurohito's Perfection's shape: one Open action per keyword sharing one use.
//   * FUKUROKUJIN'S WISDOM (2727, Celestial): "You have +1 maximum hand size." is Kazehime's increase-own-hand-size Effect; "Battle: Discard this card and choose one or more of your performing
//     Personalities: Move them home. Straighten the unit of each of them who moved." The performers are a performing slot at the current battlefield (Dislodge the Foe's shape), moved by the shared rule.
// Not wired (see the pass report): Natsu-Togumara's Guidance and Benten's Blessing (a Celestial enters play through ResolveCelestial, which opens no entry window to choose Personalities in), Green
// Nightshade Blend (no multi-token transfer decision), Lying in Ambush (no record of the Personalities an action created), Minor Blackmail, Near Miss, Open Recruitment.
public static class PromoCelestialBAbilities
{
    public const string BraveNewWorldMove = "promo-b-brave-new-world-move";
    public const string EconomicManipulationDestroy = "promo-b-economic-manipulation-destroy";
    public const string FolkloreForce = "promo-b-folklore-force", FolkloreMove = "promo-b-folklore-move";
    public const string GainingCavalry = "promo-b-gaining-experience-cavalry", GainingTactician = "promo-b-gaining-experience-tactician";
    public const string FukurokujinMoveHome = "promo-b-fukurokujin-move-home";
    public const string OwnHumanPersonalityAtBattle = "promo-b-own-human-personality-at-battle";
    public const string SmallHoldingOfRichController = "promo-b-small-holding-of-rich-controller";
    /// <summary>Economic Manipulation's Effect: "This card has -1 Gold Cost if you control a Merchant."</summary>
    public const string EconomicManipulationCostEffect = "promo-b-economic-manipulation-cost";

    public static Dictionary<string, AbilityDefinition> AddPromoCelestialB(this Dictionary<string, AbilityDefinition> all)
    {
        all[BraveNewWorldMove] = new("Battle", "Choose your performing Personality and discard a card: Move your Personality to the current battlefield. Straighten his unit if he moved", "hand",
            Performer: "own-personality", Costs: ActionCost.DiscardTarget, MovesToBattle: true, Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield));
        all[EconomicManipulationDestroy] = new("Limited", "Target a Holding with a base Gold Cost of 4 or less controlled by a player who controls five or more Holdings: Destroy it", SmallHoldingOfRichController);
        all[FolkloreForce] = new("BattleOrOpen", "Target an unaligned Personality: Give him -3F", "personality", TargetMustBe: new TargetStatus(Keywords: ["Unaligned"]), UsageKey: "promo-b-folklore");
        all[FolkloreMove] = new("Battle", "Choose your performing Human Personality and target a Nonhuman Personality: Move it home", "personality",
            Performer: OwnHumanPersonalityAtBattle, TargetMustBe: new TargetStatus(Keywords: ["Nonhuman"]), Move: new(MoveSpec.Target, MoveSpec.Home), UsageKey: "promo-b-folklore");
        all[GainingCavalry] = new("Open", "Choose your performing Samurai: Permanently give him Cavalry", "none", Performer: "own-personality-with-keyword", RequiredKeywords: ["Samurai"], UsageKey: "promo-b-gaining-experience");
        all[GainingTactician] = new("Open", "Choose your performing Samurai: Permanently give him Tactician", "none", Performer: "own-personality-with-keyword", RequiredKeywords: ["Samurai"], UsageKey: "promo-b-gaining-experience");
        all[FukurokujinMoveHome] = new("Battle", "Discard this card and choose one or more of your performing Personalities: Move them home. Straighten the unit of each of them who moved", "slots",
            Slots: [new("performers", "own-personality-at-battle", "Your performing Personalities at the current battlefield", 1, 12, Performing: true)],
            Costs: [new(CostVerbs.DiscardFromPlay)], Move: new(MoveSpec.PerformingSlot, MoveSpec.Home));
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- Performer and Target categories ----

    private bool PromoBPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        PromoCelestialBAbilities.OwnHumanPersonalityAtBattle => Definition(performer).Type == CardType.Personality && KeywordOrHuman(performer, "Human") && (!actingAsBattle || AtBattle(performer)),
        _ => false
    };

    private string?[]? PromoBPerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer == PromoCelestialBAbilities.OwnHumanPersonalityAtBattle
        ? _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && PromoBPerformerAllows(ability, c, actingAsBattle)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray()
        : null;

    private bool PromoBTargetAllows(string category, CardInstance target) => category switch
    {
        // Economic Manipulation: a Holding in play whose printed Gold Cost is 4 or less, its controller controlling five or more Holdings in play.
        PromoCelestialBAbilities.SmallHoldingOfRichController => Definition(target).Type == CardType.Holding && InPlay(target) && Definition(target).GoldCost <= 4
            && _state.Cards.Values.Count(c => c.OwnerId == target.OwnerId && InPlay(c) && Definition(c).Type == CardType.Holding) >= 5,
        _ => false
    };

    // "This card has -1 Gold Cost if you control a Merchant": the Strategy's own price as it is played (GoldQuotes' SourcePrinted), never below 0.
    private int PromoBSourceCostAdjustment(CardInstance source) =>
        Definition(source).Effect == PromoCelestialBAbilities.EconomicManipulationCostEffect
        && _state.Cards.Values.Any(c => c.OwnerId == source.OwnerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Merchant"))
            ? -Math.Min(1, Definition(source).GoldCost) : 0;

    // ---- validation ----

    private void ValidatePromoCelestialB(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case PromoCelestialBAbilities.BraveNewWorldMove:
                // "Discard a card": another card of the hand, never the Strategy that is being played.
                Require(command.TargetId is null || command.TargetId != command.CardId, "Discard a card other than A Brave New World.");
                break;
            case PromoCelestialBAbilities.FolkloreMove:
                Require(command.PerformerId is { } folklorePerformer && Card(folklorePerformer).OwnerId == player.Id, "Choose one of your own Human Personalities.");
                break;
        }
    }

    // ---- execution: null when the id is not this pass's; false when its effects are done ----

    private bool? RunPromoCelestialB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case PromoCelestialBAbilities.BraveNewWorldMove:
            {
                var mover = Card(command.PerformerId!);
                var wasThere = AtBattle(mover);
                ExecuteMove(command, ability, source, target);
                if (!wasThere && AtBattle(mover)) StraightenUnit(mover);
                return false;
            }
            case PromoCelestialBAbilities.EconomicManipulationDestroy:
                DestroyTargetCard(target!, player.Id);
                return false;
            case PromoCelestialBAbilities.FolkloreForce:
                ApplyModifier(target!, source!.Id, id, "Force", -3);
                return false;
            case PromoCelestialBAbilities.FolkloreMove:
                ExecuteMove(command, ability, source, target);
                return false;
            case PromoCelestialBAbilities.GainingCavalry:
                GiveKeywordPermanently(Card(command.PerformerId!), "Cavalry", source!.Id);
                return false;
            case PromoCelestialBAbilities.GainingTactician:
                GiveKeywordPermanently(Card(command.PerformerId!), "Tactician", source!.Id);
                return false;
            case PromoCelestialBAbilities.FukurokujinMoveHome:
            {
                var performers = PerformingSlotCards(command, ability).ToArray();
                var before = performers.ToDictionary(c => c.Id, c => c.Zone);
                ExecuteMove(command, ability, source, target);
                foreach (var performer in performers)
                    if (before[performer.Id] != performer.Zone) StraightenUnit(performer);
                return false;
            }
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoCelestialBHooks()
    {
        EngineHooks.Abilities.Register("AddPromoCelestialB", 10010, static abilities => abilities.AddPromoCelestialB());
        EngineHooks.PerformerAllows.Register("PromoBPerformerAllows", 10010, static (engine, context) => engine.PromoBPerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.PerformerCandidates.Register("PromoBPerformers", 10010, static (engine, context) => engine.PromoBPerformers(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.TargetAllows.Register("PromoBTargetAllows", 10010, static (engine, context) => engine.PromoBTargetAllows(context.ability.Target, context.target));
        EngineHooks.DestroysTarget.Register("PromoBDestroysTarget", 10010, static context => context.abilityId == PromoCelestialBAbilities.EconomicManipulationDestroy);
        EngineHooks.ValidateMiddle.Register("ValidatePromoCelestialB", 10010, static (engine, context) => engine.ValidatePromoCelestialB(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunPromoCelestialB", 10010, static (engine, context) => engine.RunPromoCelestialB(context.id, context.ability, context.command, context.player, context.source, context.target));
    }
}
