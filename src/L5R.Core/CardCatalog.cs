using System.Collections.ObjectModel;

namespace L5R.Core;

/// <summary>Explicitly supported training fixtures and the frozen published Lion/Crane pool.</summary>
public static class CardCatalog
{
    public static IReadOnlyDictionary<string, CardDefinition> Cards { get; } = new ReadOnlyDictionary<string, CardDefinition>(Build());
    public static CardDefinition Get(string id) => Cards.TryGetValue(id, out var card) ? card : throw new ArgumentException($"Unsupported card definition: {id}");

    private static Dictionary<string, CardDefinition> Build()
    {
        CardDefinition[] cards = [
            new("training-fragile-ancestor", "Training fragile ancestor", CardType.Personality, "Lion", Force:4, Chi:0, Keywords:["Ancestor"], Text:"Synthetic Chi-death fixture, never included in a playable deck."),
            new("training-dishonored-samurai", "Training dishonored samurai", CardType.Personality, "Crane", GoldCost:3, Force:2, Chi:2, PersonalHonor:2, Effect:"enters-dishonored", Keywords:["Samurai"], Text:"Synthetic Seppuku fixture: enters play Dishonorable. Never included in a playable deck."),
            new("training-enters-bowed-samurai", "Training enters-bowed samurai", CardType.Personality, "Crane", GoldCost:3, Force:2, Chi:2, PersonalHonor:2, Effect:"enters-bowed", Keywords:["Samurai"], Text:"Synthetic fixture: enters play bowed directly, never included in a playable deck."),
            new("training-crane-stronghold", "Crane training stronghold", CardType.Stronghold, "Crane", GoldProduction:4, ProvinceStrength:6, StartingHonor:8, Text:"Training fixture. Bow to produce 4 Gold for one payment."),
            new("training-lion-stronghold", "Lion training stronghold", CardType.Stronghold, "Lion", GoldProduction:4, ProvinceStrength:6, StartingHonor:6, Text:"Training fixture. Bow to produce 4 Gold for one payment."),
            new("training-border-keep", "Training border keep", CardType.Holding, GoldProduction:2, Text:"Training fixture: bow for 2 Gold. This is not the published Border Keep; its filtering abilities are absent."),
            new("training-bamboo", "Training bamboo harvesters", CardType.Holding, GoldProduction:2, Effect:"skip-first-straighten", Text:"Training fixture: starts bowed for the second player; does not straighten on that player's first turn. Bow for 2 Gold."),
            new("training-marketplace", "Training marketplace", CardType.Holding, GoldCost:2, GoldProduction:2, Effect:"crane-gold", Text:"Training fixture: bow for 2 Gold, or 3 if you are a Crane player. Conditional production is evaluated when paying."),
            new("training-farm", "Training farm", CardType.Holding, GoldCost:2, GoldProduction:2, Text:"Training fixture. Enters play bowed; bow for 2 Gold for one payment."),
            new("training-crane-samurai", "Crane training samurai", CardType.Personality, "Crane", GoldCost:5, Force:3, Chi:3, PersonalHonor:2, HonorRequirement:4, Text:"Training fixture. No card abilities."),
            new("training-crane-courtier", "Crane training courtier", CardType.Personality, "Crane", GoldCost:6, Force:2, Chi:3, PersonalHonor:3, HonorRequirement:6, Text:"Training fixture. No card abilities."),
            new("training-crane-veteran", "Crane training veteran", CardType.Personality, "Crane", GoldCost:8, Force:5, Chi:4, PersonalHonor:3, HonorRequirement:6, Text:"Training fixture. No card abilities."),
            new("training-crane-dignitary", "Crane training dignitary", CardType.Personality, "Crane", GoldCost:6, Force:3, Chi:4, PersonalHonor:4, HonorRequirement:12, Text:"Training fixture. High Honor Requirement demonstrates Clan allegiance."),
            new("training-lion-samurai", "Lion training samurai", CardType.Personality, "Lion", GoldCost:5, Force:4, Chi:3, PersonalHonor:2, HonorRequirement:4, Text:"Training fixture. No card abilities."),
            new("training-lion-courtier", "Lion training courtier", CardType.Personality, "Lion", GoldCost:6, Force:2, Chi:3, PersonalHonor:3, HonorRequirement:6, Text:"Training fixture. No card abilities."),
            new("training-lion-veteran", "Lion training veteran", CardType.Personality, "Lion", GoldCost:8, Force:6, Chi:4, PersonalHonor:3, HonorRequirement:6, Text:"Training fixture. No card abilities."),
            new("training-lion-dignitary", "Lion training dignitary", CardType.Personality, "Lion", GoldCost:6, Force:3, Chi:4, PersonalHonor:4, HonorRequirement:12, Text:"Training fixture. High Honor Requirement demonstrates Clan allegiance."),
            new("training-follower", "Training ashigaru", CardType.Follower, GoldCost:2, Force:2, HonorRequirement:1, Text:"Training fixture. Requires a Personality with at least 1 Personal Honor. Adds 2 unit Force when unbowed."),
            new("training-item", "Training battle standard", CardType.Item, GoldCost:2, Force:2, Text:"Training fixture. Adds 2 Force directly to its Personality."),
            new("training-rally", "Training rally", CardType.Strategy, Effect:"force-three", Text:"Training fixture. Battle: Target your Personality at the current battlefield: Give it +3 Force until the turn ends."),
            new("training-pressure", "Training pressure", CardType.Strategy, Effect:"bow-small", Text:"Training fixture. Battle: Target an enemy Personality with 3 or less Force at the current battlefield: Bow it.")
        ];
        return cards.Concat(PublishedContent.Snapshot.Cards).ToDictionary(c => c.Id, StringComparer.Ordinal);
    }
}
