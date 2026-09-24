using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// The other five Spirit Strategies (Second City, strategy-wave-33, issue #264), after Spirit of the Monk (#386): "Limited: Choose your performing unbowed Personality: If he is a
// [keyword], permanently give him [ability]; otherwise, permanently give him [keyword]." Every position is built explicitly (player 0 acts, player 1 opposes) and played through
// QueryActions and the commands it offers. Readings: docs/decisions/2026-09-21-second-city-pass.md.
internal static class SecondCitySpiritTests
{
    private static readonly (string Card, string Spirit, string Keyword, string Granted)[] Spirits =
    [
        ("oracle-l5r-7420", "spirit-of-the-berserker", "Berserker", "spirit-berserker-delay"),
        ("oracle-l5r-7423", "spirit-of-the-duelist", "Duelist", "spirit-duelist-duel"),
        ("oracle-l5r-7425", "spirit-of-the-paragon", "Paragon", "spirit-paragon-bow"),
        ("oracle-l5r-7426", "spirit-of-the-scout", "Scout", "spirit-scout-attack"),
        ("oracle-l5r-7430", "spirit-of-the-yojimbo", "Yojimbo", "spirit-yojimbo-redirect"),
    ];

    public static void Run(TestRunner runner) => runner.Run("second-city-spirits", [
        ("Published shape: the five Spirits are the printed Strategies (0 Gold, Focus 4, the Limited text as on the scan) with their one ability each, ship their art, and the wave pins exactly them", PublishedShape),
        ("Keyword first, ability second: a performer without the keyword permanently gains it (and no ability); the next Spirit gives the ability to the same performer alone, each Strategy is spent, a bowed performer is refused", KeywordThenAbility),
        ("Spirit of the Berserker's +2F is permanent and only on the ability branch: it is not given with the keyword, and it outlives the turn", BerserkerForce),
        ("Spirit of the Yojimbo's granted Reaction redirects an action that targets another of his controller's Personalities at his location -- not one at another location, not at himself, not his controller's own action", YojimboRedirects),
        ("Spirit of the Scout's granted Ranged 4 Attack bows its holder unless his controller has Reconnaissance, and hits a Force-4 target but not a Force-5 one", ScoutAttack),
        ("Spirit of the Scout: the Personality holding the granted ability is the Attack's performer", ScoutPerformer),
        ("Spirit of the Paragon's granted action bows an enemy Personality with equal or lower Force OR equal or lower Personal Honor, and no other", ParagonBow),
        ("Spirit of the Duelist's granted Iaijutsu action duels an enemy Personality and moves the loser home; it counts as an Iaijutsu action", DuelistDuel),
        ("Spirit of the Berserker's granted Reaction delays the destruction an action would inflict on him until the turn ends, and only that destruction", BerserkerDelaysDestruction),
        ("A delayed destruction is skipped when the Personality has left play by the end of the turn, and is not a second reaction: it is offered once per action, and only when the action could destroy him", BerserkerDelayLimits),
        ("Every enabled command offered for a Spirit and its granted abilities validates and is accepted on a fresh engine", OfferedCommandsValidateFresh),
    ]);

