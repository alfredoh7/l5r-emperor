using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private const float ValuesCourtWidth = 580, ValuesInset = 32, ValuesGap = 18;
    private BattleValuesView? BattleValues(int field) => View.Battlefields?.FirstOrDefault(b => b.Index == field)?.Values;
    private float ValuesTop(int field) => BattleValues(field) is null ? MatHeader :
        ValuesInset + BattleValueRail.Height + ValuesGap + (IsAdditionalBattlefield(field) ? 32 : 0);
    private float ValuesBottom(int field) => BattleValues(field) is null ? MatPadding : ValuesInset + BattleValueRail.Height + ValuesGap;
    private float CourtHeight(int field, float occupied) => Math.Max(270, occupied + ValuesTop(field) + ValuesBottom(field) + 38);
    private readonly Dictionary<(int Field, int Owner), BattleValueRail> _valueRails = [];
    private readonly Dictionary<int, Control> _strengthBadges = [];

    private void AddBattleValues(Control court, int field, Rect2 bounds)
    {
        if (BattleValues(field) is not { } values) return;
        foreach (var player in View.Players)
        {
            var attacker = player.Id == values.AttackerId;
            var rail = new BattleValueRail(player.Clan, player.Id, attacker ? "Attacker" : "Defender",
                attacker ? values.AttackerForce : values.DefenderForce, attacker ? values.AttackerUnits : values.DefenderUnits,
                bounds.Size.X - 2 * ValuesInset);
            var y = player.Id == Self.Id ? bounds.Size.Y - ValuesInset - BattleValueRail.Height
                : ValuesTop(field) - ValuesGap - BattleValueRail.Height;
            Place(court, rail, ValuesInset, y, rail.Size.X, BattleValueRail.Height);
            _valueRails[(field, player.Id)] = rail;
        }
    }

    private void AddProvinceStrength(Control area, PlayerView owner, ProvinceView province, float width)
    {
        if (!BattleActive) return;
        var field = View.Battlefields?.FirstOrDefault(b => b.Province == province.Index && b.Values?.DefenderId == owner.Id);
        if (field?.Values?.ProvinceStrength is not int strength) return;
        var badge = new BattleStrengthBadge { MouseFilter = Control.MouseFilterEnum.Ignore };
        var text = Palette.Text($"STRENGTH  {strength}", 18, Palette.Paper);
        text.HorizontalAlignment = HorizontalAlignment.Center; text.VerticalAlignment = VerticalAlignment.Center;
        text.MouseFilter = Control.MouseFilterEnum.Ignore;
        Place(badge, text, 0, 0, width - 12, 32);
        Place(area, badge, 6, PlaneHeight(CardHeight) + 4, width - 12, 32);
        _strengthBadges[province.Index] = badge;
    }
}

public partial class BattleStrengthBadge : Control
{
    private static readonly Texture2D LeftArt = GD.Load<Texture2D>("res://Art/BattleValues/strength-left.png");
    private static readonly Texture2D RunArt = GD.Load<Texture2D>("res://Art/BattleValues/strength-run.png");
    private static readonly Texture2D RightArt = GD.Load<Texture2D>("res://Art/BattleValues/strength-right.png");
    public override void _Draw()
    {
        var cap = Size.Y * 47 / 57;
        DrawTextureRect(LeftArt, new Rect2(0, 0, cap, Size.Y), false);
        DrawTextureRect(RunArt, new Rect2(cap, 0, Size.X - 2 * cap, Size.Y), false);
        DrawTextureRect(RightArt, new Rect2(Size.X - cap, 0, cap, Size.Y), false);
    }
}
