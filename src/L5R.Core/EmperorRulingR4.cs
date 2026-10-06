namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), Mendon's rulings on the open questions of the decision document, group R4. Where each is built:
//   1. Rhetoric / Solid Defense / Shiro Chugo / Unimpeachable Name: "may be played on any honor gain or loss" -- Honor changes are intercepted where they are applied (EmperorRulingR4Honor.cs); Honor
//      paid as a true cost stays outside; A Weak Poison's and Exploited Advantage's printed "Lose 2 Honor." moved from a cost to an effect (CardAbilities.cs);
//   2. Cherry Blossom Festival: "Proclaim is a reaction. Overconfident and traits are not reactions (despite triggering like them)" -- TraitReactions below;
//   3. Luxurious Silk: what "paying for an action" means for a recruit by an ability (EmperorRemainingF.cs, PaymentIsForAnAction);
//   4. The Killing Grounds: the first pass of EACH player (EmperorRemainingD.cs);
//   5. Bad Kharma / Footsteps of Madness: the out-of-play window now follows the before-action window instead of being skipped (EmperorRulingR4Honor.cs, ChainOutOfPlayReactionWindow); "a Focus Effect
//      is not an action" (MatchEngine._resolvingFocusEffect);
//   6. Ordered searches and Deception Revealed's Rings: DeckSearch.cs, EmperorRemainingA.cs (RecordBowedRings).

/// <summary>
/// Abilities the engine models with Timing "Reaction" (an optional, once-a-turn ability offered in a reaction window) although the card prints them as TRAITS: their text has no "Reaction:"
/// designator -- "After this card enters play: Gain 2 Honor", "Invest 5 Gold: ...", "After your Bushido Virtue action resolves, three times per turn: ...". Mendon's ruling (R4): "Overconfident and
/// traits are not reactions (despite triggering like them)". A rule that speaks of Reactions (Cherry Blossom Festival's "No players may take Reactions") must not touch these; it does touch every
/// Reaction-timed ability NOT listed here, Proclaim included. The set is pinned and audited against the printed texts in tests (EmperorRulingR4Tests).
/// </summary>
public static class TraitReactions
{
    private static readonly HashSet<string> Ids = new(StringComparer.Ordinal)
    {
        // "After this card enters play: Gain 2 Honor" / "Choose a player. He loses 2 Honor" (the Celestial Swords, The Ancestral Sword of Hantei).
        "ancestral-sword-hantei-honor", "celestial-sword-crab-honor", "celestial-sword-crane-honor", "celestial-sword-lion-honor", "celestial-sword-mantis-honor",
        "celestial-sword-phoenix-honor", "celestial-sword-unicorn-honor", "celestial-sword-scorpion-lose",
        // "After [name] enters play, lose N Honor" (Ikoma Ayumi, Akodo Nojima, Akodo Tsudoken - exp 2); "... Lose 2 Honor and you may put a Dark Virtue ..." (Daigotsu Aya).
        "ayumi-entry", "nojima-entry", "tsudoken-exp2-entry", "aya-entry-dark-virtue",
        // Other entering-play traits: Bayushi Makubesu's Followers, Small-Time Bully's dishonor, Thick Marsh's bow.
        "makubesu-create", "small-time-bully-dishonor", "thick-marsh-entry",
        // Invest (a special trait, core rules): Clay Kshatriya, Togashi Noritada.
        "clay-kshatriya-invest", "noritada-invest",
        // Triggered traits with other triggers: Never Safe's return ("After this battle ends, if this card is in your discard pile..."), The Castle of Order's redirect, The Spider's Web's attach,
        // Kyuden Otomo's Favor token, Matsu Rika's bonus, Akodo Shihiro's bonus.
        "never-safe-return", "castle-of-order-redirect", "spiders-web-attach", "spiders-web-attach-kensai", "otomo-favor-token", "rika-bushido-virtue-bonus", "shihiro-boost",
        // Issue #501: Goju Asuka's "After Asuka enters play: You may give control of her to another player."
        "goju-asuka-give",
        // Issue #501 (Mendon): Kakita Kae's "After a Battle action targets Kae, you may discard the Imperial Favor: ..." triggers like a Reaction but is a trait (no designator): repeatable, usable while bowed.
        "kae-favor-negate",
        // Issue #503: Shinjo Hayan's "After engaging: Once per turn, you may assign ..." (no designator: a trait; "you may" leaves the assignment optional, usable while he is bowed).
        "hayan-assign",
        // Issue #622: Words of Consecration's "After this card enters play, if this Shugenja is Air: Choose a player, who gains or loses 1 Honor."
        "words-of-consecration-honor",
        // Issue #622: Band of Brothers' "After this Follower enters play, target one or two of your Personalities. Create and attach a 1F Follower to each of them." prints no designator: a trait.
        "tf-band-brothers-create",
    };

    /// <summary>The trait-modelled Reaction-timed abilities, for the audit.</summary>
    public static IReadOnlyCollection<string> All => Ids;

    /// <summary>Whether this ability is a trait (not a printed Reaction) that the engine models with Reaction timing.</summary>
    // The Ivory Sword (PromoEmperorItems.cs); Yoritomo Toganin's Invest (StatTermCards.cs, issue #672)
    public static bool Is(string abilityId) => Ids.Contains(abilityId) || PromoEmperorItemsAbilities.IsTrait(abilityId) || StatTermCards.IsTrait(abilityId);
}

public sealed partial class MatchEngine
{
    // "No players may take Reactions" (Cherry Blossom Festival) bars a Reaction-timed ability unless it is a trait. Every place that decides whether a Reaction may be taken, offered or is worth a window
    // asks this one predicate.
    private bool BarredByCherryBlossom(string abilityId) => ReactionsBarred() && !TraitReactions.Is(abilityId);
}
