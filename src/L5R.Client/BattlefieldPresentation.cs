using L5R.Core;

namespace L5R.Client;

/// <summary>Names and associations from the permitted view, never battle legality.</summary>
public static class BattlefieldPresentation
{
    public const string OpenHelp = "Battle without a province\n\nNo province strength.\nNo adjacent battlefields.\nNo province can be destroyed here.";

    public static int? Province(MatchView view, int battlefield)
    {
        // An explicit null also matters for primary battlefields after destruction.
        if (view.Battlefields?.FirstOrDefault(b => b.Index == battlefield) is { } field)
            return field.Province;
        if (battlefield >= MatchEngine.FirstAdditionalBattlefield) return null;
        return view.Players.Single(p => p.Id != view.ActivePlayerId).Provinces
            .FirstOrDefault(p => p.Index == battlefield && !p.Destroyed)?.Index;
    }

    public static string Name(MatchView view, int battlefield) => Province(view, battlefield) is int province
        ? battlefield < MatchEngine.FirstAdditionalBattlefield ? $"Province {province + 1}" : $"Province {province + 1} · extra front"
        : "Open battlefield";

    public static string Letter(int ordinal)
    {
        var result = "";
        do { result = (char)('A' + ordinal % 26) + result; ordinal = ordinal / 26 - 1; } while (ordinal >= 0);
        return result;
    }
}
