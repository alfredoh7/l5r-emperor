namespace L5R.Core;

// Promotional-Emperor pass (issue #653): the Personalities and Followers of the set, each wired from its own scan (personality-wave-123, follower-wave-46). Cards that only need existing
// surface are packaged with it: Armed Rice Farmer (Expendable) and Rice Farmer (Kharmic) print only keyword reminders, Daigotsu Hirose and Daigotsu Gakku's entry Honor loss are
// EntryHonorLoss, Gakku's "paying 1 less Gold if you are a Spider Clan player" is the "spider-clan-discount" Effect, Tess'kss is Shosuro Tsuji's "Target an enemy card: Bow it"
// (tsuji-bow), Chikara's "Target an enemy attachment: Destroy it" is Hida Komatsu's (komatsu-destroy-attachment), Tamahime - exp's "Target an enemy card without attachments:
// Destroy it" is Kuronada's (kuronada-destroy), both Tamahime's Sensei entry trait is the Sensei Effect (discard-other-sensei, run on entering play and on an overlay), Horse's
// "Will only attach to a Human. This Personality has Cavalry." is AttachRequiresKeyword/AttachGrantsKeyword (Commander's Steed's shape). This file carries the rest:
//   Gakku            "Battle: Melee 7 Attack."                                                                              promo-gakku-melee
//   Seppun Barru     "Battle: Give your Followers in Barru's army +1F."                                                     promo-barru-followers (until the turn ends, the rulebook default)
//   Mahatsu          "Open: Give Mahatsu Air, Earth, Fire, or Water."                                                       promo-mahatsu-air/earth/fire/water (Asako Hayate's shape: one use, a choice of three)
//   Isawa Hikarou    "Hikarou has +1F for each of his Fire Spells."                                                         Effect promo-hikarou-fire-spells (a live Force term)
//   Seppun Ichigo    "Ichigo has +1F for each Dojo you control."                                                            Effect promo-ichigo-dojo-force (a live Force term)
//   Seppun Ritisharu "Your Holdings produce 1 additional Gold when they bow to produce Gold."                               Effect promo-ritisharu-holdings (a term of GoldProduced)
//   Kataoka          "Enters play for 1 less Gold for each Farm other players control."                                     Effect promo-kataoka-farm-discount (a term of the entry price)
//   Tamahime         "Your other Ronin Personalities have Tactician."                                                       live keyword grant, read by Keyword()
//   Tamahime - exp   the same, "Your other Ronin cards enter play paying 1 less Gold."                                      a term of the entry price
//   Tsuruchi Nishima "After Nishima assigns in Infantry Maneuvers, give him Naval."                                         a mandatory trait read where an assignment is made (Effect promo-nishima-naval)
//   Fudo - exp       the five Charging-token rules and his Limited, Reaction and Battle actions                              Effect promo-fudo, promo-fudo-charge/negate/destroy
//   Horse            "Attaches to Kataoka ignoring costs."                                                                   a term of the Equip price (FollowerAttachDiscount)
//   Treasure Hunter  "Reaction: After a battle resolves, if this card's army destroyed a province, destroy this card: ... Create a Holding with the trait, 'Bow this card:
//                    Produce 2 Gold', and put it into play (bowed)."                                                         promo-treasure-hunter-retire (the published created-only Charter, oracle-l5r-10806)
//
// Readings, for Mendon to confirm or correct:
//   * Horse: "ignoring costs" is its Gold Cost (it has no other); the Follower may still attach to any other Human, paying its 3 Gold. "Will only attach to a Human" is a separate sentence.
//   * Nishima: "assigns in Infantry Maneuvers" is any assignment of his in that segment, as Attacker or as Defender; Naval lasts the turn (R-001) and is a mandatory trait.
//   * Fudo: "may not assign, and will not move, to battlefields" locks an assignment (either side) and every movement to a battlefield while he has fewer than five Charging tokens; the
//     two tokens come off after any assignment. The Reaction's token is a cost, so it is not offered without one; it negates every effect of the action on him.
//   * Treasure Hunter: "this card's army" is the army of the Personality it is attached to, at the battlefield whose province the resolution destroyed (the Attacker's army only).
public static class PromoEmperorPersonalitiesAbilities
{
    public const string GakkuMelee = "promo-gakku-melee";
    public const string BarruFollowers = "promo-barru-followers";
    public const string MahatsuAir = "promo-mahatsu-air";
    public const string MahatsuEarth = "promo-mahatsu-earth";
    public const string MahatsuFire = "promo-mahatsu-fire";
    public const string MahatsuWater = "promo-mahatsu-water";
    public const string FudoCharge = "promo-fudo-charge";
    public const string FudoNegate = "promo-fudo-negate";
    public const string FudoDestroy = "promo-fudo-destroy";
    public const string TreasureHunterRetire = "promo-treasure-hunter-retire";

