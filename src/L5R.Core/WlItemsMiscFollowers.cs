namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #674, CLUSTER "ITEMS-MISC" (follower-wave-48): Maddened Horde, Reconnaissance Scouts, Shinobi Vassal, Sycophants and The Bestial Ones, wired from the printed text of their scans.
//
//   MADDENED HORDE          "Battle: Discard the top card of any Fate deck. Melee Attack with strength equal to the card's Focus Value." The deck is the choice between two actions sharing one use (your
//                           own Fate deck, or the other player's: the siege-volley precedent for a choice printed inside one sentence); the discarded card's Focus Value is the Attack's strength. An empty deck
//                           is the "information that is not available" of the Coils of Madness supplement (rule 6): the discard and the Attack that depends on it both fail.
//   RECONNAISSANCE SCOUTS   "Engage: You have Reconnaissance at this battlefield. If you are the Attacker, you have the first opportunity to take a Battle action or pass. You may target and give an
//                           enemy Personality or Follower -3F." Engage is a Reaction to "after engaging" (supplement rule 3); "this battlefield" is the battlefield its Personality is at. "You may target"
//                           is the pair of actions sharing one use: one without the penalty's target, one with it (the first-opportunity claim and Reconnaissance are the same in both).
//   SHINOBI VASSAL          "Will only attach to a Ninja" is AttachRequiresKeyword; "You may Equip this Follower as a Battle action" is Fading Shadows' Equip (the performing Ninja at the battlefield
//                           is the wielder; the Follower leaves the hand for his unit, paying all costs); "Battle: Destroy a target enemy card without attachments. Destroy this Follower." destroys the target
//                           and then the Follower, the second a plain effect that happens whether or not the first did (supplement rule 6).
//   SYCOPHANTS              "Political Limited: Bow this card: Straighten this Personality. You may use his Political abilities that may only be used once per turn a second time (this turn)." The
//                           second use is one more use of every Political action of the Personality that is limited to once a turn (the ExtraUsesPerTurn channel, as Mempo of the Amethyst Champion).
//   THE BESTIAL ONES        "After this Follower enters play, lose 1 Honor and discard a card" is EntryHonorLoss and a queued forced discard of the controller's own hand; "Engage: Give a target enemy
//                           Personality or Follower -2F" is a Reaction to "after engaging".
// ======================================================================================================================================================================================

public static class WlItemsMiscFollowerAbilities
{
    public const string HordeOwn = "wl-horde-own", HordeEnemy = "wl-horde-enemy", ScoutsEngage = "wl-scouts-engage", ScoutsPenalty = "wl-scouts-engage-penalty", VassalEquip = "wl-vassal-equip",
        VassalDestroy = "wl-vassal-destroy", SycophantsStraighten = "wl-sycophants-straighten", BestialEngage = "wl-bestial-engage";
    /// <summary>The Effect key of The Bestial Ones' "discard a card" entry trait (CardDefinition.Effect).</summary>
    public const string BestialEntryEffect = "wl-bestial-entry-discard";
    /// <summary>The use mark Sycophants puts on its Personality (CardInstance.AbilityUses key; the turn number it was used).</summary>
    public const string SycophantsMark = "wl-sycophants:second-use";

