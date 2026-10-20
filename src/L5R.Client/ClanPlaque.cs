using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Approved Placard A artwork with live public state; no hand-limit rules in the client.</summary>
public partial class ClanPlaque : Control
{
    public const float DesignWidth = 310;
    public const float DesignHeight = 108;
    public bool Acting { get; set; }
    public bool TurnOwner { get; }
    public bool ReducedMotion { get; set; }
    public string TurnText { get; }
    public string ActionText { get; }
    public string HandText { get; }
    public string HandHelp { get; }
    public Button HandRegion { get; }
    /// <summary>The emblem button: opens the Dead cards, and chooses the player while a "choose a player" action is being drafted (issue #960).</summary>
    public Button Hit { get; private set; } = null!;
    public string HitTooltip { get; private set; } = "";
    private readonly Control _design;
    private static FontFile? _bodyFace, _headingFace;
    private static FontFile CrispFace(string path)
    {
        // Local copies keep scaled placards crisp without changing other menus' font resources.
        var face = (FontFile)GD.Load<FontFile>(path).Duplicate();
        face.MultichannelSignedDistanceField = true; face.MsdfSize = 96; face.MsdfPixelRange = 12;
        return face;
    }
    internal static Font BodyFace => _bodyFace ??= CrispFace("res://Art/Fonts/Caudex-Regular.ttf");
    internal static Texture2D FrameTexture => GD.Load<Texture2D>("res://Art/ClanPlacard/frame.png");