    public const string HikarouEffect = "promo-hikarou-fire-spells";
    public const string IchigoEffect = "promo-ichigo-dojo-force";
    public const string RitisharuEffect = "promo-ritisharu-holdings";
    public const string KataokaEffect = "promo-kataoka-farm-discount";
    public const string NishimaEffect = "promo-nishima-naval";
    public const string FudoEffect = "promo-fudo";
    public const string HorseEffect = "promo-horse-kataoka-free";

    public const string TamahimeId = "oracle-l5r-10194", TamahimeExpId = "oracle-l5r-10193", KataokaId = "oracle-l5r-10583", CharterProxy = "oracle-l5r-10806";
    public const string Charging = "Charging";

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorPersonalities(this Dictionary<string, AbilityDefinition> all)
    {
        all[GakkuMelee] = new("Battle", "Melee 7 Attack", "attack");
        all[BarruFollowers] = new("Battle", "Give your Followers in Barru's army +1F", "none");
        all[MahatsuAir] = new("Open", "Give Mahatsu Air", "none", UsageKey: "promo-mahatsu-element");
        all[MahatsuEarth] = new("Open", "Give Mahatsu Earth", "none", UsageKey: "promo-mahatsu-element");
        all[MahatsuFire] = new("Open", "Give Mahatsu Fire", "none", UsageKey: "promo-mahatsu-element");
        all[MahatsuWater] = new("Open", "Give Mahatsu Water", "none", UsageKey: "promo-mahatsu-element");
        all[FudoCharge] = new("Limited", "Give Fudo two Charging tokens", "none");
        all[FudoNegate] = new("Reaction", "Twice per turn, after an action targets Fudo, remove one of its Charging tokens: Negate the action's effects on it", "none",
            Trigger: ReactionTriggers.TargetedByAction, UsesPerTurn: 2, Costs: [new(CostVerbs.DestroyToken, CostSubject.Source, Token: Charging)]);
        all[FudoDestroy] = new("Battle", "Target an enemy unit: Destroy it", "enemy-personality");
        all[TreasureHunterRetire] = new("Reaction", "After a battle resolves, if this card's army destroyed a province, destroy this card: He plunders enough wealth to retire. Create a Holding with the trait, \"Bow this card: Produce 2 Gold\", and put it into play (bowed)", "none",
            Trigger: ReactionTriggers.AfterBattleResolution, Costs: [new(CostVerbs.Sacrifice)], Creates: new(CardType.Holding, ProxyId: CharterProxy) { EntersBowed = true });
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- live terms read by the shared pipelines (each one call from its shared site) ----

    // Hikarou: +1F for each Fire Spell attached to him. Ichigo: +1F for each Dojo (a card with the keyword) his controller controls in play.
    private int PromoPersonalitiesForce(CardInstance card)
    {
        var effect = Definition(card).Effect;
        if (effect == PromoEmperorPersonalitiesAbilities.HikarouEffect)
            return Attachments(card.Id).Count(a => Definition(a).Type == CardType.Spell && Keyword(a, "Fire"));
        if (effect == PromoEmperorPersonalitiesAbilities.IchigoEffect)
            return _state.Cards.Values.Count(c => c.OwnerId == card.OwnerId && InPlay(c) && Keyword(c, "Dojo"));
        return 0;
    }

    // Tamahime and Tamahime - exp: "Your other Ronin Personalities have Tactician." In play, live, so it ends with its source (called from Keyword()).
    private bool PromoGrantsKeyword(CardInstance card, string keyword)
    {
        if (keyword != "Tactician" || !InPlay(card) || Definition(card).Type != CardType.Personality || !Keyword(card, "Ronin")) return false;
        return _state.Cards.Values.Any(c => c.Id != card.Id && c.OwnerId == card.OwnerId && InPlay(c)
            && (c.DefinitionId == PromoEmperorPersonalitiesAbilities.TamahimeId || c.DefinitionId == PromoEmperorPersonalitiesAbilities.TamahimeExpId));
    }

    // Kataoka: 1 less Gold for each Farm another player controls. Tamahime - exp: her controller's other Ronin cards enter play paying 1 less. Both are terms of the one entry price.
    private (int Amount, string Term) PromoEmperorEntryDiscount(CardInstance entering, int playerId)
    {
        var amount = 0; var terms = new List<string>();
        if (Definition(entering).Effect == PromoEmperorPersonalitiesAbilities.KataokaEffect)
        {
            var farms = _state.Cards.Values.Count(c => c.OwnerId != playerId && InPlay(c) && Definition(c).Type == CardType.Holding && Keyword(c, "Farm"));
            if (farms > 0) { amount += farms; terms.Add("Farms other players control"); }
        }
        if (Keyword(entering, "Ronin"))
        {
            var tamahime = _state.Cards.Values.Count(c => c.Id != entering.Id && c.OwnerId == playerId && InPlay(c) && c.DefinitionId == PromoEmperorPersonalitiesAbilities.TamahimeExpId);
            if (tamahime > 0) { amount += tamahime; terms.Add("Tamahime"); }
        }
        return (amount, string.Join(" and ", terms));
    }

    // Seppun Ritisharu: each of his controller's Holdings that bows to produce Gold produces 1 more. A Holding with no production of its own produces none to add to.
    private int PromoProductionBonus(CardInstance card, int playerId)
    {
        if (Definition(card).Type != CardType.Holding || Definition(card).GoldProduction <= 0 && ConditionalProductionOf(card) is null) return 0;
        return _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == PromoEmperorPersonalitiesAbilities.RitisharuEffect);
    }

