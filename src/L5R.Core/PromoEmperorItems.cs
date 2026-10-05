using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-EMPEROR ITEMS (issue #653, item-wave-42): twenty-three of the thirty Promotional-Emperor Items, wired from the PRINTED text of their scans (the catalogue's text is a different printing for
// several of them). Seven were left unwired, each with a named blocker in tools/content/pe_items_blockers.json.
//
//   ATTACH RESTRICTIONS   "Will only attach to a Crab Clan or Naga Personality" (Ancient Armor, Battle Standard, Pearl-Encrusted Staff, Arcane Cloak, Yari of the Shabura, Blessed Mantle of the Greensnakes) is
//                         AttachRequiresKeyword "Crab Clan|Naga": a '|' lists alternatives (KeywordOrHuman). "Attaches to a [X] Champion ignoring costs" (Bokken, Mantles of the Onyx and Jade Champions, Emerald
//                         Armor, Obsidian Armor, Ivory Sword) is that Champion keyword as the restriction and a zero Gold price (EquipGoldCost: PromoItemsAttachesFree).
//   STATIC TRAITS         Imperial Sword (+1F while he is Imperial), Battle Standard (+1F to the controller's other Personalities and to Followers at its battlefield), Mantles of the Onyx and Jade Champions (a Force
//                         bonus equal to the highest Focus Value among Spells in the unit; Maho on the Onyx's Spells), Kimono (+1PH while Turquoise Champion), Emerald Armor (+1PH, Imperial and Magistrate),
//                         Blessed Mantle of the Greensnakes (Lobby treats your Honor as 20 higher; its Personality lobbies without bowing, a pseudo-keyword the Lobby action's bow cost reads).
//   DESTRUCTION           Ancient Armor negates any destruction of itself (not its Personality's), as Gift Armor does; Six-Ring Sword negates the first destruction each turn that another player's effect from a
//                         card would cause. Armor's Reaction negates the Personality's destruction from an action that targeted him.
//   TARGETING             The Emerald Armor and The Arcane Cloak refuse another player's action a target while another legal one exists (RequirePromoItemsNotProtected). The Yari of the Shabura's Reaction bars one
//                         Personality's actions from targeting its unit for the battle (PromoItemsYariBars).
//   ABILITY SHAPES        Mantle of the Jade Champion is Library of Rebirth's reactive Gold source on an attached card (ReactiveGoldSource now also looks at attachments); the Ivory Sword's trait is a Reaction-timed
//                         trait (TraitReactions.Is) whose decision is Isawa Kimi - exp 2's (the other player chooses and discards); the Yari's return is Never Safe's after-resolution Reaction from the discard pile;
//                         Family Sword transfers through AttachmentTransfers.cs.
//
// READINGS (each is also in the batch report):
//   * "Bow" in a printed cost is bow THIS card, as every other Item reads it; a Weapon's or Armor's action is performed by its wielder.
//   * Exquisite Blade's "After this card bows: Straighten it" is Blade of Perfection's trait (its Effect, blade-perfection-rebound). Its Battle action prints no cost: it is a Melee 6 Attack that is simply taken.
//   * Ritual Dagger: "Melee Attack with strength equal to this Personality's Force before you destroyed this card" is the wielder's Force with the dagger's own +1F still counted, read as the Attack is made.
//   * Enigmatic Pendant: "Destroy this card and discard your hand: Draw a number of cards equal to the number you discarded" -- both are costs (paid even when the action is negated); the draw is the hand's
//     size when it was discarded (PromoItemsHandDiscardCount, recorded by the cost).
//   * Heart of Fudo: "any other player" is the opponent; "more provinces than you" counts provinces not destroyed.
//   * Six-Ring Sword and Ancient Armor: "another player's effect from a card" is an effect of an action another player took (CurrentActionFacts); a battle's own sweep, the 0-Chi rule and a cost the
//     controller pays are not.
//   * The Arcane Cloak: "any other legal targets in this army" are legal targets in the controller's OTHER units standing at the same location (the unit's own cards cannot be the alternative that frees
//     another of them, or none could ever be targeted). The Emerald Armor's alternatives are any other Personality at his location (either player's), as printed.
//   * Kimono of the Turquoise Champion: "before an action resolves ... negate any Honor losses until the action ends" is played in the window before an Honor loss (Mendon's R4 ruling, EmperorRulingR4Honor.cs),
//     like Solid Defense, and negates the losses of that action (any player's).
//   * Family Sword: the Torn Asunder supplement's rule of thumb (docs/rules/EMPEROR_EDITION_CORE_RULES.md, "Family names"): the first word of a two-word title that is not ordinary English (the, a, an, master,
//     lord, lady, old, great, honorable, young) is the family name, for a Personality with a Clan alignment or a keyword ending in "Clan".
//   * The Pearl-Encrusted Staff: it may name any Personality (the printed "target a Personality"), but is offered only before a Battle action whose effects would move one and whose movement can be negated.
//   * The Yari of the Shabura: "actions he performs this battle may not target cards in its unit (when the action is performed)" is checked as each action is announced, for every card it names, while the Yari
//     remains attached; "after this battle ends" is the window after the battle's resolution (Never Safe's).
//   * The Ivory Sword: "while he opposed this card" is read at the action's resolution (performer and the Sword's wielder at one battlefield, for the two players); the trait is modelled as an optional
//     Reaction like the other no-designator traits (TraitReactions), so its controller could decline.
//   * The Blessed Mantle of the Greensnakes: "Lobbying does not bow this Personality" (the catalogue) is the scan's "may lobby ignoring the cost of bowing": the Lobby action does not bow him, and he may lobby while bowed.
//   * Mantle of the Jade Champion: "when paying for a Spell" is an Equip of a Spell (either timing), once a turn like every Reaction.
// ======================================================================================================================================================================================

public static class PromoEmperorItemsAbilities
{
    public const string QamarArmor = "oracle-l5r-9970", BattleStandard = "oracle-l5r-9973", Bokken = "oracle-l5r-9963", EnigmaticPendant = "oracle-l5r-10411", ExquisiteBlade = "oracle-l5r-2418",
        HeartOfFudo = "oracle-l5r-3015", ImperialSword = "oracle-l5r-10594", Kimono = "oracle-l5r-9965", Lantern = "oracle-l5r-10413", OnyxMantle = "oracle-l5r-9969", PatchworkArmor = "oracle-l5r-5886",
        RitualDagger = "oracle-l5r-6354", SimpleSpears = "oracle-l5r-10593", SixRingSword = "oracle-l5r-9968", ArcaneCloak = "oracle-l5r-9972", EmeraldArmor = "oracle-l5r-9974",
        ObsidianArmor = "oracle-l5r-9978", PearlStaff = "oracle-l5r-9981", YariOfShabura = "oracle-l5r-9984", IvorySword = "oracle-l5r-9976", JadeMantle = "oracle-l5r-9967";

    /// <summary>The Effect keys of the static traits (CardDefinition.Effect).</summary>
    public const string QamarEffect = "pe-qamar-armor", StandardEffect = "pe-battle-standard", ImperialSwordEffect = "pe-imperial-sword", KimonoEffect = "pe-kimono-turquoise", OnyxEffect = "pe-onyx-mantle",
        SixRingEffect = "pe-six-ring-sword", CloakEffect = "pe-arcane-cloak", EmeraldEffect = "pe-emerald-armor", JadeEffect = "pe-jade-mantle";

    public const string QamarNegate = "pe-qamar-negate-destruction", BokkenBow = "pe-bokken-bow", PendantDraw = "pe-pendant-draw-hand", ExquisiteMelee = "pe-exquisite-melee", FudoCharge = "pe-fudo-charging",
        KimonoNegate = "pe-kimono-negate-losses", LanternLook = "pe-lantern-look", OnyxRanged = "pe-onyx-ranged", DaggerMelee = "pe-dagger-melee", CloakHome = "pe-cloak-move-home",
        ObsidianBow = "pe-obsidian-fear-bow", PearlNegate = "pe-pearl-negate-movement", YariBar = "pe-yari-bar-targeting", YariRanged = "pe-yari-ranged", YariReturn = "pe-yari-return",
        IvoryDiscard = "pe-ivory-discard", JadeGold = "pe-jade-gold-for-spell", FamilyTransfer = "pe-family-sword-transfer";

    public const string FamilySword = "oracle-l5r-10412", BlessedMantle = "oracle-l5r-9977", BlessedMantleEffect = "pe-blessed-mantle-greensnakes", BlessedMantleDraw = "pe-greensnakes-draw-for-favor";

    /// <summary>The pseudo-keyword a Personality with The Blessed Mantle of the Greensnakes has ("This Personality may lobby ignoring the cost of bowing"): the Lobby action does not bow him.</summary>
    public const string LobbiesWithoutBowing = "Lobbies Without Bowing";

    /// <summary>Target category: your Personality with the same Family Name as the Personality this card is attached to, who this card may be transferred to (Family Sword).</summary>
    public const string FamilyMate = "pe-own-personality-same-family-name";

    /// <summary>Mantle of the Jade Champion's payment condition: paying for a Spell (equipped or attached).</summary>
    public const string PayingForSpellWhen = "pe-paying-for-spell";

    /// <summary>Target categories: an enemy Personality or Follower with 6 or lower Force (Bokken's slot); an enemy Personality at the battlefield where this card's wielder stands (the Yari's Reaction).</summary>
    public const string EnemyForceLte6 = "pe-enemy-personality-or-follower-force-lte-6", EnemyOpposingSource = "pe-enemy-personality-opposing-source";

    /// <summary>The Items that attach to a Champion "ignoring costs": their Gold price is nothing.</summary>
    public static readonly string[] FreeAttachIds = [Bokken, OnyxMantle, EmeraldArmor, ObsidianArmor, IvorySword, JadeMantle];

    /// <summary>The abilities that are traits (no printed Reaction designator) modelled as Reaction-timed abilities (TraitReactions, EmperorRulingR4.cs).</summary>
    public static bool IsTrait(string abilityId) => abilityId == IvoryDiscard;

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorItems(this Dictionary<string, AbilityDefinition> all)
    {
        // Ancient Armor of the Qamar (oracle-l5r-9970): "Reaction: After another player's action targets this Personality: Negate his destruction from the action's effects."
        all[QamarNegate] = new("Reaction", "After another player's action targets this Personality: Negate his destruction from the action's effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        // Bokken of the Ruby Champion (oracle-l5r-9963): "Battle: Target one or two enemy Personalities or Followers with 6 or lower Force each: Bow them."
        all[BokkenBow] = new("Battle", "Target one or two enemy Personalities or Followers with 6 or lower Force each: Bow them", "slots",
            Slots: [new("targets", EnemyForceLte6, "One or two enemy Personalities or Followers with 6 or lower Force each", 1, 2)]);
        // Enigmatic Pendant (oracle-l5r-10411, the scan): "Limited: Destroy this card and discard your hand: Draw a number of cards equal to the number you discarded."
        all[PendantDraw] = new("Limited", "Destroy this card and discard your hand: Draw a number of cards equal to the number you discarded", "none",
            Costs: [new(CostVerbs.Sacrifice), new(CostVerbs.DiscardHand)]);
        // Exquisite Blade (oracle-l5r-2418): "Battle: Melee 6 Attack."
        all[ExquisiteMelee] = new("Battle", "Melee 6 Attack", "attack");
        // Heart of Fudo - exp (oracle-l5r-3015): "Limited: Give this Personality one Charging token, or two if any other player has 20 or higher Family Honor or more provinces than you."
        all[FudoCharge] = new("Limited", "Give this Personality one Charging token, or two if any other player has 20 or higher Family Honor or more provinces than you", "none");
        // Kimono of the Turquoise Champion (oracle-l5r-9965): "Reaction: Before an action resolves, bow this card: ... Negate any Honor losses until the action ends."
        all[KimonoNegate] = new("Reaction", "Before an action resolves, bow this card: You silence your enemies with your creative genius. Negate any Honor losses until the action ends", "none",
            Trigger: HonorChangeReactions.Trigger, Costs: ActionCost.BowSource);
        // Lantern of Revelation (oracle-l5r-10413): "Limited: Look at the top card of one of your decks. You may put it at the bottom."
        all[LanternLook] = new("Limited", "Look at the top card of one of your decks. You may put it at the bottom", "none", Look: PromoEmperorItemsLooks.Lantern);
        // Mantle of the Onyx Champion (oracle-l5r-9969): "Maho Battle: Ranged 5 Attack." ("Maho" is a plain classification word, Asako Rikate's precedent.)
        all[OnyxRanged] = new("Battle", "Maho Battle: Ranged 5 Attack", "attack", ActionKeywords: ["Maho"]);
        // Ritual Dagger (oracle-l5r-6354): "Battle: Destroy this card: Melee Attack with strength equal to this Personality's Force before you destroyed this card."
        all[DaggerMelee] = new("Battle", "Destroy this card: Melee Attack with strength equal to this Personality's Force before you destroyed this card", "attack", Costs: [new(CostVerbs.Sacrifice)]);
        // The Arcane Cloak of the Chameleons (oracle-l5r-9972): "Battle: Move this Personality home."
        all[CloakHome] = new("Battle", "Move this Personality home", "none", Performer: "own-wielder", Move: new(MoveSpec.Performer, MoveSpec.Home));
        // The Obsidian Armor (oracle-l5r-9978): "Fear Battle: Target an enemy unit: Bow each Personality and Follower in it with 5 Force or lower." (A unit is named by its Personality.)
        all[ObsidianBow] = new("Battle", "Fear Battle: Target an enemy unit: Bow each Personality and Follower in it with 5 Force or lower", "enemy-personality", ActionKeywords: ["Fear"]);
        // The Pearl-Encrusted Staff of the Cobras (oracle-l5r-9981): "Reaction: Before a Battle action resolves, target a Personality: Negate his movement from the action's effects."
        all[PearlNegate] = new("Reaction", "Before a Battle action resolves, target a Personality: Negate his movement from the action's effects", "personality", Trigger: "before-action-resolves");
        // The Yari of the Shabura (oracle-l5r-9984): "Reaction: After engaging, target a Personality opposing this card: While this card remains in play, actions he performs this battle may not target
        // cards in its unit (when the action is performed)." / "Battle: Destroy this card: Ranged 4 Attack. You may put this card into your hand after this battle ends."
        all[YariBar] = new("Reaction", "After engaging, target a Personality opposing this card: While this card remains in play, actions he performs this battle may not target cards in its unit (when the action is performed)",
            EnemyOpposingSource, Trigger: "after-engaging");
        all[YariRanged] = new("Battle", "Destroy this card: Ranged 4 Attack. You may put this card into your hand after this battle ends", "attack", Costs: [new(CostVerbs.Sacrifice)]);
        // The return half is Never Safe's shape: a Reaction the card takes from a discard pile in the window after the battle it was destroyed in.
        all[YariReturn] = new("Reaction", "After this battle ends, if this card is in your discard pile, you may put it into your hand", "none",
            Trigger: ReactionTriggers.AfterBattleResolution, FromDiscard: true, Cost: GoldCostRule.Fixed(0));
        // The Ivory Sword (oracle-l5r-9976): "After an action resolves that was performed by a dishonorable Personality while he opposed this card: His controller chooses and discards a card." A trait (no
        // designator), modelled as a Reaction-timed ability like the others (TraitReactions); the other player chooses and discards (Isawa Kimi - exp 2's decision).
        all[IvoryDiscard] = new("Reaction", "After an action resolves that was performed by a dishonorable Personality while he opposed this card: His controller chooses and discards a card", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Mantle of the Jade Champion (oracle-l5r-9967): "Reaction: Even if this card is bowed, when paying for a Spell: Produce 4 Gold." An alternate Gold source that does not bow its card and is usable
        // while it is bowed (Library of Rebirth's shape, ReactiveGoldSource), for a payment for a Spell.
        all[JadeGold] = new("Reaction", "Even if this card is bowed, when paying for a Spell: Produce 4 Gold", "none", Trigger: "paying-gold-cost",
            PaymentProduction: 4, PaymentBows: false, PaymentWhen: PayingForSpellWhen, PaymentEvenIfBowed: true);
        // The Blessed Mantle of the Greensnakes (oracle-l5r-9977, the scan): "Limited: Bow this card and discard the Imperial Favor: Draw a card." ("When a Lobby action checks Family Honor, your Honor is
        // treated as 20 points higher" and "This Personality may lobby ignoring the cost of bowing" are its statics: LobbyHonorBonus and the Lobby action's bow cost.)
        all[BlessedMantleDraw] = new("Limited", "Bow this card and discard the Imperial Favor: Draw a card", "none", Costs: [new(CostVerb.Bow), new(CostVerbs.DiscardFavor)]);
        // Family Sword (oracle-l5r-10412, the scan): "Open: Even if this card is bowed, target your Personality with the same Family Name as this Personality: Transfer this card to the target."
        all[FamilyTransfer] = new("Open", "Even if this card is bowed, target your Personality with the same Family Name as this Personality: Transfer this card to the target", FamilyMate);
        return all;
    }
}

public static class PromoEmperorItemsLooks
{
    /// <summary>Lantern of Revelation: "Look at the top card of one of your decks. You may put it at the bottom." Either of the player's own decks (the printed "one of your decks").</summary>
    public static LookSpec Lantern { get; } = new("pe-lantern-revelation", "Look at the top card of one of your decks", [new("Fate"), new("Dynasty")], 1, [new(LookDisposition.Bottom, 0, 1)]);
}

/// <summary>The Yari of the Shabura: <paramref name="ActorId"/>'s actions may not target cards in the unit of <paramref name="YariId"/> during battle number <paramref name="Battle"/>, while the Yari remains in play.</summary>
public sealed record PromoYariBar(string ActorId, string YariId, int Battle);

public sealed partial class MatchState
{
    /// <summary>Enigmatic Pendant: how many cards the hand held when the last "discard your hand" cost was paid (the draw that follows is that many).</summary>
    [JsonPropertyOrder(4201)]
    public int PromoItemsHandDiscardCount { get; set; }
    /// <summary>The Yari of the Shabura's standing restrictions (see <see cref="PromoYariBar"/>); an entry of an earlier battle holds nothing.</summary>
    [JsonPropertyOrder(4202)]
    public List<PromoYariBar> PromoItemsYariBars { get; set; } = [];
}

public sealed partial class MatchEngine
{
    // ---- attach ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // EquipGoldCost: "Attaches to a [X] Champion ignoring costs" -- nothing is paid to attach it to a Champion it may attach to.
    private bool PromoItemsAttachesFree(CardInstance card, CardInstance wielder) =>
        PromoEmperorItemsAbilities.FreeAttachIds.Contains(Definition(card).Id, StringComparer.Ordinal) && Definition(card).AttachRequiresKeyword is { } required && KeywordOrHuman(wielder, required);

    // ---- statics -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> PromoAttached(CardInstance host, string effect) => Attachments(host.Id).Where(a => Definition(a).Effect == effect);

    // Mantle of the Onyx Champion: "a Force bonus equal to the highest Focus Value among Spells in its unit".
    private int PromoHighestSpellFocus(CardInstance host) => Attachments(host.Id).Where(a => Definition(a).Type == CardType.Spell).Select(a => Definition(a).Focus).DefaultIfEmpty(0).Max();

    // The Battle Standards of a player in play at a battlefield: attached to a Personality standing there.
    private IEnumerable<CardInstance> PromoStandardsAt(int ownerId, int? province) =>
        province is null ? [] : _state.Cards.Values.Where(s => s.OwnerId == ownerId && s.Zone == Zone.Attached && Definition(s).Effect == PromoEmperorItemsAbilities.StandardEffect
            && s.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield && host.ProvinceIndex == province);

    // PersonalityForce's hook.
    private int PromoItemsPersonalityForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || !InPlay(card)) return 0;
        var force = 0;
        int swords = 0, mantles = 0;
        foreach (var attached in Attachments(card.Id))
        {
            var effect = Definition(attached).Effect;
            if (effect == PromoEmperorItemsAbilities.ImperialSwordEffect) swords++;
            else if (effect is PromoEmperorItemsAbilities.OnyxEffect or PromoEmperorItemsAbilities.JadeEffect) mantles++;
        }
        // Imperial Sword: "This Personality has +1F while he is Imperial."
        if (swords > 0 && Keyword(card, "Imperial")) force += swords;
        // Mantle of the Onyx Champion, Mantle of the Jade Champion: "This card has a Force bonus equal to the highest Focus Value among Spells in its unit" (it conveys to the wielder like any Item's).
        if (mantles > 0) force += mantles * PromoHighestSpellFocus(card);
        // Battle Standard of the Naga: "Your other Personalities and Followers at this card's battlefield have +1F."
        if (card.Zone == Zone.Battlefield) force += PromoStandardsAt(card.OwnerId, card.ProvinceIndex).Count(s => s.AttachedTo != card.Id);
        return force;
    }

    // FollowerForce's hook: the Standard's +1F to each of its controller's Followers whose Personality stands at the Standard's battlefield.
    private int PromoItemsFollowerForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Follower || card.Zone != Zone.Attached || card.AttachedTo is not { } parentId || !_state.Cards.TryGetValue(parentId, out var parent) || parent.Zone != Zone.Battlefield) return 0;
        return PromoStandardsAt(card.OwnerId, parent.ProvinceIndex).Count();
    }

    // StaticPersonalHonorBonus's term: Kimono "+1PH while he is a Turquoise Champion", The Emerald Armor "+1PH".
    private int PromoItemsPersonalHonorBonus(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || !InPlay(card)) return 0;
        int bonus = 0, kimonos = 0;
        foreach (var attached in Attachments(card.Id))
        {
            var effect = Definition(attached).Effect;
            if (effect == PromoEmperorItemsAbilities.EmeraldEffect) bonus++;
            else if (effect == PromoEmperorItemsAbilities.KimonoEffect) kimonos++;
        }
        if (kimonos > 0 && Keyword(card, "Turquoise Champion")) bonus += kimonos;
        return bonus;
    }

    // Keyword()'s hook: The Emerald Armor "This Personality has +1PH and Imperial - Magistrate", Mantle of the Onyx Champion "Spells in this unit have Maho". Live, never a durable grant.
    private bool PromoItemsKeywordGranted(CardInstance card, string keyword)
    {
        if (keyword is not ("Imperial" or "Magistrate" or "Maho" or PromoEmperorItemsAbilities.LobbiesWithoutBowing)) return false;
        if (keyword == PromoEmperorItemsAbilities.LobbiesWithoutBowing)
            return Definition(card).Type == CardType.Personality && InPlay(card) && PromoAttached(card, PromoEmperorItemsAbilities.BlessedMantleEffect).Any();
        if (keyword == "Maho")
            return card.Zone == Zone.Attached && Definition(card).Type == CardType.Spell && card.AttachedTo is { } hostId && Attachments(hostId).Any(a => Definition(a).Effect == PromoEmperorItemsAbilities.OnyxEffect);
        return Definition(card).Type == CardType.Personality && card.Zone != Zone.Dead && Attachments(card.Id).Any(a => Definition(a).Effect == PromoEmperorItemsAbilities.EmeraldEffect);
    }

    // ---- destruction -------------------------------------------------------------------------------------------------------------------------------------------------------

    // DestroyAttachment's negation (GiftArmorNegatesDestruction's term). Ancient Armor: "Negate any destruction of this card, except for its Personality being destroyed" (the destruction of the
    // Personality takes it along and does not come through here). Six-Ring Sword: "Before the first time each turn another player's effect from a card destroys this card: Negate this card's destruction."
    private bool PromoItemsNegateDestruction(CardInstance card)
    {
        var effect = Definition(card).Effect;
        if (effect == PromoEmperorItemsAbilities.QamarEffect) return true;
        if (effect != PromoEmperorItemsAbilities.SixRingEffect || !CurrentActionFacts(out _, out var actorId) || actorId == card.OwnerId || HiromiUnnegatable()) return false; // a destruction that will not be negated spends nothing
        if (card.AbilityUses.TryGetValue("pe-six-ring-negate", out var used) && used == _state.TurnNumber) return false;
        card.AbilityUses["pe-six-ring-negate"] = _state.TurnNumber;
        return true;
    }

    // ---- targeting ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool _promoProbe;

    // The Emerald Armor: "Other players may not target this Personality with actions while there is another Personality at his location who is a legal target for the action." The Arcane Cloak:
    // "Other players may not target cards in this unit with actions if there are any other legal targets in this army." Judged inside target legality, like Goju Yurishi's trait (TargetOfLastResort.cs):
    // every other card the action could name instead is tried against the very same validation, and the card is refused when one is accepted. The probe never nests.
    private void RequirePromoItemsNotProtected(GameCommand command, AbilityDefinition ability, AbilityDefinition? slotAbility)
    {
        RequirePromoYariNotBarred(command, ability);
        if (_promoProbe || command.TargetId is null || ability.Slots is not null) return;
        if (!_state.Cards.TryGetValue(command.TargetId, out var target) || !InPlay(target) || target.OwnerId == command.PlayerId) return;
        var host = Definition(target).Type == CardType.Personality ? target : target.Zone == Zone.Attached && target.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var parent) ? parent : null;
        if (host is null || !InPlay(host)) return;
        var emerald = Definition(target).Type == CardType.Personality && PromoAttached(host, PromoEmperorItemsAbilities.EmeraldEffect).Any();
        var cloak = PromoAttached(host, PromoEmperorItemsAbilities.CloakEffect).Any();
        if (!emerald && !cloak) return;
        _promoProbe = true;
        try
        {
            foreach (var other in SlotCandidateCards(command.PlayerId))
            {
                if (other.Id == target.Id) continue;
                var emeraldAlternative = emerald && Definition(other).Type == CardType.Personality && SameLocation(other, host);
                var cloakAlternative = cloak && InPlay(other) && other.OwnerId == host.OwnerId && UnitHostId(other) != host.Id && SameLocation(Card(UnitHostId(other)), host);
                if (!emeraldAlternative && !cloakAlternative) continue;
                try { ValidateAbility(command with { TargetId = other.Id }, slotAbility); }
                catch (RuleViolation) { continue; }
                throw new RuleViolation($"{Definition(target).Name} may not be targeted by another player's action while another legal target ({Definition(other).Name}) is available.");
            }
        }
        finally { _promoProbe = false; }
    }

    // The Yari of the Shabura: "While this card remains in play, actions he performs this battle may not target cards in its unit (when the action is performed)." The bar is checked as the action is
    // announced, for every card it names (TargetBars.cs' channels), like Superior Reach's.
    private void RequirePromoYariNotBarred(GameCommand command, AbilityDefinition ability)
    {
        if (_state.PromoItemsYariBars.Count == 0 || PerformingCard(command, ability) is not { } actor) return;
        foreach (var bar in _state.PromoItemsYariBars.Where(b => b.ActorId == actor.Id && b.Battle == _state.BattleNumber))
        {
            if (!_state.Cards.TryGetValue(bar.YariId, out var yari) || yari.Zone != Zone.Attached) continue;
            foreach (var targetId in BarredTargetChannelIds(command))
                if (_state.Cards.TryGetValue(targetId, out var target) && UnitHostId(target) == UnitHostId(yari))
                    Require(false, $"{Definition(target).Name} is in {Definition(yari).Name}'s unit: {Definition(actor).Name}'s actions may not target it this battle.");
        }
    }

    // ---- Family Sword --------------------------------------------------------------------------------------------------------------------------------------------------------

    // Words that are ordinary English, not a family name, as the Torn Asunder supplement's rule of thumb has it ("the," "Master"): the first word of a two-word title is the family name when it is not one of
    // these and the Personality carries a Clan alignment (or a keyword ending in "Clan"). A three-word title or a Clanless Personality has no family name.
    private static readonly string[] PromoNotFamilyWords = ["the", "a", "an", "master", "lord", "lady", "old", "great", "honorable", "young", "little", "captain", "wise"];

    private string? PromoFamilyName(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality) return null;
        var words = Experienced.BaseTitle(Definition(card).Name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != 2 || PromoNotFamilyWords.Contains(words[0], StringComparer.OrdinalIgnoreCase)) return null;
        return ClanAlignments(card).Length > 0 || (Definition(card).Keywords ?? []).Any(k => k.EndsWith(" Clan", StringComparison.Ordinal)) ? words[0] : null;
    }

    private static bool PromoItemsEvenIfBowed(string abilityId) => abilityId == PromoEmperorItemsAbilities.FamilyTransfer;

    // LobbyHonorBonus's term: "When a Lobby action checks Family Honor, your Honor is treated as 20 points higher" -- 20 for each Mantle of the Greensnakes in play under the player's control.
    private int PromoItemsLobbyHonorBonus(PlayerState player) =>
        20 * _state.Cards.Values.Count(c => c.OwnerId == player.Id && c.Zone == Zone.Attached && Definition(c).Effect == PromoEmperorItemsAbilities.BlessedMantleEffect);

    // TargetAllows: Bokken's "enemy Personalities or Followers with 6 or lower Force"; the Yari's "a Personality opposing this card"; Family Sword's "your Personality with the same Family Name as this
    // Personality" (a transfer the card's own attach restrictions allow, CanTransferTo).
    private bool PromoItemsTargetAllows(string category, PlayerState player, CardInstance? source, CardInstance target)
    {
        if (category == PromoEmperorItemsAbilities.FamilyMate)
            return source is { Zone: Zone.Attached, AttachedTo: { } familyHostId } && _state.Cards.TryGetValue(familyHostId, out var familyHost) && target.OwnerId == player.Id && InPlay(target)
                && PromoFamilyName(familyHost) is { } family && PromoFamilyName(target) == family && CanTransferTo(source, target);
        if (category == PromoEmperorItemsAbilities.EnemyOpposingSource)
            return target.OwnerId != player.Id && Definition(target).Type == CardType.Personality && target.Zone == Zone.Battlefield && source is { Zone: Zone.Attached, AttachedTo: { } hostId }
                && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield && host.ProvinceIndex == target.ProvinceIndex;
        if (category != PromoEmperorItemsAbilities.EnemyForceLte6) return false;
        var type = Definition(target).Type;
        return target.OwnerId != player.Id && (type == CardType.Personality && InPlay(target) || type == CardType.Follower && target.Zone == Zone.Attached) && CardForce(target) <= 6;
    }

    // ---- the Yari's return and the Ivory Sword's trait ----------------------------------------------------------------------------------------------------------------------------

    // AfterResolutionApplies' term: the Yari, destroyed by its own Battle action in THIS battle, may be put into its owner's hand now. Null: not this card's.
    private bool? PromoItemsAfterResolutionApplies(CardInstance source, string abilityId) =>
        abilityId == PromoEmperorItemsAbilities.YariReturn ? source.Zone is Zone.FateDiscard && source.ReturnAfterBattle == _state.BattleNumber : null;

    // AfterActionResolvedApplies' term: "After an action resolves that was performed by a dishonorable Personality while he opposed this card: His controller chooses and discards a card." The performer
    // stood at the Sword's wielder's battlefield for the other player; his controller has a card in his hand to discard. Null: not this card's.
    private bool? PromoItemsAfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        if (abilityId != PromoEmperorItemsAbilities.IvoryDiscard) return null;
        if (WielderOf(source) is not { Zone: Zone.Battlefield } wielder || facts.PerformerId is not { } performerId || !_state.Cards.TryGetValue(performerId, out var performer)) return false;
        return Definition(performer).Type == CardType.Personality && performer.Dishonorable && performer.OwnerId != playerId && performer.Zone == Zone.Battlefield
            && performer.ProvinceIndex == wielder.ProvinceIndex && Player(1 - playerId).Hand.Count > 0;
    }

    // ---- Reactions ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // Ancient Armor's Reaction: another player's action targets the Personality and could destroy him (an Attack always could, an ability on DestroysTarget's curated list).
    private bool PromoItemsTargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected) =>
        abilityId == PromoEmperorItemsAbilities.QamarNegate && subject.Command.PlayerId != card.OwnerId && WielderOf(card) is { } wielder && affected.Id == wielder.Id
        && Definition(affected).Type == CardType.Personality && (subject.IsAttack || DestroysTarget(subject.Command.AbilityId!));

    // The Pearl-Encrusted Staff: any player's Battle action that would move a Personality, whose movement can be negated. Null: not this card's Reaction.
    private bool? PromoItemsBeforeActionApplies(CardInstance card, string abilityId, GameCommand command)
    {
        if (abilityId != PromoEmperorItemsAbilities.PearlNegate) return null;
        return InPlay(card) && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) && IsAction(acting) && IsBattleAction(command)
            && !MovementCannotBeNegated(command) && DeferredActionMovers(command).Any();
    }

    // Kimono: any Honor loss, played in the window before it.
    private bool? PromoItemsHonorChangeApplies(CardInstance card, string abilityId, PendingHonorChange change) =>
        abilityId == PromoEmperorItemsAbilities.KimonoNegate ? InPlay(card) && change.Amount < 0 : null;

    // ---- the Enigmatic Pendant's count ---------------------------------------------------------------------------------------------------------------------------------------

    private void PromoItemsNoteHandDiscard(int count) => _state.PromoItemsHandDiscardCount = count;

    // ---- attacks -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int? PromoItemsFixedStrength(string abilityId) => abilityId switch
    {
        PromoEmperorItemsAbilities.ExquisiteMelee => 6,
        PromoEmperorItemsAbilities.OnyxRanged => 5,
        PromoEmperorItemsAbilities.YariRanged => 4,
        _ => null
    };

    private int? PromoItemsAttackStrength(string abilityId, string? performerId)
    {
        if (PromoItemsFixedStrength(abilityId) is { } fixedStrength) return fixedStrength;
        if (abilityId != PromoEmperorItemsAbilities.DaggerMelee) return null;
        // "This Personality's Force before you destroyed this card": the Dagger is already destroyed (its cost), so its own +1F is counted back in.
        var command = _runningCommand ?? _state.PendingAttack?.Command;
        if (performerId is null || !_state.Cards.TryGetValue(performerId, out var wielder) || command?.CardId is null || !_state.Cards.TryGetValue(command.CardId, out var dagger)) return 0;
        return PersonalityForce(wielder) + Math.Max(0, Definition(dagger).Force + StatModifier(dagger, "Force"));
    }

    private static string? PromoItemsAttackKind(string abilityId) => abilityId switch
    {
        PromoEmperorItemsAbilities.ExquisiteMelee or PromoEmperorItemsAbilities.DaggerMelee => "Melee",
        PromoEmperorItemsAbilities.OnyxRanged or PromoEmperorItemsAbilities.YariRanged => "Ranged",
        _ => null
    };

    // ---- execution ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this batch's. false: the effects are done. true: a window opened (an Attack's reaction window).
    private bool? RunPromoEmperorItems(RunHookContext context)
    {
        var (id, ability, command, player, source, consumedGrant, bowedBeforeAction) = (context.id, context.ability, context.command, context.player, context.source, context.consumedGrant, context.bowedBeforeAction);
        switch (id)
        {
            case PromoEmperorItemsAbilities.YariBar:
                _state.PromoItemsYariBars.Add(new(command.TargetId!, source!.Id, _state.BattleNumber));
                Emit("targetBarred", $"{Definition(Card(command.TargetId!)).Name}'s actions may not target cards in {Definition(source).Name}'s unit this battle.", player.Id, source.Id);
                return false;
            case PromoEmperorItemsAbilities.BlessedMantleDraw:
                Draw(player);
                return false;
            case PromoEmperorItemsAbilities.FamilyTransfer:
                TransferAttachment(source!, Card(command.TargetId!), Definition(source!).Name);
                return false;
            case PromoEmperorItemsAbilities.YariReturn:
                ReturnToHand(source!); source!.ReturnAfterBattle = -1;
                return false;
            case PromoEmperorItemsAbilities.IvoryDiscard:
                return BeginDeciderChoice(DeciderSpecs.KimiDiscard, command, consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, bowedBeforeAction, context.preExistingGrants);
            case PromoEmperorItemsAbilities.YariRanged:
                // "You may put this card into your hand after this battle ends": remembered for the after-resolution window. The Attack is its wielder's (the Yari was destroyed as the cost).
                source!.ReturnAfterBattle = _state.BattleNumber;
                return ResolveOrDeferAttack(id, player, command.TargetId, source.LastHostId ?? ActionPerformer(ability, command, source).Id, consumedGrant);
            case PromoEmperorItemsAbilities.QamarNegate:
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            case PromoEmperorItemsAbilities.PearlNegate:
                NegateEffects(Card(command.TargetId!), "move");
                return false;
            case PromoEmperorItemsAbilities.BokkenBow:
                foreach (var target in SlotCards(command, "targets").ToArray())
                    if (InPlay(target) && !StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                return false;
            case PromoEmperorItemsAbilities.ObsidianBow:
                foreach (var member in UnitMembers(Card(command.TargetId!)).Where(c => Definition(c).Type is CardType.Personality or CardType.Follower && CardForce(c) <= 5).ToArray())
                    if (!StalwartNegatesBow(member, player.Id)) member.Bowed = true;
                return false;
            case PromoEmperorItemsAbilities.PendantDraw:
                {
                    var count = _state.PromoItemsHandDiscardCount; _state.PromoItemsHandDiscardCount = 0;
                    for (var i = 0; i < count; i++) Draw(player);
                    return false;
                }
            case PromoEmperorItemsAbilities.FudoCharge:
                {
                    if (WielderOf(source!) is not { } wielder) return false;
                    var other = Player(1 - player.Id);
                    var tokens = other.Honor >= 20 || other.Provinces.Count(p => !p.Destroyed) > player.Provinces.Count(p => !p.Destroyed) ? 2 : 1;
                    for (var i = 0; i < tokens; i++) CreateToken(wielder, "Charging", id);
                    return false;
                }
            case PromoEmperorItemsAbilities.KimonoNegate:
                if (_state.PendingHonorChange is { } held) RemC.NegateLossesActionKey = held.ActionKey;
                Emit("honorLossesNegated", $"{Definition(source!).Name}: every Honor loss from the action's effects is negated.", player.Id, source!.Id);
                return false;
            case PromoEmperorItemsAbilities.CloakHome:
                ExecuteMove(command, ability, source, null);
                return false;
            case PromoEmperorItemsAbilities.LanternLook:
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBeforeAction); // the look finishes the action (DeckLook.cs)
                return true;
            case PromoEmperorItemsAbilities.DaggerMelee:
                // The Dagger was destroyed as the cost: the Attack is its wielder's (the Personality it was attached to), not the destroyed card's.
                return ResolveOrDeferAttack(id, player, command.TargetId, source!.LastHostId ?? ActionPerformer(ability, command, source).Id, consumedGrant);
            case PromoEmperorItemsAbilities.ExquisiteMelee:
            case PromoEmperorItemsAbilities.OnyxRanged:
                return ResolveOrDeferAttack(id, player, command.TargetId, ActionPerformer(ability, command, source).Id, consumedGrant);
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoEmperorItemsHooks()
    {
        EngineHooks.Abilities.Register("AddPromoEmperorItems", 12400, static abilities => abilities.AddPromoEmperorItems());
        EngineHooks.Looks.Register("PromoEmperorItemsLooks", 12400, static () => [PromoEmperorItemsLooks.Lantern]);
        EngineHooks.TargetAllows.Register("PromoEmperorItemsTargetAllows", 12400, static (engine, context) => engine.PromoItemsTargetAllows(context.ability.Target, context.player, context.source, context.target));
        EngineHooks.TargetedReaction.Register("PromoEmperorItemsTargetedReaction", 12400, static (engine, context) => engine.PromoItemsTargetedReactionApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.RunEffects.Register("RunPromoEmperorItems", 12400, static (engine, context) => engine.RunPromoEmperorItems(context));
        EngineHooks.AttackStrengthAfter.Register("PromoEmperorItemsAttackStrength", 12400, static (engine, context) => engine.PromoItemsAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("PromoEmperorItemsAttackKind", 12400, static context => PromoItemsAttackKind(context.abilityId));
        EngineHooks.BowsTarget.Register("PromoEmperorItemsBowsTarget", 12400, static context => context.abilityId is PromoEmperorItemsAbilities.BokkenBow);
        EngineHooks.PersonalityForce.Register("PromoEmperorItemsForce", 12400, static (engine, context) => engine.PromoItemsPersonalityForce(context.card));
    }
}