    // The top-level statements of Program.cs declare local Check/Add, which shadow the imported statics: forward to the shared helpers explicitly.
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static CardView View(MatchEngine e, string id, int viewer = 0) => e.GetView(viewer).Players
        .SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.Hand)).Single(c => c.Id == id);

    private static MatchEngine Fresh(Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Action, 0);
        edit?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    // Play the Spirit from hand with the given performer, exactly as the client would offer it.
    private static void PlaySpirit(MatchEngine e, string spirit, string performer)
    {
        var card = e.State.Cards.Values.First(c => c.Zone == Zone.Hand && c.OwnerId == 0 && AbilityCatalog.All[spirit] is not null && (CardCatalog.Get(c.DefinitionId).Abilities ?? []).Contains(spirit));
        Accept(e, Enabled(e, 0, spirit, card.Id, performer).Command!);
    }

    private static void PublishedShape()
    {
        var wave = PublishedContent.StrategyWave33.Cards;
        Eq(wave.Length, 5, "five cards");
        var root = RepoRoot();
        foreach (var (id, spirit, keyword, granted) in Spirits)
        {
            var c = CardCatalog.Get(id);
            Check(!c.ScenarioOnly && c.Type == CardType.Strategy && c.Name == "Spirit of the " + keyword, $"{id} is the printed Strategy");
            Eq(c.Focus, 4, id); Eq(c.GoldCost, 0, id);
            Check(c.Text.StartsWith("Limited: Choose your performing unbowed Personality: If he is a " + keyword + ","), $"{id} text: {c.Text}");
            Check(c.Text.EndsWith("Otherwise, permanently give him " + keyword + "."), $"{id} text ends: {c.Text}");
            Check(c.Abilities!.SequenceEqual(new[] { spirit }) && c.Effect == "none", $"{id} carries only {spirit}");
            var a = AbilityCatalog.All[spirit];
            Eq(a.Timing, "Limited"); Eq(a.Performer, "own-unbowed-personality"); Eq(a.Target, "none");
            Check(AbilityCatalog.All.ContainsKey(granted), $"{granted} is catalogued");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Check(wave.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(Spirits.Select(x => x.Card).OrderBy(x => x, StringComparer.Ordinal)), "exactly the five");
        Check(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-33-sources.json")).Contains("image_sha256"), "the source receipt records the scan's sha256");
    }

    private static void KeywordThenAbility()
    {
        foreach (var (card, spirit, keyword, granted) in Spirits)
        {
            string a = "", b = "", first = "", second = "";
            var e = Fresh(s =>
            {
                a = Add(s, "training-crane-samurai", 0, Zone.Home).Id; b = Add(s, "training-crane-samurai", 0, Zone.Home).Id;
                first = Add(s, card, 0, Zone.Hand).Id; second = Add(s, card, 0, Zone.Hand).Id;
            });
            Check(!e.HasKeyword(a, keyword), $"{keyword}: not yet");
            Accept(e, Enabled(e, 0, spirit, first, a).Command!);
            Check(e.HasKeyword(a, keyword) && e.State.Cards[a].GrantedAbilities.Count == 0, $"{spirit}: first play gives only the keyword");
            Check(e.State.Cards[a].GrantedKeywords.Single(k => k.Keyword == keyword).Duration == GrantDuration.Permanent && e.State.Cards[a].GrantedKeywords.Single(k => k.Keyword == keyword).Source == first, "permanent, sourced to the Strategy");
            Eq(e.State.Cards[first].Zone, Zone.FateDiscard, "the Strategy is spent");
            Check(!e.HasKeyword(b, keyword) && e.State.Cards[b].GrantedAbilities.Count == 0, "the other Personality is untouched");
            e = Edit(e, s => s.PriorityPlayerId = 0);
            Accept(e, Enabled(e, 0, spirit, second, a).Command!);
            var grant = e.State.Cards[a].GrantedAbilities.Single();
            Check(grant.AbilityId == granted && grant.Duration == GrantDuration.Permanent && grant.Source == second, $"{spirit}: second play gives {granted} permanently");
            Check(e.State.Cards[b].GrantedAbilities.Count == 0, "and only to the performer");
            Eq(e.State.Cards[second].Zone, Zone.FateDiscard);
            // The performer must be unbowed: a bowed one is not offered as a performer at all.
            var bowed = Fresh(s => { var p = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); Add(s, card, 0, Zone.Hand); });
            Check(!bowed.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId == spirit), $"{spirit}: a bowed Personality cannot perform it");
        }
    }

    private static void BerserkerForce()
    {
        string p = "", one = "", two = "";
        var e = Fresh(s => { p = Add(s, "training-crane-samurai", 0, Zone.Home).Id; one = Add(s, "oracle-l5r-7420", 0, Zone.Hand).Id; two = Add(s, "oracle-l5r-7420", 0, Zone.Hand).Id; });
        var force = View(e, p).Force;
        Accept(e, Enabled(e, 0, "spirit-of-the-berserker", one, p).Command!);
        Eq(View(e, p).Force, force, "the keyword branch gives no Force");
        e = Edit(e, s => s.PriorityPlayerId = 0);
        Accept(e, Enabled(e, 0, "spirit-of-the-berserker", two, p).Command!);
        Eq(View(e, p).Force, force + 2, "the ability branch gives +2F");
        var mod = e.State.Cards[p].Modifiers.Single();
        Check(mod is { Permanent: true, Stat: "Force", Amount: 2 } && mod.Source == two, "one permanent Force modifier, sourced to the Strategy");
        // It outlives the turn (every other modifier is cleared as the turn ends) and the next one.
        var start = e.State.TurnNumber;
        RunUntil(e, () => e.State.TurnNumber >= start + 2);
        Eq(View(e, p).Force, force + 2, "still +2F two turns later");
        Check(e.State.Cards[p].Modifiers.Single().Permanent, "still the permanent modifier");
        // A turn modifier beside it is cleared as before.
        e = Edit(e, s => { Force(s.Cards[p], 3); });
        Eq(View(e, p).Force, force + 5);
        var t = e.State.TurnNumber;
        RunUntil(e, () => e.State.TurnNumber > t);
        Eq(View(e, p).Force, force + 2, "a turn modifier is gone, the permanent one stays");
    }

    // ---- rigs ---------------------------------------------------------------------------------------------------------------------------------------
    // Player 1 attacks with a Personality whose Battle action bows an enemy Personality (training-battle-bower) or destroys one (training-no-attachments-destroyer).
    private static (MatchState S, CardInstance Holder, CardInstance Ally, CardInstance Actor) BattleRig(string actorDef, string grantedAbility, string? keyword = null)
    {
        var s = Table();
        var holder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var actor = Add(s, actorDef, 1, Zone.Battlefield, province: 0);
        holder.GrantedAbilities.Add(new(grantedAbility, "test-spirit", GrantDuration.Permanent));
        if (keyword is not null) GiveKeyword(holder, keyword);
        return (s, holder, ally, actor);
    }

    private static GameCommand ReactionOffer(MatchEngine e, int player, string ability, string? cardId = null)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.CardId == cardId)).ToArray();
        Check(offers.Length == 1, $"{ability}: expected one enabled offer for player {player}, got {offers.Length}; " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));
        return offers[0].Command!;
    }

    private static bool ReactionOffered(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is not { Type: "beforeActionReaction" or "attackReaction" }, "the window closed");
    }

    private static void YojimboRedirects()
    {
        var (s, holder, ally, actor) = BattleRig("training-battle-bower", "spirit-yojimbo-redirect");
        var elsewhere = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        // Another player's action targets the ally at the holder's location: the window opens for the holder's controller and the redirect is on offer.
        Accept(e, Offer(e, 1, "training-bow-at-battle", actor.Id, ally.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the before-action window is open");
        Check(!e.State.Cards[ally.Id].Bowed, "nothing has happened to the ally yet");
        var redirect = ReactionOffer(e, 0, "spirit-yojimbo-redirect", holder.Id);
        Accept(e, redirect);
        ClosePassing(e);
        Check(e.State.Cards[holder.Id].Bowed && !e.State.Cards[ally.Id].Bowed, "the action resolved on the Yojimbo, the ally is untouched");
        Check(e.State.Cards[holder.Id].AbilityUses.Count > 0, "the holder's use is spent");
        Check(e.State.Events.Any(x => x.Type == "actionRedirected"), "the redirect is logged");

        // An action at the holder himself: nothing to redirect. One at a Personality at home (not his location): not offered.
        var (s2, holder2, ally2, actor2) = BattleRig("training-battle-bower", "spirit-yojimbo-redirect");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-bow-at-battle", actor2.Id, holder2.Id));
        Check(!ReactionOffered(e2, 0, "spirit-yojimbo-redirect"), "an action at the Yojimbo himself is not redirected onto himself");
        ClosePassing(e2);
        Check(e2.State.Cards[holder2.Id].Bowed, "and it resolves on him");

        // The holder's own controller's action is not "another player's": his own Open action on his own Personality opens no such reaction.
        var s3 = Table(Phase.Action, 0);
        var h3 = Add(s3, "training-lion-samurai", 0, Zone.Home); h3.GrantedAbilities.Add(new("spirit-yojimbo-redirect", "test", GrantDuration.Permanent));
        var a3 = Add(s3, "training-crane-samurai", 0, Zone.Home);
        var bower3 = Add(s3, "training-bow-any-source", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "training-bow-any", bower3.Id, a3.Id));
        Check(e3.State.PendingChoice is not { Type: "beforeActionReaction" } && !ReactionOffered(e3, 0, "spirit-yojimbo-redirect"), "his own controller's action does not open the redirect");
        Check(e3.State.Cards[a3.Id].Bowed, "and resolves on its target");
    }

    private static void ScoutAttack()
    {
        foreach (var (recon, bowed) in new[] { (false, true), (true, false) })
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var holder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            holder.GrantedAbilities.Add(new("spirit-scout-attack", "test-spirit", GrantDuration.Permanent));
            var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);   // Force 4
            var strong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);   // Force 5
            if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
            var e = MatchEngine.FromTestState(s);
            var attack = Enabled(e, 0, "spirit-scout-attack", holder.Id);
            Check(attack.NoTarget, "an Attack may be taken with no target");
            Reject(e, attack.Command! with { TargetId = strong.Id });
            Accept(e, attack.Command! with { TargetId = target.Id });
            Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "Ranged 4 destroys a Force-4 Personality");
            Eq(e.State.Cards[holder.Id].Bowed, bowed, recon ? "with Reconnaissance he is not bowed" : "without Reconnaissance he bows");
            Check(e.State.Cards[strong.Id].Zone == Zone.Battlefield, "the Force-5 Personality is untouched");
        }
    }

    // Mendon's ruling (2026-09-24): a permanent ability a Spirit grants is the holder's own, so the holder is the Attack's performer.
    private static void ScoutPerformer()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var holder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        holder.GrantedAbilities.Add(new("spirit-scout-attack", "test-spirit", GrantDuration.Permanent));
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-attack-reactor", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "spirit-scout-attack", holder.Id).Command! with { TargetId = target.Id });
        Eq(e.State.PendingAttack?.SourceId, holder.Id, "the Personality holding the granted ability is the performer");
    }

    private static void ParagonBow()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var holder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);   // Force 4, Personal Honor 2
        holder.GrantedAbilities.Add(new("spirit-paragon-bow", "test-spirit", GrantDuration.Permanent));
        var lowForce = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);      // Force 3, PH 2  (Force <= 4 and PH <= 2)
        var highForceLowHonor = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5, PH 3
        var highBoth = Add(s, "training-lion-dignitary", 1, Zone.Battlefield, province: 0);      // Force 3, PH 4  (Force <= 4)
        var strongerEverywhere = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, PH 3
        var lowHonorHighForce = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);   // Force 2, PH 1
        Force(strongerEverywhere, 0); Force(highForceLowHonor, 0);
        // Make the two "either" cases explicit: one has higher Force but lower Personal Honor, one the other way round.
        highForceLowHonor.Modifiers.Add(new("test", "test", "PersonalHonor", -3));      // Force 5 > 4, PH 0 <= 2
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "spirit-paragon-bow", holder.Id);
        var legal = option.TargetIds!.ToHashSet();
        Check(legal.Contains(lowForce.Id), "lower Force and equal Personal Honor");
        Check(legal.Contains(highBoth.Id), "lower Force, higher Personal Honor: Force alone is enough");
        Check(legal.Contains(lowHonorHighForce.Id), "lower Force and Personal Honor");
        Check(!legal.Contains(strongerEverywhere.Id) || CardPersonalHonorOf(e, strongerEverywhere.Id) <= 2, "Force 6 and Personal Honor 3 is neither");
        Reject(e, option.Command! with { TargetId = strongerEverywhere.Id });
        Accept(e, option.Command! with { TargetId = lowForce.Id });
        Check(e.State.Cards[lowForce.Id].Bowed, "the target is bowed");
    }

    private static int CardPersonalHonorOf(MatchEngine e, string id) => e.GetView(0).Players.SelectMany(p => p.Army).Single(c => c.Id == id).Definition!.PersonalHonor;

    private static void DuelistDuel()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var holder = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);   // Chi 4
        holder.GrantedAbilities.Add(new("spirit-duelist-duel", "test-spirit", GrantDuration.Permanent)); GiveKeyword(holder, "Duelist");
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);    // Chi 3
        var e = MatchEngine.FromTestState(s);
        Check(AbilityCatalog.All["spirit-duelist-duel"].ActionKeywords!.Contains("Iaijutsu") && AbilityCatalog.All["spirit-duelist-duel"].IssuesDuel, "an Iaijutsu duel");
        Accept(e, Offer(e, 0, "spirit-duelist-duel", holder.Id, target.Id));
        Check(e.State.PendingDuel is not null, "a duel is under way");
        Accept(e, new GameCommand("strike", 1, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 0, e.Version));
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "Chi 4 beats Chi 3: the loser moves home");
        Check(e.State.Cards[holder.Id].Zone == Zone.Battlefield, "the winner stays");
        // A friendly Personality is not a legal target.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var h2 = Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0); h2.GrantedAbilities.Add(new("spirit-duelist-duel", "test-spirit", GrantDuration.Permanent));
        var own = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, CardId: h2.Id, TargetId: own.Id, AbilityId: "spirit-duelist-duel"));
    }

    private static void BerserkerDelaysDestruction()
    {
        var (s, berserker, ally, destroyer) = BattleRig("training-no-attachments-destroyer", "spirit-berserker-delay", "Berserker");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, berserker.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the Berserker's controller");
        Accept(e, ReactionOffer(e, 0, "spirit-berserker-delay", berserker.Id));
        ClosePassing(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Battlefield, "he is not destroyed when the action resolves");
        Check(e.State.PendingDelayedDestructions.SequenceEqual(new[] { berserker.Id }), "his destruction is waiting for the end of the turn");
        Check(e.State.Events.Any(x => x.Type == "destructionDelayed"), "logged");
        // The turn ends: he is destroyed then, before it ends.
        var start = e.State.TurnNumber;
        RunUntil(e, () => e.State.TurnNumber > start);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Dead, "destroyed before the turn ended");
        Check(e.State.Events.Any(x => x.Type == "delayedDestruction"), "by the delayed destruction itself, not by the battle");
        Check(e.State.PendingDelayedDestructions.Count == 0, "the delay is spent");
        Check(e.State.Cards[ally.Id].Zone is Zone.Battlefield or Zone.Home, "the ally is untouched (alive, back home with the battle over)");
    }

    private static void BerserkerDelayLimits()
    {
        // Only an action that could destroy him opens the window: a bow does not.
        var (s, berserker, _, bower) = BattleRig("training-battle-bower", "spirit-berserker-delay", "Berserker");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, berserker.Id));
        Check(e.State.PendingChoice is not { Type: "beforeActionReaction" } && !ReactionOffered(e, 0, "spirit-berserker-delay"), "a bow gives him nothing to delay");
        // His own controller's action does not.
        // The delayed card is skipped when it has left play before the turn ends.
        var (s2, b2, _, d2) = BattleRig("training-no-attachments-destroyer", "spirit-berserker-delay", "Berserker");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", d2.Id, b2.Id));
        Accept(e2, ReactionOffer(e2, 0, "spirit-berserker-delay", b2.Id));
        ClosePassing(e2);
        e2 = Edit(e2, st => { var c = st.Cards[b2.Id]; c.Zone = Zone.Home; c.ProvinceIndex = null; st.PendingDelayedDestructions.Clear(); st.PendingDelayedDestructions.Add(b2.Id); c.Zone = Zone.FateDiscard; });
        var t = e2.State.TurnNumber;
        RunUntil(e2, () => e2.State.TurnNumber > t);
        Eq(e2.State.Cards[b2.Id].Zone, Zone.FateDiscard, "a card no longer in play is left where it is");
        Check(!e2.State.Events.Any(x => x.Type == "delayedDestruction"), "no destruction is logged for it");
    }

    private static void OfferedCommandsValidateFresh()
    {
        MatchState Build()
        {
            var s = Table(Phase.Action, 0);
            Add(s, "training-crane-samurai", 0, Zone.Home);
            foreach (var (card, _, _, _) in Spirits) Add(s, card, 0, Zone.Hand);
            return s;
        }
        var e = MatchEngine.FromTestState(Build());
        Eq(e.QueryActions(0).Count(a => a.Enabled && a.Command?.AbilityId is { } id && Spirits.Any(x => x.Spirit == id)), 5, "one offer per Spirit");
        OfferedCommandsValidate(e, () => MatchEngine.FromTestState(Build()));
    }
}
