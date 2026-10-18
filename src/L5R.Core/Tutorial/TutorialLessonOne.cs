namespace L5R.Core;

/// <summary>Lesson 1, "Cards and card types" (issue #935): the first two turns of a game, Lion against the practice Crane opponent, taught card by card.
/// The words come from tools/tutorial/lesson1_source.py (generated into <see cref="TutorialLessonOneText"/>, already paged to fit the note column; the build prompt in
/// docs/tutorial/ is made from the same source). This file adds what each step does to the table: which move it waits for, which cards it points at, what its button changes.
/// Every number the text quotes is checked against the catalogue by a test. The script only chooses what is offered and when; the engine decides every rule.</summary>
internal static class TutorialLessonOne
{
    private const string Continue = "Continue";

    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? ability = null, string? card = null) =>
        (look, command) => command.Type == type && (ability is null || command.AbilityId == ability) && (card is null || look.Is(command.CardId, card));

    /// <summary>The province an attack names: the command carries it as the one assignment of the attacking unit.</summary>
    private static int? AttackedProvince(GameCommand command) => command.Assignments is { Count: 1 } assignments ? assignments.Values.Single() : command.Province;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d));
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> Provinces(params int[] indices) => look => indices.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> Both(Func<TutorialLookup, IEnumerable<string>> first, Func<TutorialLookup, IEnumerable<string>> second) => look => first(look).Concat(second(look));

    /// <summary>True when the command pays with exactly these Gold sources and no others.</summary>
    private static bool PaidBy(TutorialLookup look, GameCommand command, string definition) =>
        command.SourceIds is { Length: > 0 } sources && sources.All(id => look.Is(id, definition)) && sources.Distinct().Count() == sources.Length;

    private static bool PaidByStronghold(TutorialLookup look, GameCommand command) => command.SourceIds is [var only] && only == look.Stronghold(TutorialRunner.Seat);

    private static TutorialStep Say(string id, string title, string body, Func<TutorialLookup, IEnumerable<string>>? highlight, string button, Action<MatchEngine>? then) =>
        new() { Id = id, Title = title, Body = body, Button = button, Highlight = highlight, OnContinue = then };

    private sealed record Mechanics(Func<TutorialLookup, GameCommand, bool> Allow, Func<TutorialLookup, GameCommand, bool>? Accept = null, string? Hint = null,
        Func<TutorialLookup, IEnumerable<string>>? Targets = null, int[]? OnlyProvinces = null);

    /// <summary>The cards a note points at, by step id. A step with no entry points at nothing.</summary>
    private static readonly Dictionary<string, Func<TutorialLookup, IEnumerable<string>>> Pointers = new()
    {
        ["stronghold"] = Own(TutorialCards.YourStronghold),
        ["starting"] = Both(Own(TutorialCards.YourStronghold, TutorialCards.BorderKeep), Provinces(0, 1, 2, 3)),
        ["border-keep"] = Own(TutorialCards.BorderKeep),
        ["bamboo"] = Foe(TutorialCards.BambooHarvesters),
        ["provinces"] = Provinces(0, 1, 2, 3),
        ["event"] = Provinces(0),
        ["what-you-can-buy"] = Provinces(1, 2, 3),
        ["action-phase"] = Own(TutorialCards.BorderKeep),
        ["border-refresh"] = Own(TutorialCards.BorderKeep),
        ["refreshed"] = Provinces(1, 2, 3),
        ["dynasty-phase"] = Provinces(1, 2, 3),
        ["buy-farm"] = Provinces(1),
        ["personality-anatomy"] = Provinces(2),
        ["keywords-trait-ability"] = Provinces(2),
        ["buy-uehara"] = Both(Provinces(2), Own(TutorialCards.YourStronghold)),
        ["buy-mine"] = Both(Provinces(3), Own(TutorialCards.BorderKeep)),
        ["hand"] = Hand(TutorialCards.TellTheTale, TutorialCards.Truce, TutorialCards.VeteranSkirmishers, TutorialCards.LightArmor, TutorialCards.EmbersFinalFire, TutorialCards.RingOfFire),
        ["strategy-limited"] = Hand(TutorialCards.TellTheTale),
        ["strategy-reaction"] = Hand(TutorialCards.Truce),
        ["follower"] = Hand(TutorialCards.VeteranSkirmishers),
        ["item"] = Hand(TutorialCards.LightArmor),
        ["spell"] = Hand(TutorialCards.EmbersFinalFire),
        ["ring"] = Hand(TutorialCards.RingOfFire),
        ["second-turn"] = Both(Provinces(0), Own(TutorialCards.MegumisGuidance)),
        ["tell-tale"] = Hand(TutorialCards.TellTheTale),
        ["attachments"] = Own(TutorialCards.AkodoUehara),
        ["equip-follower"] = Both(Hand(TutorialCards.VeteranSkirmishers), Own(TutorialCards.BorderKeep)),
        ["equip-item"] = Both(Hand(TutorialCards.LightArmor), Own(TutorialCards.CopperMine)),
        ["assign-attacker"] = Both(Own(TutorialCards.AkodoUehara), FoeProvinces(0)),
        ["pick-battle"] = FoeProvinces(0),
        ["reading-battle"] = Both(Own(TutorialCards.AkodoUehara), Foe(TutorialCards.DojiAtsumichi)),
        ["defensive-stance"] = Hand(TutorialCards.DefensiveStance),
        ["tactician"] = Both(Own(TutorialCards.AkodoUehara), Hand(TutorialCards.Truce)),
        ["ring-reaction"] = Hand(TutorialCards.RingOfFire),
    };

    /// <summary>What a note's last button does to the table, by step id and beat letter.</summary>
    private static readonly Dictionary<string, Action<MatchEngine>> ButtonEffects = new()
    {
        ["turn-phases"] = engine => engine.TutorialBeginTurn(),
        ["straighten-events"] = engine => engine.TutorialResumeEvents(),
        ["forgotten-draw"] = engine => engine.TutorialDraw(TutorialRunner.Seat, 6),
    };

    /// <summary>The one move each action step waits for, by its key.</summary>
    private static readonly Dictionary<string, Mechanics> Moves = new()
    {
        ["border-refresh"] = new(Move("useAbility", "border-first"),
            Accept: (look, command) => command.CardIds is { Length: 3 } ids && new[] { 1, 2, 3 }.All(i => ids.Contains(look.Province(TutorialRunner.Seat, i))),
            Hint: "Choose the three face-up province cards (provinces two to four) and nothing else.",
            Targets: look => new[] { 1, 2, 3 }.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>()),
        ["pass-action"] = new(Move("pass")),
        ["decline-attack"] = new(Move("pass")),
        ["buy-farm"] = new(Move("purchase", card: TutorialCards.SmallFarm)),
        ["buy-uehara"] = new(Move("purchase", card: TutorialCards.AkodoUehara),
            Accept: (look, command) => command.ClanDiscount && PaidByStronghold(look, command), Hint: "Buy Akodo Uehara with the Clan Discount, paid by your Stronghold alone."),
        ["buy-mine"] = new(Move("purchase", card: TutorialCards.CopperMine),
            Accept: (look, command) => PaidBy(look, command, TutorialCards.BorderKeep), Hint: "Buy Copper Mine, paid by your Border Keep alone."),
        ["end-dynasty"] = new(Move("endDynasty")),
        ["tell-tale"] = new(Move("useAbility", "tell-the-tale-honor")),
        ["equip-follower"] = new(Move("equip", card: TutorialCards.VeteranSkirmishers),
            Accept: (look, command) => PaidBy(look, command, TutorialCards.BorderKeep), Hint: "Pay for the Follower with your Border Keep."),
        ["equip-item"] = new(Move("equip", card: TutorialCards.LightArmor),
            Accept: (look, command) => PaidBy(look, command, TutorialCards.CopperMine), Hint: "Pay for the Light Armor with your Copper Mine."),
        ["pass-turn-two"] = new(Move("pass")),
        ["declare-attack"] = new(Move("declareAttack")),
        ["assign-attacker"] = new((_, command) => command.Type == "attack" && AttackedProvince(command) == 0, Hint: "Attack the opponent's first province.", OnlyProvinces: [0]),
        ["pick-battle"] = new((_, command) => command is { Type: "selectBattle", Province: 0 }),
        ["pass-for-bot"] = new(Move("pass")),
        ["defensive-stance"] = new(Move("useAbility", "defensive-stance", TutorialCards.DefensiveStance),
            Accept: (look, command) => look.Is(command.TargetId ?? command.PerformerId, TutorialCards.AkodoUehara), Hint: "Play Defensive Stance on Akodo Uehara."),
        ["tactician"] = new(Move("useAbility", "tactical-advantage"),
            Accept: (look, command) => look.Is(command.TargetId, TutorialCards.Truce) && look.Is(command.PerformerId, TutorialCards.AkodoUehara), Hint: "Use Akodo Uehara's Tactician ability and discard Truce."),
        ["pass-battle"] = new(Move("pass")),
        ["ring-reaction"] = new(Move("useAbility", "ring-of-fire-recruit")),
    };

    /// <summary>What to click, step by step: the whole of what is needed for the lesson to move on. A test requires one for every action step.</summary>
    internal static readonly Dictionary<string, string> Instructions = TutorialLessonOneText.Beats.Where(b => b.IsMove).ToDictionary(b => b.Key, b => b.Instruction);

    public static readonly TutorialStep[] Steps = Build();

    private static TutorialStep[] Build()
    {
        var steps = new List<TutorialStep>();
        foreach (var beat in TutorialLessonOneText.Beats)
        {
            var baseId = beat.IsMove ? beat.Key : beat.Step + (beat.Letter.Length > 0 ? "-" + beat.Letter : "");
            Func<TutorialLookup, IEnumerable<string>>? highlight = null;
            if (Pointers.TryGetValue(beat.IsMove ? beat.Key : baseId, out var own)) highlight = own;
            else if (!beat.IsMove && Pointers.TryGetValue(beat.Step, out var byStep)) highlight = byStep;
            for (var page = 0; page < beat.Pages.Length; page++)
            {
                var last = page == beat.Pages.Length - 1;
                var id = beat.IsMove ? (last ? beat.Key : $"{beat.Key}-note{page + 1}") : page == 0 ? baseId : $"{baseId}-p{page + 1}"; // a move's own id is its key, on its last page
                if (beat.IsMove && last)
                {
                    var m = Moves[beat.Key];
                    steps.Add(new()
                    {
                        Id = beat.Key, Title = beat.Title, Body = beat.Pages[page], Instruction = beat.Instruction, Allow = m.Allow, Accept = m.Accept, Highlight = highlight, Targets = m.Targets,
                        OnlyProvinces = m.OnlyProvinces, RefusalHint = m.Hint ?? "Do the highlighted move the lesson asks for.",
                    });
                    continue;
                }
                var button = last && !beat.IsMove ? beat.Button : Continue;
                Action<MatchEngine>? then = last && !beat.IsMove && ButtonEffects.TryGetValue(beat.Step, out var effect) ? effect : null;
                steps.Add(Say(id, beat.Title, beat.Pages[page], highlight, button, then));
            }
        }
        return [.. steps];
    }
}
