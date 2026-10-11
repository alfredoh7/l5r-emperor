namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group A: the twenty-four Strategies of strategy-wave-58 -- A Forefather's Vengeance, A Paragon's Strength, A Pure Stroke,
// A Time for Mortal Men, Ancestral Protection, Aramoro's Promise, Bad Kharma, Bend like a Reed, Besieged, Breath of the Heavens, Burn the Towers, Caught in the Act, Control the
// Field, Dangerous Indulgence, Deadly Orders, Deception Revealed, Duel of Serpents, Extended Maneuvers, Face of Ninube, Falling Leaf Strike, Final Confrontation, Flooded Pass,
// Footsteps of Madness and Fortitude. The declarative ability data is here (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this
// file, each naming its card. Every card's text is the SCAN's (the Oracle wording differs for several); readings the printed text leaves open are recorded at the card.
//
// New primitives:
//   * PERFORMER CATEGORIES with an ANY-of keyword list on a card that is not necessarily a Personality: "rem-a-own-personality-with-any-keyword" (Aramoro's Promise: Saboteur or Ninja),
//     "rem-a-own-unbowed-unit-with-any-keyword" (Duel of Serpents: a Nonhuman or Spirit Personality OR Follower), "rem-a-own-personality-scout-tactician-or-chi-3-at-battle" (Control the Field).
//   * TARGET CATEGORIES: a Weapon in play, a Personality with Chi at most the performer's (either side), a Personality or Follower, an enemy unit whose total Force is below the
//     performer's BASE Force, a Ring that the last resolved action bowed, and Aramoro's optional "a Dynasty card in a province".
//   * A TARGETED-BY-ACTION REACTION WITH A PERFORMER TO CHOOSE (Ancestral Protection): one offered option per Ancestor that could pay the destruction cost.
//   * A HAND REACTION TO THE ANNOUNCEMENT OF ANOTHER PLAYER'S ACTION (Bad Kharma, Footsteps of Madness) that negates it whole: the Unimpeachable Name window is opened for it too, the
//     negated action is resolved through ResolveNegatedAction, and Bad Kharma's "ignores the action's costs" is a flag that resolution reads (nothing of the action is paid).
//   * A VARIABLE GOLD COST READ OFF THE HELD ACTION'S STRONGHOLD (Footsteps of Madness): CostStatSubject.HeldActionSource / CostStat.GoldProduction.
//   * A COST "SHUFFLE YOUR HAND INTO YOUR FATE DECK" (Face of Ninube): CostVerbs.ShuffleHand; the draw effect reads the count.
//   * A NEW WINDOW BEFORE A DUEL'S FOCUS EFFECTS RESOLVE (Bend like a Reed): after the Duelists' re-focus, before the Focus Effects are ordered, either player holding a Reaction
//     with the trigger "before-focus-effects" may play it; a duel remembers who struck and carries "+N to the duel stat until the duel ends" per Personality.
//   * A BEFORE-RESOLUTION TRAIT WITH TWO INDEPENDENT OPTIONAL CHOICES (Extended Maneuvers): straighten a Samurai and/or bow a non-Samurai at the battlefield.
//   * AN ITEM'S FORCE AND CHI MODIFIERS RAISED (A Pure Stroke): Modifiers on the Item itself, summed into its wielder's Force and Chi like the printed modifiers.
//   * A TERRAIN'S PROVINCE STRENGTH TRAIT and a Terrain play that needs no presence at the battlefield (Flooded Pass, Effect "flooded-pass").
//   * A DELAYED "AFTER THIS BATTLE'S RESOLUTION" DESTRUCTION (Deadly Orders) and "the action bowed a Ring" tracking on the action record (Deception Revealed).
public static class EmperorRemainingAAbilities
{
    public const string ProvinceCardOrNone = "rem-a-province-card-or-none";
    public const string BeforeFocusEffects = "before-focus-effects";
    /// <summary>Bad Kharma / Footsteps of Madness: a Strategy in a hand reacting to another player's announced action (a Strategy is played from a hand only, never from a discard pile).</summary>
    public const string BeforeAnyActionStrategy = "before-any-action-resolves-strategy";
    /// <summary>The CostStatSubject of Footsteps of Madness: the Stronghold the held action is from.</summary>
    public const string HeldActionSource = "held-action-source";
    public const string ExtendedManeuversTrait = "extended-maneuvers-before-battle";
    public const string FloodedPassEffect = "flooded-pass";

    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingA(this Dictionary<string, AbilityDefinition> all)
    {
        // A Forefather's Vengeance (oracle-l5r-23): "Limited: Gain 3 Honor, or 1 Honor for each Ancestor you have in play, whichever is lower." and "Battle: Destroy your performing unbowed
        // Ancestor and target an enemy card without attachments: Destroy it." (Visitation's destruction cost, Tactical Assault's target).
        all["forefather-honor"] = new("Limited", "Gain 3 Honor, or 1 Honor for each Ancestor you have in play, whichever is lower", "none");
        all["forefather-destroy"] = new("Battle", "Destroy your performing unbowed Ancestor and target an enemy card without attachments: Destroy it", "enemy-card-without-attachments",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ancestor"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        // A Paragon's Strength (oracle-l5r-61): "Battle: Choose your performing unbowed Paragon: Ranged Attack with strength equal to his Force. Lose 4 Honor." and "Battle: Choose your
        // performing unbowed Paragon and target an enemy Personality with equal or lower Personal Honor: Move him home. Gain 2 Honor." The Honor loss is an effect independent of the Attack.
        all["paragon-strength-attack"] = new("Battle", "Choose your performing unbowed Paragon: Ranged Attack with strength equal to his Force. Lose 4 Honor", "attack",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Paragon"]);
        all["paragon-strength-move"] = new("Battle", "Choose your performing unbowed Paragon and target an enemy Personality with equal or lower Personal Honor: Move him home. Gain 2 Honor", "enemy-personality-honor-lte-performer",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Paragon"]);
        // A Pure Stroke (oracle-l5r-68): "Battle/Open: Target a Weapon: Raise its Force and Chi modifiers to twice its base Force and Chi modifiers (this does not affect negative base modifiers)."
        all["pure-stroke-raise"] = new("BattleOrOpen", "Target a Weapon: Raise its Force and Chi modifiers to twice its base Force and Chi modifiers", "rem-a-weapon");
        // A Time for Mortal Men (oracle-l5r-90): "Battle/Open: Destroy your performing Ancestor and target your Personality: Give him +3F."
        all["mortal-men-boost"] = new("BattleOrOpen", "Destroy your performing Ancestor and target your Personality: Give him +3F", "own-personality",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Ancestor"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        // Ancestral Protection (oracle-l5r-370): "Reaction: After an action targets your Personality, destroy your performing Ancestor: Negate the action's effects."
        all["ancestral-protection-negate"] = new("Reaction", "After an action targets your Personality, destroy your performing Ancestor: Negate the action's effects", "none",
            Trigger: ReactionTriggers.TargetedByAction, Performer: "own-personality-with-keyword", RequiredKeywords: ["Ancestor"], Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        // Aramoro's Promise (oracle-l5r-424, a Ninja card): "Limited: Choose your performing Saboteur or Ninja Personality: Discard a Dynasty card in a province. Lose 2 Honor." The card is chosen
        // with the command (nothing is targeted in the rules' sense -- a province card reacts to nothing), any player's, face-down ones included; none may be named only when no province holds one.
        all["aramoro-discard"] = new("Limited", "Choose your performing Saboteur or Ninja Personality: Discard a Dynasty card in a province. Lose 2 Honor", ProvinceCardOrNone,
            Performer: "rem-a-own-personality-with-any-keyword", RequiredKeywords: ["Saboteur", "Ninja"]);
        // Bad Kharma (oracle-l5r-663): "Reaction: After another player with Family Honor of -1 or lower announces an action on a Strategy: He ignores the action's costs. Negate its effects."
        all["bad-kharma-negate"] = new("Reaction", "After another player with Family Honor of -1 or lower announces an action on a Strategy: He ignores the action's costs. Negate its effects", "none", Trigger: BeforeAnyActionStrategy);
        // Bend like a Reed (oracle-l5r-954): "You may only play one copy of Bend like a Reed per duel." "Reaction: Before Focus Effects resolve, if your performing Personality in the duel did not
        // strike: Give him +2 to his duel stat until the duel ends." The performing Personality is the player's own Personality in the duel (implicit: there is one).
        all["bend-reed-boost"] = new("Reaction", "Before Focus Effects resolve, if your performing Personality in the duel did not strike: Give him +2 to his duel stat until the duel ends", "none", Trigger: BeforeFocusEffects);
        // Besieged (oracle-l5r-963, a Siege, Unique card): "Open: Destroy a performing Personality you own and control: Increase or decrease the strength of all players' provinces by an amount
        // equal to the Personality's base Chi." The choice is two ability ids sharing one use (Expensive Achievement's shape).
        all["besieged-raise"] = new("Open", "Destroy a performing Personality you own and control: Increase the strength of all players' provinces by the Personality's base Chi", "none",
            Performer: "own-personality", UsageKey: "besieged", Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        all["besieged-lower"] = new("Open", "Destroy a performing Personality you own and control: Decrease the strength of all players' provinces by the Personality's base Chi", "none",
            Performer: "own-personality", UsageKey: "besieged", Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        // Breath of the Heavens (oracle-l5r-1135, a Fire Tattoo card): "Battle: Choose your performing unbowed Fire or Tattooed Personality: Ranged 3 Attack with +1 strength for each Fire token on
        // your Personality. Give your Personality a +1F Fire token." The strength is read before the new token (the Attack is the first sentence).
        all["breath-heavens-attack"] = new("Battle", "Choose your performing unbowed Fire or Tattooed Personality: Ranged 3 Attack with +1 strength for each Fire token on your Personality. Give your Personality a +1F Fire token", "attack",
            Performer: "own-unbowed-personality-with-any-keyword-at-battle", RequiredKeywords: ["Fire", "Tattooed"]);
        // Burn the Towers (oracle-l5r-1180, a Recon card): "Reaction: After engaging, choose your performing unbowed Scout Personality at the current battlefield: You have Reconnaissance there.
        // Give its province -3 strength." (Watchers in the Dark's Engage-window Reaction.)
        all["burn-towers-recon"] = new("Reaction", "After engaging, choose your performing unbowed Scout Personality at the current battlefield: You have Reconnaissance there. Give its province -3 strength", "none",
            Trigger: "after-engaging", Performer: "own-unbowed-personality-with-keyword-explicitly-at-battle", RequiredKeywords: ["Scout"]);
        // Caught in the Act (oracle-l5r-1238): "Reaction: After another player's action targets one or more of your Personalities: Negate their bowing from the action's effects."
        all["caught-act-negate-bow"] = new("Reaction", "After another player's action targets one or more of your Personalities: Negate their bowing from the action's effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        // Control the Field (oracle-l5r-1487): "Battle: Choose your performing Personality with Scout, Tactician, or 3 or higher Chi, and target a Terrain: Destroy it. You may take an additional
        // Battle action to play a Terrain." (Control the Board's destruction; the additional action is scoped to the Terrains in the player's hand, Plain of Gold's grant.)
        all["control-field-destroy"] = new("Battle", "Choose your performing Personality with Scout, Tactician, or 3 or higher Chi, and target a Terrain: Destroy it. You may take an additional Battle action to play a Terrain", "terrain-at-battle",
            Performer: "rem-a-own-personality-scout-tactician-or-chi-3-at-battle");
        // Dangerous Indulgence (oracle-l5r-1820): "Battle: Choose your performing unbowed Tactician and target a Personality with equal or lower Chi: Bow or straighten him." -- the two effects
        // are two ability ids sharing one use; "equal or lower Chi" compares against the performing Tactician's current Chi (Fear Me!'s reading).
        all["dangerous-indulgence-bow"] = new("Battle", "Choose your performing unbowed Tactician and target a Personality with equal or lower Chi: Bow him", "rem-a-personality-chi-lte-performer",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Tactician"], UsageKey: "dangerous-indulgence");
        all["dangerous-indulgence-straighten"] = new("Battle", "Choose your performing unbowed Tactician and target a Personality with equal or lower Chi: Straighten him", "rem-a-personality-chi-lte-performer",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Tactician"], UsageKey: "dangerous-indulgence");
        // Deadly Orders (oracle-l5r-1885): "Battle: Choose your performing Commander and target your Follower: Give it a Force bonus equal to twice its current Force. After this battle's
        // resolution, destroy the Follower."
        all["deadly-orders-boost"] = new("Battle", "Choose your performing Commander and target your Follower: Give it a Force bonus equal to twice its current Force. After this battle's resolution, destroy the Follower", "own-follower",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Commander"]);
        // Deception Revealed (oracle-l5r-1908): "Reaction: After the resolution of an action that bowed a Ring: Straighten it." and "Battle: Target a Personality or Follower: Give it -4F. Bow it if you
        // control a Ring."
        all["deception-revealed-straighten"] = new("Reaction", "After the resolution of an action that bowed a Ring: Straighten it", "rem-a-ring-bowed-by-last-action", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        all["deception-revealed-weaken"] = new("Battle", "Target a Personality or Follower: Give it -4F. Bow it if you control a Ring", "rem-a-personality-or-follower");
        // Duel of Serpents (oracle-l5r-2246): "Open: If another player has higher Family Honor than you, bow your performing Nonhuman or Spirit: Gain 3 Honor." and "Battle: Choose your
        // performing unbowed Nonhuman or Spirit and target an enemy card without attachments: Bow it." -- a Nonhuman or Spirit Personality or Follower.
        all["duel-serpents-honor"] = new("Open", "If another player has higher Family Honor than you, bow your performing Nonhuman or Spirit: Gain 3 Honor", "none",
            Performer: "rem-a-own-unbowed-unit-with-any-keyword", RequiredKeywords: ["Nonhuman", "Spirit"], Costs: ActionCost.BowPerformer);
        all["duel-serpents-bow"] = new("Battle", "Choose your performing unbowed Nonhuman or Spirit and target an enemy card without attachments: Bow it", "enemy-card-without-attachments",
            Performer: "rem-a-own-unbowed-unit-with-any-keyword", RequiredKeywords: ["Nonhuman", "Spirit"]);
        // Extended Maneuvers (oracle-l5r-2419, a Terrain): "Before this battle resolves: You may choose and straighten a Samurai Personality at the current battlefield, and you may choose and bow a
        // non-Samurai Personality there." -- a before-resolution trait; its Terrain play is the ordinary one.
        all[ExtendedManeuversTrait] = new("BeforeResolution", "Before this battle resolves: You may choose and straighten a Samurai Personality at the current battlefield, and you may choose and bow a non-Samurai Personality there", "none", Trigger: "before-battle-resolves");
        // Face of Ninube (oracle-l5r-2431): "Open: Shuffle your hand into your Fate deck: Draw a number of cards equal to the number you shuffled in." The card being played is not in the hand
        // that is shuffled (the same reading as "the card you play is not the card you discard").
        all["face-ninube-draw"] = new("Open", "Shuffle your hand into your Fate deck: Draw a number of cards equal to the number you shuffled in", "none", Costs: [new(CostVerbs.ShuffleHand)]);
        // Falling Leaf Strike (oracle-l5r-2454, a Kiho card): "Battle: Choose your performing unbowed Monk and target an enemy Personality or Follower: Move your Monk to the current battlefield. Give
        // the target -3F. Bow it if its Force is now 0." -- an Absent, move-to-battle action whose Monk may perform from anywhere he can move from (The Lesson of Earth's shape).
        all["falling-leaf-strike"] = new("Battle", "Choose your performing unbowed Monk and target an enemy Personality or Follower: Move your Monk to the current battlefield. Give the target -3F. Bow it if its Force is now 0", "enemy-force",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Monk"], Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        // Final Confrontation (oracle-l5r-2547, an Iaijutsu Political card): "As a Focus Effect: After this duel ends, if your Personality won it, take the Imperial Favor." and "Battle: Choose your
        // performing unbowed Duelist and target one or two Personalities whose total Chi is lower than or equal to your Duelist's Chi: Bow them."
        all["final-confrontation-focus"] = new("FocusEffect", "As a Focus Effect: After this duel ends, if your Personality won it, take the Imperial Favor", "none");
        all["final-confrontation-bow"] = new("Battle", "Choose your performing unbowed Duelist and target one or two Personalities whose total Chi is lower than or equal to your Duelist's Chi: Bow them", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Duelist"],
            Slots: [new("targets", "rem-a-personality-chi-lte-performer", "One or two Personalities whose total Chi is at most your Duelist's", 1, 2)]);
        // Footsteps of Madness (oracle-l5r-2629): "Reaction: Before the resolution of an action from a Stronghold, pay Gold equal to the Stronghold's base Gold Production: Negate the action's effects."
        all["footsteps-madness-negate"] = new("Reaction", "Before the resolution of an action from a Stronghold, pay Gold equal to the Stronghold's base Gold Production: Negate the action's effects", "none",
            Trigger: BeforeAnyActionStrategy, Cost: GoldCostRule.StatValue(HeldActionSource, CostStat.GoldProduction, basePrinted: true));
        // Fortitude (oracle-l5r-2671): "Battle: Choose your performing unbowed Berserker and target an enemy unit with lower total Force than your Berserker's base Force: Bow the target. Negate all
        // current and new Force penalties on your Berserker from actions." (Tireless Efforts' negation, ForcePenalties.cs.) The unit's leader, its Personality, is the target that bows.
        all["fortitude-bow"] = new("Battle", "Choose your performing unbowed Berserker and target an enemy unit with lower total Force than your Berserker's base Force: Bow the target. Negate all current and new Force penalties on your Berserker from actions", "rem-a-enemy-unit-total-force-lt-performer-base",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Berserker"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    /// <summary>The Ring bowed by the action recorded on the action record (Deception Revealed).</summary>
    public const string BowedRingKind = "bowed-ring";

    // Ranged Attack strengths of this file (AttackStrength's chain): read live off the performing Personality (sourceId).
    private int? RemAAttackStrength(string abilityId, string? sourceId) => abilityId switch
    {
        "paragon-strength-attack" => sourceId is not null ? CardForce(Card(sourceId)) : 0,
        "breath-heavens-attack" => 3 + (sourceId is not null ? TokenCount(Card(sourceId), "Fire") : 0),
        _ => null
    };

    // ---- performer categories --------------------------------------------------------------------------------------------------------------------------

    private static bool RemAOwnsPerformerCategory(string? category) =>
        category is "rem-a-own-personality-with-any-keyword" or "rem-a-own-unbowed-unit-with-any-keyword" or "rem-a-own-personality-scout-tactician-or-chi-3-at-battle";

    private bool RemAAnyKeyword(CardInstance card, string[]? keywords) => (keywords ?? []).Any(k => Keyword(card, k));

    // The validation half: the shared performer check has already required the card to be the player's own and in play.
    private bool RemAPerformerAllows(string? category, CardInstance performer, AbilityDefinition ability, bool actingAsBattle)
    {
        var type = Definition(performer).Type;
        return category switch
        {
            // Aramoro's Promise: "your performing Saboteur or Ninja Personality" -- wherever he stands and bowed or not (a Limited action).
            "rem-a-own-personality-with-any-keyword" => type == CardType.Personality && RemAAnyKeyword(performer, ability.RequiredKeywords),
            // Duel of Serpents: "your performing (unbowed) Nonhuman or Spirit" -- a Personality or an attached Follower; at the battlefield only while acting as a Battle action.
            "rem-a-own-unbowed-unit-with-any-keyword" => (type == CardType.Personality || type == CardType.Follower && performer.Zone == Zone.Attached) && !performer.Bowed
                && RemAAnyKeyword(performer, ability.RequiredKeywords) && (!actingAsBattle || AtBattle(performer)),
            // Control the Field: "your performing Personality with Scout, Tactician, or 3 or higher Chi" (no "unbowed" is printed; the current Chi).
            "rem-a-own-personality-scout-tactician-or-chi-3-at-battle" => type == CardType.Personality && (Keyword(performer, "Scout") || Keyword(performer, "Tactician") || PersonalityChi(performer) >= 3)
                && (!actingAsBattle || AtBattle(performer)),
            _ => false
        };
    }

    // The listing half (AbilityActions): null when the category is not this file's.
    private string?[]? RemAPerformerCandidates(AbilityDefinition ability, int playerId, bool actingAsBattle)
    {
        if (!RemAOwnsPerformerCategory(ability.Performer)) return null;
        return _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && RemAPerformerAllows(ability.Performer, c, ability, actingAsBattle))
            .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();
    }

    // A Reaction's performers for the listing of a targeted-by-action Reaction (Ancestral Protection): every card of the category the player controls.
    private IEnumerable<CardInstance> RemAReactionPerformers(int playerId, AbilityDefinition reaction) => reaction.Performer switch
    {
        "own-personality-with-keyword" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && HasKeywords(c, reaction.RequiredKeywords))
            .OrderBy(c => c.Id, StringComparer.Ordinal),
        // The Loyalty of the Scorpion (ruling pass R2, issue #492): "your performing Scorpion Clan Personality" -- every Personality of the category the player controls.
        { } category when category.StartsWith("rr2-", StringComparison.Ordinal) => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && RulingR2PerformerAllows(reaction, c, false)).OrderBy(c => c.Id, StringComparer.Ordinal),
        Wave829Abilities.BentenPerformer => Wave829RedirectPerformers(playerId, reaction),
        _ => throw new InvalidOperationException($"A targeted-by-action Reaction with the performer '{reaction.Performer}' is not listed.")
    };

    // ---- target categories -----------------------------------------------------------------------------------------------------------------------------

    // The in-play categories (the tail of ValidateAbility's target switch): the target is a card in play.
    private bool RemATargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var type = Definition(target).Type;
        var personality = type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        return category switch
        {
            // A Pure Stroke: "a Weapon" -- an attached Item with the Weapon keyword, either player's.
            "rem-a-weapon" => target.Zone == Zone.Attached && type == CardType.Item && Keyword(target, "Weapon"),
            // Dangerous Indulgence / Final Confrontation: "a Personality with equal or lower Chi" than the performing Personality's current Chi (either side's).
            "rem-a-personality-chi-lte-performer" => personality && command.PerformerId is not null && PersonalityChi(target) <= PersonalityChi(Card(command.PerformerId)),
            // Deception Revealed: "a Personality or Follower".
            "rem-a-personality-or-follower" => personality || type == CardType.Follower && target.Zone == Zone.Attached,
            // Fortitude: "an enemy unit with lower total Force than your Berserker's base Force" -- the unit's Personality is the target (core rules: a unit is targeted through its Personality).
            "rem-a-enemy-unit-total-force-lt-performer-base" => personality && !own && command.PerformerId is not null && BattleForce(target) < Definition(Card(command.PerformerId)).Force,
            // Deception Revealed's Reaction: a Ring the action just resolved bowed (the record is read as the after-action window opens), still bowed.
            "rem-a-ring-bowed-by-last-action" => type == CardType.Ring && target.Bowed && _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == BowedRingKind && a.CardId == target.Id),
            _ => false
        };
    }

    // Aramoro's Promise: "a Dynasty card in a province" -- any player's, face-down included (only an id is offered; nothing is disclosed until it is discarded face-up).
    private IEnumerable<CardInstance> RemAProvinceCards() => _state.Cards.Values.Where(c => c.Zone == Zone.Province && IsDynastyCardType(Definition(c).Type)).OrderBy(c => c.Id, StringComparer.Ordinal);

    private void ValidateRemAProvinceCardOrNone(GameCommand command)
    {
        if (command.TargetId is null)
        {
            Require(!RemAProvinceCards().Any(), "Choose a Dynasty card in a province to discard.");
            return;
        }
        Require(_state.Cards.TryGetValue(command.TargetId, out var card) && card.Zone == Zone.Province && IsDynastyCardType(Definition(card).Type), "Choose a Dynasty card in a province.");
    }

    private string?[] RemAProvinceCardCandidates() => RemAProvinceCards().Select(c => (string?)c.Id).Append(null).ToArray();

    // ---- validation (ValidateAbility, beside the other per-id Requires) --------------------------------------------------------------------------------------

    private void ValidateEmperorRemainingA(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        // The Unimpeachable Name window is opened for the Strategy Reactions below as well: Unimpeachable Name itself is offered only when its own condition holds.
        if (id == "unimpeachable-name-reaction")
            Require(_state.PendingHonorChange is not null || UnimpeachableNameAvailable(player.Id, _state.PendingOutOfPlayActionReaction?.AbilityId), "Unimpeachable Name needs a dishonorable Personality or an Honor loss to react to.");
        CardInstance? performer = command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var chosen) ? chosen : null;
        switch (id)
        {
            // Besieged: "a performing Personality you own AND control" -- a card taken from its owner is not his to sacrifice.
            case "besieged-raise":
            case "besieged-lower":
                if (performer is not null) Require(performer.OriginalOwnerId is null, "You must own and control the performing Personality.");
                break;
            // Duel of Serpents: "If another player has higher Family Honor than you".
            case "duel-serpents-honor":
                Require(Player(1 - player.Id).Honor > player.Honor, "Another player must have higher Family Honor than you.");
                break;
            // Bad Kharma: another player with Family Honor of -1 or lower announced an action on a Strategy (the action held in the window).
            case "bad-kharma-negate":
                Require(_state.PendingOutOfPlayActionReaction is { } held && held.PlayerId != player.Id && Player(held.PlayerId).Honor <= -1, "Another player with Family Honor of -1 or lower must have announced the action.");
                Require(_state.PendingOutOfPlayActionReaction?.CardId is { } heldCard && _state.Cards.TryGetValue(heldCard, out var heldSource) && Definition(heldSource).Type == CardType.Strategy, "The action must be an action on a Strategy.");
                break;
            // Footsteps of Madness: the held action is from a Stronghold.
            case "footsteps-madness-negate":
                Require(_state.PendingOutOfPlayActionReaction?.CardId is { } stronghold && _state.Cards.TryGetValue(stronghold, out var heldStronghold) && Definition(heldStronghold).Type == CardType.Stronghold, "The action must be an action from a Stronghold.");
                break;
            // Bend like a Reed: the acting player's own Personality in the duel did not strike, and no copy has been played in this duel.
            case "bend-reed-boost":
                Require(_state.PendingDuel is { Stage: "FocusEffectsReaction" } duel && duel.StrikerPlayerId is int striker && striker != player.Id, "Your Personality in the duel must not have struck.");
                Require(_state.PendingDuel?.BendUsed != true, "You may only play one copy of Bend like a Reed per duel.");
                break;
            // Final Confrontation: "one or two Personalities whose total Chi is lower than or equal to your Duelist's Chi".
            case "final-confrontation-bow" when ability.Slots is not null:
                Require(performer is not null && SlotCards(command, "targets").Sum(PersonalityChi) <= PersonalityChi(performer), "The targets' total Chi must be lower than or equal to your Duelist's Chi.");
                break;
        }
    }

    // ---- reactions to an action that targets a Personality (TargetedActionReactionApplies' default) --------------------------------------------------------------

    private bool RemATargetedActionReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        var command = subject.Command;
        return abilityId switch
        {
            // Ancestral Protection: any action (either player's) targets one of the reactor's Personalities, and an Ancestor of his can pay the destruction.
            "ancestral-protection-negate" => affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality
                && RemAReactionPerformers(card.OwnerId, AbilityCatalog.All[abilityId]).Any(),
            // Caught in the Act: another player's action targets one or more of the reactor's Personalities and bows its target.
            "caught-act-negate-bow" => command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality
                && command.AbilityId is not null && BowsTarget(command.AbilityId),
            _ => false
        };
    }

    private static bool RemABowsTarget(string abilityId) => abilityId is "duel-serpents-bow" or "dangerous-indulgence-bow" or "deception-revealed-weaken" or "fortitude-bow" or "falling-leaf-strike"
        or "final-confrontation-bow";

    // ---- the Strategy Reactions to an announced action (Bad Kharma, Footsteps of Madness) ------------------------------------------------------------------------

    // Opens the Unimpeachable Name window for a player who holds one of these in a hand and could play it now against the held command.
    private bool RemAOutOfPlayReactionAvailable(int reactingPlayerId, GameCommand command)
    {
        var reactor = Player(reactingPlayerId);
        var saved = _state.PendingOutOfPlayActionReaction;
        _state.PendingOutOfPlayActionReaction = command;
        try
        {
            foreach (var (card, abilityId) in OutOfPlayReactions(reactingPlayerId, EmperorRemainingAAbilities.BeforeAnyActionStrategy))
            {
                var probe = new GameCommand("useAbility", reactingPlayerId, _state.Version, CardId: card.Id, AbilityId: abilityId);
                var quote = QuoteAbilityGold(probe);
                if (quote is not null && (quote.Error is not null || quote.Cost > 0 && quote.Payments.Length == 0)) continue;
                try { RemAValidateHeldReaction(abilityId, reactor); return true; }
                catch (RuleViolation) { }
            }
            return false;
        }
        finally { _state.PendingOutOfPlayActionReaction = saved; }
    }

    private void RemAValidateHeldReaction(string abilityId, PlayerState player)
    {
        var held = _state.PendingOutOfPlayActionReaction;
        if (abilityId == "bad-kharma-negate")
        {
            Require(held is not null && held.PlayerId != player.Id && Player(held.PlayerId).Honor <= -1, "Another player with Family Honor of -1 or lower must have announced the action.");
            Require(held?.CardId is { } heldCard && _state.Cards.TryGetValue(heldCard, out var heldSource) && Definition(heldSource).Type == CardType.Strategy, "The action must be an action on a Strategy.");
        }
        else if (abilityId == "footsteps-madness-negate")
            Require(held?.CardId is { } stronghold && _state.Cards.TryGetValue(stronghold, out var heldStronghold) && Definition(heldStronghold).Type == CardType.Stronghold, "The action must be an action from a Stronghold.");
    }

    // The Stronghold card the held action is from: the priced subject of Footsteps of Madness.
    private CardInstance? RemAHeldActionSourceCard() =>
        _state.PendingOutOfPlayActionReaction?.CardId is { } id && _state.Cards.TryGetValue(id, out var card) ? card : null;

    // The held action, once its window closed: negated as a whole by a Strategy Reaction of this file. Returns whether it was, and whether its costs are ignored (Bad Kharma).
    private bool RemATakeHeldNegation(out bool ignoreCosts)
    {
        var negated = _state.RemANegatesHeldAction; ignoreCosts = _state.RemAIgnoresHeldCosts;
        _state.RemANegatesHeldAction = false; _state.RemAIgnoresHeldCosts = false;
        return negated;
    }

    // ---- execution (RunAbilityEffects' default): null when the id is not this file's; false when its effects are done; true when a window opened -------------------------------

    private bool? RunEmperorRemainingA(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        var name = source is null ? "an action" : Definition(source).Name;
        switch (id)
        {
            // A Forefather's Vengeance.
            case "forefather-honor":
                GainHonor(player.Id, Math.Min(3, _state.Cards.Values.Count(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Ancestor"))), name, source);
                return false;
            case "forefather-destroy":
                DestroyTargetCard(target!, player.Id);
                return false;
            // A Paragon's Strength: the Honor loss, then the Attack; the move, then the Honor gain (the performer's, so a dishonorable Paragon is rehonored instead).
            case "paragon-strength-attack":
                {
                    var paragon = Card(command.PerformerId!);
                    GainHonor(player.Id, -4, name, source);
                    return ResolveOrDeferAttack(id, player, target?.Id, paragon.Id, consumedGrant);
                }
            case "paragon-strength-move":
                MoveHome(target!, false);
                GainHonorFromPerformer(Card(command.PerformerId!), 2, name);
                return false;
            // A Pure Stroke: each of the Weapon's Force and Chi modifiers with a positive base is raised to twice its base (nothing changes for a base of 0 or less, or a modifier already there).
            case "pure-stroke-raise":
                RaiseWeaponModifiers(target!, source!.Id, id);
                return false;
            // A Time for Mortal Men: the Ancestor was destroyed as the cost; a target that left with him (it was the Ancestor) gets nothing.
            case "mortal-men-boost":
                if (InPlay(target!)) ApplyModifier(target!, source!.Id, id, "Force", 3);
                return false;
            // Ancestral Protection: the Ancestor was destroyed as the cost.
            case "ancestral-protection-negate":
                NegateHeldAction(source!);
                return false;
            // Aramoro's Promise.
            case "aramoro-discard":
                if (target is not null && target.Zone == Zone.Province) DiscardProvinceCard(target);
                GainHonor(player.Id, -2, name, source);
                return false;
            // Bad Kharma / Footsteps of Madness: the held action is negated as a whole when its window closes.
            case "bad-kharma-negate":
                _state.RemANegatesHeldAction = true; _state.RemAIgnoresHeldCosts = true;
                Emit("actionNegated", $"{name}: the action is negated and its costs are ignored.", player.Id, source?.Id);
                return false;
            case "footsteps-madness-negate":
                _state.RemANegatesHeldAction = true;
                Emit("actionNegated", $"{name}: the action is negated.", player.Id, source?.Id);
                return false;
            // Bend like a Reed.
            case "bend-reed-boost":
                {
                    var duel = _state.PendingDuel!;
                    duel.BendUsed = true;
                    var duelist = Card(duel.ChallengerPlayerId == player.Id ? duel.ChallengerId : duel.ChallengedId);
                    duel.StatBonuses[duelist.Id] = duel.StatBonuses.GetValueOrDefault(duelist.Id) + 2;
                    Emit("duelStatRaised", $"{name}: {Definition(duelist).Name} has +2 to his duel stat until the duel ends.", player.Id, duelist.Id);
                    return false;
                }
            // Besieged: every province of every player, by the sacrificed Personality's base Chi.
            case "besieged-raise":
            case "besieged-lower":
                {
                    var amount = Definition(Card(command.PerformerId!)).Chi * (id == "besieged-raise" ? 1 : -1);
                    foreach (var owner in _state.Players) foreach (var province in owner.Provinces.Where(p => !p.Destroyed)) GiveProvinceStrengthThisTurn(owner.Id, province.Index, amount, name); // until end of turn (issue #492 ruling R3)
                    return false;
                }
            // Breath of the Heavens: the Ranged 3 Attack (+1 for each Fire token now on the Personality), then the new +1F Fire token.
            case "breath-heavens-attack":
                {
                    var performer = Card(command.PerformerId!);
                    var opened = ResolveOrDeferAttack(id, player, target?.Id, performer.Id, consumedGrant);
                    if (InPlay(performer)) CreateToken(performer, "Fire", id, "Force", 1);
                    return opened;
                }
            // Burn the Towers.
            case "burn-towers-recon":
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                if (_state.CurrentBattleProvince is int towered) GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, towered, -3, name); // until end of turn (issue #492 ruling R3)
                return false;
            // Caught in the Act: the bowing of every Personality of the reactor's the held action targets is negated.
            case "caught-act-negate-bow":
                {
                    var held = HeldAction();
                    var targeted = ReactionWindowTargets(held.Command).OfType<string>().Append(held.TargetId).OfType<string>().Distinct(StringComparer.Ordinal)
                        .Select(Card).Where(c => c.OwnerId == player.Id && Definition(c).Type == CardType.Personality).ToArray();
                    foreach (var personality in targeted) NegateEffects(personality, "bow");
                    return false;
                }
            // Control the Field: destroy the Terrain; an additional Battle action may play a Terrain (scoped to the Terrains in the hand).
            case "control-field-destroy":
                {
                    DiscardTerrain(target!); player.TerrainDestroyedThisBattle = true;
                    var terrains = player.Hand.Select(Card).Where(c => Definition(c).Type == CardType.Strategy && Keyword(c, "Terrain")).Select(c => c.Id).ToArray();
                    if (terrains.Length > 0) GrantAdditionalAction(player.Id, terrains);
                    return false;
                }
            // Dangerous Indulgence.
            case "dangerous-indulgence-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "dangerous-indulgence-straighten":
                Straighten(target!);
                return false;
            // Deadly Orders: twice its current Force as a bonus now; destroyed when the battle's resolution is over.
            case "deadly-orders-boost":
                ApplyModifier(target!, source!.Id, id, "Force", 2 * CardForce(target!));
                ScheduleDelayedEffect(target!, DelayedKinds.Destroy, DelayUntil.EndOfBattle, null, name, source.Id); // the shared delay store (issue #492, EmperorRulingR2.cs): destroyed once the battle has ended
                return false;
            // Deception Revealed.
            case "deception-revealed-straighten":
                Straighten(target!);
                return false;
            case "deception-revealed-weaken":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                if (ControlsAnyRing(player.Id) && InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Duel of Serpents: the performer was bowed as the cost of the Open half.
            case "duel-serpents-honor":
                {
                    var serpent = Card(command.PerformerId!);
                    if (Definition(serpent).Type == CardType.Personality) GainHonorFromPerformer(serpent, 3, name); else GainHonor(player.Id, 3, name, source);
                    return false;
                }
            case "duel-serpents-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Face of Ninube: the hand was shuffled in as the cost; the count is what is drawn.
            case "face-ninube-draw":
                for (var i = 0; i < _state.RemAShuffledCount; i++) Draw(player);
                _state.RemAShuffledCount = 0;
                return false;
            // Falling Leaf Strike: the Monk moves to the battlefield, the target gets -3F, and bows if its Force is now 0.
            case "falling-leaf-strike":
                ExecuteMove(command, ability, source, target);
                ApplyModifier(target!, source!.Id, id, "Force", -3);
                if (InPlay(target!) && CardForce(target!) == 0 && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Final Confrontation.
            case "final-confrontation-bow":
                foreach (var bowed in SlotCards(command, "targets").ToArray())
                    if (InPlay(bowed) && !StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                return false;
            // Fortitude.
            case "fortitude-bow":
                BowUnitByEffect(target!, player.Id); // "Bow the target" -- the target is an enemy unit (issue #492, ruling R5: every card in the unit bows, each unless its own bowing is prevented)
                NegateForcePenalties(Card(command.PerformerId!));
                return false;
            default:
                return null;
        }
    }

    // A Pure Stroke: "Raise its Force and Chi modifiers to twice its base Force and Chi modifiers (this does not affect negative base modifiers)." The Item's modifiers are its printed
    // Force and Chi plus the Modifiers on it (PersonalityForce / PersonalityChi sum them for the wielder); one that is already at or above twice its base is left as it is.
    private void RaiseWeaponModifiers(CardInstance weapon, string source, string effect)
    {
        var def = Definition(weapon);
        foreach (var (stat, printed) in new[] { ("Force", def.Force), ("Chi", def.Chi) })
        {
            if (printed <= 0) continue;
            var current = printed + StatModifier(weapon, stat);
            if (current >= 2 * printed) continue;
            weapon.Modifiers.Add(new(source, effect, stat, 2 * printed - current));
            Emit("modifierApplied", $"{def.Name}: its {stat} modifier is raised to {2 * printed:+#;-#;0}.", weapon.OwnerId, weapon.Id);
        }
    }

    // The wielder's Force and Chi read the modifiers on the Items too (A Pure Stroke); an Item with none adds 0, so nothing else changes.
    private int RemAItemForceModifiers(CardInstance card) => Attachments(card.Id).Where(c => Definition(c).Type == CardType.Item).Sum(c => StatModifier(c, "Force"));
    private int RemAItemChiModifiers(CardInstance card) => Attachments(card.Id).Where(c => Definition(c).Type == CardType.Item).Sum(c => StatModifier(c, "Chi"));

    // Aramoro's Promise: the card leaves its province face-up into its owner's Dynasty discard pile, and the province is refilled face-down as after any discard from it.
    private void DiscardProvinceCard(CardInstance card)
    {
        var owner = Player(card.OwnerId);
        var province = owner.Provinces.Single(p => p.CardId == card.Id);
        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null; card.FaceUp = true;
        province.CardId = null;
        Emit("provinceDiscard", $"{Definition(card).Name} was discarded from {owner.Name}'s province {province.Index + 1}.", owner.Id, card.Id);
        Refill(owner, province);
    }

    // ---- Face of Ninube's cost --------------------------------------------------------------------------------------------------------------------------------

    private void RemAShuffleHandIntoDeck(PlayerState player, CardInstance? played)
    {
        var cards = player.Hand.Where(id => id != played?.Id).ToArray();
        foreach (var id in cards)
        {
            player.Hand.Remove(id);
            var card = Card(id);
            card.Zone = Zone.FateDeck; card.FaceUp = false; card.ProvinceIndex = null;
            player.FateDeck.Add(id);
        }
        ShuffleDeck(player, SearchDeck.Fate);
        _state.RemAShuffledCount = cards.Length;
        Emit("handShuffled", $"{player.Name} shuffled {cards.Length} card{(cards.Length == 1 ? "" : "s")} from hand into the Fate deck.", player.Id);
    }

    // ---- Deadly Orders: its destruction "after this battle's resolution" is a delayed effect (DelayUntil.EndOfBattle, EmperorRulingR2.cs). ----

    // ---- Deception Revealed -------------------------------------------------------------------------------------------------------------------------------------

    // Mendon's R4 ruling (issue #492): "Focus effects are not actions, only effects that start with Limited, Open, Battle, Reaction ... that bow a ring may be used to straighten it. This includes the
    // ring bowing to pay a cost or being bowed as an effect of an action." So the Rings an ACTION bowed -- as a cost or as an effect, whichever way the action resolved (an ability, the Attack or the
    // duel it declared) -- are the ones on its record; a Ring bowed while a Focus Effect resolves is excluded from the duel's action.
    private HashSet<string> BowedRingIds() => _state.Cards.Values.Where(c => c.Bowed && Definition(c).Type == CardType.Ring).Select(c => c.Id).ToHashSet();

    private void ExcludeFocusEffectRingBows(HashSet<string> ringsBowedBefore)
    {
        if (_state.PendingDuel is not { RingsBowedBefore: { } tracked }) return;
        foreach (var id in BowedRingIds()) if (!ringsBowedBefore.Contains(id) && !tracked.Contains(id)) tracked.Add(id);
    }

    // The Rings this action bowed (as an effect or a cost) are put on its record.
    private void RecordBowedRings(HashSet<string> bowedBeforeAction)
    {
        foreach (var ring in _state.Cards.Values.Where(c => Definition(c).Type == CardType.Ring && InPlay(c) && c.Bowed && !bowedBeforeAction.Contains(c.Id)))
            RecordActionEffect(ring.Id, BowedRingKind);
    }

    // ---- Flooded Pass --------------------------------------------------------------------------------------------------------------------------------------------

    // "This province has +2 strength for each unit in the attacking army": read live, while the Terrain stands at the battlefield of that province during its battle.
    private int RemATerrainProvinceBonus(ProvinceState province)
    {
        if (CurrentProvinceIndex() != province.Index || !ProvinceIsBattlefieldOfCurrentBattle(province)) return 0;
        var battlefield = _state.CurrentBattleProvince!.Value;
        var floods = TerrainsAtBattlefield(battlefield).Count(t => Definition(t).Effect == EmperorRemainingAAbilities.FloodedPassEffect);
        return floods == 0 ? 0 : 2 * floods * Army(_state.ActivePlayerId, battlefield).Count();
    }

    // "Battle: Even if you control no units at the current battlefield, if there are no Terrains there: Put this card into play there." -- the Terrain play needs no presence.
    private static bool TerrainPlaysWithoutPresence(CardDefinition definition) => definition.Effect == EmperorRemainingAAbilities.FloodedPassEffect;

    // ---- Bend like a Reed: the window before a duel's Focus Effects resolve ---------------------------------------------------------------------------------------------

    private CardInstance RemADuelPersonality(PendingDuel duel, int playerId) => Card(duel.ChallengerPlayerId == playerId ? duel.ChallengerId : duel.ChallengedId);

    private bool RemAHoldsFocusEffectsReaction(PendingDuel duel, int playerId)
    {
        if (duel.StrikerPlayerId is not int striker || striker == playerId || duel.BendUsed) return false;
        return Player(playerId).Hand.Select(Card).Any(c => (Definition(c).Abilities ?? []).Contains("bend-reed-boost"));
    }

    // Called where the Duelists' re-focus is done and the Focus Effects are about to be ordered: the window opens when someone could play a Reaction to it.
    private void BeginOrderingEffectsAfterReactions()
    {
        var duel = _state.PendingDuel!;
        duel.Stage = "FocusEffectsReaction"; // one window for every "Before Focus Effects resolve" Reaction: a held one (Bend like a Reed, group A) or one in play (Falling Rain Dojo, group F)
        _state.PendingChoice = new("duelReaction", _state.ActivePlayerId, null, "Before Focus Effects resolve: react or pass."); // set first: the in-play Reactions are validated against the open window
        if (!RemAHoldsFocusEffectsReaction(duel, 0) && !RemAHoldsFocusEffectsReaction(duel, 1) && !InPlayBeforeFocusReactionAvailable()) { _state.PendingChoice = null; ContinueToOrderingEffects(); return; }
        var first = RemAHoldsFocusEffectsReaction(duel, _state.ActivePlayerId) || !RemAHoldsFocusEffectsReaction(duel, 1 - _state.ActivePlayerId) ? _state.ActivePlayerId : 1 - _state.ActivePlayerId;
        _state.PendingChoice = new("duelReaction", first, null, "Before Focus Effects resolve: react or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = first;
        Emit("reactionWindow", "Before-Focus-Effects reaction window opened.", first);
    }

    private int RemADuelStatBonus(CardInstance card) => _state.PendingDuel?.StatBonuses.GetValueOrDefault(card.Id) ?? 0;

    // ---- Extended Maneuvers: its before-resolution trait, answered through "beforeBattleOptional" -----------------------------------------------------------------------

    private bool IsExtendedManeuversChoice(PendingBeforeBattleResolve pending) =>
        pending.ActiveSourceId is { } itemId && _state.Cards.TryGetValue(itemId, out var source) && BeforeResolutionAbilityId(source) == EmperorRemainingAAbilities.ExtendedManeuversTrait;

    private IEnumerable<CardInstance> ExtendedManeuversPersonalities(int provinceIndex) =>
        _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal);

    // A Samurai worth straightening is a bowed one; a non-Samurai worth bowing is an unbowed one.
    private IEnumerable<CardInstance> ExtendedManeuversSamurai(int provinceIndex) => ExtendedManeuversPersonalities(provinceIndex).Where(c => Keyword(c, "Samurai") && c.Bowed);
    private IEnumerable<CardInstance> ExtendedManeuversOthers(int provinceIndex) => ExtendedManeuversPersonalities(provinceIndex).Where(c => !Keyword(c, "Samurai") && !c.Bowed);

    // True when the choice opened (something can be straightened or bowed); false when there is nothing to choose and the battle carries on.
    private bool OpenExtendedManeuversChoice(CardInstance source, PendingBeforeBattleResolve pending)
    {
        if (!ExtendedManeuversSamurai(pending.ProvinceIndex).Any() && !ExtendedManeuversOthers(pending.ProvinceIndex).Any()) return false;
        _state.PendingChoice = new("beforeBattleOptional", source.OwnerId, source.Id, $"Before this battle resolves: you may straighten a Samurai and/or bow a non-Samurai at this battlefield ({Definition(source).Name}).");
        _state.PriorityPlayerId = source.OwnerId;
        return true;
    }

    // "use" names a Samurai to straighten (TargetId), a non-Samurai to bow (CardIds[0]), or both; "decline" does neither -- each is its own "you may".
    private void ResolveExtendedManeuvers(GameCommand command, PendingBeforeBattleResolve pending, int playerId)
    {
        if (command.Choice != "use") { Emit("beforeBattleEffect", "Extended Maneuvers: nothing was straightened or bowed.", playerId); return; }
        var samurai = command.TargetId is null ? null : Card(command.TargetId);
        var other = command.CardIds is { Length: > 0 } ids ? Card(ids[0]) : null;
        Require(samurai is not null || other is not null, "Choose a Samurai to straighten, a non-Samurai to bow, or both.");
        Require(samurai is null || ExtendedManeuversSamurai(pending.ProvinceIndex).Contains(samurai), "Choose a bowed Samurai Personality at this battlefield to straighten.");
        Require(other is null || ExtendedManeuversOthers(pending.ProvinceIndex).Contains(other), "Choose an unbowed non-Samurai Personality at this battlefield to bow.");
        if (samurai is not null) { Straighten(samurai); Emit("beforeBattleEffect", $"Extended Maneuvers: {Definition(samurai).Name} straightened.", playerId, samurai.Id); }
        if (other is not null && !StalwartNegatesBow(other, playerId)) { other.Bowed = true; Emit("beforeBattleEffect", $"Extended Maneuvers: {Definition(other).Name} bowed.", playerId, other.Id); }
    }

    private IEnumerable<ActionOption> ExtendedManeuversOptions(int playerId, string? sourceId)
    {
        if (_state.PendingBeforeBattleResolve is not { } pending || _state.PendingChoice?.PlayerId != playerId) yield break;
        yield return new("beforeBattleOptional", "Straighten and bow no card", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, Choice: "decline"), sourceId);
        var samurai = ExtendedManeuversSamurai(pending.ProvinceIndex).ToArray();
        var others = ExtendedManeuversOthers(pending.ProvinceIndex).ToArray();
        foreach (var s in samurai)
            yield return new("beforeBattleOptional", $"Straighten {Definition(s).Name}", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: s.Id, Choice: "use"), s.Id);
        foreach (var o in others)
            yield return new("beforeBattleOptional", $"Bow {Definition(o).Name}", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, CardIds: [o.Id], Choice: "use"), o.Id);
        foreach (var s in samurai)
            foreach (var o in others)
                yield return new("beforeBattleOptional", $"Straighten {Definition(s).Name} and bow {Definition(o).Name}", true, null,
                    new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: s.Id, CardIds: [o.Id], Choice: "use"), s.Id);
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingAHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorRemainingA", 400, static abilities => abilities.AddEmperorRemainingA());
        EngineHooks.AttackStrengthBefore.Register("RemAAttackStrength", 800, static (engine, context) => engine.RemAAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.PerformerAllows.Register("RemAPerformerAllows", 100, static (engine, context) => engine.RemAPerformerAllows(context.ability.Performer, context.performer, context.ability, context.actingAsBattle));
        EngineHooks.PerformerCandidates.Register("RemAPerformerCandidates", 200, static (engine, context) => engine.RemAPerformerCandidates(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.BowsTarget.Register("RemABowsTarget", 100, static context => RemABowsTarget(context.abilityId));
        EngineHooks.TargetedReaction.Register("RemATargetedActionReactionApplies", 100, static (engine, context) => engine.RemATargetedActionReactionApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorRemainingA", 300, static (engine, context) => engine.ValidateEmperorRemainingA(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorRemainingA", 100, static (engine, context) => engine.RunEmperorRemainingA(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant));
    }
}
