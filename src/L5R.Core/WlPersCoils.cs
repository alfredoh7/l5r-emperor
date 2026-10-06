using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// THE "pers-coils" CLUSTER OF THE #668 WIRE CARDS (issue #674): twenty-two Coils of Madness Personalities, personality-wave-130. Every card is built from existing mechanisms; the reading of each
// printed line is in docs/decisions/2026-10-06-674-pers-coils.md. What each card is:
//
//   Moto Xin, Yoritomo Tonogi      Expendable (the keyword) and "After X enters play, lose N Honor" (EntryHonorLoss): no ability of their own.
//   Takujin                         Expendable, and "You have +1 maximum hand size" (the Effect increase-own-hand-size, Kazehime's).
//   Kasuga Aizawa                   "Bow this card: Produce 1 Gold" is the Personality's own GoldProduction (Daidoji Soken's shape).
//   Iuchi Takaki                    "Engage: Give a target card a Madness token" is a Reaction to "after engaging" (the supplement: Engage plays exactly like it) on any in-play card.
//   Hiruma Nikaru - exp2            "Nikaru uses his printed Force as his duel stat": a term of DuelStatValue (the one-line call in Dueling.cs).
//   Kakita Hideo - exp2             "After Hideo wins a duel, give him two +1F tokens": a trait read where a duel's winner is known (the call beside Izumiko's in Dueling.cs).
//   Kakita Ichigiku - exp           Battle: +3F, or +4F with Kitsuki Kinaro; its "If no other player has more Family Honor than you" is a condition on taking the action (Haruki's Deer's shape).
//   Nishiguchi                      Battle: a Force penalty equal to the target's Madness tokens.
//   Moshi Tomiko - exp / Moto Ming-Gwok - exp   each names the other: bow your Ring or Ming-Gwok and draw / bow or straighten a Ring or Tomiko; Ming-Gwok's Water Battle is a Ranged 3 Attack
//                                   with the printed "If there is a dead Personality in any discard pile" as a condition on taking it.
//   Shiba Iaimiko - exp             Open, three uses a turn: bow your Shugenja (or Ikoma Aimi) to give him +1F, Cavalry or Conqueror (Mahatsu's shape, one use shared by three entries).
//   Shosuro Kameyoi - exp, Yasuki Tono - exp   the two Political actions; the Open player and the Holding / named-card target categories are this file's.
//   Shinjo Tselu - exp              Limited: search the Fate deck for a non-Unique Cavalry Follower, then discard a card (Ikoma Shizuka's Terrain search effect: to hand, then discard).
//   Tonbo Inuyama                   Void Limited: look at the top card of any of the four decks and may put it on the bottom (Seeking the Question's shape).
//   Kitsuki Daisuke - exp2          Limited: look at the top three cards of the Fate deck, may show an Iaijutsu Strategy into the hand, then discard a card, and put the rest back in any order; the
//                                   discard is the search flow's own "discard a card" decision (a search over the hand: nothing is shuffled), opened as the look ends (the call in DeckLook.cs).
//   Yoritomo Minori - exp           Limited: Scavenging Party's refill with a discarded (not dead) Personality, then the acting player discards a card (a decider choice that is his own).
//   Asako Chukage - exp2            Water Battle: straighten or bow a card without attachments in a unit; with a Ring the player may also remove the Ring and the card from the game. The options are
//                                   listed by the engine (the effectChoice decision), one per mode and per Ring he controls.
//   Daigotsu Arakan - exp           Maho Battle/Limited: Equip a non-Unique Follower from the discard pile to Arakan or Zansho, paying all costs; permanently give it Shadowlands and Undead; lose 2 Honor.
//   The Dark Naga - exp             Open: Equip a Nonhuman Follower from the hand or discard pile to The Dark Naga paying 4 less; Pearl Battle, bow: Ranged 9 Attack.
//   Daidoji Adachi                  A mandatory trait after an action resolves (the call in ActionEffectTracking.cs): queues the owed discard of a card the other player's unit leader owes.
//
// State: one counter (WlDaisukeShownMark) so the look's follow-up knows whether a card was taken. Hook priorities 14300-14399, JsonPropertyOrder 20300-20399.
// ======================================================================================================================================================================================

public sealed partial class MatchState
{
    /// <summary>Kitsuki Daisuke - exp2: how many cards had been shown (MatchState.ShownCards) when his look began; -1 when none is open.</summary>
    [JsonPropertyOrder(20300)]
    public int WlDaisukeShownMark { get; set; } = -1;
}

