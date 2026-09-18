using L5R.Core;
using static RegressionAssertions;

internal static class StabilizationTests
{
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    static MatchState State(bool battle=false)
    {
        var s=MatchEngine.CreateEconomyScenario().State;
        s.Phase=battle?Phase.Battle:Phase.Action;s.ActivePlayerId=0;s.PriorityPlayerId=0;
        if(battle){s.CurrentBattleProvince=0;s.UnresolvedBattlefields=[0];}
        return s;
    }
    static CardInstance Add(MatchState s,string definition,int owner,Zone zone,int? province=null,string? attached=null)
    {
        var c=new CardInstance{Id="stabilization-"+s.Cards.Count,DefinitionId=definition,OwnerId=owner,Zone=zone,FaceUp=true,ProvinceIndex=province,AttachedTo=attached};
        s.Cards.Add(c.Id,c);if(zone==Zone.Hand)s.Players[owner].Hand.Add(c.Id);return c;
    }
    static void Keyword(CardInstance c,params string[] words) => c.GrantedKeywords=words.Select(w=>new GrantedKeyword(w,"fixture","Permanent")).ToList();
    static void PassReaction(MatchEngine e)
    {
        for(var n=0;e.State.PendingChoice is not null && n<5;n++)
            Accept(e,e.QueryActions(e.State.PriorityPlayerId).Single(a=>a.Type=="declineReaction").Command!);
        Check(e.State.PendingChoice is null,"Reaction never resumed play");
    }
    static ActionOption Offered(MatchEngine e,string ability) => e.QueryActions(e.State.PriorityPlayerId).Single(a=>a.Enabled&&a.Command?.AbilityId==ability);
    static void VerifyOffers(MatchEngine e)
    {
        var state=e.State;OfferedCommandsValidate(e,()=>MatchEngine.FromTestState(state),alternateTargets:true);
        Check(e.QueryActions(1-state.PriorityPlayerId).All(a=>a.Type is "wait" or "concede"),"Inactive seat received decisions");
    }
    public static void Brash()
    {
        var s=State();s.Phase=Phase.AttackDeclaration;
        var first=Add(s,"training-brash-personality",0,Zone.Home);var second=Add(s,"training-brash-personality",0,Zone.Home);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new("attack",0,e.Version,Assignments:new(){[first.Id]=0,[second.Id]=1}));
        var count=e.State.Players[1].Hand.Count;
        Check(e.QueryActions(1).Count(a=>a.Type=="brashDraw")==2,"Brash draw/decline not offered");VerifyOffers(e);
        var stale=e.QueryActions(1).Single(a=>a.Command?.Choice=="draw").Command!;
        Accept(e,e.QueryActions(1).Single(a=>a.Command?.Choice=="decline").Command!);Reject(e,stale);
        Accept(e,e.QueryActions(1).Single(a=>a.Command?.Choice=="draw").Command!);
        Check(e.State.PendingChoice is null&&e.State.Phase==Phase.DefenseAssignment&&e.State.Players[1].Hand.Count==count+1,"Brash did not resume assignments");
    }
    public static void BowReactions()
    {
        foreach(var home in new[]{false,true})
        {
            var s=State(!home);var zone=home?Zone.Home:Zone.Battlefield;int? province=home?null:0;
            var watcher=Add(s,home?"oracle-l5r-7789":"oracle-l5r-8521",0,zone,province);
            var bower=Add(s,home?"training-bow-enemy-source":"training-battle-bower",0,zone,province);
            var enemy=Add(s,"training-lion-samurai",1,zone,province);
            var target=home?Add(s,"training-crane-samurai",0,zone):enemy;
            var e=MatchEngine.FromTestState(s);
            Accept(e,new("useAbility",0,e.Version,CardId:bower.Id,TargetId:enemy.Id,AbilityId:home?"training-bow-enemy":"training-bow-at-battle"));
            var action=Offered(e,home?"ruya-bow":"hizumi-negate-straighten");VerifyOffers(e);
            Check(action.TargetIds!.Contains(target.Id),"Legal reaction target omitted");
            Accept(e,action.Command! with {TargetId=target.Id});
            Check(!e.QueryActions(0).Any(a=>a.Enabled&&a.Command?.AbilityId==action.Command!.AbilityId),"Used reaction remains enabled");
            PassReaction(e);
            Check(home?e.State.Cards[target.Id].Bowed:e.State.Cards[target.Id].StraightenBlockedTurn==e.State.TurnNumber,"Bow reaction did not resolve");
        }
    }
    public static void BeforeAction()
    {
        foreach(var react in new[]{false,true})
        {
            var s=State(true);var destroyer=Add(s,"training-no-attachments-destroyer",0,Zone.Battlefield,0);
            var watcher=Add(s,"training-rokai",1,Zone.Battlefield,0);var target=Add(s,"training-lion-samurai",1,Zone.Battlefield,0);
            var e=MatchEngine.FromTestState(s);
            Accept(e,new("useAbility",0,e.Version,CardId:destroyer.Id,TargetId:target.Id,AbilityId:"training-destroy-no-attachments"));
            var action=Offered(e,"rokai-negate-destruction");VerifyOffers(e);
            if(react)Accept(e,action.Command!);
            PassReaction(e);
            Check(e.State.Cards[target.Id].Zone==(react?Zone.Battlefield:Zone.Dead),"Deferred destroy resolved incorrectly");
            Check(e.State.PendingNegatableAbility is null,"Deferred action retained");
        }
        var st=State(true);var mover=Add(st,"training-battle-straightener",0,Zone.Battlefield,0);
        var bowed=Add(st,"training-crane-samurai",1,Zone.Battlefield,0);bowed.Bowed=true;
        Add(st,"training-tamoko",1,Zone.Battlefield,0);
        var tamoko=MatchEngine.FromTestState(st);
        Accept(tamoko,new("useAbility",0,tamoko.Version,CardId:mover.Id,TargetId:bowed.Id,AbilityId:"training-straighten-at-battle"));
        VerifyOffers(tamoko);Accept(tamoko,Offered(tamoko,"tamoko-negate").Command!);PassReaction(tamoko);
        Check(tamoko.State.Cards[bowed.Id].Bowed,"Tamoko failed to negate straightening");
    }
    public static void HoldingProduction()
    {
        foreach(var (id,production) in new[]{("oracle-l5r-430",2),("oracle-l5r-7291",5),("oracle-l5r-7875",2),("oracle-l5r-8721",2)})
        {
            Check(CardCatalog.Get(id).GoldProduction==production,id+" missing printed Gold production");
            var s=State();s.Players[0].Clan="Lion";foreach(var c in s.Cards.Values.Where(c=>c.OwnerId==0))c.Bowed=true;
            var holding=Add(s,id,0,Zone.Home);var host=Add(s,"training-lion-samurai",0,Zone.Home);var item=Add(s,"oracle-l5r-6481",0,Zone.Hand);
            var e=MatchEngine.FromTestState(s);
            Check(e.PaymentOptions(0,2).Any(p=>p.SourceIds.SequenceEqual(new[]{holding.Id})&&p.Produced==production),"Holding cannot fund payment");
            Accept(e,new("equip",0,e.Version,CardId:item.Id,TargetId:host.Id,SourceIds:[holding.Id]));
            Check(e.State.Cards[holding.Id].Bowed&&e.State.Cards[item.Id].Zone==Zone.Attached,"Holding payment failed");
        }
    }
    public static void WeaknessTarget()
    {
        foreach(var modified in new[]{false,true})
        {
            var s=State();var strategy=Add(s,"oracle-l5r-8425",0,Zone.Hand);
            var monk=Add(s,"training-crane-samurai",0,Zone.Home);Keyword(monk,"Monk");
            var target=Add(s,"training-lion-samurai",1,Zone.Home);
            if(modified)target.Modifiers.Add(new("fixture","Turn","Force",-1)); // live 3 matches the Monk, differs from its own printed 4
            var e=MatchEngine.FromTestState(s);var cmd=new GameCommand("useAbility",0,e.Version,CardId:strategy.Id,PerformerId:monk.Id,TargetId:target.Id,AbilityId:"weakness-of-man-toggle");
            var listed=e.QueryActions(0).Any(a=>a.Enabled&&a.Command?.AbilityId==cmd.AbilityId&&(a.TargetIds??[]).Contains(target.Id));
            Check(listed==modified,"Weakness compares against performer instead of target base");
            if(modified)Accept(e,cmd);else Reject(e,cmd);
        }
    }
    public static void Shadowlands()
    {
        foreach(var shadow in new[]{false,true})
        {
            var s=State(true);var strat=Add(s,"oracle-l5r-2029",0,Zone.Hand);
            var performer=Add(s,"training-crane-samurai",0,Zone.Battlefield,0);Keyword(performer,"Magistrate");
            var target=Add(s,"training-lion-samurai",1,Zone.Battlefield,0);if(shadow)Keyword(target,"Shadowlands");
            var e=MatchEngine.FromTestState(s);Accept(e,new("useAbility",0,e.Version,CardId:strat.Id,PerformerId:performer.Id,TargetId:target.Id,AbilityId:"dispensing-justice-destroy"));
            Check(e.State.Cards[target.Id].Zone==(shadow?Zone.Dead:Zone.Home),"Justice ignored Shadowlands keyword");
            s=State(true);strat=Add(s,"oracle-l5r-1121",0,Zone.Hand);performer=Add(s,"training-crane-samurai",0,Zone.Battlefield,0);
            Keyword(performer,shadow?["Paragon","Shadowlands"]:["Paragon"]);target=Add(s,"training-lion-samurai",1,Zone.Battlefield,0);
            e=MatchEngine.FromTestState(s);Accept(e,new("useAbility",0,e.Version,CardId:strat.Id,PerformerId:performer.Id,TargetId:target.Id,AbilityId:"bow-before-my-will"));
            Check((e.State.Cards[target.Id].StraightenBlockedTurn==e.State.TurnNumber)==shadow,"Bow Before My Will ignored Shadowlands keyword");
            s=State();performer=Add(s,"training-shugenja",0,Zone.Home);var spell=Add(s,"oracle-l5r-9344",0,Zone.Attached,attached:performer.Id);
            target=Add(s,"training-crane-samurai",1,Zone.Home);if(shadow)Keyword(target,"Shadowlands");var honor=s.Players[1].Honor;
            e=MatchEngine.FromTestState(s);Accept(e,new("useAbility",0,e.Version,CardId:spell.Id,TargetId:target.Id,AbilityId:"winds-of-dismissal-choice"));
            Check(e.State.Players[1].Honor==honor-(shadow?CardCatalog.Get(target.DefinitionId).PersonalHonor:0),"Winds ignored Shadowlands keyword");
        }
    }
    public static void FateCosts()
    {
        foreach(var grove in new[]{true,false})foreach(var enough in new[]{false,true})
        {
            var s=State(true);s.ActivePlayerId=1;
            foreach(var id in s.Players[0].FateDeck)s.Cards[id].Zone=Zone.FateDiscard;s.Players[0].FateDeck.Clear();
            var required=grove?3:1;for(var n=0;n<required-(enough?0:1);n++)s.Players[0].FateDeck.Add(Add(s,"training-item",0,Zone.FateDeck).Id);
            var source=Add(s,grove?"oracle-l5r-5841":"oracle-l5r-3720",0,grove?Zone.Region:Zone.Hand,grove?0:null);
            var performer=Add(s,"training-crane-samurai",0,Zone.Battlefield,0);Keyword(performer,"Artisan");
            var target=Add(s,"training-lion-samurai",1,Zone.Battlefield,0);
            var e=MatchEngine.FromTestState(s);var cmd=new GameCommand("useAbility",0,e.Version,CardId:source.Id,TargetId:grove?target.Id:null,PerformerId:grove?null:performer.Id,AbilityId:grove?"overgrown-grove-move":"improved-defenses-strength");
            Check(e.QueryActions(0).Any(a=>a.Enabled&&a.Command?.AbilityId==cmd.AbilityId)==enough,"Unaffordable Fate cost offered or legal cost missing");
            if(enough){Accept(e,cmd);Check(e.State.Players[0].FateDeck.Count==0,"Wrong Fate cost paid");}else Reject(e,cmd);
        }
    }
    public static void EquipQuotes()
    {
        var s=State();s.Players[0].Clan="Lion";foreach(var c in s.Cards.Values.Where(c=>c.OwnerId==0))c.Bowed=true;
        var gold=Add(s,"training-marketplace",0,Zone.Home);var host=Add(s,"training-crane-samurai",0,Zone.Home);
        var katagi=Add(s,"oracle-l5r-5112",0,Zone.Home);var item=Add(s,"oracle-l5r-6481",0,Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        var options=e.QueryActions(0).Where(a=>a.Type=="equip"&&a.CardId==item.Id&&a.Enabled).ToArray();
        Check(options.Any(a=>(a.TargetIds??[]).Contains(host.Id)&&a.Label.Contains("2 Gold")),"Lion's affordable 2-Gold equip missing");
        Check(options.Any(a=>(a.TargetIds??[]).Contains(katagi.Id)&&a.Label.Contains("0 Gold")),"Target-specific Katagi discount missing");
        foreach(var a in options)
        {
            var wire=System.Text.Json.JsonSerializer.Deserialize<ActionOption>(System.Text.Json.JsonSerializer.Serialize(a))!;
            Check(wire.GoldCost==a.GoldCost && wire.GoldCost is not null,"Authoritative price lost in transport");
        }
        Check(System.Text.Json.JsonSerializer.Deserialize<ActionOption>("{\"Type\":\"equip\",\"Label\":\"legacy\",\"Enabled\":true}")!.GoldCost is null,"Older action payload no longer loads");
        foreach(var a in options)foreach(var target in a.TargetIds??[])Accept(MatchEngine.FromTestState(s),a.Command! with {TargetId=target});
    }
    public static void EarthFlows()
    {
        foreach(var opposed in new[]{false,true})
        {
            var s=State(true);var attacker=Add(s,"training-lion-samurai",0,Zone.Battlefield,0);Keyword(attacker,"Earth");
            Add(s,"oracle-l5r-8038",0,Zone.Attached,0,attacker.Id);
            if(opposed){var defender=Add(s,"training-crane-samurai",1,Zone.Battlefield,0);defender.Bowed=true;}
            var honor=s.Players[0].Honor;var e=MatchEngine.FromTestState(s);
            Accept(e,new("pass",0,e.Version));Accept(e,new("pass",1,e.Version));
            Check(e.State.Players[0].Honor==honor+(opposed?4:0),"Earth Flows granted Honor without destroying an army, or lost its valid bonus");
        }
    }
}
