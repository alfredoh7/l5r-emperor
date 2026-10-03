using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation only: consumes already-redacted views and exact engine decisions.</summary>
public static class ProvincePresentation
{
    public static ProvinceCardSlotView[] Slots(ProvinceView province) =>
        province.DynastySlots ?? (province.HoldsNoDynastyCards ? [] : [new(0, province.Card)]);

    public static IEnumerable<CardView> Cards(ProvinceView province) =>
        Slots(province).Where(s => s.Card is not null).Select(s => s.Card!)
            .Concat(province.Region is { } region ? [region] : []);

    public static string SlotName(int index) => index switch { 0 => "A", 1 => "B", _ => (index + 1).ToString() };

    // A retained button/selection may outlive a packet, a refill or a seat change.
    public static ProvinceOrderOption? CurrentOption(MatchView view, string decisionId, long version, string optionId)
    {
        var order = view.PendingProvinceOrder;
        if (order is null || order.Id != decisionId || view.Version != version || order.PlayerId != view.ViewerId)
            return null;
        var option = order.Options.FirstOrDefault(o => o.Id == optionId && o.Enabled);
        return option?.Command is { } command && command.PlayerId == view.ViewerId && command.ExpectedVersion == version
            ? option : null;
    }
}
