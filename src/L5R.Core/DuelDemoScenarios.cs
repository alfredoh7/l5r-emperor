namespace L5R.Core;

public sealed record DuelDemoScenario(string Id, string Name, string Description, string ChallengeAbility,
    bool TrainingFixture = false);

/// <summary>Prepared positions, not playable deck profiles. IDs and shuffle algorithm are replay inputs.</summary>
public static class DuelDemoScenarios
{
    public const ulong DefaultSeed = 205;
    public static IReadOnlyList<DuelDemoScenario> All { get; } = Array.AsReadOnly(new DuelDemoScenario[] {
        new("steel", "Steel on Steel", "Doji Tatsuki versus Matsu Hachiro. Courtesy from Crane's hand and Paid Off from Lion's deck reproduce 8–7 with one focus each.", "steel-on-steel-duel"),
        new("refusal", "Accept or refuse", "A synthetic refusable challenge. Accept to focus, or refuse without a duel.", "training-duel-challenge", true),
        new("reaction", "Before the duel", "Synthetic reaction fixtures let both players react before focusing.", "steel-on-steel-duel", true),
        new("empty", "Immediate Strike / empty sources", "Neither player has a focus source. Strike immediately.", "steel-on-steel-duel"),
        new("four", "Four-focus limit", "Both players have enough cards to reach the four-focus limit. Use manual mode to exercise the limit.", "steel-on-steel-duel"),
        new("duelist", "Duelist replacement", "Kitsuki Daisuke may replace a focused card after Strike.", "daisuke-duel"),
        new("tie", "Equal totals", "Two copies of Matsu Hachiro start with equal Chi. Strike immediately for a double loss.", "steel-on-steel-duel"),
        new("duelist-tie", "Two Duelists", "Both Daisukes have equal Chi. The active player replaces first; equal final totals mean both lose.", "daisuke-duel"),
        new("effects", "Focus Effect order", "Courtesy is available on both sides. Focus it on each side to choose effect order after Strike.", "steel-on-steel-duel"),
        new("force", "Force duel", "Synthetic challenge using authoritative Force, including attachments.", "training-duel-force", true),
        new("honor", "Personal Honor duel", "Synthetic challenge using authoritative Personal Honor.", "training-duel-honor", true),
        new("discard", "Focus from discard", "Daisuke with Mirumoto Hikaro: one public discard-pile focus, removed after resolution.", "daisuke-duel"),
        new("aftermath", "Aftermath reactions", "Experienced Daisuke can draw after winning; defeating Overconfident Asahina Tsugio offers another draw.", "steel-on-steel-duel"),
        new("choice-discard", "Focus Effect · return to hand", "Disreputable Deal offers two Political Strategies from your discard pile.", "steel-on-steel-duel"),
        new("choice-crowded", "Focus Effect · many cards", "A larger discard choice tests scrolling and complete card faces.", "steel-on-steel-duel"),
        new("choice-attach", "Focus Effect · attach a card", "Shamsir offers legal recipients after the duel.", "steel-on-steel-duel"),
        new("choice-straighten", "Focus Effect · required target", "Ceaseless Vigil requires a bowed Personality to straighten.", "steel-on-steel-duel"),
        new("choice-destroy", "Focus Effect · destroy attachment", "The Crimson Mark offers a surviving attachment as an optional target.", "steel-on-steel-duel"),
        new("choice-effect", "Focus Effect · Honor or card", "Sandalwood Incense offers two different effects after a win.", "steel-on-steel-duel"),
        new("choice-multiple", "Focus Effect · consecutive choices", "Two Disreputable Deals pause in the selected effect order.", "steel-on-steel-duel"),
        new("open", "Return to Action phase", "Synthetic Open challenge exercises return to the outer Action phase.", "training-duel-open", true)
    });
    public static DuelDemoScenario Get(string id) => All.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException("Unknown duel demo scenario.");
}

public sealed partial class MatchEngine
{
    public static MatchEngine CreateDuelDemo(string scenario = "steel", ulong seed = DuelDemoScenarios.DefaultSeed) => Create(seed, "duel-demo/" + scenario);

