using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Personality pass (issue #477), batch 2: the 12 of the batch's 13 candidate cards wired in personality-wave-69, driven through QueryActions and the commands it offers
// on explicit positions of the economy table (player 0 the Crane, player 1 the Lion). Akodo Makotai stays unwired and unpublished (tools/content/ee_batch2_blockers.json).
internal static class EmperorPersonalityBatch2Tests
{
    private const string Horu = "oracle-l5r-3108", Kisada = "oracle-l5r-3127", Komatsu = "oracle-l5r-3130", Takeuchi = "oracle-l5r-3200", Watari = "oracle-l5r-3227", Esumi = "oracle-l5r-4109",
        Hideaki = "oracle-l5r-4115", Shinoda = "oracle-l5r-4587", Shishido = "oracle-l5r-8651", Shinju = "oracle-l5r-3638", Hana = "oracle-l5r-4916", Yuuto = "oracle-l5r-5010",
        Higaonna = "oracle-l5r-775";
    private static readonly string[] Wired = [Horu, Kisada, Komatsu, Takeuchi, Watari, Esumi, Hideaki, Shinoda, Shishido, Shinju, Hana, Yuuto];
    private const string Makotai = "oracle-l5r-241";

    public static void Run(TestRunner runner) => runner.Run("emperor-personality-batch-2", [
        ("Published shape: personality-wave-69 publishes exactly the 12 wired cards with their printed stats, keywords, ability ids and Effects, ships their art and lists them in its manifest; Akodo Makotai is not in the catalogue", PublishedShape),
        ("Hida Horu: he contributes no Force to a battle resolution while his army has two or more units (his Followers still do); alone in his army he contributes normally", HoruCrowdedArmy),
        ("Hida Kisada: another player's Battle action targeting him opens a redirect to another Personality of his controller's at his location that the action accepts; his own controller's action opens nothing; an Attack is redirected too", KisadaRedirect),
        ("Hida Kisada: Battle destroys an enemy card with lower Force (equal or higher is not offered)", KisadaDestroy),
        ("Hida Komatsu: bows an enemy Follower or Personality with lower Force and no attachments; destroys an enemy attachment (never his own)", KomatsuActions),
        ("Hida Takeuchi: after an Equip that attached a Follower from its controller's hand to him he may draw a card; not for another host, another attachment type, or after passing", TakeuchiDraw),
        ("Hida Watari: his only Follower has Cavalry (two Followers, none: no); his Fear Battle bows every Follower with 3 or lower Force in an enemy unit", WatariActions),
        ("Kaiu Esumi: Ranged 4 Attack whose targeting gives the current battlefield's province +2 or -2 strength (one use for the two choices; none when no card was targeted; never below 0)", EsumiAttack),
        ("Kaiu Hideaki: after engaging at his battlefield the province has +3 strength until the battle's resolution begins; not from another battlefield or home", HideakiEngage),
        ("Kuni Shinoda: from another battlefield, with enemy units at the current one, he and an optional Crab Clan Personality at his battlefield move to the current battlefield; each condition is required", ShinodaMove),
        ("Toritaka Shishido: Ranged Attacks from cards in his army have +1 strength (one per Shishido); not other armies, not Melee", ShishidoBonus),
        ("Ikoma Shinju: destroys a Terrain and an enemy card without attachments together; both targets are required", ShinjuDestroy),
        ("Matsu Hana: bows each card in an enemy unit with equal or lower Force than Hana and gains 1 Honor for each card this bowed (a bowed card or a negated bow is not counted; a dishonorable Hana is rehonored instead)", HanaBow),
        ("Matsu Yuuto: Ranged Attacks may not target cards in his unit (Melee may, other units are unaffected)", YuutoImmunity),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static MatchState Battle(int attacker = 0) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static int Strength(MatchEngine e, int player, int province) => e.GetView(player).Players[player].Provinces[province].Strength;
    private static string? Window(MatchEngine e) => e.State.PendingChoice?.Type;
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static void Pass(MatchEngine e) => Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
    private static string LastResolution(MatchEngine e) => e.State.Events.Last(ev => ev.Type == "battleResolved").Message;

    private static GameCommand ReactionCommand(MatchEngine e, int player, string ability, string? targetId = null)
    {
        var enabled = Listed(e, player, ability).Where(a => a.Enabled && (targetId is null || a.Command!.TargetId == targetId)).ToArray();
        Check(enabled.Length == 1, $"{ability} for player {player}{(targetId is null ? "" : " -> " + targetId)}: expected exactly one enabled offer, got {enabled.Length} of "
            + string.Join("; ", Listed(e, player, ability).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return enabled[0].Command!;
    }

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; Window(e) is "beforeActionReaction" or "attackReaction" or "afterActionResolvedReaction" && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string name, string clan, int force, int chi, int cost, int ph, string keywords, string[] abilities, string effect = "none")
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && !c.CreatedOnly && c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph
                    && Keywords(id) == keywords && c.Effect == effect && c.EntryHonorLoss is null,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} keywords={Keywords(id)} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Stats(Horu, "Hida Horu", "Crab", 8, 3, 9, 1, "Berserker,Crab Clan,Soul of Hida Hebi", [], "horu-crowded-army-no-force");
        Stats(Kisada, "Hida Kisada (Emperor)", "Crab", 7, 5, 12, 3, "Clan Champion,Crab Clan,Kensai,Little Bear,Loyal,Samurai,Siege,Tactician,Unique", ["kisada-redirect", "kisada-destroy"]);
        Stats(Komatsu, "Hida Komatsu", "Crab", 6, 2, 8, 1, "Berserker,Crab Clan,Hero,Soul of Hida Rikyu Experienced,Unique", ["komatsu-bow", "komatsu-destroy-attachment"]);
        Stats(Takeuchi, "Hida Takeuchi", "Crab", 3, 2, 6, 1, "Crab Clan,Samurai,Soul of Hida Kosedo", ["takeuchi-draw"]);
        Stats(Watari, "Hida Watari", "Crab", 5, 2, 8, 1, "Cavalry,Crab Clan,Samurai,Soul of Hida Tobashi,Unique,Veteran,Wyrm Rider", ["watari-fear-bow"], "watari-sole-follower-cavalry");
        Stats(Esumi, "Kaiu Esumi", "Crab", 3, 3, 7, 2, "Crab Clan,Samurai,Siege,Tactician", ["esumi-attack-raise", "esumi-attack-lower"]);
        Stats(Hideaki, "Kaiu Hideaki", "Crab", 3, 2, 4, 2, "Crab Clan,Samurai,Siege,Soul of Kaiu Seison", ["hideaki-province"]);
        Stats(Shinoda, "Kuni Shinoda", "Crab", 2, 2, 4, 2, "Crab Clan,Earth,Shugenja,Soul of Kuni Yasashii", ["shinoda-move"]);
        Stats(Shishido, "Toritaka Shishido", "Crab", 3, 3, 6, 3, "Crab Clan,Samurai,Scout,Soul of Toritaka Okabe", [], "shishido-ranged-bonus");
        Stats(Shinju, "Ikoma Shinju", "Lion", 4, 3, 7, 2, "Lion Clan,Samurai,Scout", ["shinju-destroy"]);
        Stats(Hana, "Matsu Hana", "Lion", 5, 3, 9, 4, "Lion Clan,Paragon,Samurai,Soul of Matsu Fumiyo Experienced,Unique", ["hana-bow"]);
        Stats(Yuuto, "Matsu Yuuto", "Lion", 4, 4, 8, 2, "Cavalry,Lion Clan,Samurai,Soul of Matsu Agetoki", [], "yuuto-unit-ranged-immune");

        // The wave publishes exactly the wired cards (the personality-wave-63 blank-card lesson): no unwired card, no blank card (an ability or an Effect each).
        Check(PublishedContent.PersonalityWave69.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)),
            "personality-wave-69 is exactly the wired cards: " + string.Join(",", PublishedContent.PersonalityWave69.Cards.Select(c => c.Id)));
        Check(PublishedContent.PersonalityWave69.Cards.All(c => (c.Abilities ?? []).Length > 0 || c.Effect != "none"), "no card of the wave is blank");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-69.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)), "the manifest pins exactly the wired cards");
        Check(!CardCatalog.Cards.ContainsKey(Makotai), "Akodo Makotai is unwired and unpublished");
        foreach (var abilityId in Wired.SelectMany(id => CardCatalog.Get(id).Abilities ?? [])) Check(AbilityCatalog.All.ContainsKey(abilityId), $"{abilityId} is catalogued");
    }

    private static void HoruCrowdedArmy()
    {
        // Two units: Horu contributes nothing, so the army totals only its other unit (3F) against the defender's 6F and loses.
        var s = Battle(); var horu = Bf(s, Horu, 0); var ally = Bf(s, "training-crane-samurai", 0); var foe = Bf(s, "training-lion-veteran", 1);
        var e = MatchEngine.FromTestState(s);
        Pass(e); Pass(e);
        Check(LastResolution(e).Contains("attacking Force 3, defending Force 6"), "Horu's 8F is withheld in a two-unit army: " + LastResolution(e));
        Check(e.State.Cards[ally.Id].Zone == Zone.Dead && e.State.Cards[horu.Id].Zone == Zone.Dead && e.State.Cards[foe.Id].Zone != Zone.Dead, $"the crowded army lost: ally {e.State.Cards[ally.Id].Zone}, horu {e.State.Cards[horu.Id].Zone}, foe {e.State.Cards[foe.Id].Zone}; {LastResolution(e)}");
        // His Followers still contribute: Horu (with a 2F Follower) and a 3F ally total 5.
        var f = Battle(); var h2 = Bf(f, Horu, 0); Add(f, "training-follower", 0, Zone.Attached, province: 0, parent: h2.Id); Bf(f, "training-crane-samurai", 0); Bf(f, "training-lion-veteran", 1);
        var fe = MatchEngine.FromTestState(f);
        Pass(fe); Pass(fe);
        Check(LastResolution(fe).Contains("attacking Force 5,"), "his Follower's Force still counts: " + LastResolution(fe));
        // Alone in his army he contributes his 8F and wins.
        var a = Battle(); var alone = Bf(a, Horu, 0); var enemy = Bf(a, "training-lion-veteran", 1);
        var ae = MatchEngine.FromTestState(a);
        Pass(ae); Pass(ae);
        Check(LastResolution(ae).Contains("attacking Force 8, defending Force 6"), "alone he contributes: " + LastResolution(ae));
        Check(ae.State.Cards[enemy.Id].Zone == Zone.Dead && ae.State.Cards[alone.Id].Zone != Zone.Dead, "the lone Horu won");
        // The defending side counts the same way (a defender's army of two).
        var d = Battle(); Bf(d, "training-lion-veteran", 0); var dh = Bf(d, Horu, 1); Bf(d, "training-lion-samurai", 1);
        var de = MatchEngine.FromTestState(d);
        Pass(de); Pass(de);
        Check(LastResolution(de).Contains("attacking Force 6, defending Force 4"), "a defending army of two withholds Horu too: " + LastResolution(de));
        _ = dh;
    }

    private static void KisadaRedirect()
    {
        // Player 1 attacks; Hana's Fear Battle names Kisada. His controller (player 0) may redirect it to another Personality at his location.
        var s = Table(Phase.Battle, 1);
        var kisada = Bf(s, Kisada, 0); var ally = Bf(s, "training-crane-samurai", 0); var home = Add(s, "training-crane-samurai", 0, Zone.Home);
        var hana = Bf(s, Hana, 1); Force(hana, 6); // Hana 11F: bows anything up to 11
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "hana-bow", hana.Id, kisada.Id));
        Eq(Window(e), "beforeActionReaction", "the window opens for Kisada's controller");
        var offers = Listed(e, 0, "kisada-redirect").Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
        Check(offers.SequenceEqual(new[] { ally.Id }), "only the other Personality at his location is offered (not the one at home, not himself): " + string.Join(",", offers));
        Accept(e, ReactionCommand(e, 0, "kisada-redirect", ally.Id));
        ClosePassing(e);
        Check(e.State.Cards[ally.Id].Bowed && !e.State.Cards[kisada.Id].Bowed, "the action landed on the chosen Personality instead");
        // Only another player's BATTLE action: an Open action of the opponent's naming him opens no window (the Action Phase), and neither does his own controller's.
        var o = Table(Phase.Action, 1); o.PriorityPlayerId = 1;
        var k2 = Add(o, Kisada, 0, Zone.Home); Add(o, "training-crane-samurai", 0, Zone.Home); var src = Add(o, "training-bow-any-source", 1, Zone.Home);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 1, "training-bow-any", src.Id, k2.Id));
        Check(Window(oe) is null && oe.State.Cards[k2.Id].Bowed, "an Open action is not a Battle action: no window, and it resolved");
        var mine = Table(Phase.Action, 0); mine.PriorityPlayerId = 0;
        var k3 = Add(mine, Kisada, 0, Zone.Home); Add(mine, "training-crane-samurai", 0, Zone.Home); var own = Add(mine, "training-bow-any-source", 0, Zone.Home);
        var me = MatchEngine.FromTestState(mine);
        Accept(me, Offer(me, 0, "training-bow-any", own.Id, k3.Id));
        Check(Window(me) is null, "his own controller's action opens nothing");
        // A non-Battle-action-targeting (the action's target is a different Personality) opens nothing either.
        var t = Table(Phase.Battle, 1); Bf(t, Kisada, 0); var other = Bf(t, "training-crane-samurai", 0); var h3 = Bf(t, Hana, 1); Force(h3, 6);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 1, "hana-bow", h3.Id, other.Id));
        Check(Window(te) is null, "no window when the action does not target Kisada");
        // An Attack that targets him is redirected too (the attack window): Kisada at 2F is a legal Ranged 4 target, and so is the 3F ally it moves to.
        var a = Table(Phase.Battle, 1);
        var ak = Bf(a, Kisada, 0); Force(ak, -5); var aAlly = Bf(a, "training-crane-samurai", 0);
        var esumi = Bf(a, Esumi, 1);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Offer(ae, 1, "esumi-attack-raise", esumi.Id, ak.Id));
        Eq(Window(ae), "attackReaction", "the Attack window opens");
        Accept(ae, ReactionCommand(ae, 0, "kisada-redirect", aAlly.Id));
        ClosePassing(ae);
        Check(ae.State.Cards[aAlly.Id].Zone == Zone.Dead && ae.State.Cards[ak.Id].Zone == Zone.Battlefield, "the Attack hit the Personality it was redirected to");
        // A redirect must be legal: the Attack cannot target a Personality above its strength, so a 6F ally is not offered.
        var b = Table(Phase.Battle, 1);
        var bk = Bf(b, Kisada, 0); Force(bk, -5); var strong = Bf(b, "training-lion-veteran", 0); var be0 = Bf(b, Esumi, 1);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 1, "esumi-attack-raise", be0.Id, bk.Id));
        Check(Window(be) is null || !Listed(be, 0, "kisada-redirect").Any(x => x.Enabled), "a target the Attack could not legally choose is not offered: " + string.Join(",", Listed(be, 0, "kisada-redirect").Select(x => x.Reason)));
        _ = strong;
    }

    private static void KisadaDestroy()
    {
        var s = Battle(); var kisada = Bf(s, Kisada, 0);
        var lower = Bf(s, "training-lion-veteran", 1); // 6F < 7F
        var equal = Bf(s, "training-lion-veteran", 1); Force(equal, 1); // 7F
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "kisada-destroy", kisada.Id).TargetIds!;
        Check(targets.Contains(lower.Id) && !targets.Contains(equal.Id), "only a card with lower Force is offered: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "kisada-destroy", kisada.Id, lower.Id));
        Eq(e.State.Cards[lower.Id].Zone, Zone.Dead, "destroyed");
    }

    private static void KomatsuActions()
    {
        var s = Battle(); var komatsu = Bf(s, Komatsu, 0); // 6F
        var plain = Bf(s, "training-lion-samurai", 1);
        var equalForce = Bf(s, "training-lion-veteran", 1); // 6F: not lower
        var armed = Bf(s, "training-lion-samurai", 1); Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: armed.Id);
        var host = Bf(s, "training-lion-samurai", 1); var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var friend = Bf(s, "training-crane-samurai", 0); var ownItem = Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: friend.Id);
        var e = MatchEngine.FromTestState(s);
        var bowTargets = Enabled(e, 0, "komatsu-bow", komatsu.Id).TargetIds!;
        Check(bowTargets.Contains(plain.Id) && bowTargets.Contains(follower.Id), "an enemy Personality without attachments and an enemy Follower are offered: " + string.Join(",", bowTargets));
        Check(!bowTargets.Contains(equalForce.Id) && !bowTargets.Contains(armed.Id) && !bowTargets.Contains(host.Id), $"equal Force, attachments and a Personality with a Follower are not: {string.Join(",", bowTargets)} (equal {equalForce.Id}, armed {armed.Id}, host {host.Id})");
        Accept(e, Offer(e, 0, "komatsu-bow", komatsu.Id, plain.Id));
        Check(e.State.Cards[plain.Id].Bowed, "bowed");
        // The second Battle action is its own: still available after the first.
        e = Edit(e, st => st.PriorityPlayerId = 0);
        var destroyTargets = Enabled(e, 0, "komatsu-destroy-attachment", komatsu.Id).TargetIds!;
        Check(destroyTargets.Contains(follower.Id) && !destroyTargets.Contains(ownItem.Id) && !destroyTargets.Contains(plain.Id), "only enemy attachments: " + string.Join(",", destroyTargets));
        Accept(e, Offer(e, 0, "komatsu-destroy-attachment", komatsu.Id, follower.Id));
        Check(e.State.Cards[follower.Id].Zone != Zone.Attached, "the Follower was destroyed");
    }

    private static void TakeuchiDraw()
    {
        // Player 0's turn, Action Phase: Equip a Follower from hand to Takeuchi.
        GameCommand EquipCommand(MatchEngine e, CardInstance card, CardInstance host) =>
            new("equip", 0, e.Version, CardId: card.Id, SourceIds: e.PaymentOptions(0, CardCatalog.Get(card.DefinitionId).GoldCost).FirstOrDefault()?.SourceIds ?? [], TargetId: host.Id);
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var takeuchi = Add(s, Takeuchi, 0, Zone.Home); var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Hand); var second = Add(s, "training-follower", 0, Zone.Hand); var weapon = Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, EquipCommand(e, follower, takeuchi));
        Eq(Window(e), "afterActionResolvedReaction", "the after-action window opens for the holder of the Reaction");
        Check(Enabled(e, 0, "takeuchi-draw", takeuchi.Id) is not null, "Takeuchi's Reaction is offered");
        Accept(e, Use(e, 0, "takeuchi-draw", takeuchi.Id));
        Eq(e.State.Players[0].Hand.Count, handBefore - 1 + 1, "the Follower left the hand and one card was drawn");
        ClosePassing(e);
        Check(Window(e) is null && e.State.PriorityPlayerId == 1, "the window closed and priority passed on");
        // Declining: no draw.
        var d = MatchEngine.FromTestState(Edit(MatchEngine.FromTestState(s), st => st.PriorityPlayerId = 0).State);
        Accept(d, EquipCommand(d, second, takeuchi));
        Accept(d, new GameCommand("declineReaction", d.State.PriorityPlayerId, d.Version)); ClosePassing(d);
        Check(Window(d) is null, "passing closes it");
        // Another host: no window at all. A Weapon on Takeuchi: no window either.
        var o = Table(Phase.Action, 0); o.PriorityPlayerId = 0;
        var t2 = Add(o, Takeuchi, 0, Zone.Home); var h2 = Add(o, "training-crane-samurai", 0, Zone.Home);
        var f2 = Add(o, "training-follower", 0, Zone.Hand); var w2 = Add(o, "training-weapon", 0, Zone.Hand);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, EquipCommand(oe, f2, h2));
        Check(Window(oe) is null, "a Follower attached to another Personality opens nothing");
        var w = Edit(oe, st => st.PriorityPlayerId = 0);
        Accept(w, EquipCommand(w, w2, t2));
        Check(Window(w) is null, "a Weapon attached to Takeuchi opens nothing");
        _ = other; _ = weapon;
    }

    private static void WatariActions()
    {
        var s = Battle(); var watari = Bf(s, Watari, 0);
        var only = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: watari.Id);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(only.Id, "Cavalry"), "his only Follower has Cavalry");
        Check(!e.HasKeyword(watari.Id, "Infantry"), "the grant is to the Follower alone");
        var two = Edit(e, st => Add(st, "training-follower", 0, Zone.Attached, province: 0, parent: watari.Id));
        Check(!two.HasKeyword(only.Id, "Cavalry"), "with two Followers neither has it");
        var moved = Edit(e, st => { st.Cards[only.Id].AttachedTo = null; st.Cards[only.Id].Zone = Zone.Home; });
        Check(!moved.HasKeyword(only.Id, "Cavalry"), "a Follower that is no longer attached has none");
        var other = Table(Phase.Battle, 0); var stranger = Bf(other, "training-crane-samurai", 0); var strangerFollower = Add(other, "training-follower", 0, Zone.Attached, province: 0, parent: stranger.Id);
        Check(!MatchEngine.FromTestState(other).HasKeyword(strangerFollower.Id, "Cavalry"), "a Follower of another Personality is not granted Cavalry");
        // Fear Battle: bow every Follower with 3 or lower Force in an enemy unit (not the Personality, not a 4F Follower).
        var b = Battle(); var w = Bf(b, Watari, 0);
        var host = Bf(b, "training-lion-samurai", 1);
        var small = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); // 2F
        var edge = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); Force(edge, 1); // 3F
        var big = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); Force(big, 2); // 4F
        var bare = Bf(b, "training-lion-samurai", 1);
        var be = MatchEngine.FromTestState(b);
        Check(Enabled(be, 0, "watari-fear-bow", w.Id).TargetIds!.Contains(host.Id) && CardCatalog.Get(Watari).Abilities!.Contains("watari-fear-bow"), "an enemy unit is targeted through its Personality");
        Check(AbilityCatalog.All["watari-fear-bow"].ActionKeywords!.Contains("Fear"), "a Fear action");
        Accept(be, Offer(be, 0, "watari-fear-bow", w.Id, host.Id));
        Check(be.State.Cards[small.Id].Bowed && be.State.Cards[edge.Id].Bowed && !be.State.Cards[big.Id].Bowed && !be.State.Cards[host.Id].Bowed, "the 2F and 3F Followers bowed, the 4F Follower and the Personality not");
        Check(!be.State.Cards[bare.Id].Bowed, "another unit is untouched");
        // A unit with no Followers may still be targeted: nothing happens.
        var n = Battle(); var nw = Bf(n, Watari, 0); var lone = Bf(n, "training-lion-samurai", 1);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 0, "watari-fear-bow", nw.Id, lone.Id));
        Check(!ne.State.Cards[lone.Id].Bowed, "no Followers, nothing bowed");
    }

    private static void EsumiAttack()
    {
        var s = Battle(); var esumi = Bf(s, Esumi, 0); var prey = Bf(s, "training-lion-samurai", 1); // 4F
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("esumi-attack-raise", esumi.Id), 4, "Ranged 4");
        Check(AbilityCatalog.All["esumi-attack-raise"].UsageKey == AbilityCatalog.All["esumi-attack-lower"].UsageKey, "one printed action: the two choices share one use");
        var base1 = Strength(e, 1, 0);
        Accept(e, Offer(e, 0, "esumi-attack-raise", esumi.Id, prey.Id));
        Check(e.State.Cards[prey.Id].Zone == Zone.Dead, "the Attack hit");
        Eq(Strength(e, 1, 0), base1 + 2, "+2 strength on the current battlefield's province");
        Eq(Strength(e, 1, 1), base1, "only that province");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(e, 0, "esumi-attack-lower", esumi.Id) && !CanUse(e, 0, "esumi-attack-raise", esumi.Id), "one use for both choices");
        // -2, and it ends with the turn.
        var l = Battle(); var le = Bf(l, Esumi, 0); var target = Bf(l, "training-lion-samurai", 1);
        var lower = MatchEngine.FromTestState(l);
        var before = Strength(lower, 1, 0);
        Accept(lower, Offer(lower, 0, "esumi-attack-lower", le.Id, target.Id));
        Eq(Strength(lower, 1, 0), before - 2, "-2 strength");
        var next = Edit(lower, st => st.TurnNumber++);
        Eq(Strength(next, 1, 0), before, "it ended with the turn");
        // A Ranged Attack that targets no card gives the province nothing; a strength already low floors at zero.
        var n = Battle(); var ne = Bf(n, Esumi, 0); Bf(n, "training-lion-veteran", 1); // 6F: out of reach, so no target can be chosen
        var noTarget = MatchEngine.FromTestState(n);
        var noBefore = Strength(noTarget, 1, 0);
        Accept(noTarget, Use(noTarget, 0, "esumi-attack-raise", ne.Id));
        Eq(Strength(noTarget, 1, 0), noBefore, "no card targeted: no change");
        var f = Battle(); var fe = Bf(f, Esumi, 0); var t3 = Bf(f, "training-lion-samurai", 1); f.Players[1].Provinces[0].Strength = 1;
        var floor = MatchEngine.FromTestState(f);
        Accept(floor, Offer(floor, 0, "esumi-attack-lower", fe.Id, t3.Id));
        Eq(Strength(floor, 1, 0), 0, "strength floors at zero");
    }

    private static MatchEngine EngageTable(Action<MatchState> arrange)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        return e;
    }

    private static void HideakiEngage()
    {
        CardInstance hideaki = null!;
        var e = EngageTable(s => { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); hideaki = Add(s, Hideaki, 1, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); });
        Eq(Window(e), "engageReaction", "the Engage window opens");
        var baseStrength = Strength(e, 1, 0);
        // The Attacker (player 0) has priority first; the defender's Hideaki reacts once it is his turn to.
        if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, Use(e, 1, "hideaki-province", hideaki.Id));
        Eq(Strength(e, 1, 0), baseStrength + 3, "+3 strength on the battlefield's province");
        Eq(Strength(e, 1, 1), baseStrength, "only that province");
        while (Window(e) == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(Strength(e, 1, 0), baseStrength + 3, "it lasts through the Combat Segment");
        Check(e.State.ProvinceStrengthEffects.Count == 1, "one effect is standing");
        Pass(e); Pass(e); // the battle resolves
        Check(!e.State.ProvinceStrengthEffects.Any(p => p.UntilBattle is not null), "gone once the resolution began");
        // Hideaki at home (not at the battlefield engaged at) has nothing to react to.
        var away = EngageTable(s => { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, Hideaki, 1, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); });
        Check(Window(away) is null, "no window opens: his Reaction has no legal use away from the battlefield");
    }

    private static void ShinodaMove()
    {
        // Player 1 attacks battlefield 0; player 0's defenders at battlefield 1 include Shinoda, a Crab and a non-Crab.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; s.UnresolvedBattlefields = [0, 1];
        var shinoda = Add(s, Shinoda, 0, Zone.Battlefield, province: 1);
        var crab = Add(s, Takeuchi, 0, Zone.Battlefield, province: 1);
        var lion = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 1);
        var crabElsewhere = Add(s, Takeuchi, 0, Zone.Home);
        var crabAtCurrent = Add(s, Takeuchi, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "shinoda-move", shinoda.Id);
        var slot = offer.Slots!.Single(x => x.Slot == "target");
        Check(slot.Min == 0 && slot.Max == 1 && slot.CardIds.SequenceEqual(new[] { crab.Id }), "the optional target is a Crab Clan Personality at Shinoda's battlefield, not another Clan, home, or the current battlefield: " + string.Join(",", slot.CardIds));
        Accept(e, Use(e, 0, "shinoda-move", shinoda.Id, selections: Pick("target", crab.Id)));
        Check(e.State.Cards[shinoda.Id].ProvinceIndex == 0 && e.State.Cards[crab.Id].ProvinceIndex == 0 && e.State.Cards[shinoda.Id].Zone == Zone.Battlefield && e.State.Cards[crab.Id].Zone == Zone.Battlefield, "both moved to the current battlefield");
        Check(e.State.Cards[lion.Id].ProvinceIndex == 1, "the non-Crab stayed");
        // No target: only Shinoda moves.
        var n = Table(Phase.Battle, 1); n.PriorityPlayerId = 0; n.UnresolvedBattlefields = [0, 1];
        var s2 = Add(n, Shinoda, 0, Zone.Battlefield, province: 1); var c2 = Add(n, Takeuchi, 0, Zone.Battlefield, province: 1); Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Use(ne, 0, "shinoda-move", s2.Id));
        Check(ne.State.Cards[s2.Id].ProvinceIndex == 0 && ne.State.Cards[c2.Id].ProvinceIndex == 1, "without a target only Shinoda moved");
        // Each condition is required: no enemy units at the current battlefield.
        var q = Table(Phase.Battle, 1); q.PriorityPlayerId = 0; q.UnresolvedBattlefields = [0, 1];
        var sq = Add(q, Shinoda, 0, Zone.Battlefield, province: 1);
        var qe = MatchEngine.FromTestState(q);
        Check(!CanUse(qe, 0, "shinoda-move", sq.Id), "no enemy units at the current battlefield: refused");
        // Shinoda already at the current battlefield, or at home.
        var here = Table(Phase.Battle, 1); here.PriorityPlayerId = 0; Add(here, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var sh = Add(here, Shinoda, 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(here), 0, "shinoda-move", sh.Id), "Shinoda at the current battlefield: refused");
        var home = Table(Phase.Battle, 1); home.PriorityPlayerId = 0; Add(home, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var sHome = Add(home, Shinoda, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(home), 0, "shinoda-move", sHome.Id), "Shinoda at home (not at a battlefield): refused");
        _ = crabElsewhere; _ = crabAtCurrent; _ = enemy;
    }

    private static void ShishidoBonus()
    {
        var s = Battle(); var shishido = Bf(s, Shishido, 0); var higaonna = Bf(s, Higaonna, 0);
        var five = Bf(s, "training-lion-samurai", 1); Force(five, 1); // 5F: beyond a plain Ranged 4
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "higaonna-attack", higaonna.Id).TargetIds!.Contains(five.Id), "Ranged 4 +1 reaches a 5F card");
        Accept(e, Offer(e, 0, "higaonna-attack", higaonna.Id, five.Id));
        Check(e.State.Cards[five.Id].Zone == Zone.Dead, "and destroys it");
        // Without Shishido a 5F card is out of reach.
        var p = Battle(); var h2 = Bf(p, Higaonna, 0); var out5 = Bf(p, "training-lion-samurai", 1); Force(out5, 1);
        Check(!Enabled(MatchEngine.FromTestState(p), 0, "higaonna-attack", h2.Id).TargetIds!.Contains(out5.Id), "no bonus without Shishido");
        // Shishido at another battlefield is not in the army.
        var a = Battle(); a.UnresolvedBattlefields = [0, 1]; Add(a, Shishido, 0, Zone.Battlefield, province: 1); var h3 = Bf(a, Higaonna, 0); var t3 = Bf(a, "training-lion-samurai", 1); Force(t3, 1);
        Check(!Enabled(MatchEngine.FromTestState(a), 0, "higaonna-attack", h3.Id).TargetIds!.Contains(t3.Id), "another army's Shishido gives nothing");
        // The opponent's Shishido does not help the other side.
        var o = Battle(); Bf(o, Shishido, 1); var h4 = Bf(o, Higaonna, 0); var t4 = Bf(o, "training-lion-samurai", 1); Force(t4, 1);
        Check(!Enabled(MatchEngine.FromTestState(o), 0, "higaonna-attack", h4.Id).TargetIds!.Contains(t4.Id), "the enemy's Shishido gives nothing");
        // One +1 for each Shishido in the army.
        var d = Battle(); Bf(d, Shishido, 0); Bf(d, Shishido, 0); var h5 = Bf(d, Higaonna, 0); var t5 = Bf(d, "training-lion-samurai", 1); Force(t5, 2); // 6F
        Check(Enabled(MatchEngine.FromTestState(d), 0, "higaonna-attack", h5.Id).TargetIds!.Contains(t5.Id), "two Shishido: +2");
        // A Melee Attack gets nothing (training-weapon-attack is Melee 2).
        var m = Battle(); Bf(m, Shishido, 0); var melee = Bf(m, "training-recon-attacker", 0); var t6 = Bf(m, "training-lion-samurai", 1); // 4F: beyond Melee 2 either way
        Check(!Enabled(MatchEngine.FromTestState(m), 0, "training-weapon-attack", melee.Id).TargetIds!.Contains(t6.Id), "Melee is not boosted");
    }

    private static void ShinjuDestroy()
    {
        var s = Battle(); var shinju = Bf(s, Shinju, 0);
        var enemy = Bf(s, "training-lion-samurai", 1); var armed = Bf(s, "training-lion-samurai", 1); Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: armed.Id);
        var terrain = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "shinju-destroy", shinju.Id);
        Check(offer.Slots!.Single(x => x.Slot == "enemy").CardIds.Contains(enemy.Id) && !offer.Slots!.Single(x => x.Slot == "enemy").CardIds.Contains(armed.Id), "an enemy card without attachments");
        Check(offer.Slots!.Single(x => x.Slot == "terrain").CardIds.SequenceEqual(new[] { terrain.Id }), "the Terrain in play");
        Accept(e, Use(e, 0, "shinju-destroy", shinju.Id, selections: [Pick("enemy", enemy.Id), Pick("terrain", terrain.Id)]));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead && e.State.Cards[terrain.Id].Zone != Zone.Battlefield, "both targets were destroyed");
        Check(e.State.Players[0].TerrainDestroyedThisBattle, "a Terrain destroyed counts for Ring of Water's condition");
        // Both targets are required: with no Terrain in play the action is unavailable.
        var n = Battle(); var s2 = Bf(n, Shinju, 0); Bf(n, "training-lion-samurai", 1);
        Check(!CanUse(MatchEngine.FromTestState(n), 0, "shinju-destroy", s2.Id), "no Terrain: refused");
        // Nor without an enemy card.
        var m = Battle(); var s3 = Bf(m, Shinju, 0); Add(m, "training-terrain", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(m), 0, "shinju-destroy", s3.Id), "no enemy card: refused");
        // A Terrain alone is refused by the engine too.
        var one = Rejection(e = MatchEngine.FromTestState(BattleWith(out var ju, out var ter)), Use(e, 0, "shinju-destroy", ju.Id, selections: Pick("terrain", ter.Id)));
        Check(one.Length > 0, "one target is not enough: " + one);
    }

    private static MatchState BattleWith(out CardInstance shinju, out CardInstance terrain)
    {
        var s = Battle(); shinju = Bf(s, Shinju, 0); Bf(s, "training-lion-samurai", 1); terrain = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        return s;
    }

    private static void HanaBow()
    {
        var s = Battle(); var hana = Bf(s, Hana, 0); // 5F
        var host = Bf(s, "training-lion-samurai", 1); // 4F
        var f1 = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); // 2F
        var f2 = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); f2.Bowed = true; // already bowed
        var f3 = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); Force(f3, 4); // 6F: above Hana
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "hana-bow", hana.Id, host.Id));
        Check(e.State.Cards[host.Id].Bowed && e.State.Cards[f1.Id].Bowed && !e.State.Cards[f3.Id].Bowed, "the Personality and the 2F Follower bowed, the 6F Follower not");
        Eq(Honor(e, 0), honor + 2, "1 Honor for each card this bowed (the already-bowed Follower is not counted)");
        // A Personality above Hana's Force is not bowed, but his weak Followers are.
        var b = Battle(); var h2 = Bf(b, Hana, 0); var strong = Bf(b, "training-lion-veteran", 1); var weak = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: strong.Id); Force(strong, 1);
        var be = MatchEngine.FromTestState(b);
        var honor2 = Honor(be, 0);
        Accept(be, Offer(be, 0, "hana-bow", h2.Id, strong.Id));
        Check(!be.State.Cards[strong.Id].Bowed && be.State.Cards[weak.Id].Bowed, "a 7F Personality stays; his 2F Follower bows");
        Eq(Honor(be, 0), honor2 + 1, "1 Honor");
        // A negated bow (Stalwart) is not counted.
        var n = Battle(); var h3 = Bf(n, Hana, 0); var stalwart = Bf(n, "training-lion-samurai", 1); GiveKeyword(stalwart, "Stalwart");
        var nf = Add(n, "training-follower", 1, Zone.Attached, province: 0, parent: stalwart.Id);
        var ne = MatchEngine.FromTestState(n);
        var honor3 = Honor(ne, 0);
        Accept(ne, Offer(ne, 0, "hana-bow", h3.Id, stalwart.Id));
        Check(!ne.State.Cards[stalwart.Id].Bowed && ne.State.Cards[nf.Id].Bowed, "the Stalwart Personality's first bow was negated");
        Eq(Honor(ne, 0), honor3 + 1, "only the Follower counted");
        // Nothing bowed: no Honor.
        var z = Battle(); var h4 = Bf(z, Hana, 0); var already = Bf(z, "training-lion-samurai", 1, bowed: true);
        var ze = MatchEngine.FromTestState(z);
        var honor4 = Honor(ze, 0);
        Accept(ze, Offer(ze, 0, "hana-bow", h4.Id, already.Id));
        Eq(Honor(ze, 0), honor4, "nothing bowed, no Honor");
        // A dishonorable Hana is rehonored instead of her controller gaining the Honor.
        var d = Battle(); var h5 = Bf(d, Hana, 0); h5.Dishonorable = true; var victim = Bf(d, "training-lion-samurai", 1);
        var de = MatchEngine.FromTestState(d);
        var honor5 = Honor(de, 0);
        Accept(de, Offer(de, 0, "hana-bow", h5.Id, victim.Id));
        Check(de.State.Cards[victim.Id].Bowed && !de.State.Cards[h5.Id].Dishonorable && Honor(de, 0) == honor5, "rehonored instead of gaining Honor");
    }

    private static void YuutoImmunity()
    {
        var s = Battle(); var higaonna = Bf(s, Higaonna, 0);
        var yuuto = Bf(s, Yuuto, 1); // 4F
        var plain = Bf(s, "training-lion-samurai", 1);
        var followed = Bf(s, "training-lion-samurai", 1); var followerOfOther = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "higaonna-attack", higaonna.Id).TargetIds!;
        Check(!targets.Contains(yuuto.Id) && targets.Contains(plain.Id) && targets.Contains(followerOfOther.Id), "Yuuto is not a Ranged target; other units are: " + string.Join(",", targets));
        var why = Rejection(e, Use(e, 0, "higaonna-attack", higaonna.Id, yuuto.Id));
        Check(why.Contains("Ranged"), "a command naming him is refused: " + why);
        // His Follower is protected too (a Follower in his unit, but not a Personality with a Follower: Yuuto himself is then immune anyway).
        var f = Battle(); var h2 = Bf(f, Higaonna, 0); var y2 = Bf(f, Yuuto, 1); var yf = Add(f, "training-follower", 1, Zone.Attached, province: 0, parent: y2.Id);
        Check(!Enabled(MatchEngine.FromTestState(f), 0, "higaonna-attack", h2.Id).TargetIds!.Contains(yf.Id), "a Follower in his unit may not be targeted either");
        // A Melee Attack may target him.
        var m = Battle(); var melee = Bf(m, "training-recon-attacker", 0); var y3 = Bf(m, Yuuto, 1); y3.Modifiers.Add(new("test", "test", "Force", -2));
        Check(Enabled(MatchEngine.FromTestState(m), 0, "training-weapon-attack", melee.Id).TargetIds!.Contains(y3.Id), "Melee is not restricted");
    }
}
