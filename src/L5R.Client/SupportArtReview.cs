using Godot;

namespace L5R.Client;

// A native art-review plate, not a gameplay screenshot. Each detail is an unchanged
// viewport into the approved source or a real native capture, at matching widths.
public partial class SupportArtReview(string output) : Control
{
    private readonly Dictionary<string, Texture2D> _images = [];
    public override async void _Ready()
    {
        Size = new Vector2(1920, 1080); MouseFilter = MouseFilterEnum.Ignore;
        foreach (var name in new[] { "approved-a", "approved-b", "approved-c", "default", "default-region-detail", "default-celestial-detail" })
        {
            var relative = name.StartsWith("approved") ? "assets/ui/regions-celestials/" : "artifacts/checks/support-cards/";
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../..", relative, name + ".png"));
            using var source = Image.LoadFromFile(path);
            _images[name] = ImageTexture.CreateFromImage(source);
        }
        QueueRedraw();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var capture = GetViewport().GetTexture().GetImage();
        var result = capture.SavePng(output);
        GD.Print("SUPPORT_ART_COMPARISON: " + result + "; approved source and native captures, matching component widths; no image retouching");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
    public override void _Draw()
    {
        if (_images.Count == 0) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("10171e"));
        void Text(float x, float y, string value, int size = 22) => DrawString(Palette.Body, new(x, y), value, fontSize: size, modulate: new Color("e7d7b9"));
        void Detail(string name, Rect2 source, float x, float y, float width = 260)
            => DrawTextureRectRegion(_images[name], new Rect2(x, y, width, source.Size.Y * width / source.Size.X), source);
        Text(50, 60, "APPROVED A + B + C / NATIVE GODOT COMPARISON", 32);
        Text(50, 104, "Unchanged source and game captures at matching component widths. Perspective and lighting remain native.");
        Text(50, 163, "A · PAIRED REGIONS", 25); Text(675, 163, "B · CELESTIAL FRAME", 25); Text(1300, 163, "C · INSPECTION", 25);
        foreach (var x in new[] { 50, 675, 1300 }) { Text(x, 198, "Approved source", 19); Text(x + 290, 198, "Native game", 19); }
        Detail("approved-a", new(445, 399, 219, 128), 50, 217);
        Detail("default-region-detail", new(628, 358, 347, 190), 340, 217);
        Detail("approved-b", new(1080, 304, 147, 122), 675, 217);
        Detail("default-celestial-detail", new(748, 313, 145, 117), 965, 217);
        Detail("approved-c", new(1230, 103, 290, 508), 1300, 217);
        Detail("default-celestial-detail", new(1515, 133, 366, 603), 1590, 217);
        Text(50, 482, "NATIVE PLAYABLE SCALE · BOTH OWNERS", 23);
        DrawTextureRect(_images["default"], new Rect2(50, 502, 1000, 562.5f), false);
        Text(1300, 730, "A reuses the original ornamental pixels.");
        Text(1300, 771, "B is a derived transparent production asset.");
        Text(1300, 812, "C uses original scans and printed source text.");
        Text(1300, 874, "The selected composition and behavior are retained.", 19);
        Text(1300, 912, "Small B ornament/texture details are derived.", 19);
        Text(1300, 950, "Whole-screen pixel equivalence is not claimed.", 19);
        Text(1300, 1023, "Art-review plate — see full native captures for play.", 18);
    }
}
