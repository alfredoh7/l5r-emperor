namespace L5R.Core;

// Seeds of Decay pass (issue #453), batch 4: the execution helpers of the cards wired there (docs/decisions/2026-09-21-second-city-pass.md is the
// template of the pass). Each helper is the body of one execution case or reveal branch; the primitives they compose live in their own files
// (StaticKeywordRules.cs, AttachmentTransfers.cs).
public sealed partial class MatchEngine
{
    // ---- Daigotsu Yamato ---------------------------------------------------------------------------------------------------------------------------

    // "After Yamato enters play: Lose 1 Honor and if any other player is Scorpion Clan, permanently remove Brash from Yamato." The Honor is his EntryHonorLoss; this is
    // the removal, a trait applied with the other mandatory entry effects (ApplyMandatoryEntryEffects). Permanent: RemovedKeywords survives overlay and death.
    private const string YamatoEntryEffect = "yamato-entry-lose-brash-vs-scorpion";
    private void RemoveBrashIfOpponentIsScorpion(CardInstance card)
    {
        if (Player(1 - card.OwnerId).Clan != "Scorpion" || card.RemovedKeywords.Contains("Brash", StringComparer.Ordinal)) return;
        card.RemovedKeywords.Add("Brash");
        Emit("keywordRemoved", $"{Definition(card).Name} permanently loses Brash: another player is Scorpion Clan.", card.OwnerId, card.Id);
    }

    // ---- Mukudori ---------------------------------------------------------------------------------------------------------------------------------

    // "Limited: Bow three of your Fudo cards to draw a card." The three chosen cards (a 3..3 slot) are bowed as the cost, then the card is drawn. The bows are paid here,
    // not as declared Costs (an ActionCost names one subject; a slot's cards are several), so a negated action skips them like every cost paid in its case.
    private void BowFudoCardsAndDraw(GameCommand command, PlayerState player)
    {
        foreach (var fudo in SlotCards(command, "fudo").ToArray()) fudo.Bowed = true;
        Draw(player);
    }

    // ---- Naka Mahatsu - exp ----------------------------------------------------------------------------------------------------------------------

    private static readonly string[] ThunderOrElementKeywords = ["Thunder", "Air", "Earth", "Fire", "Water", "Void"];

