using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<int, int> _openBattlefieldOrdinals = [];
    private int? _viewedBattlefield, _navigationActiveBattle;
    private (int Turn, int Attacker)? _openBattlefieldBattle;
    private readonly Dictionary<int, Button> _battlefieldNavigation = [];
    private PopupPanel? _battlefieldHelp;
    private Control? _battlefieldHeaders;
    private readonly Dictionary<int, Control> _battlefieldCaptions = [];

    private void ClearBattlefieldHeaders()
    {
        if (_battlefieldHeaders is null)
        {
            _battlefieldHeaders = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 20 };
            _root.AddChild(_battlefieldHeaders);
        }
        Palette.Clear(_battlefieldHeaders); _battlefieldCaptions.Clear();
    }

    private int? BattlefieldProvince(int index) => BattlefieldPresentation.Province(View, index);
    // Additional fronts are laid out beyond the piles. Their province association
    // is named explicitly; a neck must never point at an unrelated physical bay.
    private bool CourtConnectsToProvince(int index) =>
        !IsAdditionalBattlefield(index) && BattlefieldProvince(index) is not null;

    private void ObserveBattlefieldLocations()
    {
        var battle = (View.TurnNumber, View.ActivePlayerId);
        if (!_tableLayoutSeen || !BattleActive || _openBattlefieldBattle != battle)
        {
            _openBattlefieldOrdinals.Clear();
            _viewedBattlefield = _navigationActiveBattle = null;
        }
        _openBattlefieldBattle = BattleActive ? battle : null;
        foreach (var index in (View.Battlefields ?? []).Select(b => b.Index)
            .Concat(View.UnresolvedBattlefields)
            .Concat(View.CurrentBattleProvince is int active ? [active] : [])
            .Concat(View.Players.SelectMany(p => p.Army).Where(c => c.ProvinceIndex is not null).Select(c => c.ProvinceIndex!.Value))
            .Distinct().Order())
            if (BattlefieldProvince(index) is null && !_openBattlefieldOrdinals.ContainsKey(index))
                _openBattlefieldOrdinals[index] = _openBattlefieldOrdinals.Count;
        if (_navigationActiveBattle != View.CurrentBattleProvince)
            _viewedBattlefield = _navigationActiveBattle = View.CurrentBattleProvince;
    }

    private void AddOpenBattlefieldHeader(int index)
    {
        var open = BattlefieldProvince(index) is null;
        if (!open && !IsAdditionalBattlefield(index)) return;
        // Readable screen-facing text follows the projected rim without flattening
        // into the table. The underlying court retains its normal legal hit target.
        var caption = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _battlefieldHeaders!.AddChild(caption); _battlefieldCaptions[index] = caption;
        var title = new Button { Text = BattlefieldName(index), Flat = true, ClipText = true };
        CompactButton(title); title.AddThemeFontOverride("font", Palette.Title);
        title.AddThemeColorOverride("font_color", Palette.Paper);
        title.AddThemeColorOverride("font_outline_color", new Color("161b1d")); title.AddThemeConstantOverride("outline_size", 3);
        title.Pressed += () => SelectProvince(1 - View.ActivePlayerId, index);
        title.TooltipText = open ? BattlefieldPresentation.OpenHelp : BattlefieldName(index);
        title.SetMeta("flat_tooltip", title.TooltipText); caption.AddChild(title);
        if (!open) return;
        var help = new BattlefieldHelpButton { Text = "?", TooltipText = BattlefieldPresentation.OpenHelp };
        CompactButton(help);
        help.AddThemeFontSizeOverride("font_size", 20);
        help.SetMeta("flat_tooltip", BattlefieldPresentation.OpenHelp);
        help.SetMeta("battlefield_help", index);
        help.Pressed += () => ShowBattlefieldHelp(help);
        caption.AddChild(help);
    }

    private void LayoutBattlefieldHeaders()
    {
        if (_battlefieldHeaders is null) return;
        _battlefieldHeaders.Visible = !MenuVisible && !PracticeVisible && !MatchDuelOpen;
        if (!_battlefieldHeaders.Visible) return;
        var scale = _root.Scale.X;
        foreach (var (index, caption) in _battlefieldCaptions)
        {
            if (!_battleCourts.TryGetValue(index, out var court)) { caption.Hide(); continue; }
            var left = WorldToScreen(new Vector2(court.Position.X + 28, court.Position.Y + 22));
            var right = WorldToScreen(new Vector2(court.End.X - 28, court.Position.Y + 22));
            var width = Math.Min(330, Math.Abs(right.X - left.X) / scale);
            var center = (left + right) / 2 / scale;
            caption.Position = center - new Vector2(width / 2, 24);
            caption.Size = new Vector2(width, 28);
            var rect = caption.GetGlobalRect();
            caption.Visible = width >= 120 && _tableSurface.GetGlobalRect().Encloses(rect) &&
                !_inspectorPanel.GetGlobalRect().Intersects(rect) && !_timeline.GetGlobalRect().Intersects(rect) &&
                !(_battlefieldReviewBar?.GetGlobalRect().Intersects(rect) ?? false);
            var title = (Button)caption.GetChild(0);
            var available = width - (caption.GetChildCount() == 2 ? 30 : 0);
            var fontSize = 18;
            while (fontSize > 12 && Palette.Title.GetStringSize(title.Text, fontSize: fontSize).X > available) fontSize--;
            title.AddThemeFontSizeOverride("font_size", fontSize); title.Position = Vector2.Zero; title.Size = new Vector2(available, 28);
            if (caption.GetChildCount() == 2) { var help = (Control)caption.GetChild(1); help.Position = new Vector2(available, 0); help.Size = new Vector2(28, 28); }
        }
    }

    private void ShowBattlefieldHelp(Control source)
    {
        CloseBattlefieldHelp();
        var popup = new PopupPanel { Theme = SidebarSkin.Theme(), Transient = true };
        _battlefieldHelp = popup;
        popup.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(402, 0) };
        box.AddThemeConstantOverride("separation", 12);
        var title = Palette.Text("BATTLE WITHOUT A PROVINCE", 20, Palette.Gold);
        title.AddThemeFontOverride("font", Palette.Title); box.AddChild(title);
        box.AddChild(Palette.Text("No province strength.\nNo adjacent battlefields.\nNo province can be destroyed here.", 18, Palette.Paper));
        box.AddChild(Palette.Text("Click outside or press Escape to close.", 14, Palette.PaperDim));
        popup.AddChild(box); AddChild(popup);
        popup.PopupHide += () => { if (_battlefieldHelp == popup) _battlefieldHelp = null; popup.QueueFree(); };
        var anchor = ScreenCenter(source);
        var window = GetViewport().GetVisibleRect().Size;
        var size = new Vector2I(430, 205);
        var x = anchor.X + 24 + size.X < _inspectorPanel.GetGlobalRect().Position.X
            ? anchor.X + 24 : anchor.X - size.X - 24;
        popup.Popup(new Rect2I(new Vector2I(
            (int)Math.Clamp(x, 12, Math.Max(12, window.X - size.X - 12)),
            (int)Math.Clamp(anchor.Y + 26, 12, Math.Max(12, window.Y - size.Y - 12))), size));
    }

    private void CloseBattlefieldHelp()
    {
        if (_battlefieldHelp is null) return;
        var old = _battlefieldHelp; _battlefieldHelp = null;
        old.Hide();
    }

    private void RenderBattlefieldNavigation()
    {
        _battlefieldNavigation.Clear();
        var courts = _battleCourts.Keys.Order().ToArray();
        if (courts.Length <= 1) return;
        var label = Palette.Text("View battlefield", 15, Palette.PaperDim);
        _sidebarHeader.AddChild(label);
        var row = new HFlowContainer();
        row.AddThemeConstantOverride("h_separation", 6); row.AddThemeConstantOverride("v_separation", 6);
        _sidebarHeader.AddChild(row);
        foreach (var index in courts)
        {
            var button = SidebarSkin.Button(BattlefieldName(index), () => ViewBattlefield(index));
            button.CustomMinimumSize = new Vector2(160, 42);
            button.AddThemeFontSizeOverride("font_size", 16);
            button.TooltipText = "Move the camera here. This does not choose the next battle or move any cards.";
            button.Disabled = PresentationBusy;
            if ((_viewedBattlefield ?? View.CurrentBattleProvince) == index)
                button.AddThemeColorOverride("font_color", Palette.Gold);
            _battlefieldNavigation[index] = button; row.AddChild(button);
        }
        if (_viewedBattlefield is int viewed && View.CurrentBattleProvince is int active && viewed != active)
            _sidebarHeader.AddChild(Palette.Wrapped($"Viewing {BattlefieldName(viewed)}. Actions remain at {BattlefieldName(active)}.", 15, Palette.PaperDim));
    }

    private void ViewBattlefield(int index)
    {
        if (!BattleActive || PresentationBusy || !_battleCourts.ContainsKey(index)) return;
        _viewedBattlefield = index;
        var from = _cameraCenter; var zoom = _cameraZoom;
        FocusBattlefield(index);
        if (!_reducedMotion)
        {
            _cameraTo = _cameraCenter; _zoomTo = _cameraZoom; _cameraFrom = from; _zoomFrom = zoom;
            _cameraGlideTime = 0; _cameraGlide = true; _glideEndsFitted = false; _cameraCenter = from; _cameraZoom = zoom; ApplyTableCamera();
        }
        RenderInspector();
    }

    private void FocusBattlefield(int index)
    {
        if (!_battleCourts.TryGetValue(index, out var court)) return;
        var bay = CourtConnectsToProvince(index)
            ? _matTargets.FirstOrDefault(s => s.Id == $"province:{1 - View.ActivePlayerId}:{index}") : null;
        var region = (bay is null ? court : court.Merge(bay.Bounds)).Grow(40);
        _cameraFitted = false; _cameraGlide = false;
        _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
    }
}

public partial class BattlefieldHelpButton : Button
{
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
}
