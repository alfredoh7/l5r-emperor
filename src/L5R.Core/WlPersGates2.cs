namespace L5R.Core;

// ======================================================================================================================================================================================
// "PERS-GATES-2" (issue #674, personality-wave-129): thirteen Gates of Chaos Personalities wired from the PRINTED text of their scans, with the engine's existing mechanisms.
//
//   * MIRUMOTO KATAGI - EXP   "Battle, X: Equip a target non-Unique Weapon in your discard pile to Katagi."  -- Foothills Keep's / Agasha Kurou's attach-from-discard (AttachSpec), priced as an Equip.
//   * MOSHI RUKIA - EXP       "Battle: Bow a target enemy card without attachments. Destroy it if it is an attachment."  -- Hiroto's own ability (hiroto-bow), reused verbatim.
//   * SHIBA TSURAO            "Battle: Give a target enemy Personality or Follower -2F."  (Expendable is the engine's keyword.)
//   * SHINJO YOSHIE           "After Yoshie enters play, lose 1 Honor." (EntryHonorLoss) and "Battle: Give a target enemy Infantry Personality or Follower -3F." -- Mirumoto Hatsuto's ability
//                             (hatsuto-weaken), reused verbatim: Infantry is "not Cavalry" in this engine (core rules glossary). Destined is the engine's keyword.
//   * SHOSURO NIGAWA / UTAKU SAYAKA   "Invest 3: Give [her] two +1F tokens." -- Clay Kshatriya's / Togashi Noritada's Invest (an own-enters-play Reaction paying the Invest cost once).
//   * SOSHI KODANSHI - EXP    "Limited: Bow your target unbowed Ninja Personality. Bow a target Personality; if he is Fudo or Fallen, he will not straighten this turn."  (two target slots; the
//                             straighten lock is LockStraightening, the "may not be straightened this turn" of Basic Lesson)
//   * TORITAKA CHOKICHI - EXP "Battle: Bow a target Spider Clan Personality or, if Chokichi has a Follower, bow a target enemy card without attachments."
//   * TSURUCHI GOMBEI, YOGO TAKASHI   "After [X] enters play, lose 1 Honor." (EntryHonorLoss); Takashi is also Destined and Expendable; YASUKI DAISUKI is Expendable only.
//   * YOGO NOBUKAI            "Open: If it is your turn or you control a Courtier, give a target Personality -2F."
//   * YORITOMO HIROMI - EXP   "Battle, X: Ranged Attack with strength equal to the Gold paid." -- Celestial Sword of the Mantis' variable-Gold attack with a multiplier of one.
//
// Decision note: docs/decisions/2026-10-06-674-pers-gates-2.md.
// ======================================================================================================================================================================================

public static class WlPersGates2Abilities
{
    public const string KatagiEquip = "wl2-katagi-equip", TsuraoWeaken = "wl2-tsurao-weaken", InvestTwo = "wl2-invest-two-tokens", KodanshiBow = "wl2-kodanshi-bow";
    public const string ChokichiBow = "wl2-chokichi-bow", NobukaiWeaken = "wl2-nobukai-weaken", HiromiAttack = "wl2-hiromi-ranged-attack";

    /// <summary>Target category: one of your own unbowed Ninja Personalities (Soshi Kodanshi's first slot).</summary>
    public const string OwnUnbowedNinja = "wl2-own-unbowed-ninja";

    /// <summary>Target category: a Spider Clan Personality of either side, or, while the source has a Follower, an enemy card without attachments (Toritaka Chokichi).</summary>
    public const string ChokichiTarget = "wl2-chokichi-target";

    public static Dictionary<string, AbilityDefinition> AddWlPersGates2(this Dictionary<string, AbilityDefinition> all)
    {
        // Mirumoto Katagi - exp (oracle-l5r-10631): "Battle, X: Equip a target non-Unique Weapon in your discard pile to Katagi."
        all[KatagiEquip] = new("Battle", "Target a non-Unique Weapon in your discard pile: Equip it to Katagi, paying all costs", "attachment-in-discard", Cost: GoldCostRule.AttachCost(),
            Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Source, [CardType.Item], ["Weapon"], ["Unique"]));
        // Shiba Tsurao (oracle-l5r-10652): "Battle: Give a target enemy Personality or Follower -2F."
        all[TsuraoWeaken] = new("Battle", "Target an enemy Personality or Follower: Give it -2F", "enemy-personality-or-follower");
        // Shosuro Nigawa (oracle-l5r-10655) and Utaku Sayaka (oracle-l5r-10671): "Invest 3: Give [her] two +1F tokens."
        all[InvestTwo] = new("Reaction", "Invest 3 Gold: give this Personality two +1F tokens", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(3));
        // Soshi Kodanshi - exp (oracle-l5r-10656): "Limited: Bow your target unbowed Ninja Personality. Bow a target Personality; if he is Fudo or Fallen, he will not straighten this turn."
        all[KodanshiBow] = new("Limited", "Bow your target unbowed Ninja Personality. Bow a target Personality; if he is Fudo or Fallen, he will not straighten this turn", "slots",
            Slots: [new("ninja", OwnUnbowedNinja, "Your unbowed Ninja Personality to bow"), new("target", "personality", "A Personality to bow")]);
        // Toritaka Chokichi - exp (oracle-l5r-10620): "Battle: Bow a target Spider Clan Personality or, if Chokichi has a Follower, bow a target enemy card without attachments."
        all[ChokichiBow] = new("Battle", "Bow a target Spider Clan Personality or, if Chokichi has a Follower, bow a target enemy card without attachments", ChokichiTarget);
        // Yogo Nobukai (oracle-l5r-10658): "Open: If it is your turn or you control a Courtier, give a target Personality -2F."
        all[NobukaiWeaken] = new("Open", "If it is your turn or you control a Courtier, give a target Personality -2F", "personality");
        // Yoritomo Hiromi - exp (oracle-l5r-10646): "Battle, X: Ranged Attack with strength equal to the Gold paid."
        all[HiromiAttack] = new("Battle", "Pay zero or more Gold: Ranged Attack with strength equal to the Gold paid", "attack", Cost: GoldCostRule.Variable());
        return all;
    }
}

