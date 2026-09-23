namespace L5R.Core;

/// <summary>
/// The generalized "target status" predicate (P2, issue: Bayushi Kahoku - exp). One declarative,
/// reusable, ANY-OF test on a target card that an action's own specification parameterizes: "the target
/// is honorable", "the target is dishonorable", "the target is Kolat, Ninja, or Shadowlands" and any
/// combination of those ("dishonorable, Kolat, Ninja, or Shadowlands" -- Dispensing Justice, Winds of
/// Dismissal). Satisfying several listed terms is still just one match (the result is a boolean, never a
/// count), and satisfying any one is sufficient.
///
/// Terms (each optional; the predicate is the OR of every term that is set):
///   Honorable / Dishonorable -- the engine's one existing honor status: a Personality whose
///     CardInstance.Dishonorable is clear / set. Only a Personality is ever honorable or dishonorable
///     (core rules Glossary: "Honorable: a Personality not in the dishonorable state"), so a Follower or
///     other card never satisfies either term. This is the same test the dishonorable-personality /
///     honorable-personality Target categories, Bayushi Kahoku's strength bonus and every other
///     hand-rolled `.Dishonorable` check make -- there is no second representation.
///   Keywords -- any listed keyword, printed or granted (the engine's live Keyword() check).
///   Clans -- any listed Clan alignment (a keyword ending in "Clan", not a plain keyword).
///   ClanOrKeyword -- any listed name held EITHER as a Clan alignment OR as a keyword. "Shadowlands" is
///     the case that needs this: fixtures and a few cards carry it as a Clan alignment, while Asako
///     Rikate, Takasho, Keppo and Daigotsu Kendo print it only as a keyword beside their real Clan.
///
/// The predicate is used in two roles, both through MatchEngine.MatchesTargetStatus:
///   * an EFFECT condition -- AbilityDefinition.IfTarget ("destroy him if ...", "+2 strength if the
///     target is dishonorable"), read back with MatchEngine.EffectConditionMet;
///   * a TARGET-LEGALITY condition -- AbilityDefinition.TargetMustBe layered on whichever Target
///     category the ability already uses (like RequireNoAttachments), and AttackFollowerException.TargetIs
///     ("may target DISHONORABLE Personalities with attached Followers").
/// </summary>
public sealed record TargetStatus(bool Honorable = false, bool Dishonorable = false, string[]? Keywords = null, string[]? Clans = null, string[]? ClanOrKeyword = null)
{
    public static readonly TargetStatus IsHonorable = new(Honorable: true);
    public static readonly TargetStatus IsDishonorable = new(Dishonorable: true);
    /// <summary>"Kolat, Ninja, or Shadowlands" (Isawa Mitsuko, Asako Megu, ...): Kolat and Ninja keywords, Shadowlands as Clan OR keyword.</summary>
    public static readonly TargetStatus KolatNinjaOrShadowlands = new(Keywords: ["Kolat", "Ninja"], ClanOrKeyword: ["Shadowlands"]);
    /// <summary>"Dishonorable, Kolat, Ninja, or Shadowlands" (Dispensing Justice, Winds of Dismissal).</summary>
    public static readonly TargetStatus DishonorableKolatNinjaOrShadowlands = new(Dishonorable: true, Keywords: ["Kolat", "Ninja"], ClanOrKeyword: ["Shadowlands"]);
    /// <summary>"Shadowlands" alone (Clan alignment or keyword).</summary>
    public static readonly TargetStatus IsShadowlands = new(ClanOrKeyword: ["Shadowlands"]);
    /// <summary>"Nonhuman or Shadowlands" (Okesa-goma, issue #453 batch 5): the Nonhuman keyword, or Shadowlands as Clan alignment or keyword.</summary>
    public static readonly TargetStatus NonhumanOrShadowlands = new(Keywords: ["Nonhuman"], ClanOrKeyword: ["Shadowlands"]);

    /// <summary>Human-readable form for rejection messages, e.g. "dishonorable, Kolat, Ninja, or Shadowlands".</summary>
    public string Describe()
    {
        var terms = new List<string>();
        if (Honorable) terms.Add("honorable");
        if (Dishonorable) terms.Add("dishonorable");
        terms.AddRange(Keywords ?? []);
        terms.AddRange(Clans ?? []);
        terms.AddRange(ClanOrKeyword ?? []);
        return terms.Count switch { 0 => "(nothing)", 1 => terms[0], 2 => $"{terms[0]} or {terms[1]}", _ => string.Join(", ", terms.Take(terms.Count - 1)) + ", or " + terms[^1] };
    }
}
