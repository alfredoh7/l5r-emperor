using System.Text.Json;
using L5R.AI;
using L5R.Core;

var runner=new TestRunner(args);
if(!runner.Valid) return 2;
var tests=new List<(string Name,Action Test)>();
Run("waits without priority and never concedes automatically",()=>
{
    var game=MatchEngine.CreateStarterMatch();
    Check(TrainingOpponent.Choose(game.GetView(1),game.QueryActions(1)).Command is null,"AI acted out of turn");
    Check(TrainingOpponent.Choose(game.GetView(0),[new("concede","Concede",true,null,new("concede",0,0))]).Command is null,"AI conceded");
});
Run("consumes only supplied detached player views",()=>
{
    var game=MatchEngine.CreateEconomyScenario();
    var view=JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(game.GetView(0)))!;
    var actions=JsonSerializer.Deserialize<ActionOption[]>(JsonSerializer.Serialize(game.QueryActions(0)))!;
    var decision=TrainingOpponent.Choose(view,actions);
    Check(decision.Command is not null,"No decision");
    Check(game.Execute(decision.Command!).Accepted,"Detached inputs produced illegal command");
});
Run("resolves purchase and Proclaim with alternating reaction priority",()=>
{
    var game=MatchEngine.CreateEconomyScenario(); var id=game.GetView(0).Players[0].Provinces[0].Card!.Id;
    var quote=game.QuotePurchase(0,id);
    Check(game.Execute(new("purchase",0,0,id,quote.Payments[0].SourceIds)).Accepted,"Purchase failed");
    var move=TrainingOpponent.Choose(game.GetView(0),game.QueryActions(0)); Check(move.Command?.Type=="proclaim","Proclaim not chosen");
    Check(game.Execute(move.Command!).Accepted,"Proclaim failed");
    for(var i=0;i<2;i++)
    {
        var player=game.GetView(0).PriorityPlayerId;
        move=TrainingOpponent.Choose(game.GetView(player),game.QueryActions(player));
        Check(move.Command?.Type=="declineReaction","Reaction not passed"); Check(game.Execute(move.Command!).Accepted,"Reaction pass failed");
    }
    Check(game.GetView(0).PendingChoice is null,"Reaction window stuck");
});
Run("declines to attack at the Declaration Segment when it has nothing to attack with, and never stalls there (issue #354)",()=>
{
    var game=MatchEngine.CreateStarterMatch();
    for(var i=0;i<12 && game.GetView(0).Phase!=Phase.AttackDeclaration;i++)
    {
        var mover=game.GetView(0).PriorityPlayerId;
        var step=TrainingOpponent.Choose(game.GetView(mover),game.QueryActions(mover));
        Check(step.Command is not null && game.Execute(step.Command).Accepted,"the AI could not reach the Declaration Segment");
    }
    var view=game.GetView(0); var actions=game.QueryActions(view.PriorityPlayerId);
    if(view.Phase==Phase.AttackDeclaration)
    {
        Check(actions.Any(a=>a.Type=="declareAttack") && actions.Any(a=>a.Type=="pass"),"the decision is one explicit choice");
        var decision=TrainingOpponent.Choose(game.GetView(view.PriorityPlayerId),actions);
        Check(decision.Command?.Type=="pass","With no army the AI declines to attack");
        Check(game.Execute(decision.Command!).Accepted && game.GetView(0).Phase==Phase.Dynasty,"and declining ends the Attack Phase");
    }
});
Run("passes, never deadlocks, in a multi-reaction before-action window (issue #318)",()=>
{
    // The legal-action listing now offers the player holding priority every reaction of their own that applies to the
    // deferred action (here Kakihara's and a Champion of Thunder's, the latter with a chosen target) alongside a pass.
    // The practice opponent scores a targetless or unknown reaction below passing, so it always passes -- and since a
    // pass is always listed for whoever holds priority, the window closes on two consecutive passes.
    var game=MatchEngine.CreateStarterMatch();
    var view=game.GetView(0) with { PendingChoice=new("beforeActionReaction",1,null,"Before this action resolves: either player may use a reaction, or pass.") };
    var actions=new ActionOption[]
    {
        new("useAbility","Kakihara",true,null,new("useAbility",0,view.Version,CardId:"kakihara",AbilityId:"kakihara-force-bonus"),"kakihara"),
        new("useAbility","Champion of Thunder",true,null,new("useAbility",0,view.Version,CardId:"champion",TargetId:"host",AbilityId:"champion-of-thunder-negate"),"champion"),
        new("declineReaction","Pass reaction",true,null,new("declineReaction",0,view.Version))
    };
    var decision=TrainingOpponent.Choose(view,actions);
    Check(decision.Command?.Type=="declineReaction","The practice opponent took a reaction over passing");
});
Run("passes, never stalls or spends Gold, in a window holding a redirect or a negation (Ezuno, Blade of Champions, Yurishi, Jinn-Kuen, Mochitoko; issue #335)",()=>
{
    // The targeted-by-action reactions are offered as targetless useAbility options (Ezuno, Yurishi, Jinn-Kuen with its Gold quote, Mochitoko) or one
    // option per candidate card (Blade of Champions). The practice opponent scores an ability it has no rule for below passing, so it always passes
    // -- and never pays Jinn-Kuen's 2 Gold or redirects an action on its own -- in both windows an action can be held in.
    var game=MatchEngine.CreateStarterMatch();
    foreach(var window in new[]{"beforeActionReaction","attackReaction"})
    {
        var view=game.GetView(0) with { PendingChoice=new(window,1,null,"An action is pending: react or pass.") };
        var own=view.Players[0].Hand.First().Id;
        var actions=new ActionOption[]
        {
            new("useAbility","Ezuno",true,null,new("useAbility",0,view.Version,CardId:"ezuno",AbilityId:"ezuno-redirect"),"ezuno"),
            new("useAbility","Blade of Champions",true,null,new("useAbility",0,view.Version,CardId:"blade",TargetId:own,AbilityId:"blade-of-champions-redirect"),"blade"),
            new("useAbility","Yurishi",true,null,new("useAbility",0,view.Version,CardId:"yurishi",AbilityId:"yurishi-disappear"),"yurishi"),
            new("useAbility","Jinn-Kuen",true,null,new("useAbility",0,view.Version,CardId:"jinn",AbilityId:"jinn-kuen-negate",SourceIds:["stronghold"]),"jinn"),
            new("useAbility","Mochitoko",true,null,new("useAbility",0,view.Version,CardId:"moch",AbilityId:"mochitoko-negate"),"moch"),
            new("declineReaction","Pass reaction",true,null,new("declineReaction",0,view.Version))
        };
        var decision=TrainingOpponent.Choose(view,actions);
        Check(decision.Command?.Type=="declineReaction",$"The practice opponent took a reaction over passing in the {window} window");
    }
});
Run("answers a forced discard (Times of Strife, Daigotsu Bofana) with the least useful card in its hand instead of stalling (issue #333)",()=>
{
    var game=MatchEngine.CreateStarterMatch();
    var view=game.GetView(0) with { PendingChoice=new("forcedDiscard",0,null,"Daigotsu Bofana: discard a card.") };
    var hand=view.Players[0].Hand; Check(hand.Length>1,"The starter hand has several cards");
    var actions=hand.Select(c=>new ActionOption("forcedDiscard",$"Discard {c.Definition!.Name}",true,null,new("forcedDiscard",0,view.Version,TargetId:c.Id),c.Id)).ToArray();
    var decision=TrainingOpponent.Choose(view,actions);
    Check(decision.Command is {Type:"forcedDiscard"},"The practice opponent stalled instead of discarding");
    int Value(CardView c)=>c.Definition?.Effect switch {"force-three"=>8,"bow-small"=>6,_=>c.Definition?.Type==CardType.Follower?5:4};
    Check(Value(hand.Single(c=>c.Id==decision.Command!.TargetId))==hand.Min(Value),"It gave up a card that was not its least useful");
});
Run("answers the opposing player's Either/Or choice (Yoritomo Sakuma, Unfortunate Hesitation) and a copy decision (Fukuzo) instead of stalling (issue #338)",()=>
{
    var game=MatchEngine.CreateStarterMatch();
    var view=game.GetView(0) with { PendingChoice=new("eitherOr",0,null,"Yoritomo Sakuma: pay 2 Gold, or your action's remaining effects are negated.") };
    var pay=new ActionOption("chooseEitherOr","Pay 2 Gold",true,null,new("chooseEitherOr",0,view.Version,Choice:"pay",SourceIds:["a-source"]));
    var negate=new ActionOption("chooseEitherOr","Do not pay",true,null,new("chooseEitherOr",0,view.Version,Choice:"negate"));
    Check(TrainingOpponent.Choose(view,[negate,pay]).Command?.Choice=="pay","The practice opponent let its action be negated instead of paying 2 Gold");
    var bow=new ActionOption("chooseEitherOr","Bow him",true,null,new("chooseEitherOr",0,view.Version,Choice:"bow"));
    var owe=new ActionOption("chooseEitherOr","Do not bow him",true,null,new("chooseEitherOr",0,view.Version,Choice:"pass"));
    Check(TrainingOpponent.Choose(view,[owe,bow]).Command?.Choice=="bow","The practice opponent took a forced pass instead of bowing the Personality");
    var unpayable=new ActionOption("chooseEitherOr","Pay 2 Gold",false,"You cannot produce 2 Gold.",null);
    Check(TrainingOpponent.Choose(view,[unpayable,negate]).Command?.Choice=="negate","The practice opponent stalled when the only enabled option was the consequence");
    var copy=new ActionOption("chooseCopyKeyword","Copy the keyword Berserker",true,null,new("chooseCopyKeyword",0,view.Version,Choice:"Berserker"));
    Check(TrainingOpponent.Choose(view with { PendingChoice=new("copyKeyword",0,null,"Fukuzo: choose the keyword to copy.") },[copy]).Command?.Choice=="Berserker","The practice opponent stalled in a copy decision");
});
Run("answers a conditional effect choice (Mirumoto Yumaru) with an option the engine offered instead of stalling (issue #337)",()=>
{
    var game=MatchEngine.CreateStarterMatch();
    var view=game.GetView(0) with { PendingChoice=new("effectChoice",0,null,"Mirumoto Yumaru: choose one.") };
    var actions=new[]{"self","target","both"}.Select(o=>new ActionOption("chooseEffect",o,true,null,new("chooseEffect",0,view.Version,Choice:o),null)).ToArray();
    var decision=TrainingOpponent.Choose(view,actions);
    Check(decision.Command is {Type:"chooseEffect",Choice:"self"},"The practice opponent stalled, or took an option that was not offered first");
});
Run("chooses an offered free Experienced Overlay",()=>
{
    var game=MatchEngine.CreateEconomyScenario();var view=game.GetView(0);
    var higher=view.Players[0].Provinces[0].Card!;
    var action=new ActionOption("overlay","Overlay",true,null,new("overlay",0,view.Version,higher.Id,TargetId:"lower"),higher.Id,["lower"]);
    var decision=TrainingOpponent.Choose(view,[action]);
    Check(decision.Command is {Type:"overlay",TargetId:"lower"},"AI did not choose the offered free Overlay");
});
// Deck search (issue #312): the training AI must answer every stage of a search from the legal actions it is offered --
// the deck choice, taking an eligible card, a Region's province, the discard after a Terrain, and Pass when nothing qualifies.
Run("resolves a deck search at every stage without stalling: Region into a province, Terrain and discard, and the mandatory pass",()=>
{
    foreach(var seed in new ulong[]{1,42,831})
    {
        var region=MatchEngine.CreateDeckSearchShowcase(seed); var regionsBefore=region.GetView(0).Players[0].Provinces.Count(p=>p.Region is not null);
        ResolveSearch(region,"shizuka-search","Dynasty");
        var regionView=region.GetView(1);
        Check(regionView.ShownCards!.Length==1 && regionView.ShownCards[0].Definition.Type==CardType.Region,"The AI took a Region and showed it");
        var attached=regionView.Players[0].Provinces.Count(p=>p.Region is not null)+regionView.Players[0].Discard.Count(c=>c.Definition?.Type==CardType.Region);
        Check(attached==regionsBefore+1,"The Region attached to a province (or was discarded)");
        var terrain=MatchEngine.CreateDeckSearchShowcase(seed); var handBefore=terrain.GetView(0).Players[0].HandCount;
        ResolveSearch(terrain,"shizuka-search","Fate");
        Check(terrain.GetView(0).Players[0].HandCount==handBefore,"Terrain into the hand, one card discarded");
        Check(terrain.GetView(1).ShownCards is [{Definition.Type: CardType.Strategy}],"The AI showed a Terrain Strategy");
        var pass=MatchEngine.CreateDeckSearchShowcase(seed);
        ResolveSearch(pass,"training-search-optional",null); // the fixture's Fate deck holds no Weapon: Pass is the only legal answer
        Check(pass.GetView(1).ShownCards!.Length==0 && pass.GetView(1).Events.Any(e=>e.Type=="searchPassed") && pass.GetView(1).Events.Any(e=>e.Type=="deckShuffled"),"Passed and still shuffled");
    }
});
// Look at the top N cards (issue #322): the AI answers every stage of every look shape from the legal actions it is offered.
Run("resolves a look at the top cards at every stage without stalling: each deck of the four (the opponent's included), a required take, a fixed order, a shown card",()=>
{
    foreach(var seed in new ulong[]{1,42,831})
        foreach(var ability in new[]{"training-look-dynasty","training-look-any","training-look-take-item","training-look-fixed","training-look-show"})
            for(var deckIndex=0;deckIndex<4;deckIndex++)
            {
                var game=MatchEngine.CreateDeckSearchShowcase(seed);
                var source=game.State.Cards.Values.First(c=>c.OwnerId==0 && c.Zone==Zone.Home && c.DefinitionId=="training-look-source").Id;
                Check(game.Execute(new("useAbility",0,game.Version,source,AbilityId:ability)).Accepted,"The look could not be started");
                var steps=0; var viewedOpponentDeck=false;
                while(game.State.PendingChoice is {} pending && pending.Type.StartsWith("look"))
                {
                    var view=game.GetView(pending.PlayerId); var actions=game.QueryActions(pending.PlayerId);
                    Check(view.PendingLook is not null,$"The viewer has no look view at {pending.Type}");
                    // Narrow the deck choice to one deck (standing in for a player who prefers it); the arrangement is left to the AI.
                    var decks=actions.Where(a=>a.Type=="chooseLookDeck").ToArray();
                    if(decks.Length>0) actions=[decks[Math.Min(deckIndex,decks.Length-1)]];
                    var decision=TrainingOpponent.Choose(view,actions);
                    Check(decision.Command is not null,$"The AI stalled at {pending.Type}");
                    if(decision.Command!.Type=="chooseLookDeck" && decision.Command.TargetPlayerId==1) viewedOpponentDeck=true;
                    var result=game.Execute(decision.Command!);
                    Check(result.Accepted,$"Illegal {decision.Command.Type} at {pending.Type}: {result.Error}");
                    Check(++steps<6,"The look did not finish in a bounded number of decisions");
                }
                Check(game.State.PendingChoice is null,$"{ability}: the look finished and play continued");
                if(viewedOpponentDeck) Check(game.GetView(1).PendingLook is null && game.GetView(0).Events.Any(e=>e.Type=="topCardsLooked"),"The deck owner only learns that a look happened");
            }
});
foreach(var seed in new ulong[]{1,42,831,20260906,99999})
    Run($"seed {seed}: AI vs AI in the deck-search fixture plays on, searches, shuffles and replays deterministically",()=>PlaySearchMatch(seed));
