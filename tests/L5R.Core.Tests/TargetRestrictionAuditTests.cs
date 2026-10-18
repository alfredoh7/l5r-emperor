using System.Text;
using System.Text.RegularExpressions;
using L5R.Core;

// Issue #889: audit of the restrictions on the performer and the target of every action the engine offers. Surety of Purpose ("target an ENEMY Personality") let its
// player bow their own Personality because its Target category (shared with a neutral one) never excluded their own cards.
//
// The restrictions are read from the printed words of each ability's label (the part before the colon: "Choose your performing unbowed Paragon and target an enemy Personality
// with equal or lower Personal Honor"): whose card (own / enemy), bow state (unbowed / bowed), honor status (honorable / dishonorable), the card type it names (a Personality only)
// and the keywords it names. Each card with a Battle / Open / Limited action is then placed on a table that holds a Personality of EVERY combination of side, bow state, honor
// status and keyword, in all four action windows, and the engine's own QueryActions says which performers and targets it offers. An offered performer or target that breaks
// a printed restriction is a violation (the engine would let the player do what the card forbids); an action no table could make legal is reported as unplayable, for diagnosis.
//
// The audit is read-only: it asks the engine, it never changes a rule. L5R_TARGET_AUDIT_OUT names a file to receive the full report.
internal static class TargetRestrictionAuditTests
{
    public static void Run(TestRunner runner) => runner.Run("target-restriction-audit", [
        ("The label parser reads whose card, bow state, honor status, type and keywords from a printed target or performer phrase", Parser),
        ("No offered performer or target of a Battle / Open / Limited action breaks a restriction its printed label states", NoRestrictionBroken),
        ("Surety of Purpose offers only enemy Personalities, and refuses its own player's Personality as a direct command", SuretyOfPurpose),
    ]);

    internal enum Side { Any, Own, Enemy }
    internal enum Bow { Any, Unbowed, Bowed }
    internal enum Honor { Any, Honorable, Dishonorable }

    internal sealed record Restriction(string Phrase, Side Side, Bow Bow, Honor Honor, bool PersonalityOnly, string[] Keywords, bool AnyKeyword, string[] NotKeywords)
    {
        public bool Any => Side == Side.Any && Bow == Bow.Any && Honor == Honor.Any && !PersonalityOnly && Keywords.Length == 0 && NotKeywords.Length == 0;
    }

    internal sealed record Violation(string Card, string Ability, string Scenario, string Role, string Rule, string Offered);

