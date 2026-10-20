namespace L5R.Core;

/// <summary>One lesson of the curriculum, built or not. <see cref="Id"/> is the id of the <see cref="TutorialLesson"/> that fills it once it is written. The menu shows every entry (nothing is locked):
/// a built one starts, an unbuilt one is a placeholder showing its <see cref="Brief"/>. <see cref="Staging"/> is the table the lesson should start from, for whoever writes it.</summary>
public sealed record TutorialEntry(string Number, string Id, string Title, string Brief, string Staging)
{
    /// <summary>True once a lesson with this id is in <see cref="TutorialLessons.All"/>.</summary>
    public bool Built => TutorialLessons.All.Any(l => l.Id == Id);
}

public sealed record TutorialTrack(string Id, string Title, TutorialEntry[] Entries);

/// <summary>Every tutorial the game needs, in the order the menu shows them (docs/TUTORIAL_CURRICULUM.md is the readable copy; issue #1035 tracks writing them).
/// Whoever builds one adds its <see cref="TutorialLesson"/> to <see cref="TutorialLessons.All"/> with the same id, and the menu starts it.</summary>
public static class TutorialCurriculum
{
    public static readonly TutorialTrack[] Tracks =
    [
        new("core", "The core loop",
        [
            new("1.1", "first-turn", "The first turn and a half", "Take a first turn as Lion against Crane: the phases, recruiting, Proclaim, equipping, attacking and destroying a province.", "Pinned Lion against pinned Crane, from turn 1."),
            new("1.2", "defense", "Defense", "You are attacked. Assign defenders, decide whether to keep one home, take the first action of a battle and see why an attacker fears the counter-attack.", "Pinned Crane defending against a Lion attack, mid-game, with a Personality at home and one to commit."),
            new("1.3", "battle-in-depth", "Battle in depth", "Force against Chi, destroying Personalities and the Dead pile, what happens when a province falls (Harsh Choices) and the end of a battle.", "A staged battle with several units on each side."),
            new("1.4", "hand-limit", "Hand limit and the End phase", "Hand size, drawing at the End phase, discarding down to the limit, and the Dynasty and Fate discard piles.", "Lion holding a full hand at its End phase."),
            new("1.5", "gold-and-payment", "Gold and payment", "Where Gold comes from, why excess Gold is lost, Reaction Gold, Clan Allegiance and alternative costs.", "A Stronghold with mixed Holdings and a hand of differently priced cards."),
            new("1.6", "dynasty-and-refresh", "Dynasty phase and refresh", "Refilling provinces, discarding a province card, face-up and face-down cards, and the Border Keep refresh.", "A turn 1 position with a poor set of provinces."),
        ]),
        new("cards", "Reading cards",
        [
            new("2.1", "reading-a-card", "Reading a card", "Cost, Force, Chi, Honor Requirement, Personal Honor, Focus Value, the text box, keywords and traits, on one card of each type.", "A hand and board of mixed card types."),
            new("2.2", "traits-and-abilities", "Traits and Abilities", "Printed traits against abilities, and the ability types: Open, Limited, Reaction and Battle.", "Cards whose traits change their numbers and cards with each ability type."),
            new("2.3a", "keywords-attack", "Keywords: attack", "Cavalry, Naval, Conqueror, Scout and Tactician, and what each changes in an attack.", "An attack with one unit of each keyword."),
            new("2.3b", "keywords-card", "Keywords: cards", "Kensai, Armor, Weapon, Duelist and the Spell elements.", "Personalities with attachments and a duelist."),
            new("2.3c", "keywords-clan-rank", "Keywords: clan and rank", "Clan keywords, Samurai, Courtier, Shugenja, Ancestor and Magistrate, and why a card cares which it is.", "Mixed Personalities with cards that name keywords."),
            new("2.4", "items-and-followers", "Items and Followers", "Attaching, the Armor and Weapon limits, a Follower's Force and what destroys an attachment.", "A Personality with equipment in hand."),
            new("2.5", "unbowed-attachments", "Unbowed attachments", "Why an attachment can stay unbowed when its Personality bows, and what that lets you do.", "A bowed Personality carrying unbowed attachments."),
            new("2.6", "created-cards", "Created cards", "What a created card is, how it is made, and where it goes when it leaves play.", "A card that creates another."),
        ]),
        new("special", "Special card types",
        [
            new("3.1", "events", "Events", "An Event is revealed in the Events phase, resolves and is discarded.", "An Event waiting in a province."),
            new("3.2", "regions", "Regions", "A Region attaches to a province, has an ongoing effect and stays until something removes it.", "A Region about to be revealed."),
            new("3.3", "celestials", "Celestials", "A Celestial enters play, stays, and has its effect for as long as it does.", "A Celestial about to be revealed."),
            new("3.4", "retainers", "Retainers", "The Retainer card type and how it differs from a Personality.", "A Retainer in a province next to a Personality."),
        ]),
        new("spells-rings", "Shugenja, Spells and Rings",
        [
            new("4.1", "spells-attaching", "Shugenja and Spells: attaching", "Only a Shugenja carries a Spell. Element keywords, cost and attaching.", "A Shugenja in play and a Spell in hand."),
            new("4.2", "spells-casting", "Shugenja and Spells: casting", "Use a Spell in a battle: its timing and its target.", "A staged battle with a Shugenja carrying a Spell."),
            new("4.3", "rings", "Rings", "How a Ring enters play (its condition) and how to discard one for its Focus Value.", "A battle that satisfies a Ring's entry condition."),
            new("4.4", "enlightenment", "Enlightenment Victory", "Control all five Rings at the start of your turn to win.", "Four Rings in play and the fifth about to enter."),
        ]),
        new("timing", "Timing",
        [
            new("5.1", "priority-and-passing", "Priority and passing", "Who may act and when, what two passes in a row end, and Open actions on your opponent's turn.", "An Action phase with both players holding actions."),
            new("5.2", "reactions-basics", "Reactions: the basics", "A Reaction answers a trigger: \"before\", \"after\" and \"when\".", "An opponent's recruit with a Reaction in your hand."),
            new("5.3", "reactions-answering", "Reactions: answering", "Respond during a battle, how responses are ordered, and what a Reaction cannot do.", "A battle with a Reaction on each side."),
            new("5.4", "imperial-favor", "Imperial Favor", "What the Favor is, how Lobby takes it and what holding it does.", "A Lion turn where the Favor can be claimed."),
        ]),
        new("honor", "Honor",
        [
            new("6.1", "honor-gain-loss", "Honor: gain and loss", "Where Honor comes from and where it goes, and the Honor counter.", "A mid-game table with Honor swinging both ways."),
            new("6.2", "honor-requirement", "Honor Requirement and Personal Honor", "Which Personalities you may recruit at your Honor, and Proclaim again.", "Honor of 4 and two Personalities to choose between."),
            new("6.3", "dishonor-seppuku", "Dishonor, Rehonor and Seppuku", "Dishonorable Personalities, restoring Honor and committing seppuku.", "A dishonored Personality in play."),
            new("6.4", "honor-victory-defeat", "Honor Victory and dishonor defeat", "40 Honor wins. -20 or lower loses at your next End phase, even if you recover first.", "One player at 38 Honor and the other at -18."),
        ]),
        new("further", "Further rules",
        [
            new("7.1", "duels", "Duels", "Focus, the Duelist keyword and tie-breaks.", "A duel offered during a battle."),
            new("7.2", "terrain-kata-overlays", "Terrain, Kata and Overlays", "Terrain, Kata, Overlays and Experienced Personalities (only for cards the engine implements).", "To be decided with the cards that ship."),
            new("7.3", "strongholds", "The Strongholds", "One short look at each clan's Stronghold: Gold, Province Strength, starting Honor and ability.", "Each clan's pinned Stronghold in turn."),
            new("7.4", "winning-and-losing", "Winning and losing", "Military, Honor, Enlightenment and Dishonor victories, and conceding.", "One staged position per ending."),
        ]),
        new("app", "Using the app",
        [
            new("8.1", "card-library", "Card Library", "Search and read the card catalogue.", "Runs in the Card Library screen, not a match."),
            new("8.2", "deck-builder", "Deck Builder", "Build, save and check a deck, and what legality means.", "Runs in the Deck Builder screen, not a match."),
            new("8.3", "starting-a-game", "Starting a game", "Practice against the bot, private rooms and conceding.", "Runs from the menu, not a match."),
            new("8.4", "settings-and-help", "Settings and help", "Reduced motion, Paused Animation and the rules help screens.", "Runs in the options and help screens."),
        ]),
    ];

    public static IEnumerable<TutorialEntry> All => Tracks.SelectMany(t => t.Entries);
}
