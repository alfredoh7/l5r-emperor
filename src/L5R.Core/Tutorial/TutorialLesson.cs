namespace L5R.Core;

/// <summary>A card that starts in play at home, face up (a Border Keep, a bowed Bamboo Harvesters).</summary>
internal sealed record TutorialHomeCard(string DefinitionId, bool Bowed = false);

/// <summary>One side of a lesson's table. The deck is the pinned deck of <see cref="Clan"/> (<c>content/published/lion-crane-v1.json</c>) unless <see cref="Deck"/> names another; the cards in
/// <see cref="DynastyTop"/> and <see cref="FateTop"/> are put on top in that order, whether or not the deck holds them. A catalogue card outside the pinned decks takes the place of the deck's last card.</summary>
internal sealed record TutorialSide(string Clan, string StrongholdId, string[] DynastyTop, string[] FateTop, TutorialHomeCard[] Home, int? Honor = null, PublishedDeck? Deck = null);

/// <summary>The table a lesson starts from. Provinces are filled and face down, hands are empty and the match stops before the first turn (<see cref="MatchEngine.TutorialBeginTurn"/>);
/// a lesson that needs more (a Ring already in play, a destroyed Province, a hand) does it from its steps' <see cref="TutorialStep.Then"/>.
/// <see cref="You"/> is the seat the player takes; <see cref="FirstPlayer"/> is whoever goes first, 0 being you.</summary>
internal sealed record TutorialSetup(TutorialSide You, TutorialSide Foe, int FirstPlayer = 0);

/// <summary>What the opponent does when a step lets it play: which Personalities it recruits with the Clan Discount, which cards it recruits at full cost (a Holding, whatever is free),
/// and the abilities it uses on a target (<c>(ability id, definition id of the card it targets)</c>). Anything else it declines or passes.</summary>
internal sealed record TutorialFoeScript(string[] RecruitWithDiscount, string[] Recruit, (string Ability, string Target)[] Abilities)
{
    public static readonly TutorialFoeScript Passive = new([], [], []);
}

/// <summary>A lesson: where it starts, what it says and what the opponent does. Every lesson stands alone (nothing is locked behind another), so each builds its own table.</summary>
/// <param name="ProvincesStep">The step that lays out the Provinces; the notes before it are read on an empty cloth. Zero when the table is shown from the start.</param>
internal sealed record TutorialLesson(string Id, string Title, TutorialSetup Setup, TutorialStep[] Steps, TutorialFoeScript Foe, int ProvincesStep = 0);

/// <summary>Every lesson, in the order the menu suggests. The menu never locks one.</summary>
internal static class TutorialLessons
{
    public static readonly TutorialLesson[] All = [TutorialLessonOne.Lesson, TutorialLessonDynasty.Lesson];
    public static TutorialLesson Default => All[0];
    public static TutorialLesson Get(string id) => All.Single(l => l.Id == id);
}