    public static Dictionary<string, AbilityDefinition> AddWlItemsMiscFollowers(this Dictionary<string, AbilityDefinition> all)
    {
        // Maddened Horde (oracle-l5r-10518): "Battle: Discard the top card of any Fate deck. Melee Attack with strength equal to the card's Focus Value."
        all[HordeOwn] = new("Battle", "Discard the top card of your Fate deck. Melee Attack with strength equal to the card's Focus Value", "attack", UsageKey: "wl-horde");
        all[HordeEnemy] = new("Battle", "Discard the top card of the other player's Fate deck. Melee Attack with strength equal to the card's Focus Value", "attack", UsageKey: "wl-horde");
        // Reconnaissance Scouts (oracle-l5r-10519): "Engage: You have Reconnaissance at this battlefield. If you are the Attacker, you have the first opportunity to take a Battle action or pass. You may target
        // and give an enemy Personality or Follower -3F."
        all[ScoutsEngage] = new("Reaction", "Engage: You have Reconnaissance at this battlefield. If you are the Attacker, you have the first opportunity to take a Battle action or pass", "none",
            Trigger: "after-engaging-at-own-battlefield", UsageKey: "wl-scouts");
        all[ScoutsPenalty] = new("Reaction", "Engage: You have Reconnaissance at this battlefield. If you are the Attacker, you have the first opportunity to take a Battle action or pass. Target an enemy Personality or Follower: Give it -3F",
            "enemy-force", Trigger: "after-engaging-at-own-battlefield", UsageKey: "wl-scouts");
        // Shinobi Vassal (oracle-l5r-10517): "You may Equip this Follower as a Battle action." and "Battle: Destroy a target enemy card without attachments. Destroy this Follower."
        all[VassalEquip] = new("Battle", "You may Equip this Follower as a Battle action: target your performing Ninja and Equip it to him", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Ninja"], Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Self, Wielder: AttachWielder.Performer));
        all[VassalDestroy] = new("Battle", "Destroy a target enemy card without attachments. Destroy this Follower", "enemy-card-without-attachments");
        // Sycophants (oracle-l5r-10338): "Political Limited: Bow this card: Straighten this Personality. You may use his Political abilities that may only be used once per turn a second time (this turn)."
        all[SycophantsStraighten] = new("Limited", "Political Limited: Bow this card: Straighten this Personality. You may use his Political abilities that may only be used once per turn a second time (this turn)", "none",
            ActionKeywords: ["Political"], Costs: ActionCost.BowSource);
        // The Bestial Ones (oracle-l5r-10677): "Engage: Give a target enemy Personality or Follower -2F."
        all[BestialEngage] = new("Reaction", "Engage: Give a target enemy Personality or Follower -2F", "enemy-force", Trigger: "after-engaging-at-own-battlefield");
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- The Bestial Ones: the entry trait ---------------------------------------------------------------------------------------------------------------------------------------

    // ApplyMandatoryEntryEffects' hook: "After this Follower enters play, lose 1 Honor [its EntryHonorLoss] and discard a card" -- the controller's own discard, queued like any forced discard
    // (a Proclaim or entry window already open is never overwritten).
    private void ApplyWlItemsMiscEntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != WlItemsMiscFollowerAbilities.BestialEntryEffect) return;
        _state.PendingForcedDiscards.Add(new(playerId, Definition(card).Name));
        _state.P1.QueuedDiscards++; // the queue is asked as the action that brought the Follower into play hands priority on (NextAction), like Hida Osote's owed discards
    }

    // ---- Sycophants: a second use of a Personality's once-a-turn Political actions ----------------------------------------------------------------------------------------------------

    // ExtraUsesPerTurn's callback: the Personality that Sycophants straightened this turn may use each of its Political actions limited to once a turn one more time.
    private int WlItemsMiscExtraUses(Dictionary<string, int> uses, AbilityDefinition ability)
    {
        if (ability.UsesPerTurn != 1 || ability.OncePerGame || !uses.TryGetValue(WlItemsMiscFollowerAbilities.SycophantsMark, out var turn) || turn != _state.TurnNumber) return 0;
        var owner = _state.Cards.Values.FirstOrDefault(c => ReferenceEquals(c.AbilityUses, uses));
        return owner is not null && ActionCarriesKeyword(ability, owner, "Political") ? 1 : 0;
    }

    // Test seam (L5R.Core.Tests has InternalsVisibleTo): the extra uses of an action for one card's own use record.
    internal int WlItemsMiscExtraUsesForTest(string cardId, string abilityId) => WlItemsMiscExtraUses(Card(cardId).AbilityUses, AbilityCatalog.All[abilityId]);

