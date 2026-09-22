using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation of the authoritative grant view, shared by hover and the scrollable inspector.</summary>
public static class GrantedAbilityText
{
    public static string Describe(GrantedAbilityView grant)
    {
        var duration = grant.Duration switch
        {
            GrantDuration.Permanent => "Permanent",
            GrantDuration.Turn => "Until this turn ends",
            GrantDuration.Battle => "Until this battle ends",
            GrantDuration.Attachment => "While the granting attachment remains attached",
            GrantDuration.Copy => "Until the copying ability is used again",
            _ => grant.Duration
        };
        var usage = grant.UsesPerTurn switch
        {
            null => "",
            AbilityUseLimit.Unlimited => "\nUnlimited uses per turn",
            var limit => $"\n{grant.UsesRemaining} of {limit} uses remaining this turn"
        };
        return $"{(grant.CopiedFrom is null ? "Granted" : "Copied")} ability · {duration}\n{grant.Timing}: {grant.Label}\nSource: {grant.SourceName ?? grant.CopiedFromName ?? grant.Source}{usage}";
    }
}
