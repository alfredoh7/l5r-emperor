namespace L5R.Core;

// Strength in Terror (oracle-l5r-7538): "...You may target and destroy one of the Personality's attachments. Lose 3 Honor." The destroy is a "may": once the Force reduction
// has resolved, the acting player is offered one choice per attachment of the target (the engine-listed effect choice every other optional card uses, EffectChoice.cs) and a
// way to decline. Nothing is asked when the target has no attachments. A command that already names the attachment (CardIds, the older path) skips the question.
public sealed partial class MatchEngine
{
    private const string StrengthInTerrorDestroyEffect = "sit-destroy:";
    private const string StrengthInTerrorDecline = "sit-decline";

    // True when the action paused for the acting player's choice.
    private bool BeginStrengthInTerrorChoice(GameCommand command, CardInstance source, CardInstance target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        if (!InPlay(target)) return false;
        var options = Attachments(target.Id).OrderBy(a => a.Id, StringComparer.Ordinal)
            .Select(a => new EffectOption(StrengthInTerrorDestroyEffect + a.Id, $"Destroy {Definition(a).Name} ({a.Id})", [StrengthInTerrorDestroyEffect + a.Id])).ToList();
        if (options.Count == 0) return false;
        options.Add(new(StrengthInTerrorDecline, "Do not destroy an attachment", []));
        _state.PendingEffectChoice = new()
        {
            Command = command, PlayerId = command.PlayerId, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
            OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList(), Options = options
        };
        _state.PendingChoice = new("effectChoice", command.PlayerId, source.Id, $"{Definition(source).Name}: you may destroy one of {Definition(target).Name}'s attachments.");
        _state.PriorityPlayerId = command.PlayerId; _state.ConsecutivePasses = 0;
        return true;
    }

    private bool StrengthInTerrorBranchEffect(string effect)
    {
        if (!effect.StartsWith(StrengthInTerrorDestroyEffect, StringComparison.Ordinal)) return false;
        if (_state.Cards.TryGetValue(effect[StrengthInTerrorDestroyEffect.Length..], out var attachment) && attachment.Zone == Zone.Attached) DestroyAttachment(attachment);
        return true;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterStrengthInTerrorHooks()
    {
        EngineHooks.BranchEffects.Register("StrengthInTerrorBranchEffect", 15400, static (engine, effect, _, _, _) => engine.StrengthInTerrorBranchEffect(effect));
    }
}