public static class WlPersCoilsAbilities
{
    public const string ChukageWater = "wl-pc-chukage-water", ArakanBattle = "wl-pc-arakan-battle", ArakanLimited = "wl-pc-arakan-limited", TakakiMadness = "wl-pc-takaki-madness",
        IchigikuForce = "wl-pc-ichigiku-force", DaisukeLook = "wl-pc-daisuke-look", TomikoBowDraw = "wl-pc-tomiko-bow-draw", MingGwokRanged = "wl-pc-mingwok-ranged", MingGwokRing = "wl-pc-mingwok-ring",
        NishiguchiPenalty = "wl-pc-nishiguchi-penalty", IaimikoForce = "wl-pc-iaimiko-force", IaimikoCavalry = "wl-pc-iaimiko-cavalry", IaimikoConqueror = "wl-pc-iaimiko-conqueror",
        TseluSearch = "wl-pc-tselu-search", KameyoiHonor = "wl-pc-kameyoi-honor", KameyoiDishonor = "wl-pc-kameyoi-dishonor", DarkNagaEquip = "wl-pc-darknaga-equip", DarkNagaRanged = "wl-pc-darknaga-ranged",
        TonboLook = "wl-pc-tonbo-look", TonoStraighten = "wl-pc-tono-straighten", MinoriRefill = "wl-pc-minori-refill";

    // Effects (traits keyed on the card's Effect).
    public const string AdachiEffect = "wl-pc-adachi", NikaruEffect = "wl-pc-nikaru", HideoEffect = "wl-pc-hideo";

    // Target and performer categories.
    public const string OwnRingOrMingGwok = "wl-pc-own-unbowed-ring-or-ming-gwok", RingOrTomiko = "wl-pc-ring-or-tomiko", HoldingOrKameyoi = "wl-pc-holding-or-kameyoi",
        PersonalityNotTono = "wl-pc-personality-not-tono", OwnShugenjaOrAimi = "wl-pc-own-unbowed-shugenja-or-aimi", ArakanOrZansho = "wl-pc-arakan-or-zansho";

    public const string MadnessToken = "Madness";

