namespace L5R.Core;

// Issue #653 (Promotional-Emperor pass): Celestials (celestial-wave-27), each wired from the PRINTED text of its scan (read by eye).
//
//   Hofukushu's Vengeance   "Reaction: After the end of an Attack Phase in which you were the Defender, discard this card: Declare an additional Attack Phase against that phase's Attacker, in
//                           which you may only assign one Personality." Retribution's window and additional Attack Phase (EmperorRemainingC.cs: DeclareAdditionalAttackPhase, the "attackPhaseEndReaction"
//                           window), played from play with the discard as its cost. Retribution's "+1F after he assigns" is its own printed text, so this card suppresses it.
//   Kenro-ji-jin's Bounty   "Reaction: When paying for a Farm: Produce 1 Gold." is a Reaction Gold source (payment token "<card>#reaction", EmperorMechE.cs) that a Celestial now offers beside Holdings;
//                           "Limited: Discard this card: Your Farms produce 1 additional Gold when they bow to produce Gold (this turn)" is a turn mark read by the Farms' production (GoldProduced).
//   Suitengu's Blessing     "Limited: Once per game, target a Personality: Give him Naval while this card remains in play" is a live grant read by Keyword (the Celestial must still be in play);
//                           "Battle: Discard this card and choose your performing Personality: If he would be opposed, move him to the current battlefield. Straighten his unit if he moved" is an
//                           absent move to the current battlefield (MovesToBattle), taken only when enemy units are there.
//
// Small shared edits (named in the report): ReactiveGoldSource, PaymentTokens and ReactivePaymentConditionMet (a Celestial as a Reaction Gold source), Keyword (the Naval grant),
// DeclareAdditionalAttackPhase and NoteRetributionAssignments (Hofukushu's phase has no +1F), and BattleAbsentMovesTests' pinned inventory (a new MovesToBattle ability).
public static class PromoEmperorCelestialAbilities
{
    public const string HofukushuDeclare = "pe-hofukushus-vengeance-declare", KenroPaymentGold = "pe-kenro-ji-jins-bounty-gold", KenroFarms = "pe-kenro-ji-jins-bounty-farms",
        SuitenguNaval = "pe-suitengus-blessing-naval", SuitenguMove = "pe-suitengus-blessing-move";
    public const string HofukushuEffect = "pe-hofukushus-vengeance";
    /// <summary>Kenro-ji-jin's Bounty's payment condition: paying for a Farm.</summary>
    public const string PayingForFarmWhen = "pe-paying-for-farm";

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorCelestials(this Dictionary<string, AbilityDefinition> all)
    {
        // Hofukushu's Vengeance (oracle-l5r-3401)
        all[HofukushuDeclare] = new("Reaction", "Reaction: After the end of an Attack Phase in which you were the Defender, discard this card: Declare an additional Attack Phase against that phase's Attacker, in which you may only assign one Personality",
            "none", Trigger: EmperorRemainingCAbilities.AttackPhaseEnd, Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Kenro-ji-jin's Bounty (oracle-l5r-10229)
        all[KenroPaymentGold] = new("Reaction", "Reaction: When paying for a Farm: Produce 1 Gold", "none", Trigger: "paying-gold-cost", PaymentProduction: 1, PaymentBows: false, PaymentWhen: PayingForFarmWhen);
        all[KenroFarms] = new("Limited", "Limited: Discard this card: Your Farms produce 1 additional Gold when they bow to produce Gold (this turn)", "none", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Suitengu's Blessing (oracle-l5r-9948)
        all[SuitenguNaval] = new("Limited", "Limited: Once per game, target a Personality: Give him Naval while this card remains in play", "personality", OncePerGame: true);
        all[SuitenguMove] = new("Battle", "Battle: Discard this card and choose your performing Personality: If he would be opposed, move him to the current battlefield. Straighten his unit if he moved", "none",
            Performer: "own-personality", MovesToBattle: true, Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), Costs: [new(CostVerbs.DiscardFromPlay)]);
        return all;
    }
}

/// <summary>Suitengu's Blessing's grant: the Personality has Naval while the Celestial that gave it is in play.</summary>
public sealed class PromoNavalGrant
{
    public string CelestialId { get; set; } = "";
    public string TargetId { get; set; } = "";
}

/// <summary>The Promotional-Emperor group's marks: Kenro-ji-jin's Bounty's turn mark per player (-1: none), Suitengu's grants, and whether the additional Attack Phase under way was Hofukushu's.</summary>
public sealed class PromoEmperorState
{
    public int[] KenroTurn { get; set; } = [-1, -1];
    public List<PromoNavalGrant> NavalGrants { get; set; } = [];
    public bool HofukushuPhase { get; set; }
}

public sealed partial class MatchState
{
    // A nondecision record: nothing here is a pending choice.
    [System.Text.Json.Serialization.JsonPropertyOrder(1480)]
    public PromoEmperorState PromoEmperor { get; set; } = new();
}

public sealed partial class MatchEngine
{
    private bool? RunPromoEmperorCelestials(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case PromoEmperorCelestialAbilities.HofukushuDeclare:
                DeclareAdditionalAttackPhase(player, source!);
                return true;
            case PromoEmperorCelestialAbilities.KenroFarms:
                _state.PromoEmperor.KenroTurn[player.Id] = _state.TurnNumber;
                Emit("farmsBlessed", $"{Definition(source!).Name}: {player.Name}'s Farms produce 1 additional Gold this turn.", player.Id, source!.Id);
                return false;
            case PromoEmperorCelestialAbilities.SuitenguNaval:
                _state.PromoEmperor.NavalGrants.Add(new() { CelestialId = source!.Id, TargetId = target!.Id });
                Emit("navalGranted", $"{Definition(source).Name}: {Definition(target).Name} has Naval while the Celestial remains in play.", player.Id, target.Id);
                return false;
            case PromoEmperorCelestialAbilities.SuitenguMove:
                {
                    var performer = ActionPerformer(ability, command, source);
                    var battlefield = _state.CurrentBattleProvince!.Value;
                    // "If he would be opposed": enemy units stand at the current battlefield.
                    if (!Army(1 - player.Id, battlefield).Any()) return false;
                    var from = (performer.Zone, performer.ProvinceIndex);
                    ExecuteMove(command, ability, source, null);
                    if ((performer.Zone, performer.ProvinceIndex) != from) StraightenUnit(performer);
                    return false;
                }
            default:
                return null;
        }
    }

