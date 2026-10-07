namespace L5R.Core;

// Issue #689: the P2 cost-term card wave. All prices are produced and paid in Core.
internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Action<MatchEngine, ResolvedActionFacts>> AfterActionTraits = new();
}

public static class Wave9501Abilities
{
    public const string Hamuro = "w9501-hamuro", Jikoji = "w9501-jikoji", Okazaki = "w9501-okazaki", Rokkaku = "w9501-rokkaku", Yunmen = "w9501-yunmen", Blade = "w9501-blade", Respect = "w9501-respect";
    public static LookSpec RokkakuLook { get; } = new("w9501-rokkaku-look", "Look at the top four cards of your Fate deck", [new("Fate")], 4, [new(LookDisposition.Bottom, 0, 1)], Reorder: true);
    public static Dictionary<string, AbilityDefinition> Add(Dictionary<string, AbilityDefinition> all)
    {
        all[Hamuro] = new("Limited", "Draw three cards. Remove Hamuro from the game", "none", ActionKeywords: ["Void"]);
        all[Jikoji] = new("Battle", "Give each of your attacking Lion Clan Samurai in Jikoji's army +1F", "none", ActionKeywords: ["Earth"]);
        all[Okazaki] = new("Limited", "Give another target Mantis Clan Personality Conqueror", "w9501-other-mantis", ActionKeywords: ["Water"]);
        all[Rokkaku] = new("Limited", "Look at the top four cards of your Fate deck. You may put one on the bottom. Put the rest back in any order", "none", Costs: ActionCost.BowSource, ActionKeywords: ["Void"], Look: RokkakuLook);
        all[Yunmen] = new("Open", "Give one to three target cards Cavalry", "slots", Costs: ActionCost.BowSource, ActionKeywords: ["Air"], Slots: [new("cards", "any-card", "One to three cards", 1, 3)]);
        all[Blade] = new("Battle", "Give this Shugenja +2F", "none", Costs: ActionCost.BowSource, ActionKeywords: ["Thunder"]);
        all[Respect] = new("Limited", "Put this Kata into play. Discard all your other Kata", "none", ActionKeywords: ["Political"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private IEnumerable<CostTerm> Wave9501Entry(EntryCostContext context)
    {
        var card = context.Card;
        if (context.Kind is EntryCostKind.Recruit or EntryCostKind.Reserve)
        {
            var ring = card.DefinitionId switch
            {
                "oracle-l5r-10474" or "oracle-l5r-10483" => "Ring of the Void",
                "oracle-l5r-10461" or "oracle-l5r-10442" => "Ring of Earth",
                "oracle-l5r-10467" => "Ring of Water",
                "oracle-l5r-10491" => "Ring of Air",
                _ => null
            };
            if (ring is not null && ControlsRing(context.PayerId, ring)) yield return new(0, ring, IgnoreCost: true);
            if (card.DefinitionId == "oracle-l5r-10842")
            {
                if (OpponentIsClan(context.PayerId, "Mantis")) yield return new(-2, "another player is Mantis Clan");
                if (IsDefendingPlayer(context.PayerId)) yield return new(-4, "you are the Defender");
            }
            if (Definition(card).Type == CardType.Personality)
            {
                foreach (var renyu in _state.Cards.Values.Where(c => c.OwnerId == context.PayerId && c.Id != card.Id && InPlay(c) && c.DefinitionId == "oracle-l5r-10275"))
                    if (PrintedGoldCost(card) >= 5) yield return new(-2, "Kuni Renyu");
                foreach (var kata in _state.Cards.Values.Where(c => InPlay(c) && c.DefinitionId == "oracle-l5r-10952"))
                    if (Definition(card).HonorRequirement is int requirement && requirement > Player(card.OwnerId).Honor) yield return new(2, "Respect");
            }
        }
        if (context.Kind == EntryCostKind.Equip)
        {
            if (Definition(card).Type == CardType.Follower && Keyword(card, "Cavalry"))
                foreach (var tselu in _state.Cards.Values.Where(c => c.OwnerId == context.PayerId && InPlay(c) && c.DefinitionId == "oracle-l5r-10328")) yield return new(-2, "Shinjo Tselu");
            if (card.DefinitionId == "oracle-l5r-10541" && context.Wielder is { } wielder && Keyword(wielder, "Thunder")) yield return new(-1, "Thunder Shugenja", WielderDependent: true);
        }
    }

    private IEnumerable<CostTerm> Wave9501AbilityCost(AbilityCostContext context)
    {
        if (context.Source is null || Definition(context.Source).Type != CardType.Strategy) yield break;
        foreach (var kanaye in _state.Cards.Values.Where(c => c.OwnerId == context.PayerId && InPlay(c) && c.DefinitionId == "oracle-l5r-10470")) yield return new(-1, "Yoritomo Kanaye");
    }

    private bool? RunWave9501(string id, PlayerState player, CardInstance? source, CardInstance? target, GameCommand command)
    {
        switch (id)
        {
            case Wave9501Abilities.Hamuro: Draw(player); Draw(player); Draw(player); RemoveUnitFromGame(source!); return false;
            case Wave9501Abilities.Jikoji:
                if (source!.OwnerId == _state.ActivePlayerId && source.Zone == Zone.Battlefield)
                    foreach (var samurai in Army(player.Id, source.ProvinceIndex!.Value).Where(c => HasClanAffiliation(c, "Lion") && Keyword(c, "Samurai"))) ApplyModifier(samurai, source.Id, id, "Force", 1);
                return false;
            case Wave9501Abilities.Okazaki: GrantKeyword(target!, "Conqueror", source!.Id, GrantDuration.Turn); return false;
            case Wave9501Abilities.Yunmen:
                foreach (var card in SlotCards(command, "cards")) GrantKeyword(card, "Cavalry", source!.Id, GrantDuration.Turn);
                return false;
            case Wave9501Abilities.Blade:
                if (WielderOf(source!) is { } shugenja) ApplyModifier(shugenja, source!.Id, id, "Force", 2);
                return false;
            case Wave9501Abilities.Respect: EnterPlayAsKata(player, source!); return false;
            default: return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register card callbacks before catalogue readers.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave9501()
    {
        EngineHooks.Abilities.Register("Wave9501", 12500, Wave9501Abilities.Add);
        EngineHooks.EntryCostTerms.Register("Wave9501Entry", 12500, c => c.Engine.Wave9501Entry(c));
        EngineHooks.AbilityCostTerms.Register("Wave9501Cost", 12500, c => c.Engine.Wave9501AbilityCost(c));
        EngineHooks.Looks.Register("Wave9501Look", 12500, () => [Wave9501Abilities.RokkakuLook]);
        EngineHooks.ProvinceBonus.Register("Wave9501Province", 12500, (engine, c) => c.mine && c.card.DefinitionId == "oracle-l5r-10275" ? 2 : 0);
        EngineHooks.TargetAllows.Register("Wave9501Target", 12500, (engine, c) => c.ability.Target == "w9501-other-mantis" && c.target.Id != c.source?.Id && Definition(c.target).Type == CardType.Personality && engine.HasClanAffiliation(c.target, "Mantis"));
        EngineHooks.RunEffects.Register("Wave9501Run", 12500, (engine, c) =>
        {
            if (c.id == Wave9501Abilities.Rokkaku)
            {
                engine.BeginLook(c.ability.Look!, c.source!, c.player.Id, "ability", c.bowedBeforeAction);
                return true;
            }
            return engine.RunWave9501(c.id, c.player, c.source, c.target, c.command);
        });
        EngineHooks.ValidateEarly.Register("Wave9501Validate", 12500, (engine, c) => { if (c.id == Wave9501Abilities.Respect) Require(c.source is { Zone: Zone.Hand }, "Put this Kata into play from your hand."); });
    }
}
