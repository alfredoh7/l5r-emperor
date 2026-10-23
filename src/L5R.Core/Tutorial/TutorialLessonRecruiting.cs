namespace L5R.Core;

/// <summary>The Recruiting Personalities lesson (issue #1075, curriculum id <c>recruiting</c>): Crab Control (you) against Classic Crane, a couple of turns in, at the Dynasty phase. You meet Cost and Honor Requirement,
/// a Loyal Personality that will not join your Clan, Proclaim, the Clan Discount and an Overlay; then the Crane player shows what an Honor Requirement does to a Personality of another Clan and how Clan Allegiance
/// ignores it for one of its own. The engine plays every rule: the script only chooses what is offered, and has the opponent make exactly the recruits the notes describe.
/// Every number in the words is checked by TutorialRecruitingTests.</summary>
internal static class TutorialLessonRecruiting
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? card = null) =>
        (look, command) => command.Type == type && (card is null || look.Is(command.CardId, card));

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d));
    private static Func<TutorialLookup, IEnumerable<string>> OwnProvinces(params int[] indices) => look => indices.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    /// <summary>What each Gold source produces for you when paying for a Personality (Yasuki Palaces produces 1 less for one; Iron Mine produces 3 for a Crab player), so a step can ask for an exact payment.</summary>
    private static int Produces(TutorialLookup look, string cardId) => look.DefinitionOf(cardId) switch
    {
        TutorialCards.YasukiPalaces => 4,
        TutorialCards.BorderKeep => 2,
        TutorialCards.BambooHarvesters => 2,
        TutorialCards.IronMine => 3,
        TutorialCards.SmallFarm => 1,
        _ => 0
    };
    private static bool PaysExactly(TutorialLookup look, GameCommand command, int gold) =>
        command.SourceIds is { Length: > 0 } sources && sources.Distinct().Count() == sources.Length && sources.Sum(id => Produces(look, id)) == gold;

    private static bool FoeHas(MatchState state, string definition, Zone zone) =>
        state.Cards.Values.Any(c => c.OwnerId == 1 - TutorialRunner.Seat && c.DefinitionId == definition && c.Zone == zone);

    private static Func<TutorialLookup, IEnumerable<string>> TwoTsujikens => look => look.Own(TutorialCards.YasukiTsujiken).Concat(look.Province(TutorialRunner.Seat, 2) is { } card ? [card] : []);
    private static readonly string[] CrabGold = [TutorialCards.YasukiPalaces, TutorialCards.BorderKeep, TutorialCards.BambooHarvesters, TutorialCards.IronMine, TutorialCards.SmallFarm];

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? emphasis = null, bool slow = false) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, Emphasis = emphasis, Slow = slow };

    private static TutorialStep Do(string id, string title, string body, string instruction, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, GameCommand, bool>? accept = null,
        string? hint = null, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string[]? glow = null) =>
        new() { Id = id, Title = title, Body = body, Instruction = instruction, Allow = allow, Accept = accept, RefusalHint = hint ?? "Do the move the lesson asks for.", Then = then, Highlight = highlight, Glow = glow };

    private static readonly string[] Honor = [TutorialGlow.Honor];

    public static readonly TutorialStep[] Steps =
    [
        Note("welcome", "Recruiting Personalities", "Welcome to Recruiting Personalities. This tutorial will explain recruiting Personalities in more depth. There are only a few rules, but it's important that you know them to make the best decisions about how you play your deck.",
            then: c => { c.StartTurn(); c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); c.Auto("pass"); }),
        Note("start", "A couple of turns in", "We'll start in the Dynasty phase a couple of turns in. Both the Crane and the Crab have lost some Honor. Each player wants to get Personalities into play because they're necessary for winning the game.",
            glow: Honor, slow: true),
        Note("unlimited", "As often as you can pay", "There are some actions that a player may perform an unlimited number of times on their turn. Recruitment is one of them. So long as you can satisfy the costs of recruiting a Personality, you can keep recruiting them until you run out.\n\nUsually, recruitment of Personalities is restricted to the Dynasty phase. However, special abilities may move recruitment into the Action phase or the Battle phase."),
        Note("cost", "Gold Cost", "When recruiting a Personality, there are a few basics to consider. The first is Cost, the central number inside the coin printed on the Personality. Holdings, attachments, and some Strategy cards generally have their printed costs in the same location. Sometimes there are additional, usually optional, costs in the card's text, so read your Personalities and Strategies closely.",
            highlight: OwnProvinces(0, 1, 2, 3), emphasis: TutorialEmphasis.Cost),
        Note("honor-requirement", "Honor Requirement", "To the left of the Gold Cost is a Personality's Honor Requirement. This indicates how honorable you must be for a Personality to join you. Each Personality in your Provinces has a \"-\" in the box, meaning they will happily join the Clan at any Honor. The Crane player isn't so lucky.",
            highlight: OwnProvinces(0, 1, 2, 3), emphasis: TutorialEmphasis.HonorRequirement),
        Note("sachina", "Yoritomo Sachina", "Let's start by recruiting Yoritomo Sachina. She has a very powerful ability. Click on her to start the recruit function.", highlight: OwnProvinces(1)),
        Note("loyal", "Loyal", "Hmm, that's odd. There seems to be something wrong. Mouse over her to see her card.\n\nLook at the bold Keywords below her costs. One of the words printed is Loyal. Loyal is a special keyword that means a Personality will not join a Clan that isn't printed on them at the time of recruitment. (A Loyal Personality may be given, or have control taken; it only blocks recruitment.) Since she's Mantis Clan, she won't join a Crab player.\n\nThe Crab player accidentally included her. They'll want to change that before their next game. We'll discard her at the end of the Dynasty phase, because discarding ends your chance to recruit.",
            highlight: OwnProvinces(1)),
        Note("costs", "Three left", "Let's move on. There are three Personalities left. Let's look at their Gold Costs:\nYasuki Baiko: 6\nYasuki Tanimura: 8\nYasuki Tsujiken: 11\n\nTotal cost: 25\n\nThe Crab player only has 12 Gold to buy Personalities but wants to recruit everyone. I'll show you how.",
            highlight: OwnProvinces(0, 2, 3)),
        Do("proclaim-baiko", "Proclaim", "Let's start with Yasuki Baiko. She costs 6 Gold. She also has 1 Personal Honor. Once per turn, when you pay full price for a Personality aligned to your Clan, you may Proclaim them. Proclaimed Personalities add their Personal Honor to your Family Honor. Typically, Proclaim is chosen for the first Personality you recruit.\n\nIf you overpay, the extra Gold is lost. If that happens, press Cancel payment and come back to this screen.",
            "Click Yasuki Baiko and choose \"6 Gold · Full cost · Proclaim eligible\" in the drop-down in the right sidebar. Click Gold sources that produce exactly 6 Gold (your Stronghold produces 4 for a Personality, your Border Keep 2), then click Recruit.",
            Move("purchase", TutorialCards.YasukiBaiko), accept: (look, command) => !command.ClanDiscount && !command.ClanAllegiance && PaysExactly(look, command, 6),
            hint: "Recruit Yasuki Baiko at her full cost, paying exactly 6 Gold.", then: c => c.Auto("proclaim"), highlight: All(OwnProvinces(3), Own(CrabGold)), glow: Honor),
        Note("proclaimed", "Proclaimed", "You've Proclaimed her and your Honor has gone from -3 to -2. That's good: it makes it harder for you to lose from Dishonor. Now it's time to learn about the Clan Discount.",
            highlight: Own(TutorialCards.YasukiBaiko), glow: Honor),
        Note("clan-discount", "The Clan Discount", "Yasuki Tanimura costs 8 Gold but you only have 6 left. You can still recruit him, thanks to the Clan Discount.\n\nClan Discount: when recruiting a Personality aligned to your Clan, you may pay 2 less Gold for him, without Proclaiming him.\n\nYou don't have to Proclaim anyone. Since you've already Proclaimed Yasuki Baiko this turn, you cannot gain Tanimura's Honor, even if you pay full price.",
            highlight: OwnProvinces(0)),
        Do("recruit-tanimura", "Recruiting Tanimura", "The game defaults to the Clan Discount after you recruit your first Personality.",
            "Click Yasuki Tanimura, check that it says \"6 Gold · Clan discount\", click the Gold sources you have left (6 Gold) and click Recruit.",
            Move("purchase", TutorialCards.YasukiTanimura), accept: (look, command) => command.ClanDiscount && !command.ClanAllegiance && PaysExactly(look, command, 6),
            hint: "Recruit Yasuki Tanimura with the Clan Discount, paying the 6 Gold you have left.", highlight: All(OwnProvinces(0), Own(CrabGold))),
        Note("out-of-gold", "Out of Gold", "You're out of Gold, but you want to recruit Yasuki Tsujiken. That's a problem.\n\nThere's already a different copy of Yasuki Tsujiken in play. Mouse over both of them and see if you notice what's different besides their artwork.",
            highlight: TwoTsujikens),
        Note("experienced", "Experienced", "You've probably noticed that these are two fairly different cards. The important distinction is that the Yasuki Tsujiken in your Provinces reads \"Experienced\", and the Yasuki Tsujiken in play has no such distinction.",
            highlight: TwoTsujikens),
        Note("overlay-rule", "Overlay", "When you have a less experienced version of a card in play and a more experienced version in your Provinces, you may, optionally, Overlay the Personality with more experience on the Personality with less. So, you may Overlay an Experienced 2 Personality on a Personality with the same title and either Experienced, no experience designation, or the Inexperienced keyword."),
        Do("overlay", "Overlaying", "The overlaying Personality takes on whatever the old Personality had attached, so it inherits the -1 Chi token and the Pillowbook. The overlaid Personality is removed from the game.",
            "Click Yasuki Tsujiken in your Provinces, then click Overlay in the right sidebar.",
            Move("overlay", TutorialCards.YasukiTsujikenExp), hint: "Overlay Yasuki Tsujiken (Experienced) onto the Yasuki Tsujiken in play.", highlight: TwoTsujikens),
        Note("overlaid", "Carried over", "The experienced Yasuki Tsujiken has the Pillowbook and the -1 Chi token the old one carried, and the old one is gone from the game. Overlaying cost no Gold.",
            highlight: Own(TutorialCards.YasukiTsujikenExp, TutorialCards.Pillowbook)),
        Do("discard-sachina", "Discarding Sachina", "You've recruited everyone you can. Discarding the card nobody can recruit ends your recruiting for the turn, so it comes last.",
            "Click Yoritomo Sachina, click Discard in the right sidebar, then click Confirm.", Move("discardProvince", TutorialCards.YoritomoSachina), hint: "Discard Yoritomo Sachina.", highlight: OwnProvinces(1)),
        Do("pass-turn", "Ending your turn", "You've cleared your Provinces. Let's see what the Crane does.", "Click End Dynasty Phase to end your turn.", Move("endDynasty"),
            then: c => { c.Foe(s => s.ActivePlayerId == 1 && s.Phase == Phase.Action && s.PriorityPlayerId == TutorialRunner.Seat); c.Auto("pass"); }),
        Note("crane-turn", "The Crane's turn", "The Crane player has 3 Personalities that they can recruit. Two of them are Crane and one is Unicorn Clan. But they're in trouble: their Family Honor is 1. Only Daidoji Tsunehiko has an Honor Requirement lower than their current Family Honor.",
            highlight: FoeProvinces(0, 1, 2), emphasis: TutorialEmphasis.HonorRequirement),
        Note("mai", "Not our Clan", "For Utaku Mai, who is not Crane Clan, this is a big problem. Her Honor Requirement is 5. Because she is not Crane Clan, that is a hard limit: she cannot be recruited by the Crane player until their Family Honor is 5 or greater.\n\nDaidoji Kenshi is also a problem. Her Honor Requirement is 4, and Proclaiming Daidoji Tsunehiko won't be enough to get the Crane player to 4 Honor. But for family, we make exceptions.",
            highlight: FoeProvinces(0, 1, 2)),
        Note("allegiance", "Clan Allegiance", "Because Kenshi is a Crane Clan Personality and the deck she is in is a Crane deck, her owner may choose to ignore her Honor Requirement by paying an additional 2 Gold to recruit her. This may be applied in conjunction with Proclaiming or with the Clan Discount.",
            then: c => c.Foe(s => FoeHas(s, TutorialCards.DaidojiKenshi, Zone.Home) && s.PendingChoice is null), highlight: FoeProvinces(1)),
        Note("kenshi", "Kenshi joins", "The Crane player opted to recruit and Proclaim Kenshi: they bowed their Stronghold and Marketplace for 7 Gold, and their Honor is now 3. Let's see what they do next.",
            then: c => c.Foe(s => FoeHas(s, TutorialCards.UtakuMai, Zone.DynastyDiscard)), highlight: Own(TutorialCards.DaidojiKenshi), glow: Honor),
        Note("crane-done", "Taking a chance", "The Crane player recruited Daidoji Tsunehiko for 6 Gold, bought the Marketplace, and discarded Utaku Mai. Rather than hope for a scenario where their Honor increases to 5, they've decided to take their chances on drawing a Personality they can recruit. Including Personalities that don't have your Clan alignment in your deck can be a powerful choice, but it also carries its own risks.",
            highlight: look => look.Foe(TutorialCards.DaidojiTsunehiko, Zone.Home).Concat(look.Foe(TutorialCards.Marketplace, Zone.Home))),
        Note("done", "Congratulations", "You're now familiar with how to recruit and Proclaim Personalities, how to apply Clan Discounts, and how to ignore Honor Requirements for Clan-aligned Personalities.\n\nYou've also learned about Overlaying Experienced Personalities, and about recruitment restrictions on Loyal Personalities and Personalities that don't share your alignment. Select the Next Tutorial to continue learning.",
            button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Crab Control (you), at Yasuki Palaces with -3 Honor: the Border Keep, Bamboo Harvesters, Iron Mine and Small Farm in play, and Yasuki Tsujiken carrying the Pillowbook and a -1 Chi token. Your Provinces open on Yasuki Tanimura,
    /// Yoritomo Sachina, Yasuki Tsujiken (Experienced) and Yasuki Baiko. Classic Crane, at Shinden Asahina with 1 Honor, has a Marketplace, Border Keep, Large Farm, Prosperous Village and Small Farm in play, and its Provinces open on Utaku Mai,
    /// Daidoji Kenshi, Daidoji Tsunehiko and a Marketplace. Nobody has a hand and nobody draws.</summary>
    private static readonly TutorialSetup Table = new(
        new("Crab", TutorialCards.YasukiPalaces,
            [TutorialCards.YasukiTanimura, TutorialCards.YoritomoSachina, TutorialCards.YasukiTsujikenExp, TutorialCards.YasukiBaiko, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.Sniping],
            [new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters), new(TutorialCards.IronMine), new(TutorialCards.SmallFarm), new(TutorialCards.YasukiTsujiken, Attachments: [TutorialCards.Pillowbook], ChiToken: -1)],
            Honor: -3, Deck: Filler("Crab", TutorialCards.YasukiPalaces, [TutorialCards.SmallFarm, TutorialCards.LargeFarm], [TutorialCards.Sniping])),
        new("Crane", TutorialCards.FoeStronghold,
            [TutorialCards.UtakuMai, TutorialCards.DaidojiKenshi, TutorialCards.DaidojiTsunehiko, TutorialCards.Marketplace, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.Sniping],
            [new(TutorialCards.Marketplace), new(TutorialCards.BorderKeep), new(TutorialCards.LargeFarm), new(TutorialCards.ProsperousVillage), new(TutorialCards.SmallFarm)],
            Honor: 1, Deck: Filler("Crane", TutorialCards.FoeStronghold, [TutorialCards.SmallFarm, TutorialCards.LargeFarm], [TutorialCards.Sniping])),
        TurnsTaken: 2, NoEndDraw: true);

    /// <summary>Crane recruits Kenshi at full cost with Clan Allegiance, paid by its Stronghold and Marketplace (7 Gold), and Proclaims her; Tsunehiko with the Clan Discount (6 Gold: Prosperous Village and Small Farm);
    /// the Marketplace (2 Gold: Border Keep); then discards Utaku Mai.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], [],
        Purchases:
        [
            new(TutorialCards.DaidojiKenshi, false, true, [TutorialCards.FoeStronghold, TutorialCards.Marketplace], Proclaim: true),
            new(TutorialCards.DaidojiTsunehiko, true, false, [TutorialCards.ProsperousVillage, TutorialCards.SmallFarm]),
            new(TutorialCards.Marketplace, false, false, [TutorialCards.BorderKeep]),
        ],
        Discard: [TutorialCards.UtakuMai]);

    public static readonly TutorialLesson Lesson = new("recruiting", "Recruiting Personalities", Table, Steps, FoePlays, ProvincesStep: 1);
}
