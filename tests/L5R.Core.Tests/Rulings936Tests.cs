using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #936, Mendon's rulings of 2026-10-13 (R-084, R-085): Great Sacrifice compares the enemy unit's TOTAL Force (bowed Followers included); a Spell's STATIC effect follows its printed subject
// ("This Shugenja ..." is silent on a host who is not currently a Shugenja; "This Personality ..." is not). docs/decisions/2026-10-13-spell-static-subject.md.
internal static class Rulings936Tests
{
    private const string GreatSacrifice = "oracle-l5r-2900", Immolation = "oracle-l5r-10127", ScouringFlood = "oracle-l5r-6502", Tempest = "oracle-l5r-7866", Suitengu = "oracle-l5r-10133";

    public static void Run(TestRunner runner) => runner.Run("rulings-936", [
        ("Great Sacrifice compares the enemy unit's total Force: a bowed Follower counts, so the unit is out of reach; with total within reach it is a legal target, and listing, validation and execution agree", GreatSacrificeTotal),
        ("A Shugenja-subject Spell's static Force (Immolation) is silent on a non-Shugenja host, works on a Shugenja, and follows a host who loses and regains Shugenja", ShugenjaSpellForce),
        ("A This-Personality Spell (Scouring Flood, Tempest of Flame) keeps its +F on a non-Shugenja host", PersonalitySpellForce),
        ("A keyword granted by a Shugenja-subject Spell (Suitengu's Path: Naval) follows the host's Shugenja status", ShugenjaSpellKeyword),
        ("Audit: every published Spell whose static text says 'This Shugenja has' is in the silenced list, none that says 'This Personality has' is, and the list holds only Spells", AuditList),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static CardInstance Shugenja(CardInstance c) { c.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); return c; }

    private static void GreatSacrificeTotal()
    {
        // Performer Force 3 + Personal Honor 2 = reach 5 (attacking).
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, GreatSacrifice, 0, Zone.Hand);
        var heavy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);                  // Force 3 ...
        Add(s, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: heavy.Id);        // ... + a bowed Follower of Force 2 = total 5: within reach
        var tooHeavy = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);               // Force 5 ...
        Add(s, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: tooHeavy.Id);     // ... + a bowed Follower = total 7: beyond it (its battle contribution is only 5)
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "great-sacrifice-destroy", strategy.Id, performer.Id);
        var legal = option.TargetIds!.ToHashSet();
        Check(legal.Contains(heavy.Id), "a unit whose total (bowed Follower included) is within the reach is a target");
        Check(!legal.Contains(tooHeavy.Id), "a unit whose total exceeds the reach only through a bowed Follower is not");
        Reject(e, option.Command! with { TargetId = tooHeavy.Id });
        Accept(e, option.Command! with { TargetId = heavy.Id });
        Eq(e.State.Cards[heavy.Id].Zone, Zone.Dead, "the legal target is destroyed");
    }

    private static void ShugenjaSpellForce()
    {
        var s = Table(Phase.Battle, 0);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);           // Force 3, no Shugenja
        Add(s, Immolation, 0, Zone.Attached, province: 0, parent: plain.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == plain.Id).Force, 3, "a non-Shugenja host gets nothing from 'This Shugenja has +3F'");

        var s2 = Table(Phase.Battle, 0);
        var wielder = Shugenja(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0));
        Add(s2, Immolation, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.GetView(0).Players[0].Army.Single(c => c.Id == wielder.Id).Force, 6, "a Shugenja host gets +3F");

        // Loses the keyword: silent again; regains it: back.
        var lost = Edit(e2, st => { st.Cards[wielder.Id].GrantedKeywords.Clear(); st.Cards[wielder.Id].RemovedKeywords.Add("Shugenja"); });
        Eq(lost.GetView(0).Players[0].Army.Single(c => c.Id == wielder.Id).Force, 3, "a host who stops being a Shugenja loses the bonus");
        var regained = Edit(lost, st => { st.Cards[wielder.Id].RemovedKeywords.Clear(); st.Cards[wielder.Id].GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); });
        Eq(regained.GetView(0).Players[0].Army.Single(c => c.Id == wielder.Id).Force, 6, "and has it back when he is a Shugenja again");
    }

    private static void PersonalitySpellForce()
    {
        var s = Table(Phase.Battle, 0);
        var a = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, ScouringFlood, 0, Zone.Attached, province: 0, parent: a.Id);
        var b = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, Tempest, 0, Zone.Attached, province: 0, parent: b.Id);
        var e = MatchEngine.FromTestState(s);
        var army = e.GetView(0).Players[0].Army;
        Eq(army.Single(c => c.Id == a.Id).Force, 5, "Scouring Flood: 'This Personality has +2F' on a non-Shugenja");
        Eq(army.Single(c => c.Id == b.Id).Force, 5, "Tempest of Flame: 'This Personality has +2F' on a non-Shugenja");
        var shugenja = Edit(e, st => { Shugenja(st.Cards[a.Id]); Shugenja(st.Cards[b.Id]); });
        Eq(shugenja.GetView(0).Players[0].Army.Single(c => c.Id == a.Id).Force, 5, "and unchanged on a Shugenja");
    }

    private static void ShugenjaSpellKeyword()
    {
        var s = Table(Phase.Battle, 0);
        var host = Shugenja(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0));
        var spell = Add(s, Suitengu, 0, Zone.Attached, province: 0, parent: host.Id);
        host.GrantedKeywords.Add(new("Naval", spell.Id, GrantDuration.Attachment));
        var e = MatchEngine.FromTestState(s);
        Check(e.KeywordForTest(host.Id, "Naval"), "a Shugenja host has the Naval the Spell gives");
        var lost = Edit(e, st => { st.Cards[host.Id].GrantedKeywords.RemoveAll(g => g.Keyword == "Shugenja"); });
        Check(!lost.KeywordForTest(host.Id, "Naval"), "a host who is no Shugenja does not");
        var back = Edit(lost, st => st.Cards[host.Id].GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)));
        Check(back.KeywordForTest(host.Id, "Naval"), "and has it again when he regains Shugenja");
    }

    private static void AuditList()
    {
        foreach (var id in MatchEngine.ShugenjaSubjectStaticSpells)
            Check(CardCatalog.Cards.TryGetValue(id, out var d) && d.Type == CardType.Spell, id + " is a published Spell");
        foreach (var spell in CardCatalog.Cards.Values.Where(c => c.Type == CardType.Spell && c.Id.StartsWith("oracle-l5r-", StringComparison.Ordinal)))
        {
            var text = spell.Text ?? "";
            if (text.Contains("This Shugenja has") || text.Contains("While this Shugenja is") && text.Contains(", he has") || text.Contains("This Shugenja gets"))
                Check(MatchEngine.ShugenjaSubjectStaticSpells.Contains(spell.Id) || text.Contains("Battle:") && !text.Contains("This Shugenja has") && !text.Contains(", he has"), spell.Name + " names the Shugenja as the subject of a static effect but is not in the silenced list");
            if (text.Contains("This Personality has"))
                Check(!MatchEngine.ShugenjaSubjectStaticSpells.Contains(spell.Id), spell.Name + " says 'This Personality' and must not be silenced");
        }
    }
}
