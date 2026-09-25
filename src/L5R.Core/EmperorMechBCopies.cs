namespace L5R.Core;

// Issue #501, Mendon's item 32 (engine side; the client's "duplicate" frame and its tooltip are Alfredo's, issue #502):
//
//   "A copied card needs a frame indicating that it is copied ... when the card is duplicated place the copy inside the frame. Mousing over the frame should produce a tooltip
//    indicating any conditions on the copy (such as timed destruction). The copy should be a duplicate of the scan of the card being copied."
//
// Every copy exposes in CardView: IsCopy, CopyOfCardId (the original instance, null when unknown), CopyOfDefinitionId (whose scan the client shows) and CopyConditions (one
// human-readable string). A card that makes a copy calls MarkAsCopy (with any condition sentence it imposes, e.g. "Removed from the game when your next turn begins."); a timed
// effect added later calls AddCopyCondition. Conditions the engine already knows are added on the fly: a created copy leaves the game instead of going to a pile, and a card
// created until the battle ends is removed then.
public sealed partial class MatchEngine
{
    /// <summary>Marks <paramref name="copy"/> as a copy of <paramref name="original"/> (its scan is the original's) with any conditions the copying card imposes.</summary>
    private void MarkAsCopy(CardInstance copy, CardInstance original, params string[] conditions)
    {
        copy.CopyOfCardId = original.CopyOfCardId ?? original.Id; // a copy of a copy shows the first original's scan
        copy.CopyOfDefinitionId = original.CopyOfDefinitionId ?? original.DefinitionId;
        foreach (var condition in conditions) AddCopyCondition(copy, condition);
    }

    /// <summary>Adds one human-readable condition to a copy's tooltip (a sentence, ending with a full stop); a repeated condition is kept once.</summary>
    private static void AddCopyCondition(CardInstance copy, string condition)
    {
        if (!string.IsNullOrWhiteSpace(condition) && !copy.CopyConditions.Contains(condition, StringComparer.Ordinal)) copy.CopyConditions.Add(condition);
    }

    // Test seam: mark a card as a copy exactly as a copying card does. Never used outside tests.
    internal void MarkAsCopyForTest(string copyId, string originalId, params string[] conditions) => MarkAsCopy(Card(copyId), Card(originalId), conditions);

    private static bool IsCopyCard(CardInstance card) => card.CreatedCopy || card.CopyOfDefinitionId is not null;

    private static string? CopyOfDefinition(CardInstance card) => card.CopyOfDefinitionId ?? (card.CreatedCopy ? card.DefinitionId : null);

    // The tooltip: the copying card's own conditions first, then what the engine enforces on every copy of this kind.
    private string? CopyConditionsText(CardInstance card)
    {
        if (!IsCopyCard(card)) return null;
        var conditions = new List<string>(card.CopyConditions);
        if (_state.CreatedUntilBattleEndsIds.Contains(card.Id)) conditions.Add("Removed from the game when this battle ends.");
        if (IsCreatedCard(card)) conditions.Add("A created copy: it is removed from the game if it leaves play.");
        return conditions.Count == 0 ? "A copy." : string.Join(" ", conditions);
    }
}