    public static Dictionary<string, AbilityDefinition> AddWlPersCoils(this Dictionary<string, AbilityDefinition> all)
    {
        // Asako Chukage - exp2 (oracle-l5r-10472): "Water Battle: Straighten or bow a target card without attachments in a unit. If you control a Ring, you may remove both it and the target from the game."
        all[ChukageWater] = new("Battle", "Water Battle: Straighten or bow a target card without attachments in a unit. If you control a Ring, you may remove both it and the target from the game", "any-unit-card",
            RequireNoAttachments: true, ActionKeywords: ["Water"]);
        // Daigotsu Arakan - exp (oracle-l5r-10485): "Maho Battle/Limited, [Gold]: Equip a non-Unique Follower in your discard pile to Arakan or Zansho. Permanently give the Follower Shadowlands - Undead. Lose 2 Honor."
        // One printed action in two timings (Waru's shape): the two entries share one use. The wielder is the chosen performer, Arakan or Zansho.
        var arakanAttach = new AttachSpec(AttachSource.Target, AttachPile.Own, AttachWielder.Performer, [CardType.Follower], ExcludedKeywords: ["Unique"]);
        all[ArakanBattle] = new("Battle", "Maho Battle: Choose Arakan or Zansho and target a non-Unique Follower in your discard pile: Equip it to him, paying all costs. Permanently give the Follower Shadowlands - Undead. Lose 2 Honor",
            "attachment-in-discard", Performer: ArakanOrZansho, UsageKey: "wl-pc-arakan", ActionKeywords: ["Maho"], Cost: GoldCostRule.AttachCost(), Attach: arakanAttach);
        all[ArakanLimited] = new("Limited", "Maho Limited: Choose Arakan or Zansho and target a non-Unique Follower in your discard pile: Equip it to him, paying all costs. Permanently give the Follower Shadowlands - Undead. Lose 2 Honor",
            "attachment-in-discard", Performer: ArakanOrZansho, UsageKey: "wl-pc-arakan", ActionKeywords: ["Maho"], Cost: GoldCostRule.AttachCost(), Attach: arakanAttach);
        // Iuchi Takaki (oracle-l5r-10498): "Engage: Give a target card a Madness token." Engage plays exactly like a Reaction to "after engaging" (Coils of Madness, major change 3).
        all[TakakiMadness] = new("Reaction", "Engage: Give a target card a Madness token", "any-card", Trigger: "after-engaging");
        // Kakita Ichigiku - exp (oracle-l5r-10447): "Battle: If no other player has more Family Honor than you, give Ichigiku +3F, or +4F if you control Kitsuki Kinaro."
        all[IchigikuForce] = new("Battle", "If no other player has more Family Honor than you, give Ichigiku +3F, or +4F if you control Kitsuki Kinaro", "none");
        // Kitsuki Daisuke - exp2 (oracle-l5r-10452): "Limited: Look at the top three cards of your Fate deck. If you find an Iaijutsu Strategy, you may show it, put it into your hand, then discard a card. Put the remaining cards back in any order."
        all[DaisukeLook] = new("Limited", "Look at the top three cards of your Fate deck. If you find an Iaijutsu Strategy, you may show it, put it into your hand, then discard a card. Put the remaining cards back in any order", "none",
            Look: WlPersCoilsLooks.Daisuke);
        // Moshi Tomiko - exp (oracle-l5r-10466): "Limited: Bow your target unbowed Ring or Moto Ming-Gwok. Draw a card." (no colon: both are effects.)
        all[TomikoBowDraw] = new("Limited", "Bow your target unbowed Ring or Moto Ming-Gwok. Draw a card", OwnRingOrMingGwok);
        // Moto Ming-Gwok - exp (oracle-l5r-10509): "Water Battle: If there is a dead Personality in any discard pile, Ranged 3 Attack." "Open: Bow or straighten a target Ring or Moshi Tomiko."
        all[MingGwokRanged] = new("Battle", "Water Battle: If there is a dead Personality in any discard pile, Ranged 3 Attack", "attack", ActionKeywords: ["Water"]);
        all[MingGwokRing] = new("Open", "Bow or straighten a target Ring or Moshi Tomiko", RingOrTomiko,
            Branch: new(BranchCondition.Always, [new("bow", "Bow it", [BranchEffect.BowTarget]), new("straighten", "Straighten it", [BranchEffect.StraightenTarget])], new("none", "Nothing", [])));
        // Nishiguchi (oracle-l5r-10488): "Battle: Give a target enemy Personality a Force penalty equal to the number of Madness tokens he has."
        all[NishiguchiPenalty] = new("Battle", "Give a target enemy Personality a Force penalty equal to the number of Madness tokens he has", "enemy-personality");
        // Shiba Iaimiko - exp (oracle-l5r-10477): "Open: Bow your target unbowed Shugenja or Ikoma Aimi to give Iaimiko +1F, Cavalry, or Conqueror. You may use this action three times per turn."
        // One printed action with three effects: three entries sharing one use count of three a turn; the bow is the cost ("to give").
        all[IaimikoForce] = new("Open", "Bow your target unbowed Shugenja or Ikoma Aimi to give Iaimiko +1F", OwnShugenjaOrAimi, UsageKey: "wl-pc-iaimiko", UsesPerTurn: 3, Costs: ActionCost.BowTarget);
        all[IaimikoCavalry] = new("Open", "Bow your target unbowed Shugenja or Ikoma Aimi to give Iaimiko Cavalry", OwnShugenjaOrAimi, UsageKey: "wl-pc-iaimiko", UsesPerTurn: 3, Costs: ActionCost.BowTarget);
        all[IaimikoConqueror] = new("Open", "Bow your target unbowed Shugenja or Ikoma Aimi to give Iaimiko Conqueror", OwnShugenjaOrAimi, UsageKey: "wl-pc-iaimiko", UsesPerTurn: 3, Costs: ActionCost.BowTarget);
        // Shinjo Tselu - exp (oracle-l5r-10511): "Limited: Search your Fate deck for a non-Unique Cavalry Follower, show it, and put it in your hand. Then, discard a card."
        all[TseluSearch] = new("Limited", "Search your Fate deck for a non-Unique Cavalry Follower, show it, and put it in your hand. Then, discard a card", "none", Search: WlPersCoilsSearches.Tselu);
        // Shosuro Kameyoi - exp (oracle-l5r-10484): "Political Open, [bow]: A target player loses 1 Honor." "Political Limited, [bow]: Dishonor a target Personality not named Yasuki Tono."
        all[KameyoiHonor] = new("Open", "Political Open, bow this card: A target player loses 1 Honor", "player", ActionKeywords: ["Political"], Costs: ActionCost.BowSource);
        all[KameyoiDishonor] = new("Limited", "Political Limited, bow this card: Dishonor a target Personality not named Yasuki Tono", PersonalityNotTono, ActionKeywords: ["Political"], Costs: ActionCost.BowSource);
        // The Dark Naga - exp (oracle-l5r-10501): "Open, [Gold]: Equip a Nonhuman Follower in your hand or discard pile to The Dark Naga, paying 4 less Gold." "Pearl Battle, [bow]: Ranged 9 Attack."
        all[DarkNagaEquip] = new("Open", "Equip a Nonhuman Follower in your hand or discard pile to The Dark Naga, paying 4 less Gold", "attachment-in-discard",
            Cost: GoldCostRule.AttachCost(-4, 0), Attach: new(AttachSource.Target, AttachPile.OwnHandOrDiscard, AttachWielder.Source, [CardType.Follower], ["Nonhuman"]));
        all[DarkNagaRanged] = new("Battle", "Pearl Battle, bow this card: Ranged 9 Attack", "attack", ActionKeywords: ["Pearl"], Costs: ActionCost.BowSource);
        // Tonbo Inuyama (oracle-l5r-10478): "Void Limited: Look at the top card of a deck. You may put it on the bottom."
        all[TonboLook] = new("Limited", "Void Limited: Look at the top card of a deck. You may put it on the bottom", "none", ActionKeywords: ["Void"], Look: WlPersCoilsLooks.Tonbo);
        // Yasuki Tono - exp (oracle-l5r-10443): "Open, [bow]: Straighten a target Holding or Shosuro Kameyoi."
        all[TonoStraighten] = new("Open", "Open, bow this card: Straighten a target Holding or Shosuro Kameyoi", HoldingOrKameyoi, Costs: ActionCost.BowSource);
        // Yoritomo Minori - exp (oracle-l5r-10471): "Limited: Put your target discarded (not dead) Personality face-up into one of your Provinces, discarding any card currently there. Discard a Fate card."
        all[MinoriRefill] = new("Limited", "Put your target discarded (not dead) Personality face-up into one of your Provinces, discarding any card currently there. Discard a Fate card", "own-discarded-personality", ChoosesOwnProvince: true);
        return all;
    }
}

