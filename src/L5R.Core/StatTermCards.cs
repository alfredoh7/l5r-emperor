namespace L5R.Core;

// Primitive P9 (issue #672): the demonstration cards of the stat-term registry (StatTerms.cs), each wired from its own scan (personality-wave-130). Every static trait below is
// DATA -- a StatTermSet keyed by the card's Effect -- and none has a branch of its own in a Force, Chi or army-total function.
//   Tochiko, the Jagged Tusk of Death (oracle-l5r-10468)  "Tochiko has +2F while opposing a Personality or Follower with lower Force."            Effect p9-tochiko
//   Toku Saiga, the Metal Storm (oracle-l5r-10490)        "Saiga has +1F/+1C for each of his Weapons."                                            Effect p9-saiga
//   Hiruma Ogata (oracle-l5r-10441)                       "After Ogata enters play, lose 2 Honor." (EntryHonorLoss)
//                                                         "Ogata has +1F for each opposing Madness token."                                        Effect p9-ogata
//   Matsu Sango (oracle-l5r-10294)                        "Will not attach Items." (WillNotAttachItems, Chiisai's rule)
//                                                         "During battle resolutions, if Sango is attacking, he contributes his Force twice."     Effect p9-sango (a ContributionRule)
//   Bayushi Manami - exp (oracle-l5r-10654)               "During battle resolution, Manami's army has +2F."                                      Effect p9-manami (an Army term)
//   Moto Daiken (oracle-l5r-10673)                        "While you control any Ancestor Personalities or have any dead Personalities in your
//                                                         discard pile, Daiken has +1F."                                                          Effect p9-daiken
//   Moto Ogaru (oracle-l5r-10326)                         "Ogaru has +1F while the enemy leader controls an Imperial card or the Imperial Favor." Effect p9-ogaru
//   Yoritomo Toganin (oracle-l5r-10859)                   "Invest 5: Give Toganin three +1F tokens."                                              p9-toganin-invest (Clay Kshatriya's shape)
//                                                         "Toganin has +1F if any other player is Crane Clan."                                    Effect p9-toganin
// The readings made for these cards are in docs/decisions/2026-10-06-672-p9-stat-terms.md, for Mendon to confirm or correct.
public static class StatTermCards
{
    public const string TochikoEffect = "p9-tochiko";
    public const string SaigaEffect = "p9-saiga";
    public const string OgataEffect = "p9-ogata";
    public const string SangoEffect = "p9-sango";
    public const string ManamiEffect = "p9-manami";
    public const string DaikenEffect = "p9-daiken";
    public const string OgaruEffect = "p9-ogaru";
    public const string ToganinEffect = "p9-toganin";

    public const string ToganinInvest = "p9-toganin-invest";

    /// <summary>Invest is a trait (core rules; Mendon's R4 ruling on Cherry Blossom Festival), modelled with Reaction timing like Clay Kshatriya's.</summary>
    public static bool IsTrait(string abilityId) => abilityId == ToganinInvest;

    internal static IEnumerable<StatTermSet> Sets() =>
    [
        new(TochikoEffect, [new("p9-tochiko-force", TermScope.Self, TermStat.Force, 2, AnyOf: [new(TermConditionKind.OpposingLowerForce)])]),
        new(SaigaEffect,
        [
            new("p9-saiga-force", TermScope.Self, TermStat.Force, 1, PerCount: new(TermCounterKind.AttachedWithKeyword, "Weapon")),
            new("p9-saiga-chi", TermScope.Self, TermStat.Chi, 1, PerCount: new(TermCounterKind.AttachedWithKeyword, "Weapon")),
        ]),
        new(OgataEffect, [new("p9-ogata-force", TermScope.Self, TermStat.Force, 1, PerCount: new(TermCounterKind.OpposingTokens, "Madness"))]),
        new(SangoEffect, [], new ContributionRule("p9-sango-twice", 2, [new(TermConditionKind.Attacking)])),
        new(ManamiEffect, [new("p9-manami-army", TermScope.Army, TermStat.Force, 2)]),
        new(DaikenEffect,
        [
            new("p9-daiken-force", TermScope.Self, TermStat.Force, 1,
                AnyOf: [new(TermConditionKind.ControlsPersonalityWithKeyword, "Ancestor"), new(TermConditionKind.DeadPersonalityInDiscard)]),
        ]),
        new(OgaruEffect,
        [
            new("p9-ogaru-force", TermScope.Self, TermStat.Force, 1,
                AnyOf: [new(TermConditionKind.EnemyLeaderControlsKeyword, "Imperial"), new(TermConditionKind.EnemyLeaderHasFavor)]),
        ]),
        new(ToganinEffect, [new("p9-toganin-force", TermScope.Self, TermStat.Force, 1, AnyOf: [new(TermConditionKind.OtherPlayerIsClan, "Crane")])]),
    ];

    public static Dictionary<string, AbilityDefinition> AddStatTermCards(this Dictionary<string, AbilityDefinition> all)
    {
        // Yoritomo Toganin: "Invest 5: Give Toganin three +1F tokens. (After this card enters play, you may also pay the Invest cost to get the effect, once.)"
        all[ToganinInvest] = new("Reaction", "Invest 5 Gold: give Toganin three +1F tokens", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(5));
        return all;
    }
}

public sealed partial class MatchEngine
{
    // Matsu Sango: "Will not attach Items." -- asked by WillNotAttachItems (ShadowsEmbraceBatch1.cs), the rule Chiisai prints.
    private bool StatTermCardWillNotAttachItems(CardInstance host) => Definition(host).Effect == StatTermCards.SangoEffect;

    private bool? RunStatTermCards(string id, CardInstance? source)
    {
        if (id != StatTermCards.ToganinInvest) return null;
        for (var n = 0; n < 3; n++) CreateToken(source!, "Invest", id, "Force", 1);
        return false;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterStatTermCardHooks()
    {
        EngineHooks.StatTerms.Register("P9StatTermCards", 13000, static () => StatTermCards.Sets());
        EngineHooks.Abilities.Register("P9AddStatTermCards", 13000, static abilities => abilities.AddStatTermCards());
        EngineHooks.RunEffects.Register("P9RunStatTermCards", 13000, static (engine, context) => engine.RunStatTermCards(context.id, context.source));
    }
}
