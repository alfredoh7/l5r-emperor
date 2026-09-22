using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Real permanent-grant action followed by native owner/opponent mouseover checks (#386).</summary>
public partial class AbilityGrantProbe : Control
{
    private MatchEngine _engine = null!;
    private string _receiver = "", _output = "";
    private CardTile _tile = null!;
    private Label _heading = null!;
    private Button _grant = null!;

    public override async void _Ready()
    {
        try
        {
            _output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--output="))?[9..]
                ?? ProjectSettings.GlobalizePath("res://../../artifacts/ability-grants");
            Directory.CreateDirectory(_output);
            Theme = Palette.MakeTheme();
            AddChild(new ColorRect { Color = new Color("101c25"), Size = new Vector2(1600, 1000), MouseFilter = MouseFilterEnum.Ignore });
            _engine = MatchEngine.CreateAbilityGrantShowcase();
            _receiver = _engine.State.Cards.Values.First(c => c.DefinitionId == "oracle-l5r-8550").Id;
            _heading = Palette.Wrapped("Spirit of the Monk · permanent ability grant", 26); _heading.Position = new(35, 25); _heading.Size = new(1100, 70); AddChild(_heading);
            _grant = new Button { Text = "Play Spirit of the Monk on Togashi Meiyu", Position = new(35, 105), Size = new(530, 45) };
            _grant.Pressed += () =>
            {
                var action = _engine.QueryActions(0).Single(a => a.Enabled && a.Command?.AbilityId == "spirit-of-the-monk" && a.Command.PerformerId == _receiver);
                var result = _engine.Execute(action.Command!);
                if (!result.Accepted) throw new Exception(result.Error);
                _grant.Disabled = true; ShowCard(0);
            };
            AddChild(_grant); ShowCard(0);
            await Wait(.7);
            var point = _grant.GetGlobalRect().GetCenter();
            Move(point);
            GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await Wait(.3);
            if (_engine.State.Cards[_receiver].GrantedAbilities.Count != 1) throw new Exception("Native click did not execute the grant");
            for (var viewer = 0; viewer < 2; viewer++)
            {
                Move(new(1100, 100)); await Wait(.2); ShowCard(viewer); await Wait(.3);
                CardTile.LastPreviewCardId = null; CardTile.LastPreviewControl = null;
                var version = _engine.Version;
                Move(_tile.GetGlobalRect().GetCenter()); await Wait(1.3);
                if (CardTile.LastPreviewCardId != _receiver || CardTile.LastPreviewControl is null) throw new Exception("Mouseover did not open the public card preview");
                var text = string.Join("\n", Walk(CardTile.LastPreviewControl).OfType<Label>().Select(l => l.Text));
                foreach (var required in new[] { "Granted ability", "Permanent", "without Armor", "Spirit of the Monk", "1 of 1 uses remaining" })
                    if (!text.Contains(required)) throw new Exception("Preview missing: " + required);
                if (_engine.Version != version) throw new Exception("Inspection changed the match");
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output, $"hover-player-{viewer}.png"));
            }
            // The unchanged duplicate has neither the ability nor a grant panel.
            var other = _engine.GetView(1).Players[0].Home.Single(c => c.Id != _receiver && c.Definition?.Id == "oracle-l5r-8550");
            if (other.GrantedAbilities is not null) throw new Exception("Grant leaked to another copy");
            GD.Print("ABILITY_GRANTS_PASS: native grant click; both players' actual hover text; source/duration/uses; inspection read-only; second copy unchanged.");
            if (!OS.GetCmdlineUserArgs().Contains("--stay")) GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
    private void ShowCard(int viewer)
    {
        if (_tile is not null) { RemoveChild(_tile); _tile.QueueFree(); }
        var card = _engine.GetView(viewer).Players[0].Home.Single(c => c.Id == _receiver);
        _tile = new CardTile(card, false, _ => { }, _ => { }) { Position = new(35, 195), Size = new(250, 355), FaceHeight = 340, ReducedMotion = true };
        AddChild(_tile);
        _heading.Text = $"Spirit of the Monk · {(viewer == 0 ? "owner" : "opponent")} mouseover";
    }
    private void Move(Vector2 point) => GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static IEnumerable<Node> Walk(Node node) { yield return node; foreach (var child in node.GetChildren()) foreach (var n in Walk(child)) yield return n; }
}