    // Keyword's term: Naval from Suitengu's Blessing while its Celestial is in play.
    private bool PromoEmperorNaval(CardInstance card) =>
        _state.PromoEmperor.NavalGrants.Count > 0
        && _state.PromoEmperor.NavalGrants.Any(g => g.TargetId == card.Id && _state.Cards.TryGetValue(g.CelestialId, out var celestial) && celestial.Zone == Zone.Celestial);

    // Kenro-ji-jin's Bounty: "paying for a Farm" -- a Farm bought, equipped or played, never an action.
    private bool PromoEmperorPaymentCondition(string when, PaymentSubject paying) =>
        when == PromoEmperorCelestialAbilities.PayingForFarmWhen && paying.Kind != PaymentKind.Action && Keyword(paying.Card, "Farm");

    // GoldProduced's term for a Farm while Kenro-ji-jin's Bounty is spent for the turn: "1 additional Gold when they bow to produce Gold".
    private int PromoEmperorFarmBonus(CardInstance card, int playerId) =>
        Definition(card).Type == CardType.Holding && _state.PromoEmperor.KenroTurn[playerId] == _state.TurnNumber && Keyword(card, "Farm") ? 1 : 0;

    // DeclareAdditionalAttackPhase's note: whose card declared the phase now beginning. NoteRetributionAssignments reads it: only Retribution gives +1F after he assigns.
    private void PromoNoteAdditionalAttackPhase(CardInstance source) => _state.PromoEmperor.HofukushuPhase = Definition(source).Effect == PromoEmperorCelestialAbilities.HofukushuEffect;
    private bool PromoEmperorNoRetributionBonus() => _state.PromoEmperor.HofukushuPhase;
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization. Priority 12410 is this group's.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoEmperorCelestialsHooks()
    {
        EngineHooks.Abilities.Register("AddPromoEmperorCelestials", 12410, static abilities => abilities.AddPromoEmperorCelestials());
        EngineHooks.RunEffects.Register("RunPromoEmperorCelestials", 12410, static (engine, context) => engine.RunPromoEmperorCelestials(context.id, context.ability, context.command, context.player, context.source, context.target));
    }
}
