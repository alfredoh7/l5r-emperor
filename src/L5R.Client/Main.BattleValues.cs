using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private BattleValuesView? BattleValues(int field) => View.Battlefields?.FirstOrDefault(b => b.Index == field)?.Values;
}
