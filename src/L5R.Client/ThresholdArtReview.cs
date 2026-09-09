using Godot;

namespace L5R.Client;

/// <summary>Native renderer comparison, explicitly separate from a gameplay screenshot.</summary>
public partial class ThresholdArtReview(string output) : Control
{
    public override async void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(1920, 1080);
        var ink = new InkMatSections(); AddChild(ink);
        ink.Sync([
            new("province", "Province 1", new Rect2(465, 175, 258, 355), ProvinceNumber: 1),
            new("selected", "Province 1", new Rect2(820, 175, 258, 355), Active:true, ProvinceNumber: 1),
            new("home", "Home", new Rect2(860, 660, 561, 136.4f)),
            new("holdings", "Holdings", new Rect2(1230, 175, 220, 355))
        ], true, "art-review");
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var result = image.SavePng(output);
        GD.Print($"ART_REFERENCE_CAPTURE: {result}; original concept regions and actual InkMatSections renderer; no gameplay fixture.");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("101a22"));
        var gold = new Color("e3c696");
        void Text(Vector2 p, string text, int size = 23) => DrawString(Palette.Body, p, text, fontSize:size, modulate:gold);
        Text(new Vector2(80,65), "APPROVED ART / NATIVE GODOT COMPARISON", 32);
        Text(new Vector2(80,105), "Actual source pixels, retained grain and silhouettes. Only the cloth background is removed.", 22);
        Text(new Vector2(80,155), "C · original crop");
        Text(new Vector2(465,155), "Godot · resting");
        Text(new Vector2(820,155), "Godot · selected");
        Text(new Vector2(1230,155), "A · compact Holdings");
        var c = GD.Load<Texture2D>("res://Art/mat-fortress-thresholds.png");
        DrawTextureRectRegion(c, new Rect2(80,175,258,355), new Rect2(416,351,258,355));
        Text(new Vector2(80,625), "A · original Home border");
        Text(new Vector2(860,625), "Godot · same border-mounted icon");
        var a = GD.Load<Texture2D>("res://Art/mat-border-crests.png");
        DrawTextureRectRegion(a, new Rect2(80,660,561,136.4f), new Rect2(434,115,1020,248));
        Text(new Vector2(80,900), "The gameplay camera projects these same textures onto the mat with the cards.");
        Text(new Vector2(80,939), "Plain edges extend with layout; decorative corners, icons and gate roofs retain their proportions.");
        Text(new Vector2(80,990), "Art inspection view — not a gameplay screenshot.", 19);
    }
}