    private static readonly string[] NounWords = ["personality", "personalities", "follower", "followers", "item", "items", "spell", "spells", "holding", "holdings", "strategy", "ring", "rings",
        "province", "provinces", "stronghold", "region", "regions", "terrain", "card", "cards", "unit", "units", "attachment", "attachments", "army", "celestial", "event", "events", "player", "players"];
    private static readonly Regex PhraseEnd = new(@"\s*(?:,|:|;|\band\b|\bwith\b|\bwhose\b|\bthat\b|\bwho\b|\bin\b|\bat\b|\bof\b|\bwithout\b|\bequal\b|\bnot\b|\bopposing\b|\battached\b|\bwhich\b|\bfrom\b|\bto\b|\bthere\b|\bwhen\b|\bif\b|\bto\b|\bnamed\b|\bon\b|\bwhile\b|\bwhere\b|\bcontrolled\b|\bminus\b|\bthis\b|\bhis\b|\bher\b|\bits\b|\btheir\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Every keyword a printed card or an ability names: a capitalized word in a phrase counts as a keyword only if it is one of these ("Gold", "Clan", "Cost" are not).
    private static readonly Lazy<HashSet<string>> KeywordVocabulary = new(() => CardCatalog.Cards.Values.SelectMany(c => c.Keywords ?? [])
        .Concat(AbilityCatalog.All.Values.SelectMany(a => a.RequiredKeywords ?? [])).Where(k => !CardCatalog.Cards.Values.Any(c => c.Clan == k)).ToHashSet(StringComparer.Ordinal)); // a clan ("Lion Clan Personality") is an affiliation, not a keyword
    private static readonly Regex AlternativeTail = new(@"\bor\s+(?:an?\s+|your\s+|the\s+)?(?:\w+\s+)?(?:follower|item|spell|attachment|card|unit|holding|stronghold|region|ring|strategy|personality)b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The restriction a noun phrase ("an enemy unbowed Shugenja", "your performing Paragon") states, or null when it names nothing worth checking or is an alternative
    /// the parser does not split ("a Holding or your Stronghold"). <paramref name="tail"/> is what follows the phrase in the label: an "or a Follower" there makes it one alternative.</summary>
    internal static Restriction? Parse(string phrase, bool performer, string before = "", string tail = "")
    {
        phrase = phrase.Trim();
        if (phrase.Length == 0) return null;
        var words = Regex.Matches(phrase, @"[A-Za-z'-]+").Select(m => m.Value).ToArray();
        var lower = words.Select(w => w.ToLowerInvariant()).ToArray();
        var vocab = KeywordVocabulary.Value;
        var keywords = words.Where((w, i) => vocab.Contains(w) && !(i > 0 && lower[i - 1] == "non")).Distinct().ToArray();
        var notKeywords = words.Where((w, i) => i > 0 && lower[i - 1] == "non" && vocab.Contains(w)).Concat(words.Where(w => w.StartsWith("non-", StringComparison.OrdinalIgnoreCase) && vocab.Contains(w[4..])).Select(w => w[4..])).Distinct().ToArray();
        var nouns = lower.Where(w => NounWords.Contains(w)).ToArray();
        if (nouns.Length == 0 && keywords.Length == 0 && notKeywords.Length == 0) return null;
        var phraseOr = lower.Contains("or");
        if (AlternativeTail.IsMatch(tail)) return null;
        if (phraseOr && (keywords.Length > 0 || lower.Contains("your") && !performer)) return null;
        var side = performer || lower.Contains("your") || before.Trim().EndsWith("your", StringComparison.OrdinalIgnoreCase) ? Side.Own
            : lower.Contains("enemy") || lower.Contains("opponent's") || lower.Contains("opponent") || lower.Contains("another") && lower.Contains("player's") ? Side.Enemy : Side.Any;
        var bow = lower.Contains("unbowed") ? Bow.Unbowed : lower.Contains("bowed") ? Bow.Bowed : Bow.Any;
        var honor = lower.Contains("dishonorable") ? Honor.Dishonorable : lower.Contains("honorable") ? Honor.Honorable : Honor.Any;
        var personalityOnly = nouns.Length > 0 && nouns.All(w => w is "personality" or "personalities") && !phraseOr;
        return new(phrase, side, bow, honor, personalityOnly, keywords, false, notKeywords);
    }

    /// <summary>The performer and target restrictions of a label (the words before its colon), each null when the label states none.</summary>
    internal static (Restriction? Performer, Restriction[] Targets) Restrictions(string label)
    {
        var colon = label.IndexOf(':');
        if (colon < 0) return (null, []);
        var head = label[..colon];
        Restriction? performer = null;
        var performing = Regex.Match(head, @"\bperforming\s+(?<p>[^,:]*)", RegexOptions.IgnoreCase);
        if (performing.Success) performer = Parse(Trim(performing.Groups["p"].Value), true);
        var targets = new List<Restriction>();
        foreach (Match m in Regex.Matches(head, @"(?<pre>(?:\S+\s+){0,2})\btargets?\s+(?<p>[^:]*)", RegexOptions.IgnoreCase))
        {
            var raw = m.Groups["p"].Value;
            var phrase = Trim(raw);
            if (Parse(phrase, false, m.Groups["pre"].Value, raw[phrase.Length..]) is { } parsed) targets.Add(parsed);
        }
        if (Regex.IsMatch(head, @",\s*or\s+target", RegexOptions.IgnoreCase)) targets.Clear(); // "target a Personality ..., or target a Follower": two alternatives
        return (performer, targets.ToArray());
    }

    private static string Trim(string phrase)
    {
        var end = PhraseEnd.Match(phrase);
        var head = end.Success ? phrase[..end.Index] : phrase;
        // "an enemy Personality or Follower" keeps its "or"; a trailing article is dropped
        return head.Trim();
    }

    private static void Parser()
    {
        var (performer, targets) = Restrictions("Choose your performing Paragon and target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 2 Honor");
        Batch336Support.Check(performer is { Side: Side.Own, Bow: Bow.Any } && performer.Keywords.SequenceEqual(["Paragon"]), "the Paragon performer");
        Batch336Support.Check(targets is [{ Side: Side.Enemy, PersonalityOnly: true }], "the enemy Personality target: " + string.Join("; ", targets.Select(t => t.Phrase)));
        var (p2, t2) = Restrictions("Choose your performing unbowed Commander and target an enemy card with as many or fewer Followers than the performer: Bow it. Negate its next straightening (this turn)");
        Batch336Support.Check(p2 is { Bow: Bow.Unbowed } && t2 is [{ Side: Side.Enemy, PersonalityOnly: false }], "the Commander / enemy card");
        var (_, t3) = Restrictions("Target an unbowed honorable Personality or Follower: Give him +2F");
        Batch336Support.Check(t3 is [{ Side: Side.Any, Bow: Bow.Unbowed, Honor: Honor.Honorable, PersonalityOnly: false }], "unbowed honorable Personality or Follower");
        var (_, t4) = Restrictions("Target your dishonorable Shugenja: Honor him");
        Batch336Support.Check(t4 is [{ Side: Side.Own, Honor: Honor.Dishonorable }] && t4[0].Keywords.SequenceEqual(["Shugenja"]), "your dishonorable Shugenja");
    }

    // ---- the table ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly string[] Windowed = ["Battle", "Open", "Limited", "BattleOrOpen"];

    /// <summary>One Personality of every side x bow state x honor status x keyword combination, added to the generic table.</summary>
    private static MatchEngine WithGrid(MatchEngine built, string scenario, int me, string[] keywords)
    {
        var battle = scenario.StartsWith("battle");
        return Batch336Support.Edit(built, s =>
        {
            foreach (var side in new[] { me, 1 - me })
                foreach (var bowed in new[] { false, true })
                    foreach (var dishonorable in new[] { false, true })
                        foreach (var keyworded in new[] { false, true })
                        {
                            var card = Batch336Support.Add(s, side == 0 ? "training-crane-samurai" : "training-lion-samurai", side, battle ? Zone.Battlefield : Zone.Home, bowed, battle ? 0 : null);
                            card.Dishonorable = dishonorable;
                            if (keyworded) foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent));
                        }
        });
    }

