namespace L5R.Core;

// ======================================================================================================================================================================================
// GAIJIN SORCERER (oracle-l5r-10585, Promotional-Emperor; issue #667, cluster "duel-sleep"). The scan prints "While your ritual is resolving, Gaijin Sorcerer counts as two Shugenja." (the
// catalogue's "After you announce your Ritual action, until it finishes resolving" is a later wording).
//
// Mendon's confirmed ruling: "The doubling only counts towards the count of shugenja and does not double the Chi contribution to a ritual."
//
// The primitive is a WEIGHTED SHUGENJA COUNT for a Ritual: RitualShugenjaCount sums each of a ritual's Shugenja as one, and a Gaijin Sorcerer (Effect "gaijin-sorcerer-two-shugenja") as two,
// for his own controller's Ritual only (the weight is read only inside a Ritual's resolution, so it never applies elsewhere). Totals of Chi (Capturing the Soul, Illusory Defense) read each
// performing Shugenja's Chi once and never call this; Sleep ("Discard an equal number of cards") is the Ritual that counts Shugenja.
// ======================================================================================================================================================================================

public sealed partial class MatchEngine
{
    internal const string GaijinSorcererEffect = "gaijin-sorcerer-two-shugenja";

    /// <summary>How many Shugenja one card counts as while its controller's Ritual is resolving: two for Gaijin Sorcerer, one for every other Shugenja.</summary>
    private int RitualShugenjaWeight(CardInstance shugenja, int ritualControllerId) =>
        InPlay(shugenja) && shugenja.OwnerId == ritualControllerId && Definition(shugenja).Effect == GaijinSorcererEffect ? 2 : 1;

    /// <summary>The count of Shugenja a Ritual (resolving, controlled by the given player) works with. Chi totals never use this: the Sorcerer's Chi counts once.</summary>
    private int RitualShugenjaCount(IEnumerable<CardInstance> shugenja, int ritualControllerId) => shugenja.Sum(s => RitualShugenjaWeight(s, ritualControllerId));
}
