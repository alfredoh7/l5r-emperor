using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    /// <summary>Battle values: a Province placard under each defending province, plus a fixed left-edge stack for the engaged battle.</summary>
    private const float PlacardStackScale = .85f;
    private readonly Dictionary<(int Owner, int Province), ProvincePlacard> _provincePlacards = [];
    private readonly List<Control> _placardStack = [];

    private void AddProvincePlacard(Control area, PlayerView owner, ProvinceView province, float width)
    {
        // The defender's provinces are what a battle is fought over, so only they carry a placard.
        if (!BattleActive || View.WinnerId is not null || owner.Id == View.ActivePlayerId) return;
        var values = View.Battlefields?.FirstOrDefault(b => b.Province == province.Index)?.Values;
        var offered = Actions.Any(a => a.Enabled && a.Type == "selectBattle" && a.Command?.Province == province.Index);
        var placard = new ProvincePlacard(province.Index, province.Strength, values?.ProvinceFalls == true, offered, ProvincePlacard.TableWidth, ProvincePlacard.TableHeight, false);
        placard.Pressed += () => SelectProvince(owner.Id, province.Index);
        Place(area, placard, (width - ProvincePlacard.TableWidth) / 2, PlaneHeight(CardHeight) + 4, ProvincePlacard.TableWidth, ProvincePlacard.TableHeight);
        _provincePlacards[(owner.Id, province.Index)] = placard;
    }

    private void AddBattleStack()
    {
        foreach (var node in _placardStack.Where(IsInstanceValid)) node.QueueFree();
        _placardStack.Clear();
        if (View.WinnerId is not null || View.CurrentBattleProvince is not int field || BattleValues(field) is not { } values) return;
        var seals = View.Players.OrderBy(p => p.Id == View.ViewerId ? 1 : 0).Select(p =>
        {
            var attacker = p.Id == values.AttackerId;
            return (Control)new BattleSeal(p.Clan, p.Id, attacker ? "Attacker" : "Defender", attacker ? values.AttackerForce : values.DefenderForce, attacker ? values.AttackerUnits : values.DefenderUnits);
        }).ToArray();
        var parts = new List<Control> { seals[0] };
        if (values.ProvinceStrength is int strength && BattlefieldProvince(field) is int province)
        {
            var offered = Actions.Any(a => a.Enabled && a.Type == "selectBattle" && a.Command?.Province == province);
            var plate = new ProvincePlacard(province, strength, values.ProvinceFalls, offered, ProvincePlacard.StackWidth, ProvincePlacard.StackHeight, true);
            plate.Pressed += () => SelectProvince(values.DefenderId, province);
            parts.Add(plate);
        }
        parts.Add(seals[1]);
        const float gap = 8;
        var total = parts.Sum(p => p.Size.Y) + gap * (parts.Count - 1);
        var y = 540 - total * PlacardStackScale / 2;
        foreach (var part in parts)
        {
            part.Scale = Vector2.One * PlacardStackScale; part.Position = new Vector2(16, y);
            _clanHud.AddChild(part); _placardStack.Add(part);
            y += (part.Size.Y + gap) * PlacardStackScale;
        }
    }
}