    private static bool Breaks(Restriction r, MatchEngine e, CardInstance card, int me, out string reason)
    {
        reason = "";
        var definition = CardCatalog.Get(card.DefinitionId);
        var personality = definition.Type == CardType.Personality;
        if (r.Side == Side.Own && card.OwnerId != me) reason = "an enemy card where the label says your own";
        else if (r.Side == Side.Enemy && card.OwnerId == me) reason = "your own card where the label says enemy";
        else if (r.Bow == Bow.Unbowed && card.Bowed) reason = "a bowed card where the label says unbowed";
        else if (r.Bow == Bow.Bowed && !card.Bowed) reason = "an unbowed card where the label says bowed";
        else if (personality && r.Honor == Honor.Honorable && card.Dishonorable) reason = "a dishonorable Personality where the label says honorable";
        else if (personality && r.Honor == Honor.Dishonorable && !card.Dishonorable) reason = "an honorable Personality where the label says dishonorable";
        else if (r.PersonalityOnly && !personality) reason = $"a {definition.Type} where the label says Personality";
        else if (r.Keywords.Length > 0 && !(r.AnyKeyword ? r.Keywords.Any(k => e.HasKeyword(card.Id, k)) : r.Keywords.All(k => e.HasKeyword(card.Id, k)))) reason = $"a card without {string.Join(r.AnyKeyword ? " or " : " and ", r.Keywords)}";
        else if (r.NotKeywords.Any(k => e.HasKeyword(card.Id, k))) reason = $"a card with {string.Join(" or ", r.NotKeywords)} where the label says non-";
        return reason.Length > 0;
    }

    internal sealed record Outcome(string Card, string Ability, string Timing, string Label, bool Offered, bool EverEnabled, string? Reason);

