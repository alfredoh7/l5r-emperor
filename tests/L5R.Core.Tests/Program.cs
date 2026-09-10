using System.Text.Json;
using L5R.Core;
using L5R.Client;

var tests = new (string Name, Action Run)[]
{
    ("Battle journal groups public effects and actual destinations without duplicating snapshots", BattleJournalProjection),
    ("Accepted battle and reaction passes publish their actor; rejected passes remain atomic", PublicPassEvents),
    ("A bowed Personality suppresses two Native Guides in the view and actual resolution", BowedPersonalityResolution),
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
    ("Terrain plays as a Battle action, caps at one per battlefield, is targetable, and leaves at resolution", TerrainPrimitive),
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
    ("Shiro Shiba's Melee Attack strength rises with a controlled Shugenja", ShiroShibaAttack),
    ("Tokens create/move/destroy, persist across turns unlike Modifiers, and are removed when their host leaves play", TokenPrimitives),
    ("A discarded dishonorable Personality can become honorably dead: rehonored, no Honor-loss penalty", MakeDeadFromDiscard),
    ("Meiyu's entering-play reaction composes with Proclaim, each usable once, before the window closes", MeiyuEntersPlayReaction),
    ("Monastery of New Thought grants a Fire token to a Tattooed Personality and destroys one to fuel its Ranged 5 Attack", MonasteryFireTokenAndAttack),
    ("Togashi Torazu's entering-play reaction and self-performed Fire-token Ranged Attack need no Performer", TorazuFireTokensAndAttack),
    ("An Attack's strength is mutable state: a Reaction may negate or boost it before resolution, when one is available", AttackReactionWindow),
    ("Import wave 1: four unconditional Ranged/Melee Attack cards need only catalog wiring", UnconditionalAttackImportWave),
    ("Tsuruchi Isas bows himself, Ranged 3 Attacks, and moves home after regardless of the hit", TsuruchiIsasAttackAndMoveHome),
    ("Import wave 2: Bayushi Kahoku's Attack strength and own Force both key off an enemy's dishonorable status", BayushiKahokuAttack),
    ("Import wave 2: Asako Ume's Force bonus keys off carrying a Spell; her Attack is unconditional", AsakoUmeAttack),
    ("Reconnaissance defaults to false per player per province, is granted independently of other players/provinces, and gates only the dependent portion of an effect", Reconnaissance),
    ("Reconnaissance persists through a province refill and clears only when its battle resolves, destroyed or not", ReconnaissanceOnlyClearsOnBattleResolution),
    ("Tactical Advantage requires a performing Tactician, is usable any number of times per turn, and gives Force equal to the discarded card's Focus Value", TacticalAdvantage),
    ("Conqueror straightens the sole eligible attacker automatically; with two eligible, the attacker must choose", ConquerorStraightensAfterVictory),
    ("Kensai raises the Weapon cap to two; Kama independently raises it to two but does not stack with Kensai", KensaiAndKamaWeaponCap),
    ("Cavalry Maneuvers follows Infantry Maneuvers; a unit is Cavalry only if the Personality and every attached Follower carry the keyword", CavalryManeuversSegment),
    ("A Two-Handed Weapon overrides Kensai and Kama entirely: at most one Weapon whenever one is involved, either way round", TwoHandedWeaponOverridesKensaiAndKama),
    ("Daigotsu Kendo's Conqueror keeps him unbowed after a win; Ikoma Natsu's Cavalry assigns during Cavalry Maneuvers", DaigotsuKendoConquerorAndIkomaNatsuCavalry),
    ("Mirumoto Reiyu gives an enemy Force/Follower a Force penalty equal to her own current Force", MirumotoReiyuForcePenalty),
    ("Shinjo Horibe may bow or straighten any Personality, but only while opposed", ShinjoHoribeBowOrStraighten),
    ("Doji Atsumichi may bow or straighten a Personality with lower Personal Honor than himself", DojiAtsumichiBowOrStraighten),
    ("Tetsuo destroys an attachment, and separately an enemy Personality with fewer Weapons than himself", TetsuoDestroyAttachmentAndWeakerWeaponed),
    ("Moto Shunsuke's Force bonus tracks the largest single dead-Personality pile; his Limited ability only targets another player's discarded Personality", MotoShunsukeForceBonusAndMakeDead),
    ("Equip (Battle) attaches a Spell to an opposed unbowed Shugenja, opens a bounded immediate follow-up window for its Battle ability, and is usable any number of times per turn", SpellsAndShugenja)
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

static void PublicPassEvents()
{
    var e=Battle();
    var before=e.State.Events.Count;
    Reject(e,new("pass",0,e.Version));
    Equal(e.State.Events.Count,before);
    Accept(e,Cmd(e,"pass"));
    var first=e.GetView(0).Events.Last(); Equal(first.Type,"passed"); Equal(first.PlayerId,1);
    Accept(e,Cmd(e,"pass"));
    var events=e.GetView(1).Events.Skip(before).ToArray();
    Equal(events.Count(x=>x.Type=="passed"),2);
    Check(events.Single(x=>x.Type=="passed" && x.PlayerId==0).Sequence < events.Single(x=>x.Type=="battleResolved").Sequence);
    var reaction=MatchEngine.CreateEconomyScenario();
    var id=ProvinceCard(reaction); var quote=reaction.QuotePurchase(0,id);
    Accept(reaction,Cmd(reaction,"purchase",id:id,sources:quote.Payments[0].SourceIds));
    Check(reaction.State.PendingChoice is not null);
    var actor=reaction.State.PriorityPlayerId;
    Accept(reaction,Cmd(reaction,"declineReaction"));
    var pass=reaction.GetView(1).Events.Last(x=>x.Type=="reactionPassed"); Equal(pass.PlayerId,actor);
    Check(pass.CardId is null,"Passing must not reveal a hidden card.");
}

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
    Accept(e,Cmd(e,"attack",assignments:[])); Equal(e.State.PriorityPlayerId,1); Equal(e.State.Phase,Phase.DefenseAssignment);
    Accept(e,Cmd(e,"assignDefense",assignments:[])); Equal(e.State.PriorityPlayerId,0); Equal(e.State.Phase,Phase.CavalryDeclaration);
    // Infantry Maneuvers committed above; Cavalry Maneuvers follows the same attacker-then-defender order.
    Accept(e,Cmd(e,"attack",assignments:[])); Equal(e.State.PriorityPlayerId,1); Equal(e.State.Phase,Phase.CavalryDefense);
    // Nobody ever placed a unit anywhere, Infantry or Cavalry: no battlefield is unresolved, so
    // there is nothing to select and the turn moves straight to the Dynasty Phase.
    Accept(e,Cmd(e,"assignDefense",assignments:[])); Equal(e.State.Phase,Phase.Dynasty);
    Equal(e.State.UnresolvedBattlefields.Count,0);
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
static void TerrainPrimitive()
{
    // MatchEngine.State returns a fresh Copy() on every access -- every fixture card this scenario
    // needs must go into the state passed to FromTestState up front, not added onto e.State later.
    var s=Battle(5,2).State;
    var attacker=s.Cards.Values.Single(c=>c.OwnerId==0 && c.Zone==Zone.Battlefield);
    var terrain=Add(s,"training-terrain",1,Zone.Hand);
    var second=Add(s,"training-terrain",0,Zone.Hand);
    var destroyer=Add(s,"training-terrain-destroyer",0,Zone.Battlefield,province:0);
    var e=MatchEngine.FromTestState(s);
    Reject(e,Cmd(e,"playStrategy",id:terrain.Id,target:attacker.Id)); // Terrain takes no target.
    Accept(e,Cmd(e,"playStrategy",id:terrain.Id));
    Equal(e.State.Cards[terrain.Id].Zone,Zone.Battlefield); Equal(e.State.Cards[terrain.Id].ProvinceIndex,0); Equal(e.State.PriorityPlayerId,0);
    // At most one Terrain per battlefield until the one in play is destroyed.
    Reject(e,Cmd(e,"playStrategy",id:second.Id));
    // The Terrain in play may be targeted by a Battle ability built for it.
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:destroyer.Id,TargetId:terrain.Id,AbilityId:"training-destroy-terrain"));
    Equal(e.State.Cards[terrain.Id].Zone,Zone.FateDiscard);
    // Once destroyed, a fresh Terrain may be played at the same battlefield again (after the
    // opponent's action opportunity, which using the ability handed to them, passes back).
    Accept(e,Cmd(e,"pass")); Equal(e.State.PriorityPlayerId,0);
    Accept(e,Cmd(e,"playStrategy",id:second.Id));
    Equal(e.State.Cards[second.Id].Zone,Zone.Battlefield);
    // Terrain leaves automatically at battle resolution, its printed effects already applied --
    // never a separate destroy step, and never carried into a later battle at this province.
    Resolve(e);
    Equal(e.State.Cards[second.Id].Zone,Zone.FateDiscard); Check(e.State.Cards[second.Id].ProvinceIndex is null);
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
static void MakeDeadFromDiscard()
{
    {
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var master=Add(s,"training-token-master",0,Zone.Home);
        var discarded=Add(s,"training-dishonored-samurai",0,Zone.DynastyDiscard); discarded.Dishonorable=true;
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:master.Id,TargetId:discarded.Id,AbilityId:"training-make-dead"));
        Equal(e.State.Cards[discarded.Id].Zone,Zone.Dead);
        Check(!e.State.Cards[discarded.Id].Dishonorable,"Becomes honorably dead: rehonored first");
        Equal(e.State.Players[0].Honor,honorBefore,"No Honor loss -- he was rehonored before becoming dead");
    }
    {
        // A Personality still in play (not yet discarded) is not a legal target.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var master=Add(s,"training-token-master",0,Zone.Home);
        var inPlay=Add(s,"training-crane-samurai",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:master.Id,TargetId:inPlay.Id,AbilityId:"training-make-dead"));
    }
}
static void TokenPrimitives()
{
    {
        // Create: a token's stat bonus applies immediately and, unlike a Modifier, survives FinishTurn.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var master=Add(s,"training-token-master",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:master.Id,TargetId:master.Id,AbilityId:"training-create-token"));
        Equal(e.State.Cards[master.Id].Tokens.Count,1); Equal(e.State.Cards[master.Id].Tokens[0].Type,"Training");
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==master.Id).Force,3,"Base Force 2 plus the token's +1F");
        Accept(e,Cmd(e,"pass")); SkipToDynasty(e); Accept(e,Cmd(e,"endDynasty"));
        Equal(e.State.Cards[master.Id].Tokens.Count,1,"Tokens are not cleared at turn end, unlike Modifiers");
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==master.Id).Force,3);
    }
    {
        // Move: the token's stat bonus transfers with it, off the source and onto the destination.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var master=Add(s,"training-token-master",0,Zone.Home);
        var other=Add(s,"training-crane-samurai",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:master.Id,TargetId:master.Id,AbilityId:"training-create-token"));
        var tokenId=e.State.Cards[master.Id].Tokens[0].Id;
        Accept(e,Cmd(e,"pass"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:master.Id,TargetId:master.Id,PerformerId:other.Id,AbilityId:"training-move-token"));
        Equal(e.State.Cards[master.Id].Tokens.Count,0);
        Equal(e.State.Cards[other.Id].Tokens.Count,1); Equal(e.State.Cards[other.Id].Tokens[0].Id,tokenId);
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==master.Id).Force,2,"Force returns to base once its token moves away");
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==other.Id).Force,4,"Base Force 3 plus the moved token's +1F");
    }
    {
        // Destroy: the token and its stat bonus are gone.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var master=Add(s,"training-token-master",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:master.Id,TargetId:master.Id,AbilityId:"training-create-token"));
        Accept(e,Cmd(e,"pass"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:master.Id,TargetId:master.Id,AbilityId:"training-destroy-token"));
        Equal(e.State.Cards[master.Id].Tokens.Count,0);
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==master.Id).Force,2);
    }
    {
        // Leaves play: a destroyed unit's own tokens, and its destroyed attachment's tokens, are
        // removed from the game entirely (core rules Glossary — Token).
        var s=MatchEngine.CreateEconomyScenario().State;
        s.Phase=Phase.Battle; s.CurrentBattleProvince=0; s.UnresolvedBattlefields=[0]; s.PriorityPlayerId=1;
        var attacker=Add(s,"training-crane-veteran",0,Zone.Battlefield,province:0); attacker.Modifiers=[new("fixture","fixture","Force",5)]; // Force 10
        var defender=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0); // Force 6
        defender.Tokens=[new("test-token","Training","test-fixture","Force",1)];
        var follower=Add(s,"training-follower",1,Zone.Attached,attachedTo:defender.Id,province:0); // +2F unbowed; total defense 8 < attack 10
        follower.Tokens=[new("test-token-2","Training","test-fixture","Force",1)];
        var e=MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[defender.Id].Zone,Zone.Dead);
        Equal(e.State.Cards[defender.Id].Tokens.Count,0,"A destroyed unit's tokens are removed from the game");
        Equal(e.State.Cards[follower.Id].Zone,Zone.FateDiscard);
        Equal(e.State.Cards[follower.Id].Tokens.Count,0,"A destroyed attachment's tokens are removed from the game too");
    }
}
static void MeiyuEntersPlayReaction()
{
    var s=MatchEngine.CreateEconomyScenario().State;
    s.Players[0].Clan="Dragon"; // in-clan with Meiyu, so Proclaim is also eligible this entry
    Add(s,"training-marketplace",0,Zone.Home); Add(s,"training-marketplace",0,Zone.Home); // enough Gold for her 5 cost
    var meiyuId=PublishedContent.PersonalityWave1.Cards.Single(c=>c.Name=="Togashi Meiyu").Id;
    var cardId=s.Players[0].Provinces[0].CardId!;
    s.Cards[cardId].DefinitionId=meiyuId; s.Cards[cardId].FaceUp=true;
    var e=MatchEngine.FromTestState(s);
    var honorBefore=e.State.Players[0].Honor;
    var payment=e.PaymentOptions(0,5).First();
    Accept(e,new GameCommand("purchase",0,e.Version,CardId:cardId,SourceIds:payment.SourceIds));
    Equal(e.State.PendingChoice?.Type,"proclaim","Both Proclaim and Meiyu's own reaction are eligible on this entry");
    // Meiyu's own reaction first; the window stays open afterward for Proclaim.
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:cardId,AbilityId:"meiyu-fire-tokens"));
    Equal(e.State.Cards[cardId].Tokens.Count,3);
    Check(e.State.Cards[cardId].Tokens.All(t=>t.Type=="Fire" && t.Stat=="Force" && t.Amount==1));
    Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==cardId).Force,3,"Base Force 0 plus three +1F Fire tokens");
    Check(e.State.PendingChoice is not null,"The window stays open for Proclaim afterward");
    // Using the reaction passed priority to the opponent, same as Proclaim's own alternation; they
    // have nothing to do here but decline, handing priority back for Proclaim.
    Accept(e,new GameCommand("declineReaction",1,e.Version));
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:cardId,AbilityId:"meiyu-fire-tokens")); // once only
    // Proclaim still works afterward, in either order.
    Accept(e,new GameCommand("proclaim",0,e.Version));
    Equal(e.State.Players[0].Honor,honorBefore+2,"Meiyu's printed Personal Honor is 2");
    Check(e.State.PendingChoice is not null,"Window still open until both players pass");
    Accept(e,new GameCommand("declineReaction",1,e.Version));
    Accept(e,new GameCommand("declineReaction",0,e.Version));
    Check(e.State.PendingChoice is null,"Window closes once both players pass consecutively");
}
static void MonasteryFireTokenAndAttack()
{
    {
        // Limited: only a Tattooed Personality is a legal target.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId="oracle-l5r-10183";
        var tattooed=Add(s,"training-tattooed",0,Zone.Home);
        var notTattooed=Add(s,"training-crane-samurai",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,TargetId:notTattooed.Id,AbilityId:"monastery-fire-token"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,TargetId:tattooed.Id,AbilityId:"monastery-fire-token"));
        Equal(e.State.Cards[tattooed.Id].Tokens.Count,1); Equal(e.State.Cards[tattooed.Id].Tokens[0].Type,"Fire");
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==tattooed.Id).Force,3,"Base Force 2 plus the token's +1F");
    }
    {
        // Battle: destroying a Fire token on the performer is a required cost -- no Fire token, no legal action.
        var s=StrongholdBattle("oracle-l5r-10183");
        var performer=Add(s,"training-tattooed",0,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:performer.Id,AbilityId:"monastery-fire-attack"));
    }
    {
        // The performer may be bowed -- no "unbowed" qualifier is printed. Deferred targeting: legal
        // with no target in sight, but the Fire token is destroyed as a cost regardless.
        var s=StrongholdBattle("oracle-l5r-10183");
        var performer=Add(s,"training-tattooed",0,Zone.Battlefield,province:0,bowed:true);
        performer.Tokens.Add(new("fixture-token","Fire","test-fixture","Force",1));
        var enemy=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0); // Force 6, above Ranged 5
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:performer.Id,AbilityId:"monastery-fire-attack"));
        Equal(e.State.Cards[performer.Id].Tokens.Count,0,"The Fire token is destroyed as a cost regardless of whether the Attack hits");
        Check(e.State.Cards[enemy.Id].Zone==Zone.Battlefield,"An out-of-range enemy is untouched");
    }
    {
        // A legal target (Force <= 5, no Followers) is destroyed.
        var s=StrongholdBattle("oracle-l5r-10183");
        var performer=Add(s,"training-tattooed",0,Zone.Battlefield,province:0);
        performer.Tokens.Add(new("fixture-token","Fire","test-fixture","Force",1));
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, no Followers
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:performer.Id,TargetId:enemy.Id,AbilityId:"monastery-fire-attack"));
        Equal(e.State.Cards[performer.Id].Tokens.Count,0);
        Equal(e.State.Cards[enemy.Id].Zone,Zone.Dead);
    }
}
static void TorazuFireTokensAndAttack()
{
    var torazuId=PublishedContent.PersonalityWave2.Cards.Single(c=>c.Name=="Togashi Torazu").Id;
    {
        // Reaction: entering play grants three +1F Fire tokens, same mechanism as Meiyu's. The
        // buying Crane player is off-clan, so only Torazu's own reaction is offered, not Proclaim.
        var s=MatchEngine.CreateEconomyScenario().State;
        Add(s,"training-marketplace",0,Zone.Home); Add(s,"training-marketplace",0,Zone.Home); // enough Gold for cost 6
        var cardId=s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId=torazuId; s.Cards[cardId].FaceUp=true;
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,6).First();
        Accept(e,new GameCommand("purchase",0,e.Version,CardId:cardId,SourceIds:payment.SourceIds));
        Equal(e.State.PendingChoice?.Type,"entryReaction");
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:cardId,AbilityId:"torazu-fire-tokens"));
        Equal(e.State.Cards[cardId].Tokens.Count,3);
        Check(e.State.Cards[cardId].Tokens.All(t=>t.Type=="Fire" && t.Stat=="Force" && t.Amount==1));
    }
    {
        // Battle: no separate Performer -- the shared "source Personality must be at this
        // battlefield" check already stands in for presence/location. No Fire token, no legal action.
        var s=StrongholdBattle("oracle-l5r-4059");
        var torazu=Add(s,torazuId,0,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:torazu.Id,AbilityId:"torazu-fire-attack"));
    }
    {
        // Deferred targeting: legal with no target in sight, but the Fire token is destroyed as a
        // cost regardless of whether the Attack hits.
        var s=StrongholdBattle("oracle-l5r-4059");
        var torazu=Add(s,torazuId,0,Zone.Battlefield,province:0);
        torazu.Tokens.Add(new("fixture-token","Fire","test-fixture","Force",1));
        var enemy=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0); // Force 6, above Ranged 3
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:torazu.Id,AbilityId:"torazu-fire-attack"));
        Equal(e.State.Cards[torazu.Id].Tokens.Count,0,"The Fire token is destroyed as a cost regardless of whether the Attack hits");
        Check(e.State.Cards[enemy.Id].Zone==Zone.Battlefield,"An out-of-range enemy is untouched");
    }
    {
        // A legal target (Force <= 3, no Followers) is destroyed.
        var s=StrongholdBattle("oracle-l5r-4059");
        var torazu=Add(s,torazuId,0,Zone.Battlefield,province:0);
        torazu.Tokens.Add(new("fixture-token","Fire","test-fixture","Force",1));
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, no Followers
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:torazu.Id,TargetId:enemy.Id,AbilityId:"torazu-fire-attack"));
        Equal(e.State.Cards[torazu.Id].Tokens.Count,0);
        Equal(e.State.Cards[enemy.Id].Zone,Zone.Dead);
    }
}
static void AttackReactionWindow()
{
    {
        // A reaction is available (the fixture reactor, anywhere in play -- Reaction timing has no
        // Battle-presence/location requirement of its own): the window opens instead of resolving
        // immediately. Negating drives strength to 0; the cost already paid stays paid regardless.
        var s=StrongholdBattle("oracle-l5r-1218");
        Add(s,"training-lion-samurai",0,Zone.Battlefield,province:0); // Presence
        var castle=Add(s,"training-castle",0,Zone.Home);
        var reactor=Add(s,"training-attack-reactor",1,Zone.Home);
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, legal target
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:castle.Id,TargetId:enemy.Id,AbilityId:"carpenter-castle-siege"));
        Equal(e.State.PendingChoice?.Type,"attackReaction","A reaction is available, so the window opens instead of resolving immediately");
        Equal(e.State.PendingAttack?.Strength,5);
        Check(e.State.Cards[castle.Id].Bowed,"The cost is paid immediately, before the window even opens");
        Accept(e,new GameCommand("useAbility",1,e.Version,CardId:reactor.Id,AbilityId:"training-attack-negate"));
        Equal(e.State.PendingAttack?.Strength,0);
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Accept(e,new GameCommand("declineReaction",1,e.Version));
        Check(e.State.PendingChoice is null && e.State.PendingAttack is null,"Window closed and the attack resolved");
        Check(e.State.Cards[enemy.Id].Zone==Zone.Battlefield,"Negated: the target survives");
    }
    {
        // Boosting raises strength; a target already legal at announce time is still destroyed, and
        // the per-ability hit bonus (Carpenter Castle's +2 Honor) still fires on the deferred hit.
        var s=StrongholdBattle("oracle-l5r-1218");
        Add(s,"training-lion-samurai",0,Zone.Battlefield,province:0);
        var castle=Add(s,"training-castle",0,Zone.Home);
        var reactor=Add(s,"training-attack-reactor",0,Zone.Home);
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:s.Players[0].StrongholdId,PerformerId:castle.Id,TargetId:enemy.Id,AbilityId:"carpenter-castle-siege"));
        Accept(e,new GameCommand("declineReaction",1,e.Version)); // the opponent has nothing to react with
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:reactor.Id,AbilityId:"training-attack-boost"));
        Equal(e.State.PendingAttack?.Strength,7,"5 base + 2 from the boost");
        Accept(e,new GameCommand("declineReaction",1,e.Version));
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Equal(e.State.Cards[enemy.Id].Zone,Zone.Dead);
        Equal(e.State.Players[0].Honor,honorBefore+2,"Carpenter Castle's hit bonus still applies to a deferred resolution");
    }
}
static void UnconditionalAttackImportWave()
{
    // Shinjo Eun-Sahng (Ranged 4), Shosuro Konishi (Ranged 5), Tsuruchi Kosoko (Ranged 5) and Stalking
    // Tiger (Melee 6): each card is its own performer with no cost and no condition -- proves the
    // shared Attack primitive needs nothing beyond a catalog entry plus a UseAbility case per card.
    foreach(var (cardId,abilityId,strength) in new[]
    {
        ("oracle-l5r-6872","shinjo-eun-sahng-attack",4),
        ("oracle-l5r-7121","shosuro-konishi-attack",5),
        ("oracle-l5r-8852","tsuruchi-kosoko-attack",5),
        ("oracle-l5r-7450","stalking-tiger-attack",6),
    })
    {
        var s=StrongholdBattle("oracle-l5r-1218");
        // Stalking Tiger is a Follower: it needs a friendly Personality unit to attach to before it
        // (or anything else) counts as "a unit at the current battlefield" (Army() is Personality-only).
        var isFollower=CardCatalog.Get(cardId).Type==CardType.Follower;
        var host=isFollower?Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0):null;
        var performer=Add(s,cardId,0,isFollower?Zone.Attached:Zone.Battlefield,province:0,attachedTo:host?.Id);
        var target=Add(s,"training-crane-veteran",1,Zone.Battlefield,province:0); // Force 5
        var e=MatchEngine.FromTestState(s);
        if(strength<5) Reject(e,new GameCommand("useAbility",0,e.Version,CardId:performer.Id,TargetId:target.Id,AbilityId:abilityId));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:performer.Id,TargetId:strength>=5?target.Id:null,AbilityId:abilityId));
        Equal(e.State.Cards[target.Id].Zone,strength>=5?Zone.Dead:Zone.Battlefield,$"{cardId}: hit expectation at strength {strength} vs Force 5");
    }
}
static void TsuruchiIsasAttackAndMoveHome()
{
    var s=StrongholdBattle("oracle-l5r-1218");
    var isas=Add(s,"oracle-l5r-8840",0,Zone.Battlefield,province:0);
    var target=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3
    var e=MatchEngine.FromTestState(s);
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:isas.Id,TargetId:target.Id,AbilityId:"tsuruchi-isas-attack"));
    Equal(e.State.Cards[target.Id].Zone,Zone.Dead,"Ranged 3 hits a Force-3 target");
    Check(e.State.Cards[isas.Id].Bowed,"Bowing Isas is the cost, paid up front");
    Equal(e.State.Cards[isas.Id].Zone,Zone.Home,"Isas moves home after the Attack resolves");
    Equal(e.State.Cards[isas.Id].ProvinceIndex,null,"Home is not associated with a province");
}
static void BayushiKahokuAttack()
{
    // No enemy Dishonorable Personality yet: no passive Force bonus, base strength 3 can't reach a
    // Force-5 target.
    var s=StrongholdBattle("oracle-l5r-1218");
    var kahoku=Add(s,"oracle-l5r-806",0,Zone.Battlefield,province:0);
    var honorable=Add(s,"training-crane-veteran",1,Zone.Battlefield,province:0); // Force 5, not dishonorable
    var e=MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==kahoku.Id).Force,3,"No enemy is dishonorable: base Force 3");
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:kahoku.Id,TargetId:honorable.Id,AbilityId:"bayushi-kahoku-attack"));

    // A dishonorable enemy Personality both raises Kahoku's own Force (+2, always-on) and the Attack's
    // strength against that specific target (+2, target-dependent) -- two separate conditionals that
    // happen to share the same Dishonorable flag.
    var s2=StrongholdBattle("oracle-l5r-1218");
    var kahoku2=Add(s2,"oracle-l5r-806",0,Zone.Battlefield,province:0);
    var dishonorable=Add(s2,"training-crane-veteran",1,Zone.Battlefield,province:0); dishonorable.Dishonorable=true; // Force 5
    var e2=MatchEngine.FromTestState(s2);
    Equal(e2.GetView(0).Players[0].Army.Single(c=>c.Id==kahoku2.Id).Force,5,"An enemy dishonorable Personality raises Kahoku's own Force by 2");
    Accept(e2,new GameCommand("useAbility",0,e2.Version,CardId:kahoku2.Id,TargetId:dishonorable.Id,AbilityId:"bayushi-kahoku-attack"));
    Equal(e2.State.Cards[dishonorable.Id].Zone,Zone.Dead,"Strength 3+2=5 destroys the dishonorable Force-5 target");
}
static void AsakoUmeAttack()
{
    var s=StrongholdBattle("oracle-l5r-1218");
    var ume=Add(s,"oracle-l5r-592",0,Zone.Battlefield,province:0);
    var target=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 2
    var e=MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==ume.Id).Force,3,"No Spell attached: base Force 3");
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:ume.Id,TargetId:target.Id,AbilityId:"asako-ume-attack"));
    Equal(e.State.Cards[target.Id].Zone,Zone.Dead,"Ranged 4 Attack is unconditional regardless of the Spell check");

    var s2=StrongholdBattle("oracle-l5r-1218");
    var ume2=Add(s2,"oracle-l5r-592",0,Zone.Battlefield,province:0);
    Add(s2,"training-spell",0,Zone.Attached,attachedTo:ume2.Id,province:0);
    var e2=MatchEngine.FromTestState(s2);
    Equal(e2.GetView(0).Players[0].Army.Single(c=>c.Id==ume2.Id).Force,4,"A Spell attached raises her own Force by 1; the Attack's strength is unaffected");
}
static void Reconnaissance()
{
    var s=MatchEngine.CreateEconomyScenario().State;
    s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
    var scout=Add(s,"training-scout",0,Zone.Home);
    var e=MatchEngine.FromTestState(s);
    Check(!e.State.Players[1].Provinces[0].Reconnaissance[0],"Defaults to false");
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:scout.Id,AbilityId:"training-gain-reconnaissance",Province:4)); // out of range
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:scout.Id,AbilityId:"training-gain-reconnaissance",Province:0));
    Check(e.State.Players[1].Provinces[0].Reconnaissance[0],"Granted for player 0 in player 1's province 0");
    Check(!e.State.Players[1].Provinces[1].Reconnaissance[0],"Not granted at a different province");
    Check(!e.State.Players[1].Provinces[0].Reconnaissance[1],"Not granted to the other player");
    Check(!e.State.Players[0].Provinces[0].Reconnaissance[0],"Not granted in the granting player's own province");

    // Move the same, now-Reconnaissance-carrying state into a battle and exercise the gated effect:
    // with Reconnaissance in the target's province, the strike's Honor-loss portion proceeds.
    var battle=e.State; battle.Phase=Phase.Battle; battle.CurrentBattleProvince=0; battle.UnresolvedBattlefields=[0]; battle.PriorityPlayerId=0;
    Add(battle,"training-lion-samurai",0,Zone.Battlefield,province:0); // Presence
    var scoutAtBattle=Add(battle,"training-scout",0,Zone.Battlefield,province:0);
    var withRecon=Add(battle,"training-crane-samurai",1,Zone.Battlefield,province:0);
    var eb=MatchEngine.FromTestState(battle);
    var honorBefore=eb.State.Players[1].Honor;
    Accept(eb,new GameCommand("useAbility",0,eb.Version,CardId:scoutAtBattle.Id,TargetId:withRecon.Id,AbilityId:"training-reconnaissance-strike"));
    Equal(eb.State.Players[1].Honor,honorBefore-2,"Reconnaissance is present in this province: the Honor-loss portion proceeds");

    // Without Reconnaissance, the same ability still succeeds -- its gated portion just does nothing.
    var s2=MatchEngine.CreateEconomyScenario().State;
    s2.Phase=Phase.Battle; s2.CurrentBattleProvince=0; s2.UnresolvedBattlefields=[0]; s2.PriorityPlayerId=0;
    Add(s2,"training-lion-samurai",0,Zone.Battlefield,province:0);
    var scoutNoRecon=Add(s2,"training-scout",0,Zone.Battlefield,province:0);
    var withoutRecon=Add(s2,"training-crane-samurai",1,Zone.Battlefield,province:0);
    var en=MatchEngine.FromTestState(s2);
    var honorBefore2=en.State.Players[1].Honor;
    Accept(en,new GameCommand("useAbility",0,en.Version,CardId:scoutNoRecon.Id,TargetId:withoutRecon.Id,AbilityId:"training-reconnaissance-strike"));
    Equal(en.State.Players[1].Honor,honorBefore2,"No Reconnaissance in this province: the action succeeds but its gated portion does nothing");
}
static void ReconnaissanceOnlyClearsOnBattleResolution()
{
    // Refilling a province with a new, unseen card does NOT clear Reconnaissance on its own --
    // resolving a battle there is the only thing that does.
    var s=MatchEngine.CreateEconomyScenario().State;
    s.Phase=Phase.Action; s.PriorityPlayerId=1; s.ActivePlayerId=1;
    var scout=Add(s,"training-scout",1,Zone.Home);
    var e=MatchEngine.FromTestState(s);
    var id=ProvinceCard(e,2);
    Accept(e,new GameCommand("useAbility",1,e.Version,CardId:scout.Id,AbilityId:"training-gain-reconnaissance",Province:2));
    Check(e.State.Players[0].Provinces[2].Reconnaissance[1],"Granted before the province refills");
    var dynasty=e.State; dynasty.Phase=Phase.Dynasty; dynasty.PriorityPlayerId=0; dynasty.ActivePlayerId=0;
    e=MatchEngine.FromTestState(dynasty);
    Accept(e,Cmd(e,"purchase",id:id,sources:e.QuotePurchase(0,id).Payments[0].SourceIds));
    Check(e.State.Players[0].Provinces[2].Reconnaissance[1],"Still granted after the province refills -- only a battle resolving there clears it");

    // A tied battle resolves without destroying the province -- Reconnaissance still resets to No.
    var e2=Battle(3,3);
    e2.State.Players[1].Provinces[0].Reconnaissance[0]=true;
    Resolve(e2);
    Check(!e2.State.Players[1].Provinces[0].Destroyed,"Sanity: a tied battle does not destroy the province");
    Check(!e2.State.Players[1].Provinces[0].Reconnaissance[0],"Reconnaissance resets to No once the battle resolves, whether or not the province survives");

    // Destroying a province via battle also clears it (same precedent values as ProvinceThreshold).
    var e3=Battle(9,2,6);
    e3.State.Players[1].Provinces[0].Reconnaissance[0]=true;
    Resolve(e3);
    Check(e3.State.Players[1].Provinces[0].Destroyed,"Sanity: the province was actually destroyed");
    Check(!e3.State.Players[1].Provinces[0].Reconnaissance[0],"Reconnaissance clears when its province's battle resolves, including when destroyed");
}
static void TacticalAdvantage()
{
    var s=StrongholdBattle("oracle-l5r-1218");
    var tactician=Add(s,"training-tactician",0,Zone.Battlefield,province:0);
    var notTactician=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
    var focus1=Add(s,"training-focus-strategy",0,Zone.Hand);
    var focus2=Add(s,"training-focus-strategy",0,Zone.Hand);
    var e=MatchEngine.FromTestState(s);
    Reject(e,new GameCommand("useAbility",0,e.Version,PerformerId:notTactician.Id,TargetId:focus1.Id,AbilityId:"tactical-advantage"));
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==tactician.Id).Force,2,"Base Force 2, no bonus yet");
    Accept(e,new GameCommand("useAbility",0,e.Version,PerformerId:tactician.Id,TargetId:focus1.Id,AbilityId:"tactical-advantage"));
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==tactician.Id).Force,5,"Base 2 + discarded card's Focus Value 3");
    Equal(e.State.Cards[focus1.Id].Zone,Zone.FateDiscard,"The discarded card moves to the Fate discard pile");
    // Any number of times per turn: a second use in the same turn is not blocked by the normal
    // once-per-turn dedupe -- only its own cost (discarding another card) limits it. Priority passed
    // to the opponent after the first use (like any action), so they must act (or pass) before it
    // returns to player 0 for the second use.
    Accept(e,new GameCommand("pass",1,e.Version));
    Accept(e,new GameCommand("useAbility",0,e.Version,PerformerId:tactician.Id,TargetId:focus2.Id,AbilityId:"tactical-advantage"));
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==tactician.Id).Force,8,"Two uses stack: base 2 + 3 + 3");
}
static void ConquerorStraightensAfterVictory()
{
    // A single eligible Conqueror resolves immediately: no choice to make.
    var s=StrongholdBattle("oracle-l5r-1218");
    var conqueror=Add(s,"training-conqueror",0,Zone.Battlefield,province:0); // Force 2
    var other=Add(s,"training-lion-samurai",0,Zone.Battlefield,province:0); // Force 4, no Conqueror
    var weak=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, attacker wins 6-3
    var e=MatchEngine.FromTestState(s);
    Resolve(e);
    Check(e.State.PendingChoice is null,"No choice needed with only one Conqueror candidate");
    Equal(e.State.Cards[conqueror.Id].Zone,Zone.Home); Check(!e.State.Cards[conqueror.Id].Bowed,"The sole Conqueror candidate returns home unbowed");
    Equal(e.State.Cards[other.Id].Zone,Zone.Home); Check(e.State.Cards[other.Id].Bowed,"A non-Conqueror attacking unit still returns home bowed");

    // Two eligible Conquerors: the attacker must choose which one straightens.
    var s2=StrongholdBattle("oracle-l5r-1218");
    var c1=Add(s2,"training-conqueror",0,Zone.Battlefield,province:0);
    var c2=Add(s2,"training-conqueror",0,Zone.Battlefield,province:0);
    var weak2=Add(s2,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, attacker wins 4-3
    var e2=MatchEngine.FromTestState(s2);
    Resolve(e2);
    Equal(e2.State.PendingChoice?.Type,"conqueror","Two eligible Conquerors: a real choice is required");
    Check(e2.State.Cards[c1.Id].Zone==Zone.Battlefield && e2.State.Cards[c2.Id].Zone==Zone.Battlefield,"Resolution defers until the choice is made");
    Reject(e2,new GameCommand("chooseConqueror",0,e2.Version,TargetId:weak2.Id)); // not an eligible candidate
    Accept(e2,new GameCommand("chooseConqueror",0,e2.Version,TargetId:c1.Id));
    Check(e2.State.PendingChoice is null && e2.State.Cards[c1.Id].Zone==Zone.Home && !e2.State.Cards[c1.Id].Bowed,"Chosen Conqueror returns home unbowed");
    Check(e2.State.Cards[c2.Id].Zone==Zone.Home && e2.State.Cards[c2.Id].Bowed,"The other eligible Conqueror still returns home bowed");
}
static void KensaiAndKamaWeaponCap()
{
    // A Kensai may attach two Weapons but not a third.
    var s=MatchEngine.CreateEconomyScenario().State;
    s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
    var kensai=Add(s,"training-kensai",0,Zone.Home);
    var w1=Add(s,"training-weapon",0,Zone.Hand);
    var w2=Add(s,"training-weapon",0,Zone.Hand);
    var w3=Add(s,"training-weapon",0,Zone.Hand);
    var e=MatchEngine.FromTestState(s);
    // Equip is a Limited action: it passes priority to the opponent (like any action), so they must
    // act (or pass) before it returns to player 0 for a second equip.
    Accept(e,Cmd(e,"equip",player:0,id:w1.Id,sources:e.PaymentOptions(0,1).First().SourceIds,target:kensai.Id));
    Accept(e,Cmd(e,"pass",player:1));
    Accept(e,Cmd(e,"equip",player:0,id:w2.Id,sources:e.PaymentOptions(0,1).First().SourceIds,target:kensai.Id));
    Equal(e.State.Cards.Values.Count(c=>c.AttachedTo==kensai.Id),2,"Kensai carries two Weapons");
    Accept(e,Cmd(e,"pass",player:1));
    Reject(e,Cmd(e,"equip",player:0,id:w3.Id,target:kensai.Id));

    // A non-Kensai personality with a Kama may attach a second Weapon, but still not a third.
    var s2=MatchEngine.CreateEconomyScenario().State;
    s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
    var plain=Add(s2,"training-crane-samurai",0,Zone.Home);
    var kama=Add(s2,"training-kama",0,Zone.Hand);
    var pw1=Add(s2,"training-weapon",0,Zone.Hand);
    var pw2=Add(s2,"training-weapon",0,Zone.Hand);
    var e2=MatchEngine.FromTestState(s2);
    Accept(e2,Cmd(e2,"equip",player:0,id:pw1.Id,sources:e2.PaymentOptions(0,1).First().SourceIds,target:plain.Id));
    Accept(e2,Cmd(e2,"pass",player:1));
    Accept(e2,Cmd(e2,"equip",player:0,id:kama.Id,sources:e2.PaymentOptions(0,1).First().SourceIds,target:plain.Id));
    Equal(e2.State.Cards.Values.Count(c=>c.AttachedTo==plain.Id),2,"A plain Weapon plus a Kama: two Weapons total");
    Accept(e2,Cmd(e2,"pass",player:1));
    Reject(e2,Cmd(e2,"equip",player:0,id:pw2.Id,target:plain.Id));

    // A Kensai wielding a Kama is still capped at two Weapons -- the two allowances do not stack.
    var s3=MatchEngine.CreateEconomyScenario().State;
    s3.Phase=Phase.Action; s3.PriorityPlayerId=0; s3.ActivePlayerId=0;
    var kensai2=Add(s3,"training-kensai",0,Zone.Home);
    var kama2=Add(s3,"training-kama",0,Zone.Hand);
    var kw1=Add(s3,"training-weapon",0,Zone.Hand);
    var kw2=Add(s3,"training-weapon",0,Zone.Hand);
    var e3=MatchEngine.FromTestState(s3);
    Accept(e3,Cmd(e3,"equip",player:0,id:kama2.Id,sources:e3.PaymentOptions(0,1).First().SourceIds,target:kensai2.Id));
    Accept(e3,Cmd(e3,"pass",player:1));
    Accept(e3,Cmd(e3,"equip",player:0,id:kw1.Id,sources:e3.PaymentOptions(0,1).First().SourceIds,target:kensai2.Id));
    Equal(e3.State.Cards.Values.Count(c=>c.AttachedTo==kensai2.Id),2,"Kensai + Kama: still two Weapons total");
    Accept(e3,Cmd(e3,"pass",player:1));
    Reject(e3,Cmd(e3,"equip",player:0,id:kw2.Id,target:kensai2.Id));
}
static void CavalryManeuversSegment()
{
    var s=MatchEngine.CreateStarterMatch().State; s.Phase=Phase.AttackDeclaration; s.PriorityPlayerId=0; s.ActivePlayerId=0;
    var infantry=Add(s,"training-crane-samurai",0,Zone.Home); // no Cavalry keyword: Infantry
    var soloCavalry=Add(s,"training-cavalry",0,Zone.Home); // Cavalry keyword, no Followers: Cavalry
    var mixedUnit=Add(s,"training-cavalry",0,Zone.Home); // Cavalry Personality...
    Add(s,"training-follower",0,Zone.Attached,attachedTo:mixedUnit.Id); // ...+ a non-Cavalry Follower: still Infantry
    var pureCavalryUnit=Add(s,"training-cavalry",0,Zone.Home); // Cavalry Personality...
    Add(s,"training-cavalry-follower",0,Zone.Attached,attachedTo:pureCavalryUnit.Id); // ...+ a Cavalry Follower: Cavalry
    var e=MatchEngine.FromTestState(s);

    // Infantry Maneuvers: only the two Infantry units (plain, and the mixed Cavalry+non-Cavalry-Follower one).
    Reject(e,Cmd(e,"attack",player:0,assignments:new(){[soloCavalry.Id]=0}));
    Reject(e,Cmd(e,"attack",player:0,assignments:new(){[pureCavalryUnit.Id]=0}));
    Accept(e,Cmd(e,"attack",player:0,assignments:new(){[infantry.Id]=0,[mixedUnit.Id]=1}));
    Equal(e.State.Cards[infantry.Id].Zone,Zone.Battlefield);
    Equal(e.State.Cards[mixedUnit.Id].Zone,Zone.Battlefield,"A Cavalry Personality with a non-Cavalry Follower assigns as Infantry");
    Equal(e.State.Phase,Phase.DefenseAssignment);

    Accept(e,Cmd(e,"assignDefense",player:1,assignments:[]));
    Equal(e.State.Phase,Phase.CavalryDeclaration,"Cavalry Maneuvers follows Infantry Maneuvers");

    // Cavalry Maneuvers: only the two genuine Cavalry units (both members of the unit carry Cavalry).
    Accept(e,Cmd(e,"attack",player:0,assignments:new(){[soloCavalry.Id]=0,[pureCavalryUnit.Id]=1}));
    Equal(e.State.Cards[soloCavalry.Id].Zone,Zone.Battlefield);
    Equal(e.State.Cards[pureCavalryUnit.Id].Zone,Zone.Battlefield,"A Cavalry Personality with a Cavalry Follower assigns as Cavalry");
    Equal(e.State.Phase,Phase.CavalryDefense);

    Accept(e,Cmd(e,"assignDefense",player:1,assignments:[]));
    Equal(e.State.Phase,Phase.BattleSelection,"Both Maneuvers segments complete before Battle Selection");
}
static void TwoHandedWeaponOverridesKensaiAndKama()
{
    // A Kensai may attach a Two-Handed Weapon as their only Weapon...
    var s=MatchEngine.CreateEconomyScenario().State;
    s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
    var kensai=Add(s,"training-kensai",0,Zone.Home);
    var twoHanded=Add(s,"training-two-handed-weapon",0,Zone.Hand);
    var plain=Add(s,"training-weapon",0,Zone.Hand);
    var e=MatchEngine.FromTestState(s);
    Accept(e,Cmd(e,"equip",player:0,id:twoHanded.Id,sources:e.PaymentOptions(0,1).First().SourceIds,target:kensai.Id));
    Equal(e.State.Cards.Values.Count(c=>c.AttachedTo==kensai.Id),1);
    // ...but it then blocks any further Weapon, even for a Kensai -- Two-Handed overrides the cap.
    Accept(e,Cmd(e,"pass",player:1));
    Reject(e,Cmd(e,"equip",player:0,id:plain.Id,target:kensai.Id));

    // A Kensai with a plain Weapon already attached cannot then attach a Two-Handed one either.
    var s2=MatchEngine.CreateEconomyScenario().State;
    s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
    var kensai2=Add(s2,"training-kensai",0,Zone.Home);
    var plain2=Add(s2,"training-weapon",0,Zone.Hand);
    var twoHanded2=Add(s2,"training-two-handed-weapon",0,Zone.Hand);
    var e2=MatchEngine.FromTestState(s2);
    Accept(e2,Cmd(e2,"equip",player:0,id:plain2.Id,sources:e2.PaymentOptions(0,1).First().SourceIds,target:kensai2.Id));
    Accept(e2,Cmd(e2,"pass",player:1));
    Reject(e2,Cmd(e2,"equip",player:0,id:twoHanded2.Id,target:kensai2.Id));
}
static void DaigotsuKendoConquerorAndIkomaNatsuCavalry()
{
    // Daigotsu Kendo carries Conqueror: winning a battle straightens him instead of bowing him.
    var s=StrongholdBattle("oracle-l5r-1218");
    var kendo=Add(s,"oracle-l5r-1736",0,Zone.Battlefield,province:0); // Force 3, no defenders present
    var e=MatchEngine.FromTestState(s);
    Resolve(e);
    Check(!e.State.Cards[kendo.Id].Bowed,"Conqueror: Kendo returns home unbowed after winning");
    Equal(e.State.Cards[kendo.Id].Zone,Zone.Home);

    // Ikoma Natsu carries Cavalry: assigns during Cavalry Maneuvers, not Infantry Maneuvers.
    var s2=MatchEngine.CreateStarterMatch().State; s2.Phase=Phase.AttackDeclaration; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
    var natsu=Add(s2,"oracle-l5r-3623",0,Zone.Home);
    var e2=MatchEngine.FromTestState(s2);
    Reject(e2,Cmd(e2,"attack",player:0,assignments:new(){[natsu.Id]=0}));
    Accept(e2,Cmd(e2,"attack",player:0,assignments:[]));
    Accept(e2,Cmd(e2,"assignDefense",player:1,assignments:[]));
    Equal(e2.State.Phase,Phase.CavalryDeclaration);
    Accept(e2,Cmd(e2,"attack",player:0,assignments:new(){[natsu.Id]=0}));
    Equal(e2.State.Cards[natsu.Id].Zone,Zone.Battlefield,"Cavalry-keyword Natsu assigns during Cavalry Maneuvers");
}
static void MirumotoReiyuForcePenalty()
{
    var s=StrongholdBattle("oracle-l5r-1218");
    var reiyu=Add(s,"oracle-l5r-5138",0,Zone.Battlefield,province:0); // Force 4
    var target=Add(s,"training-crane-veteran",1,Zone.Battlefield,province:0); // Force 5
    var e=MatchEngine.FromTestState(s);
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:reiyu.Id,TargetId:target.Id,AbilityId:"reiyu-force-penalty"));
    Equal(e.GetView(0).Players[1].Army.Single(c=>c.Id==target.Id).Force,1,"Force 5 - 4 (Reiyu's current Force) = 1");
}
static void ShinjoHoribeBowOrStraighten()
{
    // Not opposed: rejected.
    var s=StrongholdBattle("oracle-l5r-1218");
    var horibe=Add(s,"oracle-l5r-6885",0,Zone.Battlefield,province:0);
    var e=MatchEngine.FromTestState(s);
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:horibe.Id,TargetId:horibe.Id,AbilityId:"horibe-bow"));

    // Opposed: may bow or straighten any Personality, including an enemy's.
    var s2=StrongholdBattle("oracle-l5r-1218");
    var horibe2=Add(s2,"oracle-l5r-6885",0,Zone.Battlefield,province:0);
    var enemy=Add(s2,"training-crane-samurai",1,Zone.Battlefield,province:0);
    var e2=MatchEngine.FromTestState(s2);
    Accept(e2,new GameCommand("useAbility",0,e2.Version,CardId:horibe2.Id,TargetId:enemy.Id,AbilityId:"horibe-bow"));
    Check(e2.State.Cards[enemy.Id].Bowed,"Bows the target enemy Personality");
    // Bow and straighten are the same single printed ability's two effects, not two independent
    // once-per-turn abilities: having just bowed, straightening (even a different target) is spent too.
    Reject(e2,new GameCommand("useAbility",0,e2.Version,CardId:horibe2.Id,TargetId:horibe2.Id,AbilityId:"horibe-straighten"));
}
static void DojiAtsumichiBowOrStraighten()
{
    var s=StrongholdBattle("oracle-l5r-1218");
    var atsumichi=Add(s,"oracle-l5r-2060",0,Zone.Battlefield,province:0); // Personal Honor 4
    var lowerHonor=Add(s,"training-crane-samurai",1,Zone.Battlefield,bowed:true,province:0); // PH 2
    var equalHonor=Add(s,"training-lion-dignitary",1,Zone.Battlefield,province:0); // PH 4: not lower
    var another=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // a second eligible lower-Honor target
    var e=MatchEngine.FromTestState(s);
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:atsumichi.Id,TargetId:equalHonor.Id,AbilityId:"atsumichi-bow"));
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:atsumichi.Id,TargetId:lowerHonor.Id,AbilityId:"atsumichi-straighten"));
    Check(!e.State.Cards[lowerHonor.Id].Bowed,"Straightens the lower-Honor target");
    // Bow and straighten are the same single printed ability's two effects, not two independent
    // once-per-turn abilities: having just straightened, bowing (even a different target) is spent too.
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:atsumichi.Id,TargetId:another.Id,AbilityId:"atsumichi-bow"));
}
static void TetsuoDestroyAttachmentAndWeakerWeaponed()
{
    var s=StrongholdBattle("oracle-l5r-1218");
    var tetsuo=Add(s,"oracle-l5r-7943",0,Zone.Battlefield,province:0);
    Add(s,"training-weapon",0,Zone.Attached,attachedTo:tetsuo.Id,province:0); // Tetsuo carries 1 Weapon
    var fewerWeapons=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // 0 Weapons
    var notFewerWeapons=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
    Add(s,"training-weapon",1,Zone.Attached,attachedTo:notFewerWeapons.Id,province:0); // 1 Weapon: not fewer
    var attachment=Add(s,"training-item",1,Zone.Attached,attachedTo:notFewerWeapons.Id,province:0);
    var e=MatchEngine.FromTestState(s);
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:tetsuo.Id,TargetId:notFewerWeapons.Id,AbilityId:"tetsuo-destroy-weaker-weaponed"));
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:tetsuo.Id,TargetId:fewerWeapons.Id,AbilityId:"tetsuo-destroy-weaker-weaponed"));
    Equal(e.State.Cards[fewerWeapons.Id].Zone,Zone.Dead);
    Accept(e,new GameCommand("pass",1,e.Version));
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:tetsuo.Id,TargetId:attachment.Id,AbilityId:"tetsuo-destroy-attachment"));
    Equal(e.State.Cards[attachment.Id].Zone,Zone.FateDiscard);
}
static void MotoShunsukeForceBonusAndMakeDead()
{
    var s=StrongholdBattle("oracle-l5r-1218");
    var shunsuke=Add(s,"oracle-l5r-5371",0,Zone.Battlefield,province:0); // base Force 3
    Add(s,"training-crane-samurai",1,Zone.Dead); // player 1's dead pile: 2
    Add(s,"training-crane-samurai",1,Zone.Dead);
    Add(s,"training-lion-samurai",0,Zone.Dead); // player 0's (Shunsuke's own) dead pile: 1
    var e=MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==shunsuke.Id).Force,5,"Base 3 + the largest single dead pile (2)");

    var s2=MatchEngine.CreateEconomyScenario().State;
    s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
    var shunsuke2=Add(s2,"oracle-l5r-5371",0,Zone.Home);
    var discardedEnemy=Add(s2,"training-crane-samurai",1,Zone.DynastyDiscard);
    var ownDiscarded=Add(s2,"training-lion-samurai",0,Zone.DynastyDiscard);
    var e2=MatchEngine.FromTestState(s2);
    Reject(e2,new GameCommand("useAbility",0,e2.Version,CardId:shunsuke2.Id,TargetId:ownDiscarded.Id,AbilityId:"shunsuke-make-dead"));
    Accept(e2,new GameCommand("useAbility",0,e2.Version,CardId:shunsuke2.Id,TargetId:discardedEnemy.Id,AbilityId:"shunsuke-make-dead"));
    Equal(e2.State.Cards[discardedEnemy.Id].Zone,Zone.Dead,"Becomes honorably dead");
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

