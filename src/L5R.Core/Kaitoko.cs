namespace L5R.Core;

// Issue #768: Asako Kaitoko - exp (oracle-l5r-10473, Coils of Madness; profile personality-wave-301, tools/content/package_personality_wave_301.py). Deferred by batch 5 (#736) for the missing stat cap and the
// keyword-conditioned target bar; Mendon asked for the cap to be built first (ForceCaps.cs).
//
//   "Personalities with Fallen, Kolat, Ninja, Nonhuman, or Shadowlands opposing Kaitoko have a maximum Force of 3, and actions from cards in their units may not target Kaitoko."
//
//   Maximum Force 3     EngineHooks.ForceCaps: a Personality at Kaitoko's battlefield, on the other side, with any of the five keywords (read live, so a keyword given by a card counts and one taken away
//                       does not), is capped at 3 while Kaitoko is at that battlefield. A Force of 3 or less is untouched; the cap ends when either leaves the battlefield. Negated for a Personality that has
//                       Force penalties negated or is immune to them (ForceCaps.cs).
//   May not target      EngineHooks.CardTargetBars (P11): actions of a card in the unit of such a Personality (the Personality, his Followers, Items and Spells) may not target Kaitoko. ActorSpec gained the unit-head
//                       keyword and opposition tests for it (CardRestrictions.cs).
public static class KaitokoAbilities
{
    public const string Effect = "kaitoko-force-cap";

    /// <summary>The keywords the printed text names.</summary>
    public static readonly string[] Keywords = ["Fallen", "Kolat", "Ninja", "Nonhuman", "Shadowlands"];

    public const int MaxForce = 3;
}

public sealed partial class MatchEngine
{
    private IEnumerable<ForceCap> KaitokoCaps(ForceCapContext context)
    {
        var card = context.Card;
        // Cheapest tests first: only a Personality standing at a battlefield can be opposing, and then only one of the five keywords matters.
        if (card.Zone != Zone.Battlefield || card.ProvinceIndex is not int battlefield || !KaitokoAbilities.Keywords.Any(k => Keyword(card, k))) yield break;
        foreach (var enemy in Army(1 - card.OwnerId, battlefield))
            if (Definition(enemy).Effect == KaitokoAbilities.Effect)
                yield return new ForceCap(KaitokoAbilities.MaxForce, enemy.Id, Definition(enemy).Name);
    }

    private static IEnumerable<StaticTargetBar> KaitokoBars(MatchEngine engine, CardInstance carrier)
    {
        if (Definition(carrier).Effect != KaitokoAbilities.Effect) yield break;
        yield return new StaticTargetBar(new ActorSpec("others", UnitCardsOnly: true, UnitHeadKeywordsAny: KaitokoAbilities.Keywords, UnitHeadOpposesCardId: carrier.Id));
    }

    /// <summary>Whether the announce-time target bars refuse this action from this source against this target.</summary>
    internal bool TargetBarredForTest(int playerId, string sourceId, string targetId, AbilityDefinition ability)
    {
        try { RequireRestrictionRulesAllow(new GameCommand("useAbility", playerId, _state.Version, sourceId, AbilityId: "test-ability", TargetId: targetId), ability); return false; }
        catch (RuleViolation) { return true; }
    }

    [System.Runtime.CompilerServices.ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    internal static void RegisterKaitokoHooks()
    {
        EngineHooks.ForceCaps.Register("KaitokoForceCap", 15600, static context => context.Engine.KaitokoCaps(context));
        EngineHooks.CardTargetBars.Register("KaitokoTargetBar", 15600, static (engine, carrier) => KaitokoBars(engine, carrier));
    }
}