    // ---- attacks and execution -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool WlItemsMiscIsHorde(string abilityId) => abilityId is WlItemsMiscFollowerAbilities.HordeOwn or WlItemsMiscFollowerAbilities.HordeEnemy;

    // null: not this cluster's Follower. false: the effects are done. true: a window opened (an Attack's reaction window).
    private bool? RunWlItemsMiscFollowers(RunHookContext context)
    {
        var (id, ability, command, player, source, target, consumedGrant) = (context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant);
        switch (id)
        {
            case WlItemsMiscFollowerAbilities.HordeOwn:
            case WlItemsMiscFollowerAbilities.HordeEnemy:
                {
                    // The discard comes first and its Focus Value is the Attack's strength; with no card to discard there is no information, so the Attack fails too.
                    var deckOwner = id == WlItemsMiscFollowerAbilities.HordeOwn ? player : Player(1 - player.Id);
                    if (DiscardTopOfFateDeck(deckOwner) is not int focus) return false;
                    WlState.HordeFocus = focus;
                    return ResolveOrDeferAttack(id, player, command.TargetId, ActionPerformer(ability, command, source).Id, consumedGrant);
                }
            case WlItemsMiscFollowerAbilities.ScoutsEngage:
            case WlItemsMiscFollowerAbilities.ScoutsPenalty:
                {
                    // "You have Reconnaissance at this battlefield" (Daidoji Yuki's grant: either side), and the Attacker's first opportunity (a claim the Defender's own overrides).
                    GrantReconnaissanceAtCurrentBattlefield(player.Id);
                    if (_state.PendingEngage is { } engage && player.Id == engage.Attacker) ClaimFirstOpportunity(player.Id, "Engage", source);
                    if (id == WlItemsMiscFollowerAbilities.ScoutsPenalty) ApplyModifier(target!, source!.Id, id, "Force", -3);
                    return false;
                }
            case WlItemsMiscFollowerAbilities.VassalDestroy:
                DestroyTargetCard(target!, player.Id);
                if (source is { Zone: Zone.Attached } && InPlay(source)) DestroyAttachment(source);
                return false;
            case WlItemsMiscFollowerAbilities.SycophantsStraighten:
                if (WielderOf(source!) is { } host)
                {
                    Straighten(host);
                    host.AbilityUses[WlItemsMiscFollowerAbilities.SycophantsMark] = _state.TurnNumber;
                    Emit("secondUseGranted", $"{Definition(source!).Name}: {Definition(host).Name} may use his Political actions that are limited to once per turn a second time this turn.", player.Id, host.Id);
                }
                return false;
            case WlItemsMiscFollowerAbilities.BestialEngage:
                ApplyModifier(target!, source!.Id, id, "Force", -2);
                return false;
            default:
                return null;
        }
    }

    // AttackStrengthAfter: Maddened Horde's strength is the Focus Value of the card it discarded.
    private int? WlItemsMiscHordeStrength(string abilityId) => WlItemsMiscIsHorde(abilityId) ? WlState.HordeFocus : null;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlItemsMiscFollowerHooks()
    {
        EngineHooks.Abilities.Register("AddWlItemsMiscFollowers", 15110, static abilities => abilities.AddWlItemsMiscFollowers());
        EngineHooks.RunEffects.Register("RunWlItemsMiscFollowers", 15110, static (engine, context) => engine.RunWlItemsMiscFollowers(context));
        EngineHooks.AttackStrengthAfter.Register("WlItemsMiscHordeStrength", 15110, static (engine, context) => engine.WlItemsMiscHordeStrength(context.abilityId));
        EngineHooks.DestroysTarget.Register("WlItemsMiscDestroysTarget", 15110, static context => context.abilityId is WlItemsMiscFollowerAbilities.VassalDestroy);
        EngineHooks.ExtraUsesPerTurn.Register("WlItemsMiscExtraUses", 15110, static (engine, context) => engine.WlItemsMiscExtraUses(context.uses, context.ability));
    }
}
