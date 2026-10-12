using System.Reflection;
using System.Text.Json;
using L5R.Core;

// Issue #850 (stability): a card that leaves play sheds its time in play in ONE place, MatchEngine.ResetCardState. This test makes that complete by construction: every CardInstance property is
// either listed below as identity / location / bookkeeping the reset must keep, or it must be back at its default value after the reset. A new per-play field added to CardInstance fails here until
// someone decides which it is -- the way a stale stamp used to survive a trip through a hand or a deck.
internal static class CardResetTests
{
    private static readonly HashSet<string> Kept = new(StringComparer.Ordinal)
    {
        "Id", "DefinitionId", "Printed", "OwnerId", "OriginalOwnerId", "UnitNumber",      // identity and control
        "Zone", "ProvinceIndex", "FaceUp", "AttachedTo", "HostCardId", "LastHostId",      // location, set by the move itself
        "EnteredPlayTurn",                                                                 // stamped and cleared by the post-command sweep
        "CopiedSpellRules", "CreatedCopy", "CopyOfCardId", "CopyOfDefinitionId", "CopyConditions", "ExpiresAtTurnsTaken", "ExpiresAtEventsPhaseTurnsTaken", // created copies: owned by their own sweeps
    };

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [("Leave play: ResetCardState returns every per-play CardInstance property to its default", ResetCoversEveryField)];
        runner.Run("card-reset", tests);
    }

    private static void Dirty(object card, PropertyInfo p)
    {
        var t = p.PropertyType; var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u == typeof(bool)) p.SetValue(card, true);
        else if (u == typeof(int)) p.SetValue(card, 7);
        else if (u == typeof(string)) p.SetValue(card, "x");
        else if (t.IsArray) { /* fixed-size flags: left as they are */ }
        else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            var item = t.GetGenericArguments()[0];
            var list = (System.Collections.IList)p.GetValue(card)!;
            list.Add(item == typeof(string) ? "x" : System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(item));
        }
        else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)) { var d = (System.Collections.IDictionary)p.GetValue(card)!; d.Add("x", Activator.CreateInstance(t.GetGenericArguments()[1])!); }
        else throw new Exception($"CardResetTests cannot dirty {p.Name} ({t.Name}); extend Dirty or add it to Kept.");
    }

    private static void ResetCoversEveryField()
    {
        var props = typeof(CardInstance).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && !Kept.Contains(p.Name)).ToList();
        var card = new CardInstance();
        foreach (var p in props) Dirty(card, p);
        MatchEngine.ResetCardStateForTest(card);
        var fresh = new CardInstance();
        var left = props.Where(p => JsonSerializer.Serialize(p.GetValue(card)) != JsonSerializer.Serialize(p.GetValue(fresh))).Select(p => p.Name).ToList();
        if (left.Count > 0) throw new Exception("ResetCardState leaves these per-play properties set (reset them, or list them as kept): " + string.Join(", ", left));
    }
}
