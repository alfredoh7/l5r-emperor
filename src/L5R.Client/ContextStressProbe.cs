using Godot;
using L5R.AI;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

/// <summary>
/// Issue #1103: drives the real Main through rapid switches between the menu, Practice, the deck builder, the library and the tutorial, while the training bot plays both seats,
/// at dwells from the same frame to 30 s. Every step is logged and the seed replays a run: -- --seed=N --steps=N [--only=a,b] [--dwell=s] [--output=dir].
/// The targeted cases (duel-leave-practice, online-then-practice) assert what they reproduce; anything else fails through the logged errors the check script greps for.
/// </summary>
public partial class ContextStressProbe : Node
{
    private Main _main = null!;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private object? Call(string key, params object?[] values) => typeof(Main).GetMethod(key, Private)!.Invoke(_main, values);
    private T Field<T>(string key) => (T)typeof(Main).GetField(key, Private)!.GetValue(_main)!;
    private static IEnumerable<T> Descendants<T>(Node parent) where T : Node => parent.GetChildren().SelectMany(n => (n is T t ? new[] { t } : []).Concat(Descendants<T>(n)));
    private MatchSession Session => Field<MatchSession>("_session");
    private bool DuelOpen => Field<object?>("_matchDuel") is not null || Field<bool>("_matchDuelPreparing");
    private Random _rng = new();
    private double[] _dwells = [0, -1, .05, .15, .4, 1, 2.5, 6, 15, 30];
    private string _output = "";
    private int _played, _failures;
    private double _playClock;

