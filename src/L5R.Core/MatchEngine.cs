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
        if (scenario == PublishedContent.Scenario) return CreatePublished(seed);
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
            case "useAbility": UseAbility(command); break;
            default: throw new RuleViolation("Unsupported command.");
        }
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
        if(Definition(card).Type==CardType.Personality && Definition(card).Effect=="enters-dishonored") card.Dishonorable=true;
        Emit("cardEnteredPlay",$"{Player(command.PlayerId).Name} brought {Definition(card).Name} into play{(card.Bowed ? " bowed" : "") }.",command.PlayerId,card.Id);
        Refill(Player(command.PlayerId),province);
        var hasEntryReaction=(Definition(card).Abilities??[]).Any(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger=="own-enters-play");
        if (quote.ProclaimEligible)
        {
            _state.PendingChoice=new("proclaim",command.PlayerId,card.Id,"After entry: Proclaim for Honor"+(hasEntryReaction?", use this card's own reaction,":"")+" or pass. Each may be used once.");
            _state.ConsecutivePasses=0;
            Emit("reactionWindow","Proclaim reaction window opened.",command.PlayerId,card.Id);
        }
        else if (hasEntryReaction)
        {
            _state.PendingChoice=new("entryReaction",command.PlayerId,card.Id,"After entry: use this card's own reaction, or pass.");
            _state.ConsecutivePasses=0;
            Emit("reactionWindow","Entry reaction window opened.",command.PlayerId,card.Id);
        }
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
        var inClan=def.Type==CardType.Personality && def.Clan==player.Clan;
        if(card.OwnerId!=playerId || card.Zone!=Zone.Province || !card.FaceUp) error="Choose your face-up province card.";
        else if(def.Type is not (CardType.Personality or CardType.Holding)) error="This card cannot be purchased.";
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
        _state.ConsecutivePasses++;
        if(_state.ConsecutivePasses==2)
        {
            _state.PendingChoice=null;
            _state.ConsecutivePasses=0;
            _state.PriorityPlayerId=_state.ActivePlayerId;
            Emit("reactionClosed","Both players passed; the reaction window closed.");
        }
        else _state.PriorityPlayerId=1-_state.PriorityPlayerId;
    }

    private void Pass()
    {
        switch(_state.Phase)
        {
            case Phase.Action:
                if(++_state.ConsecutivePasses==2) SetPhase(Phase.AttackDeclaration,_state.ActivePlayerId);
                else _state.PriorityPlayerId=1-_state.PriorityPlayerId;
                break;
            case Phase.AttackDeclaration: SetPhase(Phase.Dynasty,_state.ActivePlayerId); break;
            case Phase.DefenseAssignment:
            case Phase.CavalryDefense:
                AssignDefense(new("assignDefense",_state.PriorityPlayerId,_state.Version,Assignments:[]));
                break;
            // Unlike a pass in AttackDeclaration (which abandons the attack outright), passing here
            // only declines further Cavalry assignments -- Infantry Maneuvers already committed.
            case Phase.CavalryDeclaration: DeclareAttack(new("attack",_state.PriorityPlayerId,_state.Version,Assignments:[])); break;
            case Phase.Battle:
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
    // (CavalryDeclaration/CavalryDefense) follows, using the same battlefields Infantry Maneuvers
    // already fixed -- every non-destroyed enemy province, set once by the Infantry attacker step.
    private void DeclareAttack(GameCommand command)
    {
        Require(_state.Phase is Phase.AttackDeclaration or Phase.CavalryDeclaration,"Declare an attack after the Action Phase.");
        var cavalry=_state.Phase==Phase.CavalryDeclaration;
        var targets=Player(1-command.PlayerId).Provinces.Where(p=>!p.Destroyed).Select(p=>p.Index).ToHashSet();
        Assign(command,targets,cavalry);
        if(!cavalry) _state.UnresolvedBattlefields=targets.Order().ToList();
        Emit("attackDeclared",$"{Player(command.PlayerId).Name} declared {(cavalry?"a Cavalry":"an Infantry")} attack.",command.PlayerId);
        SetPhase(cavalry?Phase.CavalryDefense:Phase.DefenseAssignment,1-command.PlayerId);
    }
    private void AssignDefense(GameCommand command)
    {
        Require(_state.Phase is Phase.DefenseAssignment or Phase.CavalryDefense,"Defenders assign after the attacking army.");
        var cavalry=_state.Phase==Phase.CavalryDefense;
        Assign(command,_state.UnresolvedBattlefields.ToHashSet(),cavalry);
        SetPhase(cavalry?Phase.BattleSelection:Phase.CavalryDeclaration,_state.ActivePlayerId);
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
            GainHonor(attacker,destroyedDefenders*2,"Battle victory");
            var province=Player(defender).Provinces.Single(p=>p.Index==provinceIndex);
            var destroyedProvince=af>df+province.Strength;
            if(destroyedProvince) DestroyProvince(defender,province);
            if(destroyedDefenders>0 || destroyedProvince) AttackerDestructionBonus(attacker,a);
        }
        else if(df>af) GainHonor(defender,DestroyUnits(a)*2,"Battle victory");
        else if(a.Length>0 && d.Length>0)
        {
            var destroyedA=DestroyUnits(a); var destroyedD=DestroyUnits(d);
            GainHonor(attacker,destroyedD,"Tied battle"); GainHonor(defender,destroyedA,"Tied battle");
            if(destroyedD>0) AttackerDestructionBonus(attacker,a);
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
        _state.UnresolvedBattlefields.Remove(provinceIndex); _state.CurrentBattleProvince=null;
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

    private void Equip(GameCommand command)
    {
        Require(_state.Phase==Phase.Action && command.PlayerId==_state.ActivePlayerId,"Equip is a Limited action on your turn.");
        var card=Card(command.CardId); var target=Card(command.TargetId); var def=Definition(card);
        Require(card.OwnerId==command.PlayerId && card.Zone==Zone.Hand && def.Type is CardType.Item or CardType.Follower,"Choose a supported attachment from your hand.");
        Require(target.OwnerId==command.PlayerId && target.Zone==Zone.Home && Definition(target).Type==CardType.Personality,"Choose your Personality at home.");
        Require(def.Type!=CardType.Follower || def.HonorRequirement is null || Definition(target).PersonalHonor>=def.HonorRequirement,"The Personality does not meet this Follower's Personal Honor Requirement.");
        Require(AttachmentAllowed(card,target),"A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
        Pay(command.PlayerId,card,def.GoldCost,command.SourceIds);
        Player(command.PlayerId).Hand.Remove(card.Id);
        card.Zone=Zone.Attached; card.AttachedTo=target.Id; card.FaceUp=true;
        Emit("equipped",$"{def.Name} attached to {Definition(target).Name}.",command.PlayerId,card.Id);
        NextAction(command.PlayerId);
    }
    private void PlayStrategy(GameCommand command)
    {
        Require(_state.Phase==Phase.Battle,"This Strategy can only be played during a battle.");
        var card=Card(command.CardId); var target=Card(command.TargetId); var def=Definition(card);
        Require(card.OwnerId==command.PlayerId && card.Zone==Zone.Hand && def.Type==CardType.Strategy,"Choose a supported Strategy from your hand.");
        Require(Army(command.PlayerId,_state.CurrentBattleProvince!.Value).Any(),"You need a unit at the current battlefield to take a Battle action.");
        Require(target.Zone==Zone.Battlefield && target.ProvinceIndex==_state.CurrentBattleProvince && Definition(target).Type==CardType.Personality,"Target a Personality at the current battlefield.");
        switch(def.Effect)
        {
            case "force-three": Require(target.OwnerId==command.PlayerId,"Training rally targets your Personality."); ApplyModifier(target,card.Id,def.Effect,"Force",3); break;
            case "bow-small": Require(target.OwnerId!=command.PlayerId && PersonalityForce(target)<=3,"Training pressure targets an enemy Personality with 3 or less Force."); target.Bowed=true; break;
            default: throw new RuleViolation("This card effect is not implemented.");
        }
        Player(command.PlayerId).Hand.Remove(card.Id); card.Zone=Zone.FateDiscard; card.FaceUp=true;
        Emit("strategyPlayed",$"{Player(command.PlayerId).Name} played {def.Name} targeting {Definition(target).Name}.",command.PlayerId,card.Id);
        NextAction(command.PlayerId);
    }

    private void NextAction(int playerId) { _state.ConsecutivePasses=0; _state.PriorityPlayerId=1-playerId; }
    private IEnumerable<CardInstance> Army(int playerId,int province) => _state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone==Zone.Battlefield && c.ProvinceIndex==province && Definition(c).Type==CardType.Personality);
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
    // "force-with-spell" (Asako Ume), "force-if-enemy-dishonorable" (Bayushi Kahoku) and
    // "force-max-dead-pile" (Moto Shunsuke) are always-on passive bonuses, unlike the attacking-only
    // attacking-two/attacking-four Effects above.
    private int PersonalityForce(CardInstance card) => Math.Max(0,Definition(card).Force+StatModifier(card,"Force")+(IsAttacking(card)?(Definition(card).Effect=="attacking-two"?2:Definition(card).Effect=="attacking-four"?4:0)-(HasHalls(card.OwnerId)&&Keyword(card,"Ancestor")?1:0):0)+Attachments(card.Id).Where(c=>Definition(c).Type==CardType.Item).Sum(c=>Definition(c).Force)
        +(Definition(card).Effect=="force-with-spell"&&Attachments(card.Id).Any(c=>Definition(c).Type==CardType.Spell)?1:0)
        +(Definition(card).Effect=="force-if-enemy-dishonorable"&&_state.Cards.Values.Any(c=>c.OwnerId!=card.OwnerId&&Definition(c).Type==CardType.Personality&&InPlay(c)&&c.Dishonorable)?2:0)
        // "The highest number of dead Personalities in any one discard pile" -- the greatest per-player
        // count of Zone.Dead Personalities, i.e. the largest single pile, not the combined total.
        +(Definition(card).Effect=="force-max-dead-pile"?_state.Cards.Values.Where(c=>c.Zone==Zone.Dead&&Definition(c).Type==CardType.Personality).GroupBy(c=>c.OwnerId).Select(g=>g.Count()).DefaultIfEmpty(0).Max():0));
    // A bowed ordinary Personality suppresses its entire unit at resolution.
    // Preserve the explicit Elite-Personality exception accepted in #19/#17.
    // An Elite Follower cannot bypass its non-Elite Personality's bowed state.
    private int BattleForce(CardInstance card)
    {
        if(card.Bowed && !Keyword(card,"Elite")) return 0;
        return PersonalityForce(card)+Attachments(card.Id)
            .Where(c=>(!c.Bowed || Keyword(c,"Elite")) && Definition(c).Type==CardType.Follower)
            .Sum(c=>Math.Max(0,Definition(c).Force+StatModifier(c,"Force")));
    }
    // "Dead / Destroy" (core rules Glossary): entering the Dead zone, distinct from merely being
    // discarded. A Personality who is still Dishonorable at this moment costs its controller Honor
    // equal to his base Personal Honor -- Seppuku (and any future rehonor-before-death effect) spares
    // this by clearing Dishonorable first (see Rehonor). This is the one shared entry point every
    // path to "becoming dead" -- combat destruction or otherwise -- must go through.
    private void MarkDead(CardInstance card)
    {
        card.Zone=Zone.Dead; card.FaceUp=true; card.Tokens.Clear();
        if(Definition(card).Type==CardType.Personality && card.Dishonorable) GainHonor(card.OwnerId,-Definition(card).PersonalHonor,"Dishonorable death");
    }
    private void Rehonor(CardInstance card) => card.Dishonorable=false;
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
            foreach(var attachment in Attachments(card.Id).ToArray()) { attachment.Zone=Zone.FateDiscard; attachment.AttachedTo=null; attachment.ProvinceIndex=null; attachment.Tokens.Clear(); count++; }
            card.ProvinceIndex=null;
            MarkDead(card);
            count++;
            if(combatDestruction && _state.Phase==Phase.Battle && HasHalls(card.OwnerId) && Keyword(card,"Ancestor")) GainHonor(card.OwnerId,1,"Halls of Memory — Combat Segment destruction");
            Emit("unitDestroyed",$"{Definition(card).Name} and its attachments were destroyed.",card.OwnerId,card.Id);
        }
        return count;
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
        player.TurnsTaken++;
        StraightenPhase(player);
    }
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
    private void GainHonor(int playerId,int amount,string reason)
    {
        if(amount==0) return;
        var player=Player(playerId); player.Honor+=amount;
        if(player.Honor<=-20) player.DishonorLossPending=true;
        Emit("honorChanged",$"{player.Name}: {amount:+#;-#;0} Honor ({reason}).",playerId,Amount:amount);
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
