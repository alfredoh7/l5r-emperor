using L5R.Core;
using static RegressionAssertions;

// Issue #915 (validation report EE-STATE-036, EE-Card-types-097): a Spell may stay attached after its host stops being a Shugenja, but its actions are unavailable until he is one again.
// The rule lives in AbilityIdsOf (CardCopy.cs), the one place every listing, validation and Reaction scan reads, so it covers every Spell, not one card.
internal static class SpellShugenjaHostTests
{
    private const string JadeEmbrace = "oracle-l5r-9985", Shugenja = "training-shugenja", Crane = "training-crane-samurai", DualAttacker = "training-dual-attacker";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    public static void Run(TestRunner runner) => runner.Run("spell-shugenja-host", [
        ("Jade Embrace: listed and accepted for a Shugenja; unlisted and refused once he loses Shugenja; back when he regains it", LoseAndRegain),
        ("Jade Embrace: a host that is granted Shugenja by an effect makes the attached Spell's action available", GrantedShugenja),
        ("Every published Spell with actions has none while attached to a non-Shugenja, and all of them with a Shugenja", EverySpell),
    ]);

    private static (MatchState State, CardInstance Host, CardInstance Spell, CardInstance Foe) Position()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var host = Add(s, Shugenja, 0, Zone.Battlefield, province: 0);
        var spell = Attach(s, JadeEmbrace, host);
        var foe = Add(s, DualAttacker, 1, Zone.Battlefield, province: 0);
        return (s, host, spell, foe);
    }

    private static bool Listed(MatchEngine e, string spellId) => e.QueryActions(0).Any(a => a.Enabled && a.Command?.CardId == spellId && a.Command.AbilityId == AbilityRemovalAbilities.JadeEmbrace);

    private static void LoseAndRegain()
    {
        var (s, host, spell, foe) = Position();
        var e = MatchEngine.FromTestState(s);
        Check(Listed(e, spell.Id), "a Shugenja's Spell offers its action");
        // The host loses Shugenja (a removed keyword): the Spell stays attached but is silent.
        var lost = Edit(e, st => st.Cards[host.Id].RemovedKeywords.Add("Shugenja"));
        Eq(lost.State.Cards[spell.Id].AttachedTo, host.Id, "the Spell remains attached");
        Check(!Listed(lost, spell.Id), "its action is not listed");
        Check(lost.AbilityIdsForTest(spell.Id).Length == 0, "and the Spell has no abilities to scan");
        var command = Batch336Support.Use(lost, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, foe.Id);
        var version = lost.Version;
        Reject(lost, command);
        Eq(lost.Version, version, "the refused command changes nothing");
        Check(!lost.State.Cards[spell.Id].Bowed, "the Spell was not bowed");
        // He regains it: the action returns, listed and executed.
        var back = Edit(lost, st => st.Cards[host.Id].RemovedKeywords.Remove("Shugenja"));
        Check(Listed(back, spell.Id), "regained: listed again");
        Accept(back, Batch336Support.Offer(back, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, foe.Id));
        Check(back.State.Cards[spell.Id].Bowed, "and it executes: the Spell bowed as its cost");
    }

    private static void GrantedShugenja()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var host = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var spell = Attach(s, JadeEmbrace, host);
        var foe = Add(s, DualAttacker, 1, Zone.Battlefield, province: 0);
        var plain = MatchEngine.FromTestState(s);
        Check(!Listed(plain, spell.Id), "a Spell on a Personality who is not a Shugenja is silent");
        Reject(plain, Batch336Support.Use(plain, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, foe.Id));
        var granted = Edit(plain, st => st.Cards[host.Id].GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)));
        Check(Listed(granted, spell.Id), "granted Shugenja: the action is available");
        Accept(granted, Batch336Support.Offer(granted, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, foe.Id));
    }

    private static void EverySpell()
    {
        var spells = CardCatalog.Cards.Values.Where(c => c.Type == CardType.Spell && !c.CreatedOnly && (c.Abilities ?? []).Length > 0).ToArray();
        Check(spells.Length > 20, "the audit covers the published Spells: " + spells.Length);
        var silenced = 0;
        foreach (var definition in spells)
        {
            var s = Batch336Support.Table(Phase.Battle, 0);
            var shugenja = Add(s, Shugenja, 0, Zone.Battlefield, province: 0);
            var other = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            var onShugenja = Attach(s, definition.Id, shugenja);
            var onOther = Attach(s, definition.Id, other);
            var e = MatchEngine.FromTestState(s);
            Check(e.AbilityIdsForTest(onOther.Id).Length == 0, $"{definition.Name}: no abilities while attached to a non-Shugenja");
            Check(e.AbilityIdsForTest(onShugenja.Id).Length > 0, $"{definition.Name}: abilities return on a Shugenja");
            var listed = e.QueryActions(0).Where(a => a.Enabled && a.Command?.CardId == onOther.Id).ToArray();
            Check(listed.Length == 0, $"{definition.Name}: nothing listed for the Spell on a non-Shugenja");
            silenced++;
        }
        Eq(silenced, spells.Length);
    }
}