    // "Reaction: If it is the Action Phase or Mahatsu is at the current battlefield, before an action with Thunder or an element keyword resolve, negate its effects." The
    // action is any player's (the text names no player), carrying Thunder or an element keyword on the ability itself or on the card it is printed on (ActionCarriesKeyword),
    // whatever it targets -- an action that names no card included, like the untargeted-action fixture of issue #373.
    // His own Reaction is itself an action carrying his element keywords: it is never its own trigger (a window for it would only ever negate itself).
    private bool MahatsuNegateApplies(CardInstance mahatsu, GameCommand command)
    {
        if (command.AbilityId is null or "mahatsu-negate" || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting) || !InPlay(mahatsu)) return false;
        if (!(_state.Phase == Phase.Action || AtBattle(mahatsu))) return false;
        var actingSource = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        return ThunderOrElementKeywords.Any(k => ActionCarriesKeyword(acting, actingSource, k));
    }

    // ---- Incense Mill -----------------------------------------------------------------------------------------------------------------------------

    // "Negate all current Force penalties on him. Negate any cards' effects that say he may not perform actions (this turn)." Only the penalties in force now: every
    // negative Force modifier and every token whose Force is negative is removed (Tireless Efforts' NegateForcePenalties also bars NEW penalties for the turn; this does not).
    // Every action lock on him is lifted, whatever its duration.
    private void CleanseForcePenaltiesAndLocks(CardInstance card)
    {
        var modifiers = card.Modifiers.RemoveAll(m => m.Stat == "Force" && m.Amount < 0);
        // Issue #503: a two-stat token (-1F/-1C Poison) loses its Force penalty and keeps its other stat.
        var twoStat = card.Tokens.Where(t => t.Stat == "Force" && t.Amount < 0 && t.Stat2 is not null).ToArray();
        foreach (var token in twoStat) card.Tokens[card.Tokens.IndexOf(token)] = token with { Stat = token.Stat2, Amount = token.Amount2, Stat2 = null, Amount2 = 0 };
        var tokens = card.Tokens.RemoveAll(t => t.Stat == "Force" && t.Amount < 0) + twoStat.Length;
        var locks = card.ActionLocks.Count; card.ActionLocks.Clear();
        Emit("penaltiesAndLocksNegated", $"{Definition(card).Name}: {modifiers + tokens} Force {(modifiers + tokens == 1 ? "penalty" : "penalties")} and {locks} action {(locks == 1 ? "lock" : "locks")} negated.", card.OwnerId, card.Id);
    }

    // ---- Cavalry Detachment -----------------------------------------------------------------------------------------------------------------------

    // "Reaction: After another player's action targets this Personality, remove this card from the game to create a 3F/1C/2PH Samurai * Cavalry Personality with your Clan
    // Alignment at the target's location. Transfer the target's attachments to the created Personality." The removal is the declared cost (paid before this runs); the target
    // is the held action's, the Personality this Follower was attached to. The created Personality (Detached Personality, oracle-l5r-10201, with the creator's Clan) enters at
    // the target's location -- home, or the battlefield he stands at -- and, before any entering-play window opens over it, every attachment of the target that can be
    // transferred to it is (attaching restrictions still apply: one that cannot stays). The held action then goes on against the target where he stands.
    private bool SpawnDetachedPersonality(AbilityDefinition ability, GameCommand command, PlayerState player)
    {
        var target = Card(HeldAction().TargetId);
        var spec = ability.Creates!;
        var scan = CreatedCards.Resolve(spec);
        var printed = DerivePrinted(scan, spec, command, player);
        CardInstance? created = null;
        // The out parameter is written before beforeWindow runs, so the transfers see the created card.
        return CreatePersonality(player.Id, scan, false, target.Zone == Zone.Battlefield ? target.ProvinceIndex : null, out created, printed, beforeWindow: () =>
        {
            foreach (var attachment in Attachments(target.Id).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray())
                if (CanTransferTo(attachment, created!)) TransferAttachment(attachment, created!, "Cavalry Detachment");
        });
    }

    // ---- Temple to Fudo ---------------------------------------------------------------------------------------------------------------------------

    // ":bow:: Produce 2 Gold and lose 1 Honor." The Honor is lost when the Holding is bowed for its Gold (Pay), by the player paying.
    private const string TempleToFudoEffect = "temple-to-fudo";
    private void LoseHonorForProducers(int playerId, string[] sourceIds)
    {
        foreach (var id in sourceIds)
            if (Definition(_state.Cards[id]).Effect == TempleToFudoEffect) GainHonor(playerId, -1, Definition(_state.Cards[id]).Name, _state.Cards[id]);
    }

    // ---- Offering Reverence -----------------------------------------------------------------------------------------------------------------------

    // "After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one of them, for each dead Personality in your discard pile, create a
    // 2F/2C/3PH Samurai * Ancestor * Spirit Personality with your Clan Alignment. If this created no Personalities, you may shuffle this Event into your deck instead of
    // discarding it normally." The Ancestors use the user-supplied Ancestor Spirit scan; its printed trait gives them their creator's Stronghold Clan at creation. They enter at home
    // with their mandatory entry effects and no entering-play window (the reveal is not an action to react to). With none created the owner chooses: shuffle the Event into
    // the Dynasty deck, or discard it. Either way the province is refilled and the reveal goes on.
    private const string OfferingReverenceEffect = "offering-reverence";
    private static readonly CreatedCardSpec ReverenceAncestor = new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3,
        Keywords: ["Samurai", "Ancestor", "Spirit"], ProxyId: "user-scan-ancestor-spirit");

    private bool ResolveOfferingReverence(PlayerState player, ProvinceState province, CardInstance card)
    {
        var dead = _state.Cards.Values.Count(c => c.OwnerId == player.Id && c.Zone == Zone.Dead && Definition(c).Type == CardType.Personality);
        if (dead == 0)
        {
            _state.PendingChoice = new("offeringReverence", player.Id, card.Id, "Offering Reverence created no Personalities: shuffle it into your deck, or discard it.");
            Emit("eventChoice", $"{Definition(card).Name}: no dead Personality in {player.Name}'s discard pile; the Event may be shuffled into the deck.", player.Id, card.Id);
            return true;
        }
        var scan = CreatedCards.Resolve(ReverenceAncestor);
        var printed = WithControllerClan(scan, player);
        for (var i = 0; i < dead; i++) CreatePersonality(player.Id, scan, true, null, out _, printed);
        Emit("eventResolved", $"{Definition(card).Name}: {player.Name} created {dead} Ancestor {(dead == 1 ? "Personality" : "Personalities")}.", player.Id, card.Id);
        province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
        Refill(player, province);
        return false;
    }

    private void ResolveOfferingReverenceChoice(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "offeringReverence" && command.PlayerId == pending.PlayerId && command.Choice is "shuffle" or "discard", "Choose whether to shuffle Offering Reverence into your deck.");
        var card = Card(pending.CardId!);
        var player = Player(pending.PlayerId);
        var province = player.Provinces[card.ProvinceIndex!.Value];
        _state.PendingChoice = null;
        province.CardId = null; card.ProvinceIndex = null;
        if (command.Choice == "shuffle")
        {
            card.Zone = Zone.DynastyDeck; card.FaceUp = false;
            player.DynastyDeck.Add(card.Id);
            ShuffleDeck(player, SearchDeck.Dynasty);
            Emit("eventResolved", $"{Definition(card).Name} was shuffled into {player.Name}'s Dynasty deck.", player.Id, card.Id);
        }
        else
        {
            card.Zone = Zone.DynastyDiscard;
            Emit("eventResolved", $"{Definition(card).Name} resolved and was discarded.", player.Id, card.Id);
        }
        Refill(player, province);
        ContinueEventsReveal(player);
    }

    // ---- Tested Land ------------------------------------------------------------------------------------------------------------------------------

    // "After this Region enters play, attach it to another player's Province (that can attach a Region; that player now controls it.) After a Holding is revealed in this
    // Province, discard it and this Region." Revealed in the owner's province, it attaches to a province of the OTHER player's that has no Region: the one there is, or the
    // one its owner chooses when several qualify (a decision in the reveal, Harsh Choices' shape); with none it is discarded like any Region that cannot attach. Attached, it is
    // controlled by the province's owner (OwnerId, with OriginalOwnerId remembering whose card it is: it returns to its owner's discard pile when it leaves play). Returns true
    // when the reveal paused for the choice.
    private const string TestedLandEffect = "tested-land";

    private ProvinceState[] TestedLandCandidates(int ownerId) =>
        Player(1 - ownerId).Provinces.Where(p => !p.Destroyed && p.RegionId is null).ToArray();

    private bool ResolveTestedLand(PlayerState player, ProvinceState province, CardInstance card)
    {
        var candidates = TestedLandCandidates(player.Id);
        if (candidates.Length > 1)
        {
            _state.PendingChoice = new("testedLandProvince", player.Id, card.Id, "Tested Land: choose which of your opponent's provinces it attaches to.");
            Emit("regionChoice", $"{Definition(card).Name}: {player.Name} chooses an enemy province to attach it to.", player.Id, card.Id);
            return true;
        }
        province.CardId = null; card.ProvinceIndex = null;
        if (candidates.Length == 0)
        {
            card.Zone = Zone.DynastyDiscard;
            Emit("regionDiscarded", $"{Definition(card).Name} was discarded: no enemy province can attach a Region.", player.Id, card.Id);
        }
        else AttachTestedLand(player, candidates[0], card);
        Refill(player, province);
        return false;
    }

    private void AttachTestedLand(PlayerState owner, ProvinceState enemyProvince, CardInstance card)
    {
        card.OriginalOwnerId ??= card.OwnerId;
        card.OwnerId = 1 - owner.Id;
        AttachRegionToProvince(Player(1 - owner.Id), enemyProvince, card);
        Emit("controlTaken", $"{Player(1 - owner.Id).Name} now controls {Definition(card).Name}.", 1 - owner.Id, card.Id);
    }

    private void ResolveTestedLandProvince(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        var card = Card(pending.CardId!);
        var player = Player(pending.PlayerId);
        var candidates = TestedLandCandidates(player.Id);
        Require(command.Type == "testedLandProvince" && command.PlayerId == pending.PlayerId && command.Province is int index && candidates.Any(p => p.Index == index),
            "Choose an enemy province with no Region attached.");
        _state.PendingChoice = null;
        var province = player.Provinces[card.ProvinceIndex!.Value];
        province.CardId = null; card.ProvinceIndex = null;
        AttachTestedLand(player, candidates.Single(p => p.Index == command.Province), card);
        Refill(player, province);
        ContinueEventsReveal(player);
    }

    // The trait's second sentence, at the Events Phase reveal: a Holding turned face up in a province Tested Land is attached to is discarded, and so is Tested Land.
    private void DiscardRevealedHoldingOnTestedLand(PlayerState player, ProvinceState province, CardInstance holding)
    {
        if (province.RegionId is not string regionId || !_state.Cards.TryGetValue(regionId, out var region) || Definition(region).Effect != TestedLandEffect) return;
        province.CardId = null; holding.ProvinceIndex = null; holding.Zone = Zone.DynastyDiscard;
        Emit("provinceDiscard", $"{Definition(holding).Name} was discarded from {player.Name}'s province {province.Index + 1} (Tested Land).", player.Id, holding.Id);
        DestroyRegion(region);
        Refill(player, province);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredOfferingReverence(string type, GameCommand command)
    {
        if (type != "offeringReverence") return false;
        ResolveOfferingReverenceChoice(command);
        return true;
    }
    private bool HandleRegisteredTestedLandProvince(string type, GameCommand command)
    {
        if (type != "testedLandProvince") return false;
        ResolveTestedLandProvince(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSeedsOfDecayBatch4Hooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredOfferingReverence", 3600, static (engine, type, command) => engine.HandleRegisteredOfferingReverence(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredTestedLandProvince", 3900, static (engine, type, command) => engine.HandleRegisteredTestedLandProvince(type, command));
    }
}
