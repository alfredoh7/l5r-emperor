using System.Text.Json;
using L5R.Core;

var tests = new (string Name, Action Run)[]
{
    ("Bowed Takakura suppresses two Native Guides in the view and actual resolution", BowedTakakuraResolution),
    ("Unit view clamps Personality reductions separately from Followers", UnitViewClamp),
    ("Unit view follows bowed and Elite contributions and attachment removal", UnitViewBows),
    ("Unit view counts Items once and matches battle resolution", UnitViewResolution),
    ("Unit view is absent outside visible in-play Personalities and old payloads", UnitViewPrivacy),
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
    ("Turn start passes through Straighten then Events before Action", StraightenAndEventsPhaseOrder),
    ("Choose-straighten fixture lets its controller leave it bowed", ChooseStraightenDecline),
    ("Choose-straighten fixture straightens when not declined", ChooseStraightenDefault),
    ("Battle destroys province only on strictly greater margin", ProvinceThreshold),
    ("Tied armies destroy each other and gain one Honor per opposing card", BattleTie),
    ("Bowed Personality suppresses its unbowed Followers at resolution", FollowerForce),
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
    ("Scripted training match completes with replay and invariant checks", CompleteMatch),
    ("Journey's End Keep moves home a Personality with no more Followers than the performer, not one with more", JourneysEndMove),
    ("Shamate Keep moves home a Personality with equal or lower Personal Honor; gains Honor only if uncontrolled", ShamateMove),
    ("Plains of the Maiden gains Honor per surviving Battle Maiden after the Attacker destroys an army", PlainsOfMaiden),
    ("Law of Darkness Dojo weakens and bows a dishonorable enemy; punishes further Honor loss from a Battle action", LawOfDarknessDojo),
    ("Carpenter Castle's Ranged Attack is legal with no target in sight; destroys a legal target and gains Honor only if it hit", CarpenterCastleAttack),
    ("Shiro Shiba's Melee Attack strength rises with a controlled Shugenja", ShiroShibaAttack)
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
static void StraightenAndEventsPhaseOrder()
{
    var e=MatchEngine.CreateEconomyScenario();
    SkipToDynasty(e);
    var result=e.Execute(Cmd(e,"endDynasty"));
    Check(result.Accepted,result.Error??"Rejected");
    var order=result.Events.Where(ev=>ev.Type=="phaseChanged").Select(ev=>ev.Message).ToArray();
    var straighten=Array.IndexOf(order,"Phase: Straighten.");
    var events=Array.IndexOf(order,"Phase: Events.");
    var action=Array.IndexOf(order,"Phase: Action.");
    Check(straighten>=0 && events>straighten && action>events,"Straighten must precede Events, which must precede Action");
    Equal(e.State.Phase,Phase.Action);
}
static void ChooseStraightenDecline()
{
    var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
    var card=Add(s,"training-choose-straighten-samurai",1,Zone.Home,bowed:true);
    var e2=MatchEngine.FromTestState(s);
    SkipToDynasty(e2); Accept(e2,Cmd(e2,"endDynasty"));
    Equal(e2.State.Phase,Phase.Straighten); Check(e2.State.PendingChoice?.Type=="declineStraighten");
    Accept(e2,Cmd(e2,"declineStraighten",cards:[card.Id]));
    Check(e2.State.Cards[card.Id].Bowed,"Declined card should stay bowed"); Equal(e2.State.Phase,Phase.Action);
}
static void ChooseStraightenDefault()
{
    var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
    var card=Add(s,"training-choose-straighten-samurai",1,Zone.Home,bowed:true);
    var e2=MatchEngine.FromTestState(s);
    SkipToDynasty(e2); Accept(e2,Cmd(e2,"endDynasty"));
    Accept(e2,Cmd(e2,"declineStraighten",cards:[]));
    Check(!e2.State.Cards[card.Id].Bowed,"Choosing to straighten everything should include the optional card");
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
    var e=MatchEngine.FromTestState(s); Resolve(e); Equal(e.State.Cards[personality.Id].Zone,Zone.Dead);
}
static void EliteFollowerForce()
{
    // With an unbowed zero-Force Personality, only the bowed Elite Follower counts.
    var s=Battle(0,0,1,false).State; var personality=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
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

static MatchState StrongholdBattle(string strongholdDefinitionId)
{
    var s=MatchEngine.CreateEconomyScenario().State;
    s.Cards[s.Players[0].StrongholdId].DefinitionId=strongholdDefinitionId;
    s.Phase=Phase.Battle; s.CurrentBattleProvince=0; s.UnresolvedBattlefields=[0]; s.PriorityPlayerId=0;
    return s;
}
static void JourneysEndMove()
{
    var s=StrongholdBattle("oracle-l5r-4059");
    var performer=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
    var heavy=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
    Add(s,"training-follower",1,Zone.Attached,attachedTo:heavy.Id,province:0);
    var light=Add(s,"training-lion-courtier",1,Zone.Battlefield,province:0);
    var e=MatchEngine.FromTestState(s);
    var ability=new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,TargetId:heavy.Id,AbilityId:"journeys-end-move",PerformerId:performer.Id);
    Reject(e,ability); // heavy has 1 Follower attached; the performer has 0 -- too many
    Accept(e,ability with {TargetId=light.Id});
    Equal(e.State.Cards[light.Id].Zone,Zone.Home,"A target with no more Followers than the performer should move home");
}
static void ShamateMove()
{
    {
        var s=StrongholdBattle("oracle-l5r-6691");
        var performer=Add(s,"training-crane-courtier",0,Zone.Battlefield,province:0); // Personal Honor 3
        var proud=Add(s,"training-lion-dignitary",1,Zone.Battlefield,province:0); // Personal Honor 4
        var honorable=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0); // Personal Honor 3
        var e=MatchEngine.FromTestState(s);
        var before=e.State.Players[0].Honor;
        var ability=new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,TargetId:proud.Id,AbilityId:"shamate-move",PerformerId:performer.Id);
        Reject(e,ability); // proud has higher Personal Honor than the performer
        Accept(e,ability with {TargetId=honorable.Id});
        Equal(e.State.Cards[honorable.Id].Zone,Zone.Home);
        Equal(e.State.Players[0].Honor,before+2,"Should gain 2 Honor for moving home a Personality this player does not control");
    }
    {
        var s=StrongholdBattle("oracle-l5r-6691");
        var performer=Add(s,"training-crane-courtier",0,Zone.Battlefield,province:0); // Personal Honor 3
        var own=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0); // Personal Honor 2, same side
        var e=MatchEngine.FromTestState(s);
        var before=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,TargetId:own.Id,AbilityId:"shamate-move",PerformerId:performer.Id));
        Equal(e.State.Cards[own.Id].Zone,Zone.Home);
        Equal(e.State.Players[0].Honor,before,"No Honor gain when the performer's controller already controls the target");
    }
}
static void PlainsOfMaiden()
{
    var s=StrongholdBattle("oracle-l5r-5982");
    s.PriorityPlayerId=1; s.Players[1].Provinces[0].Strength=1;
    Add(s,"training-battle-maiden",0,Zone.Battlefield,province:0);
    Add(s,"training-battle-maiden",0,Zone.Battlefield,province:0);
    Add(s,"training-lion-courtier",1,Zone.Battlefield,province:0);
    var e=MatchEngine.FromTestState(s);
    var before=e.State.Players[0].Honor;
    Resolve(e);
    Equal(e.State.Players[0].Honor,before+4,"2 Honor for the destroyed defender plus 2 for two surviving Battle Maidens");
}
static void LawOfDarknessDojo()
{
    {
        var s=StrongholdBattle("oracle-l5r-4676");
        Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var honorable=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,TargetId:honorable.Id,AbilityId:"law-of-darkness-weaken"));
        Check(e.State.Cards[honorable.Id].Modifiers.Any(m=>m.Stat=="Force" && m.Amount==-4),"Should apply -4 Force");
        Check(!e.State.Cards[honorable.Id].Bowed,"An honorable target should not be bowed");
    }
    {
        var s=StrongholdBattle("oracle-l5r-4676");
        Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var dishonorable=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0); dishonorable.Dishonorable=true;
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,TargetId:dishonorable.Id,AbilityId:"law-of-darkness-weaken"));
        Check(e.State.Cards[dishonorable.Id].Bowed,"A dishonorable target should be bowed");
    }
    {
        var s=StrongholdBattle("oracle-l5r-4676");
        var samurai=Add(s,"training-honor-strike-samurai",0,Zone.Battlefield,province:0);
        var enemy=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        var before=e.State.Players[1].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:samurai.Id,TargetId:enemy.Id,AbilityId:"training-honor-strike"));
        Equal(e.State.Players[1].Honor,before-3,"2 Honor from the training ability plus 1 more from Law of Darkness Dojo's passive");
    }
}
static void CarpenterCastleAttack()
{
    {
        // Ranged/Melee Attack targeting is deferred, not required up front: the action is legal even
        // though the only enemy in play is too strong to be a legal target.
        var s=StrongholdBattle("oracle-l5r-1218");
        Add(s,"training-lion-samurai",0,Zone.Battlefield,province:0); // Presence
        var castle=Add(s,"training-castle",0,Zone.Home);
        var enemy=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0); // Force 6, above Ranged 5
        var e=MatchEngine.FromTestState(s);
        var before=e.State.Players[0].Honor;
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:castle.Id,TargetId:enemy.Id,AbilityId:"carpenter-castle-siege"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:castle.Id,AbilityId:"carpenter-castle-siege"));
        Check(e.State.Cards[castle.Id].Bowed,"Bowing the Castle/Siege card is a cost paid regardless of whether the Attack hits");
        Check(e.State.Cards[enemy.Id].Zone==Zone.Battlefield,"An out-of-range enemy is untouched");
        Equal(e.State.Players[0].Honor,before,"No Honor gain without a hit");
    }
    {
        // A Personality with a Follower attached is not itself targetable; the Follower is.
        var s=StrongholdBattle("oracle-l5r-1218");
        Add(s,"training-lion-samurai",0,Zone.Battlefield,province:0); // Presence
        var castle=Add(s,"training-castle",0,Zone.Home);
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, would be in range alone
        var follower=Add(s,"training-follower",1,Zone.Attached,attachedTo:enemy.Id,province:0); // Force 2
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:castle.Id,TargetId:enemy.Id,AbilityId:"carpenter-castle-siege"));
        var before=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:castle.Id,TargetId:follower.Id,AbilityId:"carpenter-castle-siege"));
        Equal(e.State.Cards[follower.Id].Zone,Zone.FateDiscard,"The targeted Follower is destroyed as an attachment, not marked Dead");
        Equal(e.State.Players[0].Honor,before+2,"Gains 2 Honor because the Attack targeted a card");
    }
    {
        // A Personality with no Followers, at or under strength, is destroyed as a full unit (Dead).
        var s=StrongholdBattle("oracle-l5r-1218");
        Add(s,"training-lion-samurai",0,Zone.Battlefield,province:0); // Presence
        var castle=Add(s,"training-castle",0,Zone.Home);
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, no Followers
        var e=MatchEngine.FromTestState(s);
        var before=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:castle.Id,TargetId:enemy.Id,AbilityId:"carpenter-castle-siege"));
        Equal(e.State.Cards[enemy.Id].Zone,Zone.Dead);
        Equal(e.State.Players[0].Honor,before+2);
    }
    {
        // The Castle/Siege card must be unbowed and carry the matching keyword.
        var s=StrongholdBattle("oracle-l5r-1218");
        var bowedCastle=Add(s,"training-castle",0,Zone.Home,bowed:true);
        var notCastle=Add(s,"training-border-keep",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:bowedCastle.Id,AbilityId:"carpenter-castle-siege"));
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:notCastle.Id,AbilityId:"carpenter-castle-siege"));
    }
}
static void ShiroShibaAttack()
{
    {
        // Base Melee 4 Attack: a Force-4 enemy without Followers is legal, a Force-5 one is not.
        var s=StrongholdBattle("oracle-l5r-7017");
        var performer=Add(s,"training-samurai",0,Zone.Battlefield,province:0);
        var tooStrong=Add(s,"training-crane-veteran",1,Zone.Battlefield,province:0); // Force 5
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:performer.Id,TargetId:tooStrong.Id,AbilityId:"shiro-shiba-melee"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:performer.Id,AbilityId:"shiro-shiba-melee"));
        Check(e.State.Cards[tooStrong.Id].Zone==Zone.Battlefield,"Above-strength enemy is untouched; performer does not bow as a cost");
        Check(!e.State.Cards[performer.Id].Bowed,"The performing Samurai is not itself bowed by this ability");
    }
    {
        // Controlling a Shugenja (anywhere in play, not just at this battlefield) raises strength to 6.
        var s=StrongholdBattle("oracle-l5r-7017");
        var performer=Add(s,"training-samurai",0,Zone.Battlefield,province:0);
        var shugenja=Add(s,"training-shugenja",0,Zone.Home);
        var target=Add(s,"training-crane-veteran",1,Zone.Battlefield,province:0); // Force 5
        var stillTooStrong=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0); // Force 6
        Add(s,"training-item",1,Zone.Attached,attachedTo:stillTooStrong.Id,province:0); // +2 Force = 8, above even the boosted strength of 6
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:performer.Id,TargetId:stillTooStrong.Id,AbilityId:"shiro-shiba-melee"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:performer.Id,TargetId:target.Id,AbilityId:"shiro-shiba-melee"));
        Equal(e.State.Cards[target.Id].Zone,Zone.Dead);
        Check(shugenja.Zone==Zone.Home,"The Shugenja need not be at this battlefield to grant the bonus");
    }
    {
        // The performer must be an unbowed Samurai at this battlefield.
        var s=StrongholdBattle("oracle-l5r-7017");
        var bowed=Add(s,"training-samurai",0,Zone.Battlefield,province:0,bowed:true);
        var notSamurai=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:bowed.Id,AbilityId:"shiro-shiba-melee"));
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:notSamurai.Id,AbilityId:"shiro-shiba-melee"));
    }
}
// Presentation consumes the same authoritative contribution used by ResolveBattle.
static (MatchState State, CardInstance Parent, CardInstance Follower) UnitViewFixture()
{
    var s=Battle(2,0,20).State;
    var p=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
    var f=Add(s,"training-follower",0,Zone.Attached,attachedTo:p.Id,province:0);
    f.Modifiers=[new("fixture","fixture","Force",5-CardCatalog.Get(f.DefinitionId).Force)];
    return(s,p,f);
}
static CardView UnitView(MatchState s,string id) => MatchEngine.FromTestState(s).GetView(0).Players[0].Army.Single(c=>c.Id==id);
static void UnitViewClamp()
{
    var(s,p,f)=UnitViewFixture(); Equal(UnitView(s,p.Id).UnitForce,7);
    p.Modifiers.Add(new("fixture","force-reduction","Force",-3));
    Equal(UnitView(s,p.Id).Force,0); Equal(UnitView(s,p.Id).UnitForce,5);
    Equal(UnitView(s,f.Id).Force,5); Equal(UnitView(s,f.Id).UnitForce,null);
}
static void UnitViewBows()
{
    var(s,p,f)=UnitViewFixture(); p.Bowed=true; Equal(UnitView(s,p.Id).UnitForce,0);
    f.Bowed=true; Equal(UnitView(s,p.Id).UnitForce,0);
    f.GrantedKeywords=[new("Elite","fixture","Permanent")]; Equal(UnitView(s,p.Id).UnitForce,0);
    p.GrantedKeywords=[new("Elite","fixture","Permanent")]; Equal(UnitView(s,p.Id).UnitForce,7);
    f.Zone=Zone.FateDiscard; f.AttachedTo=null; Equal(UnitView(s,p.Id).UnitForce,2);
    p.Zone=Zone.Home; p.ProvinceIndex=null;
    Equal(MatchEngine.FromTestState(s).GetView(0).Players[0].Home.Single(c=>c.Id==p.Id).UnitForce,2);
}
static void UnitViewResolution()
{
    var(s,p,f)=UnitViewFixture(); var item=Add(s,"training-item",0,Zone.Attached,bowed:true,attachedTo:p.Id,province:0);
    var bonus=CardCatalog.Get(item.DefinitionId).Force;
    Equal(UnitView(s,p.Id).Force,2+bonus); Equal(UnitView(s,p.Id).UnitForce,7+bonus);
    var e=MatchEngine.FromTestState(s); var total=UnitView(s,p.Id).UnitForce;
    Resolve(e); Check(e.GetView(0).Events.Single(ev=>ev.Type=="battleResolved").Message.Contains($"attacking Force {total},"));
    Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==p.Id).UnitForce,0,"Returning bowed units must refresh the projection");
}
static void UnitViewPrivacy()
{
    var e=MatchEngine.CreateStarterMatch(); var v=e.GetView(1);
    Check(v.Players.SelectMany(p=>p.Provinces).All(p=>p.Card?.UnitForce is null));
    Check(v.Players.SelectMany(p=>p.Hand).All(c=>c.UnitForce is null));
    Equal(v.Players[0].Hand.Length,0); Equal(v.Players[0].Stronghold.UnitForce,null);
    var(s,p,f)=UnitViewFixture(); p.Zone=Zone.Dead; f.Zone=Zone.FateDiscard; f.AttachedTo=null;
    Check(MatchEngine.FromTestState(s).GetView(0).Players[0].Discard.All(c=>c.UnitForce is null));
    var payload=System.Text.Json.Nodes.JsonNode.Parse(Json(UnitView(UnitViewFixture().State,UnitViewFixture().Parent.Id)))!.AsObject();
    payload.Remove("UnitForce"); Equal(JsonSerializer.Deserialize<CardView>(payload.ToJsonString())!.UnitForce,null);
}

