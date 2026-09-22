using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #431: the Before the Dawn cards that create other cards or pause a duel for a choice (personality-wave-45, spell-wave-17, follower-wave-21): Death, Defeated, Goju Sawaki, Kakita Yasunori
// and The Crimson Mark, with the created-only scans they use.
internal static class BeforeTheDawnCreationTests
{
    private const string DeathDefeated = "oracle-l5r-1898", Sawaki = "oracle-l5r-2858", Yasunori = "oracle-l5r-4240", CrimsonMark = "oracle-l5r-8002", Genin = "oracle-l5r-9711",
        WalkingDead = "oracle-l5r-9741", YasunorisGift = "oracle-l5r-10019";

    public static void Run(TestRunner runner) => runner.Run("btd-creation", [
        ("Published shapes: the five cards and the two created Personalities are the printed cards with their abilities, traits and art", PublishedShapes),
        ("Death, Defeated: +2F and Shadowlands for its Shugenja; removes a dead Personality to create The Walking Dead at the battlefield for the battle, and loses 2 Honor", DeathDefeatedCreates),
        ("Goju Sawaki: destroyed as the cost when a Battle action targets him, he creates two Goju Genin at the battlefield and the action goes on without him", SawakiSpawns),
        ("Kakita Yasunori: removes a card of your Fate discard pile from the game and creates Yasunori's Gift on your Personality", YasunoriCreates),
        ("The Crimson Mark: a Focus Effect that may destroy an attachment on a Personality in the duel; a Battle action that destroys it and an enemy card without attachments", CrimsonMark_),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);
    private static MatchState Battle() { var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; return s; }
    private static CardInstance[] Created(MatchEngine e, string definitionId) => e.State.Cards.Values.Where(c => c.DefinitionId == definitionId && c.Id.StartsWith("created-")).ToArray();

    private static void PublishedShapes()
    {
        var expected = new (string Id, string Name, CardType Type, string[] Abilities, string Effect)[]
        {
            (DeathDefeated, "Death, Defeated", CardType.Spell, ["death-defeated-create"], "death-defeated-force"), (Sawaki, "Goju Sawaki", CardType.Personality, ["sawaki-spawn"], "none"),
            (Yasunori, "Kakita Yasunori", CardType.Personality, ["yasunori-create"], "none"), (CrimsonMark, "The Crimson Mark", CardType.Follower, ["crimson-mark-focus", "crimson-mark-destroy"], "none"),
            (Genin, "Goju Genin", CardType.Personality, [], "none"), (WalkingDead, "The Walking Dead", CardType.Personality, [], "none"),
        };
        var root = RepoRoot();
        foreach (var (id, name, type, abilities, effect) in expected)
        {
            var card = CardCatalog.Get(id);
            Check(card.Name == name && card.Type == type && (card.Abilities ?? []).SequenceEqual(abilities) && card.Effect == effect, $"{name}: {card.Name} {card.Type} {string.Join(",", card.Abilities ?? [])} {card.Effect}");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{name}: art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(CardCatalog.Get(DeathDefeated).AttachGrantsKeyword, "Shadowlands", "the Spell gives its Shugenja Shadowlands");
        Eq(CardCatalog.Get(Sawaki).EntryHonorLoss, 2, "Sawaki loses 2 Honor entering play");
        Check(CardCatalog.Get(Genin).CreatedOnly && CardCatalog.Get(WalkingDead).CreatedOnly, "the two created Personalities are created-only scans");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["death-defeated-create"].Creates!).Id, WalkingDead, "Death, Defeated creates The Walking Dead");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["sawaki-spawn"].Creates!).Id, Genin, "Sawaki creates Goju Genin");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["yasunori-create"].Creates!).Id, YasunorisGift, "Yasunori creates Yasunori's Gift");
        Check(CardCatalog.PlayableFromDecks.All(c => c.Id is not (Genin or WalkingDead)), "neither created Personality is playable from a deck");
    }

    private static void DeathDefeatedCreates()
    {
        // +2F, and Shadowlands from the keyword the Spell grants while attached.
        var s = Battle(); var shugenja = Bf(s, "training-shugenja", 0); var spell = Add(s, DeathDefeated, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        shugenja.GrantedKeywords.Add(new("Shadowlands", spell.Id, "Attachment"));
        var foe = Bf(s, "training-lion-samurai", 1);
        var deadEnemy = Add(s, "training-lion-veteran", 1, Zone.Dead); var deadOwn = Add(s, "training-crane-samurai", 0, Zone.Dead); var discarded = Add(s, "training-crane-veteran", 0, Zone.DynastyDiscard);
        var plain = MatchEngine.FromTestState(Battle());
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(shugenja.Id), Def(shugenja).Force + 2, "+2F for the Shugenja");
        Check(e.HasKeyword(shugenja.Id, "Shadowlands"), "and Shadowlands");
        var option = Options(e, 0, "death-defeated-create", spell.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(deadEnemy.Id) && option.TargetIds.Contains(deadOwn.Id) && !option.TargetIds.Contains(discarded.Id) && !option.TargetIds.Contains(foe.Id), "a dead Personality in any player's discard pile: " + string.Join(",", option.TargetIds ?? []));
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "death-defeated-create", spell.Id, deadEnemy.Id));
        Eq(e.State.Cards[deadEnemy.Id].Zone, Zone.RemovedFromGame, "the dead Personality is removed from the game");
        var walking = Created(e, WalkingDead).Single();
        Check(walking.OwnerId == 0 && walking.Zone == Zone.Battlefield && walking.ProvinceIndex == 0, "The Walking Dead is created at the current battlefield, for the acting player");
        Check(e.State.CreatedUntilBattleEndsIds.SequenceEqual([walking.Id]), "and leaves when the battle ends");
        Check(e.CardForceForTest(walking.Id) == 2 && e.HasKeyword(walking.Id, "Undead") && e.HasKeyword(walking.Id, "Shadowlands"), "2F, Shadowlands and Undead");
        Eq(Honor(e, 0), honor - 2, "2 Honor lost");
        Check(!CanUse(e, 0, "death-defeated-create", spell.Id), "once a turn");
        // Without the Spell there is no bonus.
        Check(plain.State.Cards.Count >= 0);
    }

    private static void SawakiSpawns()
    {
        // Player 1's Tsuji bows Sawaki (a Battle action): Sawaki may destroy himself to create two Goju Genin; the bow is gone with him.
        foreach (var react in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var sawaki = Bf(s, Sawaki, 0); var tsuji = Bf(s, "oracle-l5r-7179", 1); var ally = Bf(s, "training-crane-samurai", 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "tsuji-bow", tsuji.Id, sawaki.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the action is held for Sawaki's Reaction");
            var offered = e.QueryActions(0).FirstOrDefault(x => x.Enabled && x.Command?.AbilityId == "sawaki-spawn");
            Check(offered is not null, "Sawaki's Reaction is offered");
            if (!react)
            {
                for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: "beforeActionReaction" }; guard++)
                    Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(x => x.Type == "declineReaction").Command!);
                Check(e.State.Cards[sawaki.Id].Bowed && Created(e, Genin).Length == 0, "declined: the action bows him and nothing is created");
                continue;
            }
            Accept(e, offered!.Command!);
            for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: "beforeActionReaction" }; guard++)
                Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(x => x.Type == "declineReaction").Command!);
            Check(e.State.Cards[sawaki.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "Sawaki is destroyed as the cost");
            var genin = Created(e, Genin);
            Eq(genin.Length, 2, "two Goju Genin");
            Check(genin.All(g => g.OwnerId == 0 && g.Zone == Zone.Battlefield && g.ProvinceIndex == 0 && e.CardForceForTest(g.Id) == 1 && e.HasKeyword(g.Id, "Ninja") && e.HasKeyword(g.Id, "Shadowlands") && e.HasKeyword(g.Id, "Spider Clan")), "1F Spider Clan, Shadowlands, Ninja Personalities at the battlefield");
            Check(e.State.CreatedUntilBattleEndsIds.Count == 0, "they stay");
            Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "beforeActionReaction", "the window is closed");
            Check(ally.Id.Length > 0);
        }
        // Not for an action of his own controller's, nor at a Personality who is not him.
        var t = Battle(); var sawaki2 = Bf(t, Sawaki, 0); var mine = Bf(t, "oracle-l5r-7179", 0); var foe = Bf(t, "training-lion-samurai", 1);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, "tsuji-bow", mine.Id, foe.Id));
        Check(!te.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId == "sawaki-spawn") && sawaki2.Id.Length > 0, "his own controller's action is not answered");
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 1; var s3 = Bf(u, Sawaki, 0); var other = Bf(u, "training-crane-samurai", 0); var tsuji3 = Bf(u, "oracle-l5r-7179", 1);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Offer(ue, 1, "tsuji-bow", tsuji3.Id, other.Id));
        Check(!ue.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId == "sawaki-spawn") && s3.Id.Length > 0, "an action targeting another Personality is not answered");
    }

    private static void YasunoriCreates()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var yasunori = Add(s, Yasunori, 0, Zone.Home); var host = Add(s, "training-crane-samurai", 0, Zone.Home); var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var ownDiscard = Add(s, "training-weapon", 0, Zone.FateDiscard); var theirDiscard = Add(s, "training-weapon", 1, Zone.FateDiscard); var inHand = Add(s, "training-follower", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var slots = Enabled(e, 0, "yasunori-create", yasunori.Id).Slots!;
        var hosts = slots.Single(x => x.Slot == "host").CardIds; var removable = slots.Single(x => x.Slot == "removed").CardIds;
        Check(hosts.Contains(host.Id) && !hosts.Contains(enemy.Id), "your Personality");
        Check(removable.Contains(ownDiscard.Id) && !removable.Contains(theirDiscard.Id) && !removable.Contains(inHand.Id), "a card in your Fate discard pile: " + string.Join(",", removable));
        var command = Enabled(e, 0, "yasunori-create", yasunori.Id).Command! with { TargetId = host.Id };
        Reject(e, command with { Selections = [Pick("host", enemy.Id), Pick("removed", ownDiscard.Id)] });
        Reject(e, command with { Selections = [Pick("host", host.Id), Pick("removed", theirDiscard.Id)] });
        Reject(e, command with { Selections = [Pick("host", host.Id), Pick("removed", inHand.Id)] });
        Accept(e, command with { Selections = [Pick("host", host.Id), Pick("removed", ownDiscard.Id)] });
        Eq(e.State.Cards[ownDiscard.Id].Zone, Zone.RemovedFromGame, "the discarded card is removed from the game");
        var gift = Created(e, YasunorisGift).Single();
        Check(gift.Zone == Zone.Attached && gift.AttachedTo == host.Id && gift.OwnerId == 0, "Yasunori's Gift is attached to the Personality");
        Eq(e.CardForceForTest(host.Id), Def(host).Force + 1, "+1F");
        Check(e.HasKeyword(gift.Id, "Weapon"), "a Weapon");
        Check(!CanUse(e, 0, "yasunori-create", yasunori.Id), "Limited: once a turn");
    }

    private static void CrimsonMark_()
    {
        // The Focus Effect: player 0's Personality duels player 1's; both wear an attachment, a third Personality's is out of it.
        (MatchEngine E, CardInstance Mine, CardInstance Theirs, CardInstance MyArm, CardInstance TheirArm, CardInstance OtherArm) Duel(bool armed = true)
        {
            var s = Battle();
            var mine = Bf(s, "training-crane-veteran", 0); mine.Modifiers.Add(new("test-source", "test", "Chi", 5)); var theirs = Bf(s, "training-lion-samurai", 1); var other = Bf(s, "training-crane-samurai", 0);
            var myArm = armed ? Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: mine.Id) : mine; var theirArm = armed ? Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: theirs.Id) : theirs;
            var otherArm = armed ? Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: other.Id) : other;
            var duel = Add(s, "training-duel-challenge", 0, Zone.Hand); var mark = Add(s, CrimsonMark, 0, Zone.Hand);
            var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-duel-challenge", duel.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("acceptDuel", 1, e.Version));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: mark.Id));
            Accept(e, new GameCommand("strike", 1, e.Version));
            return (e, mine, theirs, myArm, theirArm, otherArm);
        }
        var (e1, _, _, myArm1, theirArm1, otherArm1) = Duel();
        Eq(e1.State.PendingChoice?.Type, "focusEffectChoice", "the Focus Effect asks its player");
        var picks = e1.QueryActions(0).Where(x => x.Type == "chooseFocusEffect" && x.Command?.TargetId is not null).Select(x => x.Command!.TargetId).ToArray();
        Check(picks.Length == 2 && picks.Contains(myArm1.Id) && picks.Contains(theirArm1.Id) && !picks.Contains(otherArm1.Id), "the attachments on the two duelists, not on a third Personality: " + string.Join(",", picks) + $" [mine {myArm1.Id} theirs {theirArm1.Id} other {otherArm1.Id}; duel {e1.State.PendingDuel?.ChallengerId}/{e1.State.PendingDuel?.ChallengedId}]");
        Check(!e1.QueryActions(1).Any(x => x.Type == "chooseFocusEffect"), "only the Focus Effect's player chooses");
        Accept(e1, e1.QueryActions(0).First(x => x.Type == "chooseFocusEffect" && x.Command?.TargetId == theirArm1.Id).Command!);
        Check(e1.State.Cards[theirArm1.Id].Zone != Zone.Attached && e1.State.Cards[myArm1.Id].Zone == Zone.Attached && e1.State.Cards[otherArm1.Id].Zone == Zone.Attached, "the chosen attachment is destroyed and no other");
        Check(e1.State.PendingDuel is null && e1.State.PendingChoice?.Type != "focusEffectChoice", "the duel finishes");
        var (e2, _, _, myArm2, theirArm2, _) = Duel();
        Accept(e2, e2.QueryActions(0).First(x => x.Type == "chooseFocusEffect" && x.Command?.Choice == "decline").Command!);
        Check(e2.State.Cards[myArm2.Id].Zone == Zone.Attached && e2.State.Cards[theirArm2.Id].Zone == Zone.Attached, "it is a may: declined, nothing is destroyed");
        var (e3, _, _, _, _, _) = Duel(armed: false);
        Check(e3.State.PendingChoice?.Type != "focusEffectChoice" && e3.State.PendingDuel is null, "with no attachment on either duelist nothing is asked");
        // Battle: destroy this Follower and an enemy card without attachments.
        var s4 = Battle(); var host = Bf(s4, "training-crane-samurai", 0); var mark4 = Add(s4, CrimsonMark, 0, Zone.Attached, province: 0, parent: host.Id);
        var bare = Bf(s4, "training-lion-samurai", 1); var armed4 = Bf(s4, "training-lion-veteran", 1); Add(s4, "training-weapon", 1, Zone.Attached, province: 0, parent: armed4.Id);
        var e4 = MatchEngine.FromTestState(s4);
        var targets = Options(e4, 0, "crimson-mark-destroy", mark4.Id).First(o => o.Enabled).TargetIds!;
        Check(targets.Contains(bare.Id) && !targets.Contains(armed4.Id), "an enemy card without attachments: " + string.Join(",", targets));
        Accept(e4, Offer(e4, 0, "crimson-mark-destroy", mark4.Id, bare.Id));
        Check(e4.State.Cards[mark4.Id].Zone != Zone.Attached && e4.State.Cards[bare.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the Follower is destroyed as the cost and so is the target");
    }
}