    public override async void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        string? Arg(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?[(name.Length + 3)..];
        var seed = int.Parse(Arg("seed") ?? "1"); var steps = int.Parse(Arg("steps") ?? "40");
        _rng = new Random(seed);
        if (Arg("dwell") is { } dwell) _dwells = [double.Parse(dwell, System.Globalization.CultureInfo.InvariantCulture)];
        _output = Arg("output") ?? ProjectSettings.GlobalizePath("user://context-stress"); Directory.CreateDirectory(_output);
        _main = (Main)GD.Load<PackedScene>("res://Main.tscn").Instantiate(); AddChild(_main);
        await Dwell(1);
        string[] actions = Arg("only")?.Split(',') ?? ["practice", "practice", "menu", "resume", "deckbuilder", "library", "practice-edit", "tutorial", "leave-tutorial", "table-library"];
        for (var i = 0; i < steps; i++)
        {
            var action = actions[_rng.Next(actions.Length)];
            var wait = _dwells[_rng.Next(_dwells.Length)];
            GD.Print($"STRESS_STEP {i} {action} then {(wait < 0 ? "1 frame" : wait + "s")} gen={Session.Generation} {Describe()} played={_played} duel={DuelOpen}");
            try { await Do(action); }
            catch (Exception e) { Fail($"step {i} {action}: {(e is TargetInvocationException t ? t.InnerException : e)}"); }
            await Dwell(wait);
        }
        GD.Print(_failures == 0 ? $"CONTEXT_STRESS_PASS seed={seed} steps={steps} played={_played}" : $"CONTEXT_STRESS_FAIL seed={seed} failures={_failures}");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private string Describe() => Session.Snapshot is { } s ? $"v{s.View.Version} {s.View.Phase} t{s.View.TurnNumber}" : "no-match";
    private void Fail(string message) { _failures++; GD.PushError("CONTEXT_STRESS " + message); }
    private void Require(bool test, string message) { if (!test) Fail(message); }

    /// <summary>Plays the local seat with the training bot through Main's own Submit, so presentations, flights and duels run as they would for a person.</summary>
    public override void _Process(double delta)
    {
        if (_main is null || !IsInstanceValid(_main) || Session.Snapshot is not { } snap || Session.Tutorial is not null || Session.Online || snap.View.WinnerId is not null) return;
        if (!Field<Control>("_root").Visible) return;
        _playClock += delta; if (_playClock < .35) return; _playClock = 0;
        if (snap.View.PriorityPlayerId != snap.View.ViewerId || snap.Actions.Length == 0) return;
        var command = snap.View.PendingDuel is not null ? DuelOpponent.Choose(snap.View, snap.Actions) : TrainingOpponent.Choose(snap.View, snap.Actions).Command;
        if (command is null) return;
        _played++; _ = (Task)Call("Submit", command)!;
    }

    private async Task Dwell(double seconds)
    {
        if (seconds == 0) return;
        if (seconds < 0) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); return; }
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
    private void AcceptConfirm() { foreach (var dialog in Descendants<ConfirmationDialog>(_main).Where(d => IsInstanceValid(d) && d.Visible)) dialog.EmitSignal(AcceptDialog.SignalName.Confirmed); }
    private static void Escape()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false });
    }
    private (string Name, Deck Deck) RandomDeck()
    {
        var decks = PreloadedDecks.Shipped();
        if (_rng.Next(3) == 0 || decks.Length == 0) { var clan = _rng.Next(2) == 0 ? "Crane" : "Lion"; return (clan + " preset", CustomPractice.Preset(clan)); }
        var pick = decks[_rng.Next(decks.Length)]; return (pick.Name, DeckFormat.Parse(pick.Text));
    }
    /// <summary>Play → Practice → Start, confirming the replacement of a running match.</summary>
    private async Task StartPractice(Deck your, Deck theirs, bool sameFrame)
    {
        Call("ShowPracticeSetup"); await Dwell(-1);
        if (Descendants<PracticeSetupScreen>(_main).FirstOrDefault() is not { } screen) { GD.Print("STRESS_NOTE no practice screen"); return; }
        screen.StartMatch(your, theirs);
        if (!sameFrame) await Dwell(-1);
        AcceptConfirm();
    }
    private void Screenshot(string name) => GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output, name + ".png"));

    private async Task Do(string action)
    {
        switch (action)
        {
            case "practice":
                {
                    var your = RandomDeck(); var theirs = RandomDeck();
                    GD.Print($"STRESS_DECKS {your.Name} vs {theirs.Name}");
                    await StartPractice(your.Deck, theirs.Deck, _rng.Next(2) == 0);
                    break;
                }
            case "menu": Call("ShowMainMenu"); break;
            case "resume": if (Session.Snapshot is not null) Call("EnterTable"); break;
            case "deckbuilder":
                Call("ShowMainMenu"); DeckBuilderDialog.Show(_main);
                await Dwell(_dwells[_rng.Next(Math.Min(5, _dwells.Length))]); Escape(); break;
            case "library":
                Call("ShowMainMenu"); LibraryDialog.Show(_main);
                await Dwell(_dwells[_rng.Next(Math.Min(5, _dwells.Length))]); Escape(); break;
            case "table-library":
                if (Session.Snapshot is not null) Call("EnterTable");
                LibraryDialog.Show(_main); await Dwell(_dwells[_rng.Next(Math.Min(5, _dwells.Length))]);
                foreach (var button in Descendants<Button>(GetTree().Root).Where(b => b.Text == "Deck Builder" && b.IsVisibleInTree()).Take(1)) button.EmitSignal(BaseButton.SignalName.Pressed);
                await Dwell(_dwells[_rng.Next(Math.Min(5, _dwells.Length))]); Escape(); await Dwell(-1); Escape(); break;
            case "practice-edit":
                {
                    Call("ShowPracticeSetup"); await Dwell(-1);
                    if (Descendants<PracticeSetupScreen>(_main).FirstOrDefault() is not { } screen) return;
                    typeof(PracticeSetupScreen).GetMethod("Edit", Private)!.Invoke(screen, [null]);
                    await Dwell(_dwells[_rng.Next(Math.Min(5, _dwells.Length))]); Escape(); await Dwell(-1);
                    typeof(PracticeSetupScreen).GetMethod("Start", Private)!.Invoke(screen, []);
                    await Dwell(-1); AcceptConfirm(); break;
                }
            case "tutorial": Call("StartTutorial", new object?[] { null }); break;
            case "leave-tutorial": if (Session.Tutorial is not null) Call("LeaveTutorial"); break;
            case "duel-leave-practice":
                {
                    // A duel kept for Resume must not come back over the next Practice (it reopened on "Opponent's move" and never returned).
                    Session.StartDuelDemo("steel", 205, 0);
                    var flow = typeof(Main).GetField("_matchDuelFlow", Private)!.GetValue(_main)!;
                    flow.GetType().GetMethod("Observe")!.Invoke(flow, [Session.Snapshot]);
                    Call("EnterTable"); _ = Session.BeginDuelDemo();
                    for (var t = 0; t < 200 && Field<object?>("_matchDuel") is null; t++) await Dwell(.05);
                    Require(Field<object?>("_matchDuel") is not null, "the prepared duel never opened");
                    await Dwell(Math.Min(6, _dwells[_rng.Next(_dwells.Length)]));
                    Call("ConfirmLeaveMatchDuel"); await Dwell(-1); AcceptConfirm(); await Dwell(-1);
                    Require(!DuelOpen, "leaving the duel did not close it");
                    await StartPractice(CustomPractice.Preset("Crane"), CustomPractice.Preset("Lion"), false);
                    await Dwell(8);
                    GD.Print($"STRESS_AFTER_NEW_PRACTICE duelOpen={DuelOpen} {Describe()} played={_played}");
                    Require(!DuelOpen, "the previous match's duel reopened over the new Practice");
                    Require(Session.Snapshot is { View.Version: > 2 }, $"the new Practice did not progress ({Describe()})");
                    if (_failures > 0) Screenshot("duel-leave-practice-failure");
                    break;
                }
            case "online-then-practice":
                {
                    // Enter a private match, close the dialog and start Practice before the server answers: the Practice must survive and nothing may touch the freed dialog.
                    if (OS.GetEnvironment("L5R_STRESS_SERVER") is not { Length: > 0 } server) { GD.Print("STRESS_NOTE online-then-practice needs L5R_STRESS_SERVER"); return; }
                    Call("ShowOnline"); await Dwell(-1);
                    var dialog = Descendants<AcceptDialog>(_main).Last(d => d.Title == "Private match");
                    Descendants<LineEdit>(dialog).First().Text = server;
                    Descendants<Button>(dialog).First(b => b.Text == "Enter private match").EmitSignal(BaseButton.SignalName.Pressed);
                    dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
                    await StartPractice(CustomPractice.Preset("Crane"), CustomPractice.Preset("Lion"), true);
                    await Dwell(6);
                    GD.Print($"STRESS_AFTER_CONNECT online={Session.Online} {Describe()} played={_played}");
                    Require(!Session.Online && Session.Snapshot is { View.Version: > 0 }, $"the late connect replaced the Practice ({Describe()}, online={Session.Online})");
                    break;
                }
        }
    }
}
