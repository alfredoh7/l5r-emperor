namespace L5R.Core;

// Engage is battlefield-specific (rules lead, issue #840, generalizing #838's The Bestial Ones): an Engage Reaction is taken by a card at the battlefield being engaged -- a Personality, Follower
// or Item in play must itself be there -- and it reaches only cards at that battlefield. Exempt are a Home Engage, an Absent one, and a Reaction whose text names another place; those are listed
// by ability id below, each with the words that exempt it. A Strategy or Item played from a hand, and a Stronghold, Holding or Region (never at a battlefield), are not unit cards in play: their
// own text names the battlefield they act on ("choose your performing Scout at the current battlefield", "if you have any units at this battlefield").
public sealed partial class MatchEngine
{
    private static bool IsEngageReaction(AbilityDefinition ability) => ability.Timing == "Reaction" && ability.Trigger is "after-engaging" or "after-engaging-at-own-battlefield";

    // The card taking the Engage need not be at the battlefield.
    private static readonly HashSet<string> EngageSourceElsewhere = new(StringComparer.Ordinal)
    {
        "w821-tsumaro-illusion",       // Air Home Engage: moves a target Air Shugenja home
        "wl-sg-recovering-lost",       // Absent Engage: moves a Personality at home to the battlefield
        "hayan-assign",                // "After engaging: ... assign your Unicorn Personality in your home"
        "wave-persists-move",          // "if this Shugenja is not at the current battlefield, target your Personality there"
        "rokku-move",                  // "Even if Rokku is bowed, after engaging ...: Move Rokku there"
        "pf-sled-move",                // "After engaging, if any enemy units are at the current battlefield: move the wearer there"
        "wl-false-ring-of-void-lock",  // "Engage, bow this card: target a Personality at any location"
    };

    // The target need not be at the battlefield.
    private static readonly HashSet<string> EngageTargetElsewhere = new(StringComparer.Ordinal)
    {
        "w821-tsumaro-illusion", "wl-sg-recovering-lost", "hayan-assign", "wl-false-ring-of-void-lock",
    };

    private bool EngageSourceMustBeAtBattlefield(string id, AbilityDefinition ability, CardInstance source) =>
        IsEngageReaction(ability) && !ability.Home && !ability.Absent && !EngageSourceElsewhere.Contains(id)
        && InPlay(source) && Definition(source).Type is CardType.Personality or CardType.Follower or CardType.Item;

    private bool EngageTargetMustBeAtBattlefield(string id, AbilityDefinition ability, CardInstance target) =>
        IsEngageReaction(ability) && !ability.Home && !ability.Absent && !EngageTargetElsewhere.Contains(id)
        && InPlay(target) && Definition(target).Type is CardType.Personality or CardType.Follower or CardType.Item;
}
