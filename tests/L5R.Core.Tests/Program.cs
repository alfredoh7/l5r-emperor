using System.Text.Json;
using L5R.Core;

var tests = new (string Name, Action Run)[]
{
    ("Starter setup uses four provinces, six-card hands and second-player bowed holding", Setup),
    ("Player snapshots hide enemy hand, all deck order and face-down own provinces", Privacy),
    ("Invalid, duplicate, stale and wrong-priority commands are atomic", AtomicReject),
    ("Exact Gold payment bows only selected sources", ExactPayment),
    ("Excess Gold is discarded and cannot fund another purchase", Overproduction),
    ("Marketplace production is conditional on Crane clan", ConditionalProduction),
    ("Clan discount does not gain Honor or offer Proclaim", Discount),
    ("Clan allegiance ignores requirement and combines with discount", Allegiance),
    ("Proclaim happens after entry and is limited to once per turn", Proclaim),
    ("Declining Proclaim keeps its once-per-turn use available", Decline),
    ("New Holding enters bowed and province refills face down immediately", HoldingAndRefill),
    ("Province discarding prevents returning to purchases", DynastyDiscard),
    ("Action phase needs consecutive passes and defender starts battle", Priority),
    ("Bamboo fixture waits until owner's second turn to straighten", Bamboo),
    ("Battle destroys province only on strictly greater margin", ProvinceThreshold),
    ("Tied armies destroy each other and gain one Honor per opposing card", BattleTie),
    ("Bowed Personality's unbowed Followers still count in army Force", FollowerForce),
    ("Bowed Elite Followers still count in unit Force; plain bowed Followers do not", EliteFollowerForce),
    ("A bowed Elite Personality's own Force still counts toward its unit", ElitePersonalityForce),
    ("Turn-duration granted keywords clear at turn end; Permanent grants do not", GrantedKeywordDuration),
    ("A Personality with the enters-dishonored effect enters play Dishonorable", EntersPlayDishonored),
    ("A Personality without an enters-bowed effect always enters play straightened", EntersPlayStraightenedByDefault),
    ("A Personality with the enters-bowed effect enters play bowed directly, not straightened then bowed", EntersPlayBowed),
    ("Seppuku requires Samurai, Courtier, or Shugenja", OrderSeppukuRequiresKeyword),
    ("Seppuku requires a Dishonorable Personality", OrderSeppukuRequiresDishonorable),
    ("Seppuku destroys honorably with no Honor change", OrderSeppukuHonorableDeath),
    ("Seppuku is an Open action, available without priority", OrderSeppukuIsOpenAction),
    ("Seppuku cannot be ordered for an opponent's Personality", CannotOrderSeppukuForOpponentsPersonality),
    ("A Dishonorable Personality's death (other than Seppuku) costs its controller Honor", DishonorableDeathLosesHonor),
    ("Battle winner gains two Honor per destroyed card including attachments", BattleHonor),
    ("Successful attacking units and attachments return bowed; defenders stay", BattleReturn),
    ("Last province destruction immediately wins military victory", MilitaryVictory),
    ("Honor victory waits for beginning of owner's turn", HonorVictory),
    ("Latched dishonor defeat resolves before end-phase draw", DishonorTiming),
    ("End phase requires exact hand-limit discard", HandLimit),
    ("Battle Strategies require presence and valid local targets", StrategyLegality),
    ("Equip validates Personal Honor, payment and action priority", Equip),
    ("Temporary Force expires at turn end", ExpiringForce),
    ("Same seed and accepted command replay reproduce complete state", Replay),
    ("Different seeds change hidden deck order", DifferentSeeds),
    ("Unsupported definitions and replay versions fail explicitly", Unsupported),
    ("Public snapshots and diagnostic state cannot mutate engine", Detached),
    ("Empty decks do not remove provinces or create cards", EmptyDecks),
    ("Every generated enabled exact command validates", ActionQueries),
    ("Invalid later assignment rolls back earlier valid assignment", AssignmentRollback),
    ("Zero-Force battle with an empty side has no outcome", EmptyBattle),
    ("Proclaim use becomes available on a subsequent turn", ProclaimReset),
    ("Scripted training match completes with replay and invariant checks", CompleteMatch)
};
var failed=0;
foreach(var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch(Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}\n{ex}"); }
}
Console.WriteLine($"{tests.Length-failed}/{tests.Length} tests passed.");
failed += PublishedTests.Run();
failed += DeckFormatTests.Run();
return failed==0?0:1;

