using System.Text.RegularExpressions;

namespace L5R.Core;

// Primitive P3 (issue #695, docs/decisions/2026-10-07-695-p3-recruit-gates.md): recruitment gates and Proclaim eligibility.
//
// Before this file the rules that decide WHO may bring a Personality into play were four hard-coded lines: Loyal (LoyaltyAllowsControl), one Effect that refuses a Clan (ControlRefusedByClan), the Honor
// Requirement and the Clan waiver printed on the card (HonorRequirementWaiverClan), and Proclaim eligibility being only "the Personality is in your Clan". A card that prints "will not join a player who controls a
// Human Personality", "Crab players may control him", "Crane players may Proclaim him" or "ignore his Honor Requirement while you control the Imperial Favor" had nowhere to say so. One registered channel is that place.
//
//   EngineHooks.RecruitGate   asked by QuotePurchase (kind Recruit / Reserve: its error chain, the Honor Requirement and Proclaim eligibility), by LoyaltyAllowsControl (kind ControlChange, or Overlay from
//                             QuoteOverlay: so TransferUnitControl, an overlay, a created card and every other reader of Loyal obey it) and by ProclaimAllowed (kind Proclaim). Every callback is asked every
//                             time; the answers are merged: the first refusal wins, every waiver / eligibility adds. A callback returns null when it has nothing to say.
//
// Not changed: an Experienced overlay never meets the Honor Requirement (QuoteOverlay does not read it), so IgnoreHonorRequirement is only ever read on a Recruit or a Reserve recruit; and Proclaim stays
// incompatible with the Clan discount (QuotePurchase's `!clanDiscount`). The player-level "ignores Honor Requirements for the rest of the game" flag is the existing PlayerState.IgnoresHonorRequirements.

/// <summary>What is being decided: a Recruit from a province (or a dead pile / deck), a Reserve recruit, an Experienced overlay, a change of control, or the Proclaim that follows an entry.</summary>
internal enum RecruitGateKind
{
    Recruit,
    Reserve,
    Overlay,
    ControlChange,
    Proclaim
}

/// <summary>The Personality, the player who would control / Proclaim him, that player's Clan (the Clan asked about, which is the player's own except where the caller names another), and the circumstance.</summary>
internal sealed record RecruitGateContext(CardInstance Card, int PlayerId, string Clan, RecruitGateKind Kind);

/// <summary>A refusal (the reason becomes the quote's Error), a waiver of the Honor Requirement, Proclaim eligibility for a Clan that is not the Personality's own, a waiver of Loyal for this player.</summary>
internal sealed record RecruitGateResult(string? RefuseReason = null, bool IgnoreHonorRequirement = false, bool ProclaimEligible = false, bool LoyalWaived = false)
{
    internal static readonly RecruitGateResult None = new();
}

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<MatchEngine, RecruitGateContext, RecruitGateResult?>> RecruitGate = new();
}

