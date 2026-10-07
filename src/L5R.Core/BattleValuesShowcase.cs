namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Explicit local review fixture; ordinary engine commands and queries run in the prepared battle.
    /// Not reachable from normal menus, deck creation or network rooms.</summary>
    public static MatchEngine CreateBattleValuesShowcase(string scenario)
    {
        if (scenario is not ("standard" or "bowed" or "open" or "empty" or "crowded" or "bonus" or "lethal" or "armor-off" or "armor-on"))
            throw new ArgumentException("Unknown battle values scenario", nameof(scenario));
        var s = CreatePublishedMatch(91).State;
        s.Phase = Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [1]; s.BattleNumber = 1;
        foreach (var card in s.Cards.Values.Where(c => c.Zone is Zone.Home or Zone.Battlefield &&
                     CardCatalog.Get(c.DefinitionId).Type is not CardType.Holding).ToArray()) s.Cards.Remove(card.Id);
        CardInstance Add(CardDefinition definition, int owner, string? parent = null, bool bowed = false)
        {
            var card = new CardInstance
            {
                Id = $"values-{s.Cards.Count}",
                DefinitionId = definition.Id,
                OwnerId = owner,
                Zone = parent is null ? Zone.Battlefield : Zone.Attached,
                ProvinceIndex = 1,
                FaceUp = true,
                AttachedTo = parent,
                Bowed = bowed
            };
            s.Cards.Add(card.Id, card); return card;
        }
        var personalities = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && !c.CreatedOnly && c.Type == CardType.Personality && c.ArtworkKey is not null).ToArray();
        var crane = personalities.First(c => c.Name.Contains("Hakuseki", StringComparison.Ordinal));
        var lion = personalities.First(c => c.Name == "Ikoma Shika");
        var second = personalities.First(c => c.Name == "Doji Dainagon");
        var follower = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && !c.CreatedOnly && c.Type == CardType.Follower && c.ArtworkKey is not null && c.Force > 0);
        if (scenario != "empty")
        {
            var own = Add(crane, 0, bowed: scenario == "bowed");
            Add(follower, 0, own.Id);
            Add(lion, 1); Add(second, 1);
            if (scenario == "crowded")
                foreach (var player in new[] { 0, 1 })
                    for (var i = 0; i < 4; i++) { var host = Add(player == 0 ? crane : lion, player, bowed: i == 2); Add(follower, player, host.Id); }
            if (scenario is "armor-off" or "armor-on")
            {
                // Attacker Force sits exactly on defender Force plus Strength without the armor, so the armor alone tips the province over.
                var probe = FromTestState(s); var totals = probe.ArmyForcesForTest();
                s.ArmyForceBonuses.Add(new(0, totals.Defender + probe.EffectiveProvinceStrength(probe.Player(1).Provinces[1]) - totals.Attacker, s.BattleNumber));
                if (scenario == "armor-on") Add(CardCatalog.Get("oracle-l5r-10118"), 0, own.Id);
            }
            if (scenario == "lethal") s.ArmyForceBonuses.Add(new(0, 20, s.BattleNumber)); // far beyond defender Force plus Strength
            if (scenario == "bonus")
            {
                s.ForceRedirects.Add(new(own.Id, 1));
                s.ArmyForceBonuses.Add(new(0, 5, s.BattleNumber));
            }
        }
        var engine = FromTestState(s);
        if (scenario == "open")
        {
            var field = engine.CreateAdditionalBattlefieldForTest(null);
            s = engine.State; s.CurrentBattleProvince = field; s.UnresolvedBattlefields = [field];
            foreach (var c in s.Cards.Values.Where(c => c.Zone is Zone.Battlefield or Zone.Attached)) c.ProvinceIndex = field;
            engine = FromTestState(s);
        }
        return engine;
    }
}
