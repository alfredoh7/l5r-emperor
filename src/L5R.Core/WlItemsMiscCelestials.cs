namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #674, CLUSTER "ITEMS-MISC" (celestial-wave-28): Blood of Shahai, Hachiman's Prowess and The Ways of Sensei Wu, wired from the printed text of their scans. Fudo's Assurances is not
// wired (tools/content/pe_wl_items_misc_blockers.json).
//
//   BLOOD OF SHAHAI        "Maho Battle/Open: Give -1C to a target Personality with lower Chi than his printed Chi" (a -1C modifier, until the turn ends, R-001; the target's current Chi is below the Chi
//                          printed on its card) and "Maho Battle: Discard this Celestial. Give each Personality at this battlefield a -1C Blood token". The discard is the first EFFECT of the second
//                          action (it is not before the colon, so not a cost); the tokens are Chi tokens on every Personality at the current battlefield, either player's (a Personality at 0 Chi is destroyed).
//   HACHIMAN'S PROWESS     "Limited: Once per game, target your Personality: While this card remains in play, he has Elite and the trait, 'May assign even if bowed'" is a record of the grant read by
//                          Keyword (Elite) and by MayAssignBowed while the Celestial is in play; "Battle: Discard this card and target your Personality: Straighten his unit" discards it as a cost.
//   THE WAYS OF SENSEI WU  "Battle: Put a target Battle Strategy from your discard pile into your hand. Discard this Celestial." The Strategy is a slot (a card of a discard pile) chosen as the action
//                          is announced; the Celestial is discarded as the second effect.
// ======================================================================================================================================================================================

public static class WlItemsMiscCelestialAbilities
{
    public const string ShahaiChi = "wl-shahai-chi", ShahaiBlood = "wl-shahai-blood", HachimanGrant = "wl-hachiman-grant", HachimanStraighten = "wl-hachiman-straighten", WuReturn = "wl-wu-return";
    /// <summary>Target categories: a Personality whose Chi is below its printed Chi (Blood of Shahai); a Battle Strategy in your own discard pile (Sensei Wu).</summary>
    public const string BelowPrintedChi = "wl-personality-below-printed-chi", OwnBattleStrategyInDiscard = "wl-own-battle-strategy-in-discard";

