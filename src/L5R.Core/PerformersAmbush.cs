namespace L5R.Core;

// ======================================================================================================================================================================================
// LYING IN AMBUSH (issue #667, strategy-wave-114): "Reaction: After the resolution of your Battle, Limited, or Open action that created a non-Shadowlands Personality: Create a 2F/2C/2PH Samurai
// Personality with your Clan alignment at the same location."
//
// Mendon's ruling: it may be played when ANY Personality is created, as long as that Personality does not have the Shadowlands keyword (Awed Witness, Overlooked Palace, ...); it may be played whether or
// not the created Personality leaves play later (Two-Fold Virtue); and after an action that creates several Personalities as long as at least one of them is not Shadowlands.
//
//   * THE RECORD: CreatePersonality (the one site that makes a Personality) notes it in the running action's record (RecordActionEffect, kind "created"): the creating Personality's id, whether it has the
//     Shadowlands keyword (HostId marker, read AT creation so a Personality that leaves play later still counts), and where it was made (FromBattlefield: the battlefield, null for home). The record is
//     snapshotted into MatchState.LastResolvedAction when the action resolves, which the existing "after an action resolves" Reaction window reads (Closing the Gap's, Without Mercy's).
//   * THE WINDOW: the action is the Reacting player's own (ResolvedActionFacts.PlayerId), a Battle, Limited, Open (or Battle/Open) ability, and at least one Personality it created is not Shadowlands.
//   * THE SAMURAI: Ambusher (user-scan-ambusher), the scan Mendon supplied: a 2F/2C Samurai Personality whose Personal Honor prints as a dash (the card text says 2PH; the scan wins, as 0PH), with the creator's Clan alignment fixed at creation (ControllerClan). "At the same location": two catalogue entries
//     sharing one use, one that creates it at home and one at the current battlefield, each offered only when the first non-Shadowlands Personality was created there (several created at different
//     locations: the first). The Samurai's own creation is a Reaction's, so it does not itself enable a second Lying in Ambush.
// ======================================================================================================================================================================================

public static class PerformersAmbushAbilities
{
    public const string AmbushHome = "pf-lying-in-ambush-home", AmbushBattlefield = "pf-lying-in-ambush-battlefield";

    /// <summary>ResolvedActionFacts.Affected: a Personality the action created. Its HostId is <see cref="ShadowlandsMark"/> when it had the Shadowlands keyword as it was created.</summary>
    public const string CreatedKind = "created", ShadowlandsMark = "shadowlands";

    /// <summary>The scan the Samurai is: Ambusher (user-supplied by Mendon, personality-wave-127), a 2F/2C Samurai Personality; the Clan alignment is the spec's ("with your Clan alignment"), not the scan's.</summary>
    public const string AmbusherScan = "user-scan-ambusher";

    public static Dictionary<string, AbilityDefinition> AddPerformersAmbush(this Dictionary<string, AbilityDefinition> all)
    {
        var samurai = new CreatedCardSpec(CardType.Personality, Force: 2, Chi: 2, Keywords: ["Samurai"], ProxyId: AmbusherScan) { ControllerClan = true };
        all[AmbushHome] = new("Reaction", "After the resolution of your Battle, Limited, or Open action that created a non-Shadowlands Personality at home: Create a 2F/2C/2PH Samurai Personality with your Clan alignment there", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "pf-lying-in-ambush", Creates: samurai);
        all[AmbushBattlefield] = new("Reaction", "After the resolution of your Battle, Limited, or Open action that created a non-Shadowlands Personality at a battlefield: Create a 2F/2C/2PH Samurai Personality with your Clan alignment there", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "pf-lying-in-ambush", Creates: samurai with { AtBattlefield = true });
        return all;
    }
}

public sealed partial class MatchEngine
{
    // CreatePersonality's hook: the running action's record of what it created.
    private void NotePersonalityCreated(CardInstance created, int? battlefield) =>
        RecordActionEffect(created.Id, PerformersAmbushAbilities.CreatedKind, Keyword(created, "Shadowlands") || HasClanAffiliation(created, "Shadowlands") ? PerformersAmbushAbilities.ShadowlandsMark : null, battlefield);

    internal bool PerformersAmbushAppliesForTest(string cardId, string abilityId, int playerId, ResolvedActionFacts facts) => PerformersAmbushApplies(Card(cardId), abilityId, playerId, facts) ?? false;

    // The first Personality the resolved action created that is not Shadowlands, or null.
    private static AffectedCard? FirstNonShadowlandsCreated(ResolvedActionFacts facts) =>
        facts.Affected.FirstOrDefault(a => a.Kind == PerformersAmbushAbilities.CreatedKind && a.HostId != PerformersAmbushAbilities.ShadowlandsMark);

    // AfterActionResolvedApplies' arm. Null: not this feature's Reaction.
    private bool? PerformersAmbushApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        if (abilityId is not (PerformersAmbushAbilities.AmbushHome or PerformersAmbushAbilities.AmbushBattlefield)) return null;
        if (source.OwnerId != playerId || facts.PlayerId != playerId || source.Zone != Zone.Hand) return false;
        if (!AbilityCatalog.All.TryGetValue(facts.AbilityId, out var acting) || acting.Timing is not ("Battle" or "Limited" or "Open" or "BattleOrOpen")) return false;
        if (FirstNonShadowlandsCreated(facts) is not { } created) return false;
        // "At the same location": at a battlefield, which is the current battlefield; otherwise at home.
        return abilityId == PerformersAmbushAbilities.AmbushBattlefield ? created.FromBattlefield is { } where && _state.CurrentBattleProvince == where : created.FromBattlefield is null;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPerformersAmbushHooks()
    {
        EngineHooks.Abilities.Register("AddPerformersAmbush", 14610, static abilities => abilities.AddPerformersAmbush());
    }
}