    private static MatchEngine CreateDuelDemoState(ulong seed, string id)
    {
        var scenario = DuelDemoScenarios.Get(id);
        var state = new MatchState { Players = [new() { Id=0,Name="Crane",Clan="Crane",Honor=8 },new() { Id=1,Name="Lion",Clan="Lion",Honor=6 }],
            Phase=id=="open"?Phase.Action:Phase.Battle, ActivePlayerId=1, PriorityPlayerId=0,
            CurrentBattleProvince=id=="open"?null:0, UnresolvedBattlefields=id=="open"?[]:[0] };
        // Match the prepared participants' affiliations. In particular, Hikaro is Loyal and
        // must not be placed under the canonical Crane player's affiliation in the discard setup.
        var firstClan=id is "duelist" or "duelist-tie" or "discard" or "aftermath"?"Dragon":id=="tie"?"Lion":"Crane";
        var secondClan=id=="duelist-tie"?"Dragon":id=="aftermath"?"Crane":"Lion";
        state.Players[0].Clan=firstClan;state.Players[1].Clan=secondClan;
        state.Players[0].Name=firstClan+(firstClan==secondClan?" · challenger":"");
        state.Players[1].Name=secondClan+(firstClan==secondClan?" · challenged":"");
        var sequence=0;
        CardInstance Add(int owner,string definition,Zone zone,string? attached=null)
        {
            _=CardCatalog.Get(definition);
            var card=new CardInstance { Id=$"demo-{++sequence:D4}",DefinitionId=definition,OwnerId=owner,Zone=zone,
                FaceUp=zone is not (Zone.Hand or Zone.FateDeck),ProvinceIndex=zone==Zone.Battlefield?0:null,AttachedTo=attached };
            state.Cards.Add(card.Id,card);
            if(zone==Zone.Hand)state.Players[owner].Hand.Add(card.Id);
            if(zone==Zone.FateDeck)state.Players[owner].FateDeck.Add(card.Id);
            return card;
        }
        foreach(var p in state.Players)
        {
            p.StrongholdId=Add(p.Id,$"training-{p.Clan.ToLowerInvariant()}-stronghold",Zone.Stronghold).Id;
            p.Provinces=Enumerable.Range(0,4).Select(i=>new ProvinceState {Index=i,Strength=6}).ToList();
        }
        var challenger=id switch { "duelist" or "duelist-tie" or "discard"=>"oracle-l5r-4406", "aftermath"=>"oracle-l5r-4407", "tie"=>"oracle-l5r-4915", _=>"oracle-l5r-2166" };
        var challenged=id switch { "duelist-tie"=>"oracle-l5r-4406", "aftermath"=>"oracle-l5r-10047", _=>"oracle-l5r-4915" };
        var zone=id=="open"?Zone.Home:Zone.Battlefield;
        var left=Add(0,challenger,zone);var right=Add(1,challenged,zone);
        // Real printed Followers preserve canonical Chi and make unit destruction observable.
        Add(0,"oracle-l5r-9147",Zone.Attached,left.Id);Add(1,"oracle-l5r-9147",Zone.Attached,right.Id);
        if(scenario.ChallengeAbility.StartsWith("training-",StringComparison.Ordinal)) Add(0,scenario.ChallengeAbility,Zone.Hand);
        else if(scenario.ChallengeAbility=="steel-on-steel-duel") Add(0,"oracle-l5r-7483",Zone.Hand);
        if(id=="reaction") { Add(0,"training-duel-reactor",Zone.Home);Add(1,"training-duel-reactor",Zone.Home); }
        if(id=="discard") { Add(0,"oracle-l5r-5095",Zone.Home);Add(0,"oracle-l5r-7996",Zone.FateDiscard); }
        if(id!="empty")
        {
            var random=new StableRandom(seed);
            // Keep the canonical first pair stable. Extra cards vary with the recorded seed.
            Add(0,"oracle-l5r-7996",Zone.Hand);
            Add(1,id=="effects"?"oracle-l5r-7996":"oracle-l5r-5861",Zone.Hand);
            foreach(var p in state.Players)
            {
                // Freeze this pool independently of future live deck edits; old demo replays keep their deal.
                var pool=new List<string>{"oracle-l5r-1045","oracle-l5r-1521","oracle-l5r-1589","oracle-l5r-2619","oracle-l5r-3730","oracle-l5r-5861","oracle-l5r-6051","oracle-l5r-9916","oracle-l5r-10368"};
                random.Shuffle(pool);
                for(var i=0;i<4;i++)Add(p.Id,pool[i%pool.Count],Zone.Hand);
                Add(p.Id,"oracle-l5r-5861",Zone.FateDeck);
                random.Shuffle(pool);
                for(var i=0;i<8;i++)Add(p.Id,pool[i%pool.Count],Zone.FateDeck);
            }
        }
        if(id.StartsWith("choice-",StringComparison.Ordinal))ConfigureDuelChoiceScenario(state,id,Add);
        var engine=new MatchEngine(state,seed,"duel-demo/"+id);
        engine.Emit("scenario",$"Duel Demo: {scenario.Name}. Seed {seed}. {scenario.Description}");
        return engine;
    }
}