    internal static (List<Violation> Violations, List<Outcome> Outcomes) Probe(IEnumerable<CardDefinition> cards)
    {
        var fullSweep = Environment.GetEnvironmentVariable("L5R_TARGET_AUDIT_OUT") is not null;
        var violations = new List<Violation>();
        var outcomes = new Dictionary<(string, string), Outcome>();
        foreach (var c in cards)
        {
            var abilities = (c.Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var d) && Windowed.Contains(d.Timing)).ToArray();
            if (abilities.Length == 0) continue;
            // The check needs only cards whose labels state a restriction; the full sweep (for the unplayable list) runs when a report file is asked for.
            if (!fullSweep && !abilities.Any(a => { var (p, ts) = Restrictions(AbilityCatalog.All[a].Label); return p is { Any: false } || ts.Any(x => !x.Any); })) continue;
            var keywords = MultiWindowAuditTestsKeywords(c);
            foreach (var scenario in MultiWindowAuditTests.Scenarios.Where(s => abilities.Any(a => MultiWindowAuditTests.Expected(AbilityCatalog.All[a].Timing).Contains(s))))
                for (var variant = 0; variant < (c.Type is CardType.Spell or CardType.Ring ? 2 : 1); variant++)
                {
                    MatchEngine e; CardInstance source; int me;
                    try
                    {
                        (e, source, me) = MultiWindowAuditTests.Build(c, scenario, variant);
                        e = WithGrid(e, scenario, me, keywords);
                    }
                    catch (Exception) { continue; }
                    ActionOption[] all;
                    try { all = e.QueryActions(me); } catch (Exception) { continue; }
                    var state = e.State;
                    foreach (var id in abilities)
                    {
                        var ability = AbilityCatalog.All[id];
                        var mine = all.Where(a => a.Type == "useAbility" && a.CardId == source.Id && (a.Command?.AbilityId == id || a.Command is null && (a.Label == ability.Label || a.Label.StartsWith(ability.Label + " · ")))).ToArray();
                        var enabled = mine.Where(a => a.Enabled && a.Command?.AbilityId == id).ToArray();
                        var key = (c.Id, id);
                        var previous = outcomes.GetValueOrDefault(key);
                        outcomes[key] = new(c.Id, id, ability.Timing, ability.Label, previous?.Offered == true || mine.Length > 0, previous?.EverEnabled == true || enabled.Length > 0, enabled.Length > 0 ? null : mine.FirstOrDefault()?.Reason ?? previous?.Reason);
                        if (enabled.Length == 0) continue;
                        var (performer, targets) = Restrictions(ability.Label);
                        if (ability.Performer is not null) { performer ??= targets.FirstOrDefault(x => x.Side == Side.Own); targets = targets.Where(x => x.Side != Side.Own).ToArray(); } // "Target your opposed Paragon" names the performer
                        if (performer is not null)
                            foreach (var pid in enabled.Select(a => a.Command!.PerformerId).OfType<string>().Distinct())
                                if (state.Cards.TryGetValue(pid, out var p) && Breaks(performer, e, p, me, out var why))
                                    violations.Add(new(c.Id, id, scenario, "performer", $"{performer.Phrase} -> {why}", $"{Describe(e, p)}"));
                        if (targets.Length == 1 && ability.Slots is null && ability.Target is not ("none" or "player" or "any-province" or "enemy-province" or "provinces"))
                            foreach (var tid in enabled.SelectMany(a => a.TargetIds ?? []).Distinct())
                                if (state.Cards.TryGetValue(tid, out var t) && Breaks(targets[0], e, t, me, out var why))
                                    violations.Add(new(c.Id, id, scenario, "target", $"{targets[0].Phrase} -> {why}", $"{Describe(e, t)}"));
                    }
                }
        }
        return (violations, outcomes.Values.ToList());
    }

    private static string Describe(MatchEngine e, CardInstance card) =>
        $"{CardCatalog.Get(card.DefinitionId).Name} ({card.Id}, owner {card.OwnerId}{(card.Bowed ? ", bowed" : "")}{(card.Dishonorable ? ", dishonorable" : "")}, {card.Zone})";

    private static string[] MultiWindowAuditTestsKeywords(CardDefinition c)
    {
        var fromCard = (c.Abilities ?? []).SelectMany(a => AbilityCatalog.All.TryGetValue(a, out var d) ? d.RequiredKeywords ?? [] : []);
        // every keyword any label in the catalogue names, so the keyworded half of the grid can satisfy any phrase
        return LabelKeywords.Value.Concat(fromCard).Distinct().ToArray();
    }

    private static readonly Lazy<string[]> LabelKeywords = new(() => AbilityCatalog.All.Values
        .SelectMany(a => { var (p, t) = Restrictions(a.Label); return (p is null ? [] : p.Keywords).Concat(t.SelectMany(x => x.Keywords)); })
        .Concat(AbilityCatalog.All.Values.SelectMany(a => a.RequiredKeywords ?? []))
        .Distinct().ToArray());

    // ---- the checks ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static IEnumerable<CardDefinition> Cards() => CardCatalog.Cards.Values.Where(c => !c.CreatedOnly).OrderBy(c => c.Id, StringComparer.Ordinal);

    private static void NoRestrictionBroken()
    {
        var (violations, outcomes) = Probe(Cards());
        var path = Environment.GetEnvironmentVariable("L5R_TARGET_AUDIT_OUT");
        if (path is not null)
        {
            var report = new StringBuilder();
            report.AppendLine($"# {violations.Count} violations; {outcomes.Count} abilities probed; {outcomes.Count(o => !o.EverEnabled)} never enabled on any table");
            foreach (var v in violations.OrderBy(v => v.Card).ThenBy(v => v.Ability)) report.AppendLine($"VIOLATION\t{v.Card}\t{v.Ability}\t{v.Scenario}\t{v.Role}\t{v.Rule}\t{v.Offered}");
            foreach (var o in outcomes.Where(o => !o.EverEnabled).OrderBy(o => o.Card)) report.AppendLine($"UNPLAYABLE\t{o.Card}\t{o.Ability}\t{o.Timing}\t{(o.Offered ? "offered" : "not offered")}\t{o.Reason}\t{o.Label}");
            File.WriteAllText(path, report.ToString());
        }
        var unreviewed = violations.Where(v => !Reviewed.Contains((v.Card, v.Ability))).ToList();
        Batch336Support.Check(unreviewed.Count == 0, $"{unreviewed.Count} unreviewed restriction violations:\n" + string.Join("\n", unreviewed.Take(40).Select(v => $"  {v.Card} {v.Ability} [{v.Scenario}] {v.Role}: {v.Rule}: {v.Offered}")));
    }

    /// <summary>(card, ability) pairs whose reported violation was reviewed against the printed card and is not one (the label parser read a word that does not restrict the slot).</summary>
    private static readonly HashSet<(string Card, string Ability)> Reviewed =
    [("oracle-l5r-10348", "wl-blood-wave-attack"),
     ("oracle-l5r-10131", "scent-of-dishonor-bow")]; // "a Kolat, Ninja, Shadowlands, or dishonorable Personality" is a list of alternatives (TargetMustBe), not "a Kolat" // "destroy your target Personality or Follower" is its cost; the Attacks' own targets are chosen separately

    private static void SuretyOfPurpose()
    {
        var card = CardCatalog.Cards.Values.First(c => (c.Abilities ?? []).Contains("surety-of-purpose"));
        foreach (var scenario in new[] { "battle-attacker", "battle-defender" })
        {
            var (built, source, me) = MultiWindowAuditTests.Build(card, scenario);
            var e = WithGrid(built, scenario, me, ["Paragon"]);
            var state = e.State;
            var offered = Batch336Support.Options(e, me, "surety-of-purpose", source.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
            Batch336Support.Check(offered.Length > 0, $"surety-of-purpose offered nothing in {scenario}");
            foreach (var id in offered)
                Batch336Support.Check(state.Cards[id].OwnerId != me && CardCatalog.Get(state.Cards[id].DefinitionId).Type == CardType.Personality, $"surety-of-purpose offered {Describe(e, state.Cards[id])} in {scenario}");
            var ownTarget = state.Cards.Values.First(x => x.OwnerId == me && x.Id != source.Id && x.Zone == Zone.Battlefield && !x.Bowed && CardCatalog.Get(x.DefinitionId).Type == CardType.Personality);
            var performer = Batch336Support.Options(e, me, "surety-of-purpose", source.Id).FirstOrDefault(o => o.Enabled)?.Command?.PerformerId;
            var direct = Batch336Support.Use(e, me, "surety-of-purpose", source.Id, ownTarget.Id, performer);
            var error = Batch336Support.Rejection(e, direct);
            Batch336Support.Check(error.Length > 0, "your own Personality was accepted as the target of Surety of Purpose");
        }
    }
}