static void Check(bool ok,string message="Assertion failed") { if(!ok) throw new Exception(message); }
static string Json<T>(T value)=>JsonSerializer.Serialize(value);
static void Equal<T>(T actual,T expected,string message="Values differ")=>Check(EqualityComparer<T>.Default.Equals(actual,expected),$"{message}: expected {expected}; got {actual}");
static GameCommand Cmd(MatchEngine e,string type,int? player=null,string? id=null,string[]? sources=null,int? province=null,bool discount=false,bool allegiance=false,Dictionary<string,int>? assignments=null,string[]? cards=null,string? target=null)
    =>new(type,player??e.State.PriorityPlayerId,e.Version,id,sources,province,discount,allegiance,assignments,cards,target);
static void Accept(MatchEngine e,GameCommand c) { var result=e.Execute(c); Check(result.Accepted,result.Error??"Rejected"); }
static void Reject(MatchEngine e,GameCommand c) { var before=Json(e.State); Check(!e.Execute(c).Accepted,"Should reject"); Equal(Json(e.State),before,"Rejection mutated state"); }
static CardInstance Find(MatchEngine e,string definition,int owner=0,Zone? zone=null)=>e.State.Cards.Values.First(c=>c.DefinitionId==definition && c.OwnerId==owner && (zone is null || c.Zone==zone));
static string[] Sources(MatchEngine e,int owner=0)=>e.State.Cards.Values.Where(c=>c.OwnerId==owner && c.Zone is Zone.Home or Zone.Stronghold && !c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction>0).Select(c=>c.Id).ToArray();
static string ProvinceCard(MatchEngine e,int index=0,int owner=0)=>e.State.Players[owner].Provinces[index].CardId!;
static void SkipToDynasty(MatchEngine e) { while(e.State.Phase!=Phase.Dynasty) Accept(e,Cmd(e,"pass")); }
static void CloseReaction(MatchEngine e) { while(e.State.PendingChoice?.Type=="proclaim") Accept(e,Cmd(e,"declineReaction")); }
static CardInstance Add(MatchState s,string def,int owner,Zone zone,bool bowed=false,int? province=null,string? attachedTo=null)
{
    var c=new CardInstance { Id=$"test-{s.Cards.Count}",DefinitionId=def,OwnerId=owner,Zone=zone,FaceUp=zone!=Zone.Hand,Bowed=bowed,ProvinceIndex=province,AttachedTo=attachedTo };
    s.Cards.Add(c.Id,c);
    if(zone==Zone.Hand) s.Players[owner].Hand.Add(c.Id);
    return c;
}
static MatchEngine Battle(int attackForce=5,int defenseForce=2,int provinceStrength=6,bool attackBowed=false)
{
    var s=MatchEngine.CreateEconomyScenario().State;
    s.Phase=Phase.Battle; s.CurrentBattleProvince=0; s.UnresolvedBattlefields=[0]; s.PriorityPlayerId=1;
    s.Players[1].Provinces[0].Strength=provinceStrength;
    var a=Add(s,"training-crane-veteran",0,Zone.Battlefield,attackBowed,0); a.Modifiers=[new("test-fixture","test-fixture","Force",attackForce-5)];
    var d=Add(s,"training-lion-veteran",1,Zone.Battlefield,false,0); d.Modifiers=[new("test-fixture","test-fixture","Force",defenseForce-6)];
    return MatchEngine.FromTestState(s);
}
static void Resolve(MatchEngine e) { Accept(e,Cmd(e,"pass")); Accept(e,Cmd(e,"pass")); }

