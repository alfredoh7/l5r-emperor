using L5R.Core;
using static RegressionAssertions;

// Audit of every Melee/Ranged Attack ability against the core rules' Choose Performers: an action on a Personality or Follower is performed by that card, an Item's or Spell's by the
// Personality it is attached to, and an explicit performer is only for an action that NAMES one ("your performing ..."). A Strategy's Ranged Attack may have no performer at all.
internal static class AttackPerformerAuditTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);

    // Attack abilities that carry an explicit Performer, each reviewed against the printed text. The last five do NOT print a named performer: the Performer field there only picks a card
    // to bow (Yayu/Tomiko: one of the source's own Spells, so the acting Personality is still the source) or a target (Strength of the Divine's Paragon, a Strategy); Carpenter Castle's
    // bowed Castle/Siege card is not its performer (ActionPerformer returns the Stronghold).
    // Forgotten Legacy pass (#467), reviewed in #476: Toritaka Chokichi ("Bow your performing Follower"), Never Beyond My Reach ("your performing unbowed Kensai") and Pearls & Spirits'
    // three Attacks ("your performing Shugenja") each print a named performer. The Shadow's Embrace pass (#476), batch 2: Moto Morio ("Choose your performing Follower").
    // The Shadow's Embrace pass (#476), batch 3: Burning Fury ("Choose your performing unbowed Personality: Melee Attack") and Lessons Never Forgotten ("Choose your performing unbowed Yojimbo:
    // Melee 4 Attack") each print a named performer.
    // Thunder's Wrath (Forgotten Legacy pass, reviewed in #476): its own-wielder Performer names the Spell's wielder, the core rules' performer of a Spell's action anyway -- needed only because
    // the Spell is destroyed as the cost ("this Shugenja" is read after it has left play).
    // Iuchi Haruma (The Shadow's Embrace pass, issue #476 batch 1): "Bow one of Haruma's Spells: Give him +2F. Ranged 4 Attack." -- Tamori Yayu's shape: the Spell is named only as
    // the cost it pays (bowed), and the Attack is Haruma's own (the card it is printed on performs it, core rules Choose Performers).
    // Issue #477: Daigotsu Misaki ("Bow Misaki's performing Follower"), and -- reviewed against their printed text when this pass found the list already behind main -- Toritaka Chokichi ("Bow your performing
    // Follower"), Never Beyond My Reach ("choose your performing unbowed Kensai") and Pearls & Spirits ("Bow your performing Shugenja", three ids), all named performers. Thunder's Wrath prints no performer: the
    // Performer field there only picks the Spell (or its Shugenja) to destroy as a cost.
    // Emperor Edition Stronghold pass (issue #486), batch 2: Shiro Chugo ("Choose your performing unbowed Personality: Melee 4 Attack") prints a named performer.
    // Emperor Edition Stronghold pass (issue #486), batch 3: The Otoro Estate ("choose your performing unbowed Ninja Personality") names its performer, both halves.
    // Emperor Edition remaining-cards pass, group A (issue #492): Breath of the Heavens ("Choose your performing unbowed Fire or Tattooed Personality") and A Paragon's Strength ("Choose your performing unbowed Paragon") name theirs.
    // Emperor Edition remaining-cards pass (issue #492, group C): Strategic Strike ("Bow your performing Scout Personality: Ranged 6 Attack") prints a named performer.
    // Emperor Edition remaining-cards pass (issue #492), group D: Unseen Assailant ("Bow your performing Ninja: Ranged 3 Attack") prints a named performer.
    // Emperor Edition remaining-cards pass (issue #492), group E: Akodo's Guidance ("choose your performing Personality") names its performer; Cleansing the Path ("Destroy this card: Ranged 4 Attack ... if the performing
    // Shugenja is Fire") prints none -- its Performer field only names the Spell's wielder, the performer of a Spell's action anyway, because the Spell is destroyed as the cost (Thunder's Wrath's shape).
    private static readonly string[] NamedPerformer = ["akio-attack", "akodo-guidance-melee", "breath-heavens-attack", "burning-fury-attack", "chokichi-attack", "chugo-melee", "junpei-attack", "lessons-never-forgotten-attack", "metsubushi-attack", "misaki-follower-attack", "monastery-fire-attack", "morio-attack", "never-beyond-reach-attack", "otoro-attack", "otoro-attack-move", "paragon-strength-attack", "pearls-spirits-chi-attack", "pearls-spirits-force-attack", "pearls-spirits-sacrifice-attack", "shiro-shiba-melee", "strategic-strike-attack", "tensin-follower-attack", "two-stings-attack", "two-stings-attack-from-discard", "unseen-assailant-attack",
        // Issue #503 (pass group P5): Tsubute ("Choose your performing Ninja", both ways to pay) and Shiba Jouta - exp ("choose your performing Shugenja", alive and dead) print named performers.
        "p5-tsubute-attack", "p5-tsubute-attack-hand", "p5-jouta-attack", "p5-jouta-attack-dead"];
    private static readonly string[] NotANamedPerformer = ["carpenter-castle-siege", "cleansing-path-attack", "haruma-attack", "strength-of-the-divine-force", "strength-of-the-divine-honor", "thunders-wrath-attack", "tomiko-attack", "yayu-bow-attack", "yayu-destroy-attack"];

    public static void Run(TestRunner runner) => runner.Run("attack-performer-audit", [
        ("Every Melee/Ranged Attack ability with an explicit Performer is one reviewed against its printed text", ExplicitPerformersAreReviewed),
        ("A card that prints an Attack without a named performer performs it itself (Item/Spell: its wielder), and only a Strategy or a Ring (False Ring of Water bows itself and performs its own action) is a non-Personality source", SourceTypes),
    ]);

    private static IEnumerable<KeyValuePair<string, AbilityDefinition>> Attacks() => AbilityCatalog.All.Where(a => a.Value.Target == "attack");

    private static void ExplicitPerformersAreReviewed()
    {
        var explicitIds = Attacks().Where(a => a.Value.Performer is not null).Select(a => a.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var reviewed = NamedPerformer.Concat(NotANamedPerformer).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check(explicitIds.SequenceEqual(reviewed), $"an Attack gained or lost an explicit Performer without review: {string.Join(",", explicitIds)}");
        foreach (var id in NamedPerformer)
            foreach (var card in CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Abilities!.Contains(id)))
                Check(card.Text.Contains("performing"), $"{card.Name} ({id}) has an explicit Performer but prints no 'performing'");
    }

    private static void SourceTypes()
    {
        foreach (var (id, ability) in Attacks().Where(a => a.Value.Performer is null))
            foreach (var card in CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Abilities!.Contains(id)))
                Check(card.Type is CardType.Personality or CardType.Follower or CardType.Item or CardType.Spell or CardType.Strategy or CardType.Ring
                    || id == "p5-foundries-attack" /* issue #503: The New Foundries, a Stronghold, prints a Ranged 4 Attack with no performer (a Stronghold is not in a unit; its Battle action names none, as a Strategy's) */,
                    $"{card.Name} ({id}) is a {card.Type}: check its performer against the core rules");
    }
}