foreach(var seed in new ulong[]{1,42,831,20260906,99999})
    Run($"seed {seed}: legal complete training match, bounded decisions and deterministic replay",()=>Play(seed));
foreach(var seed in new ulong[]{1,42,831,20260906,99999})
    Run($"seed {seed}: published abilities, complete match and replay",()=>Play(seed,true));
runner.Run("ai",tests);
return runner.Finish();

void Run(string name,Action run) => tests.Add((name,run));

static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
// Starts the search with an explicit command, then lets the training AI answer every decision from ONLY its own view and its
// offered actions (forceDeck narrows the deck choice, standing in for a player who prefers that deck).
static void ResolveSearch(MatchEngine game,string ability,string? forceDeck)
{
    var source=game.State.Cards.Values.First(c=>c.OwnerId==0 && c.Zone==Zone.Home && (CardCatalog.Get(c.DefinitionId).Abilities??[]).Contains(ability)).Id;
    Check(game.Execute(new("useAbility",0,game.Version,source,AbilityId:ability)).Accepted,"The search could not be started");
    var steps=0;
    while(game.State.PendingChoice is {} pending && pending.Type.StartsWith("search"))
    {
        var view=game.GetView(pending.PlayerId); var actions=game.QueryActions(pending.PlayerId);
        Check(view.PendingSearch is not null,$"The searcher has no deck viewer at {pending.Type}");
        if(forceDeck is not null) actions=actions.Where(a=>a.Type!="chooseSearchDeck" || a.Command?.Choice==forceDeck).ToArray();
        var decision=TrainingOpponent.Choose(view,actions);
        Check(decision.Command is not null,$"The AI stalled at {pending.Type}");
        var result=game.Execute(decision.Command!);
        Check(result.Accepted,$"Illegal {decision.Command!.Type} at {pending.Type}: {result.Error}");
        Check(++steps<8,"The search did not finish in a bounded number of decisions");
    }
    Check(game.State.PendingChoice is null,"The search finished and play continued");
}
static void PlaySearchMatch(ulong seed)
{
    // A bounded number of decisions rather than a finished match: the fixture's Regions (Fallow Lands' +2 strength) can make a
    // training match a genuine stalemate (seed 42 never ends), which says nothing about the search. What matters is that the AI
    // never stalls, never makes an illegal move, really searches and shuffles, and that the whole thing replays exactly.
    var game=MatchEngine.CreateDeckSearchShowcase(seed); var commands=0;
    while(game.GetView(0).WinnerId is null && commands<700)
    {
        var player=game.GetView(0).PriorityPlayerId;
        var decision=TrainingOpponent.Choose(game.GetView(player),game.QueryActions(player));
        Check(decision.Command is not null,$"No decision at {game.GetView(player).Phase}, step {commands}");
        var result=game.Execute(decision.Command!);
        Check(result.Accepted,$"Illegal {decision.Command!.Type}: {result.Error}"); commands++;
    }
    var view=game.GetView(0);
    Check(view.ShownCards!.Length>0 && view.Events.Any(e=>e.Type=="deckShuffled"),"The AI never searched: the fixture would prove nothing");
    var replay=MatchEngine.Replay(game.ExportReplay());
    Check(JsonSerializer.Serialize(replay.GetView(0))==JsonSerializer.Serialize(game.GetView(0)) && JsonSerializer.Serialize(replay.State)==JsonSerializer.Serialize(game.State),"Replay differs (shuffles included)");
    Console.WriteLine($"  {commands} decisions; {view.ShownCards.Length} cards shown; {view.Events.Count(e=>e.Type=="deckShuffled")} shuffles; {view.VictoryReason}; turn {view.TurnNumber}");
}
static void Play(ulong seed,bool published=false)
{
    var game=published?MatchEngine.CreatePublishedMatch(seed):MatchEngine.CreateStarterMatch(seed); var commands=0;
    while(game.GetView(0).WinnerId is null && commands<1600)
    {
        var player=game.GetView(0).PriorityPlayerId;
        var view=game.GetView(player); var actions=game.QueryActions(player);
        // Also exercise assignment reconstruction from legal target/province metadata.
        if(commands%3==0) actions=actions.Select(a=>a.Type is "attack" or "assignDefense"?a with {Command=null}:a).ToArray();
        var decision=TrainingOpponent.Choose(view,actions);
        Check(decision.Command is not null,$"No decision at {view.Phase}, step {commands}");
        var result=game.Execute(decision.Command!);
        Check(result.Accepted,$"Illegal {decision.Command!.Type}: {result.Error}"); commands++;
    }
    Check(game.GetView(0).WinnerId is not null,$"Match exceeded 1600 decisions (turn {game.GetView(0).TurnNumber})");
    Check(game.GetView(0).VictoryReason?.Contains("Military")!=true || game.GetView(0).Events.Any(e=>e.Type=="attackDeclared"),"A military victory needs a declared attack: the AI still declares (issue #354)");
    var replay=MatchEngine.Replay(game.ExportReplay());
    Check(JsonSerializer.Serialize(replay.GetView(0))==JsonSerializer.Serialize(game.GetView(0)),"Replay differs");
    Console.WriteLine($"  {commands} decisions; {game.GetView(0).VictoryReason}; turn {game.GetView(0).TurnNumber}");
}
