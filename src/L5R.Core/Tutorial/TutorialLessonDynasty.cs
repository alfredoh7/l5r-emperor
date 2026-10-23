namespace L5R.Core;

/// <summary>The Events, Regions, and Celestials lesson (issue #1074, curriculum id <c>dynasty-deck</c>): the Unicorn Maidens (you) against Mantis Manipulation, the first turn of each. Your Provinces hold an Event
/// (Dark Experiments), a Region (Natural Aviary), a Celestial (Ebisu's Honesty) and a Holding; then the Mantis player's first turn shows a Region and a Celestial entering play and what Border Keep's first-turn
/// refresh does to them. The engine plays every rule: the script only stops the Events phase at each province (<see cref="MatchEngine.TutorialHoldReveal"/>) so the note can talk about the card, and has the
/// opponent use its Border Keep. Every number in the words is checked by TutorialTests.</summary>
internal static class TutorialLessonDynasty
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? ability = null, string? card = null) =>
        (look, command) => command.Type == type && (ability is null || command.AbilityId == ability) && (card is null || look.Is(command.CardId, card));

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d));
    private static Func<TutorialLookup, IEnumerable<string>> OwnProvinces(params int[] indices) => look => indices.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    private static bool PaidBy(TutorialLookup look, GameCommand command, params string[] definitions) =>
        command.SourceIds is { Length: > 0 } sources && sources.Distinct().Count() == sources.Length && sources.Length == definitions.Length
        && definitions.All(d => sources.Any(id => look.Is(id, d)));

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? show = null, bool slow = false, string[]? animateOnly = null) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, ShowDefinitionId = show, Slow = slow, AnimateOnly = animateOnly };

    private static TutorialStep Do(string id, string title, string body, string instruction, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, GameCommand, bool>? accept = null,
        string? hint = null, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string[]? glow = null, int[]? onlyProvinces = null, bool slow = false) =>
        new()
        {
            Id = id,
            Title = title,
            Body = body,
            Instruction = instruction,
            Allow = allow,
            Accept = accept,
            RefusalHint = hint ?? "Do the move the lesson asks for.",
            Then = then,
            Highlight = highlight,
            Glow = glow,
            OnlyProvinces = onlyProvinces,
            Slow = slow
        };

    private static readonly string[] Bar = [TutorialGlow.PhaseBar];

    /// <summary>The Mantis Provinces Dark Experiments may name: not the leftmost, where the Mantis player's Region attaches later in the lesson (a Region attaching to a Dark Experiments Province is destroyed).</summary>
    private static readonly int[] MarkableProvinces = [1, 2, 3];
    private static bool NamesAMarkableMantisProvince(GameCommand command) =>
        command.Choice?.Split(':') is [var owner, var index] && owner == (1 - TutorialRunner.Seat).ToString() && int.TryParse(index, out var i) && MarkableProvinces.Contains(i);

    public static readonly TutorialStep[] Steps =
    [
        Note("intro", "More Dynasty cards", "You've seen Personalities and Holdings. However, there are 3 other kinds of Dynasty cards you need to be familiar with to play L5R. Each of these cards enters play during the Events phase, so we'll start there.",
            then: c =>
            {
                // The Events phase stops at each of your Provinces so the note can talk about the card in it (before it is turned over, and with it face up before it resolves).
                c.HoldReveal(0, 0, after: true);
                for (var province = 1; province < 4; province++) { c.HoldReveal(0, province, after: false); c.HoldReveal(0, province, after: true); }
                c.StartTurn();
            }, glow: Bar, slow: true),
        Note("event", "Events", "This is an Event. All Events have a similar white border. Events are single-use effects that occur once and are then discarded. Sometimes they have a persistent effect, in which case there will often be a reminder placed on the board somewhere.",
            show: TutorialCards.DarkExperiments, glow: Bar),
        Note("dark-experiments", "Dark Experiments", "Dark Experiments allows you to select a Province and permanently reduce its strength by 2, and the Gold production of any Holding that enters play from it by 1.",
            then: c => c.Resume(), show: TutorialCards.DarkExperiments, glow: Bar),
        Do("choose-province", "Resolving it", "To resolve Dark Experiments, select one of the Mantis player's Provinces and confirm.",
            "Click one of the Mantis player's Provinces, except the leftmost one (the lesson needs that one later), then click Confirm choice.",
            (_, command) => command.Type == "eventChoice" && NamesAMarkableMantisProvince(command), hint: "Choose one of the Mantis player's Provinces other than the leftmost, then confirm.",
            highlight: FoeProvinces(1, 2, 3), onlyProvinces: MarkableProvinces, glow: Bar),
        Note("marker", "The reminder", "You can see that a -1 Gold coin and a -2 Province Strength marker have been applied to the Province you selected. This is the reminder for Dark Experiments.", highlight: FoeProvinces(1, 2, 3), glow: Bar),
        Note("refill", "Replacing the card", "When a Dynasty card leaves a Province, it is always replaced with a face-down card immediately.", then: c => c.Resume(), highlight: OwnProvinces(0), glow: Bar),
        Note("region", "Regions", "This is a Region. Regions attach to Provinces the way Items, Followers, and Spells attach to a Personality.", then: c => c.Resume(), show: TutorialCards.NaturalAviary, glow: Bar),
        Note("region-actions", "Region actions", "A Region with a Battle action on it may only be performed during battles at the Province it is attached to. Regions with Limited or Open actions may be used during the Action phase. Regions with Reactions tell you when those Reactions are triggered.",
            highlight: Own(TutorialCards.NaturalAviary), glow: Bar),
        Note("region-inspect", "Reading an attachment", "You can mouse over any attached card to see its full text. This is useful because the text on attachments is hidden by what they are attached to. Mouse over the Natural Aviary to see what it does.",
            then: c => c.Resume(), highlight: Own(TutorialCards.NaturalAviary), glow: Bar),
        Note("celestial", "Celestials", "This is a Celestial. Unlike Events and Regions, which only resolve in the Events phase, Celestials enter play whenever they are revealed. That is mostly the Events phase, but it can be other times as well.",
            then: c => c.Resume(), show: TutorialCards.EbisusHonesty, glow: Bar),
        Note("celestial-placed", "In play", "Celestials (and other cards that aren't Holdings or Personalities) generally enter play on the right-hand side of your tableau. Ongoing reminders are placed here too.",
            then: c => c.Resume(), highlight: Own(TutorialCards.EbisusHonesty), glow: Bar),
        Note("village", "The last Province", "The last Province holds a Holding, Prosperous Village. A Holding doesn't do anything when it is revealed; it waits in its Province until the Dynasty phase. There isn't much else to do this turn: we'll pass our Action, decline to attack, and move to the Dynasty phase.",
            then: c => c.Resume(), show: TutorialCards.ProsperousVillage, glow: Bar),
        Do("pass-action", "Passing", "Nothing in your hand or on the table needs an Action this turn.", "Click Pass action.", Move("pass"), glow: Bar),
        Do("decline-attack", "No attack yet", "You have no Personalities to attack with, so decline to attack.", "Click Decline to attack.", Move("pass"), glow: Bar),
        Do("buy-village", "The Dynasty phase", "Click Prosperous Village, then bow your Stronghold and your Border Keep to pay for it, and confirm to purchase the Holding.",
            "Click Prosperous Village, then click your Stronghold and your Border Keep to pay 6 Gold. Click Confirm to buy it.",
            Move("purchase", card: TutorialCards.ProsperousVillage), accept: (look, command) => !command.ClanDiscount && !command.ClanAllegiance && PaidBy(look, command, TutorialCards.UnicornStronghold, TutorialCards.BorderKeep),
            hint: "Buy Prosperous Village, paid by your Stronghold and your Border Keep.", highlight: All(OwnProvinces(3), Own(TutorialCards.UnicornStronghold, TutorialCards.BorderKeep)), glow: Bar, slow: true,
            then: c => { c.HoldAt(Phase.Events, 1); c.Auto("endDynasty"); }),
        Note("mantis-turn", "The Mantis turn", "Now it's the Mantis player's turn. Let's see how this plays out for them. Just like you, the Mantis player has had a Region and a Celestial enter play during the Events phase.",
            highlight: Foe(TutorialCards.ShindenShorai, TutorialCards.YoritomosGuidance), glow: Bar),
        Note("border-keep", "Border Keep", "However, the Mantis player is clever and wants to use the Limited action on their Border Keep, usable on their first turn, to see more cards.",
            then: c => { c.Resume(); c.HoldReveal(1, 1, after: true); c.FoeBorderKeep(0, 1, 3); }, highlight: Foe(TutorialCards.BorderKeep), glow: Bar, slow: true),
        Note("one-celestial", "One Celestial at a time", "Suitengu's Blessing was just revealed in the next Province. A player may only have one Celestial in play at a time, and Celestials enter play immediately when revealed.",
            then: c => c.Resume(), highlight: Foe(TutorialCards.SuitengusBlessing), glow: Bar, animateOnly: [TutorialCards.SuitengusBlessing]),
        Note("replaced", "Replaced", "This causes Yoritomo's Guidance to be discarded. It is replaced with Suitengu's Blessing.", highlight: Foe(TutorialCards.SuitengusBlessing), glow: Bar),
        Note("boastful", "Not yet", "In the final Province the Mantis player revealed Boastful Proclamation. Notice that it is currently the Action phase in the turn tracker. Events and Regions only enter play during the Events phase, so Boastful Proclamation will not resolve until the Mantis player's next Events phase.",
            highlight: Foe(TutorialCards.BoastfulProclamation), glow: Bar),
        Note("one-region", "One Region per Province", "Bad luck for the Mantis player: each Province can only have one Region attached, and you may not swap them once they're attached. Regions, like Events, only attach during the Events phase. Plains of Otosan Uchi will either be discarded during the Dynasty phase or, if the Mantis player does nothing, during their next Events phase when it fails to attach.",
            highlight: Foe(TutorialCards.PlainsOfOtosanUchi, TutorialCards.ShindenShorai), glow: Bar),
        Note("done", "Congratulations", "Congratulations, you've completed the tutorial on the remaining types of Dynasty cards! Select the Next Tutorial to continue learning.", button: "Tutorial Menu"),
    ];

    /// <summary>A deck of exactly these cards on top, then filler to forty so the draws never run dry.</summary>
    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Unicorn Maidens (you): your four Provinces open on Dark Experiments, Natural Aviary, Ebisu's Honesty and Prosperous Village, then three farms for the refills. Mantis Manipulation: Shinden Shorai, Yoritomo's
    /// Guidance, Kobune Port and Sheltered Port; then two farms (the refills of the Region and the Celestial) before the cards Border Keep's refresh reveals: Plains of Otosan Uchi, Suitengu's Blessing and Boastful Proclamation.</summary>
    private static readonly TutorialSetup Table = new(
        new("Unicorn", TutorialCards.UnicornStronghold, [TutorialCards.DarkExperiments, TutorialCards.NaturalAviary, TutorialCards.EbisusHonesty, TutorialCards.ProsperousVillage, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.SneakAttack, TutorialCards.SoundOfThunder, TutorialCards.WallOfHonor, TutorialCards.PureIntent], [new(TutorialCards.BorderKeep)],
            Deck: Filler("Unicorn", TutorialCards.UnicornStronghold, [TutorialCards.SmallFarm, TutorialCards.LargeFarm], [TutorialCards.SneakAttack, TutorialCards.SoundOfThunder, TutorialCards.WallOfHonor, TutorialCards.PureIntent])),
        new("Mantis", TutorialCards.MantisStronghold, [TutorialCards.ShindenShorai, TutorialCards.YoritomosGuidance, TutorialCards.KobunePort, TutorialCards.ShelteredPort, TutorialCards.SmallFarm, TutorialCards.SmallFarm,
                TutorialCards.PlainsOfOtosanUchi, TutorialCards.SuitengusBlessing, TutorialCards.BoastfulProclamation],
            [TutorialCards.Sniping], [new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters, Bowed: true)],
            Deck: Filler("Mantis", TutorialCards.MantisStronghold, [TutorialCards.SmallFarm, TutorialCards.LargeFarm], [TutorialCards.Sniping])));

    public static readonly TutorialLesson Lesson = new("dynasty-deck", "Events, Regions, and Celestials", Table, Steps, TutorialFoeScript.Passive);
}
