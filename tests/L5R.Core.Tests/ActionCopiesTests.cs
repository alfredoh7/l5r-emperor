using L5R.Core;

// Issue #908 (Mendon's gameplay note): the once-per-turn limit of an action belongs to the INSTANCE of the card, not to its name. Three copies of a non-unique Strategy each perform
// the same Battle action in the same battle.
internal static class ActionCopiesTests
{
    public static void Run(TestRunner runner) => runner.Run("action-copies", [
        ("A Paragon's Strength: three copies each perform the same Battle action in one battle", ParagonThreeCopies),
        ("Every non-unique Strategy's action can be performed once for each of three copies in the same phase (no copy is barred by a use of another)", EveryStrategyThreeCopies),
    ]);

    private static readonly string[] Windowed = ["Battle", "Open", "Limited", "BattleOrOpen"];

    /// <summary>Plays the offered command and settles the windows it opened until the acting player holds priority again; false when it could not be settled.</summary>
    private static bool Play(MatchEngine e, int me, ActionOption offered)
    {
        var target = offered.TargetIds?.FirstOrDefault();
        var command = offered.Command! with { TargetId = target ?? offered.Command.TargetId };
        if (!e.Execute(command).Accepted) return false;
        for (var guard = 0; guard < 24 && (e.State.PendingChoice is not null || e.State.PriorityPlayerId != me); guard++)
        {
            var who = e.State.PendingChoice?.PlayerId is int p && e.State.PriorityPlayerId != p ? p : e.State.PriorityPlayerId;
            var step = e.QueryActions(who).FirstOrDefault(o => o.Enabled && o.Command is not null && o.Type is "declineReaction" or "pass");
            if (step is null || !e.Execute(step.Command!).Accepted) return false;
        }
        return e.State.PendingChoice is null && e.State.PriorityPlayerId == me;
    }