    public static Dictionary<string, AbilityDefinition> AddWlItemsMiscCelestials(this Dictionary<string, AbilityDefinition> all)
    {
        // Blood of Shahai (oracle-l5r-10602)
        all[ShahaiChi] = new("BattleOrOpen", "Maho Battle/Open: Give -1C to a target Personality with lower Chi than his printed Chi", BelowPrintedChi, ActionKeywords: ["Maho"]);
        all[ShahaiBlood] = new("Battle", "Maho Battle: Discard this Celestial. Give each Personality at this battlefield a -1C Blood token", "none", ActionKeywords: ["Maho"]);
        // Hachiman's Prowess (oracle-l5r-10257)
        all[HachimanGrant] = new("Limited", "Once per game, target your Personality: While this card remains in play, he has Elite and the trait, \"May assign even if bowed\"", "own-personality", OncePerGame: true);
        all[HachimanStraighten] = new("Battle", "Discard this card and target your Personality: Straighten his unit", "own-personality", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // The Ways of Sensei Wu (oracle-l5r-10818)
        all[WuReturn] = new("Battle", "Put a target Battle Strategy from your discard pile into your hand. Discard this Celestial", "slots",
            Slots: [new("strategy", OwnBattleStrategyInDiscard, "A Battle Strategy in your discard pile to put into your hand")]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- target categories ----------------------------------------------------------------------------------------------------------------------------------------------------

    // TargetAllows: Blood of Shahai's "a target Personality with lower Chi than his printed Chi" (his current Chi, every modifier counted).
    private bool WlItemsMiscTargetAllows(string category, CardInstance target) =>
        category == WlItemsMiscCelestialAbilities.BelowPrintedChi && Definition(target).Type == CardType.Personality && InPlay(target) && PersonalityChi(target) < Definition(target).Chi;

    // The out-of-play categories ValidateAbility asks before it demands a card in play: a Battle Strategy (a Strategy with a Battle or Battle/Open action) in the acting player's Fate discard pile.
    private static bool WlItemsMiscIsOutOfPlayTarget(string category) => category == WlItemsMiscCelestialAbilities.OwnBattleStrategyInDiscard;

    private bool WlItemsMiscOutOfPlayTargetAllows(PlayerState player, CardInstance target) =>
        target.OwnerId == player.Id && Definition(target).Type == CardType.Strategy && target.Zone == Zone.FateDiscard && IsBattleStrategy(target);

    // ---- Hachiman's Prowess: the grant ----------------------------------------------------------------------------------------------------------------------------------------

    private bool WlItemsMiscHasHachimanGrant(CardInstance card) =>
        WlState.HachimanGrants.Count > 0 && InPlay(card)
        && WlState.HachimanGrants.Any(g => g.TargetId == card.Id && _state.Cards.TryGetValue(g.CelestialId, out var celestial) && celestial.Zone == Zone.Celestial);

    // Keyword's term: Elite while the Celestial that gave it is in play. Other keywords are not this cluster's.
    private bool WlItemsMiscKeywordGranted(CardInstance card, string keyword) => keyword == "Elite" && WlItemsMiscHasHachimanGrant(card);

    // MayAssignBowed's term: "the trait, 'May assign even if bowed.'"
    private bool WlItemsMiscMayAssignBowed(CardInstance card) => WlItemsMiscHasHachimanGrant(card);

    // Test seam (L5R.Core.Tests has InternalsVisibleTo): whether a bowed Personality at home may be assigned (every term of the rule, Embracing Battle's included).
    internal bool WlItemsMiscMayAssignBowedForTest(string cardId) => MayAssignBowed(Card(cardId));

    // ---- execution --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // A Celestial discarded as an effect (Blood of Shahai, Sensei Wu): it leaves play to its owner's Dynasty discard pile.
    private void WlItemsMiscDiscardCelestial(CardInstance celestial)
    {
        if (celestial.Zone != Zone.Celestial) return;
        celestial.Zone = Zone.DynastyDiscard; celestial.FaceUp = true; celestial.ProvinceIndex = null;
        Emit("celestialDiscarded", $"{Definition(celestial).Name} was discarded.", celestial.OwnerId, celestial.Id);
    }

    // null: not this cluster's Celestial. false: the effects are done.
    private bool? RunWlItemsMiscCelestials(RunHookContext context)
    {
        var (id, command, player, source, target) = (context.id, context.command, context.player, context.source, context.target);
        switch (id)
        {
            case WlItemsMiscCelestialAbilities.ShahaiChi:
                ApplyModifier(target!, source!.Id, id, "Chi", -1);
                return false;
            case WlItemsMiscCelestialAbilities.ShahaiBlood:
                {
                    WlItemsMiscDiscardCelestial(source!);
                    var battlefield = _state.CurrentBattleProvince;
                    foreach (var personality in _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield)
                        .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
                        if (InPlay(personality)) CreateToken(personality, "Blood", id, "Chi", -1);
                    return false;
                }
            case WlItemsMiscCelestialAbilities.HachimanGrant:
                WlState.HachimanGrants.Add(new(source!.Id, target!.Id));
                Emit("eliteGranted", $"{Definition(source).Name}: {Definition(target).Name} has Elite and may assign even if bowed while the Celestial remains in play.", player.Id, target.Id);
                return false;
            case WlItemsMiscCelestialAbilities.HachimanStraighten:
                StraightenUnit(target!);
                return false;
            case WlItemsMiscCelestialAbilities.WuReturn:
                {
                    foreach (var strategy in SlotCards(command, "strategy").ToArray())
                    {
                        if (strategy.Zone != Zone.FateDiscard || P2CardIntoHandBlocked(player.Id)) continue;
                        strategy.Zone = Zone.Hand; strategy.FaceUp = true; ResetCardState(strategy); // a card taken back into the hand is a fresh card (a stale once-per-turn stamp is gone)
                        player.Hand.Add(strategy.Id); NoteCardIntoHand(player.Id);
                        Emit("cardReturnedToHand", $"{player.Name} put {Definition(strategy).Name} from the discard pile into their hand.", player.Id, strategy.Id);
                    }
                    WlItemsMiscDiscardCelestial(source!);
                    return false;
                }
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlItemsMiscCelestialHooks()
    {
        EngineHooks.Abilities.Register("AddWlItemsMiscCelestials", 15130, static abilities => abilities.AddWlItemsMiscCelestials());
        EngineHooks.TargetAllows.Register("WlItemsMiscTargetAllows", 15130, static (engine, context) => engine.WlItemsMiscTargetAllows(context.ability.Target, context.target));
        EngineHooks.RunEffects.Register("RunWlItemsMiscCelestials", 15130, static (engine, context) => engine.RunWlItemsMiscCelestials(context));
    }
}
