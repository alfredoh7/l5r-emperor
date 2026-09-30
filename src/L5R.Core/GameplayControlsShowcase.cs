namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Isolated native review seeds using the real engine. Never used by rooms or deck construction.</summary>
    public static MatchEngine CreateGameplayControlsShowcase(string kind)
    {
        var s = CreateGoldPaymentShowcase(kind == "discount" ? "derived" : "fixed").State;
        CardInstance Put(string definition, int owner, Zone zone, bool bowed = false, string? parent = null)
        {
            var c = new CardInstance
            {
                Id = "controls-" + s.Cards.Count,
                DefinitionId = definition,
                OwnerId = owner,
                Zone = zone,
                FaceUp = true,
                Bowed = bowed,
                ProvinceIndex = zone == Zone.Battlefield ? 0 : null,
                AttachedTo = parent
            };
            s.Cards.Add(c.Id, c); if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id); return c;
        }
        void Execute(MatchEngine e, GameCommand command)
        { var result = e.Execute(command); if (!result.Accepted) throw new InvalidOperationException($"Controls fixture {kind}: {result.Error}"); }
        if (kind is "reaction" or "destruction")
        {
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && Definition(c).GoldProduction > 0)) c.Bowed = true;
            if (kind == "reaction")
            {
                s.Phase = Phase.Dynasty;
                s.Cards[s.Players[0].Provinces[0].CardId!].DefinitionId = "training-lion-samurai";
                Put("oracle-l5r-6529", 0, Zone.Home); Put("training-marketplace", 0, Zone.Home);
            }
            else { Put("oracle-l5r-9828", 0, Zone.Home); Put("oracle-l5r-9798", 0, Zone.Home, true); }
        }
        else if (kind == "discount")
        {
            Put("oracle-l5r-5361", 0, Zone.Battlefield);
            Put("training-crane-samurai", 0, Zone.Dead);
        }
        else if (kind == "entry")
        {
            s.Phase = Phase.Dynasty;
            var id = s.Players[0].Provinces[0].CardId!; s.Cards[id].DefinitionId = "oracle-l5r-2331";
            var holder = Put("training-crane-samurai", 0, Zone.Home); holder.Tokens.Add(new("controls-sake", "Sake", "review fixture"));
            Put("oracle-l5r-2896", 0, Zone.Hand); Put("oracle-l5r-8127", 0, Zone.Hand);
            var e = FromTestState(s); var option = e.QueryActions(0).First(a => a.Enabled && a.Type == "purchase" && a.CardId == id);
            Execute(e, option.Command! with { SourceIds = option.Quote!.Payments[0].SourceIds }); return e;
        }
        else if (kind is "honor" or "honor-reaction")
        {
            s.ActivePlayerId = s.PriorityPlayerId = 1;
            var victim = Put("training-crane-samurai", 0, Zone.Home);
            var source = Put(CardCatalog.Cards.Values.First(d => d.Abilities?.Contains("tsujiken-exp-dishonor") == true).Id, 1, Zone.Home);
            if (kind == "honor")
            {
                var temple = Put("oracle-l5r-7908", 0, Zone.Region); temple.ProvinceIndex = 1; s.Players[0].Provinces[1].RegionId = temple.Id;
            }
            else Put(CardCatalog.Cards.Values.First(d => d.Abilities?.Contains(HonorChangeReactions.UnimpeachableName) == true).Id, 0, Zone.Hand);
            var e = FromTestState(s); var action = e.QueryActions(1).First(a => a.Enabled && a.Command?.AbilityId == "tsujiken-exp-dishonor");
            Execute(e, action.Command! with { TargetId = victim.Id });
            if (kind == "honor-reaction")
                for (var guard = 0; guard < 8 && e.State.PendingChoice is not null && e.State.PendingChoice.Type != "honorChangeReaction"; guard++)
                {
                    var pass = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Type == "declineReaction");
                    if (pass?.Command is null) break;
                    Execute(e, pass.Command);
                }
            return e;
        }
        else if (kind == "event")
        {
            var source = Put(CardCatalog.Cards.Values.First(d => d.Effect == GloryOfTheShogunEffect).Id, 0, Zone.EventInPlay);
            var e = FromTestState(s);
            e.OpenEventChoice(new PendingEventChoice
            {
                Kind = "draw",
                PlayerId = 0,
                EventCardId = source.Id,
                Origin = "deferred",
                ClosesTo = 0,
                Prompt = "Glory of the Shogun: you may draw a card."
            }); return e;
        }
        else if (kind is "killing" or "greasing")
        {
            s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
            if (kind == "killing")
            {
                s.PriorityPlayerId = 1;
                Put("oracle-l5r-8188", 0, Zone.Battlefield);
                Put("training-crane-samurai", 0, Zone.Battlefield);
                Put("training-lion-samurai", 1, Zone.Battlefield); Put("training-lion-veteran", 1, Zone.Battlefield);
                var e = FromTestState(s); Execute(e, new("pass", 1, e.Version)); return e;
            }
            Put("training-lion-courtier", 0, Zone.Battlefield);
            var target = Put("training-lion-samurai", 1, Zone.Battlefield, true);
            Put("training-item", 1, Zone.Attached, true, target.Id); Put("oracle-l5r-2896", 0, Zone.Hand);
        }
        else if (kind == "infiltrator")
        {
            s.ActivePlayerId = s.PriorityPlayerId = 1;
            var asuka = Put("oracle-l5r-10087", 0, Zone.Home, true); asuka.OriginalOwnerId = 1;
            Put("training-crane-samurai", 0, Zone.Home, true); Put("training-lion-samurai", 0, Zone.Home, true);
            var e = FromTestState(s);
            for (var guard = 0; guard < 24 && e.State.PendingChoice?.Type != "infiltratorStraighten"; guard++)
            {
                var action = e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Type is "pass" or "endDynasty" or "declineReaction");
                Execute(e, action.Command!);
            }
            if (e.State.PendingChoice?.Type != "infiltratorStraighten") throw new InvalidOperationException("Infiltrator review did not open");
            return e;
        }
        else if (kind == "favor")
        {
            s.FavorPlayerId = 0;
            Put(CardCatalog.Cards.Values.First(d => d.Effect == EmperorThreeSAbilities.KurohitoEffect).Id, 0, Zone.Celestial);
        }
        else if (kind == "additional") s.AdditionalActions.Add(new(0, null));
        else throw new ArgumentException("Unknown controls review scenario", nameof(kind));
        return FromTestState(s);
    }
}