public sealed partial class MatchEngine
{
    // "will not join a <Clan> player" (any Clan), "<Clan> players may control him" (waives Loyal), "<Clan> players may Proclaim him", and the conditions below. Names are Effects of a card.
    private static readonly Regex WillNotJoinClanEffect = new(@"^will-not-join-([a-z]+)-player$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ClanMayControlEffect = new(@"^([a-z]+)-players-may-control$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ClanMayProclaimEffect = new(@"^([a-z]+)-players-may-proclaim$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    internal const string WillNotJoinHumanControllerEffect = "will-not-join-human-controller";
    internal const string WillNotEnterWhileHonorNonNegativeEffect = "will-not-enter-honor-nonnegative";
    internal const string FavorIgnoresHonorRequirementEffect = "favor-ignores-honor-requirement";
    // QuotePurchase said exactly this about Tanemono before his rule moved onto the channel (SeedsOfDecayBatch3.cs); the same words keep that quote byte-identical.
    internal const string LoyalRecruitRefusal = "Loyal Personalities can only be recruited by a Clan they are aligned with.";

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterRecruitGates695Hooks()
    {
        EngineHooks.RecruitGate.Register("P3ClanWillNotJoin", 16400, static (engine, context) => engine.WillNotJoinClanGate(context));
        EngineHooks.RecruitGate.Register("P3HumanControllerWillNotJoin", 16400, static (engine, context) => engine.WillNotJoinHumanGate(context));
        EngineHooks.RecruitGate.Register("P3HonorNonNegativeWillNotEnter", 16400, static (engine, context) => engine.WillNotEnterWhileHonorGate(context));
        EngineHooks.RecruitGate.Register("P3ClanMayControlOrProclaim", 16400, static (engine, context) => engine.ClanMayGate(context));
        EngineHooks.RecruitGate.Register("P3FavorIgnoresHonorRequirement", 16400, static (engine, context) => engine.FavorIgnoresHonorRequirementGate(context));
    }

    /// <summary>The merged answer of every registered gate for this card and player. A non-Personality is never gated.</summary>
    private RecruitGateResult RecruitGateFor(CardInstance card, int playerId, string clan, RecruitGateKind kind)
    {
        if (Definition(card).Type != CardType.Personality) return RecruitGateResult.None;
        var context = new RecruitGateContext(card, playerId, clan, kind);
        string? refuse = null; var ignoreHonor = false; var proclaim = false; var loyalWaived = false;
        foreach (var hook in EngineHooks.RecruitGate.Items)
        {
            if (hook(this, context) is not { } answer) continue;
            refuse ??= answer.RefuseReason; ignoreHonor |= answer.IgnoreHonorRequirement; proclaim |= answer.ProclaimEligible; loyalWaived |= answer.LoyalWaived;
        }
        return refuse is null && !ignoreHonor && !proclaim && !loyalWaived ? RecruitGateResult.None : new RecruitGateResult(refuse, ignoreHonor, proclaim, loyalWaived);
    }

    /// <summary>The Proclaim half of the gate: ProclaimAllowed reads its refusal, QuotePurchase its extra-Clan eligibility.</summary>
    private RecruitGateResult ProclaimGateFor(CardInstance card, PlayerState player) => RecruitGateFor(card, player.Id, player.Clan, RecruitGateKind.Proclaim);
    private bool ProclaimGateEligible(CardInstance card, PlayerState player) => ProclaimGateFor(card, player) is { RefuseReason: null, ProclaimEligible: true };

    // "[Card] will not join a [Clan] player": a Loyal-like refusal keyed on the controlling player's Clan, read wherever Loyal is (recruiting, overlaying, a change of control).
    private RecruitGateResult? WillNotJoinClanGate(RecruitGateContext context)
    {
        var effect = Definition(context.Card).Effect;
        if (string.IsNullOrEmpty(effect) || context.Kind == RecruitGateKind.Proclaim) return null;
        var match = WillNotJoinClanEffect.Match(effect);
        return match.Success && context.Clan == ClanNamed(match.Groups[1].Value) ? new RecruitGateResult($"{Definition(context.Card).Name} will not join a {context.Clan} player.") : null;
    }

    // "Will not join a player who controls a Human Personality": a Recruit, a Reserve recruit or a change of control (an overlay replaces his own title, so it is never refused for this).
    private RecruitGateResult? WillNotJoinHumanGate(RecruitGateContext context)
    {
        if (Definition(context.Card).Effect != WillNotJoinHumanControllerEffect || context.Kind is RecruitGateKind.Overlay or RecruitGateKind.Proclaim) return null;
        var human = _state.Cards.Values.Any(c => c.Id != context.Card.Id && c.OwnerId == context.PlayerId && InPlay(c) && Definition(c).Type == CardType.Personality && KeywordOrHuman(c, "Human"));
        return human ? new RecruitGateResult($"{Definition(context.Card).Name} will not join a player who controls a Human Personality.") : null;
    }

    // "Will not enter play if your Family Honor is 0 or above": only an entry from a province (Recruit, Reserve); control of a card already in play is not an entry.
    private RecruitGateResult? WillNotEnterWhileHonorGate(RecruitGateContext context) =>
        Definition(context.Card).Effect == WillNotEnterWhileHonorNonNegativeEffect && context.Kind is RecruitGateKind.Recruit or RecruitGateKind.Reserve && Player(context.PlayerId).Honor >= 0
            ? new RecruitGateResult($"{Definition(context.Card).Name} will not enter play while your Family Honor is 0 or above.") : null;

    // "<Clan> players may control him" (Loyal is waived for that Clan's player) and "<Clan> players may Proclaim him" (Proclaim eligibility for that Clan, besides his own).
    private RecruitGateResult? ClanMayGate(RecruitGateContext context)
    {
        var effect = Definition(context.Card).Effect;
        if (string.IsNullOrEmpty(effect)) return null;
        if (context.Kind != RecruitGateKind.Proclaim && ClanMayControlEffect.Match(effect) is { Success: true } control && context.Clan == ClanNamed(control.Groups[1].Value)) return new RecruitGateResult(LoyalWaived: true);
        if (context.Kind == RecruitGateKind.Proclaim && ClanMayProclaimEffect.Match(effect) is { Success: true } proclaim && context.Clan == ClanNamed(proclaim.Groups[1].Value)) return new RecruitGateResult(ProclaimEligible: true);
        return null;
    }

    // "Ignore his Honor Requirement while you control the Imperial Favor": a Recruit or a Reserve recruit by the player who holds it now.
    private RecruitGateResult? FavorIgnoresHonorRequirementGate(RecruitGateContext context) =>
        Definition(context.Card).Effect == FavorIgnoresHonorRequirementEffect && context.Kind is RecruitGateKind.Recruit or RecruitGateKind.Reserve && _state.FavorPlayerId == context.PlayerId
            ? new RecruitGateResult(IgnoreHonorRequirement: true) : null;

    private static string ClanNamed(string lower) => char.ToUpperInvariant(lower[0]) + lower[1..];

    /// <summary>"He may ignore Honor Requirements when bringing Personalities into play until the game ends" (Delicate Gamble, Alter History): the existing player-level flag, set with its event.</summary>
    internal void IgnoreHonorRequirementsForRestOfGame(int playerId, string via)
    {
        var player = Player(playerId);
        if (player.IgnoresHonorRequirements) return;
        player.IgnoresHonorRequirements = true;
        Emit("honorRequirementsIgnored", $"{via}: {player.Name} may ignore Honor Requirements when bringing Personalities into play until the game ends.", playerId);
    }

    // Test seam: the whole-unit control change a take effect makes (TransferUnitControl), so a gate is proven on the real path.
    internal bool TransferUnitControlForTest(string cardId, int controllerId) => TransferUnitControl(Card(cardId), controllerId, "test", checkLoyal: true, fromEffect: false);
}
