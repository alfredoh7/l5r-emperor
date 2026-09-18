using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Recon-action classification and the repeatable "after the resolution of your [keyword] action" trait (issue #321,
// PR B of #309): Daidoji Ebizo (personality-wave-26), Master Scouts (follower-wave-15), and their integration with
// Daidoji Yuki's own Recon Reaction (personality-wave-25). Each test builds an explicit position and asserts both the
// positive path and the cases that must NOT fire; see docs/decisions/2026-09-18-recon-action-tag-and-daidoji-ebizo.md.
internal static class ReconActionTests
{
    private const string Ebizo = "oracle-l5r-1629", Yuki = "oracle-l5r-1696", MasterScouts = "oracle-l5r-4867";
    private const string Actor = "training-recon-actor", KeywordStrategy = "training-recon-keyword-strategy", PlainStrategy = "training-plain-strategy";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Daidoji Ebizo / Master Scouts: published shape and the stated Recon-action classification", PublishedShape),
            ("Ebizo: +1F on each of his controller's Recon actions -- two in one turn are +2 -- and the bonus is the live Force", FiresOnEachReconAction),
            ("Ebizo: in force wherever he is in play (Home, a battlefield, bowed), never out of play, never for his own non-Recon action", FiresAnywhereInPlay),
            ("Ebizo: not for the opponent's Recon actions, not for non-Recon actions, each controller's own copy fires only for their own", OnlyOwnReconActions),
            ("Recon classification: an ability's printed Recon tag OR its card's Recon keyword makes it a Recon action; neither does not", TagOrKeyword),
            ("Ebizo: a Recon Attack that defers behind an attack reaction window fires when it RESOLVES, exactly once", DeferredReconAttack),
            ("Ebizo: a Recon duel-issuing action fires when the DUEL resolves, exactly once", DeferredReconDuel),
            ("Ebizo: Daidoji Yuki's own Recon Reaction (attacking or defending) triggers his controller's Ebizo, and only theirs", YukiTriggersEbizo),
            ("Ebizo: Ranged Attack with strength equal to his live Force (a Recon action's +1F raises it; a stronger target survives)", EbizoRangedAttack),
            ("Ebizo: the +1F lasts until the end of the turn and is stackable, like Apprentice Shinobi's and every other action-granted bonus", BonusEndsWithTheTurn),
            ("Master Scouts: the same trait on a Follower (+1F to itself per own Recon action, not the host's), by Effect alone", MasterScoutsTrait),
        ];
        runner.Run("recon-action",tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok,string message="Assertion failed") { if(!ok) throw new Exception(message); }
    private static void Eq<T>(T actual,T expected,string message="Values differ") => Check(EqualityComparer<T>.Default.Equals(actual,expected),$"{message}: expected {expected}; got {actual}");
    private static CardInstance Add(MatchState s,string def,int owner,Zone zone,bool bowed=false,int? province=null,string? parent=null)
    {
        var c=new CardInstance{Id=$"rc-{s.Cards.Count}",DefinitionId=def,OwnerId=owner,Zone=zone,FaceUp=zone!=Zone.Hand,Bowed=bowed,ProvinceIndex=province,AttachedTo=parent};
        s.Cards.Add(c.Id,c);
        if(zone==Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }
    // An Action-phase table (economy scenario, published Strongholds so player abilities exist), player 0 to act.
    private static MatchState ActionTable(int priority=0)
    {
        var s=MatchEngine.CreateEconomyScenario().State;
        foreach(var p in s.Players) s.Cards[p.StrongholdId].DefinitionId="oracle-l5r-1218";
        s.Phase=Phase.Action; s.PriorityPlayerId=priority; s.ActivePlayerId=0;
        return s;
    }
    // The Battle Selection moment (as in the battle-timing tests): player 0 attacks the Defender's province 0.
    private static MatchState BattleTable()
    {
        var s=MatchEngine.CreateEconomyScenario().State;
        foreach(var p in s.Players) s.Cards[p.StrongholdId].DefinitionId="oracle-l5r-1218";
        s.Phase=Phase.Battle; s.ActivePlayerId=0; s.PriorityPlayerId=0; s.CurrentBattleProvince=0; s.UnresolvedBattlefields=[0];
        return s;
    }
    private static GameCommand Use(MatchEngine e,int player,string ability,string? card=null,string? performer=null,string? target=null)
        =>new("useAbility",player,e.Version,CardId:card,AbilityId:ability,PerformerId:performer,TargetId:target);
    private static GameCommand PassC(MatchEngine e,int? player=null)=>new("pass",player??e.State.PriorityPlayerId,e.Version);
    private static GameCommand Decline(MatchEngine e,int? player=null)=>new("declineReaction",player??e.State.PriorityPlayerId,e.Version);
    private static int Force(MatchEngine e,CardInstance card)
    {
        var view=e.GetView(0);
        return view.Players.SelectMany(p=>p.Home.Concat(p.Army)).Single(c=>c.Id==card.Id).Force;
    }
    private static int Bonuses(MatchEngine e,CardInstance card)=>e.State.Cards[card.Id].Modifiers.Count(m=>m.Effect=="force-after-own-recon-action");

    // ---- published shape and classification ---------------------------------------------------------------------
    private static void PublishedShape()
    {
        var ebizo=CardCatalog.Get(Ebizo);
        Eq(PublishedContent.PersonalityWave26.Cards.Length,1,"personality-wave-26 is exactly Ebizo");
        Check(!ebizo.ScenarioOnly && ebizo.Type==CardType.Personality);
        Eq(ebizo.Name,"Daidoji Ebizo"); Eq(ebizo.Clan,"Crane");
        Eq(ebizo.GoldCost,9); Eq(ebizo.Force,4); Eq(ebizo.Chi,4); Eq(ebizo.PersonalHonor,2); Eq(ebizo.HonorRequirement,(int?)4);
        foreach(var k in new[]{"Crane Clan","Samurai","Bounty Hunter","Iron Crane","Scout"}) Check(ebizo.Keywords!.Contains(k),$"printed keyword {k}");
        Check(!ebizo.Keywords!.Contains("Unique"),"Ebizo is not Unique");
        Eq(ebizo.Text,"After the resolution of your Recon action: Give Ebizo +1F.\nBattle: Ranged Attack with strength equal to Ebizo's Force.");
        Eq(string.Join(",",ebizo.Abilities!),"ebizo-attack"); Eq(ebizo.Effect,"force-after-own-recon-action");
        Eq(AbilityCatalog.All["ebizo-attack"].Timing,"Battle");
        Check(AbilityCatalog.All["ebizo-attack"].ActionKeywords is null,"His attack is not itself a Recon action");
        var scouts=CardCatalog.Get(MasterScouts);
        Eq(PublishedContent.FollowerWave15.Cards.Length,1);
        Check(!scouts.ScenarioOnly && scouts.Type==CardType.Follower);
        Eq(scouts.GoldCost,3); Eq(scouts.Force,2); Eq(scouts.Chi,0); Eq(scouts.HonorRequirement,(int?)1); Eq(scouts.Focus,3);
        foreach(var k in new[]{"Elite","Gaijin","Scout"}) Check(scouts.Keywords!.Contains(k),$"Master Scouts printed keyword {k}");
        Eq(scouts.Text,"After the resolution of a Recon action you took: Give this card +1F."); Eq(scouts.Effect,"force-after-own-recon-action"); Eq(scouts.Abilities!.Length,0);
        // The classification: exactly Yuki's real ability and the training fixtures print a Recon tag; every published Recon-prefixed text is tagged.
        Eq(string.Join(",",AbilityCatalog.All.Where(a=>a.Value.ActionKeywords is not null).Select(a=>a.Key).OrderBy(x=>x,StringComparer.Ordinal)),"training-recon-attack,training-recon-duel,training-recon-open,yuki-engage-recon");
    }

    // ---- the trait ----------------------------------------------------------------------------------------------
    private static void FiresOnEachReconAction()
    {
        var s=ActionTable();
        var ebizo=Add(s,Ebizo,0,Zone.Home); var a=Add(s,Actor,0,Zone.Home); var b=Add(s,Actor,0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Eq(Force(e,ebizo),4);
        Accept(e,Use(e,0,"training-recon-open",a.Id));
        Eq(Bonuses(e,ebizo),1); Eq(Force(e,ebizo),5,"+1F after the first Recon action");
        Check(e.GetView(0).Events.Any(ev=>ev.Type=="actionTraitFired" && ev.CardId==ebizo.Id),"The trait announces itself");
        Accept(e,PassC(e)); // the opponent passes; the same player acts again
        Accept(e,Use(e,0,"training-recon-open",b.Id));
        Eq(Bonuses(e,ebizo),2,"Repeatable: every time, no once-per-turn limit"); Eq(Force(e,ebizo),6,"two Recon actions in one turn are +2");
        // The action that just resolved is what matters: the same ability on a card already used this turn is refused (the ability's own limit), not the trait's.
        Accept(e,PassC(e));
        Reject(e,Use(e,0,"training-recon-open",a.Id));
        Eq(Bonuses(e,ebizo),2,"A refused action fires nothing");
    }

    private static void FiresAnywhereInPlay()
    {
        // At a battlefield, unbowed: a Recon Battle action from a card ELSEWHERE in his army resolves.
        {
            var s=BattleTable();
            var ebizo=Add(s,Ebizo,0,Zone.Battlefield,province:0); var actor=Add(s,Actor,0,Zone.Battlefield,province:0); Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
            s.PriorityPlayerId=0;
            var e=MatchEngine.FromTestState(s);
            Accept(e,Use(e,0,"training-recon-attack",actor.Id));
            Eq(Bonuses(e,ebizo),1,"At the battlefield");
        }
        // At Home (not at the battle at all) and bowed: "regardless of Ebizo's location", and traits are usable while bowed.
        foreach(var bowed in new[]{false,true})
        {
            var s=BattleTable();
            var ebizo=Add(s,Ebizo,0,Zone.Home,bowed:bowed); var actor=Add(s,Actor,0,Zone.Battlefield,province:0); Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
            var e=MatchEngine.FromTestState(s);
            Accept(e,Use(e,0,"training-recon-attack",actor.Id));
            Eq(Bonuses(e,ebizo),1,bowed?"At Home, bowed":"At Home");
        }
        // Not in play: a face-up province card, a discard pile, the dead pile, a hand -- no bonus, and nothing else changes.
        foreach(var zone in new[]{Zone.Province,Zone.DynastyDiscard,Zone.Dead,Zone.Hand,Zone.FateDiscard})
        {
            var s=ActionTable();
            var ebizo=Add(s,Ebizo,0,zone,province:zone==Zone.Province?0:null); var actor=Add(s,Actor,0,Zone.Home);
            if(zone==Zone.Province) s.Players[0].Provinces[0].CardId=ebizo.Id;
            var e=MatchEngine.FromTestState(s);
            Accept(e,Use(e,0,"training-recon-open",actor.Id));
            Eq(Bonuses(e,ebizo),0,$"{zone}: out of play");
        }
        // His own Ranged Attack is not a Recon action: no self-trigger, so the bonus comes only from actual Recon actions.
        {
            var s=BattleTable();
            var ebizo=Add(s,Ebizo,0,Zone.Battlefield,province:0); Add(s,"training-lion-veteran",1,Zone.Battlefield,province:0);
            var e=MatchEngine.FromTestState(s);
            Accept(e,Use(e,0,"ebizo-attack",ebizo.Id));
            Eq(Bonuses(e,ebizo),0);
        }
    }

    private static void OnlyOwnReconActions()
    {
        var s=ActionTable();
        var mine=Add(s,Ebizo,0,Zone.Home); var theirs=Add(s,Ebizo,1,Zone.Home);
        var myActor=Add(s,Actor,0,Zone.Home); var theirActor=Add(s,Actor,1,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        // The opponent's Recon action (on their own priority): not "your Recon action" for player 0's Ebizo, but player 1's own Ebizo is theirs.
        Accept(e,Use(e,0,"training-plain-open",myActor.Id)); // a non-Recon action of mine: nobody's Ebizo reacts
        Eq(Bonuses(e,mine),0,"a non-Recon action is not a Recon action"); Eq(Bonuses(e,theirs),0);
        Accept(e,Use(e,1,"training-recon-open",theirActor.Id));
        Eq(Bonuses(e,mine),0,"The opponent's Recon action does not trigger my Ebizo");
        Eq(Bonuses(e,theirs),1,"but it triggers THEIR Ebizo");
        Accept(e,Use(e,0,"training-recon-open",myActor.Id));
        Eq(Bonuses(e,mine),1,"my own Recon action triggers mine"); Eq(Bonuses(e,theirs),1,"and not theirs");
    }

    private static void TagOrKeyword()
    {
        var s=ActionTable();
        var ebizo=Add(s,Ebizo,0,Zone.Home);
        var tagged=Add(s,Actor,0,Zone.Home);                  // the ability prints Recon (its card does not have the keyword)
        var keyword=Add(s,KeywordStrategy,0,Zone.Hand);       // an UNTAGGED ability on a card with the Recon keyword
        var plain=Add(s,PlainStrategy,0,Zone.Hand);           // the same untagged ability, no keyword: the control
        var untaggedOnActor=tagged;                           // training-plain-open on the same actor (no Recon keyword on the card)
        Check(!CardCatalog.Get(Actor).Keywords!.Contains("Recon") && AbilityCatalog.All["training-plain-open"].ActionKeywords is null);
        var e=MatchEngine.FromTestState(s);
        Accept(e,Use(e,0,"training-plain-open",plain.Id));
        Eq(Bonuses(e,ebizo),0,"a plain ability on a plain card is not a Recon action"); Accept(e,PassC(e));
        Accept(e,Use(e,0,"training-plain-open",untaggedOnActor.Id));
        Eq(Bonuses(e,ebizo),0,"a plain ability on a card that is a Scout (not Recon) is not one either"); Accept(e,PassC(e));
        Accept(e,Use(e,0,"training-plain-open",keyword.Id));
        Eq(Bonuses(e,ebizo),1,"the SAME ability on a Recon-keyword card is a Recon action: an ability inherits its card's keywords"); Accept(e,PassC(e));
        Accept(e,Use(e,0,"training-recon-open",tagged.Id));
        Eq(Bonuses(e,ebizo),2,"a printed Recon tag on the ability is one too");
    }

    private static void DeferredReconAttack()
    {
        // Immediate path (nothing can react): exactly one bonus, when the action resolves.
        {
            var s=BattleTable();
            var ebizo=Add(s,Ebizo,0,Zone.Home); var actor=Add(s,Actor,0,Zone.Battlefield,province:0); Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
            var e=MatchEngine.FromTestState(s);
            Accept(e,Use(e,0,"training-recon-attack",actor.Id));
            Eq(Bonuses(e,ebizo),1,"an immediately-resolving Recon Attack fires once, not twice");
        }
        // Deferred path: an attack reaction is available, so the Attack waits; Ebizo's trait waits for its RESOLUTION.
        {
            var s=BattleTable();
            var ebizo=Add(s,Ebizo,0,Zone.Home); var actor=Add(s,Actor,0,Zone.Battlefield,province:0);
            Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0); Add(s,"training-attack-reactor",1,Zone.Battlefield,province:0);
            var e=MatchEngine.FromTestState(s);
            Accept(e,Use(e,0,"training-recon-attack",actor.Id));
            Eq(e.State.PendingChoice?.Type,"attackReaction","The Recon Attack is pending, open to reaction");
            Eq(Bonuses(e,ebizo),0,"Announced, not yet resolved: no bonus yet");
            Accept(e,Decline(e)); Accept(e,Decline(e));
            Check(e.State.PendingChoice is null && e.State.PendingAttack is null);
            Eq(Bonuses(e,ebizo),1,"Resolved: the bonus lands, exactly once");
        }
    }

    private static void DeferredReconDuel()
    {
        // A duel-issuing Recon action resolves when the DUEL does: the trait fires once its outcome has applied, not at the challenge.
        var s=BattleTable();
        var ebizo=Add(s,Ebizo,0,Zone.Home); var actor=Add(s,Actor,0,Zone.Battlefield,province:0); var target=Add(s,"training-lion-samurai",1,Zone.Battlefield,province:0);
        var e=MatchEngine.FromTestState(s);
        Accept(e,Use(e,0,"training-recon-duel",actor.Id,target:target.Id));
        Check(e.State.PendingDuel is not null,"the duel is under way");
        Eq(Bonuses(e,ebizo),0,"announced, not yet resolved");
        Accept(e,new GameCommand("strike",e.State.PriorityPlayerId,e.Version)); // the challenged player has the first option: strike at once
        Check(e.State.PendingDuel is null,"the duel is over");
        Eq(Bonuses(e,ebizo),1,"the Recon duel action resolved: exactly one bonus");
    }

    // ---- Daidoji Yuki (a Recon Reaction) ------------------------------------------------------------------------
    private static void YukiTriggersEbizo()
    {
        // Attacking: Yuki and Ebizo (both Scouts) at the battlefield; Yuki's Recon Reaction inside the Engage window is a Recon action.
        {
            var s=BattleTable(); s.Phase=Phase.BattleSelection; s.CurrentBattleProvince=null;
            var yuki=Add(s,Yuki,0,Zone.Battlefield,province:0); var ebizo=Add(s,Ebizo,0,Zone.Battlefield,province:0);
            var theirEbizo=Add(s,Ebizo,1,Zone.Home); Add(s,"training-scout-samurai",1,Zone.Battlefield,province:0);
            var e=MatchEngine.FromTestState(s);
            Accept(e,new GameCommand("selectBattle",0,e.Version,Province:0));
            Eq(e.State.PendingChoice?.Type,"engageReaction");
            Accept(e,Use(e,0,"yuki-engage-recon",yuki.Id));
            Eq(Bonuses(e,ebizo),1,"Yuki's Recon Reaction triggers Ebizo (a Reaction is an action too)");
            Eq(Bonuses(e,theirEbizo),0,"and not the opponent's Ebizo");
            Check(e.State.Players[1].Provinces[0].Reconnaissance[0],"(and Yuki did his own job: Reconnaissance for the Attacker)");
        }
        // Defending: a defending Yuki triggers HIS controller's Ebizo, whichever side the battle is on -- and not the attacker's.
        {
            var s=BattleTable(); s.Phase=Phase.BattleSelection; s.CurrentBattleProvince=null;
            Add(s,"training-crane-samurai",0,Zone.Battlefield,province:0);
            var attackerEbizo=Add(s,Ebizo,0,Zone.Home);
            var yuki=Add(s,Yuki,1,Zone.Battlefield,province:0); var defenderEbizo=Add(s,Ebizo,1,Zone.Battlefield,province:0);
            var e=MatchEngine.FromTestState(s);
            Accept(e,new GameCommand("selectBattle",0,e.Version,Province:0));
            Accept(e,Decline(e)); // the Attacker has no reaction to play: pass, so the Defender's opportunity comes
            Accept(e,Use(e,1,"yuki-engage-recon",yuki.Id));
            Eq(Bonuses(e,defenderEbizo),1,"A defending Yuki triggers his controller's Ebizo"); Eq(Bonuses(e,attackerEbizo),0);
        }
    }

    // ---- the Ranged Attack --------------------------------------------------------------------------------------
    private static void EbizoRangedAttack()
    {
        (MatchEngine E,CardInstance Ebizo,CardInstance Target,CardInstance A,CardInstance B) Build(string target)
        {
            var s=BattleTable();
            var ebizo=Add(s,Ebizo,0,Zone.Battlefield,province:0);
            var a=Add(s,Actor,0,Zone.Battlefield,province:0); var b=Add(s,Actor,0,Zone.Battlefield,province:0);
            var t=Add(s,target,1,Zone.Battlefield,province:0);
            s.PriorityPlayerId=0;
            return (MatchEngine.FromTestState(s),ebizo,t,a,b);
        }
        // Force 4 (his printed Force): destroys a Force-4 Personality, not a Force-6 one.
        {
            var (e,ebizo,target,_,_)=Build("training-lion-samurai"); // Force 4
            Accept(e,Use(e,0,"ebizo-attack",ebizo.Id,target:target.Id));
            Eq(e.State.Cards[target.Id].Zone,Zone.Dead,"strength 4 destroys a Force-4 target");
        }
        {
            var (e,ebizo,target,_,_)=Build("training-lion-veteran"); // Force 6
            Reject(e,Use(e,0,"ebizo-attack",ebizo.Id,target:target.Id)); // a declared target above the Attack's strength (4) is not a legal target
            Accept(e,Use(e,0,"ebizo-attack",ebizo.Id)); // ...but the action itself needs no target up front
            Eq(e.State.Cards[target.Id].Zone,Zone.Battlefield,"strength 4 cannot destroy a Force-6 target");
        }
        // Two Recon actions first: Ebizo is Force 6 now, so the SAME attack destroys the Force-6 target -- the strength is his live Force.
        {
            var (e,ebizo,target,a,b)=Build("training-lion-veteran");
            Accept(e,Use(e,0,"training-recon-attack",a.Id)); Accept(e,PassC(e));
            Accept(e,Use(e,0,"training-recon-attack",b.Id)); Accept(e,PassC(e));
            Eq(Force(e,ebizo),6);
            Accept(e,Use(e,0,"ebizo-attack",ebizo.Id,target:target.Id));
            Eq(e.State.Cards[target.Id].Zone,Zone.Dead,"strength 6 (his live Force) destroys a Force-6 target");
        }
        // A bowed Ebizo cannot use his ability (only the trait works bowed).
        {
            var (e,ebizo,target,_,_)=Build("training-lion-samurai");
            var s=e.State; s.Cards[ebizo.Id].Bowed=true;
            var e2=MatchEngine.FromTestState(s);
            Reject(e2,Use(e2,0,"ebizo-attack",ebizo.Id,target:target.Id));
        }
    }

    // ---- duration and Master Scouts ----------------------------------------------------------------------------
    private static void BonusEndsWithTheTurn()
    {
        var s=ActionTable();
        var ebizo=Add(s,Ebizo,0,Zone.Home); var a=Add(s,Actor,0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,Use(e,0,"training-recon-open",a.Id));
        Eq(Force(e,ebizo),5);
        Check(e.State.Cards[ebizo.Id].Modifiers.Single().Amount==1 && e.State.Cards[ebizo.Id].Modifiers.Single().Stat=="Force","an ordinary modifier, cleared with every other one at turn end");
        // Play the rest of the turn out: everyone passes through Action, the Attack phase (nothing declared) and Dynasty.
        var guard=0;
        while(e.State.Phase!=Phase.Dynasty && guard++<40) Accept(e,PassC(e));
        Eq(e.State.Phase,Phase.Dynasty);
        Eq(Force(e,ebizo),5,"still +1F during the rest of the same turn");
        Accept(e,new GameCommand("endDynasty",e.State.PriorityPlayerId,e.Version));
        if(e.State.PendingChoice?.Type=="discardHand")
        {
            var pending=e.State.PendingChoice; var hand=e.State.Players[pending.PlayerId].Hand;
            Accept(e,new GameCommand("discardHand",pending.PlayerId,e.Version,CardIds:hand.Take(hand.Count-8).ToArray()));
        }
        Eq(e.State.TurnNumber,2,"the next turn has begun");
        Eq(e.State.Cards[ebizo.Id].Modifiers.Count,0,"the +1F ended with the turn");
        Eq(Force(e,ebizo),4);
    }

    private static void MasterScoutsTrait()
    {
        var s=ActionTable();
        var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var scouts=Add(s,MasterScouts,0,Zone.Attached,parent:host.Id);
        var otherHost=Add(s,"training-crane-veteran",0,Zone.Home);
        var otherScouts=Add(s,MasterScouts,0,Zone.Attached,parent:otherHost.Id);
        var theirHost=Add(s,"training-lion-samurai",1,Zone.Home);
        var theirScouts=Add(s,MasterScouts,1,Zone.Attached,parent:theirHost.Id);
        var a=Add(s,Actor,0,Zone.Home); var plain=Add(s,PlainStrategy,0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        int ForceOf(CardInstance c)=>e.GetView(0).Players.SelectMany(p=>p.Home.Concat(p.Army)).Single(x=>x.Id==c.Id).Force;
        Eq(ForceOf(scouts),2);
        Accept(e,Use(e,0,"training-plain-open",plain.Id)); Accept(e,PassC(e));
        Eq(ForceOf(scouts),2,"a non-Recon action does nothing");
        Accept(e,Use(e,0,"training-recon-open",a.Id));
        Eq(ForceOf(scouts),3,"+1F to the Master Scouts itself"); Eq(ForceOf(otherScouts),3,"every copy of the controller's own");
        Eq(ForceOf(theirScouts),2,"never the opponent's");
        Eq(e.State.Cards[host.Id].Modifiers.Count,0,"its host is untouched: 'this card' is the Follower");
        Accept(e,PassC(e));
        // The same trigger as Ebizo's, wired by Effect alone: both in play, one Recon action, both react.
        var s2=ActionTable();
        var ebizo=Add(s2,Ebizo,0,Zone.Home); var host2=Add(s2,"training-crane-samurai",0,Zone.Home); var scouts2=Add(s2,MasterScouts,0,Zone.Attached,parent:host2.Id); var a2=Add(s2,Actor,0,Zone.Home);
        var e2=MatchEngine.FromTestState(s2);
        Accept(e2,Use(e2,0,"training-recon-open",a2.Id));
        Eq(e2.State.Cards[ebizo.Id].Modifiers.Count(m=>m.Effect=="force-after-own-recon-action"),1); Eq(e2.State.Cards[scouts2.Id].Modifiers.Count(m=>m.Effect=="force-after-own-recon-action"),1);
    }
}