static void BowedTakakuraResolution()
{
    foreach(var owner in new[]{0,1})
    {
        var s=Battle(1,1,6).State;
        var p=s.Cards.Values.Single(c=>c.OwnerId==owner && c.Zone==Zone.Battlefield);
        p.DefinitionId=PublishedContent.Snapshot.Cards.Single(d=>d.Name=="Ikoma Takakura").Id;
        p.Modifiers.Clear(); p.Bowed=true;
        var guide=PublishedContent.Snapshot.Cards.Single(d=>d.Name=="Native Guide").Id;
        Add(s,guide,owner,Zone.Attached,attachedTo:p.Id,province:0);
        Add(s,guide,owner,Zone.Attached,attachedTo:p.Id,province:0);
        var e=MatchEngine.FromTestState(s);
        var bowedCards=e.GetView(owner).Players[owner].Army;
        var card=bowedCards.Single(c=>c.Id==p.Id);
        Equal(card.Force,3); Equal(card.Chi,3); Equal(card.UnitForce,0);
        Resolve(e);
        Equal(e.State.Cards[p.Id].Zone,Zone.Dead,"Bowed Personality and its Followers must lose to the opposing 1F unit");
        Check(!e.State.Players[1].Provinces[0].Destroyed);
        var result=e.GetView(owner).Events.Single(ev=>ev.Type=="battleResolved").Message;
        Check(result.Contains(owner==0?"attacking Force 0, defending Force 1":"attacking Force 1, defending Force 0"),result);
        p.Bowed=false;
        var ready=MatchEngine.FromTestState(s).GetView(owner).Players[owner].Army.Single(c=>c.Id==p.Id);
        Equal(ready.Force,3); Equal(ready.Chi,3); Equal(ready.UnitForce,13);
        if(owner==0 && Environment.GetEnvironmentVariable("L5R_UNIT_VIEW_FIXTURE") is { Length: >0 } output)
            File.WriteAllText(output,Json(new[]{bowedCards,MatchEngine.FromTestState(s).GetView(owner).Players[owner].Army}));
    }
}
