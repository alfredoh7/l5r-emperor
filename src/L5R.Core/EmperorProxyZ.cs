namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), proxy group Z: personality-wave-78 -- Kitsune Denhei and Shosuro Rokuta, blocked until now only because the Proxy each creates had no local
// scan, and the two Proxies themselves (Denhei's Snake, oracle-l5r-10002; Rokuta's Apprentice, oracle-l5r-10013). Everything reuses an existing primitive; what is new is small:
//   * Denhei: Kitsu Kagako's entering-play Create, of the Snake. The Snake's "Open: Target a Personality: He is bitten. His base abilities may not be used." is Kitsuki Yoyugi's lock of
//     every printed action of the target for the turn ("He is bitten" is flavour that names no rule of its own; the duration is the turn, the core-rules default for a lock like Yoyugi's).
//   * Rokuta: one printed Reaction taken by the dead Personality himself (Hida Yamadera - exp's FromDead shape), in two halves that share one use (a UsageKey, Horume's shape) because the
//     two moments have different triggers: after the resolution of an action from a card another player owns that destroyed him (the after-action-resolved machinery), and after a
//     battle's resolution that destroyed him (PendingAfterResolution.DestroyedCardIds, added by group D for Words Have Strength). Both create the Apprentice in the controller's home and then
//     make the chosen player lose 3 Honor (Celestial Sword of the Scorpion's "choose a player" as a command's TargetPlayerId).
public static class EmperorProxyZAbilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorProxyZ(this Dictionary<string, AbilityDefinition> all)
    {
        // Kitsune Denhei (oracle-l5r-4456): "Reaction: After Denhei enters play: Create a 2F/2C/3PH Mantis Clan - Nonhuman - Snake - Spirit Personality with the ability, 'Open: Target a
        // Personality: He is bitten. His base abilities may not be used.'"
        all["denhei-create"] = new("Reaction", "After Denhei enters play: Create a 2F/2C/3PH Mantis Clan · Nonhuman · Snake · Spirit Personality", "none", Trigger: "own-enters-play",
            Creates: new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3, Keywords: ["Mantis Clan", "Nonhuman", "Snake", "Spirit"], ProxyId: "oracle-l5r-10002"));
        // Denhei's Snake (oracle-l5r-10002): "Open: Target a Personality: He is bitten. His base abilities may not be used."
        all["denhei-snake-bite"] = new("Open", "Target a Personality: He is bitten. His base abilities may not be used", "personality");
        // Shosuro Rokuta (oracle-l5r-7156): "Reaction: After the resolution of a battle, or an action or trait from a card that another player owns, if the resolution or card destroyed
        // Rokuta: Create a 0F/2C/1PH Scorpion Clan - Courtier - Apprentice Personality (in your home). Choose a player, who loses 3 Honor."
        var apprentice = new CreatedCardSpec(CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 1, Keywords: ["Scorpion Clan", "Courtier", "Apprentice"], ProxyId: "oracle-l5r-10013");
        all["rokuta-action-create"] = new("Reaction", "After the resolution of an action from a card that another player owns, if the card destroyed Rokuta: Create a 0F/2C/1PH Scorpion Clan · Courtier · Apprentice Personality (in your home). Choose a player, who loses 3 Honor",
            "player", Trigger: AfterActionReactionTriggers.AfterActionResolved, FromDead: true, UsageKey: "rokuta-create", Creates: apprentice);
        all["rokuta-battle-create"] = new("Reaction", "After the resolution of a battle, if the resolution destroyed Rokuta: Create a 0F/2C/1PH Scorpion Clan · Courtier · Apprentice Personality (in your home). Choose a player, who loses 3 Honor",
            "player", Trigger: ReactionTriggers.AfterBattleResolution, FromDead: true, UsageKey: "rokuta-create", Creates: apprentice);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // True when the action paused (a created card's entering-play window) and finishes from there.
    private bool RunEmperorProxyZEffect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case "denhei-create":
                return ExecuteCreate(ability, ability.Creates!, command, player);
            case "denhei-snake-bite":
                LockPrintedAbilities(target!, Definition(source!).Name);
                return false;
            // Rokuta: the Apprentice is created in his controller's home, then the chosen player loses 3 Honor (an Honor loss, not a gain by his own controller: no rehonor substitution).
            case "rokuta-action-create":
            case "rokuta-battle-create":
                return ExecuteCreate(ability, ability.Creates!, command, player, () => ApplyPlayerHonorChoice(source!, command.TargetPlayerId!.Value, -3));
            default:
                throw new InvalidOperationException($"Unknown proxy group Z ability '{id}'.");
        }
    }

    // "After the resolution of an action ... from a card that another player owns, if the card destroyed Rokuta": any timing, either sort of card, read off the last resolved action's own
    // destroyed list. Whose card it is is its original owner (a card whose control changed is still its owner's); an action with no source card (a player ability) is judged by its player.
    private bool RokutaActionApplies(CardInstance rokuta, int playerId, ResolvedActionFacts facts) =>
        rokuta.Zone == Zone.Dead && facts.Affected.Any(a => a.Kind == DestroyedKind && a.CardId == rokuta.Id)
        && (facts.SourceId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var actionCard) ? (actionCard.OriginalOwnerId ?? actionCard.OwnerId) != playerId : facts.PlayerId != playerId);

    // "After the resolution of a battle, if the resolution destroyed Rokuta" (AfterResolutionApplies).
    private static bool RokutaBattleApplies(CardInstance rokuta, PendingAfterResolution facts) => rokuta.Zone == Zone.Dead && facts.DestroyedCardIds.Contains(rokuta.Id);
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorProxyZ_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorProxyZ", 1300, static abilities => abilities.AddEmperorProxyZ());
    }
}