static void Setup()
{
    var e=MatchEngine.CreateStarterMatch(); var s=e.State;
    Equal(s.Phase,Phase.Action); Equal(s.ActivePlayerId,0); Equal(s.Version,0L);
    foreach(var p in s.Players) { Equal(p.Provinces.Count,4); Equal(p.Hand.Count,6); Equal(p.DynastyDeck.Count,36); }
    Check(Find(e,"training-bamboo",1).Bowed); Check(!s.Cards.Values.Any(c=>c.OwnerId==0 && c.DefinitionId=="training-bamboo"));
    Check(s.Players[0].Provinces.All(p=>s.Cards[p.CardId!].FaceUp));
    Check(s.Players[1].Provinces.All(p=>!s.Cards[p.CardId!].FaceUp));
}
static void Privacy()
{
    var e=MatchEngine.CreateStarterMatch(); var view=e.GetView(1); var s=e.State;
    Equal(view.Players[0].Hand.Length,0); Equal(view.Players[0].HandCount,6); Equal(view.Players[1].Hand.Length,6);
    foreach(var p in view.Players[1].Provinces) { Check(p.Card!.Definition is null); Check(p.Card.Id.StartsWith("hidden-")); }
    var json=Json(view);
    foreach(var id in s.Players[0].Hand.Concat(s.Players[0].FateDeck).Concat(s.Players[1].DynastyDeck)) Check(!json.Contains($"\"{id}\""),"Hidden card id leaked");
    var quote=e.QuotePurchase(1,ProvinceCard(e,owner:1)); Equal(quote.Cost,0); Check(quote.Error is not null);
    Check(!json.Contains("Seed"));
}
static void AtomicReject()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=ProvinceCard(e); var sources=Sources(e);
    Reject(e,Cmd(e,"purchase",player:1,id:id,sources:sources));
    Reject(e,Cmd(e,"purchase",id:id,sources:[sources[0],sources[0]]));
    Reject(e,Cmd(e,"purchase",id:id,sources:[]));
    Reject(e,new("pass",0,99)); Reject(e,new("pass",44,0)); Reject(e,Cmd(e,"unimplemented"));
    var c=Cmd(e,"purchase",id:id,sources:e.QuotePurchase(0,id).Payments[0].SourceIds);
    Accept(e,c); Reject(e,c);
}
static void ExactPayment()
{
    var e=MatchEngine.CreateEconomyScenario(); var p=e.QuotePurchase(0,ProvinceCard(e)).Payments.First(p=>p.Excess==0);
    var unused=Sources(e).Except(p.SourceIds).ToArray();
    Accept(e,Cmd(e,"purchase",id:ProvinceCard(e),sources:p.SourceIds));
    Check(p.SourceIds.All(id=>e.State.Cards[id].Bowed)); Check(unused.All(id=>!e.State.Cards[id].Bowed));
    Equal(e.State.Players[0].Honor,8);
}
static void Overproduction()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=ProvinceCard(e,1);
    var p=e.QuotePurchase(0,id).Payments.First(); Equal(p.Cost,8); Equal(p.Produced,9); Equal(p.Excess,1);
    Accept(e,Cmd(e,"purchase",id:id,sources:p.SourceIds)); CloseReaction(e);
    var holding=ProvinceCard(e,2); Equal(e.QuotePurchase(0,holding).Payments.Length,0);
    Reject(e,Cmd(e,"purchase",id:holding,sources:[]));
}
static void ConditionalProduction()
{
    var e=MatchEngine.CreateEconomyScenario(); var s=e.State; var crane=Find(e,"training-marketplace",zone:Zone.Home);
    var lion=Add(s,"training-marketplace",1,Zone.Home); var x=MatchEngine.FromTestState(s);
    Equal(x.GoldProduced(crane.Id,0),3); Equal(x.GoldProduced(lion.Id,1),2);
}
static void Discount()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=ProvinceCard(e); var q=e.QuotePurchase(0,id,true);
    Equal(q.Cost,4); Check(!q.ProclaimEligible);
    Accept(e,Cmd(e,"purchase",id:id,sources:q.Payments[0].SourceIds,discount:true));
    Equal(e.State.Players[0].Honor,8); Check(e.State.PendingChoice is null); Reject(e,Cmd(e,"proclaim"));
}
static void Allegiance()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=ProvinceCard(e,3);
    Check(e.QuotePurchase(0,id).Error is not null);
    var q=e.QuotePurchase(0,id,true,true); Equal(q.Cost,6); Check(q.Error is null); Check(!q.ProclaimEligible);
    Accept(e,Cmd(e,"purchase",id:id,sources:q.Payments[0].SourceIds,discount:true,allegiance:true));
    var other=MatchEngine.CreateEconomyScenario(); var q2=other.QuotePurchase(0,ProvinceCard(other,3),false,true);
    Equal(q2.Cost,8); Check(q2.ProclaimEligible);
}
static void Proclaim()
{
    var s=MatchEngine.CreateEconomyScenario().State;
    for(var i=0;i<6;i++) Add(s,"training-farm",0,Zone.Home);
    var e=MatchEngine.FromTestState(s); var id=ProvinceCard(e);
    Accept(e,Cmd(e,"purchase",id:id,sources:e.QuotePurchase(0,id).Payments[0].SourceIds));
    Equal(e.State.Cards[id].Zone,Zone.Home); Equal(e.State.Players[0].Honor,8); Check(e.State.PendingChoice is not null);
    Reject(e,Cmd(e,"endDynasty")); Accept(e,Cmd(e,"proclaim")); Equal(e.State.Players[0].Honor,11);
    Accept(e,Cmd(e,"declineReaction")); Reject(e,Cmd(e,"proclaim")); Accept(e,Cmd(e,"declineReaction"));
    var next=ProvinceCard(e,1); Check(!e.QuotePurchase(0,next).ProclaimEligible);
    Accept(e,Cmd(e,"purchase",id:next,sources:e.QuotePurchase(0,next).Payments[0].SourceIds));
    Check(e.State.PendingChoice is null); Equal(e.State.Players[0].Honor,11);
}
static void Decline()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=ProvinceCard(e);
    Accept(e,Cmd(e,"purchase",id:id,sources:e.QuotePurchase(0,id).Payments[0].SourceIds)); CloseReaction(e);
    Equal(e.State.Players[0].ProclaimUsedTurn,-1); Equal(e.State.Players[0].Honor,8);
}
static void HoldingAndRefill()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=ProvinceCard(e,2); var before=e.State.Players[0].DynastyDeck.Count;
    Accept(e,Cmd(e,"purchase",id:id,sources:e.QuotePurchase(0,id).Payments[0].SourceIds));
    Check(e.State.Cards[id].Bowed); Equal(e.State.Cards[id].Zone,Zone.Home); Equal(e.State.Players[0].DynastyDeck.Count,before-1);
    var replacement=ProvinceCard(e,2); Check(replacement!=id); Check(!e.State.Cards[replacement].FaceUp);
    Check(e.GetView(0).Players[0].Provinces[2].Card!.Definition is null);
}
static void DynastyDiscard()
{
    var e=MatchEngine.CreateEconomyScenario(); Accept(e,Cmd(e,"discardProvince",id:ProvinceCard(e,2)));
    Equal(e.State.Phase,Phase.DynastyDiscard); Reject(e,Cmd(e,"purchase",id:ProvinceCard(e),sources:Sources(e)));
    Reject(e,Cmd(e,"discardProvince",id:ProvinceCard(e,2)));
}
static void Priority()
{
    var e=MatchEngine.CreateStarterMatch(); Accept(e,Cmd(e,"pass")); Equal(e.State.Phase,Phase.Action); Equal(e.State.PriorityPlayerId,1);
    Accept(e,Cmd(e,"pass")); Equal(e.State.Phase,Phase.AttackDeclaration);
    Accept(e,Cmd(e,"attack",assignments:[])); Equal(e.State.PriorityPlayerId,1);
    Accept(e,Cmd(e,"assignDefense",assignments:[])); Equal(e.State.PriorityPlayerId,0);
    Accept(e,Cmd(e,"selectBattle",province:0)); Equal(e.State.PriorityPlayerId,1);
}
static void Bamboo()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=Find(e,"training-bamboo",1).Id;
    Accept(e,Cmd(e,"endDynasty")); Check(e.State.Cards[id].Bowed);
    SkipToDynasty(e); Accept(e,Cmd(e,"endDynasty")); SkipToDynasty(e); Accept(e,Cmd(e,"endDynasty"));
    Check(!e.State.Cards[id].Bowed); Equal(e.State.Players[1].TurnsTaken,2);
}
static void ProvinceThreshold()
{
    var exact=Battle(8,2,6); Resolve(exact); Check(!exact.State.Players[1].Provinces[0].Destroyed);
    var over=Battle(9,2,6); Resolve(over); Check(over.State.Players[1].Provinces[0].Destroyed);
}
static void BattleTie()
{
    var e=Battle(3,3); var a=e.State.Players[0].Honor; var d=e.State.Players[1].Honor; Resolve(e);
    Equal(e.State.Cards.Values.Count(c=>c.Zone==Zone.Dead),2); Equal(e.State.Players[0].Honor,a+1); Equal(e.State.Players[1].Honor,d+1);
}
static void FollowerForce()
{
    var s=Battle(5,1,6,true).State; var personality=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
    Add(s,"training-follower",0,Zone.Attached,attachedTo:personality.Id,province:0);
    var e=MatchEngine.FromTestState(s); Resolve(e); Check(e.State.Cards[personality.Id].Zone!=Zone.Dead);
}
static void EliteFollowerForce()
{
    // Bowed personality contributes nothing; a plain bowed Follower drops out too, but a bowed
    // Elite Follower's printed Force still counts toward its unit.
    var s=Battle(5,0,1,true).State; var personality=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
    Add(s,"training-follower",0,Zone.Attached,bowed:true,attachedTo:personality.Id,province:0);
    var elite=Add(s,"training-follower",0,Zone.Attached,bowed:true,attachedTo:personality.Id,province:0);
    elite.GrantedKeywords=[new("Elite","test-fixture","Permanent")];
    var e=MatchEngine.FromTestState(s); Resolve(e);
    Check(e.State.Players[1].Provinces[0].Destroyed,"Bowed Elite Follower's Force should still count toward the attacking unit");
}
static void ElitePersonalityForce()
{
    // A bowed Personality normally contributes nothing to its unit's Force; Elite is the exception,
    // whether printed or granted, same as for a Follower.
    var s=Battle(9,0,1,true).State; var attacker=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
    attacker.GrantedKeywords=[new("Elite","test-fixture","Permanent")];
    var e=MatchEngine.FromTestState(s); Resolve(e);
    Check(e.State.Players[1].Provinces[0].Destroyed,"A bowed Elite Personality's own Force should still count toward its unit");
}
static void BattleHonor()
{
    var s=Battle(7,2).State; var defender=s.Cards.Values.Single(c=>c.OwnerId==1 && c.Zone==Zone.Battlefield);
    Add(s,"training-item",1,Zone.Attached,attachedTo:defender.Id,province:0);
    var e=MatchEngine.FromTestState(s); Resolve(e); Equal(e.State.Players[0].Honor,12);
}
static void BattleReturn()
{
    var s=Battle(8,2).State; s.UnresolvedBattlefields=[0,1]; var attacker=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
    var follower=Add(s,"training-follower",0,Zone.Attached,attachedTo:attacker.Id,province:0);
    var e=MatchEngine.FromTestState(s); Resolve(e);
    Equal(e.State.Cards[attacker.Id].Zone,Zone.Home); Check(e.State.Cards[attacker.Id].Bowed); Check(e.State.Cards[follower.Id].Bowed);
    Equal(e.State.Phase,Phase.BattleSelection);
    var ds=Battle(2,8).State; ds.UnresolvedBattlefields=[0,1]; var def=ds.Cards.Values.Single(c=>c.OwnerId==1 && c.Zone==Zone.Battlefield);
    var de=MatchEngine.FromTestState(ds); Resolve(de); Equal(de.State.Cards[def.Id].Zone,Zone.Battlefield); Check(!de.State.Cards[def.Id].Bowed);
}
static void MilitaryVictory()
{
    var s=Battle(10,2).State; foreach(var p in s.Players[1].Provinces.Skip(1)) { p.Destroyed=true; p.CardId=null; }
    var e=MatchEngine.FromTestState(s); Resolve(e); Equal(e.State.WinnerId,0); Equal(e.State.Phase,Phase.Finished);
    Reject(e,Cmd(e,"pass"));
}
static void HonorVictory()
{
    var s=MatchEngine.CreateEconomyScenario().State; s.Players[0].Honor=40;
    var e=MatchEngine.FromTestState(s); Accept(e,Cmd(e,"endDynasty")); Check(e.State.WinnerId is null);
    SkipToDynasty(e); Accept(e,Cmd(e,"endDynasty")); Equal(e.State.WinnerId,0);
}
static void DishonorTiming()
{
    var s=MatchEngine.CreateEconomyScenario().State; s.Players[0].DishonorLossPending=true; s.Players[0].Honor=5;
    var count=s.Players[0].Hand.Count; var e=MatchEngine.FromTestState(s); Accept(e,Cmd(e,"endDynasty"));
    Equal(e.State.WinnerId,1); Equal(e.State.Players[0].Hand.Count,count);
}
static void HandLimit()
{
    var s=MatchEngine.CreateEconomyScenario().State;
    Add(s,"training-rally",0,Zone.Hand); Add(s,"training-rally",0,Zone.Hand);
    var e=MatchEngine.FromTestState(s); Accept(e,Cmd(e,"endDynasty")); Equal(e.State.Players[0].Hand.Count,9);
    Equal(e.State.PendingChoice!.Type,"discardHand"); Reject(e,Cmd(e,"discardHand",cards:[]));
    Accept(e,Cmd(e,"discardHand",cards:[e.State.Players[0].Hand[0]])); Equal(e.State.Players[0].Hand.Count,8); Equal(e.State.ActivePlayerId,1);
}
static void StrategyLegality()
{
    var s=Battle(5,2).State; var target=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
    var card=Add(s,"training-pressure",1,Zone.Hand); var e=MatchEngine.FromTestState(s);
    Reject(e,Cmd(e,"playStrategy",id:card.Id,target:target.Id));
    var outside=Add(s,"training-crane-samurai",0,Zone.Home); var rally=Add(s,"training-rally",1,Zone.Hand);
    e=MatchEngine.FromTestState(s); Reject(e,Cmd(e,"playStrategy",id:rally.Id,target:outside.Id));
    var own=s.Cards.Values.Single(c=>c.OwnerId==1 && c.Zone==Zone.Battlefield);
    Accept(e,Cmd(e,"playStrategy",id:rally.Id,target:own.Id)); Equal(e.State.PriorityPlayerId,0);
    Equal(e.State.Cards[rally.Id].Zone,Zone.FateDiscard);
}
static void Equip()
{
    var s=MatchEngine.CreateStarterMatch().State; var p=Add(s,"training-crane-courtier",0,Zone.Home); var item=Add(s,"training-item",0,Zone.Hand);
    var e=MatchEngine.FromTestState(s); var payment=e.PaymentOptions(0,2)[0];
    Accept(e,Cmd(e,"equip",id:item.Id,sources:payment.SourceIds,target:p.Id)); Equal(e.State.Cards[item.Id].Zone,Zone.Attached);
    Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==p.Id).Force,4); Equal(e.State.PriorityPlayerId,1);
    Reject(e,Cmd(e,"equip",id:item.Id,target:p.Id));
}
static void ExpiringForce()
{
    var s=MatchEngine.CreateEconomyScenario().State; var p=Add(s,"training-crane-samurai",0,Zone.Home); p.Modifiers=[new("test-fixture","test-fixture","Force",3)];
    var e=MatchEngine.FromTestState(s); Accept(e,Cmd(e,"endDynasty")); Equal(e.State.Cards[p.Id].Modifiers.Count,0);
}
static void GrantedKeywordDuration()
{
    // Turn-duration grants clear at turn end; Permanent grants do not.
    var s=MatchEngine.CreateEconomyScenario().State; var p=Add(s,"training-crane-samurai",0,Zone.Home);
    p.GrantedKeywords=[new("Elite","test-fixture","Turn"),new("Elite","test-fixture","Permanent")];
    var e=MatchEngine.FromTestState(s); Accept(e,Cmd(e,"endDynasty"));
    Equal(e.State.Cards[p.Id].GrantedKeywords.Count,1); Equal(e.State.Cards[p.Id].GrantedKeywords[0].Duration,"Permanent");
}
static void EntersPlayDishonored()
{
    var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
    var cardId=s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId="training-dishonored-samurai";
    e=MatchEngine.FromTestState(s); var quote=e.QuotePurchase(0,cardId);
    Accept(e,Cmd(e,"purchase",id:cardId,sources:quote.Payments[0].SourceIds));
    Check(e.State.Cards[cardId].Dishonorable,"A Personality with Effect 'enters-dishonored' should enter play Dishonorable");
}
static void EntersPlayStraightenedByDefault()
{
    var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
    var cardId=s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId="training-crane-samurai";
    e=MatchEngine.FromTestState(s); var quote=e.QuotePurchase(0,cardId);
    Accept(e,Cmd(e,"purchase",id:cardId,sources:quote.Payments[0].SourceIds));
    Check(!e.State.Cards[cardId].Bowed,"A Personality without an enters-bowed effect should enter play straightened");
}
static void EntersPlayBowed()
{
    var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
    var cardId=s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId="training-enters-bowed-samurai";
    e=MatchEngine.FromTestState(s); var quote=e.QuotePurchase(0,cardId);
    var result=e.Execute(Cmd(e,"purchase",id:cardId,sources:quote.Payments[0].SourceIds));
    Check(result.Accepted,result.Error??"Rejected");
    Check(e.State.Cards[cardId].Bowed,"A Personality with Effect 'enters-bowed' should enter play bowed");
    var entered=result.Events.Single(ev=>ev.Type=="cardEnteredPlay" && ev.CardId==cardId);
    Check(entered.Message.Contains("bowed"),"Entry should report bowed as part of the same transition");
    Check(!result.Events.Any(ev=>ev.CardId==cardId && ev.Type is "straightened" or "unbowed"),"No separate straighten-then-bow event should exist for this card");
}
static void OrderSeppukuRequiresKeyword()
{
    var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action;
    var veteran=Add(s,"training-crane-veteran",0,Zone.Home); veteran.Dishonorable=true;
    var e=MatchEngine.FromTestState(s);
    Reject(e,Cmd(e,"orderSeppuku",id:veteran.Id));
}
static void OrderSeppukuRequiresDishonorable()
{
    var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action;
    var samurai=Add(s,"training-dishonored-samurai",0,Zone.Home);
    var e=MatchEngine.FromTestState(s);
    Reject(e,Cmd(e,"orderSeppuku",id:samurai.Id));
}
static void OrderSeppukuHonorableDeath()
{
    var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action;
    var samurai=Add(s,"training-dishonored-samurai",0,Zone.Home); samurai.Dishonorable=true;
    var e=MatchEngine.FromTestState(s); var honorBefore=e.State.Players[0].Honor;
    Accept(e,Cmd(e,"orderSeppuku",id:samurai.Id));
    Equal(e.State.Cards[samurai.Id].Zone,Zone.Dead);
    Check(!e.State.Cards[samurai.Id].Dishonorable,"Seppuku should leave the Personality honorable in death");
    Equal(e.State.Players[0].Honor,honorBefore);
}
static void OrderSeppukuIsOpenAction()
{
    // Available regardless of who holds priority.
    var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=1;
    var samurai=Add(s,"training-dishonored-samurai",0,Zone.Home); samurai.Dishonorable=true;
    var e=MatchEngine.FromTestState(s);
    Accept(e,Cmd(e,"orderSeppuku",player:0,id:samurai.Id));
    Equal(e.State.Cards[samurai.Id].Zone,Zone.Dead);
}
static void CannotOrderSeppukuForOpponentsPersonality()
{
    var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action;
    var samurai=Add(s,"training-dishonored-samurai",0,Zone.Home); samurai.Dishonorable=true;
    var e=MatchEngine.FromTestState(s);
    Reject(e,Cmd(e,"orderSeppuku",player:1,id:samurai.Id));
}
static void DishonorableDeathLosesHonor()
{
    var s=Battle(9,2,6).State; var defender=s.Cards.Values.Single(c=>c.OwnerId==1 && c.Zone==Zone.Battlefield);
    defender.Dishonorable=true; // training-lion-veteran: PersonalHonor 3
    var e=MatchEngine.FromTestState(s); Resolve(e);
    Equal(e.State.Players[1].Honor,3);
}
static void Replay()
{
    var e=MatchEngine.CreateEconomyScenario(724); var q=e.QuotePurchase(0,ProvinceCard(e));
    Accept(e,Cmd(e,"purchase",id:q.CardId,sources:q.Payments[0].SourceIds)); Accept(e,Cmd(e,"proclaim")); CloseReaction(e);
    Accept(e,Cmd(e,"endDynasty")); SkipToDynasty(e);
    var replay=JsonSerializer.Deserialize<ReplayRecord>(Json(e.ExportReplay()))!;
    Equal(Json(MatchEngine.Replay(replay).State),Json(e.State));
    Equal(Json(e.State),Json(MatchEngine.Replay(e.ExportReplay()).State));
}
static void DifferentSeeds()=>Check(Json(MatchEngine.CreateStarterMatch(1).State.Players[0].DynastyDeck)!=Json(MatchEngine.CreateStarterMatch(2).State.Players[0].DynastyDeck));
static void Unsupported()
{
    var failed=false; try { CardCatalog.Get("Bayushi Nitoshi"); } catch(ArgumentException) { failed=true; } Check(failed);
    failed=false; try { MatchEngine.Replay(new("future","starter",1,[])); } catch(ArgumentException) { failed=true; } Check(failed);
    Check(CardCatalog.Cards.Values.Where(c=>c.Id.StartsWith("training-")).All(c=>c.ScenarioOnly));
}
static void Detached()
{
    var e=MatchEngine.CreateStarterMatch(); var state=e.State; state.Players[0].Honor=200; state.Cards.Clear();
    Equal(e.State.Players[0].Honor,8); Check(e.State.Cards.Count>0);
    var view=e.GetView(0); view.Players[0].Provinces[0]=new(0,0,true,null); Check(!e.GetView(0).Players[0].Provinces[0].Destroyed);
}
static void EmptyDecks()
{
    var s=MatchEngine.CreateEconomyScenario().State; s.Players[0].DynastyDeck.Clear(); s.Players[0].FateDeck.Clear();
    var e=MatchEngine.FromTestState(s); Accept(e,Cmd(e,"discardProvince",id:ProvinceCard(e))); Check(e.State.Players[0].Provinces[0].CardId is null);
    Check(!e.State.Players[0].Provinces[0].Destroyed); var hand=e.State.Players[0].Hand.Count; Accept(e,Cmd(e,"endDynasty")); Equal(e.State.Players[0].Hand.Count,hand);
}
static void ActionQueries()
{
    foreach(var source in new[] {MatchEngine.CreateEconomyScenario(),MatchEngine.CreateStarterMatch()})
        foreach(var action in source.QueryActions(source.State.PriorityPlayerId).Where(a=>a.Enabled && a.Command is not null))
        {
            var clone=MatchEngine.Replay(source.ExportReplay()); Accept(clone,action.Command!);
        }
}
static void AssignmentRollback()
{
    var s=MatchEngine.CreateStarterMatch().State; s.Phase=Phase.AttackDeclaration;
    var p=Add(s,"training-crane-samurai",0,Zone.Home);
    var e=MatchEngine.FromTestState(s);
    Reject(e,Cmd(e,"attack",assignments:new() { [p.Id]=0,["nonexistent"]=1 }));
    Equal(e.State.Cards[p.Id].Zone,Zone.Home);
}
static void EmptyBattle()
{
    var s=Battle(0,0).State;
    var attacker=s.Cards.Values.Single(c=>c.Zone==Zone.Battlefield && c.OwnerId==0);
    var defender=s.Cards.Values.Single(c=>c.Zone==Zone.Battlefield && c.OwnerId==1);
    defender.Zone=Zone.Home; defender.ProvinceIndex=null;
    var e=MatchEngine.FromTestState(s); Resolve(e);
    Equal(e.State.Cards[attacker.Id].Zone,Zone.Home); Equal(e.State.Players[0].Honor,8); Equal(e.State.Players[1].Honor,6);
}
static void ProclaimReset()
{
    var e=MatchEngine.CreateEconomyScenario(); var id=ProvinceCard(e);
    Accept(e,Cmd(e,"purchase",id:id,sources:e.QuotePurchase(0,id).Payments[0].SourceIds)); Accept(e,Cmd(e,"proclaim")); CloseReaction(e);
    Check(e.GetView(0).Players[0].ProclaimUsedThisTurn); Accept(e,Cmd(e,"endDynasty")); SkipToDynasty(e); Accept(e,Cmd(e,"endDynasty"));
    Check(!e.GetView(0).Players[0].ProclaimUsedThisTurn); SkipToDynasty(e); Check(e.QuotePurchase(0,ProvinceCard(e,1)).ProclaimEligible);
}
static void CompleteMatch()
{
    var e=MatchEngine.CreateStarterMatch(831); var count=0;
    while(e.State.WinnerId is null && count++<2000)
    {
        var s=e.State; var actions=e.QueryActions(s.PriorityPlayerId).Where(a=>a.Enabled && a.Command is not null && a.Type!="concede").ToArray();
        ActionOption? chosen=null;
        if(s.PendingChoice?.Type=="proclaim") chosen=actions.FirstOrDefault(a=>a.Type=="proclaim")??actions.First(a=>a.Type=="declineReaction");
        else if(s.PendingChoice is not null) chosen=actions.First(a=>a.Type=="discardHand");
        else if(s.Phase==Phase.Dynasty)
        {
            chosen=actions.FirstOrDefault(a=>a.Type=="purchase" && CardCatalog.Get(s.Cards[a.CardId!].DefinitionId).Type==CardType.Holding && s.TurnNumber<9)
                ??actions.FirstOrDefault(a=>a.Type=="purchase" && !a.Command!.ClanDiscount)
                ??actions.FirstOrDefault(a=>a.Type=="purchase")
                ??actions.FirstOrDefault(a=>a.Type=="discardProvince")
                ??actions.First(a=>a.Type=="endDynasty");
        }
        else if(s.Phase==Phase.DynastyDiscard) chosen=actions.FirstOrDefault(a=>a.Type=="discardProvince")??actions.First(a=>a.Type=="endDynasty");
        else if(s.Phase==Phase.AttackDeclaration && s.PriorityPlayerId==0) chosen=actions.FirstOrDefault(a=>a.Type=="attack")??actions.First(a=>a.Type=="pass");
        else if(s.Phase==Phase.BattleSelection) chosen=actions.First(a=>a.Type=="selectBattle");
        else chosen=actions.FirstOrDefault(a=>a.Type=="pass")??actions.First();
        Accept(e,chosen.Command!);
        Check(e.State.Players.All(p=>p.Honor>=0));
        Check(e.State.Cards.Values.GroupBy(c=>c.Id).All(g=>g.Count()==1));
    }
    Check(e.State.WinnerId is not null,$"Match did not finish in {count} commands.");
    Equal(Json(MatchEngine.Replay(e.ExportReplay()).State),Json(e.State));
}
