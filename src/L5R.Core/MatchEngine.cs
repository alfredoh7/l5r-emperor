using System.Text.Json;

namespace L5R.Core;

/// <summary>Single-threaded authoritative simulator. Execute validates on a detached state and commits atomically.</summary>
public sealed partial class MatchEngine
{
    public const string ContentVersion = "emperor-lion-crane-0.2.0";
    private MatchState _state;
    private readonly ulong _seed;
    private readonly string _scenario;
    private readonly List<GameCommand> _commands = [];
    private static readonly string[] Limits = [
        "Training fixtures only. These are synthetic cards and decks, not verified Emperor tournament content.",
        "Implemented: payment-specific Gold, Clan purchase options, Proclaim, infantry battles, basic attachments and two fixture Battle actions.",
        "Not implemented: published card abilities, Border Keep filtering, Imperial Favor, cavalry, duels, dishonored cards, Rings, events, regions, spells and general nested reactions."
    ];

    private MatchEngine(MatchState state, ulong seed, string scenario) { _state = state; _seed = seed; _scenario = scenario; }
    /// <summary>Detached diagnostic snapshot, containing hidden information. Never send to a client.</summary>
    public MatchState State => Copy(_state);
    public long Version => _state.Version;
    public static MatchEngine CreateStarterMatch(ulong seed = 20260906) => Create(seed, "starter");
    public static MatchEngine CreateEconomyScenario(ulong seed = 20260906) => Create(seed, "economy");
    public static MatchEngine CreatePublishedMatch(ulong seed = 20260906) => Create(seed, PublishedContent.Scenario);
    // Frozen deck lists (PublishedContent.TestSnapshot) for the C# test suite only -- never used by the
    // client or RoomManager. Editing the live deck (CreatePublishedMatch) for manual playtesting must
    // never silently change what this exercises.
    public static MatchEngine CreatePublishedTestMatch(ulong seed = 20260906) => Create(seed, PublishedContent.TestScenario);
    internal static MatchEngine FromTestState(MatchState state) => new(Copy(state), 1, "test-only");

    public ReplayRecord ExportReplay() => new(ContentVersion, _scenario, _seed, _commands.Select(Copy).ToArray());
    public static MatchEngine Replay(ReplayRecord replay)
    {
        if (replay.ContentVersion != ContentVersion) throw new ArgumentException("Replay content version is not supported.");
        var engine = Create(replay.Seed, replay.Scenario);
        foreach (var command in replay.Commands)
        {
            var result = engine.Execute(command);
            if (!result.Accepted) throw new ArgumentException($"Invalid replay command: {result.Error}");
        }
        return engine;
    }

    private static MatchEngine Create(ulong seed, string scenario)
    {
        if (scenario == PublishedContent.Scenario) return CreatePublished(seed, PublishedContent.Scenario, PublishedContent.Snapshot);
        if (scenario == PublishedContent.TestScenario) return CreatePublished(seed, PublishedContent.TestScenario, PublishedContent.TestSnapshot);
        if (scenario is not ("starter" or "economy")) throw new ArgumentException("Unknown scenario.");
        var state = new MatchState { Players = [new() { Id=0, Name="Crane", Clan="Crane", Honor=8 }, new() { Id=1, Name="Lion", Clan="Lion", Honor=6 }] };
        var random = new StableRandom(seed);
        var nextId = 0;
        CardInstance Add(int owner, string definition, Zone zone, bool faceUp = false, bool bowed = false)
        {
            _ = CardCatalog.Get(definition);
            var card = new CardInstance { Id=$"card-{++nextId:D4}", DefinitionId=definition, OwnerId=owner, Zone=zone, FaceUp=faceUp, Bowed=bowed };
            state.Cards.Add(card.Id,card);
            return card;
        }
        foreach (var player in state.Players)
        {
            var clan = player.Clan.ToLowerInvariant();
            player.StrongholdId = Add(player.Id,$"training-{clan}-stronghold",Zone.Stronghold,true).Id;
            Add(player.Id,"training-border-keep",Zone.Home,true);
            if (player.Id == 1) Add(player.Id,"training-bamboo",Zone.Home,true,true);
            for (var n = 0; n < 40; n++)
            {
                var definition = n < 12 ? (n % 2 == 0 ? "training-farm" : "training-marketplace")
                    : $"training-{clan}-{new[] { "samurai", "courtier", "veteran", "dignitary" }[n%4]}";
                player.DynastyDeck.Add(Add(player.Id,definition,Zone.DynastyDeck).Id);
                player.FateDeck.Add(Add(player.Id,new[] {"training-follower","training-item","training-rally","training-pressure"}[n%4],Zone.FateDeck).Id);
            }
            random.Shuffle(player.DynastyDeck);
            random.Shuffle(player.FateDeck);
            player.Provinces = Enumerable.Range(0,4).Select(i => new ProvinceState { Index=i,Strength=6 }).ToList();
        }
        var engine = new MatchEngine(state,seed,scenario);
        foreach (var player in state.Players)
        {
            foreach (var province in player.Provinces) engine.Refill(player,province);
            for (var n=0;n<6;n++) engine.Draw(player);
        }
        if (scenario == "economy")
        {
            var player=state.Players[0];
            string[] definitions=["training-crane-courtier","training-crane-veteran","training-marketplace","training-crane-dignitary"];
            for(var i=0;i<4;i++)
            {
                var old=state.Cards[player.Provinces[i].CardId!];
                old.DefinitionId=definitions[i]; // Explicit scenario fixture, not a shuffled production deck.
            }
            Add(0,"training-marketplace",Zone.Home,true);
        }
        engine.StartTurn();
        if(scenario=="economy") { state.Phase=Phase.Dynasty; engine.Emit("scenario","Economy exercise: choose one payment at a time; Gold never carries over."); }
        return engine;
    }

    public CommandResult Execute(GameCommand command)
    {
        if (command.PlayerId is <0 or >1) return Reject("Unknown player.");
        if (command.ExpectedVersion != _state.Version) return Reject("The board has changed. Refresh before acting.");
        if (_state.WinnerId is not null) return Reject("The match has finished.");
        var original = _state;
        _state = Copy(original);
        var firstEvent = _state.Events.Count;
        try
        {
            Apply(command);
            _state.Version++;
            _commands.Add(Copy(command));
            return new(true,null,_state.Version,_state.Events.Skip(firstEvent).ToArray());
        }
        catch (RuleViolation error)
        {
            _state=original;
            return Reject(error.Message);
        }
        catch
        {
            _state=original;
            throw;
        }
    }

    private CommandResult Reject(string error) => new(false,error,_state.Version,[]);
    private void Apply(GameCommand command)
    {
        if (command.Type=="concede") { Win(1-command.PlayerId,"Concession"); return; }
        // Open action: cuts through the normal priority gate and any pending choice, same as concede.
        if (command.Type=="orderSeppuku") { OrderSeppuku(command); return; }
        Require(command.PlayerId==_state.PriorityPlayerId,"Wait for your action opportunity.");
        if (_state.PendingChoice is not null)
        {
            if (_state.PendingChoice.Type=="shinden") { ResolveShinden(command); return; }
            if (_state.PendingChoice.Type=="declineStraighten") { ResolveDeclineStraighten(command); return; }
            if (_state.PendingChoice.Type=="conqueror") { ResolveConqueror(command); return; }
            if (_state.PendingChoice.Type=="proclaim")
            {
                // "useAbility" here resolves the entering card's own entering-play Reaction (if it
                // has one) -- ValidateAbility's "Reaction" case gates it to this exact window/card.
                // The window stays open afterward (UseAbility's own NextAction flips priority without
                // clearing PendingChoice), so Proclaim can still follow, in either order, until passed.
                Require(command.Type is "proclaim" or "useAbility" or "declineReaction" or "pass","Resolve the Proclaim reaction window first.");
                if (command.Type=="proclaim") Proclaim(command);
                else if (command.Type=="useAbility") UseAbility(command);
                else PassReaction();
                return;
            }
            if (_state.PendingChoice.Type=="entryReaction")
            {
                // Same window as "proclaim" above, but for a card that has its own entering-play
                // Reaction and was not itself Proclaim-eligible (e.g. bought with Clan Discount).
                Require(command.Type is "useAbility" or "declineReaction" or "pass","Resolve the entry reaction window first.");
                if (command.Type=="useAbility") UseAbility(command); else PassReaction();
                return;
            }
            if (_state.PendingChoice.Type=="attackReaction")
            {
                // Open to either side (unlike the entry-reaction windows above) -- the top-level
                // priority check already enforces whose turn it is to act or pass.
                Require(command.Type is "useAbility" or "declineReaction" or "pass","Resolve the pending attack first.");
                if (command.Type=="useAbility") UseAbility(command); else PassReaction();
                if (_state.PendingChoice is null && _state.PendingAttack is not null) ResolveAttack();
                return;
            }
            if (_state.PendingChoice.Type=="additionalActionReaction")
            {
                // Additional Action (issue #181): open to either side, same shape as attackReaction
                // above -- the opponent may play Effortless Counterattack's Reaction (negating the
                // pending effects) or pass; the granting player may also pass here to decline this
                // specific opportunity outright (forfeiting it -- see ConsumeAdditionalActionGrant,
                // already removed the grant regardless of what happens next).
                Require(command.Type is "useAbility" or "declineReaction" or "pass","Resolve the pending additional action first.");
                if (command.Type=="useAbility") UseAbility(command); else PassReaction();
                if (_state.PendingChoice is null && _state.PendingAdditionalAction is not null)
                {
                    var pending=_state.PendingAdditionalAction; _state.PendingAdditionalAction=null;
                    ExecuteAbilityEffects(pending,consumedGrant:true);
                }
                return;
            }
            Require(command.Type=="discardHand","Choose cards to discard to the hand limit.");
            DiscardHand(command);
            return;
        }
        switch(command.Type)
        {
            case "pass": Pass(); break;
            case "purchase": Purchase(command); break;
            case "discardProvince": DiscardProvince(command); break;
            case "endDynasty": Require(_state.Phase is Phase.Dynasty or Phase.DynastyDiscard,"Not in the Dynasty Phase."); EndTurn(); break;
            case "attack": DeclareAttack(command); break;
            case "assignDefense": AssignDefense(command); break;
            case "selectBattle": SelectBattle(command); break;
            case "equip": Equip(command); break;
            case "playStrategy": PlayStrategy(command); break;
            case "recruitReserve": RecruitReserve(command); break;
            case "useAbility": UseAbility(command); break;
            default: throw new RuleViolation("Unsupported command.");
        }
    }