    internal static List<string> CopyViolations(IEnumerable<CardDefinition> cards)
    {
        var found = new List<string>();
        foreach (var c in cards)
            foreach (var id in (c.Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var d) && Windowed.Contains(d.Timing)))
            {
                var timing = AbilityCatalog.All[id].Timing;
                var scenario = MultiWindowAuditTests.Expected(timing)[0];
                try
                {
                    var (built, source, me) = MultiWindowAuditTests.Build(c, scenario);
                    var e = Batch336Support.Edit(built, s => { for (var i = 0; i < 2; i++) Batch336Support.Add(s, c.Id, me, Zone.Hand); s.Players[me].Honor = 40; });
                    var copies = e.State.Cards.Values.Where(x => x.DefinitionId == c.Id && x.Zone == Zone.Hand && x.OwnerId == me).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
                    if (copies.Length < 3) continue;
                    for (var i = 0; i < 3; i++)
                    {
                        var options = Batch336Support.Options(e, me, id, copies[i].Id);
                        var offered = options.FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == id);
                        if (offered is null)
                        {
                            if (i > 0 && options.Length > 0) found.Add($"{c.Name} [{id}] copy {i + 1} of 3 was not offered after copy {i} was played: {options[0].Reason}");
                            break;
                        }
                        if (i == 2 || !Play(e, me, offered)) break;
                    }
                }
                catch (Exception) { }
            }
        return found;
    }

    private static void EveryStrategyThreeCopies()
    {
        var cards = CardCatalog.Cards.Values.Where(c => !c.CreatedOnly && c.Type == CardType.Strategy && !(c.Keywords ?? []).Contains("Unique")).OrderBy(c => c.Id, StringComparer.Ordinal);
        var found = CopyViolations(cards);
        var path = Environment.GetEnvironmentVariable("L5R_COPIES_OUT");
        if (path is not null) File.WriteAllLines(path, found);
        var useLimit = found.Where(f => f.Contains("within its limit", StringComparison.Ordinal) || f.Contains("already been used", StringComparison.Ordinal)).ToList();
        Batch336Support.Check(useLimit.Count == 0, "a later copy was barred by the use limit of an earlier one (the limit belongs to the instance of the card):" + Environment.NewLine + string.Join(Environment.NewLine, useLimit));
        var unreviewed = found.Where(f => !Reviewed.Any(r => f.Contains(r))).ToList();
        Batch336Support.Check(unreviewed.Count == 0, $"{unreviewed.Count} abilities barred a later copy:{Environment.NewLine}" + string.Join(Environment.NewLine, unreviewed.Take(40)));
    }

    /// <summary>Findings reviewed against the printed card and kept: each names the card and ability and says why the later copy may be barred.</summary>
    // None of these is a use limit (no finding says "already been used"): the copy before it changed the table -- it used up the only legal target, performer or discard-pile card, put a Terrain
    // into the battlefield (one per battlefield), moved the unit away, or the card prints its own limit (Trade Embargo: one copy per turn; a Personality wears one Armor and one Weapon).
    private static readonly string[] Reviewed = [
        "[ascending-ranks-open]",
        "[at-any-cost-recruit]",
        "[beyond-the-line-recon]",
        "[black-marketeering-take]",
        "[crushing-destroy]",
        "[defensive-stance]",
        "[harsh-taskmaster-attach]",
        "[inspirational-address-attach]",
        "[moving-unmoving-retreat]",
        "[on-the-hunt-equip]",
        "[oyos-second-lesson-reborn]",
        "[pe-govern-the-land-attach]",
        "[pe-govern-the-land-holding]",
        "[phantom-visage-bow]",
        "[preserving-forces-open]",
        "[proxy677-never-without-weapon]",
        "[readied-steel-attach]",
        "[selfless-defense-destroy]",
        "[stay-put-bow-attach]",
        "[unstable-ground-destroy]",
        "[w732-embargo]",
        "[w736-harbor-play]",
        "[w741-prayer]",
        "[w835-warded-play]",
        "[wl-tactical-sacrifice]",
        "[wlsac-suffer-the-consequences]",
        "[wlsac-thoughtless-sacrifice]"];

    private const string Paragon = "oracle-l5r-61";

    private static void ParagonThreeCopies()
    {
        foreach (var (ability, attacker) in new[] { ("paragon-strength-attack", 0), ("paragon-strength-move", 0), ("paragon-strength-attack", 1), ("paragon-strength-move", 1) })
        {
            var s = Batch336Support.Table(Phase.Battle, attacker); s.PriorityPlayerId = 0;
            var paragon = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            paragon.GrantedKeywords.Add(new("Paragon", "test-fixture", GrantDuration.Permanent));
            for (var i = 0; i < 4; i++) Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Batch336Support.Add(s, "training-attack-reactor", 1, Zone.Home); // the opponent holds a Reaction to an Attack: every Attack opens its window
            var copies = Enumerable.Range(0, 3).Select(_ => Batch336Support.Add(s, Paragon, 0, Zone.Hand)).ToArray();
            s.Players[0].Honor = 40;
            var e = MatchEngine.FromTestState(s);
            var played = 0;
            foreach (var copy in copies)
            {
                var offered = Batch336Support.Options(e, 0, ability, copy.Id).FirstOrDefault(o => o.Enabled);
                Batch336Support.Check(offered is not null, $"{ability}: copy {played + 1} of 3 was not offered: " + string.Join("; ", Batch336Support.Options(e, 0, ability, copy.Id).Select(o => $"[{o.Enabled}] {o.Reason}")));
                var target = offered!.TargetIds?.FirstOrDefault();
                RegressionAssertions.Accept(e, offered.Command! with { TargetId = target ?? offered.Command.TargetId });
                for (var guard = 0; e.State.PendingChoice is not null && guard < 8; guard++) RegressionAssertions.Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
                if (e.State.PriorityPlayerId != 0) RegressionAssertions.Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
                played++;
            }
            Batch336Support.Eq(played, 3, ability);
        }
    }
}