public static class WlPersCoilsLooks
{
    /// <summary>Kitsuki Daisuke - exp2: the top three cards of the Fate deck; one Iaijutsu Strategy may be shown and taken; the rest go back in any order.</summary>
    public static LookSpec Daisuke { get; } = new("wl-pc-daisuke", "Look at the top three cards of your Fate deck", [new("Fate")], 3,
        [new(LookDisposition.Show, 0, 1, new("Iaijutsu Strategy", [CardType.Strategy], ["Iaijutsu"]))], Reorder: true);
    /// <summary>Tonbo Inuyama: the top card of any of the four decks; it may go to the bottom of that deck.</summary>
    public static LookSpec Tonbo { get; } = new("wl-pc-tonbo", "Look at the top card of a deck", [new("Fate"), new("Dynasty"), new("Fate", Opponent: true), new("Dynasty", Opponent: true)], 1, [new(LookDisposition.Bottom, 0, 1)]);

    public static IEnumerable<LookSpec> All => [Daisuke, Tonbo];
}

public static class WlPersCoilsSearches
{
    /// <summary>Shinjo Tselu - exp: a non-Unique Cavalry Follower from the Fate deck, shown and put in the hand; then the searcher discards a card (never the one found, Mendon's ruling on the Terrain search).</summary>
    public static SearchSpec Tselu { get; } = new("wl-pc-tselu", "Search your Fate deck for a non-Unique Cavalry Follower",
        [new(SearchDeck.Fate, new("non-Unique Cavalry Follower", [CardType.Follower], ["Cavalry"], NonUnique: true), SearchEffect.ToHandThenDiscard)]);
    /// <summary>Kitsuki Daisuke - exp2's "then discard a card": the search flow's own discard decision over the hand. It is opened as his look ends, never started by an ability, so its deck is the hand
    /// (a search of the hand shuffles nothing) and its effect the same "to hand, then discard".</summary>
    public static SearchSpec DaisukeDiscard { get; } = new("wl-pc-daisuke-discard", "Discard a card",
        [new(SearchDeck.Hand, new("Iaijutsu Strategy", [CardType.Strategy], ["Iaijutsu"]), SearchEffect.ToHandThenDiscard)]);

    public static IEnumerable<SearchSpec> All => [Tselu, DaisukeDiscard];
}

public static class WlPersCoilsSpecs
{
    /// <summary>Yoritomo Minori - exp: "Discard a Fate card" -- the acting player picks a card of his hand (a decider choice that is his own).</summary>
    public const string MinoriDiscard = "wl-pc-minori-discard";
}

public sealed partial class MatchEngine
{
    private const string WlEffectPrefix = "wlpc:";

    // ---- helpers ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "a card named X": the title, whatever the experience version printed after it.
    private static bool WlNamed(CardInstance card, string title) =>
        Definition(card).Type == CardType.Personality && string.Equals(Experienced.BaseTitle(Definition(card).Name), title, StringComparison.OrdinalIgnoreCase);