    // "After this X enters play" (core rules): a card's own Reaction fires every time it is placed
    // into a player's home, either standalone or as an attachment -- from a province, a hand, or (once
    // a card returns cards from there) a discard pile. Only "removed from the game" is permanent.
    private static string? EntryReactionAbility(CardInstance card) =>
        (Definition(card).Abilities ?? []).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "own-enters-play");

    // Opens the entering card's own "after entering play" Reaction, if it has one; every entry point
    // (Purchase, Equip, a played Terrain) calls this. Repeatable each time the card enters play, not
    // just once ever: clears any earlier use so a card that left and is entering again gets a fresh
    // window, per the printed trigger firing anew on each entry. closesTo controls who regains
    // priority once the window fully closes (both sides passed) -- see PassReaction.
    private bool OpenEntryReactionWindow(int playerId, CardInstance card, int? closesTo = null)
    {
        // Destined keyword (core rules): a mandatory Fate-deck draw every time a card carrying it
        // enters play -- unconditional, not an opt-in Reaction, so it fires here unconditionally
        // rather than through the Reaction window below. Every entry point (Purchase, Equip, a played
        // Terrain, equip-battle's Spell attach) already funnels through this one shared function.
        if(Keyword(card,"Destined")) Draw(Player(playerId));
        var reactionId = EntryReactionAbility(card);
        // Singh Remnants' shape: a DIFFERENT card (in hand or a discard pile) watching for this entry,
        // not the entering card's own reaction -- only relevant when a Personality is what entered.
        var watchingReactionAvailable = Definition(card).Type==CardType.Personality && AnyPersonalityEntryReactionAvailable(playerId);
        if (reactionId is null && !watchingReactionAvailable) return false;
        if (reactionId is not null) card.AbilityUses.Remove(reactionId);
        _state.PendingChoice = new("entryReaction", playerId, card.Id, "After entry: use this card's own reaction, or pass.", closesTo);
        _state.ConsecutivePasses = 0;
        Emit("reactionWindow", "Entry reaction window opened.", playerId, card.Id);
        return true;
    }

    private void Purchase(GameCommand command)
    {
        Require(_state.Phase==Phase.Dynasty,"Purchases are only allowed before province discarding in the Dynasty Phase.");
        var card=Card(command.CardId);
        var quote=QuotePurchase(command.PlayerId,card.Id,command.ClanDiscount,command.ClanAllegiance);
        Require(quote.Error is null,quote.Error??"");
        Pay(command.PlayerId,card,quote.Cost,command.SourceIds);
        var province=Player(command.PlayerId).Provinces.Single(p=>p.CardId==card.Id);
        province.CardId=null;
        card.Zone=Zone.Home; card.ProvinceIndex=null; card.FaceUp=true;
        // A Personality is straightened by default; entering bowed is only ever a direct
        // consequence of a printed effect, never a separate straighten-then-bow step.
        card.Bowed=Definition(card).Type==CardType.Holding || (Definition(card).Type==CardType.Personality && Definition(card).Effect=="enters-bowed");
        if(Definition(card).Type==CardType.Personality && Definition(card).Effect=="enters-dishonored") Dishonor(card);
        Emit("cardEnteredPlay",$"{Player(command.PlayerId).Name} brought {Definition(card).Name} into play{(card.Bowed ? " bowed" : "") }.",command.PlayerId,card.Id);
        Refill(Player(command.PlayerId),province);
        var reactionId=EntryReactionAbility(card);
        if (quote.ProclaimEligible)
        {
            if(reactionId is not null) card.AbilityUses.Remove(reactionId);
            _state.PendingChoice=new("proclaim",command.PlayerId,card.Id,"After entry: Proclaim for Honor"+(reactionId is not null?", use this card's own reaction,":"")+" or pass. Each may be used once.");
            _state.ConsecutivePasses=0;
            Emit("reactionWindow","Proclaim reaction window opened.",command.PlayerId,card.Id);
        }
        else OpenEntryReactionWindow(command.PlayerId,card);
    }

    // Reserve (core rules, issue #184): a Dynasty Personality with this keyword may be recruited as an
    // "absent battle action" -- the recruiting player needs no presence of their own at this
    // battlefield, unlike every other Battle action. Two conditions instead: the card is already
    // face up in its province (a face-down Reserve card cannot be activated), there is a battlefield
    // at that province (any currently unresolved one, not necessarily the one being resolved right
    // now), and the ENEMY has presence there. Recruited directly to the battlefield, skipping the
    // assignment step entirely -- no assignment-triggered Reaction exists in the engine yet, so there
    // is nothing to actually skip today, but nothing here waits for one before this Personality is
    // fully in play either. Deferred: Proclaim's own narrow exception for a Reserve recruit made
    // possible by a defender's battlefield-creation ability at the attacker's own province -- that
    // mechanic does not exist yet, so QuotePurchase's own ProclaimEligible (which already requires
    // playerId==ActivePlayerId) is reused as-is and simply never overridden for this narrow case.
    private void RecruitReserve(GameCommand command)
    {
        Require(_state.Phase==Phase.Battle,"Reserve recruitment is an absent battle action, only available during the Battle Phase.");
        var card=Card(command.CardId);
        Require(card.OwnerId==command.PlayerId && card.Zone==Zone.Province && card.FaceUp && Definition(card).Type==CardType.Personality && Keyword(card,"Reserve"),
            "Choose your own face-up Reserve Personality in a province.");
        var provinceIndex=card.ProvinceIndex!.Value;
        Require(_state.UnresolvedBattlefields.Contains(provinceIndex),"There is no battlefield at this province.");
        Require(Army(1-command.PlayerId,provinceIndex).Any(),"The enemy must have presence at this battlefield.");
        var quote=QuotePurchase(command.PlayerId,card.Id,command.ClanDiscount,command.ClanAllegiance);
        Require(quote.Error is null,quote.Error??"");
        Pay(command.PlayerId,card,quote.Cost,command.SourceIds);
        var province=Player(command.PlayerId).Provinces.Single(p=>p.CardId==card.Id);
        province.CardId=null;
        card.Zone=Zone.Battlefield; card.FaceUp=true;
        card.Bowed=Definition(card).Effect=="enters-bowed";
        if(Definition(card).Effect=="enters-dishonored") Dishonor(card);
        Emit("reserveRecruited",$"{Player(command.PlayerId).Name} recruited {Definition(card).Name} from Reserve.",command.PlayerId,card.Id);
        Refill(Player(command.PlayerId),province);
        var reactionId=EntryReactionAbility(card);
        if (quote.ProclaimEligible)
        {
            if(reactionId is not null) card.AbilityUses.Remove(reactionId);
            _state.PendingChoice=new("proclaim",command.PlayerId,card.Id,"After entry: Proclaim for Honor"+(reactionId is not null?", use this card's own reaction,":"")+" or pass. Each may be used once.");
            _state.ConsecutivePasses=0;
            Emit("reactionWindow","Proclaim reaction window opened.",command.PlayerId,card.Id);
        }
        else if(OpenEntryReactionWindow(command.PlayerId,card,1-command.PlayerId)) return;
        // An ordinary action (never itself an Additional Action) forfeits any of this player's own
        // still-unused grants, same rule as ExecuteAbilityEffects' own tail and MatchEngine.Pass.
        _state.AdditionalActions.RemoveAll(g=>g.PlayerId==command.PlayerId);
        NextAction(command.PlayerId);
    }

    private void OrderSeppuku(GameCommand command)
    {
        Require(_state.Phase==Phase.Action,"Order Seppuku is only available during the Action Phase.");
        var card=Card(command.CardId);
        Require(card.OwnerId==command.PlayerId,"You may only order Seppuku for your own Personality.");
        Require(InPlay(card) && Definition(card).Type==CardType.Personality,"Choose your Personality in play.");
        Require(card.Dishonorable,"Only a Dishonorable Personality may perform Seppuku.");
        Require(Keyword(card,"Samurai") || Keyword(card,"Courtier") || Keyword(card,"Shugenja"),"Only a Samurai, Courtier, or Shugenja may perform Seppuku.");
        // Honorable before destruction: this is what spares the controller the dishonorable-death
        // Honor penalty applied generally in MarkDead.
        Rehonor(card);
        DestroyUnits([card]);
        Emit("seppuku",$"{Definition(card).Name} performed Seppuku, dying honorably.",command.PlayerId,card.Id);
    }

    public PurchaseQuote QuotePurchase(int playerId,string cardId,bool clanDiscount=false,bool clanAllegiance=false)
    {
        if (playerId is <0 or >1 || !_state.Cards.TryGetValue(cardId,out var card)) return new(cardId,0,clanDiscount,clanAllegiance,false,"Unknown card or player.",[]);
        var def=Definition(card); var player=Player(playerId);
        if(card.OwnerId!=playerId || card.Zone!=Zone.Province || !card.FaceUp)
            return new(cardId,0,clanDiscount,clanAllegiance,false,"Choose your face-up province card.",[]);
        string? error=null;
        var inClan=def.Type==CardType.Personality && ClanAlignments(card).Contains(player.Clan);
        if(card.OwnerId!=playerId || card.Zone!=Zone.Province || !card.FaceUp) error="Choose your face-up province card.";
        else if(def.Type is not (CardType.Personality or CardType.Holding)) error="This card cannot be purchased.";
        // Loyal (core rules Glossary): recruitable only by a Stronghold whose Clan matches one of the
        // Personality's own alignments -- a Clan-locked deck never exercises this today, only a custom
        // deck mixing an off-Clan Loyal Personality into a player's own provinces would.
        else if(def.Type==CardType.Personality && Keyword(card,"Loyal") && !inClan) error="Loyal Personalities can only be recruited by a Clan they are aligned with.";
        else if((clanDiscount||clanAllegiance)&&!inClan) error="Clan discount and allegiance require your Clan's Personality.";
        else if(def.Type==CardType.Personality && def.HonorRequirement is int requirement && player.Honor<requirement && !clanAllegiance) error="Family Honor is below the Honor Requirement; Clan allegiance may allow purchase.";
        var cost=Math.Max(0,def.GoldCost-(def.Effect=="prosperous-discount"?_state.Cards.Values.Count(c=>c.OwnerId==playerId && InPlay(c) && Definition(c).Type==CardType.Personality):0)+(clanAllegiance?2:0)-(clanDiscount?2:0));
        if (Keyword(card,"Unique") && _state.Cards.Values.Any(c=>c.OwnerId==playerId && InPlay(c) && Definition(c).Name==def.Name)) error="You already control this Unique card.";
        var eligible=error is null && inClan && !clanDiscount && playerId==_state.ActivePlayerId && player.ProclaimUsedTurn!=_state.TurnNumber;
        return new(cardId,cost,clanDiscount,clanAllegiance,eligible,error,error is null ? PaymentOptions(playerId,cost) : []);
    }

    public int GoldProduced(string cardId,int playerId)
    {
        var card=Card(cardId); var def=Definition(card);
        return def.GoldProduction+(def.Effect=="crane-gold" && Player(playerId).Clan=="Crane" || def.Effect=="dragon-gold" && Player(playerId).Clan=="Dragon"?1:0);
    }

    public PaymentOption[] PaymentOptions(int playerId,int cost)
    {
        var sources=Producers(playerId).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
        if(cost==0) return [new([],0,0,0)];
        // Dynamic programming keeps useful alternatives without exponential work for large economies.
        var byTotal=new Dictionary<int,List<string[]>> { [0]=[[]] };
        foreach(var source in sources)
        {
            var production=GoldProduced(source.Id,playerId);
            foreach(var pair in byTotal.OrderByDescending(p=>p.Key).ToArray())
            {
                if(pair.Key>=cost) continue;
                var total=pair.Key+production;
                if(!byTotal.TryGetValue(total,out var combinations)) byTotal[total]=combinations=[];
                foreach(var previous in pair.Value.ToArray())
                    if(combinations.Count<12) combinations.Add([..previous,source.Id]);
            }
        }
        return byTotal.Where(p=>p.Key>=cost).SelectMany(p=>p.Value.Select(ids=>new PaymentOption(ids,p.Key,cost,p.Key-cost)))
            .OrderBy(p=>p.Excess).ThenBy(p=>p.SourceIds.Length).ThenBy(p=>string.Join(",",p.SourceIds),StringComparer.Ordinal).Take(24).ToArray();
    }

    private IEnumerable<CardInstance> Producers(int playerId) => _state.Cards.Values.Where(c=>c.OwnerId==playerId && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield && Definition(c).GoldProduction>0);
    private void ValidatePayment(int playerId,int cost,string[]? sourceIds)
    {
        var ids=sourceIds??[];
        Require(ids.Distinct(StringComparer.Ordinal).Count()==ids.Length,"A Gold source cannot be used twice in one payment.");
        Require(cost>0 || ids.Length==0,"No Gold sources are needed for this free action.");
        var producers=Producers(playerId).Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        Require(ids.All(producers.Contains),"Every Gold source must be yours, in play, unbowed and able to produce Gold.");
        var produced=ids.Sum(id=>GoldProduced(id,playerId));
        Require(produced>=cost,$"This payment needs {cost} Gold; selected sources produce {produced}.");
    }
    private void Pay(int playerId,CardInstance target,int cost,string[]? sourceIds)
    {
        ValidatePayment(playerId,cost,sourceIds);
        var ids=sourceIds??[];
        var produced=ids.Sum(id=>GoldProduced(id,playerId));
        foreach(var id in ids) _state.Cards[id].Bowed=true;
        Emit("payment",$"{Player(playerId).Name} paid {cost} Gold for {Definition(target).Name}, producing {produced}; {produced-cost} excess was lost.",playerId,target.Id,produced-cost);
    }

    private void Proclaim(GameCommand command)
    {
        var pending=_state.PendingChoice!;
        var player=Player(command.PlayerId);
        Require(pending.PlayerId==command.PlayerId && command.PlayerId==_state.ActivePlayerId,"Only the entering Personality's controller may Proclaim during their turn.");
        Require(player.ProclaimUsedTurn!=_state.TurnNumber,"Proclaim has already been used this turn.");
        var card=Card(pending.CardId);
        player.ProclaimUsedTurn=_state.TurnNumber;
        GainHonor(player.Id,Definition(card).PersonalHonor,"Proclaim");
        _state.ConsecutivePasses=0;
        _state.PriorityPlayerId=1-command.PlayerId;
    }
    private void PassReaction()
    {
        var pending=_state.PendingChoice!;
        Emit("reactionPassed", $"{Player(_state.PriorityPlayerId).Name} passed the reaction opportunity.", _state.PriorityPlayerId);
        _state.ConsecutivePasses++;
        if(_state.ConsecutivePasses==2)
        {
            _state.PendingChoice=null;
            _state.ConsecutivePasses=0;
            // Defaults to ActivePlayerId (Dynasty-phase purchasing and mid-Battle attacks never cede
            // priority away from the active player on their own); an entry-reaction window opened by
            // an Action-phase card (Equip, a played Terrain) instead sets ActingPlayerId to the
            // opponent, so the normal "any action cedes priority" rule still holds once it closes.
            _state.PriorityPlayerId=pending.ActingPlayerId ?? _state.ActivePlayerId;
            Emit("reactionClosed","Both players passed; the reaction window closed.");
        }
        else _state.PriorityPlayerId=1-_state.PriorityPlayerId;
    }

    private void Pass()
    {
        Emit("passed", $"{Player(_state.PriorityPlayerId).Name} passed.", _state.PriorityPlayerId);
        switch(_state.Phase)
        {
            case Phase.Action:
                if(++_state.ConsecutivePasses==2) SetPhase(Phase.AttackDeclaration,_state.ActivePlayerId);
                else _state.PriorityPlayerId=1-_state.PriorityPlayerId;
                break;
            // Declining to assign any Infantry is not a decision to skip the whole Attack Phase --
            // Cavalry Maneuvers is a separate step that must still get its own opportunity, exactly
            // as an empty Cavalry declaration below only declines further Cavalry, not the Battle
            // Phase that follows it.
            case Phase.AttackDeclaration: DeclareAttack(new("attack",_state.PriorityPlayerId,_state.Version,Assignments:[])); break;
            case Phase.DefenseAssignment:
            case Phase.CavalryDefense:
                AssignDefense(new("assignDefense",_state.PriorityPlayerId,_state.Version,Assignments:[]));
                break;
            case Phase.CavalryDeclaration: DeclareAttack(new("attack",_state.PriorityPlayerId,_state.Version,Assignments:[])); break;
            case Phase.Battle:
                // Additional Action (issue #181): passing forfeits any of this player's own still-
                // unused grants rather than letting them linger for a future, ordinary turn -- the
                // whole point of an Additional Action is that it resolves immediately, in a burst.
                _state.AdditionalActions.RemoveAll(g=>g.PlayerId==_state.PriorityPlayerId);
                if(++_state.ConsecutivePasses==2) ResolveBattle(); else _state.PriorityPlayerId=1-_state.PriorityPlayerId;
                break;
            case Phase.Dynasty:
            case Phase.DynastyDiscard: EndTurn(); break;
            default: throw new RuleViolation("Choose a battlefield before continuing.");
        }
    }

    private void DiscardProvince(GameCommand command)
    {
        Require(_state.Phase is Phase.Dynasty or Phase.DynastyDiscard,"Only discard provinces at the end of the Dynasty Phase.");
        var card=Card(command.CardId);
        Require(card.OwnerId==command.PlayerId && card.Zone==Zone.Province && card.FaceUp,"Choose your face-up province card.");
        var province=Player(command.PlayerId).Provinces.Single(p=>p.CardId==card.Id);
        card.Zone=Zone.DynastyDiscard; card.ProvinceIndex=null;
        province.CardId=null;
        Emit("provinceDiscard",$"{Player(command.PlayerId).Name} discarded {Definition(card).Name} from a province.",command.PlayerId,card.Id);
        Refill(Player(command.PlayerId),province);
        SetPhase(Phase.DynastyDiscard,command.PlayerId);
    }

    // Infantry Maneuvers (AttackDeclaration/DefenseAssignment) always runs first; Cavalry Maneuvers
    // (CavalryDeclaration/CavalryDefense) follows, adding to the same battlefields Infantry
    // Maneuvers already opened -- only provinces where the attacker actually placed a unit,
    // recomputed after each of the two attacker steps so a Cavalry-only or Infantry-only
    // declaration is just as capable of putting a province up for battle as a mixed one.
    private void DeclareAttack(GameCommand command)
    {
        Require(_state.Phase is Phase.AttackDeclaration or Phase.CavalryDeclaration,"Declare an attack after the Action Phase.");
        // Utaku Chikako's own restriction (issue #186): a broad "may not declare an attack this turn"
        // -- blocks any non-empty attacker assignment outright, Infantry or Cavalry alike, regardless
        // of what would otherwise grant the assignment opportunity (a future "Ambush"-style card
        // included, since this checks the assignment itself, not a specific path to it). An empty
        // assignment (declining to attack) is always still legal.
        Require(!(command.Assignments?.Count>0) || !Player(command.PlayerId).MayNotAttackThisTurn,"You may not declare an attack this turn.");
        var cavalry=_state.Phase==Phase.CavalryDeclaration;
        var targets=Player(1-command.PlayerId).Provinces.Where(p=>!p.Destroyed).Select(p=>p.Index).ToHashSet();
        Assign(command,targets,cavalry);
        _state.UnresolvedBattlefields=_state.Cards.Values.Where(c=>c.OwnerId==command.PlayerId && c.Zone==Zone.Battlefield && c.ProvinceIndex is int)
            .Select(c=>c.ProvinceIndex!.Value).Distinct().Order().ToList();
        Emit("attackDeclared",$"{Player(command.PlayerId).Name} declared {(cavalry?"a Cavalry":"an Infantry")} attack.",command.PlayerId);
        SetPhase(cavalry?Phase.CavalryDefense:Phase.DefenseAssignment,1-command.PlayerId);
    }
    private void AssignDefense(GameCommand command)
    {
        Require(_state.Phase is Phase.DefenseAssignment or Phase.CavalryDefense,"Defenders assign after the attacking army.");
        var cavalry=_state.Phase==Phase.CavalryDefense;
        Assign(command,_state.UnresolvedBattlefields.ToHashSet(),cavalry);
        // Nothing to fight this turn (no Infantry or Cavalry ever attacked anywhere): skip the
        // battlefield-selection/resolution phases outright rather than stall with zero choices.
        SetPhase(cavalry?(_state.UnresolvedBattlefields.Count>0?Phase.BattleSelection:Phase.Dynasty):Phase.CavalryDeclaration,_state.ActivePlayerId);
    }
    private void Assign(GameCommand command,HashSet<int> provinces,bool cavalry)
    {
        foreach(var assignment in command.Assignments??[])
        {
            var card=Card(assignment.Key);
            Require(card.OwnerId==command.PlayerId && card.Zone==Zone.Home && !card.Bowed && Definition(card).Type==CardType.Personality,"Assign only your unbowed Personalities at home.");
            Require(IsCavalryUnit(card)==cavalry,cavalry?"Only a Cavalry unit may assign during Cavalry Maneuvers.":"An Infantry unit must assign during Infantry Maneuvers.");
            Require(provinces.Contains(assignment.Value),"Choose an existing enemy province battlefield.");
            card.Zone=Zone.Battlefield; card.ProvinceIndex=assignment.Value;
            foreach(var attached in Attachments(card.Id)) attached.ProvinceIndex=assignment.Value;
            Emit("unitAssigned",$"{Definition(card).Name} assigned to province {assignment.Value+1}.",command.PlayerId,card.Id);
        }
    }
    // Cavalry (core rules Attack Phase): a unit is Cavalry only if the Personality AND every attached
    // Follower carry the Cavalry keyword -- there is no printed "Infantry" keyword, so any unit
    // missing Cavalry anywhere in it defaults to Infantry and must assign, if at all, during Infantry
    // Maneuvers, regardless of how many of its other cards are Cavalry.
    private bool IsCavalryUnit(CardInstance personality) => Keyword(personality,"Cavalry") &&
        Attachments(personality.Id).Where(c=>Definition(c).Type==CardType.Follower).All(f=>Keyword(f,"Cavalry"));
    private void SelectBattle(GameCommand command)
    {
        Require(_state.Phase==Phase.BattleSelection,"Battle order may be chosen between battles.");
        Require(command.Province is int && _state.UnresolvedBattlefields.Contains(command.Province.Value),"Choose an unresolved battlefield.");
        _state.CurrentBattleProvince=command.Province;
        SetPhase(Phase.Battle,1-_state.ActivePlayerId);
        Emit("battleStarted",$"Battle at province {command.Province+1}: defender acts first.");
    }
    private void ResolveBattle()
    {
        var provinceIndex=_state.CurrentBattleProvince!.Value;
        var attacker=_state.ActivePlayerId; var defender=1-attacker;
        var a=Army(attacker,provinceIndex).ToArray(); var d=Army(defender,provinceIndex).ToArray();
        var af=a.Sum(BattleForce); var df=d.Sum(BattleForce);
        Emit("battleResolved",$"Province {provinceIndex+1}: attacking Force {af}, defending Force {df}.");
        // Reconnaissance resets to No for both players at this battlefield once its battle resolves,
        // regardless of the outcome -- the only trigger that clears it.
        Player(defender).Provinces.Single(p=>p.Index==provinceIndex).Reconnaissance=new bool[2];
        var attackerWon=af>df;
        if(attackerWon)
        {
            var destroyedDefenders=DestroyUnits(d);
            if(!RehonorWinners(a)) GainHonor(attacker,destroyedDefenders*2,"Battle victory");
            var province=Player(defender).Provinces.Single(p=>p.Index==provinceIndex);
            var destroyedProvince=af>df+province.Strength;
            if(destroyedProvince) DestroyProvince(defender,province);
            if(destroyedDefenders>0 || destroyedProvince) AttackerDestructionBonus(attacker,a);
            // Ring recruitment (issue #194): checked with this resolution's own facts, before the
            // per-battle flags below reset. False Ring of Air requires an outright win; Ring of
            // Water does not (see the tie branch below).
            GrantRingRecruit(Player(attacker),d.Length>0,"false-ring-of-air-recruit");
            CheckRingOfWaterRecruit(Player(attacker),destroyedDefenders>0||destroyedProvince);
        }
        else if(df>af)
        {
            var destroyedAttackers=DestroyUnits(a);
            if(!RehonorWinners(d)) GainHonor(defender,destroyedAttackers*2,"Battle victory");
            GrantRingRecruit(Player(defender),a.Length>0,"false-ring-of-air-recruit");
            CheckRingOfWaterRecruit(Player(defender),destroyedAttackers>0);
        }
        else if(a.Length>0 && d.Length>0)
        {
            // Tie (issue #186): check and rehonor BEFORE destruction -- a dishonorable member about to
            // be destroyed in the same tie must never also take its own dishonorable-death Honor
            // penalty (MarkDead) on top of this battle's own gain negation.
            var attackerHadDishonorable=RehonorWinners(a);
            var defenderHadDishonorable=RehonorWinners(d);
            var destroyedA=DestroyUnits(a); var destroyedD=DestroyUnits(d);
            if(!attackerHadDishonorable) GainHonor(attacker,destroyedD,"Tied battle");
            if(!defenderHadDishonorable) GainHonor(defender,destroyedA,"Tied battle");
            if(destroyedD>0) AttackerDestructionBonus(attacker,a);
            // Ring of Water (issue #194): no "win" requirement, unlike False Ring of Air -- a tie
            // still counts as "a battle resolves" for both players independently.
            CheckRingOfWaterRecruit(Player(attacker),destroyedD>0);
            CheckRingOfWaterRecruit(Player(defender),destroyedA>0);
        }
        // Conqueror (core rules Glossary): only relevant when the attacker won -- the attacking army
        // (a) is untouched in that branch (only defenders are destroyed), so candidates read off it
        // here are still accurate even if the choice must be deferred to the attacker.
        var conquerorCandidates=attackerWon ? a.Where(c=>c.Zone==Zone.Battlefield && Keyword(c,"Conqueror")).Select(c=>c.Id).ToArray() : [];
        if(conquerorCandidates.Length>1)
        {
            _state.PendingConqueror=new PendingConqueror { PlayerId=attacker, ProvinceIndex=provinceIndex, CandidateIds=conquerorCandidates };
            _state.PendingChoice=new("conqueror",attacker,null,"Choose a Conqueror personality to straighten instead of returning home bowed.");
            _state.PriorityPlayerId=attacker;
            Emit("conquerorChoice","Choose a Conqueror personality to straighten.",attacker);
            return; // FinishBattleResolution runs once the attacker picks (ResolveConqueror).
        }
        FinishBattleResolution(attacker,provinceIndex,a,conquerorCandidates.Length==1?conquerorCandidates[0]:null);
    }
    private void ResolveConqueror(GameCommand command)
    {
        var pending=_state.PendingConqueror!;
        Require(command.Type=="chooseConqueror" && command.PlayerId==pending.PlayerId && command.TargetId is not null && pending.CandidateIds.Contains(command.TargetId),
            "Choose one of the eligible Conqueror personalities.");
        var chosenId=command.TargetId!;
        var a=Army(pending.PlayerId,pending.ProvinceIndex).ToArray();
        _state.PendingChoice=null; _state.PendingConqueror=null;
        FinishBattleResolution(pending.PlayerId,pending.ProvinceIndex,a,chosenId);
    }
    // Common tail of battle resolution: attacking units return home (bowed, except the Conqueror
    // choice, if any), then the battlefield closes out. Split out so a deferred Conqueror choice can
    // reach the same completion as the immediate (0 or 1 candidate) path.
    private void FinishBattleResolution(int attacker,int provinceIndex,CardInstance[] a,string? conquerorUnbowedId)
    {
        foreach(var card in a.Where(c=>c.Zone==Zone.Battlefield))
        {
            ReturnHome(card,card.Id!=conquerorUnbowedId);
            if(card.Id==conquerorUnbowedId) Emit("conquerorStraightened",$"{Definition(card).Name} returned home unbowed (Conqueror).",card.OwnerId,card.Id);
        }
        // Terrain leaves the battlefield here, after its printed effects (already applied earlier in
        // ResolveBattle) but before the battlefield itself closes out -- never carried into a later
        // battle at the same province, or across turns.
        foreach(var terrain in _state.Cards.Values.Where(c=>c.Zone==Zone.Battlefield && c.ProvinceIndex==provinceIndex && Keyword(c,"Terrain")).ToArray())
            DiscardTerrain(terrain);
        _state.UnresolvedBattlefields.Remove(provinceIndex); _state.CurrentBattleProvince=null;
        // Ashigaru Elite's boost is "for the remainder of the battle," not the turn -- a turn can
        // contain more than one battle (Infantry/Cavalry Maneuvers), so it must not carry into the next.
        _state.AshigaruBoost=new int[2];
        // Additional Action grants/restrictions (issue #181) are battle-scoped too -- Two Hands'
        // "ceases during battle resolution" is explicit, and an unused Again!/Equip(Battle) grant
        // makes no sense carried into a battle it was never meant for.
        // Ring recruitment (issue #194): a grant scoped to a Ring card (added above, in ResolveBattle,
        // BEFORE this same call) must survive this Clear() -- it represents a right earned as of THIS
        // battle's own conclusion, to be used in a later window, unlike an ordinary Additional Action
        // grant (which genuinely is scoped to just this battle).
        _state.AdditionalActions.RemoveAll(g => !(g.ScopeCardIds is { Length: 1 } ids && Definition(Card(ids[0])).Type == CardType.Ring));
        _state.PendingAdditionalAction=null; _state.NoNewAdditionalActionsThisBattle=false;
        // Ring of Water's own recruitment condition (issue #194) is battle-scoped, same reasoning.
        foreach(var p in _state.Players) { p.TerrainPlayedThisBattle=false; p.TerrainDestroyedThisBattle=false; }
        if(_state.WinnerId is not null) return;
        if(_state.UnresolvedBattlefields.Count==0)
        {
            foreach(var card in _state.Cards.Values.Where(c=>c.Zone==Zone.Battlefield).ToArray()) ReturnHome(card,card.OwnerId==attacker);
            SetPhase(Phase.Dynasty,attacker);
        }
        else SetPhase(Phase.BattleSelection,attacker);
    }
    // Plains of the Maiden: after the Attacker destroys a province or army in resolution, gain Honor
    // equal to their surviving Battle Maidens at this battlefield. Checked against units still in the
    // battlefield zone after destruction, so a Battle Maiden destroyed in the same (tied) resolution
    // does not count toward her own bonus.
    private void AttackerDestructionBonus(int attacker,CardInstance[] attackingArmy)
    {
        if(Definition(Card(Player(attacker).StrongholdId)).Effect!="plains-honor") return;
        var maidens=attackingArmy.Count(c=>c.Zone==Zone.Battlefield && Keyword(c,"Battle Maiden"));
        if(maidens>0) GainHonor(attacker,maidens,"Plains of the Maiden");
    }
    // Dishonor/Rehonor (core rules Glossary, issue #186): "if a dishonorable personality is in an
    // army that wins or ties a battle and destroys cards in an opposing army, all of the dishonorable
    // personalities in the winner's army are rehonored and the FULL honor gain from winning is
    // negated." Called (and, for a tie, rehonoring applied) before that army's own units are
    // destroyed, in case it is also on the losing end of a tie. Returns whether any rehonoring
    // happened, so the caller knows whether to skip its own GainHonor entirely.
    private bool RehonorWinners(CardInstance[] army)
    {
        var dishonorable=army.Where(c=>c.Zone==Zone.Battlefield && c.Dishonorable).ToArray();
        foreach(var card in dishonorable) Rehonor(card);
        return dishonorable.Length>0;
    }

    private void Equip(GameCommand command)
    {
        Require(_state.Phase==Phase.Action && command.PlayerId==_state.ActivePlayerId,"Equip is a Limited action on your turn.");
        var card=Card(command.CardId); var target=Card(command.TargetId); var def=Definition(card);
        Require(card.OwnerId==command.PlayerId && card.Zone==Zone.Hand && def.Type is CardType.Item or CardType.Follower,"Choose a supported attachment from your hand.");
        Require(target.OwnerId==command.PlayerId && target.Zone==Zone.Home && Definition(target).Type==CardType.Personality,"Choose your Personality at home.");
        Require(def.AttachRequiresKeyword is null || KeywordOrHuman(target,def.AttachRequiresKeyword),$"Will only attach to a {def.AttachRequiresKeyword} Personality.");
        Require(def.AttachExcludesKeyword is null || !KeywordOrHuman(target,def.AttachExcludesKeyword),$"Will not attach to a {def.AttachExcludesKeyword} Personality.");
        Require(def.AttachRequiresName is null || Definition(target).Name==def.AttachRequiresName,$"Only attaches to {def.AttachRequiresName}.");
        Require(def.Type!=CardType.Follower || def.HonorRequirement is null || CardPersonalHonor(target)>=def.HonorRequirement,"The Personality does not meet this Follower's Personal Honor Requirement.");
        Require(AttachmentAllowed(card,target),"A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
        Pay(command.PlayerId,card,def.GoldCost,command.SourceIds);
        Player(command.PlayerId).Hand.Remove(card.Id);
        card.Zone=Zone.Attached; card.AttachedTo=target.Id; card.FaceUp=true;
        if(def.AttachGrantsKeyword is string granted) GrantKeyword(target,granted,card.Id,"Attachment");
        Emit("equipped",$"{def.Name} attached to {Definition(target).Name}.",command.PlayerId,card.Id);
        if(OpenEntryReactionWindow(command.PlayerId,card,1-command.PlayerId)) return;
        NextAction(command.PlayerId);
    }
    private void PlayStrategy(GameCommand command)
    {
        Require(_state.Phase==Phase.Battle,"This Strategy can only be played during a battle.");
        var card=Card(command.CardId); var def=Definition(card);
        Require(card.OwnerId==command.PlayerId && card.Zone==Zone.Hand && def.Type==CardType.Strategy,"Choose a supported Strategy from your hand.");
        var province=_state.CurrentBattleProvince!.Value;
        Require(Army(command.PlayerId,province).Any(),"You need a unit at the current battlefield to take a Battle action.");
        // Terrain (core rules Glossary): the printed effects only apply at the battlefield where it was
        // played -- itself just stays there as a card, not a target for a Personality-shaped effect --
        // and the default limit is at most one Terrain per battlefield until the old one is destroyed.
        if(Keyword(card,"Terrain"))
        {
            Require(command.TargetId is null,"Playing a Terrain does not target another card.");
            Require(!_state.Cards.Values.Any(c=>c.Zone==Zone.Battlefield && c.ProvinceIndex==province && Keyword(c,"Terrain")),
                "This battlefield already has a Terrain in play.");
            Player(command.PlayerId).Hand.Remove(card.Id);
            card.Zone=Zone.Battlefield; card.ProvinceIndex=province; card.FaceUp=true;
            Player(command.PlayerId).TerrainPlayedThisBattle=true; // Ring of Water's own recruitment condition (issue #194)
            Emit("terrainPlayed",$"{Player(command.PlayerId).Name} played {def.Name} as Terrain at this battlefield.",command.PlayerId,card.Id);
            if(OpenEntryReactionWindow(command.PlayerId,card,1-command.PlayerId)) return;
            NextAction(command.PlayerId);
            return;
        }
        var target=Card(command.TargetId);
        Require(target.Zone==Zone.Battlefield && target.ProvinceIndex==province && Definition(target).Type==CardType.Personality,"Target a Personality at the current battlefield.");
        switch(def.Effect)
        {
            case "force-three": Require(target.OwnerId==command.PlayerId,"Training rally targets your Personality."); ApplyModifier(target,card.Id,def.Effect,"Force",3); break;
            case "bow-small": Require(target.OwnerId!=command.PlayerId && PersonalityForce(target)<=3,"Training pressure targets an enemy Personality with 3 or less Force."); if(!StalwartNegatesBow(target,command.PlayerId)) target.Bowed=true; break;
            default: throw new RuleViolation("This card effect is not implemented.");
        }
        Player(command.PlayerId).Hand.Remove(card.Id); card.Zone=Zone.FateDiscard; card.FaceUp=true;
        Emit("strategyPlayed",$"{Player(command.PlayerId).Name} played {def.Name} targeting {Definition(target).Name}.",command.PlayerId,card.Id);
        NextAction(command.PlayerId);
    }

    // stayWithPlayer (issue #181): true only when the action just resolved was itself Additional-
    // Action-related (consumed a grant, or -- Again!/Two Hands themselves -- just created one) AND
    // the player still has an unused grant afterward. An ordinary action never sets this, even if
    // unrelated grants happen to be sitting unused for this player -- only the specific action that
    // touched them may skip ceding priority.
    private void NextAction(int playerId,bool stayWithPlayer=false) { _state.ConsecutivePasses=0; _state.PriorityPlayerId=stayWithPlayer?playerId:1-playerId; }
    private IEnumerable<CardInstance> Army(int playerId,int province) => _state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone==Zone.Battlefield && c.ProvinceIndex==province && Definition(c).Type==CardType.Personality);
    // Province adjacency (core rules): a player's provinces sit in a line, 1-2-3-4. A destroyed
    // province is skipped entirely -- its former neighbors become directly adjacent to each other,
    // compounding across multiple destructions (destroy both middle provinces and the two ends become
    // adjacent to each other). Equivalent to the "provinces slide together" mental model, and derived
    // on demand from each ProvinceState's own Destroyed flag rather than tracked as separate routed
    // state -- no renumbering, no adjacency table to keep in sync as provinces are destroyed/refilled.
    // Not hardcoded to 4: the loop bound is Provinces.Count, so this also covers a match with more
    // provinces (a 5th/6th added by a game effect: fate and dynasty decks shift one card-space apart
    // and a new face-down dynasty card is placed in the new space, numbered 5/6) with no code change --
    // that's simply a new ProvinceState appended to the list; adjacency for it falls out unchanged.
    private IEnumerable<int> AdjacentProvinces(int playerId,int index)
    {
        var provinces=Player(playerId).Provinces;
        for(var i=index-1;i>=0;i--) if(!provinces[i].Destroyed) { yield return i; break; }
        for(var i=index+1;i<provinces.Count;i++) if(!provinces[i].Destroyed) { yield return i; break; }
    }
    // By default a Personality carries at most one Armor and one Weapon. Kensai (printed or granted --
    // Keyword() already checks both) raises the Weapon cap to two. Kama independently raises it to two
    // without granting Kensai, but the two do not stack: a Kensai wielding a Kama is still capped at
    // two Weapons, not three.
    private bool AttachmentAllowed(CardInstance attachment,CardInstance target)
    {
        if(Keyword(attachment,"Armor")) return !Attachments(target.Id).Any(c=>Keyword(c,"Armor"));
        if(Keyword(attachment,"Weapon"))
        {
            var existingWeapons=Attachments(target.Id).Where(c=>Keyword(c,"Weapon")).ToArray();
            // Two-Handed overrides Kensai and Kama entirely: whenever a Two-Handed Weapon is involved
            // (already attached, or the one being attached), at most one Weapon total is ever allowed.
            if(Keyword(attachment,"Two-Handed") || existingWeapons.Any(c=>Keyword(c,"Two-Handed"))) return existingWeapons.Length==0;
            var weaponCap=Keyword(target,"Kensai") || Keyword(attachment,"Kama") || existingWeapons.Any(c=>Keyword(c,"Kama")) ? 2 : 1;
            return existingWeapons.Length<weaponCap;
        }
        return true;
    }
    private IEnumerable<CardInstance> Attachments(string id) => _state.Cards.Values.Where(c=>c.Zone==Zone.Attached && c.AttachedTo==id);
    // "Without attachments" (core rules): a boolean condition, not a count -- Followers/Items/Spells
    // always qualify as attachments; so do tokens specifically designated as one (a Follower token, a
    // Weapon token, an Armor token), unlike an ordinary stat/status token (Fire, Poison, Madness...).
    // An attachment can never itself have attachments, so this is always false for one -- no special
    // case needed, since nothing in this engine attaches to an attachment or grants it one of these
    // token types.
    private static readonly string[] AttachmentTokenTypes = ["Follower","Weapon","Armor"];
    private bool HasAttachments(CardInstance card) => Attachments(card.Id).Any() || card.Tokens.Any(t=>AttachmentTokenTypes.Contains(t.Type));
    // "Human" is never printed as a keyword -- a Personality/Follower without "Nonhuman" printed is
    // Human by default (core rules Glossary).
    private static bool KeywordOrHuman(CardInstance card,string keyword) => keyword=="Human" ? !Keyword(card,"Nonhuman") : Keyword(card,keyword);
    // An attachment's own "Will only/not attach to X" restriction and Follower Honor Requirement,
    // checked together wherever a legal target is decided (Equip's own Require and the offered-action
    // legal-targets filter both call this, so they can never disagree).
    private bool AttachmentLegalTarget(CardDefinition attachmentDef,CardInstance target) =>
        (attachmentDef.AttachRequiresKeyword is null || KeywordOrHuman(target,attachmentDef.AttachRequiresKeyword)) &&
        (attachmentDef.AttachExcludesKeyword is null || !KeywordOrHuman(target,attachmentDef.AttachExcludesKeyword)) &&
        (attachmentDef.AttachRequiresName is null || Definition(target).Name==attachmentDef.AttachRequiresName) &&
        (attachmentDef.Type!=CardType.Follower || attachmentDef.HonorRequirement is null || CardPersonalHonor(target)>=attachmentDef.HonorRequirement);
    // "Current" Personal Honor (core rules Glossary): a Dishonorable Personality's is fixed at 0 until
    // Rehonor restores them -- fixed at 0, not merely reduced, so a persistent modifier (e.g. a token)
    // is not removed by this and resumes applying once honorable again. The handful of rules that
    // deliberately key off the *printed* value regardless of current status (Dishonorable death's
    // Honor penalty, Proclaim's grant) intentionally do not use this helper.
    private int CardPersonalHonor(CardInstance card) => card.Dishonorable && !DishonorablePersonalHonorOverridden(card) ? 0 : Definition(card).PersonalHonor+StatModifier(card,"PersonalHonor");
    // Utaku Chikako's own passive (issue #186): "Your dishonorable Battle Maidens do not have a
    // maximum Personal Honor of 0" -- a Personality's own passive (not a Stronghold's), so checked
    // generically against any in-play Personality the SAME owner controls carrying this Effect,
    // rather than fixed to one specific card.
    private bool DishonorablePersonalHonorOverridden(CardInstance card) =>
        Keyword(card,"Battle Maiden") && _state.Cards.Values.Any(c=>c.OwnerId==card.OwnerId && InPlay(c) && Definition(c).Effect=="battle-maiden-dishonor-immunity");
    // "force-with-spell" (Asako Ume), "force-if-enemy-dishonorable" (Bayushi Kahoku) and
    // "force-max-dead-pile" (Moto Shunsuke) are always-on passive bonuses, unlike the attacking-only
    // attacking-two/attacking-four Effects above.
    private int PersonalityForce(CardInstance card) => Math.Max(0,Definition(card).Force+StatModifier(card,"Force")+(IsAttacking(card)?(Definition(card).Effect=="attacking-two"?2:Definition(card).Effect=="attacking-four"?4:0)-(HasHalls(card.OwnerId)&&Keyword(card,"Ancestor")?1:0):0)
        // An Item's printed "+F" box (and any conditional bonus it grants the wielder, e.g. Kensai's
        // Blade's "+1F to a Personality with Kensai") always conveys to the wielder, whether the Item
        // is bowed or not -- unlike a Follower's own Force, which stays with the Follower itself (see
        // FollowerForce) and only joins the unit total in BattleForce when unbowed or Elite.
        +Attachments(card.Id).Where(c=>Definition(c).Type==CardType.Item).Sum(c=>Definition(c).Force+ItemForceToWielder(c,card))
        +(Definition(card).Effect=="force-with-spell"&&Attachments(card.Id).Any(c=>Definition(c).Type==CardType.Spell)?1:0)
        +(Definition(card).Effect=="force-if-enemy-dishonorable"&&_state.Cards.Values.Any(c=>c.OwnerId!=card.OwnerId&&Definition(c).Type==CardType.Personality&&InPlay(c)&&c.Dishonorable)?2:0)
        // "The highest number of dead Personalities in any one discard pile" -- the greatest per-player
        // count of Zone.Dead Personalities, i.e. the largest single pile, not the combined total.
        +(Definition(card).Effect=="force-max-dead-pile"?_state.Cards.Values.Where(c=>c.Zone==Zone.Dead&&Definition(c).Type==CardType.Personality).GroupBy(c=>c.OwnerId).Select(g=>g.Count()).DefaultIfEmpty(0).Max():0)
        +AshigaruBattleBoost(card)+ArmyBattleMaidenBoost(card));
    // An Item's own conditional Force bonus to its wielder, keyed by the Item's Effect string --
    // distinct from a Follower's self-referential conditional Force (FollowerForce below), since an
    // Item's bonus always targets the Personality, never itself.
    private int ItemForceToWielder(CardInstance item,CardInstance wielder) => Definition(item).Effect switch
    {
        "force-to-kensai-wielder" => Keyword(wielder,"Kensai") ? 1 : 0,
        _ => 0
    };
    // A Follower's own standalone Force, including any static conditional bonus it grants itself
    // (e.g. "+2F while defending") -- distinct from an Item's, which always conveys to the wielder
    // instead (see PersonalityForce/ItemForceToWielder). Feeds both CardForce (targeting, always
    // visible regardless of bow state) and BattleForce (unit total, gated on unbowed-or-Elite).
    private int FollowerForce(CardInstance card) => Math.Max(0,Definition(card).Force+StatModifier(card,"Force")+(Definition(card).Effect switch
    {
        "force-while-defending" => IsDefendingUnit(card) ? 2 : 0,
        // Disciples of Ganesh: "+2F while defending" -- shares FollowerForce's existing case, plus a
        // separate destruction-triggered Honor gain (see DestroyedDuringBattleBonus, keyed by the same
        // Effect string) -- one card, two engine hooks, since Effect is a single field per card.
        "force-while-defending-honor-if-destroyed" => IsDefendingUnit(card) ? 2 : 0,
        // Family Sensei / Merchant Guard (core rules): self-referential, even though the condition
        // counts/checks other cards -- the same shape PersonalityForce's conditional bonuses already
        // cover, not the "boost OTHER cards" aggregate primitives below.
        "force-with-dojo" => ControlsKeyword(card.OwnerId,"Dojo") ? 1 : 0,
        "force-with-market" => ControlsKeyword(card.OwnerId,"Market") ? 1 : 0,
        // Ashigaru Guards: "+1F for every two Farms you control" -- a controlled-card COUNT, still
        // self-referential (the bonus lands on this card), same shape as PersonalityForce's
        // force-max-dead-pile.
        "force-per-two-farms" => _state.Cards.Values.Count(c=>c.OwnerId==card.OwnerId && InPlay(c) && Keyword(c,"Farm"))/2,
        // Brothers of the Great Lake: "+1F for each OTHER [same name] in this army" -- counts sibling
        // copies at the same battlefield, excluding itself. A Follower's own Zone is Attached, not
        // Battlefield, so this resolves through its parent Personality first (same as ArmyBattleMaidenBoost).
        "force-per-other-same-name-in-army" => BattlefieldParent(card) is {} parent
            ? _state.Cards.Values.Count(c=>c.Id!=card.Id && Definition(c).Name==Definition(card).Name
                && BattlefieldParent(c) is {} otherParent && otherParent.OwnerId==parent.OwnerId && otherParent.ProvinceIndex==parent.ProvinceIndex)
            : 0,
        // Ninja Guards: "Force bonus equal to its Personality's printed Force" -- reads the WIELDER's
        // own printed stat, the opposite data-flow direction from ItemForceToWielder (which conveys an
        // Item's bonus TO the wielder); here the Follower's own Force is keyed off the wielder instead.
        "force-equals-wielder-printed-force" => card.AttachedTo is null ? 0 : Definition(Card(card.AttachedTo)).Force,
        _ => 0
    })+UnitFollowerBoost(card)+AshigaruBattleBoost(card)+ArmyBattleMaidenBoost(card));
    // "Boost other cards in the unit" primitive (Senior Infantry Legion's shape): distinct from the
    // self-referential conditional bonuses above, this is a card that grants a static Force bonus to
    // every OTHER Follower attached to the same Personality (a unit is a Personality and all of its
    // attachments -- core rules Glossary). Excludes itself, so two copies of the same booster in one
    // unit grant each other the bonus but never themselves. Always-on and static, so it belongs here
    // in FollowerForce rather than behind an activated ability.
    private int UnitFollowerBoost(CardInstance follower) =>
        follower.AttachedTo is null ? 0 : Attachments(follower.AttachedTo).Where(c=>c.Id!=follower.Id).Sum(c=>Definition(c).Effect switch
        {
            "boost-followers-in-unit" => 1,
            // Aulus's shape: keyword-based rather than type-based, still unit-scoped -- "Legionnaires
            // in this unit have +1F" reuses the exact same unit-scoping as Senior Infantry Legion, just
            // gated by a keyword instead of the Follower type (already implicit, since UnitFollowerBoost
            // only ever feeds FollowerForce).
            "boost-legionnaires-in-unit" => Keyword(follower,"Legionnaire") ? 1 : 0,
            _ => 0
        });
    // Aggregate/group Force effects, second shape (Ashigaru Elite): unlike UnitFollowerBoost's static
    // per-unit bonus, this is an activated, stacking, per-battle bonus that reaches every Ashigaru-
    // keyword card the performer controls at the CURRENT battlefield -- not just its own unit. Read
    // from MatchState.AshigaruBoost (incremented once per use, reset to zero whenever the battle
    // resolves -- see FinishBattleResolution), and gated by AtBattle so it never leaks to a card
    // sitting at a different or no-longer-current battlefield.
    private int AshigaruBattleBoost(CardInstance card) => AtBattle(card) && Keyword(card,"Ashigaru") ? _state.AshigaruBoost[card.OwnerId] : 0;
    // Aggregate/group Force effects, third shape (Utaku Elite Guard): like UnitFollowerBoost, a static
    // "boost others" bonus, but keyword-based (Battle Maiden) rather than type-based (Follower), and
    // army-scoped -- every OTHER Battle-Maiden card the same owner controls in the same battlefield
    // army (same province, both in Zone.Battlefield, following an attachment through to its parent
    // Personality) -- rather than confined to one unit. Excludes itself, matching Senior Infantry
    // Legion's "not to itself" rule: two Utaku Elite Guards in one army grant each other, not themselves.
    // The real card's own text ("...have +1F while attacking") additionally gates on IsAttacking --
    // reusing the same helper PersonalityForce's attacking-two/attacking-four Effects already use.
    private int ArmyBattleMaidenBoost(CardInstance card)
    {
        if (!Keyword(card,"Battle Maiden")) return 0;
        var parent = BattlefieldParent(card);
        if (parent is null || !IsAttacking(parent)) return 0;
        return _state.Cards.Values.Count(c=>c.Id!=card.Id && c.OwnerId==parent.OwnerId && c.Zone==Zone.Battlefield && c.ProvinceIndex==parent.ProvinceIndex
            && Definition(c).Type==CardType.Personality && Definition(c).Effect=="boost-battle-maidens-in-army");
    }
    // Shared by both army-scoped aggregate effects above: a card's own Personality "unit" at the
    // battlefield -- itself, if it's the Personality directly on the battlefield, or (for an attached
    // Follower, whose own Zone is Attached, never Battlefield) its wielder, if THAT is on the
    // battlefield. Null when neither the card nor its wielder is actually at a battlefield.
    private CardInstance? BattlefieldParent(CardInstance card)
    {
        var parent = card.Zone==Zone.Attached ? Card(card.AttachedTo!) : card;
        return parent.Zone==Zone.Battlefield ? parent : null;
    }
    // A bowed ordinary Personality suppresses its entire unit at resolution.
    // Preserve the explicit Elite-Personality exception accepted in #19/#17.
    // An Elite Follower cannot bypass its non-Elite Personality's bowed state.
    // EliteEffect: true for a card with the actual printed/granted Elite keyword, but also for a
    // wielder carrying an attachment that grants only Elite's effect without the keyword itself
    // (Seppun Heavy Elite's shape -- see AttachGrantsEliteEffect).
    private bool EliteEffect(CardInstance card) => Keyword(card,"Elite") || Attachments(card.Id).Any(a=>Definition(a).AttachGrantsEliteEffect);
    private int BattleForce(CardInstance card)
    {
        if(card.Bowed && !EliteEffect(card)) return 0;
        return PersonalityForce(card)+Attachments(card.Id)
            .Where(c=>(!c.Bowed || EliteEffect(c)) && Definition(c).Type==CardType.Follower)
            .Sum(FollowerForce);
    }
    // "Dead / Destroy" (core rules Glossary): entering the Dead zone, distinct from merely being
    // discarded. A Personality who is still Dishonorable at this moment costs its controller Honor
    // equal to his base Personal Honor -- Seppuku (and any future rehonor-before-death effect) spares
    // this by clearing Dishonorable first (see Rehonor). This is the one shared entry point every
    // path to "becoming dead" -- combat destruction or otherwise -- must go through.
    private void MarkDead(CardInstance card)
    {
        card.Zone=Zone.Dead; card.FaceUp=true; card.Tokens.Clear();
        if(Definition(card).Type==CardType.Personality && card.Dishonorable) GainHonor(card.OwnerId,-Definition(card).PersonalHonor,"Dishonorable death",card);
    }
    private void Rehonor(CardInstance card) => card.Dishonorable=false;
    // Dishonor (core rules Glossary, issue #186): gives a Personality the Dishonorable status --
    // treated as Personal Honor 0 (see CardPersonalHonor) until Rehonor restores them. The 180-degree
    // rotation the rules describe is purely visual, driven client-side off this same flag; no engine
    // state beyond it.
    private void Dishonor(CardInstance card) => card.Dishonorable=true;
    // A card already sitting in a discard pile (Zone.DynastyDiscard/FateDiscard) -- never destroyed
    // via DestroyUnits, so it carries no attachments/tokens to cascade -- becomes dead. "Honorably
    // dead" (Glossary) means dead and honorable; honorable:false leaves its current status as-is.
    private void MakeDead(CardInstance card, bool honorable)
    {
        if(honorable) Rehonor(card);
        MarkDead(card);
        Emit("becameDead",$"{Definition(card).Name} became {(honorable?"honorably ":"")}dead.",card.OwnerId,card.Id);
    }
    private int DestroyUnits(CardInstance[] cards, bool combatDestruction = false)
    {
        var count=0;
        foreach(var card in cards)
        {
            foreach(var attachment in Attachments(card.Id).ToArray())
            {
                attachment.Zone=Zone.FateDiscard; attachment.AttachedTo=null; attachment.ProvinceIndex=null; attachment.Tokens.Clear(); count++;
                DestroyedDuringBattleBonus(attachment);
                ExpendableDraw(attachment);
            }
            card.ProvinceIndex=null;
            MarkDead(card);
            count++;
            if(combatDestruction && _state.Phase==Phase.Battle && HasHalls(card.OwnerId) && Keyword(card,"Ancestor")) GainHonor(card.OwnerId,1,"Halls of Memory — Combat Segment destruction");
            DestroyedDuringBattleBonus(card);
            ExpendableDraw(card);
            Emit("unitDestroyed",$"{Definition(card).Name} and its attachments were destroyed.",card.OwnerId,card.Id);
        }
        return count;
    }
    // Disciples of Ganesh's shape: "After this Follower is destroyed during battle, gain 1 Honor" --
    // a static Effect checked at every destruction path (the sweep above, and DestroyAttachment's own
    // single-card path), gated on the Battle phase rather than combatDestruction specifically, since the
    // printed condition is "during battle" (the phase), not "as a result of battle resolution."
    private void DestroyedDuringBattleBonus(CardInstance card)
    {
        if(_state.Phase==Phase.Battle && Definition(card).Effect is "gain-honor-if-destroyed" or "force-while-defending-honor-if-destroyed") GainHonor(card.OwnerId,1,$"{Definition(card).Name} destroyed during battle");
    }
    // Expendable keyword (core rules): a mandatory Fate-deck draw whenever a card carrying it is
    // destroyed by any effect -- distinct from merely being discarded (core rules Glossary: Destroyed
    // vs. Discarded). Checked at every real "destroy" path -- here, and DestroyAttachment's own
    // single-attachment path -- never at a plain discard (DiscardFate, hand discards, etc.), since
    // those never call these functions. Keyword-keyed, not Effect-keyed: any card type can carry it.
    private void ExpendableDraw(CardInstance card)
    {
        if(Keyword(card,"Expendable")) Draw(Player(card.OwnerId));
    }
    private void DestroyProvince(int owner,ProvinceState province)
    {
        province.Destroyed=true;
        if(province.CardId is string id) { var card=Card(id); card.Zone=Zone.DynastyDiscard; card.FaceUp=true; card.ProvinceIndex=null; }
        province.CardId=null;
        Emit("provinceDestroyed",$"{Player(owner).Name}'s province {province.Index+1} was destroyed.",owner);
        if(Player(owner).Provinces.All(p=>p.Destroyed)) Win(1-owner,"Military victory");
    }
    private void ReturnHome(CardInstance card,bool bow)
    {
        card.Zone=Zone.Home; card.ProvinceIndex=null;
        if(bow) card.Bowed=true;
        foreach(var attachment in Attachments(card.Id)) { attachment.ProvinceIndex=null; if(bow) attachment.Bowed=true; }
    }

    private void EndTurn()
    {
        var player=Player(_state.ActivePlayerId);
        SetPhase(Phase.End,player.Id);
        if(player.DishonorLossPending) { Win(1-player.Id,"Dishonor defeat at the beginning of the End Phase"); return; }
        Draw(player);
        if(player.Hand.Count>8) _state.PendingChoice=new("discardHand",player.Id,null,$"Discard {player.Hand.Count-8} card(s) to return to eight.");
        else FinishTurn();
    }
    private void DiscardHand(GameCommand command)
    {
        var player=Player(command.PlayerId); var ids=command.CardIds??[];
        Require(ids.Length==player.Hand.Count-8 && ids.Distinct().Count()==ids.Length && ids.All(player.Hand.Contains),"Select exactly enough different cards from your hand to return to eight.");
        foreach(var id in ids) { player.Hand.Remove(id); var card=Card(id); card.Zone=Zone.FateDiscard; card.FaceUp=true; Emit("handDiscard",$"{player.Name} discarded {Definition(card).Name}.",player.Id,id); }
        _state.PendingChoice=null; FinishTurn();
    }
    private void FinishTurn()
    {
        foreach(var card in _state.Cards.Values) { card.Modifiers.Clear(); card.GrantedKeywords.RemoveAll(g=>g.Duration=="Turn"); }
        _state.TurnNumber++; _state.ActivePlayerId=1-_state.ActivePlayerId;
        StartTurn();
    }
    private void StartTurn()
    {
        var player=Player(_state.ActivePlayerId);
        _state.PriorityPlayerId=player.Id;
        if(player.Honor>=40) { Win(player.Id,"Honor victory at the start of the turn"); return; }
        // Enlightenment victory (issue #194): begins their turn with all five REAL elemental Rings
        // (Fire/Water/Air/Earth/Void) in play -- checked by Keyword, not a fixed card id, since more
        // than one printing of a given Ring exists. Every False Ring carries its own matching
        // elemental keyword too (e.g. False Ring of Air prints "Air"), so this alone is not enough
        // to exclude them -- but all five False Rings also carry "Fudo", which no real Ring does,
        // so that keyword is the actual False-Ring exclusion.
        if(RingElements.All(e=>_state.Cards.Values.Any(c=>c.OwnerId==player.Id && InPlay(c) && Definition(c).Type==CardType.Ring && Keyword(c,e) && !Keyword(c,"Fudo"))))
        { Win(player.Id,"Enlightenment victory"); return; }
        player.TurnsTaken++;
        player.MayNotAttackThisTurn=false;
        player.FavorActionsThisTurn=0;
        player.DestroyedEnemyPersonalityActionsThisTurn=0;
        StraightenPhase(player);
    }
    private static readonly string[] RingElements=["Fire","Water","Air","Earth","Void"];
    private void StraightenPhase(PlayerState player)
    {
        SetPhase(Phase.Straighten,player.Id);
        // A card with the choose-straighten effect lets its controller decide, per turn, whether to
        // straighten it instead of that being automatic or a fixed forced-skip condition.
        if(EligibleForOptionalStraighten(player.Id).Length>0)
        {
            _state.PendingChoice=new("declineStraighten",player.Id,null,"Choose which cards to leave bowed instead of straightening.");
            return;
        }
        FinishStraightening(player,[]);
    }
    private string[] EligibleForOptionalStraighten(int playerId)=>_state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Bowed && c.Zone is Zone.Home or Zone.Stronghold or Zone.Attached
        && Definition(c).Effect=="choose-straighten" && c.StraightenBlockedTurn!=_state.TurnNumber).Select(c=>c.Id).ToArray();
    private void ResolveDeclineStraighten(GameCommand command)
    {
        var pending=_state.PendingChoice!;
        Require(command.Type=="declineStraighten" && command.PlayerId==pending.PlayerId,"Choose which cards to leave bowed instead of straightening.");
        var eligible=EligibleForOptionalStraighten(pending.PlayerId).ToHashSet(StringComparer.Ordinal);
        var declined=(command.CardIds??[]).Distinct(StringComparer.Ordinal).ToArray();
        Require(declined.All(eligible.Contains),"Only cards with the choose-straighten ability can be left bowed here.");
        _state.PendingChoice=null;
        FinishStraightening(Player(pending.PlayerId),declined);
    }
    private void FinishStraightening(PlayerState player,string[] declinedIds)
    {
        foreach(var card in _state.Cards.Values.Where(c=>c.OwnerId==player.Id && c.Zone is Zone.Home or Zone.Stronghold or Zone.Attached))
        {
            if(declinedIds.Contains(card.Id)) Emit("straightenDeclined",$"{Definition(card).Name} was left bowed by choice.",card.OwnerId,card.Id);
            else Straighten(card);
        }
        Emit("straightened",$"{player.Name}'s turn: eligible cards straightened.",player.Id);
        EventsPhase(player);
    }
    private void EventsPhase(PlayerState player)
    {
        SetPhase(Phase.Events,player.Id);
        // No card in the current published pool triggers an Events-phase decision; the phase still
        // exists in turn order and the log for future Event/Celestial/Region cards to hook into.
        foreach(var province in player.Provinces.Where(p=>!p.Destroyed && p.CardId is not null))
        {
            var card=Card(province.CardId);
            if(!card.FaceUp) { card.FaceUp=true; Emit("provinceRevealed",$"{player.Name} revealed {Definition(card).Name} in province {province.Index+1}.",player.Id,card.Id); }
        }
        SetPhase(Phase.Action,player.Id);
    }
    private void Draw(PlayerState player)
    {
        if(player.FateDeck.Count==0) { Emit("deckEmpty",$"{player.Name}'s Fate deck is empty; no card drawn.",player.Id); return; }
        var id=player.FateDeck[0]; player.FateDeck.RemoveAt(0); player.Hand.Add(id);
        Card(id).Zone=Zone.Hand;
        Emit("cardDrawn",$"{player.Name} drew a Fate card.",player.Id); // Deliberately excludes hidden identity.
    }
    private void Refill(PlayerState player,ProvinceState province)
    {
        if(province.Destroyed || province.CardId is not null || player.DynastyDeck.Count==0) return;
        var id=player.DynastyDeck[0]; player.DynastyDeck.RemoveAt(0);
        province.CardId=id; var card=Card(id); card.Zone=Zone.Province; card.ProvinceIndex=province.Index; card.FaceUp=false;
        Emit("provinceRefilled",$"{player.Name} refilled province {province.Index+1} face down.",player.Id); // Neither player can inspect it.
    }
    // Honor loss/gain is permanent by default, with no floor or ceiling and no memory of past
    // changes -- but a standing passive (a Stronghold's printed text, keyed by its Effect below) can
    // negate a loss outright when its source is a card the losing player owns and matching Clan/Fate
    // criteria (core rules Glossary). Only ever checked against the LOSING player's own Stronghold:
    // "cards you own" never reaches an opponent's passive. Only losses are covered so far -- no
    // published or training card yet negates a gain, but nothing here assumes losses are the only
    // negatable direction if one shows up.
    private static readonly Dictionary<string,(string? Clan,bool Fate)> HonorLossImmunity = new(StringComparer.Ordinal)
    {
        ["training-honor-immunity"] = ("Spider", true),
    };
    private static bool IsFateCardType(CardType type) => type is CardType.Strategy or CardType.Follower or CardType.Item or CardType.Spell;
    private bool HonorLossNegated(int playerId, CardInstance? source)
    {
        if(source is null || source.OwnerId != playerId) return false;
        var stronghold = Definition(Card(Player(playerId).StrongholdId)).Effect;
        if(!HonorLossImmunity.TryGetValue(stronghold, out var rule)) return false;
        var def = Definition(source);
        return rule.Clan == def.Clan || (rule.Fate && IsFateCardType(def.Type));
    }
    private void GainHonor(int playerId,int amount,string reason,CardInstance? source=null)
    {
        if(amount==0) return;
        if(amount<0 && HonorLossNegated(playerId,source))
        {
            Emit("honorLossNegated",$"{Player(playerId).Name}'s Honor loss from {reason} was negated.",playerId,source?.Id);
            return;
        }
        var player=Player(playerId); player.Honor+=amount;
        if(player.Honor<=-20) player.DishonorLossPending=true;
        Emit("honorChanged",$"{player.Name}: {amount:+#;-#;0} Honor ({reason}).",playerId,Amount:amount);
    }
    // Dishonor/Rehonor (core rules Glossary, issue #186): "if one or more personalities are the
    // performer of an action that would grant its controller Honor, the gain is negated and the
    // performer is restored to honorable status" instead -- e.g. Doji Shunya's own ability, Shamate
    // Keep's chosen performer. Distinct from a card's own explicit "rehonor him" text (e.g. No Time
    // For Games), which just calls Rehonor directly; this is the generic derived version for an
    // ability that does not spell it out itself.
    private void GainHonorFromPerformer(CardInstance performer,int amount,string reason)
    {
        if(performer.Dishonorable) { Rehonor(performer); return; }
        GainHonor(performer.OwnerId,amount,reason,performer);
    }
    private void Win(int winner,string reason) { _state.WinnerId=winner; _state.VictoryReason=reason; _state.Phase=Phase.Finished; _state.PendingChoice=null; Emit("matchFinished",$"{Player(winner).Name} wins: {reason}.",winner); }
    private void SetPhase(Phase phase,int priority) { _state.Phase=phase; _state.PriorityPlayerId=priority; _state.ConsecutivePasses=0; Emit("phaseChanged",$"Phase: {phase}."); }
    private void Emit(string type,string message,int? playerId=null,string? cardId=null,int? Amount=null) => _state.Events.Add(new(_state.Events.Count+1,type,message,playerId,cardId,Amount));
    private PlayerState Player(int id) => _state.Players[id];
    private CardInstance Card(string? id) { Require(id is not null && _state.Cards.ContainsKey(id),"Unknown card."); return _state.Cards[id!]; }
    private static CardDefinition Definition(CardInstance card) => CardCatalog.Get(card.DefinitionId);
    private static void Require(bool condition,string message) { if(!condition) throw new RuleViolation(message); }
    private sealed class RuleViolation(string message):Exception(message);
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private sealed class StableRandom(ulong seed)
    {
        private ulong _value=seed;
        private ulong Next()
        {
            // SplitMix64; owned algorithm makes shuffles stable across .NET versions.
            var z=(_value+=0x9E3779B97F4A7C15UL);
            z=(z^(z>>30))*0xBF58476D1CE4E5B9UL; z=(z^(z>>27))*0x94D049BB133111EBUL;
            return z^(z>>31);
        }
        public void Shuffle<T>(IList<T> cards)
        {
            for(var i=cards.Count-1;i>0;i--) { var j=(int)(Next()%(ulong)(i+1)); (cards[i],cards[j])=(cards[j],cards[i]); }
        }
    }
}
