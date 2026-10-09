using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #771: Journey's End Keep - exp (oracle-l5r-10406; profile stronghold-wave-56, tools/content/package_wave_771.py) and the Imperial Clan. Readings for Mendon to confirm or correct are in
// docs/decisions/2026-10-08-journeys-end-keep-and-imperial-clan.md. The card:
//
//   "'Imperial' is a Clan alignment, and it is your Clan alignment. You may not buy Personalities with Clan discount if Imperial is their only Clan alignment. If no other player has an Imperial Stronghold,
//    you begin the game with the Imperial Favor."  /  (Oracle text) "When this Stronghold produces Gold, you may not Recruit Personalities until the phase ends or until after you have spent 5 total Gold for
//    non-Recruit costs or payments."
//
//   Imperial, the 10th Clan   The profile's Clan is Imperial, so the player is LISTED as Imperial (player.Clan, the placard, the battle placard). While a Journey's End Keep is a Stronghold in the match, every
//                             Personality with the Imperial keyword has the Imperial Clan alignment (StaticClanAlignments), so it is aligned to the JEK player for Loyal, Proclaim, the Clan discount and the
//                             Clan allegiance without a line in any of them: QuotePurchase's `inClan` already reads ClanAlignments against the player's Clan.
//   Proclaim / allegiance      follow from that: an Imperial Personality is Proclaim-eligible at full price, and may be recruited past the Honor Requirement for 2 more Gold (Clan allegiance).
//   Clan discount bar          ImperialBarsClanDiscount: the JEK player cannot buy a Personality whose ONLY alignment is Imperial with the Clan discount (QuotePurchase's error chain). One with another alignment
//                             (Akodo Uehara - exp, Bayushi Makubesu, Daidoji Narizane, Utaku Mai) can: the discount stands on his additional affiliation.
//   The Favor at setup         ApplyImperialSetup (static, as Foothold's: the engine does not exist yet): a JEK player begins with the Imperial Favor unless the other player's Stronghold also has the Imperial keyword.
//   The Gold lock              MatchState.Wave771Locks. When JEK produces Gold for a payment the player's lock for that turn and phase starts, and every non-Recruit payment counts the Gold its producers made (the cost PLUS any
//                             excess: JEK paying a 3 Gold Holding spends all 5) until 5 have been spent; a RecruitGate refuses a Personality recruit while a lock is open. A Holding is not a Personality, so buying one is a
//                             non-Recruit payment.
public static class Wave771Abilities
{
    public const string Effect = "w771-journeys-end-keep";
    public const string ImperialClan = "Imperial";
    /// <summary>The Gold that lifts the lock ("spent 5 total Gold for non-Recruit costs or payments").</summary>
    public const int LockGold = 5;
}

public sealed partial class MatchState
{
    // 23000-23009 is reserved for the Journey's End Keep wave (issue #771). Omitted from the saved game until used.
    /// <summary>Each Journey's End Keep player's Gold lock, by player id: [turn, phase, Gold spent on non-Recruit payments since the Stronghold produced Gold]. Replaced when a later phase starts one.</summary>
    [JsonPropertyOrder(23000)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, int[]>? Wave771Locks { get; set; }
}

public sealed partial class MatchEngine
{
    private static bool IsJourneysEndKeepStronghold(MatchState state, PlayerState player) =>
        player.StrongholdId is { Length: > 0 } && state.Cards.TryGetValue(player.StrongholdId, out var card) && CardCatalog.Get(card.DefinitionId).Effect == Wave771Abilities.Effect;

    private static bool HasImperialKeyword(MatchState state, PlayerState player) =>
        player.StrongholdId is { Length: > 0 } && state.Cards.TryGetValue(player.StrongholdId, out var card) && (CardCatalog.Get(card.DefinitionId).Keywords ?? []).Contains("Imperial", StringComparer.Ordinal);

    /// <summary>"If no other player has an Imperial Stronghold, you begin the game with the Imperial Favor." Setup only: it is not an action or a Reaction and emits nothing. A Journey's End Keep player takes the Favor
    /// unless the other player's Stronghold has the Imperial keyword too (then neither does).</summary>
    internal static void ApplyImperialSetup(MatchState state)
    {
        foreach (var player in state.Players)
            if (IsJourneysEndKeepStronghold(state, player) && !state.Players.Any(other => other.Id != player.Id && HasImperialKeyword(state, other))) state.FavorPlayerId = player.Id;
    }

    /// <summary>Whether any player's Stronghold is a Journey's End Keep: Imperial is a Clan for the whole match (the 10th Clan).</summary>
    private bool ImperialClanInPlay() => _state.Players.Any(p => IsJourneysEndKeepStronghold(_state, p));

    /// <summary>The Clan alignment a Personality with the Imperial keyword gains while Imperial is a Clan (StaticClanAlignments).</summary>
    private string[] ImperialClanAlignment(CardInstance card) =>
        ImperialClanInPlay() && Definition(card).Type == CardType.Personality && Keyword(card, "Imperial") ? [Wave771Abilities.ImperialClan] : [];

    /// <summary>"You may not buy Personalities with Clan discount if Imperial is their only Clan alignment": asked of the buyer, only for a Journey's End Keep player.</summary>
    private bool ImperialBarsClanDiscount(CardInstance card, int playerId) =>
        IsJourneysEndKeepStronghold(_state, Player(playerId)) && ClanAlignments(card) is [{ } only] && only == Wave771Abilities.ImperialClan;

    // ---- the Gold lock ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private int[]? Wave771Lock(int playerId) =>
        _state.Wave771Locks is { } locks && locks.TryGetValue(playerId, out var entry) && entry[0] == _state.TurnNumber && entry[1] == (int)_state.Phase ? entry : null;

    // Every payment: JEK producing Gold opens the lock (this payment's own Gold counts), and while it is open each payment that is not the recruit of a Personality adds the Gold its producers made.
    private void RecordWave771Payment(AfterPaymentContext context)
    {
        var player = Player(context.PlayerId);
        if (!IsJourneysEndKeepStronghold(_state, player) && Wave771Lock(context.PlayerId) is null) return;
        var lockEntry = Wave771Lock(context.PlayerId);
        if (lockEntry is null && context.ProducerIds.Contains(player.StrongholdId, StringComparer.Ordinal))
        {
            lockEntry = [_state.TurnNumber, (int)_state.Phase, 0];
            (_state.Wave771Locks ??= new())[context.PlayerId] = lockEntry;
        }
        if (lockEntry is null) return;
        var recruitsPersonality = context.Subject is { Kind: PaymentKind.Recruit } subject && Definition(subject.Card).Type == CardType.Personality;
        if (!recruitsPersonality) lockEntry[2] += Math.Max(context.Cost, context.Produced);
    }

    private RecruitGateResult? Wave771GoldLockGate(RecruitGateContext context) =>
        context.Kind is RecruitGateKind.Recruit or RecruitGateKind.Reserve && Wave771Lock(context.PlayerId) is { } entry && entry[2] < Wave771Abilities.LockGold
            ? new RecruitGateResult($"Journey's End Keep produced Gold: no Personality can be recruited until the phase ends or {Wave771Abilities.LockGold} total Gold has been spent on other costs ({entry[2]} so far).")
            : null;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave771Hooks()
    {
        EngineHooks.AfterPayment.Register("Wave771GoldLockLedger", 23000, static (engine, context) => engine.RecordWave771Payment(context));
        EngineHooks.RecruitGate.Register("Wave771GoldLock", 23000, static (engine, context) => engine.Wave771GoldLockGate(context));
    }
}
