using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Inspectable public reminders. These never acquire a card id or target behavior.</summary>
public partial class HomeReminder : Button
{
    public int OwnerId { get; }
    public CardView? Effect { get; }
    public bool IsFavor => Effect is null;
    public bool Selected { get; set; }
    public string Clan { get; }
    private readonly Texture2D? _face;
    private readonly TextureRect _art;
    public static Font BodyFont => GD.Load<FontFile>("res://Art/Fonts/Caudex-Regular.ttf");
    public static Texture2D FavorFace(string clan)
    {
        var name = clan.ToLowerInvariant();
        var path = $"res://Art/PersistentHome/Favor/{name}.webp";
        return GD.Load<Texture2D>(ResourceLoader.Exists(path) ? path : "res://Art/PersistentHome/Favor/neutral.webp");
    }
    public HomeReminder(int ownerId, string clan, CardView? effect, Action inspect)
    {
        OwnerId = ownerId; Clan = clan; Effect = effect;
        _face = IsFavor ? FavorFace(clan) : null;
        Flat = true; Text = ""; MouseDefaultCursorShape = CursorShape.PointingHand;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        TooltipText = IsFavor ? $"{clan} · Imperial Favor\nPublic reminder · not a card in play.\nSelect to inspect the Favor actions currently offered by the game."
            : $"{effect!.Definition?.Name ?? "Ongoing effect"}\nEffect reminder · not a card in play.\n{effect.Definition?.Text}";
        _art = new TextureRect {
            Texture = GD.Load<Texture2D>(IsFavor ? "res://Art/PersistentHome/noble-crest.png" : "res://Art/PersistentHome/effect-lacquer.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/PersistentHome/chroma-cutout.gdshader") },
            MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps
        };
        AddChild(_art);
        if (!IsFavor)
        {
            var name = Palette.Text(effect!.Definition?.Name ?? "Ongoing effect", 23, Palette.Paper);
            name.AddThemeFontOverride("font", BodyFont); name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; name.ClipText = true;
            var kind = Palette.Text("Effect reminder", 16, Palette.PaperDim);
            kind.AddThemeFontOverride("font", GD.Load<FontFile>("res://Art/Fonts/Caudex-Italic.ttf"));
            kind.HorizontalAlignment = HorizontalAlignment.Center;
            name.MouseFilter = kind.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(name); AddChild(kind);
            Resized += () => {
                name.Position = new Vector2(Size.X * .28f, Size.Y * .23f); name.Size = new Vector2(Size.X * .65f, 30);
                kind.Position = new Vector2(Size.X * .28f, Size.Y * .55f); kind.Size = new Vector2(Size.X * .65f, 24);
            };
        }
        Pressed += inspect;
        Resized += () => { _art.Size = Size; QueueRedraw(); };
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Ready() { _art.Size = Size; QueueRedraw(); }
    public Rect2 FaceRect
    {
        get
        {
            // The complete scan fits inside the generated opening; no text or card edge is cropped.
            var opening = new Rect2(Size.X * .167f, Size.Y * .145f, Size.X * .666f, Size.Y * .727f);
            var height = Math.Min(opening.Size.Y, opening.Size.X * 1.4f);
            var face = new Vector2(height / 1.4f, height);
            return new Rect2(opening.GetCenter() - face / 2, face);
        }
    }
    public override void _Draw()
    {
        if (_face is not null)
        {
            var opening = new Rect2(Size.X * .16f, Size.Y * .14f, Size.X * .68f, Size.Y * .742f);
            DrawRect(opening, new Color("11100e"));
            DrawTextureRect(_face, FaceRect, false);
        }
        if (IsHovered() || HasFocus() || Selected)
            DrawRect(new Rect2(Vector2.One * 5, Size - Vector2.One * 10), Selected ? Palette.Crane : Palette.Gold, false, 1.2f, true);
    }
    public Control CreateReadingPreview()
    {
        var panel = new LacquerPanel { CustomMinimumSize = new Vector2(340, 0) };
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 9); panel.AddChild(column);
        column.AddChild(Palette.Text(IsFavor ? "IMPERIAL FAVOR" : Effect?.Definition?.Name ?? "ONGOING EFFECT", 22, Palette.Paper, true));
        if (IsFavor)
        {
            column.AddChild(new TextureRect { Texture = _face, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(292, 409) });
            column.AddChild(Palette.Wrapped($"Held by {Clan}. Public reminder; not a card in play.", 15, Palette.PaperDim));
        }
        else
        {
            if (Effect!.Definition is not null) column.AddChild(CardPreview.Face(Effect, 320));
            column.AddChild(Palette.Wrapped("Ongoing effect reminder · not a card in play", 15, Palette.Gold));
            column.AddChild(Palette.Wrapped(Effect.Definition?.Text ?? "", 16, Palette.Paper));
        }
        return panel;
    }
    public override Control _MakeCustomTooltip(string forText) => CreateReadingPreview();
}