public sealed partial class MatchEngine
{
    private bool WlPersGates2TargetAllows(string category, CardInstance? source, PlayerState player, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        switch (category)
        {
            case WlPersGates2Abilities.OwnUnbowedNinja:
                return personality && own && !target.Bowed && Keyword(target, "Ninja");
            case WlPersGates2Abilities.ChokichiTarget:
                {
                    if (personality && (HasClanAffiliation(target, "Spider") || Keyword(target, "Spider Clan"))) return true;
                    // "If Chokichi has a Follower": read when the action is chosen, as a condition of this second target.
                    var hasFollower = source is not null && Attachments(source.Id).Any(a => Definition(a).Type == CardType.Follower);
                    return hasFollower && !own && (Definition(target).Type == CardType.Follower || personality && !HasAttachments(target));
                }
            default:
                return false;
        }
    }

    private void ValidateWlPersGates2(string id, PlayerState player)
    {
        // "If it is your turn or you control a Courtier": an Open action that does nothing otherwise is not offered.
        if (id == WlPersGates2Abilities.NobukaiWeaken)
            Require(_state.ActivePlayerId == player.Id || ControlsKeyword(player.Id, "Courtier"), "It is not your turn and you control no Courtier.");
    }

    // null: not this feature's. false: the effects are done. true: a window opened (an Attack's reaction window).
    private bool? RunWlPersGates2(RunHookContext context)
    {
        var (id, ability, command, player, source, target, consumedGrant) = (context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant);
        switch (id)
        {
            case WlPersGates2Abilities.TsuraoWeaken:
                ApplyModifier(target!, source!.Id, id, "Force", -2);
                return false;
            case WlPersGates2Abilities.NobukaiWeaken:
                ApplyModifier(target!, source!.Id, id, "Force", -2);
                return false;
            // Invest: the engine's own shape (Clay Kshatriya, Noritada) -- two +1F tokens on the card that just entered play.
            case WlPersGates2Abilities.InvestTwo:
                CreateToken(source!, "Invest", id, "Force", 1);
                CreateToken(source!, "Invest", id, "Force", 1);
                return false;
            case WlPersGates2Abilities.ChokichiBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case WlPersGates2Abilities.KodanshiBow:
                {
                    var ninja = SlotCards(command, "ninja").Single();
                    var victim = SlotCards(command, "target").Single();
                    if (!StalwartNegatesBow(ninja, player.Id)) ninja.Bowed = true;
                    if (!StalwartNegatesBow(victim, player.Id)) victim.Bowed = true;
                    // The straighten lock is its own sentence: read when it resolves, whether or not the bow happened.
                    if (InPlay(victim) && (Keyword(victim, "Fudo") || Keyword(victim, "Fallen"))) LockStraightening(victim);
                    return false;
                }
            // Ranged Attack, strength equal to the Gold paid: priced at resolution from the variable payment (Celestial Sword of the Mantis' shape with a multiplier of one).
            case WlPersGates2Abilities.HiromiAttack:
                {
                    var performerId = ActionPerformer(ability, command, source).Id;
                    return ResolveOrDeferAttack(id, player, target?.Id, performerId, consumedGrant,
                        strengthOverride: AttackStrength(id, player, target, performerId) + (target is null ? 0 : AttackDefenseModifier(target, AttackKind(id))) + PaidGoldAmount(command));
                }
            default:
                return null;
        }
    }

    private static string? WlPersGates2AttackKind(string abilityId) => abilityId == WlPersGates2Abilities.HiromiAttack ? "Ranged" : null;

    private static bool WlPersGates2BowsTarget(string abilityId) => abilityId is WlPersGates2Abilities.ChokichiBow or WlPersGates2Abilities.KodanshiBow;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlPersGates2Hooks()
    {
        EngineHooks.Abilities.Register("AddWlPersGates2", 14200, static abilities => abilities.AddWlPersGates2());
        EngineHooks.TargetAllows.Register("WlPersGates2TargetAllows", 14200, static (engine, context) => engine.WlPersGates2TargetAllows(context.ability.Target, context.source, context.player, context.target));
        EngineHooks.ValidateMiddle.Register("ValidateWlPersGates2", 14200, static (engine, context) => engine.ValidateWlPersGates2(context.id, context.player));
        EngineHooks.RunEffects.Register("RunWlPersGates2", 14200, static (engine, context) => engine.RunWlPersGates2(context));
        EngineHooks.AttackStrengthAfter.Register("WlPersGates2AttackStrength", 14200, static (engine, context) => context.abilityId == WlPersGates2Abilities.HiromiAttack ? 0 : null);
        EngineHooks.AttackKind.Register("WlPersGates2AttackKind", 14200, static context => WlPersGates2AttackKind(context.abilityId));
        EngineHooks.BowsTarget.Register("WlPersGates2BowsTarget", 14200, static context => WlPersGates2BowsTarget(context.abilityId));
    }
}