    // Horse: "Attaches to Kataoka ignoring costs." The Follower's whole Gold Cost when its wielder is Kataoka.
    private static int PromoHorseAttachDiscount(CardInstance follower, CardInstance wielder) =>
        Definition(follower).Effect == PromoEmperorPersonalitiesAbilities.HorseEffect && wielder.DefinitionId == PromoEmperorPersonalitiesAbilities.KataokaId ? Definition(follower).GoldCost : 0;

    // Fudo: "may not assign, and will not move, to battlefields unless it has 5 Charging tokens" (called from MovementLocked).
    private bool PromoMovementLocked(CardInstance card, MovementKinds kind) =>
        Definition(card).Effect == PromoEmperorPersonalitiesAbilities.FudoEffect
        && (kind & (MovementKinds.Assign | MovementKinds.ToBattlefield | MovementKinds.AssignAttack)) != 0
        && TokenCount(card, PromoEmperorPersonalitiesAbilities.Charging) < 5;

    // After a Personality is assigned (called from Assign, once per assignment): Nishima in Infantry Maneuvers gains Naval; Fudo loses two Charging tokens.
    private void PromoEmperorAssigned(CardInstance card, bool cavalrySegment)
    {
        var effect = Definition(card).Effect;
        if (effect == PromoEmperorPersonalitiesAbilities.NishimaEffect && !cavalrySegment && !Keyword(card, "Naval"))
        {
            GrantKeyword(card, "Naval", card.Id, GrantDuration.Turn);
            Emit("keywordGranted", $"{Definition(card).Name} assigned in Infantry Maneuvers: he has Naval.", card.OwnerId, card.Id);
        }
        else if (effect == PromoEmperorPersonalitiesAbilities.FudoEffect)
            for (var n = 0; n < 2 && card.Tokens.FirstOrDefault(t => t.Type == PromoEmperorPersonalitiesAbilities.Charging) is { } token; n++) DestroyToken(card, token);
    }

    // ---- targeted reaction, validation, effects ----

    // Fudo: any action (anyone's) targets him. The token is the cost, so the Reaction is only offered while he has one.
    private bool PromoEmperorTargetedReaction(CardInstance card, string abilityId, CardInstance affected) =>
        abilityId == PromoEmperorPersonalitiesAbilities.FudoNegate && affected.Id == card.Id && TokenCount(card, PromoEmperorPersonalitiesAbilities.Charging) > 0;