    private bool WlControlsNamed(int playerId, string title) => _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && WlNamed(c, title));

    // Bows a card as an effect of an action (a Stalwart or negated bow does not happen).
    private void WlBow(CardInstance card, int actingPlayerId)
    {
        if (!InPlay(card) || IsTerrainCard(card) || card.Bowed || StalwartNegatesBow(card, actingPlayerId)) return;
        card.Bowed = true;
    }

    // ---- target and performer categories -----------------------------------------------------------------------------------------------------------------------------

    private bool WlPersCoilsTargetAllows(string category, PlayerState player, CardInstance target)
    {
        var type = Definition(target).Type; var own = target.OwnerId == player.Id;
        return category switch
        {
            // Moshi Tomiko: "your target unbowed Ring or Moto Ming-Gwok".
            WlPersCoilsAbilities.OwnRingOrMingGwok => own && !target.Bowed && (type == CardType.Ring || WlNamed(target, "Moto Ming-Gwok")),
            // Moto Ming-Gwok: "a target Ring or Moshi Tomiko" (either player's).
            WlPersCoilsAbilities.RingOrTomiko => type == CardType.Ring || WlNamed(target, "Moshi Tomiko"),
            // Yasuki Tono: "a target Holding or Shosuro Kameyoi" (either player's).
            WlPersCoilsAbilities.HoldingOrKameyoi => type == CardType.Holding || WlNamed(target, "Shosuro Kameyoi"),
            // Shosuro Kameyoi: "a target Personality not named Yasuki Tono" (either side's).
            WlPersCoilsAbilities.PersonalityNotTono => type == CardType.Personality && !WlNamed(target, "Yasuki Tono"),
            // Shiba Iaimiko: "your target unbowed Shugenja or Ikoma Aimi".
            WlPersCoilsAbilities.OwnShugenjaOrAimi => own && !target.Bowed && type == CardType.Personality && (Keyword(target, "Shugenja") || WlNamed(target, "Ikoma Aimi")),
            _ => false
        };
    }

    // Daigotsu Arakan - exp: "Arakan or Zansho" -- a Personality of that title (Arakan is Unique, and so is Zansho); at the battlefield when the action is a Battle action (the rule of location).
    private bool WlPersCoilsPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) =>
        ability.Performer == WlPersCoilsAbilities.ArakanOrZansho && InPlay(performer) && (WlNamed(performer, "Daigotsu Arakan") || WlNamed(performer, "Zansho"))
        && (!actingAsBattle || AtBattle(performer));

    private string?[]? WlPersCoilsPerformerCandidates(AbilityDefinition ability, int playerId, bool actingAsBattle)
    {
        if (ability.Performer != WlPersCoilsAbilities.ArakanOrZansho) return null;
        return _state.Cards.Values.Where(c => c.OwnerId == playerId && WlPersCoilsPerformerAllows(ability, c, actingAsBattle)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();
    }

    // ---- conditions on taking an action ---------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlPersCoils(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // "If no other player has more Family Honor than you" (Haruki's Deer's shape: an unmet "If" before the effects is a condition on taking the action).
            case WlPersCoilsAbilities.IchigikuForce:
                Require(_state.Players.All(p => p.Id == player.Id || p.Honor <= player.Honor), "Another player has more Family Honor than you.");
                break;
            // "If there is a dead Personality in any discard pile".
            case WlPersCoilsAbilities.MingGwokRanged:
                Require(_state.Cards.Values.Any(c => Definition(c).Type == CardType.Personality && c.Zone == Zone.Dead), "There is no dead Personality in any discard pile.");
                break;
            // "your target discarded (not dead) Personality": the category it shares with Scavenging Party takes dead ones too.
            case WlPersCoilsAbilities.MinoriRefill:
                Require(command.TargetId is not null && Card(command.TargetId).Zone != Zone.Dead, "Choose a discarded Personality that is not dead.");
                break;
        }
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this feature's. false: the effects are done. true: the action paused (a choice, a look, a search, an entering-play window).
    private bool? RunWlPersCoils(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case WlPersCoilsAbilities.TakakiMadness:
                CreateToken(target!, WlPersCoilsAbilities.MadnessToken, id);
                return false;
            case WlPersCoilsAbilities.NishiguchiPenalty:
                ApplyModifier(target!, source!.Id, id, "Force", -TokenCount(target!, WlPersCoilsAbilities.MadnessToken));
                return false;
            case WlPersCoilsAbilities.IchigikuForce:
                ApplyModifier(source!, source!.Id, id, "Force", WlControlsNamed(player.Id, "Kitsuki Kinaro") ? 4 : 3);
                return false;
            case WlPersCoilsAbilities.TomikoBowDraw:
                WlBow(target!, player.Id);
                Draw(player);
                return false;
            case WlPersCoilsAbilities.MingGwokRanged:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case WlPersCoilsAbilities.DarkNagaRanged:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case WlPersCoilsAbilities.IaimikoForce:
                ApplyModifier(source!, source!.Id, id, "Force", 1);
                return false;
            case WlPersCoilsAbilities.IaimikoCavalry:
                GrantKeyword(source!, "Cavalry", source!.Id, GrantDuration.Turn);
                return false;
            case WlPersCoilsAbilities.IaimikoConqueror:
                GrantKeyword(source!, "Conqueror", source!.Id, GrantDuration.Turn);
                return false;
            case WlPersCoilsAbilities.KameyoiHonor:
                ApplyPlayerHonorChoice(source!, command.TargetPlayerId!.Value, -1);
                return false;
            case WlPersCoilsAbilities.KameyoiDishonor:
                Dishonor(target!, player.Id);
                return false;
            case WlPersCoilsAbilities.TonoStraighten:
                if (InPlay(target!)) Straighten(target!);
                return false;
            case WlPersCoilsAbilities.TseluSearch:
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBefore);
                return true;
            case WlPersCoilsAbilities.TonboLook:
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBefore);
                return true;
            case WlPersCoilsAbilities.DaisukeLook:
                _state.WlDaisukeShownMark = _state.ShownCards.Count;
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBefore);
                return true;
            case WlPersCoilsAbilities.ArakanBattle:
            case WlPersCoilsAbilities.ArakanLimited:
                {
                    // "Equip ... to Arakan or Zansho. Permanently give the Follower Shadowlands - Undead. Lose 2 Honor." The attach opens the Follower's entering-play window; the two sentences after it
                    // are done first, so the window does not wait for them.
                    var window = ExecuteAttach(command, ability.Attach!, player, source, target);
                    GrantKeyword(target!, "Shadowlands", source!.Id, GrantDuration.Permanent);
                    GrantKeyword(target!, "Undead", source.Id, GrantDuration.Permanent);
                    GainHonor(player.Id, -2, Definition(source).Name, source);
                    return window;
                }
            case WlPersCoilsAbilities.MinoriRefill:
                {
                    RefillProvinceWithDiscardedPersonality(player, target!, command.Province!.Value, Definition(source!).Name);
                    // "Discard a Fate card": the acting player chooses it from his hand; with one card there is nothing to choose, with none nothing happens.
                    return BeginDeciderChoice(WlPersCoilsSpecs.MinoriDiscard, command, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            case WlPersCoilsAbilities.ChukageWater:
                return BeginChukageChoice(command, player, target!, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            default:
                return null;
        }
    }

    // ---- Asako Chukage - exp2 -----------------------------------------------------------------------------------------------------------------------------------------------

    // "Straighten or bow a target card ... If you control a Ring, you may remove both it and the target from the game." One option per mode, and one per mode and Ring he controls.
    private bool BeginChukageChoice(GameCommand command, PlayerState player, CardInstance target, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore,
        List<AdditionalActionGrant> preExistingGrants)
    {
        var name = Definition(target).Name;
        var rings = _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Ring).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        var options = new List<EffectOption>();
        foreach (var (mode, verb, effect) in new[] { ("straighten", "Straighten", BranchEffect.StraightenTarget), ("bow", "Bow", BranchEffect.BowTarget) })
        {
            options.Add(new(mode, $"{verb} {name}", [effect]));
            foreach (var ring in rings)
                options.Add(new($"{mode}-remove-{ring.Id}", $"{verb} {name}, then remove {Definition(ring).Name} and {name} from the game", [effect, WlEffectPrefix + "remove:" + ring.Id]));
        }
        return BeginListedEffectChoice(command, options.ToArray(), $"{Definition(Card(command.CardId)).Name}: straighten or bow {name}{(rings.Length > 0 ? ", and you may remove one of your Rings and it from the game" : "")}.",
            consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
    }

    // ApplyBranchEffect's arm: the removal of a Ring and the target from the game; false for any other effect.
    private bool ApplyWlPersCoilsBranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith(WlEffectPrefix, StringComparison.Ordinal)) return false;
        var kind = effect[WlEffectPrefix.Length..];
        if (!kind.StartsWith("remove:", StringComparison.Ordinal) || command.TargetId is not { } targetId || !_state.Cards.TryGetValue(targetId, out var target)) return true;
        var ring = Card(kind["remove:".Length..]);
        // The Ring must still be one the acting player controls in play, and the target in play; an effect negated on the target ("all") removes neither.
        if (!InPlay(ring) || Definition(ring).Type != CardType.Ring || ring.OwnerId != command.PlayerId || !InPlay(target) || NegatesEffect(target, "all")) return true;
        var sourceName = command.CardId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var source) ? Definition(source).Name : "An action";
        RemoveFromGame(ring);
        if (Definition(target).Type == CardType.Personality) RemoveUnitFromGame(target, sourceName);
        else
        {
            var host = target.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var found) ? found : null;
            RemoveFromGame(target);
            // A removed Item may take Chi with it; a Personality left with none is destroyed (as when an attachment is destroyed).
            if (host is not null && Definition(host).Type == CardType.Personality && InPlay(host) && PersonalityChi(host) <= 0) DestroyUnits([host], true);
        }
        return true;
    }

    // ---- Yoritomo Minori - exp: the acting player's own discard -------------------------------------------------------------------------------------------------------

    private string? WlPersCoilsDeciderPrompt(PendingDeciderChoice p) =>
        p.SpecId == WlPersCoilsSpecs.MinoriDiscard ? $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose a card from your hand. It is discarded." : null;

    private (List<string> Candidates, int Min, int Max)? WlPersCoilsDeciderOptions(string specId, int deciderId) =>
        specId == WlPersCoilsSpecs.MinoriDiscard ? (Player(deciderId).Hand.OrderBy(id => id, StringComparer.Ordinal).ToList(), 1, 1) : null;

    private bool WlPersCoilsApplyDecider(PendingDeciderChoice p)
    {
        if (p.SpecId != WlPersCoilsSpecs.MinoriDiscard) return false;
        foreach (var id in p.Chosen) { var card = Card(id); if (card.Zone == Zone.Hand) DiscardFate(Player(card.OwnerId), card); }
        return true;
    }

    // ---- Kitsuki Daisuke - exp2: "then discard a card" after a card was shown ----------------------------------------------------------------------------------------

    // FinishLook's hook. True when the discard decision opened (the action then finishes from the search flow's end).
    private bool WlPersCoilsAfterLook(PendingLook look)
    {
        if (look.LookId != WlPersCoilsLooks.Daisuke.Id) return false;
        var shownMark = _state.WlDaisukeShownMark; _state.WlDaisukeShownMark = -1;
        if (shownMark < 0 || _state.ShownCards.Count <= shownMark) return false; // no card was shown and taken: nothing to discard
        var player = Player(look.PlayerId);
        var found = player.Hand.Select(Card).LastOrDefault(c => c.DefinitionId == _state.ShownCards[^1].DefinitionId);
        if (found is null) return false;
        var name = Definition(Card(look.SourceCardId)).Name;
        // The card found cannot be the one discarded (Mendon's ruling on the Terrain search, issue #368); with no other card in hand nothing is discarded.
        if (player.Hand.All(id => id == found.Id)) { Emit("searchDiscardSkipped", $"{player.Name} has no other card in hand to discard.", player.Id); return false; }
        _state.PendingLook = null;
        _state.PendingSearch = new PendingSearch
        {
            SearchId = WlPersCoilsSearches.DaisukeDiscard.Id,
            SourceCardId = look.SourceCardId,
            PlayerId = look.PlayerId,
            Stage = SearchStage.ChooseDiscard,
            Deck = SearchDeck.Hand,
            SelectedCardId = found.Id,
            Origin = look.Origin,
            BowedBefore = look.BowedBefore,
            Command = _runningCommand
        };
        _state.PendingChoice = new("searchDiscard", look.PlayerId, look.SourceCardId, $"{name}: discard a card from your hand other than the card you found.");
        _state.PriorityPlayerId = look.PlayerId; _state.ConsecutivePasses = 0;
        return true;
    }

    // ---- attacks -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int? WlPersCoilsAttackStrength(string abilityId) => abilityId switch
    {
        WlPersCoilsAbilities.MingGwokRanged => 3,
        WlPersCoilsAbilities.DarkNagaRanged => 9,
        _ => null
    };

    private static string? WlPersCoilsAttackKind(string abilityId) => abilityId is WlPersCoilsAbilities.MingGwokRanged or WlPersCoilsAbilities.DarkNagaRanged ? "Ranged" : null;

    // ---- traits --------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Hiruma Nikaru - exp2: "Nikaru uses his printed Force as his duel stat." DuelStatValue's term, in every duel he is in.
    private int? WlPersCoilsDuelStat(CardInstance card) => Definition(card).Effect == WlPersCoilsAbilities.NikaruEffect ? Definition(card).Force : null;

    // Kakita Hideo - exp2: "After Hideo wins a duel, give him two +1F tokens." Called where the duel's winner is known; a tie has no winner.
    private void ApplyWlPersCoilsDuelWin(string? winnerId)
    {
        if (winnerId is null || !_state.Cards.TryGetValue(winnerId, out var winner) || !InPlay(winner) || Definition(winner).Effect != WlPersCoilsAbilities.HideoEffect) return;
        CreateToken(winner, "Force", WlPersCoilsAbilities.HideoEffect, "Force", 1);
        CreateToken(winner, "Force", WlPersCoilsAbilities.HideoEffect, "Force", 1);
        Emit("tokensGiven", $"{Definition(winner).Name} won a duel: he has two +1F tokens.", winner.OwnerId, winner.Id);
    }

    // Daidoji Adachi: "After an action from a card opposing Adachi resolves, if its unit's Personality has more Madness tokens than printed Personal Honor, his controller discards a card." A trait
    // (no "Reaction:"), so it is mandatory and runs before the Reactions to the same moment (FinishActionEffectTracking's trait position). The card is one of a unit at the battlefield Adachi
    // stands at (a Personality, or a Follower, Item or Spell attached to one), under the other player's control; "his controller" is the Personality's, who owes the discard. Queued as every owed
    // discard is (PendingForcedDiscards); WlPersCoilsDrainDiscards asks it once no Reaction window has opened over the action.
    private void ApplyWlPersCoilsAfterAction(string? sourceId)
    {
        if (sourceId is null || !_state.Cards.TryGetValue(sourceId, out var source) || !InPlay(source)) return;
        var leader = Card(UnitHostId(source));
        if (Definition(leader).Type != CardType.Personality || leader.Zone != Zone.Battlefield || leader.ProvinceIndex is not int battlefield) return;
        if (TokenCount(leader, WlPersCoilsAbilities.MadnessToken) <= Definition(leader).PersonalHonor) return;
        foreach (var adachi in _state.Cards.Values.Where(c => c.OwnerId != leader.OwnerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && Definition(c).Effect == WlPersCoilsAbilities.AdachiEffect)
                     .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            if (P2HandDiscardBlocked(leader.OwnerId, adachi.OwnerId, false, Definition(adachi).Name)) continue; // Pillowbook
            _state.PendingForcedDiscards.Add(new(leader.OwnerId, Definition(adachi).Name));
        }
    }

    private bool WlPersCoilsDrainDiscards(int closesTo) =>
        _state.PendingForcedDiscards.Any(d => d.Reason == "Daidoji Adachi") && _state.PendingChoice is null && OpenOverconfidentDrawWindow(closesTo);

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlPersCoilsHooks()
    {
        EngineHooks.Abilities.Register("AddWlPersCoils", 14300, static abilities => abilities.AddWlPersCoils());
        EngineHooks.Looks.Register("WlPersCoilsLooks", 14300, static () => WlPersCoilsLooks.All);
        EngineHooks.Searches.Register("WlPersCoilsSearches", 14300, static () => WlPersCoilsSearches.All);
        EngineHooks.RunEffects.Register("RunWlPersCoils", 14300, static (engine, context) => engine.RunWlPersCoils(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant,
            context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.ValidateMiddle.Register("ValidateWlPersCoils", 14300, static (engine, context) => engine.ValidateWlPersCoils(context.id, context.command, context.player, context.source));
        EngineHooks.TargetAllows.Register("WlPersCoilsTargetAllows", 14300, static (engine, context) => engine.WlPersCoilsTargetAllows(context.ability.Target, context.player, context.target));
        EngineHooks.PerformerAllows.Register("WlPersCoilsPerformerAllows", 14300, static (engine, context) => engine.WlPersCoilsPerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.PerformerCandidates.Register("WlPersCoilsPerformerCandidates", 14300, static (engine, context) => engine.WlPersCoilsPerformerCandidates(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.AttackStrengthBefore.Register("WlPersCoilsAttackStrength", 14300, static (engine, context) => WlPersCoilsAttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("WlPersCoilsAttackKind", 14300, static context => WlPersCoilsAttackKind(context.abilityId));
        EngineHooks.DeciderPrompt.Register("WlPersCoilsDeciderPrompt", 14300, static (engine, p) => engine.WlPersCoilsDeciderPrompt(p));
        EngineHooks.DeciderOptions.Register("WlPersCoilsDeciderOptions", 14300, static (engine, specId, deciderId, command) => engine.WlPersCoilsDeciderOptions(specId, deciderId));
        EngineHooks.DeciderVerb.Register("WlPersCoilsDeciderVerb", 14300, static (engine, specId) => specId == WlPersCoilsSpecs.MinoriDiscard ? "Discard" : null);
        EngineHooks.DeciderIdBefore.Register("WlPersCoilsDeciderIdBefore", 14300, static (engine, specId, command) => specId == WlPersCoilsSpecs.MinoriDiscard ? command.PlayerId : null);
        EngineHooks.ApplyDecider.Register("WlPersCoilsApplyDecider", 14300, static (engine, p) => engine.WlPersCoilsApplyDecider(p));
    }
}