static void BowedPersonalityResolution()
{
    foreach(var owner in new[]{0,1})
    {
        var s=Battle(1,1,6).State;
        var p=s.Cards.Values.Single(c=>c.OwnerId==owner && c.Zone==Zone.Battlefield);
        p.DefinitionId=PublishedContent.TestSnapshot.Cards.Single(d=>d.Name=="Akodo Kisho").Id; // Force 3, Chi 3
        p.Modifiers.Clear(); p.Bowed=true;
        var guide=PublishedContent.TestSnapshot.Cards.Single(d=>d.Name=="Native Guide").Id;
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

static void BattleJournalProjection()
{
    var state=MatchEngine.CreateEconomyScenario().State;
    state.Phase=Phase.Battle; state.CurrentBattleProvince=0; state.UnresolvedBattlefields=[0]; state.PriorityPlayerId=1;
    state.Cards.Add("public-attacker",new CardInstance{Id="public-attacker",DefinitionId="training-crane-veteran",OwnerId=0,Zone=Zone.Battlefield,ProvinceIndex=0,FaceUp=true});
    state.Cards.Add("public-defender",new CardInstance{Id="public-defender",DefinitionId="training-lion-veteran",OwnerId=1,Zone=Zone.Battlefield,ProvinceIndex=0,FaceUp=true,Modifiers=[new("fixture","fixture","Force",-5)]});
    var engine=MatchEngine.FromTestState(state); var journal=new BattleJournal(); var before=engine.GetView(0);
    journal.Observe(before,null); var revision=journal.Revision;
    journal.Observe(before,before); Check(journal.Revision==revision,"Rerender reinserted events.");
    Check(engine.Execute(new("pass",1,engine.Version)).Accepted,"First pass rejected.");
    var intermediate=engine.GetView(0); journal.Observe(intermediate,before);
    Check(engine.Execute(new("pass",0,engine.Version)).Accepted,"Second pass rejected.");
    var after=engine.GetView(0); journal.Observe(after,intermediate);
    Check(journal.Exchanges.Count(e=>e.Events[0].Type=="passed")==2,"Lost either player's pass.");
    var resolution=journal.Exchanges.Single(e=>e.Events[0].Type=="battleResolved");
    Check(resolution.Events.Any(e=>e.Type=="unitDestroyed"),"Destruction separated from its resolution cause.");
    Check(journal.Aftermath.Any(r=>r.Destination=="Dead") && journal.Aftermath.Any(r=>r.Destination=="Home · bowed"),"Aftermath lost actual destinations.");
    var count=journal.Exchanges.Count; journal.Observe(after,after); Check(count==journal.Exchanges.Count,"Duplicate outcome on rerender.");
    var restored=new BattleJournal(); restored.Observe(after,null);
    Check(restored.Aftermath.Count==0,"Reconnect invented historical movement destinations.");
    Check(restored.Exchanges.SelectMany(x=>x.Events).All(e=>after.Events.Contains(e)),"Journal invented a public event.");
    restored.Reset(); Check(restored.Exchanges.Count==0 && restored.LastSequence==-1,"New match retained previous history.");
}
static void SpellsAndShugenja()
{
    {
        // Not offered at all (not merely disabled) without a Spell in hand, even with an eligible Shugenja.
        var s=StrongholdBattle("oracle-l5r-1218");
        Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Check(!e.QueryActions(0).Any(a=>a.Command?.AbilityId=="equip-battle"),"No Spell in hand: Equip (Battle) should not be offered");
    }
    {
        // Not offered without an opposed unbowed Shugenja, even with a Spell in hand.
        var s=StrongholdBattle("oracle-l5r-1218");
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Check(!e.QueryActions(0).Any(a=>a.Command?.AbilityId=="equip-battle"),"No opposed unbowed Shugenja: Equip (Battle) should not be offered");
    }
    {
        // A successful equip attaches the Spell (paying its cost) and opens the immediate follow-up window,
        // keeping priority with the equipping player throughout.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Any(a=>a.Command?.AbilityId=="equip-battle"),"Eligible Shugenja and Spell: Equip (Battle) should be offered");
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell.Id,SourceIds:payment.SourceIds));
        Equal(e.State.Cards[spell.Id].Zone,Zone.Attached); Equal(e.State.Cards[spell.Id].AttachedTo,shugenja.Id);
        Equal(e.State.Cards[spell.Id].ProvinceIndex,0);
        Check(!e.State.Players[0].Hand.Contains(spell.Id),"Spell removed from hand");
        Equal(e.State.PendingChoice?.Type,"spellFollowUp");
        Equal(e.State.PriorityPlayerId,0,"Priority stays with the equipping player during the follow-up window");
    }
    {
        // Using the Spell's own Battle ability during the follow-up window succeeds, closes the window,
        // and hands priority onward once resolved.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell.Id,SourceIds:payment.SourceIds));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:spell.Id,AbilityId:"training-spell-force-boost",TargetId:shugenja.Id));
        Check(e.State.PendingChoice is null,"Window closes after the follow-up ability resolves");
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==shugenja.Id).Force,3,"+2 Force applied on top of base Force 1");
        Equal(e.State.PriorityPlayerId,1,"Priority hands onward once the window closes");
    }
    {
        // Declining the follow-up also closes the window and hands priority onward.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell.Id,SourceIds:payment.SourceIds));
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Check(e.State.PendingChoice is null); Equal(e.State.PriorityPlayerId,1);
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==shugenja.Id).Force,1,"Declined: no Force bonus applied");
    }
    {
        // Only the just-attached Spell's own ability may be used in the follow-up window -- a useAbility
        // naming any other card is rejected outright, regardless of that card's own ability.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        var other=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell.Id,SourceIds:payment.SourceIds));
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:other.Id,AbilityId:"training-spell-force-boost",TargetId:other.Id));
    }
    {
        // Tireless: any number of times per turn. A second Equip (Battle) succeeds the same turn, right
        // after the first follow-up closes and the opponent hands priority back with a pass.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell1=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var spell2=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment1=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell1.Id,SourceIds:payment1.SourceIds));
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Equal(e.State.PriorityPlayerId,1);
        Accept(e,new GameCommand("pass",1,e.Version));
        var payment2=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell2.Id,SourceIds:payment2.SourceIds));
        Equal(e.State.Cards[spell2.Id].Zone,Zone.Attached,"Tireless: usable again the same turn");
    }
}
