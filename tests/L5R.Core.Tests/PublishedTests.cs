using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

internal static class PublishedTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published pool pins card definitions and the ability-free vanilla wave", Catalogue),
            ("CatalogueDecks/live: Lion and Crane retain 40/40 decks", () => CatalogueDecks(PublishedContent.Snapshot)),
            ("CatalogueDecks/frozen: Lion and Crane test fixture retains 40/40 decks", () => CatalogueDecks(PublishedContent.TestSnapshot)),
            ("Published setup pins Lion first turn, Honor, province strengths and Crane Bamboo", Setup),
            ("Published Bamboo stays bowed on Crane first turn and straightens on second", BambooTiming),
            ("Border Keep first-turn and once-per-game refresh are independent", Border),
            ("Border Keep rejects bowed sources, foreign cards, duplicates and later first-turn use", BorderIllegal),
            ("Province refresh handles empty decks and preserves bottom order", BorderEmpty),
            ("All real Holdings and Soken produce their printed Gold", Gold),
            ("Prosperous Village discounts for controlled Personalities in all locations", Prosperous),
            ("Shinden accepts bowed Stronghold and suspends action for opponent choice", Shinden),
            ("Shinden Honor choice, source use limits and payment rollback", ShindenHonor),
            ("Nanae can straighten either player's Air Personality once per turn", Nanae),
            ("Paid Off validates four-Gold payment and both effect choices", PaidOff),
            ("Prepared for Death performs with a bowed Personality and expires", Prepared),
            ("Kouki can act from home through Halls but still requires presence", Kouki),
            ("Battle targeting excludes units at other battlefields", Location),
            ("Costly Feud affects Followers and clamps Force at zero", Costly),
            ("Incapacitated moves defending unit without bowing it", Incapacitated),
            ("Block Supply Lines moves and bows entire attacking unit", Block),
            ("Defensive Stance requires bowed performer and respects negation", Defensive),
            ("Crushing Strength requires opposition and offers two exclusive abilities", Crushing),
            ("Never Afraid may destroy either player's attachment", Never),
            ("Destroying an attachment clears the Attachment-duration keyword it granted", AttachmentGrantedKeywordClears),
            ("Destroying an attachment removes its tokens from the game", AttachmentTokenClears),
            ("Items add Force and Chi even bowed; Armor and Weapon limits enforced", Attachments),
            ("Followers/Veteran Skirmishers: equip for 2 Gold and display 3 Force separately", () => Followers(9147,3,2)),
            ("Followers/Rank & File: equip for 4 Gold and display 4 Force separately", () => Followers(10253,4,4)),
            ("Followers/Native Guide: equip for 6 Gold and display 5 Force separately", () => Followers(9871,5,6)),
            ("Lion attack traits turn on and off as units move", AttackTraits),
            ("Passing Infantry Maneuvers with nothing to assign still reaches Cavalry Maneuvers", CavalryAfterEmptyInfantry),
            ("Halls triggers on Combat destruction, not Resolution casualties", Ancestor),
            ("Lobby and Shunya use Favor with individual once-per-turn limits", Favor),
            ("Favor draw discards from hand and handles empty Fate deck", FavorCycle),
            ("Favor Battle requires presence and moves attacker without bowing", FavorBattle),
            ("Focus has its printed Focus value and no invented action", Focus),
            ("Published views hide enemy hands and face-down provinces", Privacy),
            ("Published nested definition ability arrays cannot mutate later snapshots", DetachedAbilities),
            ("OfferedActions/action/player-0: live-deck commands and alternate targets validate", () => OfferedActions(false,0)),
            ("OfferedActions/action/player-1: live-deck commands and alternate targets validate", () => OfferedActions(false,1)),
            ("OfferedActions/battle/player-0: live-deck commands and alternate targets validate", () => OfferedActions(true,0)),
            ("OfferedActions/battle/player-1: live-deck commands and alternate targets validate", () => OfferedActions(true,1)),
            ("Published full-game transcript replays deterministically", Replay),
        ];
        runner.Run("published",tests.Concat(CatalogueWaves()));
    }
    private static string Id(int id)=>"oracle-l5r-"+id;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Check(bool value,string message="Assertion failed") { if(!value) throw new Exception(message); }
    private static void Eq<T>(T actual,T expected) => Check(EqualityComparer<T>.Default.Equals(actual,expected),$"Expected {expected}; got {actual}");
    private static MatchState State(bool battle=false, int priority=0)
    {
        var s=MatchEngine.CreatePublishedTestMatch(11).State;
        foreach(var c in s.Cards.Values.Where(c=>c.Zone!=Zone.Stronghold).ToArray()) s.Cards.Remove(c.Id);
        foreach(var p in s.Players) { p.Hand.Clear();p.DynastyDeck.Clear();p.FateDeck.Clear();p.TurnsTaken=2;p.Honor=p.Id==0?10:7;foreach(var province in p.Provinces) province.CardId=null; }
        s.Events.Clear();s.ActivePlayerId=0;s.PriorityPlayerId=priority;s.Phase=battle?Phase.Battle:Phase.Action;
        if(battle) {s.CurrentBattleProvince=0;s.UnresolvedBattlefields=[0];Add(s,1645,0,Zone.Battlefield,province:0);Add(s,3636,1,Zone.Battlefield,province:0);}
        return s;
    }
    private static CardInstance Add(MatchState s,int id,int owner=0,Zone zone=Zone.Home,bool bowed=false,int? province=null,string? parent=null)=>Add(s,Id(id),owner,zone,bowed,province,parent);
    private static CardInstance Add(MatchState s,string id,int owner=0,Zone zone=Zone.Home,bool bowed=false,int? province=null,string? parent=null)
    {
        var c=new CardInstance{Id="test-"+s.Cards.Count,DefinitionId=id,OwnerId=owner,Zone=zone,Bowed=bowed,ProvinceIndex=province,AttachedTo=parent,FaceUp=zone!=Zone.Hand};s.Cards.Add(c.Id,c);
        var p=s.Players[owner];
        if(zone==Zone.Hand)p.Hand.Add(c.Id);if(zone==Zone.DynastyDeck)p.DynastyDeck.Add(c.Id);if(zone==Zone.FateDeck)p.FateDeck.Add(c.Id);if(zone==Zone.Province)p.Provinces[province!.Value].CardId=c.Id;
        return c;
    }
    private static CardInstance Unit(MatchState s,int owner)=>s.Cards.Values.First(c=>c.OwnerId==owner&&c.Zone==Zone.Battlefield);
    private static GameCommand Use(MatchEngine e,string? source,string ability,string? target=null,string? performer=null,string[]? cards=null,string[]? sources=null,int? actor=null)=>new("useAbility",actor??e.State.PriorityPlayerId,e.Version,source,sources,CardIds:cards,TargetId:target,AbilityId:ability,PerformerId:performer);
    private static void Pass(MatchEngine e)=>Accept(e,new("pass",e.State.PriorityPlayerId,e.Version));
    private static CardInstance Card(MatchEngine e,string id)=>e.State.Cards[id];
    private static CardView ViewCard(MatchEngine e,string id)=>e.GetView(0).Players.SelectMany(p=>p.Home.Concat(p.Army)).Single(c=>c.Id==id);
    private static void Catalogue()
    {
        var published=CardCatalog.Cards.Values.Where(c=>!c.ScenarioOnly).ToArray();Eq(published.Length,282);
        foreach(var c in published){Check(c.ArtworkKey?.EndsWith(c.Id+".jpg")==true);Check(c.Abilities is not null);Check(c.Keywords is not null);Check(c.Abilities!.All(AbilityCatalog.All.ContainsKey));}
        Eq(CardCatalog.Get(Id(3636)).GoldCost,4);Eq(CardCatalog.Get(Id(521)).GoldCost,4);Eq(CardCatalog.Get(Id(9879)).Chi,1);
        Check(CardCatalog.Get(Id(10368)).Text.Contains("performing bowed"));Check(!CardCatalog.Get(Id(2158)).Text.StartsWith("Favor"));
        var vanilla=PublishedContent.VanillaWave1.Cards;Eq(vanilla.Length,63);
        foreach(var c in vanilla){Eq(c.Effect,"none");Eq(c.Text,"");Eq(c.Abilities!.Length,0);Check(CardCatalog.Get(c.Id).Id==c.Id);}
        Check(!vanilla.Select(c=>c.Id).Intersect(PublishedContent.Snapshot.Cards.Select(c=>c.Id)).Any());
    }
    private static void CatalogueDecks(PublishedSnapshot snapshot)
    {
        Eq(snapshot.Cards.Length,40);
        foreach(var deck in snapshot.Decks)
        {
            Eq(deck.Dynasty.Length,40);Eq(deck.Fate.Length,40);
            Check(deck.Dynasty.All(id=>CardCatalog.Get(id).Type is CardType.Holding or CardType.Personality));
        }
    }
    private static IEnumerable<(string Name,Action Test)> CatalogueWaves()
    {
        (string Name,Func<PublishedSnapshot> Load,int Count,CardType Type,Func<PublishedSnapshot>? Previous)[] waves = [
            ("StrongholdWave1",()=>PublishedContent.StrongholdWave1,4,CardType.Stronghold,null),
            ("StrongholdWave2",()=>PublishedContent.StrongholdWave2,2,CardType.Stronghold,()=>PublishedContent.StrongholdWave1),
            ("StrongholdWave3",()=>PublishedContent.StrongholdWave3,1,CardType.Stronghold,null),
            ("PersonalityWave1",()=>PublishedContent.PersonalityWave1,1,CardType.Personality,null),
            ("PersonalityWave2",()=>PublishedContent.PersonalityWave2,1,CardType.Personality,()=>PublishedContent.PersonalityWave1),
            ("PersonalityWave3",()=>PublishedContent.PersonalityWave3,4,CardType.Personality,()=>PublishedContent.PersonalityWave2),
            ("FollowerWave1",()=>PublishedContent.FollowerWave1,1,CardType.Follower,null),
            ("PersonalityWave4",()=>PublishedContent.PersonalityWave4,2,CardType.Personality,()=>PublishedContent.PersonalityWave3),
            ("PersonalityWave5",()=>PublishedContent.PersonalityWave5,5,CardType.Personality,()=>PublishedContent.PersonalityWave4),
        ];
        foreach(var wave in waves)
            yield return ($"CatalogueWaves/{wave.Name}: count, type, text and catalogue wiring",()=>
            {
                var cards=wave.Load().Cards;Eq(cards.Length,wave.Count);
                foreach(var card in cards)
                {
                    Eq(card.Type,wave.Type);Check(card.Text.Length>0,$"{card.Id}: missing text");
                    Eq(CardCatalog.Get(card.Id).Id,card.Id);
                }
                if(wave.Previous is not null)
                    Check(!cards.Select(c=>c.Id).Intersect(wave.Previous().Cards.Select(c=>c.Id)).Any(),"Wave overlaps its predecessor");
            });
    }
    private static void Setup()
    {
        var e=MatchEngine.CreatePublishedTestMatch();var s=e.State;Eq(s.ActivePlayerId,1);Eq(s.PriorityPlayerId,1);Eq(s.Players[0].Honor,6);Eq(s.Players[1].Honor,7);
        Check(s.Players[0].Provinces.All(p=>p.Strength==6));Check(s.Players[1].Provinces.All(p=>p.Strength==7));
        var bamboo=s.Cards.Values.Single(c=>c.DefinitionId==Id(679));Eq(bamboo.OwnerId,0);Check(bamboo.Bowed);
        OpeningCounts(s);
        foreach(var c in s.Cards.Values)Check(!CardCatalog.Get(c.DefinitionId).ScenarioOnly);
    }
    private static void BambooTiming()
    {
        var e=MatchEngine.CreatePublishedTestMatch();
        var bamboo=e.State.Cards.Values.Single(c=>c.DefinitionId==Id(679));Check(bamboo.Bowed);
        while(e.State.ActivePlayerId==1)Pass(e);Check(Card(e,bamboo.Id).Bowed);
        while(e.State.ActivePlayerId==0)Pass(e);while(e.State.ActivePlayerId==1)Pass(e);Check(!Card(e,bamboo.Id).Bowed);
    }
    private static void Border()
    {
        var s=State();s.Players[0].TurnsTaken=1;var keep=Add(s,1106);var a=Add(s,7303,zone:Zone.Province,province:0);var b=Add(s,4664,zone:Zone.Province,province:1);
        var next=Add(s,1645,zone:Zone.DynastyDeck);var next2=Add(s,521,zone:Zone.DynastyDeck);var e=MatchEngine.FromTestState(s);
        Accept(e,Use(e,keep.Id,"border-first",cards:[b.Id,a.Id]));Eq(e.State.Players[0].Provinces[0].CardId,next.Id);Eq(e.State.Players[0].Provinces[1].CardId,next2.Id);Eq(string.Join(",",e.State.Players[0].DynastyDeck),b.Id+","+a.Id);Check(Card(e,next.Id).FaceUp);Check(!Card(e,keep.Id).Bowed);
        Pass(e);Reject(e,Use(e,keep.Id,"border-first",cards:[next.Id]));Accept(e,Use(e,keep.Id,"border-game",cards:[next.Id]));Pass(e);Reject(e,Use(e,keep.Id,"border-game",cards:[e.State.Players[0].Provinces[0].CardId!]));
        var later=e.State;later.TurnNumber+=2;later.PriorityPlayerId=0;e=MatchEngine.FromTestState(later);Reject(e,Use(e,keep.Id,"border-game",cards:[later.Players[0].Provinces[0].CardId!]));
    }
    private static void BorderIllegal()
    {
        var s=State();var k=Add(s,1106);var p=Add(s,7303,zone:Zone.Province,province:0);var foreign=Add(s,7303,1,Zone.Province,province:0);var e=MatchEngine.FromTestState(s);
        Reject(e,Use(e,k.Id,"border-first",cards:[p.Id]));Reject(e,Use(e,k.Id,"border-game",cards:[p.Id,p.Id]));Reject(e,Use(e,k.Id,"border-game",cards:[foreign.Id]));Reject(e,Use(e,k.Id,"border-game",cards:[]));
        k.Bowed=true;e=MatchEngine.FromTestState(s);Reject(e,Use(e,k.Id,"border-game",cards:[p.Id]));
    }
    private static void BorderEmpty()
    {
        var s=State();var k=Add(s,1106);var p=Add(s,7303,zone:Zone.Province,province:0);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,k.Id,"border-game",cards:[p.Id]));Eq(e.State.Players[0].Provinces[0].CardId,p.Id);Eq(e.State.Players[0].DynastyDeck.Count,0);Check(Card(e,p.Id).FaceUp);
    }
    private static void Gold()
    {
        var s=State();foreach(var (id,gold) in new[]{(7303,1),(4664,2),(6258,3),(4840,3),(2867,2),(6086,5),(1106,2),(679,2),(10625,3)}) {var c=Add(s,id);var e=MatchEngine.FromTestState(s);Eq(e.GoldProduced(c.Id,0),gold);}
        var mine=s.Cards.Values.Single(c=>c.DefinitionId==Id(2867));s.Players[0].Clan="Dragon";Eq(MatchEngine.FromTestState(s).GoldProduced(mine.Id,0),3);
        s=State();var soken=Add(s,10625);var holding=Add(s,6258,zone:Zone.Province,province:0);s.Phase=Phase.Dynasty;var game=MatchEngine.FromTestState(s);Accept(game,new("purchase",0,game.Version,holding.Id,[soken.Id]));Check(Card(game,soken.Id).Bowed);Check(Card(game,holding.Id).Bowed);
    }
    private static void Prosperous()
    {
        var s=State();var village=Add(s,6086,zone:Zone.Province,province:0);Add(s,521);Add(s,507,zone:Zone.Battlefield,province:0);Add(s,3636,1);Add(s,521,zone:Zone.Dead);var e=MatchEngine.FromTestState(s);Eq(e.QuotePurchase(0,village.Id).Cost,4);
        for(var i=0;i<8;i++)Add(s,521);Eq(MatchEngine.FromTestState(s).QuotePurchase(0,village.Id).Cost,0);
    }
    private static void Shinden()
    {
        var s=State();var strong=s.Cards[s.Players[0].StrongholdId];strong.Bowed=true;var shu=Add(s,521);var enemy=Add(s,3636,1);var e=MatchEngine.FromTestState(s);
        Accept(e,Use(e,strong.Id,"shinden-choice",enemy.Id,shu.Id));Check(Card(e,shu.Id).Bowed);Eq(e.State.PendingChoice?.Type,"shinden");Eq(e.State.PriorityPlayerId,1);
        Reject(e,new("pass",1,e.Version));Reject(e,new("chooseShinden",0,e.Version,Choice:"bow"));Accept(e,new("chooseShinden",1,e.Version,Choice:"bow"));Check(Card(e,enemy.Id).Bowed);Check(ViewCard(e,enemy.Id).StraightenBlocked);Eq(e.State.PriorityPlayerId,1);Eq(e.State.Players[0].Honor,10);
        // Straightening negation lasts only for the current turn; next owner's Straighten Phase works.
        while(e.State.ActivePlayerId==0)Pass(e);Check(!Card(e,enemy.Id).Bowed);
    }
    private static void ShindenHonor()
    {
        var s=State();var shu=Add(s,521);var enemy=Add(s,3636,1);var e=MatchEngine.FromTestState(s);var strong=s.Players[0].StrongholdId;
        Reject(e,Use(e,strong,"shinden-choice",enemy.Id,enemy.Id));Reject(e,Use(e,strong,"shinden-choice",enemy.Id,shu.Id,sources:[strong]));
        Accept(e,Use(e,strong,"shinden-choice",enemy.Id,shu.Id));Accept(e,new("chooseShinden",1,e.Version,Choice:"honor"));Eq(e.State.Players[0].Honor,11);Check(!Card(e,enemy.Id).Bowed);Pass(e);Reject(e,Use(e,strong,"shinden-choice",enemy.Id,shu.Id));
    }
    private static void Nanae()
    {
        var s=State();var n=Add(s,507);var enemy=Add(s,521,1,bowed:true);var notAir=Add(s,3636,1,bowed:true);var e=MatchEngine.FromTestState(s);
        Reject(e,Use(e,n.Id,"air-straighten",notAir.Id));Accept(e,Use(e,n.Id,"air-straighten",enemy.Id));Check(!Card(e,enemy.Id).Bowed);Pass(e);Reject(e,Use(e,n.Id,"air-straighten",enemy.Id));
        var later=e.State;later.TurnNumber++;later.PriorityPlayerId=0;later.ActivePlayerId=1;e=MatchEngine.FromTestState(later);Accept(e,Use(e,n.Id,"air-straighten",n.Id));
        later=e.State;later.TurnNumber++;later.PriorityPlayerId=0;later.Cards[n.Id].Bowed=true;e=MatchEngine.FromTestState(later);Reject(e,Use(e,n.Id,"air-straighten",enemy.Id));
    }
    private static void PaidOff()
    {
        foreach(var mode in new[]{"paid-bow","paid-straighten"})
        {
            var s=State();var card=Add(s,5861,zone:Zone.Hand);var enemy=Add(s,3636,1,bowed:mode=="paid-straighten");var source=s.Players[0].StrongholdId;var e=MatchEngine.FromTestState(s);
            Reject(e,Use(e,card.Id,mode,enemy.Id));Reject(e,Use(e,card.Id,mode,enemy.Id,sources:[source,source]));
            Accept(e,Use(e,card.Id,mode,enemy.Id,sources:[source]));Eq(Card(e,enemy.Id).Bowed,mode=="paid-bow");Check(Card(e,source).Bowed);Eq(Card(e,card.Id).Zone,Zone.FateDiscard);
        }
        var b=State();var paid=Add(b,5861,zone:Zone.Hand);var target=Add(b,521,bowed:true);target.StraightenBlockedTurn=b.TurnNumber;var g=MatchEngine.FromTestState(b);Accept(g,Use(g,paid.Id,"paid-straighten",target.Id,sources:[b.Players[0].StrongholdId]));Check(Card(g,target.Id).Bowed);Eq(Card(g,paid.Id).Zone,Zone.FateDiscard);
    }
    private static void Prepared()
    {
        var s=State();var c=Add(s,6051,zone:Zone.Hand);var p=Add(s,521,bowed:true);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"prepared-death",p.Id));Eq(ViewCard(e,p.Id).Force,2);Check(Card(e,p.Id).Bowed);while(e.State.ActivePlayerId==0)Pass(e);Eq(ViewCard(e,p.Id).Force,0);
    }
    private static void Kouki()
    {
        var s=State(true,1);var kouki=Add(s,10063,1);var enemy=Unit(s,0);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,kouki.Id,"kouki-force",enemy.Id));Eq(ViewCard(e,enemy.Id).Force,0);Check(Card(e,kouki.Id).Bowed);Check(!Card(e,enemy.Id).Bowed);
        var empty=State(true,1);var k=Add(empty,10063,1);Unit(empty,1).Zone=Zone.Home;var g=MatchEngine.FromTestState(empty);Reject(g,Use(g,k.Id,"kouki-force",Unit(empty,0).Id));
        var crane=State(true);var kc=Add(crane,10063);g=MatchEngine.FromTestState(crane);Reject(g,Use(g,kc.Id,"kouki-force",Unit(crane,1).Id));
    }
    private static void Location()
    {
        var s=State(true);var card=Add(s,1521,zone:Zone.Hand);var remote=Add(s,3636,1,Zone.Battlefield,province:1);var e=MatchEngine.FromTestState(s);Reject(e,Use(e,card.Id,"costly-feud",remote.Id));
        s.Phase=Phase.Action;e=MatchEngine.FromTestState(s);Reject(e,Use(e,card.Id,"costly-feud",Unit(s,1).Id));
    }
    private static void Costly()
    {
        foreach(var follower in new[]{false,true})
        {
            var s=State(true);var c=Add(s,1521,zone:Zone.Hand);var target=follower?Add(s,9147,1,Zone.Attached,province:0,parent:Unit(s,1).Id):Unit(s,1);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"costly-feud",target.Id));Eq(ViewCard(e,target.Id).Force,0);Check(Card(e,target.Id).Bowed);
        }
    }
    private static void Incapacitated()
    {
        var s=State(true);var c=Add(s,3730,zone:Zone.Hand);var target=Unit(s,1);var a=Add(s,9147,1,Zone.Attached,province:0,parent:target.Id);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"incapacitated",target.Id));Eq(Card(e,target.Id).Zone,Zone.Home);Check(!Card(e,target.Id).Bowed);Check(!Card(e,a.Id).Bowed);Eq(Card(e,a.Id).ProvinceIndex,null);Eq(Card(e,a.Id).AttachedTo,target.Id);
    }
    private static void Block()
    {
        var s=State(true,1);var c=Add(s,1045,1,Zone.Hand);var target=Unit(s,0);var a=Add(s,9147,0,Zone.Attached,province:0,parent:target.Id);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"block-supply",target.Id));Eq(Card(e,target.Id).Zone,Zone.Home);Check(Card(e,target.Id).Bowed&&Card(e,a.Id).Bowed);
        // Printed target says attacking, so the attacker can also move their own unit.
        s=State(true);c=Add(s,1045,zone:Zone.Hand);e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"block-supply",Unit(s,0).Id));
    }
    private static void Defensive()
    {
        var s=State(true);var c=Add(s,10368,zone:Zone.Hand);var p=Unit(s,0);var e=MatchEngine.FromTestState(s);Reject(e,Use(e,c.Id,"defensive-stance",p.Id));
        p.Bowed=true;p.StraightenBlockedTurn=s.TurnNumber;e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"defensive-stance",p.Id));Eq(ViewCard(e,p.Id).Force,6);Check(Card(e,p.Id).Bowed);
        p.StraightenBlockedTurn=-1;e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"defensive-stance",p.Id));Check(!Card(e,p.Id).Bowed);
    }
    private static void Crushing()
    {
        var s=State(true);var c=Add(s,1589,zone:Zone.Hand);var p=Unit(s,0);var attachment=Add(s,9879,1,Zone.Attached,province:0,parent:Unit(s,1).Id);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"crushing-force",p.Id));Eq(ViewCard(e,p.Id).Force,6);Pass(e);Reject(e,Use(e,c.Id,"crushing-destroy",attachment.Id));
        e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"crushing-destroy",attachment.Id));Eq(Card(e,attachment.Id).Zone,Zone.FateDiscard);Eq(Card(e,attachment.Id).AttachedTo,null);
        Unit(s,1).Zone=Zone.Home;e=MatchEngine.FromTestState(s);Reject(e,Use(e,c.Id,"crushing-force",p.Id));
    }
    private static void Never()
    {
        foreach(var owner in new[]{0,1}){var s=State(true);var c=Add(s,9916,zone:Zone.Hand);var target=Add(s,10118,owner,Zone.Attached,province:0,parent:Unit(s,owner).Id);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,c.Id,"never-afraid",target.Id));Eq(Card(e,target.Id).Zone,Zone.FateDiscard);}
    }
    private static void AttachmentGrantedKeywordClears()
    {
        // An Attachment-duration grant is removed once its granting attachment is destroyed.
        var s=State(true);var never=Add(s,9916,zone:Zone.Hand);var parent=Unit(s,0);var item=Add(s,10118,0,Zone.Attached,province:0,parent:parent.Id);
        parent.GrantedKeywords=[new("Elite",item.Id,"Attachment")];
        var e=MatchEngine.FromTestState(s);Accept(e,Use(e,never.Id,"never-afraid",item.Id));
        Eq(Card(e,parent.Id).GrantedKeywords.Count,0);
    }
    private static void AttachmentTokenClears()
    {
        // Core rules Glossary — Token: "removed from the game if their card leaves play."
        var s=State(true);var never=Add(s,9916,zone:Zone.Hand);var parent=Unit(s,0);var item=Add(s,10118,0,Zone.Attached,province:0,parent:parent.Id);
        item.Tokens=[new("test-token","Training","test-fixture","Force",1)];
        var e=MatchEngine.FromTestState(s);Accept(e,Use(e,never.Id,"never-afraid",item.Id));
        Eq(Card(e,item.Id).Zone,Zone.FateDiscard);Eq(Card(e,item.Id).Tokens.Count,0);
    }
    private static void Attachments()
    {
        var s=State();var p=Add(s,1645,bowed:true);var armor=Add(s,10118,zone:Zone.Hand);var sword=Add(s,9879,zone:Zone.Attached,bowed:true,parent:p.Id);var e=MatchEngine.FromTestState(s);
        Accept(e,new("equip",0,e.Version,armor.Id,[s.Players[0].StrongholdId],TargetId:p.Id));Eq(ViewCard(e,p.Id).Force,11);Eq(ViewCard(e,p.Id).Chi,4);Check(Card(e,p.Id).Bowed);Pass(e);
        s=e.State;var duplicate=Add(s,10118,zone:Zone.Hand);var weapon=Add(s,9879,zone:Zone.Hand);Add(s,6086);Add(s,6086);e=MatchEngine.FromTestState(s);
        Reject(e,new("equip",0,e.Version,duplicate.Id,TargetId:p.Id));Reject(e,new("equip",0,e.Version,weapon.Id,e.PaymentOptions(0,6)[0].SourceIds,TargetId:p.Id));Check(e.QueryActions(0).Where(a=>a.Type=="equip").All(a=>!a.Enabled));
    }
    private static void Followers(int id,int force,int cost)
    {
        var s=State();var p=Add(s,521);Add(s,6086);var c=Add(s,id,zone:Zone.Hand);
        var e=MatchEngine.FromTestState(s);
        Accept(e,new("equip",0,e.Version,c.Id,e.PaymentOptions(0,cost)[0].SourceIds,TargetId:p.Id));
        Eq(ViewCard(e,p.Id).Force,0);Eq(ViewCard(e,c.Id).Force,force);
    }
    private static void AttackTraits()
    {
        foreach(var (id,home,attacking) in new[]{(4915,1,3),(3641,4,8)})
        {
            var s=State(true);s.ActivePlayerId=1;var p=Add(s,id,1,Zone.Battlefield,province:0);var e=MatchEngine.FromTestState(s);Eq(ViewCard(e,p.Id).Force,attacking);p.Zone=Zone.Home;e=MatchEngine.FromTestState(s);Eq(ViewCard(e,p.Id).Force,home);p.Zone=Zone.Battlefield;s.ActivePlayerId=0;e=MatchEngine.FromTestState(s);Eq(ViewCard(e,p.Id).Force,home);
        }
    }
    private static void CavalryAfterEmptyInfantry()
    {
        // A pass with no Infantry to assign only declines Infantry Maneuvers -- it must not skip
        // the whole Attack Phase, or a deck with only Cavalry-keyword units could never attack.
        var s=State();s.Phase=Phase.AttackDeclaration;s.PriorityPlayerId=0;
        var cavalry=Add(s,3623,0,Zone.Home); // Ikoma Natsu: printed Cavalry keyword.
        var e=MatchEngine.FromTestState(s);
        Pass(e);Eq(e.State.Phase,Phase.DefenseAssignment);
        Pass(e);Eq(e.State.Phase,Phase.CavalryDeclaration);Eq(e.State.PriorityPlayerId,0);
        Accept(e,new("attack",0,e.Version,Assignments:new(){[cavalry.Id]=0}));
        Eq(Card(e,cavalry.Id).Zone,Zone.Battlefield);Eq(Card(e,cavalry.Id).ProvinceIndex,0);
        Check(e.State.UnresolvedBattlefields.Contains(0));
        Eq(e.State.Phase,Phase.CavalryDefense);
    }
    private static void Ancestor()
    {
        var s=State(true);var ancestor=Add(s,"training-fragile-ancestor",1,Zone.Battlefield,province:0);var sword=Add(s,9879,1,Zone.Attached,province:0,parent:ancestor.Id);var never=Add(s,9916,zone:Zone.Hand);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,never.Id,"never-afraid",sword.Id));Eq(Card(e,ancestor.Id).Zone,Zone.Dead);Eq(e.State.Players[1].Honor,8);
        s=State(true);var lion=Unit(s,1);lion.GrantedKeywords=[new("Ancestor","test-fixture","Permanent")];lion.Modifiers=[new("test-fixture","test-fixture","Force",-2)];var a=Unit(s,0);a.Modifiers=[new("test-fixture","test-fixture","Force",30)];e=MatchEngine.FromTestState(s);Pass(e);Pass(e);Eq(e.State.Players[1].Honor,7);
        s=State(true);s.ActivePlayerId=1;lion=Unit(s,1);lion.GrantedKeywords=[new("Ancestor","test-fixture","Permanent")];e=MatchEngine.FromTestState(s);Eq(ViewCard(e,lion.Id).Force,1);
    }
    private static void Favor()
    {
        var s=State();var lobby=Add(s,521);var shunya=Add(s,2158);var e=MatchEngine.FromTestState(s);Reject(e,Use(e,shunya.Id,"shunya-honor"));Accept(e,Use(e,null,"lobby",lobby.Id));Eq(e.State.FavorPlayerId,0);Check(Card(e,lobby.Id).Bowed);Pass(e);Accept(e,Use(e,shunya.Id,"shunya-honor"));Eq(e.State.Players[0].Honor,12);Eq(e.State.FavorPlayerId,null);Check(!Card(e,shunya.Id).Bowed);Pass(e);Reject(e,Use(e,null,"lobby",shunya.Id));
        s=State();s.Players[0].Honor=7;lobby=Add(s,521);e=MatchEngine.FromTestState(s);Reject(e,Use(e,null,"lobby",lobby.Id));
        // Spending Favor (a Limited ability) does not consume the separate once-per-turn Lobby
        // limit -- they are tracked by distinct AbilityUses keys. Once the opponent's action
        // opportunity passes back, the same active player may Lobby again the same turn.
        s=State();s.FavorPlayerId=0;var discard=Add(s,2619,zone:Zone.Hand);var relobby=Add(s,521);e=MatchEngine.FromTestState(s);
        Accept(e,Use(e,null,"favor-cycle",discard.Id));Eq(e.State.FavorPlayerId,null);Pass(e);
        Accept(e,Use(e,null,"lobby",relobby.Id,actor:0));Eq(e.State.FavorPlayerId,0);
    }
    private static void FavorCycle()
    {
        foreach(var empty in new[]{false,true}) {var s=State();s.FavorPlayerId=0;var discard=Add(s,2619,zone:Zone.Hand);var next=empty?null:Add(s,6051,zone:Zone.FateDeck);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,null,"favor-cycle",discard.Id));Eq(e.State.FavorPlayerId,null);Eq(Card(e,discard.Id).Zone,Zone.FateDiscard);Eq(e.State.Players[0].Hand.Count,empty?0:1);if(next is not null)Eq(e.State.Players[0].Hand[0],next.Id);}
    }
    private static void FavorBattle()
    {
        var s=State(true,1);s.FavorPlayerId=1;var p=Unit(s,0);var e=MatchEngine.FromTestState(s);Accept(e,Use(e,null,"favor-battle",p.Id));Eq(Card(e,p.Id).Zone,Zone.Home);Check(!Card(e,p.Id).Bowed);Eq(e.State.FavorPlayerId,null);
        Unit(s,1).Zone=Zone.Home;e=MatchEngine.FromTestState(s);Reject(e,Use(e,null,"favor-battle",p.Id));
    }
    private static void Focus()
    {
        var s=State();var focus=Add(s,2619,zone:Zone.Hand);var e=MatchEngine.FromTestState(s);Eq(CardCatalog.Get(Id(2619)).Focus,5);Check(!e.QueryActions(0).Any(a=>a.CardId==focus.Id));Reject(e,Use(e,focus.Id,"paid-bow"));
    }
    private static void Privacy()
    {
        var e=MatchEngine.CreatePublishedTestMatch();var s=e.State;var view=e.GetView(1);
        Eq(view.Players[0].Hand.Length,0);Check(view.Players[0].Provinces.All(p=>p.Card!.Definition is null));
        HiddenIdsAbsent(view,s.Players[0].Hand);
    }
    private static void DetachedAbilities()
    {
        var e=MatchEngine.CreatePublishedTestMatch();var view=e.GetView(1);
        view.Players[1].Home.Single().Definition!.Abilities![0]="bad";
        Eq(e.GetView(1).Players[1].Home.Single().Definition!.Abilities![0],"border-first");
    }
    // Deliberately exercises the live app deck (Snapshot), not the frozen test scenario: this is a
    // smoke test of whatever is actually deployed, so a card newly swapped into the live deck (e.g. for
    // playtesting) gets its abilities checked here too, not just at content-load time.
    private static void OfferedActions(bool battle,int player)
    {
        var s=State(battle,player);s.FavorPlayerId=player;s.Players[player].TurnsTaken=1;s.ActivePlayerId=player;
        foreach(var c in PublishedContent.Snapshot.Cards.Where(c=>c.Type!=CardType.Stronghold))Add(s,c.Id,player,c.Type is CardType.Strategy or CardType.Item or CardType.Follower?Zone.Hand:Zone.Home);
        Add(s,10118,1-player,Zone.Attached,province:0,parent:battle?Unit(s,1-player).Id:Add(s,521,1-player).Id);
        if(battle)Unit(s,player).Bowed=true;
        Add(s,7303,player,Zone.Province,province:0);var engine=MatchEngine.FromTestState(s);
        OfferedCommandsValidate(engine,()=>MatchEngine.FromTestState(s),alternateTargets:true);
    }
    // Deliberately exercises the live app deck too: a full scripted game is exactly the kind of check
    // that should catch an unplayable state a newly swapped-in live-deck card might introduce.
    private static void Replay()
    {
        var e=MatchEngine.CreatePublishedMatch(801);var count=0;
        while(e.State.WinnerId is null&&count++<1500)
        {
            var s=e.State;var options=e.QueryActions(s.PriorityPlayerId).Where(a=>a.Enabled&&a.Command is not null&&a.Type!="concede").ToArray();
            var chosen=options.FirstOrDefault(a=>a.Type=="proclaim")??options.FirstOrDefault(a=>a.Type=="declineReaction")??options.FirstOrDefault(a=>a.Type=="chooseShinden")??options.FirstOrDefault(a=>a.Type=="discardHand")
                ??options.FirstOrDefault(a=>a.Type=="purchase"&&!a.Command!.ClanDiscount)??options.FirstOrDefault(a=>a.Type=="purchase")??options.FirstOrDefault(a=>a.Type=="discardProvince")
                ??options.FirstOrDefault(a=>a.Type=="useAbility"&&a.Command!.AbilityId=="border-first")
                ??options.FirstOrDefault(a=>a.Type=="selectBattle")??options.FirstOrDefault(a=>a.Type=="endDynasty")??options.FirstOrDefault(a=>a.Type=="attack")??options.First(a=>a.Type=="pass");
            Accept(e,chosen.Command!);
        }
        Check(e.State.WinnerId is not null,"Published scripted match did not finish.");Eq(Json(MatchEngine.Replay(e.ExportReplay()).State),Json(e.State));
    }
}