    private void ValidatePromoEmperor(string id, PlayerState player, CardInstance? source)
    {
        if (id == PromoEmperorPersonalitiesAbilities.FudoNegate)
            Require(source is not null && TokenCount(source, PromoEmperorPersonalitiesAbilities.Charging) > 0, "Fudo has no Charging token to remove.");
        if (id != PromoEmperorPersonalitiesAbilities.TreasureHunterRetire) return;
        // "After a battle resolves, if this card's army destroyed a province": the Attacker's army (the Personality this Follower is attached to) stood at the battlefield whose province
        // the resolution destroyed. Only the after-resolution window holds those facts.
        var armyDestroyedProvince = _state.PendingAfterResolution is { } facts && facts.ProvinceDestroyedBy == player.Id && facts.Attacker == player.Id
            && source is { Zone: Zone.Attached, AttachedTo: { } hostId } && facts.AttackerIds.Contains(hostId)
            && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield && host.ProvinceIndex == facts.ProvinceIndex;
        Require(armyDestroyedProvince, "This card's army did not destroy a province in this battle's resolution.");
    }

    private bool? RunPromoEmperor(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            case PromoEmperorPersonalitiesAbilities.GakkuMelee:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case PromoEmperorPersonalitiesAbilities.BarruFollowers:
                if (source!.Zone == Zone.Battlefield && source.ProvinceIndex is int province)
                    foreach (var personality in Army(source.OwnerId, province).ToArray())
                        foreach (var follower in Attachments(personality.Id).Where(a => Definition(a).Type == CardType.Follower && a.OwnerId == source.OwnerId).ToArray())
                            ApplyModifier(follower, source.Id, id, "Force", 1);
                return false;
            case PromoEmperorPersonalitiesAbilities.MahatsuAir: GrantKeyword(source!, "Air", source!.Id, GrantDuration.Turn); return false;
            case PromoEmperorPersonalitiesAbilities.MahatsuEarth: GrantKeyword(source!, "Earth", source!.Id, GrantDuration.Turn); return false;
            case PromoEmperorPersonalitiesAbilities.MahatsuFire: GrantKeyword(source!, "Fire", source!.Id, GrantDuration.Turn); return false;
            case PromoEmperorPersonalitiesAbilities.MahatsuWater: GrantKeyword(source!, "Water", source!.Id, GrantDuration.Turn); return false;
            case PromoEmperorPersonalitiesAbilities.FudoCharge:
                CreateToken(source!, PromoEmperorPersonalitiesAbilities.Charging, id);
                CreateToken(source!, PromoEmperorPersonalitiesAbilities.Charging, id);
                return false;
            case PromoEmperorPersonalitiesAbilities.FudoNegate:
                NegateAllEffectsOn(source!);
                return false;
            case PromoEmperorPersonalitiesAbilities.FudoDestroy:
                DestroyUnits([target!], false, player.Id);
                return false;
            // Treasure Hunter is not handled here: the default arm runs the ability's Creates (the Charter), after the destruction paid as its cost.
            default:
                return null;
        }
    }

    private int? PromoEmperorAttackStrength(string abilityId) => abilityId == PromoEmperorPersonalitiesAbilities.GakkuMelee ? 7 : null;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoEmperorPersonalitiesHooks()
    {
        EngineHooks.Abilities.Register("AddPromoEmperorPersonalities", 12000, static abilities => abilities.AddPromoEmperorPersonalities());
        EngineHooks.RunEffects.Register("RunPromoEmperor", 12000, static (engine, context) => engine.RunPromoEmperor(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant));
        EngineHooks.ValidateMiddle.Register("ValidatePromoEmperor", 12000, static (engine, context) => engine.ValidatePromoEmperor(context.id, context.player, context.source));
        EngineHooks.PersonalityForce.Register("PromoPersonalitiesForce", 12000, static (engine, context) => engine.PromoPersonalitiesForce(context.card));
        EngineHooks.AttackStrengthBefore.Register("PromoEmperorAttackStrength", 12000, static (engine, context) => engine.PromoEmperorAttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("PromoEmperorAttackKind", 12000, static context => context.abilityId == PromoEmperorPersonalitiesAbilities.GakkuMelee ? "Melee" : null);
        EngineHooks.DestroysTarget.Register("PromoPersonalitiesDestroysTarget", 12000, static context => context.abilityId == PromoEmperorPersonalitiesAbilities.FudoDestroy);
        EngineHooks.TargetedReaction.Register("PromoEmperorTargetedReaction", 12000, static (engine, context) => engine.PromoEmperorTargetedReaction(context.card, context.abilityId, context.affected));
    }
}