    public ClanPlaque(string clan, int honor, int gold, bool favor, Action inspect, string action = "", string turn = "",
        bool acting = false, bool turnOwner = false, int? handCount = null, HandLimitView? handLimit = null)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Acting = acting; TurnOwner = turnOwner; TurnText = turn; ActionText = action;
        HandText = $"{handCount?.ToString() ?? "—"} / {handLimit?.Maximum.ToString() ?? "—"}";
        HandHelp = DescribeHand(handCount, handLimit);
        Size = new Vector2(DesignWidth, DesignHeight);
        _design = new Control { Size = Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_design);
        AddSprite(FrameTexture, new Rect2(0, 0, DesignWidth, DesignHeight));
        var emblem = Emblem(clan, new Vector2(94, 94)); emblem.Position = new Vector2(7, 7); _design.AddChild(emblem);
        var title = Palette.Text(clan.ToUpperInvariant(), 22, Palette.Paper, true);
        title.AddThemeFontOverride("font", new FontVariation
        {
            BaseFont = _headingFace ??= CrispFace("res://Art/Fonts/WeatheredQuill-Regular.ttf"),
            SpacingGlyph = -1,
            SpacingTop = -15,
            SpacingBottom = -11
        });
        title.Position = new Vector2(113, 3); title.Size = new Vector2(180, 29);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; _design.AddChild(title);
        var turnCue = Text(turn, 11, turnOwner ? new Color("a7d3d5") : Palette.PaperDim, new Rect2(115, 36, 178, 16));
        turnCue.TooltipText = turn;
        var name = Text(action, 14, Palette.PaperDim, new Rect2(114, 54, 180, 20));
        name.ClipText = true; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; name.Size = new Vector2(180, 20);
        name.TooltipText = action; name.MouseFilter = MouseFilterEnum.Stop;
        // Labels can expand to their font's minimum width before the first layout pass.
        // A bounded viewport keeps untrusted/long player names inside the frame regardless.
        var nameClip = new Control
        {
            Position = new Vector2(114, 54),
            Size = new Vector2(180, 20),
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _design.RemoveChild(name); _design.AddChild(nameClip); nameClip.AddChild(name); name.Position = Vector2.Zero;
        AddIcon(0, new Rect2(108, 74, 28, 23)); _honorFan = (TextureRect)_design.GetChild(_design.GetChildCount() - 1); _honorFan.PivotOffset = _honorFan.Size / 2;
        _honorValue = Text(honor.ToString(), honor is > 99 or < -9 ? 18 : 21, Palette.Paper, new Rect2(140, 70, 31, 28)); _honor = honor;
        AddSprite(StrongholdCoin, new Rect2(179, 69, 28, 28)); // the Gold Production coin printed on strongholds, as in the gold flow
        var goldValue = Text(gold.ToString(), gold > 99 ? 15 : 21, new Color("17110b"), new Rect2(178, 68, 30, 29));
        goldValue.HorizontalAlignment = HorizontalAlignment.Center;
        AddIcon(2, new Rect2(225, 72, 26, 24));
        var countSize = 21;
        while (countSize > 12 && BodyFace.GetStringSize(HandText, fontSize: countSize).X > 47) countSize--;
        var hand = new RichTextLabel
        {
            Position = new Vector2(256, 70),
            Size = new Vector2(48, 28),
            BbcodeEnabled = true,
            ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore,
            Text = $"{handCount?.ToString() ?? "—"} / [color=#edc46f]{handLimit?.Maximum.ToString() ?? "—"}[/color]"
        };
        hand.AddThemeFontOverride("normal_font", BodyFace); hand.AddThemeFontSizeOverride("normal_font_size", countSize);
        hand.AddThemeColorOverride("default_color", Palette.Paper); _design.AddChild(hand);
        AddStatRegion(new Rect2(106, 67, 64, 30), "Family Honor", $"{honor} Family Honor");
        AddStatRegion(new Rect2(175, 66, 38, 31), "Available Gold", $"{gold} available Gold production\nProduced when paying, not a saved balance.");
        HandRegion = AddStatRegion(new Rect2(222, 66, 82, 31), "Hand limit", HandHelp);
        var hit = new TooltipButton
        {
            Flat = true,
            Text = "",
            Position = new Vector2(9, 10),
            Size = new Vector2(86, 87),
            TooltipText = clan + " · inspect Dead cards",
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        foreach (var state in new[] { "normal", "pressed", "hover", "focus" })
            hit.AddThemeStyleboxOverride(state, Palette.Box(Colors.Transparent, state is "hover" or "focus" ? Palette.Gold : null, 0, 40));
        hit.Pressed += inspect; _design.AddChild(hit); Hit = hit; HitTooltip = hit.TooltipText;
        if (favor)
        {
            var mark = Text("◆", 10, Palette.Gold, new Rect2(290, 15, 12, 15));
            mark.MouseFilter = MouseFilterEnum.Stop; mark.TooltipText = "Imperial Favor";
        }
        _design.AddChild(new PlacardAccents(turnOwner) { MouseFilter = MouseFilterEnum.Ignore, TurnCue = turn });
        Resized += FitDesign;
    }
    public override void _Ready() => FitDesign();

    private readonly TextureRect _honorFan;
    private readonly Label _honorValue;
    private readonly int _honor;
    /// <summary>The Family Honor fan and count, on screen; the tutorial lights it up.</summary>
    public Rect2 HonorGlobalRect => _honorFan.GetGlobalRect().Merge(_honorValue.GetGlobalRect());
    /// <summary>Shows a different Honor for a moment (the tutorial counting toward an Honor Victory); null puts the real value back. Display only.</summary>
    public void ShowHonor(int? shown, float fanScale = 1)
    {
        var value = shown ?? _honor;
        _honorValue.Text = value.ToString(); _honorValue.AddThemeFontSizeOverride("font_size", value is > 99 or < -9 ? 18 : 21);
        _honorFan.Scale = Vector2.One * fanScale;
    }
    private void FitDesign()
    {
        var scale = Math.Min(Size.X / DesignWidth, Size.Y / DesignHeight);
        _design.Scale = Vector2.One * scale;
        _design.Position = (Size - new Vector2(DesignWidth, DesignHeight) * scale) * .5f;
    }
    public static string DescribeHand(int? count, HandLimitView? limit)
    {
        var current = count.HasValue ? $"{count} cards in hand" : "Hand count unavailable";
        if (limit is null) return current + " · limit unavailable\nThe current server snapshot does not include a hand limit.";
        var lines = new List<string> { $"{current} · limit {limit.Maximum}", "The limit is checked during the End Phase." };
        if (limit.Modifiers.Length > 0)
        {
            lines.Add($"Base {limit.Base}");
            lines.AddRange(limit.Modifiers.Select(m => $"{m.Amount:+0;-0;0} · {m.CardName}"));
        }
        return string.Join("\n", lines);
    }
    private Label Text(string value, int size, Color color, Rect2 rect)
    {
        var label = Palette.Text(value, size, color); label.AddThemeFontOverride("font", BodyFace);
        label.Position = rect.Position; label.Size = rect.Size; _design.AddChild(label); return label;
    }
    private static readonly Texture2D StrongholdCoin = GD.Load<Texture2D>("res://Art/Stronghold/stronghold-coin-brass.png");
    private void AddSprite(Texture2D texture, Rect2 rect, Material? material = null)
        => _design.AddChild(new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = rect.Position,
            Size = rect.Size,
            Material = material,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps
        });
    private void AddIcon(int cell, Rect2 rect)
    {
        var atlas = GD.Load<Texture2D>("res://Art/ClanPlacard/stats.png");
        var cellWidth = atlas.GetWidth() / 3f;
        var texture = new AtlasTexture
        {
            Atlas = atlas,
            Region = new Rect2(cellWidth * cell + cellWidth * .10f, atlas.GetHeight() * .10f, cellWidth * .80f, atlas.GetHeight() * .80f)
        };
        AddSprite(texture, rect, new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/ClanPlacard/black-cutout.gdshader") });
    }
    private Button AddStatRegion(Rect2 rect, string heading, string help)
    {
        var target = new PlacardStatRegion(heading)
        {
            Position = rect.Position,
            Size = rect.Size,
            Flat = true,
            TooltipText = help,
            FocusMode = FocusModeEnum.All
        };
        foreach (var state in new[] { "normal", "pressed", "hover", "focus" })
            target.AddThemeStyleboxOverride(state, Palette.Box(Colors.Transparent, state == "focus" ? Palette.Gold : null, 0, 2));
        _design.AddChild(target); return target;
    }
    public static Control MakeStatTooltip(string heading, string text, float scale = 1)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && BodyFace.GetStringSize(candidate, fontSize: 18).X > 356)
                { lines.Add(line); line = word; }
                else line = candidate;
            }
            lines.Add(line);
        }
        var design = new Vector2(410, 84 + lines.Count * 25);
        var shell = new Control { CustomMinimumSize = design * scale, MouseFilter = MouseFilterEnum.Ignore };
        var panel = new Control { Size = design, Scale = Vector2.One * scale, MouseFilter = MouseFilterEnum.Ignore };
        shell.AddChild(panel);
        panel.AddChild(new NinePatchRect
        {
            Texture = FrameTexture,
            PatchMarginLeft = 96,
            PatchMarginRight = 96,
            PatchMarginTop = 96,
            PatchMarginBottom = 96,
            Scale = Vector2.One * .25f,
            Size = design * 4,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps
        });
        var title = Palette.Text(heading, 23, Palette.Paper); title.AddThemeFontOverride("font", BodyFace);
        title.Position = new Vector2(25, 17); panel.AddChild(title);
        var copy = Palette.Wrapped(string.Join("\n", lines), 18, Palette.Paper); copy.AddThemeFontOverride("font", BodyFace);
        copy.Position = new Vector2(25, 53); copy.Size = design - new Vector2(50, 70); panel.AddChild(copy);
        return shell;
    }
    private partial class PlacardStatRegion(string heading) : Button
    {
        public override Control _MakeCustomTooltip(string forText)
            => MakeStatTooltip(heading, forText, GetTree().Root.GetVisibleRect().Size.Y / 1080f);
    }
    private partial class PlacardAccents(bool turnOwner) : Control
    {
        public override void _Draw()
        {
            foreach (var x in new[] { 170f, 216f })
                DrawLine(new Vector2(x, 73), new Vector2(x, 94), Palette.Gold with { A = .65f }, 1, true);
            if (!turnOwner) return;
            DrawLine(new Vector2(108, 43), new Vector2(112, 43), Palette.Crane with { A = .8f }, 1, true);
            var cueWidth = BodyFace.GetStringSize(TurnCue, fontSize: 11).X;
            DrawLine(new Vector2(Math.Min(290, 120 + cueWidth), 43), new Vector2(Math.Min(297, 144 + cueWidth), 43), Palette.Crane with { A = .8f }, 1, true);
        }
        public string TurnCue { get; set; } = "YOUR TURN";
    }
    public static TextureRect Emblem(string clan, Vector2 size)
    {
        var emblemRegion = clan switch
        {
            "Lion" => new Rect2(114, 225, 224, 218),
            "Crane" => new Rect2(574, 220, 225, 223),
            "Phoenix" => new Rect2(0, 0, 222, 222),
            "Scorpion" => new Rect2(234, 0, 219, 222),
            "Crab" => new Rect2(465, 0, 218, 222),
            "Unicorn" => new Rect2(689, 0, 220, 222),
            "Dragon" => new Rect2(340, 220, 224, 223),
            _ => new Rect2(114, 225, 224, 218)
        };
        var extra = clan is "Mantis" or "Spider" or "Imperial" ? GD.Load<Texture2D>("res://Art/clan-" + clan.ToLowerInvariant() + ".png") : null;
        return Decal(extra is null ? "clan-reference.png" : "clan-" + clan.ToLowerInvariant() + ".png", extra is null ? emblemRegion : new Rect2(Vector2.Zero, extra.GetSize()), Vector2.Zero, size, false);
    }
    private static TextureRect Decal(string file, Rect2 region, Vector2 position, Vector2 size, bool coin)
    {
        var sprite = new TextureRect
        {
            Texture = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://Art/" + file), Region = region },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = position,
            Size = size,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps
        };
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/aged-emblem.gdshader") };
        material.SetShaderParameter("decal_size", size); material.SetShaderParameter("white_cutout", coin); material.SetShaderParameter("saturation", coin ? .80f : .53f);
        sprite.Material = material; return sprite;
    }
}
