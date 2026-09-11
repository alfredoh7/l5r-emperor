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
    ("A Stronghold's Honor-loss immunity negates a matching Clan or Fate source the losing player owns, and only that", HonorLossImmunity),
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
    ("Equip (Battle) attaches a Spell to an opposed unbowed Shugenja, opens a bounded immediate follow-up window for its Battle ability, and is usable any number of times per turn", SpellsAndShugenja),
    ("Loyal restricts recruitment to a Clan the Personality is aligned with; a multi-Clan Personality recruits for any of its alignments", LoyalKeyword),
    ("A keyword with no mechanical ability of its own still satisfies a generic 'choose your performing/target [Keyword] Personality' condition, on both Target and Performer selection", KeywordSatisfaction),
    ("Strategy wave 1: Immovable Object (Battle/Open dual timing, both abilities), Blind Rage, Chokehold, and Unorthodox Attack", StrategyWave1KeywordCards),
    ("Follower wave 2: 11 cards found by auditing every not-yet-imported Follower for ones needing nothing beyond already-built engine pieces", FollowerWave2Cards),
    ("Follower wave 3: 28 cards, including the three real cards behind this session's aggregate/group Force effects (Senior Infantry Legion, Ashigaru Elite, Utaku Elite Guard) and province adjacency's first consumer (Moto Gunso); AttachGrantsKeyword's first published use; several new small primitives (Force-swap, destruction-triggered Honor, wielder-keyed Follower Force, Melee-only immunity, compound OR/controller-scoped targets)", FollowerWave3Cards),
    ("The 'after entering play' Reaction window now opens from Equip and a played Terrain too, not just Purchase; it is repeatable per entry, and correctly cedes priority to the opponent for Action-phase entries", GeneralizedEntryReactionWindow),
    ("A Follower's own static conditional Force stays with the Follower (visible for targeting even bowed, joins the unit total only unbowed/Elite); an Item's printed and conditional Force/Chi always convey to its wielder regardless of the Item's own bow state", FollowerAndItemStaticForceHooks),
    ("'Boost other Followers in the unit' primitive (Senior Infantry Legion's shape): a static +1F to every OTHER Follower attached to the same Personality, never to itself, and never leaking into a different unit; two boosters in one unit grant each other but not themselves", UnitForceBoostPrimitive),
    ("Ashigaru Elite's shape: an activated, tireless, stacking Battle ability -- bow an unbowed Farm to give +1F to every Ashigaru-keyword card the performer controls at the current battlefield, army-scoped (not just one unit); unaffected: a non-Ashigaru card, and the enemy's own Ashigaru card; the per-battle counter resets once the battle resolves", AshigaruEliteBoostPrimitive),
    ("Expendable/Destined/Kharmic keyword mechanisms: a mandatory Fate-deck draw on destruction (not a plain discard) for Expendable; a mandatory Fate-deck draw on entering play (Purchase or Equip alike) for Destined; a repeatable pay-2-Gold-and-discard action for Kharmic, refilling a Dynasty province face up or drawing an extra Fate card", KeywordMechanics),
    ("Passive Attack-strength defense (Destrier/Singh Remnants' shape, Scope Self/Unit/Wielder, kind-specific) and a new 'any Personality enters play' Reaction letting a card in hand or a discard pile attach itself (Singh Remnants' shape), following Equip's own restrictions including Personal Honor", PassiveAttackDefenseAndEntryWatch),
    ("Follower wave 5 / Item wave 1: Scourge of the Sea, Unparalleled Sword, and Justice of the Crane -- three more AttachGrantsKeyword carriers (Naval/Kensai/Magistrate), GrantKeyword's idempotency guard, a new Battle Dishonor action, and (once Additional Actions exist) Justice of the Crane's own printed-Magistrate bonus", GrantsKeywordWave),
    ("The Additional Action primitive (issue #181): Again!'s unscoped grant, Two Hands' unit-scoped grant and its 'no new grants this battle' restriction, Effortless Counterattack's negating Reaction and its own unrelated move-home Battle action, and Equip (Battle)'s bespoke follow-up window replaced by a genuine scoped grant", AdditionalActionPrimitive),
    ("Stalwart (issue #184): the first enemy-caused bow each turn is negated (and only the bow), a second succeeds, the card's own controller bowing it is never affected, and a stale marker from an earlier turn does not block this turn's negation", StalwartKeyword),
    ("Reserve (issue #184): a Dynasty Personality recruited as an absent battle action straight to the battlefield (no presence of the recruiter's own required, only enemy presence, face up only), and a non-Spell Fate card attached via Equip (Battle) the same way a Spell is, but without the bonus Additional Action", ReserveKeyword),
    ("Discipline (issue #184): a card played from a discard pile, paying its own printed Gold cost, resolving its effect, then removed from the game (the new Zone.RemovedFromGame/PlayerView.RemovedFromGame) rather than left in the discard pile; unusable from hand; unaffected by an earlier, unrelated discard the same turn", DisciplineKeyword),
    ("Dishonor/Rehonor (issue #186): the Personal-Honor override (Utaku Chikako's shape), GainHonorFromPerformer negating a dishonorable performer's own Honor-granting ability and rehonoring them instead, and battle resolution rehonoring a dishonorable winner/tier and negating the full Honor gain (a tie's rehonoring happens before its own mutual destruction, so no double penalty)", DishonorRehonorMechanics),
    ("Utaku Chikako (issue #186): the real card's own Personal-Honor override, and her Limited ability (gain 2 Honor; may not declare an attack this turn, a broad restriction on the assignment itself that resets next turn)", UtakuChikako),
    ("Utaku Elite Guard's shape: a static +1F, keyword-based (Battle Maiden) and army-scoped (every OTHER Battle-Maiden card the owner controls at the same battlefield, not just one unit) -- unlike Senior Infantry Legion's type-based, unit-scoped boost; unaffected: a non-Battle-Maiden card, a Battle Maiden not at this battlefield, and the enemy's own Battle Maiden; two guards in one army grant each other but not themselves", ArmyBattleMaidenBoostPrimitive),
    ("An attachment's 'Will only/not attach to X' restriction (keyword, the Human-is-not-Nonhuman special case, or a named Personality) gates Equip, both at Accept and in the offered legal-targets list; the Follower Honor Requirement now reads current, not printed, Personal Honor -- fixed at 0 while Dishonorable, with a Personal-Honor-boosting token still applying once honorable", AttachmentRestrictions),
    ("'Without attachments' is a boolean, not a count: any Item/Follower/Spell, or a Follower/Weapon/Armor token, blocks a Ranged/Melee Attack target and any ability flagged RequireNoAttachments (layered onto an existing category); an ordinary stat token does not, and an attachment itself is always a legal 'without attachments' target", NoAttachmentsFilter),
    ("Province adjacency: a destroyed province is skipped, its former neighbors become directly adjacent, and this compounds across multiple destructions -- matching every worked example given, including both ends destroyed", ProvinceAdjacency)
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
static void HonorLossImmunity()
{
    {
        // Fate-card source (Clan is irrelevant here -- the Strategy is Unaligned): a Battle ability
        // that costs its own controller Honor to use is negated by the acting player's Stronghold.
        var s=StrongholdBattle("training-honor-immune-stronghold");
        Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var strategy=Add(s,"training-honor-cost-strategy",0,Zone.Hand);
        var honorBefore=s.Players[0].Honor;
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:strategy.Id,AbilityId:"training-honor-cost-attack"));
        Equal(e.State.Players[0].Honor,honorBefore,"Fate-card Honor cost negated by the immune Stronghold");
        Check(e.State.Events.Any(x=>x.Type=="honorLossNegated"));
    }
    {
        // Without the immune Stronghold, the identical cost applies normally.
        var s=StrongholdBattle("oracle-l5r-4059");
        Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var strategy=Add(s,"training-honor-cost-strategy",0,Zone.Hand);
        var honorBefore=s.Players[0].Honor;
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:strategy.Id,AbilityId:"training-honor-cost-attack"));
        Equal(e.State.Players[0].Honor,honorBefore-2);
    }
    {
        // Clan-matching source: a Spider Clan Personality's own "loses Honor after entering play"
        // Reaction is negated by the same Stronghold.
        var s=MatchEngine.CreateEconomyScenario().State;
        s.Cards[s.Players[0].StrongholdId].DefinitionId="training-honor-immune-stronghold";
        Add(s,"training-marketplace",0,Zone.Home);
        var cardId=s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId="training-spider-honor-loss"; s.Cards[cardId].FaceUp=true;
        var honorBefore=s.Players[0].Honor;
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,3).First();
        Accept(e,new GameCommand("purchase",0,e.Version,CardId:cardId,SourceIds:payment.SourceIds));
        Equal(e.State.PendingChoice?.Type,"entryReaction");
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:cardId,AbilityId:"training-honor-loss-entry"));
        Equal(e.State.Players[0].Honor,honorBefore,"Spider Clan Honor loss negated by the immune Stronghold");
    }
    {
        // A Dishonorable death from a card matching neither the immune Clan nor Fate is not negated,
        // even under the same Stronghold -- the check is scoped, not blanket.
        var s=Battle(9,2).State;
        s.Cards[s.Players[1].StrongholdId].DefinitionId="training-honor-immune-stronghold";
        var defender=s.Cards.Values.Single(c=>c.OwnerId==1 && c.Zone==Zone.Battlefield);
        defender.Dishonorable=true;
        var honorBefore=s.Players[1].Honor;
        var e=MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[defender.Id].Zone,Zone.Dead);
        Equal(e.State.Players[1].Honor,honorBefore-CardCatalog.Get("training-lion-veteran").PersonalHonor,"Lion Personality Honor loss is not negated by a Spider/Fate-scoped Stronghold");
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
        // A successful equip attaches the Spell (paying its cost) and grants an Additional Action
        // (issue #181) scoped to it, keeping priority with the equipping player. No Reaction is
        // available in this scenario, so it resolves immediately without opening a window.
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
        Check(e.State.PendingChoice is null,"No Reaction available to disrupt it -- resolves immediately");
        Check(e.State.AdditionalActions.Any(g=>g.PlayerId==0 && g.ScopeCardIds!.Contains(spell.Id)),"Grants an Additional Action scoped to the just-attached Spell");
        Equal(e.State.PriorityPlayerId,0,"Priority stays with the equipping player -- the granted action is still available");
    }
    {
        // Using the Spell's own Battle ability consumes the granted Additional Action and hands
        // priority onward once it resolves.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell.Id,SourceIds:payment.SourceIds));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:spell.Id,AbilityId:"training-spell-force-boost",TargetId:shugenja.Id));
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0),"Grant consumed");
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==shugenja.Id).Force,3,"+2 Force applied on top of base Force 1");
        Equal(e.State.PriorityPlayerId,1,"Priority hands onward once the granted action is used up");
    }
    {
        // Additional Actions may be passed (issue #181): passing forfeits the grant rather than using
        // it, same as an ordinary Battle-phase pass.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell.Id,SourceIds:payment.SourceIds));
        Accept(e,new GameCommand("pass",0,e.Version));
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0),"Passing forfeits the granted Additional Action");
        Equal(e.State.PriorityPlayerId,1);
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==shugenja.Id).Force,1,"Declined: no Force bonus applied");
    }
    {
        // Tireless (any number of times per turn) and scoped to exactly the Spell it was granted for
        // (issue #181): a second, independent Equip (Battle) succeeds immediately -- still holding
        // priority, since it creates its own fresh grant -- but forfeits the first Spell's still-
        // unused one as an unrelated action, rather than letting it linger.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var spell1=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var spell2=Add(s,"training-spell-with-ability",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment1=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell1.Id,SourceIds:payment1.SourceIds));
        var payment2=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:spell2.Id,SourceIds:payment2.SourceIds));
        Equal(e.State.Cards[spell2.Id].Zone,Zone.Attached,"Tireless: usable again the same turn, still holding priority");
        Equal(e.State.PriorityPlayerId,0);
        Check(!e.State.AdditionalActions.Any(g=>g.ScopeCardIds!.Contains(spell1.Id)),"Spell1's grant is forfeited, not carried over");
        Check(e.State.AdditionalActions.Any(g=>g.ScopeCardIds!.Contains(spell2.Id)),"Spell2's own fresh grant is still there");
    }
}
static void LoyalKeyword()
{
    {
        // A Loyal Personality recruits normally for a matching Clan.
        var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
        var cardId=s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId="training-loyal-crane";
        e=MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0,cardId).Error is null,"A Loyal Crane Personality recruits normally for a Crane player");
    }
    {
        // A Loyal Personality is not recruitable by a Clan it is not aligned with.
        var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
        s.Players[0].Clan="Lion"; var cardId=s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId="training-loyal-crane";
        e=MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0,cardId).Error is not null,"A Crane-Loyal Personality should not be recruitable by a Lion player");
        Reject(e,Cmd(e,"purchase",id:cardId,sources:[]));
    }
    {
        // A Personality aligned with more than one Clan (Clans overrides the single Clan field) may be
        // recruited by either -- Loyal restricts to "a Clan it is aligned with," not only the first.
        var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
        s.Players[0].Clan="Lion"; var cardId=s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId="training-loyal-multi-clan";
        e=MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0,cardId).Error is null,"A Crane/Lion dual-aligned Loyal Personality recruits normally for a Lion player");
    }
    {
        var e=MatchEngine.CreateEconomyScenario(); var s=e.State;
        var cardId=s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId="training-loyal-multi-clan";
        e=MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0,cardId).Error is null,"A Crane/Lion dual-aligned Loyal Personality recruits normally for a Crane player");
    }
}
static void KeywordSatisfaction()
{
    {
        // Target-side: "Choose your performing Berserker" -- a Personality carrying the Berserker
        // keyword (with no mechanical ability of its own) can be chosen and straightened; one without
        // it cannot, even though nothing else about it differs.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0;
        var satisfier=Add(s,"training-keyword-satisfier",0,Zone.Home);
        var berserker=Add(s,"training-berserker",0,Zone.Home,bowed:true);
        var other=Add(s,"training-crane-samurai",0,Zone.Home,bowed:true);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:satisfier.Id,AbilityId:"training-keyword-conditional-straighten",TargetId:other.Id));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:satisfier.Id,AbilityId:"training-keyword-conditional-straighten",TargetId:berserker.Id));
        Check(!e.State.Cards[berserker.Id].Bowed,"The chosen Berserker is straightened");
    }
    {
        // Performer-side: "Choose your performing Berserker at the current battlefield" -- the same
        // gate, mirrored onto Performer selection instead of Target selection.
        var s=StrongholdBattle("oracle-l5r-1218");
        var satisfier=Add(s,"training-keyword-satisfier",0,Zone.Battlefield,province:0);
        var berserker=Add(s,"training-berserker",0,Zone.Battlefield,province:0);
        var other=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:satisfier.Id,AbilityId:"training-keyword-conditional-force-boost",PerformerId:other.Id));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:satisfier.Id,AbilityId:"training-keyword-conditional-force-boost",PerformerId:berserker.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==berserker.Id).Force,4,"Base Force 2 + 2 from the boost");
    }
}
static void StrategyWave1KeywordCards()
{
    {
        // Immovable Object's first ability, the keyword-satisfaction primitive's own worked example:
        // usable via the Open route (dual Battle/Open timing) with no battle in progress at all.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0;
        var io=Add(s,"oracle-l5r-3671",0,Zone.Hand);
        var berserker=Add(s,"training-berserker",0,Zone.Home,bowed:true);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:io.Id,TargetId:berserker.Id,AbilityId:"immovable-object-straighten"));
        Check(!e.State.Cards[berserker.Id].Bowed,"Straightened via the Open route, with no battle in progress");
    }
    {
        // Immovable Object's second ability: Battle-timing, bows an enemy card with lower Force.
        var s=StrongholdBattle("oracle-l5r-1218");
        var io=Add(s,"oracle-l5r-3671",0,Zone.Hand);
        var berserker=Add(s,"training-berserker",0,Zone.Battlefield,province:0); // Force 2
        var weak=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); weak.Modifiers=[new("test-fixture","test-fixture","Force",-2)]; // Force 1
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:io.Id,PerformerId:berserker.Id,TargetId:weak.Id,AbilityId:"immovable-object-bow"));
        Check(e.State.Cards[weak.Id].Bowed,"Lower-Force enemy is bowed");
    }
    {
        // Blind Rage: +5F to the performing unbowed Berserker, Lose 2 Honor.
        var s=StrongholdBattle("oracle-l5r-1218");
        var blindRage=Add(s,"oracle-l5r-1044",0,Zone.Hand);
        var berserker=Add(s,"training-berserker",0,Zone.Battlefield,province:0); // Force 2
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:blindRage.Id,PerformerId:berserker.Id,AbilityId:"blind-rage"));
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==berserker.Id).Force,7,"Base Force 2 + 5");
        Equal(e.State.Players[0].Honor,honorBefore-2);
    }
    {
        // Chokehold: performing unbowed Monk bows an enemy Personality with equal or lower Chi.
        var s=StrongholdBattle("oracle-l5r-1218");
        var chokehold=Add(s,"oracle-l5r-1336",0,Zone.Hand);
        var monk=Add(s,"training-monk",0,Zone.Battlefield,province:0); // Chi 2
        var target=Add(s,"training-dishonored-samurai",1,Zone.Battlefield,province:0); // Chi 2
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:chokehold.Id,PerformerId:monk.Id,TargetId:target.Id,AbilityId:"chokehold"));
        Check(e.State.Cards[target.Id].Bowed,"Equal Chi satisfies 'equal or lower'");
    }
    {
        // Unorthodox Attack: reuses the pre-existing Samurai Performer and enemy-force Target
        // categories entirely -- no new engine pieces at all.
        var s=StrongholdBattle("oracle-l5r-1218");
        var unorthodox=Add(s,"oracle-l5r-8999",0,Zone.Hand);
        var samurai=Add(s,"training-samurai",0,Zone.Battlefield,province:0);
        var enemy=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:unorthodox.Id,PerformerId:samurai.Id,TargetId:enemy.Id,AbilityId:"unorthodox-attack"));
        Check(e.State.Cards[enemy.Id].Bowed);
    }
}
static void FollowerWave2Cards()
{
    {
        // Plain Ranged/Melee Attack Followers: no cost, no condition, matching UnconditionalAttackImportWave.
        foreach(var (cardId,abilityId,strength) in new[] { ("oracle-l5r-1839","dark-naga-scouts-attack",3), ("oracle-l5r-3456","horse-archers-attack",4) })
        {
            var s=StrongholdBattle("oracle-l5r-1218");
            var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
            var performer=Add(s,cardId,0,Zone.Attached,province:0,attachedTo:host.Id);
            var target=Add(s,"training-crane-veteran",1,Zone.Battlefield,province:0); // Force 5
            var e=MatchEngine.FromTestState(s);
            Reject(e,new GameCommand("useAbility",0,e.Version,CardId:performer.Id,TargetId:target.Id,AbilityId:abilityId));
            Accept(e,new GameCommand("useAbility",0,e.Version,CardId:performer.Id,AbilityId:abilityId));
            Equal(e.State.Cards[target.Id].Zone,Zone.Battlefield,$"{cardId}: strength {strength} misses Force 5");
        }
    }
    {
        // Cavalry Flankers: only the Attacker may use it, and it bows itself as part of the attack.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var flankers=Add(s,"oracle-l5r-10333",0,Zone.Attached,province:0,attachedTo:host.Id);
        var target=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); target.Modifiers=[new("test-fixture","test-fixture","Force",-1)]; // Force 2
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:flankers.Id,TargetId:target.Id,AbilityId:"cavalry-flankers-attack"));
        Check(e.State.Cards[flankers.Id].Bowed,"Bows itself as part of the attack");
        Equal(e.State.Cards[target.Id].Zone,Zone.Dead,"Melee 2 destroys Force 2");
        var s2=StrongholdBattle("oracle-l5r-1218"); s2.ActivePlayerId=1; s2.PriorityPlayerId=0;
        var host2=Add(s2,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var flankers2=Add(s2,"oracle-l5r-10333",0,Zone.Attached,province:0,attachedTo:host2.Id);
        var e2=MatchEngine.FromTestState(s2);
        Reject(e2,new GameCommand("useAbility",0,e2.Version,CardId:flankers2.Id,AbilityId:"cavalry-flankers-attack"));
    }
    {
        // Murderous Thieves: bows itself, no other condition.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var thieves=Add(s,"oracle-l5r-5445",0,Zone.Attached,province:0,attachedTo:host.Id);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:thieves.Id,AbilityId:"murderous-thieves-attack"));
        Check(e.State.Cards[thieves.Id].Bowed);
    }
    {
        // Veteran Advisor: draws a card, no cost.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var advisor=Add(s,"oracle-l5r-9144",0,Zone.Attached,province:0,attachedTo:host.Id);
        var e=MatchEngine.FromTestState(s);
        var handBefore=e.State.Players[0].Hand.Count;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:advisor.Id,AbilityId:"veteran-advisor-draw"));
        Equal(e.State.Players[0].Hand.Count,handBefore+1);
    }
    {
        // Hired Legion: bows itself, draws a card.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var legion=Add(s,"oracle-l5r-3277",0,Zone.Attached,province:0,attachedTo:host.Id);
        var e=MatchEngine.FromTestState(s);
        var handBefore=e.State.Players[0].Hand.Count;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:legion.Id,AbilityId:"hired-legion-draw"));
        Check(e.State.Cards[legion.Id].Bowed); Equal(e.State.Players[0].Hand.Count,handBefore+1);
    }
    {
        // Naga Remnants: bows a target enemy attachment.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var remnants=Add(s,"oracle-l5r-10513",0,Zone.Attached,province:0,attachedTo:host.Id);
        var enemyHost=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var enemyItem=Add(s,"training-item",1,Zone.Attached,province:0,attachedTo:enemyHost.Id);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:remnants.Id,TargetId:enemyItem.Id,AbilityId:"naga-remnants-bow"));
        Check(e.State.Cards[enemyItem.Id].Bowed);
    }
    {
        // Hiruma Sniper: only usable with Reconnaissance at the current battlefield.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var sniper=Add(s,"oracle-l5r-3325",0,Zone.Attached,province:0,attachedTo:host.Id);
        var target=Add(s,"training-crane-veteran",1,Zone.Battlefield,province:0); // Force 5
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:sniper.Id,TargetId:target.Id,AbilityId:"hiruma-sniper-attack"));

        var s2=StrongholdBattle("oracle-l5r-1218");
        s2.Players[1].Provinces[0].Reconnaissance[0]=true;
        var host2=Add(s2,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var sniper2=Add(s2,"oracle-l5r-3325",0,Zone.Attached,province:0,attachedTo:host2.Id);
        var target2=Add(s2,"training-lion-samurai",1,Zone.Battlefield,province:0); target2.Modifiers=[new("test-fixture","test-fixture","Force",-2)]; // Force 2
        var e2=MatchEngine.FromTestState(s2);
        Accept(e2,new GameCommand("useAbility",0,e2.Version,CardId:sniper2.Id,TargetId:target2.Id,AbilityId:"hiruma-sniper-attack"));
        Equal(e2.State.Cards[target2.Id].Zone,Zone.Dead,"Ranged 4 destroys Force 2 with Reconnaissance");
    }
    {
        // Heavy Chargers: only usable while opposed.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Battle; s.CurrentBattleProvince=0; s.UnresolvedBattlefields=[0]; s.PriorityPlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var chargers=Add(s,"oracle-l5r-9869",0,Zone.Attached,province:0,attachedTo:host.Id);
        var hand=Add(s,"training-focus-strategy",0,Zone.Hand); // Focus 3
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:chargers.Id,TargetId:hand.Id,AbilityId:"heavy-chargers-boost"));
    }
    {
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var chargers=Add(s,"oracle-l5r-9869",0,Zone.Attached,province:0,attachedTo:host.Id);
        var hand=Add(s,"training-focus-strategy",0,Zone.Hand); // Focus 3
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:chargers.Id,TargetId:hand.Id,AbilityId:"heavy-chargers-boost"));
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==chargers.Id).Force,6,"Base Force 3 + 3");
    }
    {
        // Spitting Llama: -3F to a target enemy Personality or Follower.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var llama=Add(s,"oracle-l5r-10686",0,Zone.Attached,province:0,attachedTo:host.Id);
        var target=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0); // Force 6
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:llama.Id,TargetId:target.Id,AbilityId:"spitting-llama-force"));
        Equal(e.GetView(0).Players[1].Army.Single(c=>c.Id==target.Id).Force,3,"Base Force 6 - 3");
    }
    {
        // Akodo Regulars: discards a card, boosts its own Force by the discard's Focus Value.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var regulars=Add(s,"oracle-l5r-262",0,Zone.Attached,province:0,attachedTo:host.Id);
        var hand=Add(s,"training-focus-strategy",0,Zone.Hand); // Focus 3
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:regulars.Id,TargetId:hand.Id,AbilityId:"akodo-regulars-boost"));
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==regulars.Id).Force,5,"Base Force 2 + 3");
    }
}
static void FollowerWave3Cards()
{
    {
        // Commander's Steed: AttachGrantsKeyword's first published use -- Conqueror. Cleanup on detach
        // is already covered generically by "Destroying an attachment clears the Attachment-duration
        // keyword it granted", so this only needs to prove the grant itself.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var steed=Add(s,"oracle-l5r-10893",0,Zone.Hand);
        var human=Add(s,"training-crane-samurai",0,Zone.Home);
        var nonhuman=Add(s,"training-nonhuman-personality",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Reject(e,Cmd(e,"equip",id:steed.Id,sources:e.PaymentOptions(0,4).First().SourceIds,target:nonhuman.Id));
        Accept(e,Cmd(e,"equip",id:steed.Id,sources:e.PaymentOptions(0,4).First().SourceIds,target:human.Id));
        Check(e.State.Cards[human.Id].GrantedKeywords.Any(g=>g.Keyword=="Conqueror"),"Attaching grants Conqueror to the wielder");
    }
    {
        // Seppun Heavy Elite (issue #179 correction): grants its wielder Elite's EFFECT (Force still
        // counts while bowed) WITHOUT granting the Elite keyword itself -- unlike Commander's Steed's
        // genuine keyword grant above, EliteEffect() reads AttachGrantsEliteEffect live off the
        // attachment, so no GrantedKeywords entry is needed or expected here.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0,bowed:true); // Force 3, bowed
        Add(s,"oracle-l5r-6596",0,Zone.Attached,province:0,attachedTo:host.Id);
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==host.Id).UnitForce,5,"Bowed, but Seppun's granted Elite EFFECT still contributes to BattleForce: host's 3 + Seppun's own unbowed 2");
        Check(!e.State.Cards[host.Id].GrantedKeywords.Any(g=>g.Keyword=="Elite"),"The wielder gains Elite's effect, not the Elite keyword itself");
    }
    {
        // Family Sensei / Merchant Guard: self-referential, ControlsKeyword reuse.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var sensei=Add(s,"oracle-l5r-10592",0,Zone.Attached,attachedTo:host.Id); // Force 2
        var guard=Add(s,"oracle-l5r-10589",0,Zone.Attached,attachedTo:host.Id); // Force 2
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==sensei.Id).Force,2,"No Dojo controlled -- base Force only");
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==guard.Id).Force,2,"No Market controlled -- base Force only");
        Add(s,"training-dojo",0,Zone.Home); Add(s,"training-market-holding",0,Zone.Home);
        var e2=MatchEngine.FromTestState(s);
        Equal(e2.GetView(0).Players[0].Home.Single(c=>c.Id==sensei.Id).Force,3,"Controls a Dojo -- +1F");
        Equal(e2.GetView(0).Players[0].Home.Single(c=>c.Id==guard.Id).Force,3,"Controls a Market -- +1F");
    }
    {
        // Ashigaru Guards: +1F for every two Farms controlled.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var guards=Add(s,"oracle-l5r-615",0,Zone.Home); // Force 0
        var e0=MatchEngine.FromTestState(s);
        Equal(e0.GetView(0).Players[0].Home.Single(c=>c.Id==guards.Id).Force,0,"No Farms -- no bonus");
        Add(s,"training-farm",0,Zone.Home); Add(s,"training-farm",0,Zone.Home);
        var e2=MatchEngine.FromTestState(s);
        Equal(e2.GetView(0).Players[0].Home.Single(c=>c.Id==guards.Id).Force,1,"Two Farms -- +1F");
        Add(s,"training-farm",0,Zone.Home);
        var e3=MatchEngine.FromTestState(s);
        Equal(e3.GetView(0).Players[0].Home.Single(c=>c.Id==guards.Id).Force,1,"Three Farms -- still +1F (integer division)");
    }
    {
        // Brothers of the Great Lake: self-count of same-named cards in the SAME ARMY, plus its own bow ability.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var lake1=Add(s,"oracle-l5r-10332",0,Zone.Attached,province:0,attachedTo:host.Id); // Force 3
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==lake1.Id).Force,3,"Alone -- no bonus");
        var lake2=Add(s,"oracle-l5r-10332",0,Zone.Attached,province:0,attachedTo:host.Id);
        var e2=MatchEngine.FromTestState(s);
        Equal(e2.GetView(0).Players[0].Army.Single(c=>c.Id==lake1.Id).Force,4,"One other same-named card in the army -- +1F");
        var enemyTarget=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
        var e3=MatchEngine.FromTestState(s);
        Accept(e3,new GameCommand("useAbility",0,e3.Version,CardId:lake1.Id,TargetId:enemyTarget.Id,AbilityId:"brothers-great-lake-bow"));
        Check(e3.State.Cards[enemyTarget.Id].Bowed,"Bows the target enemy card");
    }
    {
        // Ninja Guards: Force bonus equal to its wielder's own PRINTED Force.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-lion-veteran",0,Zone.Home); // printed Force 6
        var guards=Add(s,"oracle-l5r-5565",0,Zone.Attached,attachedTo:host.Id); // base Force 0
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==guards.Id).Force,6,"Base 0 + wielder's printed Force 6");
    }
    {
        // Disciples of Ganesh: "+2F while defending" (self, existing shape) plus a destruction-triggered
        // Honor gain (new shape) -- one card, two engine hooks off the same Effect string.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=1; // player 0 defends
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var ganesh=Add(s,"oracle-l5r-10895",0,Zone.Attached,province:0,attachedTo:host.Id); // Force 1
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==ganesh.Id).Force,3,"Base 1 + 2 while defending");
        var destroyer=Add(s,"training-no-attachments-destroyer",1,Zone.Battlefield,province:0);
        s.PriorityPlayerId=1;
        var e2=MatchEngine.FromTestState(s);
        var honorBefore=e2.State.Players[0].Honor;
        Accept(e2,new GameCommand("useAbility",1,e2.Version,CardId:destroyer.Id,TargetId:ganesh.Id,AbilityId:"training-destroy-no-attachments"));
        Equal(e2.State.Cards[ganesh.Id].Zone,Zone.Dead,"Destroyed during battle");
        Equal(e2.State.Players[0].Honor,honorBefore+1,"Its own controller gains 1 Honor from the destruction");
    }
    {
        // Daifuke: swaps its own Force with the target's, symmetrically.
        var s=StrongholdBattle("oracle-l5r-1218"); s.Phase=Phase.Action; s.PriorityPlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var daifuke=Add(s,"oracle-l5r-10235",0,Zone.Attached,attachedTo:host.Id); // Force 3
        var enemyHost=Add(s,"training-lion-veteran",1,Zone.Home); // Force 6
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:daifuke.Id,TargetId:enemyHost.Id,AbilityId:"daifuke-swap-force"));
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==daifuke.Id).Force,6,"Daifuke now has the enemy's old Force");
        Equal(e.GetView(1).Players[1].Home.Single(c=>c.Id==enemyHost.Id).Force,3,"The enemy now has Daifuke's old Force");
    }
    {
        // Moto Gunso: +1F to every OTHER Follower at the current battlefield AND each adjacent one.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var gunso=Add(s,"oracle-l5r-5320",0,Zone.Attached,province:0,attachedTo:host.Id); // Force 2
        var sameBattlefield=Add(s,"training-follower",0,Zone.Attached,province:0,attachedTo:host.Id); // Force 2
        var adjacentHost=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:1);
        var adjacent=Add(s,"training-follower",0,Zone.Attached,province:1,attachedTo:adjacentHost.Id); // Force 2, province 1 (adjacent to 0)
        var farHost=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:3);
        var far=Add(s,"training-follower",0,Zone.Attached,province:3,attachedTo:farHost.Id); // Force 2, province 3 (NOT adjacent to 0)
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:gunso.Id,AbilityId:"moto-gunso-boost"));
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==gunso.Id).Force,2,"Not boosted -- 'other' Followers only");
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==sameBattlefield.Id).Force,3,"Same battlefield -- +1F");
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==adjacent.Id).Force,3,"Adjacent battlefield -- +1F");
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==far.Id).Force,2,"Not adjacent -- unaffected");
    }
    {
        // Elite Sentry: "destroy without attachments" (existing #158 shape) and a Tireless straighten of
        // its own unit (wielder plus every attachment).
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0,bowed:true);
        var sentry=Add(s,"oracle-l5r-2304",0,Zone.Attached,province:0,attachedTo:host.Id); // unbowed -- its own ability needs to use it
        var otherFollower=Add(s,"training-follower",0,Zone.Attached,province:0,attachedTo:host.Id,bowed:true);
        var enemyNaked=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:sentry.Id,TargetId:enemyNaked.Id,AbilityId:"elite-sentry-destroy"));
        Equal(e.State.Cards[enemyNaked.Id].Zone,Zone.Dead,"Destroys the unattached enemy card");
        Accept(e,new GameCommand("pass",1,e.Version));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:sentry.Id,AbilityId:"elite-sentry-straighten"));
        Check(!e.State.Cards[host.Id].Bowed && !e.State.Cards[otherFollower.Id].Bowed,"Straightens the whole unit: wielder and every attachment");
    }
    {
        // Furugaro: destroys itself and gives the target Fudo two Charging tokens.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var furugaro=Add(s,"oracle-l5r-10236",0,Zone.Attached,attachedTo:host.Id);
        var fudo=Add(s,"training-fudo-personality",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:furugaro.Id,TargetId:fudo.Id,AbilityId:"furugaro-charge"));
        Equal(e.State.Cards[furugaro.Id].Zone,Zone.Dead,"Destroys itself");
        Equal(e.State.Cards[fudo.Id].Tokens.Count(t=>t.Type=="Charging"),2,"Gives the target Fudo two Charging tokens");
    }
    {
        // P'an Ku Cultist: entry Honor loss (own amount, -1) and a Limited ability giving a Madness token.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var hand=Add(s,"oracle-l5r-10590",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,Cmd(e,"equip",id:hand.Id,sources:e.PaymentOptions(0,3).First().SourceIds,target:host.Id));
        Equal(e.State.PendingChoice?.Type,"entryReaction");
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:hand.Id,AbilityId:"panku-entry-honor-loss"));
        Equal(e.State.Players[0].Honor,honorBefore-1,"Entering play costs 1 Honor");
        // panku-madness is a separate Limited ability, tested independently of the entry-reaction window.
        var s2=MatchEngine.CreateEconomyScenario().State; s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
        var cultist=Add(s2,"oracle-l5r-10590",0,Zone.Home);
        var target=Add(s2,"training-crane-samurai",0,Zone.Home);
        var e2=MatchEngine.FromTestState(s2);
        Accept(e2,new GameCommand("useAbility",0,e2.Version,CardId:cultist.Id,TargetId:target.Id,AbilityId:"panku-madness"));
        Check(e2.State.Cards[target.Id].Tokens.Any(t=>t.Type=="Madness"),"Gives the target a Madness token");
    }
    {
        // Soshi House Guard: conditional bow (only during a Combat Segment) plus a dishonorable-conditional
        // Honor loss.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var guard=Add(s,"oracle-l5r-7347",0,Zone.Attached,province:0,attachedTo:host.Id);
        var dishonorable=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); dishonorable.Dishonorable=true;
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[1].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:guard.Id,TargetId:dishonorable.Id,AbilityId:"soshi-conditional-bow"));
        Check(e.State.Cards[dishonorable.Id].Bowed,"Bows the target during the Battle phase");
        Equal(e.State.Players[1].Honor,honorBefore-1,"Dishonorable target's controller loses 1 Honor");
    }
    {
        // Unseen Assassins: bows itself, destroys a target Follower, then destroys itself and loses 2 Honor.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var assassin=Add(s,"oracle-l5r-10410",0,Zone.Attached,attachedTo:host.Id);
        var enemyHost=Add(s,"training-lion-samurai",1,Zone.Home);
        var enemyFollower=Add(s,"training-follower",1,Zone.Attached,attachedTo:enemyHost.Id);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:assassin.Id,TargetId:enemyFollower.Id,AbilityId:"unseen-assassin-strike"));
        Equal(e.State.Cards[enemyFollower.Id].Zone,Zone.Dead,"Destroys the target Follower");
        Equal(e.State.Cards[assassin.Id].Zone,Zone.Dead,"Destroys itself");
        Equal(e.State.Players[0].Honor,honorBefore-2,"Loses 2 Honor");
    }
    {
        // Asahina House Guard: straighten a target bowed Personality, gain 1 Honor.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-clan-samurai",0,Zone.Battlefield,province:0); // Crane Clan keyword
        var guard=Add(s,"oracle-l5r-494",0,Zone.Attached,province:0,attachedTo:host.Id);
        var bowedAlly=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0,bowed:true);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:guard.Id,TargetId:bowedAlly.Id,AbilityId:"asahina-straighten"));
        Check(!e.State.Cards[bowedAlly.Id].Bowed,"Straightens the target");
        Equal(e.State.Players[0].Honor,honorBefore+1,"Gains 1 Honor");
    }
    {
        // Bounty Hunter: bows a target attachment OR a target dishonorable Personality.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var hunter=Add(s,"oracle-l5r-1119",0,Zone.Attached,province:0,attachedTo:host.Id);
        var enemyHost=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
        var enemyAttachment=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:enemyHost.Id);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:hunter.Id,TargetId:enemyHost.Id,AbilityId:"bounty-hunter-bow")); // honorable Personality, not an attachment
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:hunter.Id,TargetId:enemyAttachment.Id,AbilityId:"bounty-hunter-bow"));
        Check(e.State.Cards[enemyAttachment.Id].Bowed,"Bows the target attachment");
    }
    {
        // Rumormonger: bows itself, targets a Personality whose controller controls a dishonorable
        // Personality (not necessarily the target itself), Force penalty equal to the target's own base PH.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=1; s.ActivePlayerId=1;
        var rumormonger=Add(s,"oracle-l5r-6404",1,Zone.Home);
        var dishonorableAlly=Add(s,"training-crane-clan-samurai",0,Zone.Home); dishonorableAlly.Dishonorable=true; // PH 3, current 0
        var otherAlly=Add(s,"training-crane-clan-low-honor",0,Zone.Home); // Force 2, PH 1
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",1,e.Version,CardId:rumormonger.Id,TargetId:otherAlly.Id,AbilityId:"rumormonger-penalty"));
        Check(e.State.Cards[rumormonger.Id].Bowed,"Bows itself as part of the cost");
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==otherAlly.Id).Force,1,"Force penalty equal to the target's own base Personal Honor (1): base 2 - 1");
    }
    {
        // Seasoned Ashigaru: immune to Melee Attacks specifically (not Ranged).
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var attacker=Add(s,"oracle-l5r-10106",0,Zone.Attached,province:0,attachedTo:host.Id); // Horseback Warriors, Melee
        var seasoned=Add(s,"oracle-l5r-10901",1,Zone.Battlefield,province:0); // Force 1, melee-immune
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:attacker.Id,TargetId:seasoned.Id,AbilityId:"horseback-warriors-attack"));
    }
    {
        // Horseback Warriors: Melee 4, or 5 against a target without the Cavalry keyword ("Infantry").
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var warriors=Add(s,"oracle-l5r-10106",0,Zone.Attached,province:0,attachedTo:host.Id);
        var infantry=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0); infantry.Modifiers=[new("test","test","Force",1)]; // Force 5
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:warriors.Id,TargetId:infantry.Id,AbilityId:"horseback-warriors-attack"));
        Equal(e.State.Cards[infantry.Id].Zone,Zone.Dead,"Melee 5 (target is Infantry -- no Cavalry keyword) destroys Force 5");
    }
    {
        // Khol Regulars: Ranged 4 Attack, and its own Reaction raises a pending attack's strength by 2
        // while it is at the current battlefield.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var regulars=Add(s,"oracle-l5r-4338",0,Zone.Attached,province:0,attachedTo:host.Id);
        var target=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, within base Ranged 4
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:regulars.Id,TargetId:target.Id,AbilityId:"khol-regulars-attack"));
        Equal(e.State.PendingAttack?.Strength,4,"Base Ranged 4");
        Accept(e,new GameCommand("declineReaction",1,e.Version));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:regulars.Id,AbilityId:"khol-regulars-reaction"));
        Equal(e.State.PendingAttack?.Strength,6,"4 base + 2 from its own Reaction");
        Accept(e,new GameCommand("declineReaction",1,e.Version));
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Equal(e.State.Cards[target.Id].Zone,Zone.Dead);
    }
    {
        // Aulus: entry Honor loss (own amount, -2) and a keyword-based unit-scoped boost ("Legionnaires
        // in this unit have +1F") -- reuses UnitFollowerBoost's existing switch with a new case.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var aulus=Add(s,"oracle-l5r-10687",0,Zone.Attached,attachedTo:host.Id);
        var legionnaire=Add(s,"training-legionnaire-follower",0,Zone.Attached,attachedTo:host.Id); // Force 1
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==legionnaire.Id).Force,2,"Base 1 + 1 from Aulus's unit-scoped boost");
        Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==aulus.Id).Force,3,"Aulus itself is not a Legionnaire -- unaffected");
    }
}
static void KeywordMechanics()
{
    {
        // Expendable: a mandatory Fate-deck draw when destroyed by any effect -- not merely discarded.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var expendable=Add(s,"training-expendable-follower",1,Zone.Attached,province:0,attachedTo:host.Id);
        var destroyer=Add(s,"training-no-attachments-destroyer",0,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        var handBefore=e.State.Players[1].Hand.Count;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:destroyer.Id,TargetId:expendable.Id,AbilityId:"training-destroy-no-attachments"));
        Equal(e.State.Cards[expendable.Id].Zone,Zone.Dead,"Destroyed via DestroyUnits' own top-level path");
        Equal(e.State.Players[1].Hand.Count,handBefore+1,"Mandatory Fate-deck draw on destruction");
    }
    {
        // Expendable does NOT trigger on a plain discard (the discardHand path never calls
        // DestroyUnits/DestroyAttachment) -- proving the Destroyed-vs-Discarded distinction. Needs a
        // hand of exactly 9 (one over the limit) so discardHand's own count check accepts discarding
        // just the one Expendable card.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        while(s.Players[0].Hand.Count<8) Add(s,"training-item",0,Zone.Hand);
        var expendable=Add(s,"training-expendable-follower",0,Zone.Hand);
        Equal(s.Players[0].Hand.Count,9,"Test setup: exactly one over the eight-card hand limit");
        s.PendingChoice=new("discardHand",0,null,"Discard 1 card(s) to return to eight.");
        var e=MatchEngine.FromTestState(s);
        var handBefore=e.State.Players[0].Hand.Count;
        Accept(e,new GameCommand("discardHand",0,e.Version,CardIds:[expendable.Id]));
        Equal(e.State.Cards[expendable.Id].Zone,Zone.FateDiscard,"Discarded");
        Equal(e.State.Players[0].Hand.Count,handBefore-1,"No extra draw -- a plain discard, not a destroy");
    }
    {
        // Destined: a mandatory Fate-deck draw every time it enters play -- exercised via Purchase
        // (from a province) and separately via Equip (a Follower attaching).
        var s=MatchEngine.CreateEconomyScenario().State;
        var cardId=s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId="training-destined-personality"; s.Cards[cardId].FaceUp=true;
        var e=MatchEngine.FromTestState(s);
        var handBefore=e.State.Players[0].Hand.Count;
        var payment=e.PaymentOptions(0,3).First();
        Accept(e,new GameCommand("purchase",0,e.Version,CardId:cardId,SourceIds:payment.SourceIds));
        Equal(e.State.Players[0].Hand.Count,handBefore+1,"Purchase (from a province): mandatory draw");

        var s2=MatchEngine.CreateEconomyScenario().State; s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
        var destinedFollower=Add(s2,"training-destined-follower",0,Zone.Hand);
        var wielder=Add(s2,"training-crane-samurai",0,Zone.Home);
        var e2=MatchEngine.FromTestState(s2);
        var handBefore2=e2.State.Players[0].Hand.Count;
        Accept(e2,Cmd(e2,"equip",id:destinedFollower.Id,sources:e2.PaymentOptions(0,2).First().SourceIds,target:wielder.Id));
        Equal(e2.State.Players[0].Hand.Count,handBefore2,"Hand lost the Follower itself, then gained the Destined draw -- net unchanged");
    }
    {
        // Kharmic: pay 2 Gold, discard a Kharmic Dynasty card from a province -- immediately refilled
        // face up (unlike a normal end-of-turn refill, which stays face down). Player abilities need a
        // published (non-ScenarioOnly) Stronghold, same requirement Tactical Advantage's own test meets.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId="oracle-l5r-1218";
        var cardId=s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId="training-kharmic-dynasty"; s.Cards[cardId].FaceUp=true;
        var e=MatchEngine.FromTestState(s);
        var deckBefore=e.State.Players[0].DynastyDeck.Count;
        Accept(e,new GameCommand("useAbility",0,e.Version,TargetId:cardId,AbilityId:"kharmic-discard",SourceIds:e.PaymentOptions(0,2).First().SourceIds));
        Equal(e.State.Cards[cardId].Zone,Zone.DynastyDiscard,"The Kharmic card is discarded");
        var newId=e.State.Players[0].Provinces[0].CardId;
        Check(newId is not null && newId != cardId,"The province is refilled with a different card");
        Check(e.State.Cards[newId!].FaceUp,"Immediately face up, unlike a normal end-of-turn refill");
        Equal(e.State.Players[0].DynastyDeck.Count,deckBefore-1,"One card left the Dynasty deck to refill the province");
    }
    {
        // Kharmic: pay 2 Gold, discard a Kharmic Fate card from hand -- an extra draw follows.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId="oracle-l5r-1218";
        var kharmicFate=Add(s,"training-kharmic-fate",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var handBefore=e.State.Players[0].Hand.Count;
        Accept(e,new GameCommand("useAbility",0,e.Version,TargetId:kharmicFate.Id,AbilityId:"kharmic-discard",SourceIds:e.PaymentOptions(0,2).First().SourceIds));
        Equal(e.State.Cards[kharmicFate.Id].Zone,Zone.FateDiscard,"Discarded from hand");
        Equal(e.State.Players[0].Hand.Count,handBefore,"Hand lost the discarded card, then gained an extra draw -- net unchanged");
    }
    {
        // Invest (Clay Kshatriya's shape): a separate, optional, standalone payment as the card
        // enters play, reusing the same own-enters-play Reaction window every entry effect shares --
        // genuinely optional, so declining it is legal too. Clay Kshatriya is a Follower, entering via
        // Equip (not Purchase, which is Personality/Holding-only).
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var invest=Add(s,"training-invest-follower",0,Zone.Hand);
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,Cmd(e,"equip",id:invest.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:host.Id));
        Equal(e.State.PendingChoice?.Type,"entryReaction");
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:invest.Id,AbilityId:"training-invest",SourceIds:[])); // 0 Gold offered, needs 3
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:invest.Id,AbilityId:"training-invest",SourceIds:e.PaymentOptions(0,3).First().SourceIds));
        Equal(e.State.Cards[invest.Id].Tokens.Count(t=>t.Type=="Invest"),1,"Invest paid: one +1F token");

        // Declining is equally legal: no tokens, no Gold spent.
        var s2=MatchEngine.CreateEconomyScenario().State; s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
        var invest2=Add(s2,"training-invest-follower",0,Zone.Hand);
        var host2=Add(s2,"training-crane-samurai",0,Zone.Home);
        var e2=MatchEngine.FromTestState(s2);
        Accept(e2,Cmd(e2,"equip",id:invest2.Id,sources:e2.PaymentOptions(0,2).First().SourceIds,target:host2.Id));
        Accept(e2,new GameCommand("declineReaction",0,e2.Version));
        Accept(e2,new GameCommand("declineReaction",1,e2.Version));
        Equal(e2.State.Cards[invest2.Id].Tokens.Count,0,"Declined -- no Invest tokens");
    }
}
static void PassiveAttackDefenseAndEntryWatch()
{
    {
        // Destrier's shape: Scope "Self" -- protects only the card itself, both Attack kinds.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var attackerHost=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var attacker=Add(s,"oracle-l5r-3456",0,Zone.Attached,province:0,attachedTo:attackerHost.Id); // Horse Archers: Ranged 4, unconditional
        var defenderHost=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var defended=Add(s,"training-attack-defense-self",1,Zone.Attached,province:0,attachedTo:defenderHost.Id); // Force 2
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:attacker.Id,TargetId:defended.Id,AbilityId:"horse-archers-attack")); // effective 4-3=1 < Force 2
    }
    {
        // Singh Remnants' shape: Scope "Unit", Ranged only -- protects every card in the unit, but a
        // Melee Attack against the same target is unaffected.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var attackerHost=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var rangedAttacker=Add(s,"oracle-l5r-3456",0,Zone.Attached,province:0,attachedTo:attackerHost.Id); // Ranged 4
        var meleeAttackerHost=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var meleeAttacker=Add(s,"oracle-l5r-10333",0,Zone.Attached,province:0,attachedTo:meleeAttackerHost.Id); // Cavalry Flankers: Melee 2, Attacker-only, bows self
        var defenderHost=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var guard=Add(s,"training-attack-defense-unit",1,Zone.Attached,province:0,attachedTo:defenderHost.Id);
        var otherFollower=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:defenderHost.Id); // Force 2, different card, same unit
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:rangedAttacker.Id,TargetId:otherFollower.Id,AbilityId:"horse-archers-attack")); // Ranged: effective 4-3=1 < Force 2, protects the WHOLE unit
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:meleeAttacker.Id,TargetId:otherFollower.Id,AbilityId:"cavalry-flankers-attack")); // Melee: unaffected, strength 2 = Force 2
        Equal(e.State.Cards[otherFollower.Id].Zone,Zone.FateDiscard,"Melee Attacks are not reduced -- Singh Remnants' shape only names Ranged (a destroyed Follower attachment lands in the Fate discard pile)");
    }
    {
        // An Item's own default scope (no "unit" language) conveys to its wielder, not the Item itself.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var attackerHost=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var attacker=Add(s,"oracle-l5r-10333",0,Zone.Attached,province:0,attachedTo:attackerHost.Id); // Cavalry Flankers: Melee 2
        var wielder=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3
        wielder.Modifiers=[new("test","test","Force",-1)]; // Force 2, matching the Melee 2 strength exactly
        Add(s,"training-attack-defense-wielder",1,Zone.Attached,province:0,attachedTo:wielder.Id); // -2 Melee, Scope Wielder
        var otherFollower=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:wielder.Id); // Force 2, same unit but not the wielder
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:attacker.Id,TargetId:wielder.Id,AbilityId:"cavalry-flankers-attack")); // effective 2-2=0 < Force 2 -- protects the wielder
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:attacker.Id,TargetId:otherFollower.Id,AbilityId:"cavalry-flankers-attack")); // the OTHER Follower in the same unit is unaffected
        Equal(e.State.Cards[otherFollower.Id].Zone,Zone.FateDiscard,"Wielder scope does not protect the rest of the unit");
    }
    {
        // Singh Remnants' shape: from hand, attach to a Personality as it enters play (via Purchase),
        // following Equip's own restrictions (Personal Honor Requirement included).
        var s=MatchEngine.CreateEconomyScenario().State;
        var lowHonorId=s.Players[0].Provinces[0].CardId!;
        s.Cards[lowHonorId].DefinitionId="training-crane-clan-low-honor"; s.Cards[lowHonorId].FaceUp=true; // PH 1 -- fails the Requirement
        var watcher=Add(s,"training-personality-entry-watcher",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,3).First();
        Accept(e,new GameCommand("purchase",0,e.Version,CardId:lowHonorId,SourceIds:payment.SourceIds));
        Equal(e.State.PendingChoice?.Type,"proclaim","In-Clan recruit: the Proclaim-eligible window doubles as this entry's reaction window");
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:watcher.Id,AbilityId:"training-personality-entry-watch",SourceIds:e.PaymentOptions(0,2).First().SourceIds)); // PH 1 < Requirement 3

        var s2=MatchEngine.CreateEconomyScenario().State;
        var highHonorId=s2.Players[0].Provinces[0].CardId!;
        s2.Cards[highHonorId].DefinitionId="training-crane-clan-samurai"; s2.Cards[highHonorId].FaceUp=true; // PH 3 -- meets the Requirement
        var watcher2=Add(s2,"training-personality-entry-watcher",0,Zone.Hand);
        var e2=MatchEngine.FromTestState(s2);
        var honorBefore=e2.State.Players[0].Honor;
        var payment2=e2.PaymentOptions(0,5).First(); // training-crane-clan-samurai costs 5
        Accept(e2,new GameCommand("purchase",0,e2.Version,CardId:highHonorId,SourceIds:payment2.SourceIds));
        Accept(e2,new GameCommand("useAbility",0,e2.Version,CardId:watcher2.Id,AbilityId:"training-personality-entry-watch",SourceIds:e2.PaymentOptions(0,2).First().SourceIds));
        Equal(e2.State.Cards[watcher2.Id].Zone,Zone.Attached,"Attached to the just-entered Personality");
        Equal(e2.State.Cards[watcher2.Id].AttachedTo,highHonorId);
        Equal(e2.State.Players[0].Honor,honorBefore+1,"Gains 1 Honor");
    }
    {
        // The same reaction also fires from a discard pile, not only from hand.
        var s=MatchEngine.CreateEconomyScenario().State;
        var highHonorId=s.Players[0].Provinces[0].CardId!;
        s.Cards[highHonorId].DefinitionId="training-crane-clan-samurai"; s.Cards[highHonorId].FaceUp=true;
        var watcher=Add(s,"training-personality-entry-watcher",0,Zone.FateDiscard);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("purchase",0,e.Version,CardId:highHonorId,SourceIds:e.PaymentOptions(0,5).First().SourceIds)); // training-crane-clan-samurai costs 5
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:watcher.Id,AbilityId:"training-personality-entry-watch",SourceIds:e.PaymentOptions(0,2).First().SourceIds));
        Equal(e.State.Cards[watcher.Id].Zone,Zone.Attached,"Attaches from a discard pile too, not only from hand");
    }
}
static void GrantsKeywordWave()
{
    {
        // Scourge of the Sea (issue #179): a pure "This Personality has X" grant with no ability of
        // its own -- AttachGrantsKeyword's second published carrier after Commander's Steed.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var scourge=Add(s,"oracle-l5r-6501",0,Zone.Hand);
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,Cmd(e,"equip",id:scourge.Id,sources:e.PaymentOptions(0,9).First().SourceIds,target:host.Id)); // costs 9 -- every producer in the economy scenario, combined
        Check(e.State.Cards[host.Id].GrantedKeywords.Any(g=>g.Keyword=="Naval"),"Attaching grants Naval to the wielder");
    }
    {
        // Unparalleled Sword (issue #179): grants Kensai (unlocking the second Weapon slot) plus a
        // printed Battle Melee 4 attack -- both reuse already-existing primitives. Added directly to a
        // raw MatchState (not via the real Equip() command, already proven for Scourge of the Sea
        // above), so the grant is applied by hand exactly as Equip() would.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var sword=Add(s,"oracle-l5r-10124",0,Zone.Attached,province:0,attachedTo:host.Id);
        host.GrantedKeywords.Add(new GrantedKeyword("Kensai",sword.Id,"Attachment"));
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:sword.Id,TargetId:enemy.Id,AbilityId:"unparalleled-sword-attack")); // Melee 4 >= Force 3
    }
    {
        // GrantKeyword idempotency (issue #179): a Personality that already carries a keyword (printed
        // here) does not gain a redundant second grant of the same keyword from an attachment.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var sword=Add(s,"oracle-l5r-10124",0,Zone.Hand);
        var host=Add(s,"training-kensai",0,Zone.Home); // already has printed Kensai
        var e=MatchEngine.FromTestState(s);
        Accept(e,Cmd(e,"equip",id:sword.Id,sources:e.PaymentOptions(0,7).First().SourceIds,target:host.Id)); // costs 7
        Check(!e.State.Cards[host.Id].GrantedKeywords.Any(g=>g.Keyword=="Kensai"),"Already has Kensai printed -- no redundant grant");
    }
    {
        // Justice of the Crane (issue #179): grants Magistrate plus a Battle Dishonor action. The
        // "additional action if the printed Magistrate keyword" bonus is deferred (no general
        // "additional action" primitive yet, same gap as Effortless Counterattack in #177).
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var sword=Add(s,"oracle-l5r-10528",0,Zone.Attached,province:0,attachedTo:host.Id);
        host.GrantedKeywords.Add(new GrantedKeyword("Magistrate",sword.Id,"Attachment"));
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Check(!enemy.Dishonorable);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:sword.Id,TargetId:enemy.Id,AbilityId:"justice-of-crane-dishonor"));
        Check(e.State.Cards[enemy.Id].Dishonorable,"Dishonors the target");
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0),"Only granted Magistrate (not printed): no additional-action bonus (issue #181)");
    }
    {
        // Justice of the Crane's own deferred bonus (#179/#180), now wired (#181): "if this Personality
        // has the PRINTED Magistrate keyword" -- checked against training-magistrate-personality's own
        // printed Keywords, distinct from the grant Justice of the Crane itself also applies.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var host=Add(s,"training-magistrate-personality",0,Zone.Battlefield,province:0);
        var sword=Add(s,"oracle-l5r-10528",0,Zone.Attached,province:0,attachedTo:host.Id);
        var enemy=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:sword.Id,TargetId:enemy.Id,AbilityId:"justice-of-crane-dishonor"));
        Check(e.State.Cards[enemy.Id].Dishonorable,"Dishonors the target");
        Check(e.State.AdditionalActions.Any(g=>g.PlayerId==0 && g.ScopeCardIds is null),"Printed Magistrate: grants an unscoped additional action");
        Equal(e.State.PriorityPlayerId,0,"Priority stays with the wielder's controller");
    }
}
static void AdditionalActionPrimitive()
{
    {
        // Again! (issue #181): straightens a bowed Kensai and his Weapons, resets the Weapons' own
        // Battle abilities so they may be used again this turn, and grants an unscoped Additional
        // Action -- keeping priority with the same player.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var kensai=Add(s,"training-kensai",0,Zone.Battlefield,province:0,bowed:true);
        var weapon=Add(s,"training-weapon-with-attack",0,Zone.Attached,province:0,attachedTo:kensai.Id,bowed:true);
        weapon.AbilityUses["training-weapon-attack"]=s.TurnNumber;
        var enemy=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0).Id); // Force 2, matching the Weapon's Melee 2
        var again=Add(s,"oracle-l5r-127",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:again.Id,PerformerId:kensai.Id,AbilityId:"again-boost"));
        Check(!e.State.Cards[kensai.Id].Bowed,"Straightens the Kensai"); Check(!e.State.Cards[weapon.Id].Bowed,"Straightens his Weapons");
        Check(!e.State.Cards[weapon.Id].AbilityUses.ContainsKey("training-weapon-attack"),"The Weapon's Battle ability may be used again this turn");
        Check(e.State.AdditionalActions.Any(g=>g.PlayerId==0 && g.ScopeCardIds is null),"Grants an unscoped additional Battle action");
        Equal(e.State.PriorityPlayerId,0,"Priority stays with the same player");
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:weapon.Id,TargetId:enemy.Id,AbilityId:"training-weapon-attack")); // Melee 2 = Force 2, consumes the grant
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0),"Grant consumed");
        Equal(e.State.PriorityPlayerId,1,"Priority hands onward once the granted action is used up");
    }
    {
        // Two Hands' scoping (issue #181): the two granted actions are restricted to cards in the
        // Kensai's own unit -- a card outside it is an ordinary action (and forfeits both grants).
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var kensai=Add(s,"training-kensai",0,Zone.Battlefield,province:0);
        var weapon=Add(s,"training-weapon-with-attack",0,Zone.Attached,province:0,attachedTo:kensai.Id);
        var outsider=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var outsiderWeapon=Add(s,"training-weapon-with-attack",0,Zone.Attached,province:0,attachedTo:outsider.Id);
        var enemyHost=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var enemy=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:enemyHost.Id); // Force 2, matching the Weapon's Melee 2
        var twoHands=Add(s,"oracle-l5r-10175",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:twoHands.Id,PerformerId:kensai.Id,AbilityId:"two-hands-grant"));
        Equal(e.State.AdditionalActions.Count(g=>g.PlayerId==0),2,"Grants two additional Battle actions");
        Check(e.State.AdditionalActions.All(g=>g.ScopeCardIds!.Contains(kensai.Id) && g.ScopeCardIds.Contains(weapon.Id)),"Scoped to the Kensai and his Weapon");
        // A card outside the unit is an ordinary action: succeeds, but is not scoped by the grant, so
        // both of it forfeit rather than lingering.
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:outsiderWeapon.Id,TargetId:enemy.Id,AbilityId:"training-weapon-attack"));
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0),"Outside the unit: forfeits both grants rather than consuming one");
        Equal(e.State.PriorityPlayerId,1,"An ordinary action correctly cedes priority");
    }
    {
        // Two Hands' own two grants ARE usable, one after another, by different cards in the unit.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var kensai=Add(s,"training-kensai",0,Zone.Battlefield,province:0);
        var weapon1=Add(s,"training-weapon-with-attack",0,Zone.Attached,province:0,attachedTo:kensai.Id);
        var weapon2=Add(s,"training-weapon-with-attack",0,Zone.Attached,province:0,attachedTo:kensai.Id);
        var enemyHost=Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var enemy1=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:enemyHost.Id); // Force 2, matching the Weapon's Melee 2
        var enemy2=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:enemyHost.Id); // a second, separate target -- the first is destroyed by the first attack
        var twoHands=Add(s,"oracle-l5r-10175",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:twoHands.Id,PerformerId:kensai.Id,AbilityId:"two-hands-grant"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:weapon1.Id,TargetId:enemy1.Id,AbilityId:"training-weapon-attack"));
        Equal(e.State.AdditionalActions.Count(g=>g.PlayerId==0),1,"One of the two grants remains");
        Equal(e.State.PriorityPlayerId,0,"Still holding priority for the second one");
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:weapon2.Id,TargetId:enemy2.Id,AbilityId:"training-weapon-attack"));
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0));
        Equal(e.State.PriorityPlayerId,1,"Both used up: priority cedes normally");
    }
    {
        // Two Hands' restriction: "other actions will not give you new additional Battle actions this
        // battle" -- Again!'s OTHER effects (straighten, reset Weapon usage) still fully apply, only
        // its own grant is suppressed.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var kensai=Add(s,"training-kensai",0,Zone.Battlefield,province:0); // Two Hands needs an unbowed performer
        var weapon=Add(s,"training-weapon-with-attack",0,Zone.Attached,province:0,attachedTo:kensai.Id,bowed:true);
        weapon.AbilityUses["training-weapon-attack"]=s.TurnNumber;
        var twoHands=Add(s,"oracle-l5r-10175",0,Zone.Hand);
        var again=Add(s,"oracle-l5r-127",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:twoHands.Id,PerformerId:kensai.Id,AbilityId:"two-hands-grant"));
        Check(e.State.NoNewAdditionalActionsThisBattle);
        // Again! is a different card, outside Two Hands' own scope -- an unrelated action, so it also
        // forfeits Two Hands' still-unused grants (same rule proven above), on top of never adding one
        // of its own. Its OTHER effects (the Weapon-ability reset here; the Kensai's own straighten is
        // already proven above) still fully apply -- only the grant itself is suppressed.
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:again.Id,PerformerId:kensai.Id,AbilityId:"again-boost"));
        Check(!e.State.Cards[weapon.Id].AbilityUses.ContainsKey("training-weapon-attack"),"Again!'s Weapon-ability reset still applies");
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0),"Again!'s own grant is suppressed by Two Hands' restriction");
    }
    {
        // Effortless Counterattack's Reaction (issue #181): negates a Battle action taken as an
        // Additional Action before it resolves, opening the same before-additional-action-resolves
        // window an out-of-play Reaction needs.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var kensai=Add(s,"training-kensai",0,Zone.Battlefield,province:0,bowed:true);
        var weapon=Add(s,"training-weapon-with-attack",0,Zone.Attached,province:0,attachedTo:kensai.Id,bowed:true);
        var enemyHost=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var enemy=Add(s,"training-follower",1,Zone.Attached,province:0,attachedTo:enemyHost.Id); // Force 2 -- would be destroyed by the Weapon's Melee 2
        var again=Add(s,"oracle-l5r-127",0,Zone.Hand);
        var counter=Add(s,"oracle-l5r-2283",1,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:again.Id,PerformerId:kensai.Id,AbilityId:"again-boost"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:weapon.Id,TargetId:enemy.Id,AbilityId:"training-weapon-attack"));
        Equal(e.State.PendingChoice?.Type,"additionalActionReaction","A Reaction is available to disrupt it -- deferred rather than resolving immediately");
        Equal(e.State.PriorityPlayerId,1,"The opponent gets the opportunity to react");
        Accept(e,new GameCommand("useAbility",1,e.Version,CardId:counter.Id,AbilityId:"effortless-counterattack-negate"));
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Accept(e,new GameCommand("declineReaction",1,e.Version));
        Check(e.State.PendingChoice is null,"Window closed");
        Check(e.State.PendingAdditionalAction is null);
        Equal(e.State.Cards[enemy.Id].Zone,Zone.Attached,"Negated: the attack never actually resolved");
        Check(!e.State.Players[1].Hand.Contains(counter.Id),"Effortless Counterattack discarded after use");
    }
    {
        // Effortless Counterattack's own plain Battle ability: unrelated to the Reaction above.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=1; s.PriorityPlayerId=1;
        var performer=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var target=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var counter=Add(s,"oracle-l5r-2283",1,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",1,e.Version,CardId:counter.Id,PerformerId:performer.Id,TargetId:target.Id,AbilityId:"effortless-counterattack-move"));
        Equal(e.State.Cards[target.Id].Zone,Zone.Home,"Moved home");
    }
    {
        // Battle-scoped: Two Hands' restriction survives a mere pass (unlike its own grants, forfeited
        // immediately -- see the scoping test above) but not into a later battle, matching AshigaruBoost's
        // own "for the remainder of the battle" precedent -- cleared at FinishBattleResolution.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var kensai=Add(s,"training-kensai",0,Zone.Battlefield,province:0);
        var twoHands=Add(s,"oracle-l5r-10175",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:twoHands.Id,PerformerId:kensai.Id,AbilityId:"two-hands-grant"));
        Check(e.State.NoNewAdditionalActionsThisBattle);
        Accept(e,new GameCommand("pass",0,e.Version)); // forfeits the still-unused grants; first of two consecutive passes
        Check(!e.State.AdditionalActions.Any(),"A mere pass already forfeits the still-unused grants");
        Check(e.State.NoNewAdditionalActionsThisBattle,"But the restriction itself survives a pass");
        Accept(e,new GameCommand("pass",1,e.Version)); // second consecutive pass: resolves the battle
        Check(!e.State.NoNewAdditionalActionsThisBattle,"Cleared once the battle actually resolves");
    }
}
static void StalwartKeyword()
{
    {
        // Stalwart (issue #184): the first time an enemy's effect would bow it each turn, the bow is
        // negated (and only the bow -- nothing else about the effect is touched); a second, independent
        // enemy-bow effect the same turn succeeds normally. Two separate source instances of the same
        // per-instance (not per-player) ability isolate this from the ordinary once-per-turn dedupe.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var stalwart=Add(s,"training-stalwart-personality",1,Zone.Home);
        var source1=Add(s,"training-bow-enemy-source",0,Zone.Home);
        var source2=Add(s,"training-bow-enemy-source",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:source1.Id,TargetId:stalwart.Id,AbilityId:"training-bow-enemy"));
        Check(!e.State.Cards[stalwart.Id].Bowed,"First enemy bow this turn is negated");
        Accept(e,new GameCommand("pass",1,e.Version));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:source2.Id,TargetId:stalwart.Id,AbilityId:"training-bow-enemy"));
        Check(e.State.Cards[stalwart.Id].Bowed,"Second enemy bow this turn succeeds");
    }
    {
        // Never negates the card's own controller bowing it -- only "an enemy's effect" ever qualifies.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var stalwart=Add(s,"training-stalwart-personality",0,Zone.Home);
        var source=Add(s,"training-bow-any-source",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:source.Id,TargetId:stalwart.Id,AbilityId:"training-bow-any"));
        Check(e.State.Cards[stalwart.Id].Bowed,"Own controller bowing it is never negated");
    }
    {
        // Resets at end phase (each player's own, regardless of whose turn) -- modeled here as a stale
        // marker from an earlier TurnNumber, matching AbilityUses' own per-turn convention this reuses;
        // TurnNumber already increments at the end of every player's own turn (FinishTurn).
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var stalwart=Add(s,"training-stalwart-personality",1,Zone.Home);
        stalwart.AbilityUses["stalwart-negate"]=s.TurnNumber-1;
        var source=Add(s,"training-bow-enemy-source",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:source.Id,TargetId:stalwart.Id,AbilityId:"training-bow-enemy"));
        Check(!e.State.Cards[stalwart.Id].Bowed,"A stale marker from an earlier turn does not block this turn's negation");
    }
    {
        // Real card (issue #184): Shiba Jikaro, pure Stalwart reminder text.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var jikaro=Add(s,"oracle-l5r-10244",1,Zone.Home);
        var source=Add(s,"training-bow-enemy-source",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:source.Id,TargetId:jikaro.Id,AbilityId:"training-bow-enemy"));
        Check(!e.State.Cards[jikaro.Id].Bowed,"Shiba Jikaro negates the first enemy bow this turn");
    }
}
static void ReserveKeyword()
{
    {
        // Reserve (issue #184), Dynasty side: an absent battle action -- the recruiting player needs
        // no presence of their own at this battlefield (only the reserve card, still in its province,
        // and the enemy's own presence there), and it goes straight to the battlefield. Also proves the
        // action is available even when it is NOT the recruiting player's own active turn (ActivePlayerId
        // is the opponent's here), matching "an absent battle action" -- just no Proclaim in that case.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=1; s.PriorityPlayerId=0;
        var reserveId=s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId="training-reserve-personality"; s.Cards[reserveId].FaceUp=true;
        Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,3).First();
        Accept(e,new GameCommand("recruitReserve",0,e.Version,CardId:reserveId,SourceIds:payment.SourceIds));
        Equal(e.State.Cards[reserveId].Zone,Zone.Battlefield,"Recruited directly to the battlefield");
        Equal(e.State.Cards[reserveId].ProvinceIndex,0);
        Check(e.State.Players[0].Provinces[0].CardId is not null,"Province refilled");
        Check(!e.State.Cards[e.State.Players[0].Provinces[0].CardId!].FaceUp,"Refilled face down, as normal");
        Check(e.State.PendingChoice is null,"Not the recruiting player's own active turn: no Proclaim window");
        Equal(e.State.PriorityPlayerId,1,"Priority cedes to the opponent, like any Battle action");
    }
    {
        // Face down: cannot be activated.
        var s=StrongholdBattle("oracle-l5r-1218");
        var reserveId=s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId="training-reserve-personality"; s.Cards[reserveId].FaceUp=false;
        Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("recruitReserve",0,e.Version,CardId:reserveId,SourceIds:e.PaymentOptions(0,3).First().SourceIds));
    }
    {
        // No battlefield at this province at all: rejected.
        var s=StrongholdBattle("oracle-l5r-1218"); s.CurrentBattleProvince=1; s.UnresolvedBattlefields=[1];
        var reserveId=s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId="training-reserve-personality"; s.Cards[reserveId].FaceUp=true;
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("recruitReserve",0,e.Version,CardId:reserveId,SourceIds:e.PaymentOptions(0,3).First().SourceIds));
    }
    {
        // A battlefield exists at the province, but the enemy has no presence there: rejected.
        var s=StrongholdBattle("oracle-l5r-1218");
        var reserveId=s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId="training-reserve-personality"; s.Cards[reserveId].FaceUp=true;
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("recruitReserve",0,e.Version,CardId:reserveId,SourceIds:e.PaymentOptions(0,3).First().SourceIds));
    }
    {
        // Reserve, Fate side: a non-Spell Item attaches via Equip (Battle), following the exact same
        // rules as a Spell -- but does NOT grant the bonus Additional Action for its own Battle ability.
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var reserveItem=Add(s,"training-reserve-item",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Any(a=>a.Command?.AbilityId=="equip-battle"),"A Reserve Item in hand: Equip (Battle) should be offered");
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:reserveItem.Id,SourceIds:payment.SourceIds));
        Equal(e.State.Cards[reserveItem.Id].Zone,Zone.Attached);
        Check(!e.State.AdditionalActions.Any(g=>g.PlayerId==0),"Non-Spell Reserve attachment: no bonus Additional Action grant, despite having a Battle ability");
    }
    {
        // Real cards (issue #184): Hida Kurabi (Dynasty Destined/Reserve) recruited as an absent battle
        // action, and Floating Reserve (Fate Reserve Follower) attached via Equip (Battle).
        var s=StrongholdBattle("oracle-l5r-1218");
        var reserveId=s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId="oracle-l5r-10832"; s.Cards[reserveId].FaceUp=true; // Hida Kurabi
        Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        var handBefore=e.State.Players[0].Hand.Count;
        Accept(e,new GameCommand("recruitReserve",0,e.Version,CardId:reserveId,SourceIds:e.PaymentOptions(0,6).First().SourceIds));
        Equal(e.State.Cards[reserveId].Zone,Zone.Battlefield,"Hida Kurabi recruited from Reserve");
        Equal(e.State.Players[0].Hand.Count,handBefore+1,"Destined's own mandatory draw fires on entering play, Reserve recruitment included");
    }
    {
        var s=StrongholdBattle("oracle-l5r-1218");
        var shugenja=Add(s,"training-shugenja",0,Zone.Battlefield,province:0);
        Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
        var floatingReserve=Add(s,"oracle-l5r-10896",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var payment=e.PaymentOptions(0,2).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,AbilityId:"equip-battle",PerformerId:shugenja.Id,TargetId:floatingReserve.Id,SourceIds:payment.SourceIds));
        Equal(e.State.Cards[floatingReserve.Id].Zone,Zone.Attached,"Floating Reserve attached via Equip (Battle)");
    }
}
static void DisciplineKeyword()
{
    {
        // Discipline (issue #184): playable from a discard pile, paying its own printed Gold cost,
        // resolving its effect, then removed from the game -- never left in the discard pile again.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var card=Add(s,"training-discipline-item",0,Zone.FateDiscard);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        var payment=e.PaymentOptions(0,2).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:card.Id,AbilityId:"training-discipline-ability",SourceIds:payment.SourceIds));
        Equal(e.State.Players[0].Honor,honorBefore+1,"Resolves its printed effect");
        Equal(e.State.Cards[card.Id].Zone,Zone.RemovedFromGame,"Removed from the game afterward, not left in the discard pile");
        Check(e.GetView(0).Players[0].RemovedFromGame.Any(c=>c.Id==card.Id),"Visible to its own controller in the new Removed-from-Game pile");
        Check(e.GetView(1).Players[0].RemovedFromGame.Any(c=>c.Id==card.Id),"Visible to the opponent too, like any discard pile");
    }
    {
        // Cannot be activated from Hand -- only from a discard pile.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var card=Add(s,"training-discipline-item",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:card.Id,AbilityId:"training-discipline-ability",SourceIds:e.PaymentOptions(0,2).First().SourceIds));
    }
    {
        // May be discarded for another purpose (here, Tactical Advantage's own Focus-Value discard)
        // and still have its own Discipline ability used from the discard pile in the same turn.
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var tactician=Add(s,"training-tactician",0,Zone.Battlefield,province:0);
        var card=Add(s,"training-discipline-item",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,PerformerId:tactician.Id,TargetId:card.Id,AbilityId:"tactical-advantage"));
        Equal(e.State.Cards[card.Id].Zone,Zone.FateDiscard,"Discarded for its Focus Value by an unrelated ability");
        Accept(e,new GameCommand("pass",1,e.Version));
        var payment=e.PaymentOptions(0,2).First();
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:card.Id,AbilityId:"training-discipline-ability",SourceIds:payment.SourceIds));
        Equal(e.State.Cards[card.Id].Zone,Zone.RemovedFromGame,"Discipline still usable the same turn, unaffected by the earlier, unrelated discard");
    }
}
static void DishonorRehonorMechanics()
{
    {
        // Dishonor's own Personal-Honor override (issue #186, Utaku Chikako's shape): without the
        // override, a dishonorable Battle Maiden's current Personal Honor reads 0 (blocking the
        // Follower Honor Requirement, the same observable probe already used elsewhere); with it (an
        // in-play Personality the same owner controls carrying the override Effect), it reads the
        // printed/base value instead.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var follower=Add(s,"training-follower",0,Zone.Hand); // HonorRequirement 1
        var maiden=Add(s,"training-battle-maiden",0,Zone.Home); maiden.Dishonorable=true; // printed PH 1
        var e=MatchEngine.FromTestState(s);
        Reject(e,Cmd(e,"equip",id:follower.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:maiden.Id));

        var s2=MatchEngine.CreateEconomyScenario().State; s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
        var follower2=Add(s2,"training-follower",0,Zone.Hand);
        var maiden2=Add(s2,"training-battle-maiden",0,Zone.Home); maiden2.Dishonorable=true; // printed PH 1
        Add(s2,"training-battle-maiden-honor-override",0,Zone.Home);
        var e2=MatchEngine.FromTestState(s2);
        Accept(e2,Cmd(e2,"equip",id:follower2.Id,sources:e2.PaymentOptions(0,2).First().SourceIds,target:maiden2.Id));
    }
    {
        // GainHonorFromPerformer (issue #186): an honorable performer's own ability grants Honor
        // normally; a dishonorable performer's gain is negated and the performer is rehonored instead.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        s.FavorPlayerId=0;
        var honorable=Add(s,"training-honor-personality",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:honorable.Id,AbilityId:"shunya-honor"));
        Equal(e.State.Players[0].Honor,honorBefore+2,"Honorable performer: gains Honor normally");

        var s2=MatchEngine.CreateEconomyScenario().State; s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
        s2.FavorPlayerId=0;
        var dishonorable=Add(s2,"training-honor-personality",0,Zone.Home); dishonorable.Dishonorable=true;
        var e2=MatchEngine.FromTestState(s2);
        var honorBefore2=e2.State.Players[0].Honor;
        Accept(e2,new GameCommand("useAbility",0,e2.Version,CardId:dishonorable.Id,AbilityId:"shunya-honor"));
        Equal(e2.State.Players[0].Honor,honorBefore2,"Dishonorable performer: gain negated");
        Check(!e2.State.Cards[dishonorable.Id].Dishonorable,"...and rehonored instead");
    }
    {
        // Battle resolution (issue #186): a dishonorable personality in the WINNING army negates the
        // full Honor gain from that victory and is rehonored.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Battle; s.CurrentBattleProvince=0; s.UnresolvedBattlefields=[0]; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var attacker=Add(s,"training-lion-veteran",0,Zone.Battlefield,province:0); attacker.Dishonorable=true; // Force 6
        var defender=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Resolve(e);
        Check(!e.State.Cards[attacker.Id].Dishonorable,"Rehonored");
        Equal(e.State.Players[0].Honor,honorBefore,"Honor gain from the victory negated entirely");
        Equal(e.State.Cards[defender.Id].Zone,Zone.Dead,"The defeated defender is still destroyed normally");
    }
    {
        // Tie (issue #186): rehonored BEFORE the tie's mutual destruction, so the dishonorable side
        // takes neither the battle's own gain nor its own dishonorable-death Honor penalty; the other
        // (honorable) side still gains Honor normally from what it destroyed.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Battle; s.CurrentBattleProvince=0; s.UnresolvedBattlefields=[0]; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var attacker=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0); attacker.Dishonorable=true; // Force 3, printed PH 2
        var defender=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3, PH 2
        var e=MatchEngine.FromTestState(s);
        var attackerHonorBefore=e.State.Players[0].Honor; var defenderHonorBefore=e.State.Players[1].Honor;
        Resolve(e);
        Equal(e.State.Cards[attacker.Id].Zone,Zone.Dead,"Destroyed in the mutual tie");
        Equal(e.State.Players[0].Honor,attackerHonorBefore,"Dishonorable side: no gain, and no dishonorable-death penalty either (rehonored first)");
        Equal(e.State.Players[1].Honor,defenderHonorBefore+1,"Honorable side still gains Honor normally (1 destroyed, a tie does not double it)");
    }
}
static void UtakuChikako()
{
    {
        // Passive: the real card grants the override too, not just the training fixture.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var follower=Add(s,"training-follower",0,Zone.Hand);
        var maiden=Add(s,"training-battle-maiden",0,Zone.Home); maiden.Dishonorable=true; // printed PH 1
        Add(s,"oracle-l5r-10102",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,Cmd(e,"equip",id:follower.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:maiden.Id));
    }
    {
        // Limited: gain 2 Honor, then may not declare an attack this turn -- a broad restriction on
        // the assignment itself, not tied to any one specific path to it. An empty (declined)
        // assignment is always still legal. Resets at the start of the next turn.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var chikako=Add(s,"oracle-l5r-10102",0,Zone.Home);
        var unit=Add(s,"training-crane-samurai",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:chikako.Id,AbilityId:"utaku-chikako-decline"));
        Equal(e.State.Players[0].Honor,honorBefore+2,"Gains 2 Honor");
        Check(e.State.Players[0].MayNotAttackThisTurn,"Restricted for the rest of this turn");
        Accept(e,new GameCommand("pass",1,e.Version));
        Accept(e,new GameCommand("pass",0,e.Version)); // Action Phase: two consecutive passes -> Attack Declaration
        Reject(e,new GameCommand("attack",0,e.Version,Assignments:new Dictionary<string,int>{[unit.Id]=0})); // declaring an attacker is rejected
        Accept(e,new GameCommand("attack",0,e.Version,Assignments:[])); // declining (empty assignment) is still legal
    }
}
static void GeneralizedEntryReactionWindow()
{
    {
        // Equip now opens the same "own-enters-play" Reaction window Purchase always has -- and,
        // unlike Purchase (which never cedes priority away from the active player), the window
        // correctly hands priority to the *opponent* once closed, matching Equip's normal
        // any-action-cedes-priority rule (ActingPlayerId set to the opponent when the window opens).
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var item=Add(s,"training-item-entry-reaction",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,Cmd(e,"equip",id:item.Id,sources:payment.SourceIds,target:host.Id));
        Equal(e.State.Cards[item.Id].Zone,Zone.Attached);
        Equal(e.State.PendingChoice?.Type,"entryReaction","Equip opens the entering card's own Reaction window");
        Equal(e.State.PriorityPlayerId,0,"Priority has not yet ceded -- the window is still open");
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:item.Id,AbilityId:"training-item-entry-reaction"));
        Equal(e.State.Players[0].Honor,honorBefore+1);
        Accept(e,new GameCommand("declineReaction",1,e.Version));
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Check(e.State.PendingChoice is null);
        Equal(e.State.PriorityPlayerId,1,"Once closed, priority correctly cedes to the opponent, unlike Purchase's own-turn windows");
    }
    {
        // Repeatable each time the card enters play: a stale usage record from an earlier entry
        // (simulating "left play, then entered again") does not block this fresh window.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var item=Add(s,"training-item-entry-reaction",0,Zone.Hand); item.AbilityUses["training-item-entry-reaction"]=0;
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[0].Honor;
        var payment=e.PaymentOptions(0,1).First();
        Accept(e,Cmd(e,"equip",id:item.Id,sources:payment.SourceIds,target:host.Id));
        Check(e.QueryActions(0).Any(a=>a.Command?.AbilityId=="training-item-entry-reaction" && a.Enabled),"Offered and enabled despite the stale earlier-entry usage record");
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:item.Id,AbilityId:"training-item-entry-reaction"));
        Equal(e.State.Players[0].Honor,honorBefore+1,"The reaction actually fires again, not just offered");
    }
    {
        // PlayStrategy's Terrain path opens the same window too, with the same opponent-cede fix.
        var s=Battle(5,2).State;
        var terrain=Add(s,"training-terrain-entry-reaction",1,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var honorBefore=e.State.Players[1].Honor;
        Accept(e,Cmd(e,"playStrategy",player:1,id:terrain.Id));
        Equal(e.State.Cards[terrain.Id].Zone,Zone.Battlefield);
        Equal(e.State.PendingChoice?.Type,"entryReaction");
        Accept(e,new GameCommand("useAbility",1,e.Version,CardId:terrain.Id,AbilityId:"training-terrain-entry-reaction"));
        Equal(e.State.Players[1].Honor,honorBefore+1);
        Accept(e,new GameCommand("declineReaction",0,e.Version));
        Accept(e,new GameCommand("declineReaction",1,e.Version));
        Equal(e.State.PriorityPlayerId,0,"Priority cedes to the opponent (player 0) once the window closes");
    }
}
static void FollowerAndItemStaticForceHooks()
{
    {
        // A Follower's own static conditional Force ("+2F while defending") stays with the Follower
        // itself and shows up in its own CardForce/view regardless of bow state...
        var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
        var attacker=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var defender=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var defFollower=Add(s,"training-follower-force-effect",1,Zone.Attached,bowed:true,province:0,attachedTo:defender.Id); // Force 2+2=4
        var atkFollower=Add(s,"training-follower-force-effect",0,Zone.Attached,province:0,attachedTo:attacker.Id); // attacking, no bonus: Force 2
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(1).Players[1].Army.Single(c=>c.Id==defFollower.Id).Force,4,"Bowed defending Follower still shows its own +2F for targeting");
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==atkFollower.Id).Force,2,"Attacking (not defending): the conditional bonus does not apply");
        // ...but only joins the unit's BattleForce total when unbowed (or Elite) -- matching every
        // other Follower's existing bowed-suppression rule.
        Equal(e.GetView(1).Players[1].Army.Single(c=>c.Id==defender.Id).UnitForce,3,"Bowed: defender's own Force 3, the bowed Follower's +2F+2F does not join the unit total");
        defFollower.Bowed=false;
        var e2=MatchEngine.FromTestState(s);
        Equal(e2.GetView(1).Players[1].Army.Single(c=>c.Id==defender.Id).UnitForce,7,"Unbowed: 3 (defender) + 4 (Follower's own Force 2 + its +2F while defending)");
    }
    {
        // An Item's printed Force/Chi and its own conditional bonus to the wielder (Kensai's Blade's
        // shape) always convey to the Personality, whether the Item itself is bowed or not.
        var s=StrongholdBattle("oracle-l5r-1218");
        var kensai=Add(s,"training-kensai",0,Zone.Battlefield,province:0); // Force 2, Chi 2
        var blade=Add(s,"training-item-force-effect",0,Zone.Attached,bowed:true,province:0,attachedTo:kensai.Id); // +2F/+1C printed, +1F conditional (Kensai)
        var nonKensai=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0); // Force 3
        var blade2=Add(s,"training-item-force-effect",1,Zone.Attached,province:0,attachedTo:nonKensai.Id);
        var e=MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==kensai.Id).Force,5,"Base 2 + printed 2 + conditional 1 (Kensai wielder), even though the Item is bowed");
        Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==kensai.Id).Chi,3,"Base Chi 2 + printed Chi 1, regardless of the Item's own bow state");
        Equal(e.GetView(1).Players[1].Army.Single(c=>c.Id==nonKensai.Id).Force,5,"Base 3 + printed 2, no conditional bonus for a non-Kensai wielder");
    }
}
static void UnitForceBoostPrimitive()
{
    // Senior Infantry Legion's shape: a card that grants a static +1F to every OTHER Follower in its
    // own unit (a Personality and all of its attachments), never to itself, and never leaking to a
    // different unit.
    var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
    var unitA=Add(s,"training-crane-samurai",0,Zone.Home);
    var plainFollower=Add(s,"training-follower",0,Zone.Attached,attachedTo:unitA.Id); // Force 2
    var booster=Add(s,"training-follower-booster",0,Zone.Attached,attachedTo:unitA.Id); // Force 1, boosts other unit Followers
    var unitB=Add(s,"training-crane-samurai",0,Zone.Home);
    var otherUnitFollower=Add(s,"training-follower",0,Zone.Attached,attachedTo:unitB.Id); // different unit -- no boost
    var e=MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==plainFollower.Id).Force,3,"Plain Follower gets +1F from the booster in the same unit");
    Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==booster.Id).Force,1,"The booster itself is not boosted (no other booster present in the unit)");
    Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==otherUnitFollower.Id).Force,2,"A Follower in a different unit is unaffected");

    var booster2=Add(s,"training-follower-booster",0,Zone.Attached,attachedTo:unitA.Id); // second booster, same unit
    var e2=MatchEngine.FromTestState(s);
    Equal(e2.GetView(0).Players[0].Home.Single(c=>c.Id==booster.Id).Force,2,"Two boosters in one unit grant each other +1F, not themselves: printed 1 + 1 from the other booster");
    Equal(e2.GetView(0).Players[0].Home.Single(c=>c.Id==booster2.Id).Force,2,"Symmetric: the second booster also gets +1F from the first, not itself");
    Equal(e2.GetView(0).Players[0].Home.Single(c=>c.Id==plainFollower.Id).Force,4,"Plain Follower now gets +1F from each of the two boosters: 2 + 1 + 1");
}
static void AshigaruEliteBoostPrimitive()
{
    // Ashigaru Elite's shape: an activated, tireless, stacking Battle ability -- bow an unbowed Farm
    // to give +1F, for the remainder of the battle, to every Ashigaru-keyword Personality/Follower the
    // performer controls at the current battlefield. Distinct from Senior Infantry Legion's static
    // unit-scoped boost: this is army-scoped (any of the performer's own units at this battlefield,
    // not just one unit) and keyed off a keyword rather than a card type.
    var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
    var booster=Add(s,"training-ashigaru-boost-source",0,Zone.Battlefield,province:0);
    var ashigaruUnit=Add(s,"training-ashigaru-personality",0,Zone.Battlefield,province:0); // Force 2
    var ashigaruFollower=Add(s,"training-ashigaru-follower",0,Zone.Attached,attachedTo:ashigaruUnit.Id,province:0); // Force 1
    var plainUnit=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0); // Force 3, no Ashigaru keyword
    var enemyAshigaru=Add(s,"training-ashigaru-personality",1,Zone.Battlefield,province:0); // enemy's own Ashigaru
    var farm1=Add(s,"training-farm",0,Zone.Home);
    var farm2=Add(s,"training-farm",0,Zone.Home);
    var e=MatchEngine.FromTestState(s);
    var use=new GameCommand("useAbility",0,e.Version,CardId:booster.Id,AbilityId:"training-ashigaru-boost",TargetId:farm1.Id);
    Reject(e,use with {TargetId=plainUnit.Id}); // not an unbowed Farm
    Accept(e,use);
    Equal(e.State.Cards[farm1.Id].Bowed,true,"Bowing the Farm is the cost");
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==ashigaruUnit.Id).Force,3,"Base 2 + 1 from one use");
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==ashigaruFollower.Id).Force,2,"Base 1 + 1 from one use -- the boost also reaches an Ashigaru Follower");
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==plainUnit.Id).Force,3,"No Ashigaru keyword -- unaffected");
    Equal(e.GetView(1).Players[1].Army.Single(c=>c.Id==enemyAshigaru.Id).Force,2,"The enemy's own Ashigaru unit is unaffected -- scoped to the performer's own cards");
    // Tireless and stacking: usable again in the same battle, bowing a second Farm. Priority passed to
    // the opponent after the first use (like any action), so they must act (or pass) first.
    Accept(e,new GameCommand("pass",1,e.Version));
    Accept(e,new GameCommand("useAbility",0,e.Version,CardId:booster.Id,AbilityId:"training-ashigaru-boost",TargetId:farm2.Id));
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==ashigaruUnit.Id).Force,4,"Base 2 + 2 from two stacking uses");
    Accept(e,new GameCommand("pass",1,e.Version));
    Reject(e,new GameCommand("useAbility",0,e.Version,CardId:booster.Id,AbilityId:"training-ashigaru-boost",TargetId:farm1.Id)); // already bowed
    // "For the remainder of the battle": once it resolves, the boost does not carry into the next one.
    Resolve(e);
    Equal(e.State.AshigaruBoost[0],0,"The per-battle counter resets once the battle resolves");
}
static void ArmyBattleMaidenBoostPrimitive()
{
    // Utaku Elite Guard's shape: like Senior Infantry Legion, a static "boost others" bonus -- but
    // keyword-based (Battle Maiden) rather than type-based (Follower), and army-scoped (every OTHER
    // Battle-Maiden card the owner controls at the same battlefield) rather than unit-scoped.
    var s=StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId=0;
    var guard=Add(s,"training-utaku-elite-guard",0,Zone.Battlefield,province:0); // Force 2, itself Battle Maiden
    var maidenUnit=Add(s,"training-battle-maiden",0,Zone.Battlefield,province:0); // Force 2, a different unit
    var otherUnit=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0); // Force 3, no keyword
    var maidenFollower=Add(s,"training-battle-maiden-follower",0,Zone.Attached,attachedTo:otherUnit.Id,province:0); // Force 1
    var plainFollower=Add(s,"training-follower",0,Zone.Attached,attachedTo:otherUnit.Id,province:0); // Force 2, no keyword
    var homeMaiden=Add(s,"training-battle-maiden",0,Zone.Home); // not at this battlefield
    var enemyMaiden=Add(s,"training-battle-maiden",1,Zone.Battlefield,province:0); // enemy's own -- unaffected
    var e=MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==guard.Id).Force,2,"The guard does not boost itself (no other booster present in the army)");
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==maidenUnit.Id).Force,3,"Base 2 + 1 -- a Battle Maiden Personality in a DIFFERENT unit, same army");
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==otherUnit.Id).Force,3,"No Battle Maiden keyword -- unaffected");
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==maidenFollower.Id).Force,2,"Base 1 + 1 -- the boost also reaches a Battle Maiden Follower");
    Equal(e.GetView(0).Players[0].Army.Single(c=>c.Id==plainFollower.Id).Force,2,"No Battle Maiden keyword -- unaffected");
    Equal(e.GetView(0).Players[0].Home.Single(c=>c.Id==homeMaiden.Id).Force,2,"Not at this battlefield -- army-scoped, so unaffected");
    Equal(e.GetView(1).Players[1].Army.Single(c=>c.Id==enemyMaiden.Id).Force,2,"The enemy's own Battle Maiden is unaffected -- scoped to the performer's own army");

    var guard2=Add(s,"training-utaku-elite-guard",0,Zone.Battlefield,province:0); // second guard, same army
    var e2=MatchEngine.FromTestState(s);
    Equal(e2.GetView(0).Players[0].Army.Single(c=>c.Id==guard.Id).Force,3,"Two guards in one army grant each other +1F, not themselves: base 2 + 1 from the other guard");
    Equal(e2.GetView(0).Players[0].Army.Single(c=>c.Id==guard2.Id).Force,3,"Symmetric: the second guard also gets +1F from the first, not itself");
    Equal(e2.GetView(0).Players[0].Army.Single(c=>c.Id==maidenUnit.Id).Force,4,"maidenUnit now gets +1F from each of the two guards: 2 + 1 + 1");
}
static void AttachmentRestrictions()
{
    {
        // Asahina House Guard's own shape: will only attach to a Crane Clan Personality with Personal
        // Honor 2+. A Crane Clan target with sufficient Honor succeeds; one without the keyword, or
        // with insufficient Honor, is rejected -- and rejected outright even by the offered-action
        // query (not just at Accept), since AttachmentLegalTarget gates both the same way.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var guard=Add(s,"training-follower-crane-guard",0,Zone.Hand);
        var craneTarget=Add(s,"training-crane-clan-samurai",0,Zone.Home); // Crane Clan, PH 3
        var lionTarget=Add(s,"training-lion-samurai",0,Zone.Home); // no Crane Clan keyword
        var e=MatchEngine.FromTestState(s);
        var equipOption=e.QueryActions(0).Single(a=>a.Type=="equip" && a.CardId==guard.Id);
        Check(equipOption.TargetIds!.SequenceEqual([craneTarget.Id]),"Only the Crane Clan target is offered as legal");
        Reject(e,Cmd(e,"equip",id:guard.Id,sources:e.PaymentOptions(0,4).First().SourceIds,target:lionTarget.Id));
        Accept(e,Cmd(e,"equip",id:guard.Id,sources:e.PaymentOptions(0,4).First().SourceIds,target:craneTarget.Id));
        Equal(e.State.Cards[guard.Id].Zone,Zone.Attached);
    }
    {
        // Current Personal Honor, not printed: Dishonorable fixes it at 0 (blocking the Honor
        // Requirement despite printed Personal Honor 3); a Personal-Honor-boosting token still applies
        // once honorable, even against a low printed value, and keeps applying rather than being
        // consumed -- the same token is present, unconsumed, in both scenarios below.
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var guard=Add(s,"training-follower-crane-guard",0,Zone.Hand);
        var dishonored=Add(s,"training-crane-clan-samurai",0,Zone.Home); dishonored.Dishonorable=true; // printed PH 3, current 0
        var e=MatchEngine.FromTestState(s);
        Reject(e,Cmd(e,"equip",id:guard.Id,sources:e.PaymentOptions(0,4).First().SourceIds,target:dishonored.Id));

        var s2=MatchEngine.CreateEconomyScenario().State; s2.Phase=Phase.Action; s2.PriorityPlayerId=0; s2.ActivePlayerId=0;
        var guard2=Add(s2,"training-follower-crane-guard",0,Zone.Hand);
        var lowHonor=Add(s2,"training-crane-clan-low-honor",0,Zone.Home); // printed PH 1, below the requirement of 2
        lowHonor.Modifiers=[new("test-fixture","test-fixture","PersonalHonor",2)]; // current 1+2=3
        var e2=MatchEngine.FromTestState(s2);
        Accept(e2,Cmd(e2,"equip",id:guard2.Id,sources:e2.PaymentOptions(0,4).First().SourceIds,target:lowHonor.Id));
    }
    {
        // "Will not attach to a Water Personality" (Spawn of Vritra's shape).
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var follower=Add(s,"training-follower-not-water",0,Zone.Hand);
        var water=Add(s,"training-water-personality",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Reject(e,Cmd(e,"equip",id:follower.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:water.Id));
    }
    {
        // "Will only attach to a Human" -- no keyword is ever printed for it; it means "does not have
        // Nonhuman" (core rules Glossary).
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var follower=Add(s,"training-follower-human-only",0,Zone.Hand);
        var nonhuman=Add(s,"training-nonhuman-personality",0,Zone.Home);
        var human=Add(s,"training-crane-samurai",0,Zone.Home); // no Nonhuman keyword: Human by default
        var e=MatchEngine.FromTestState(s);
        Reject(e,Cmd(e,"equip",id:follower.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:nonhuman.Id));
        Accept(e,Cmd(e,"equip",id:follower.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:human.Id));
    }
    {
        // A named-Personality restriction (e.g. "Attaches to Yotsu Ueda").
        var s=MatchEngine.CreateEconomyScenario().State; s.Phase=Phase.Action; s.PriorityPlayerId=0; s.ActivePlayerId=0;
        var follower=Add(s,"training-follower-named",0,Zone.Hand);
        var named=Add(s,"training-crane-veteran",0,Zone.Home); // Name: "Crane training veteran"
        var other=Add(s,"training-crane-samurai",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Reject(e,Cmd(e,"equip",id:follower.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:other.Id));
        Accept(e,Cmd(e,"equip",id:follower.Id,sources:e.PaymentOptions(0,2).First().SourceIds,target:named.Id));
    }
}
static void NoAttachmentsFilter()
{
    {
        // A Ranged/Melee Attack target must have no attachments at all -- not just no Follower, the
        // previous (too-narrow) check. An attached Item blocks targeting just as a Follower would.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var attacker=Add(s,"oracle-l5r-1839",0,Zone.Attached,province:0,attachedTo:host.Id);
        var withItem=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0); // Force 4
        Add(s,"training-item",1,Zone.Attached,province:0,attachedTo:withItem.Id);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:attacker.Id,TargetId:withItem.Id,AbilityId:"dark-naga-scouts-attack"));
    }
    {
        // A Follower/Weapon/Armor *token* (no card attachment at all) also counts as an attachment...
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var attacker=Add(s,"oracle-l5r-1839",0,Zone.Attached,province:0,attachedTo:host.Id);
        var withToken=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0); withToken.Tokens=[new("t1","Follower","test-fixture")];
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:attacker.Id,TargetId:withToken.Id,AbilityId:"dark-naga-scouts-attack"));
    }
    {
        // ...but an ordinary stat/status token (Fire) does not -- still a legal target.
        var s=StrongholdBattle("oracle-l5r-1218");
        var host=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
        var attacker=Add(s,"oracle-l5r-1839",0,Zone.Attached,province:0,attachedTo:host.Id);
        var withFireToken=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0); withFireToken.Tokens=[new("t1","Fire","test-fixture")];
        withFireToken.Modifiers=[new("test-fixture","test-fixture","Force",-2)]; // Force 4-2=2, within Ranged 3
        var e=MatchEngine.FromTestState(s);
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:attacker.Id,TargetId:withFireToken.Id,AbilityId:"dark-naga-scouts-attack"));
        Equal(e.State.Cards[withFireToken.Id].Zone,Zone.Dead);
    }
    {
        // The generic RequireNoAttachments flag, layered onto an existing category ("enemy-force"):
        // matches "Target an enemy card without attachments" verbatim. A Personality with an
        // attachment is rejected; a clean one is destroyed; an enemy Follower (an attachment itself)
        // is always a legal target, since an attachment can never itself have attachments.
        var s=StrongholdBattle("oracle-l5r-1218");
        var destroyer=Add(s,"training-no-attachments-destroyer",0,Zone.Battlefield,province:0);
        var withItem=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
        Add(s,"training-item",1,Zone.Attached,province:0,attachedTo:withItem.Id);
        var clean=Add(s,"training-crane-samurai",1,Zone.Battlefield,province:0);
        var enemyFollower=Add(s,"training-cavalry-follower",1,Zone.Attached,province:0,attachedTo:clean.Id);
        var e=MatchEngine.FromTestState(s);
        Reject(e,new GameCommand("useAbility",0,e.Version,CardId:destroyer.Id,TargetId:withItem.Id,AbilityId:"training-destroy-no-attachments"));
        Accept(e,new GameCommand("useAbility",0,e.Version,CardId:destroyer.Id,TargetId:enemyFollower.Id,AbilityId:"training-destroy-no-attachments"));
        Equal(e.State.Cards[enemyFollower.Id].Zone,Zone.Dead,"An attachment is always a legal 'without attachments' target");
    }
}
static void ProvinceAdjacency()
{
    // Provinces sit in a line, 0-1-2-3 (the user's "Province 1..4"). A destroyed province is skipped
    // -- its former neighbors become directly adjacent instead, compounding across destructions --
    // derived on demand from Destroyed, not tracked as separate routed state ("provinces slide
    // together"). IsLegal(currentProvince, destroyed, targetProvince) checks whether a Personality at
    // targetProvince is a legal target for an ability sourced at currentProvince.
    bool IsLegal(int currentProvince,int[] destroyed,int targetProvince)
    {
        var s=MatchEngine.CreateEconomyScenario().State;
        s.Phase=Phase.Battle; s.CurrentBattleProvince=currentProvince; s.UnresolvedBattlefields=[..new[]{currentProvince,targetProvince}.Distinct()]; s.PriorityPlayerId=0;
        foreach(var d in destroyed) s.Players[0].Provinces[d].Destroyed=true;
        var source=Add(s,"training-adjacent-booster",0,Zone.Battlefield,province:currentProvince);
        var target=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:targetProvince);
        var e=MatchEngine.FromTestState(s);
        return e.Execute(new GameCommand("useAbility",0,e.Version,CardId:source.Id,TargetId:target.Id,AbilityId:"training-adjacent-boost")).Accepted;
    }
    // Baseline: 0-1, 1-2, 2-3 only. Not transitively adjacent (0 and 2 are not adjacent).
    Check(IsLegal(0,[],1),"0 adjacent to 1"); Check(IsLegal(1,[],0),"1 adjacent to 0 (symmetric)");
    Check(IsLegal(1,[],2),"1 adjacent to 2"); Check(IsLegal(2,[],3),"2 adjacent to 3");
    Check(!IsLegal(0,[],2),"0 not adjacent to 2 with nothing destroyed");
    // Province 2 (index 1) destroyed: 0 and 2 become adjacent; 2-3 is untouched.
    Check(IsLegal(0,[1],2),"0 adjacent to 2 once province 1 (index 1) is destroyed");
    Check(IsLegal(2,[1],0),"...symmetric");
    Check(IsLegal(2,[1],3),"2 still adjacent to 3 (unaffected by index 1's destruction)");
    Check(!IsLegal(0,[1],3),"0 still not adjacent to 3");
    // Province 3 (index 2) destroyed: 1 and 3 become adjacent.
    Check(IsLegal(1,[2],3),"1 adjacent to 3 once province 2 (index 2) is destroyed");
    Check(IsLegal(3,[2],1),"...symmetric");
    // Both middle provinces (index 1 and 2) destroyed: the two ends become adjacent.
    Check(IsLegal(0,[1,2],3),"0 adjacent to 3 once both middle provinces are destroyed");
    Check(IsLegal(3,[1,2],0),"...symmetric");
    // The user's own closing example: index 1 and index 3 destroyed -- only 0 and 2 remain adjacent.
    Check(IsLegal(0,[1,3],2),"0 adjacent to 2 when index 1 and index 3 are both destroyed");
    Check(IsLegal(2,[1,3],0),"...symmetric");

    // Not hardcoded to 4 provinces: AdjacentProvinces scans however many entries Provinces actually
    // has, so a player with more than the usual 4 (some Stronghold effect, say) is handled exactly
    // the same way, with no changes needed. Proven directly here with 6.
    bool IsLegalWithProvinces(int provinceCount,int currentProvince,int[] destroyed,int targetProvince)
    {
        var s=MatchEngine.CreateEconomyScenario().State;
        while(s.Players[0].Provinces.Count<provinceCount) s.Players[0].Provinces.Add(new(){Index=s.Players[0].Provinces.Count,Strength=6});
        s.Phase=Phase.Battle; s.CurrentBattleProvince=currentProvince; s.UnresolvedBattlefields=[..new[]{currentProvince,targetProvince}.Distinct()]; s.PriorityPlayerId=0;
        foreach(var d in destroyed) s.Players[0].Provinces[d].Destroyed=true;
        var source=Add(s,"training-adjacent-booster",0,Zone.Battlefield,province:currentProvince);
        var target=Add(s,"training-crane-samurai",0,Zone.Battlefield,province:targetProvince);
        var e=MatchEngine.FromTestState(s);
        return e.Execute(new GameCommand("useAbility",0,e.Version,CardId:source.Id,TargetId:target.Id,AbilityId:"training-adjacent-boost")).Accepted;
    }
    Check(IsLegalWithProvinces(6,4,[],5),"index 4 adjacent to index 5 with 6 provinces");
    Check(!IsLegalWithProvinces(6,0,[],5),"index 0 still not adjacent to index 5 (not transitive)");
    Check(IsLegalWithProvinces(6,3,[4],5),"index 3 adjacent to index 5 once index 4 (a middle province) is destroyed, with 6 provinces");
}
