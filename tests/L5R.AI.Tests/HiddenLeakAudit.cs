using System.Text.Json;
using L5R.Core;

/// <summary>
/// Issue #1027: while two bots play a soak match, checks that nothing a seat is shown names a card that seat has never been shown. After every accepted command, for each seat: every card with a
/// printed Definition anywhere in that seat's <see cref="MatchView"/> is recorded as seen; then each new event, and the pending decision's prompt, that carries a card id the seat has not seen
/// yet but whose message contains that card's printed name is a leak (the shared event log names a hidden card; the Wave739 toDeck bug of #1026 was one). A card the seat saw earlier may be named
/// again (a face-up card returned to a deck is public). Limits: only events and prompts that carry a CardId are covered, and a name revealed and re-hidden inside one command would be a false positive.
/// </summary>
internal sealed class HiddenLeakAudit
{
    private readonly HashSet<string>[] _seen = [new(StringComparer.Ordinal), new(StringComparer.Ordinal)];
    private readonly long[] _lastEvent = [0, 0];

    /// <summary>
    /// Events that name a card because the rules make that public: an announced Reaction ("X will play Y" is said aloud, and "does not play Y after all" follows it), a card a player
    /// shows or gives away, a Province card revealed, and the "before this battle resolves" effect of a card already announced. The card counts as seen from then on.
    /// </summary>
    private static readonly HashSet<string> PublicByRule = new(StringComparer.Ordinal) { "reactionAnnounced", "reactionSkipped", "eventChose", "beforeBattleEffect", "provinceRevealed" };

    /// <summary>Records what each seat can see now and returns a description of each leak found since the last call.</summary>
    public List<string> Check(MatchEngine game)
    {
        var leaks = new List<string>();
        for (var viewer = 0; viewer < 2; viewer++)
        {
            var view = game.GetView(viewer);
            Collect(JsonSerializer.SerializeToElement(view), _seen[viewer]);
            foreach (var e in view.Events.Where(e => e.Sequence > _lastEvent[viewer]))
            {
                _lastEvent[viewer] = Math.Max(_lastEvent[viewer], e.Sequence);
                if (PublicByRule.Contains(e.Type)) { if (e.CardId is not null) _seen[viewer].Add(e.CardId); continue; }
                if (Leaks(game, viewer, e.CardId, e.Message) is { } name) leaks.Add($"seat {viewer} was shown event #{e.Sequence} {e.Type} \"{e.Message}\" naming {name} ({e.CardId}), which it has not seen" + Debug(view));
            }
            if (view.PendingChoice is { } pending && Leaks(game, viewer, pending.CardId, pending.Prompt) is { } promptName)
                leaks.Add($"seat {viewer} was shown the {pending.Type} prompt \"{pending.Prompt}\" naming {promptName} ({pending.CardId}), which it has not seen");
        }
        return leaks;
    }

    private static string Debug(MatchView view) => Environment.GetEnvironmentVariable("L5R_LEAK_DEBUG") is null ? "" : " [p0 province2: " + JsonSerializer.Serialize(view.Players[0].Provinces[2].Card is { } c ? new { c.Id, c.FaceUp, Def = c.Definition?.Name } : null) + "; p1 province2: " + JsonSerializer.Serialize(view.Players[1].Provinces[2].Card is { } d ? new { d.Id, d.FaceUp, Def = d.Definition?.Name } : null) + "]";

    private string? Leaks(MatchEngine game, int viewer, string? cardId, string text)
    {
        if (cardId is null || _seen[viewer].Contains(cardId) || !game.State.Cards.TryGetValue(cardId, out var card)) return null;
        string name;
        try { name = CardCatalog.Get(card.DefinitionId).Name; } catch (Exception) { return null; }
        return name.Length > 0 && text.Contains(name, StringComparison.Ordinal) ? name : null;
    }

    /// <summary>Every JSON object that has an "id" and a non-null "definition" is a card the viewer can read.</summary>
    private static void Collect(JsonElement element, HashSet<string> seen)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string? id = null; var shown = false;
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("Id") || property.NameEquals("id")) { if (property.Value.ValueKind == JsonValueKind.String) id = property.Value.GetString(); }
                    else if ((property.NameEquals("Definition") || property.NameEquals("definition")) && property.Value.ValueKind == JsonValueKind.Object) shown = true;
                    Collect(property.Value, seen);
                }
                if (id is not null && shown) seen.Add(id);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Collect(item, seen);
                break;
        }
    }
}
