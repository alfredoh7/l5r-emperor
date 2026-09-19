using L5R.Core;

namespace L5R.AI;

public sealed record OpponentDecision(GameCommand? Command,string Reason);

/// <summary>A deliberately modest practice opponent. Its only inputs are one player's permitted view and offered actions.</summary>
public static class TrainingOpponent
{
    public static OpponentDecision Choose(MatchView view,ActionOption[] actions)
    {
        if(view.WinnerId is not null) return new(null,"The match has finished.");
        if(view.ViewerId!=view.PriorityPlayerId) return new(null,"Waiting for the other player's decision.");
        var self=view.Players.Single(p=>p.Id==view.ViewerId);
        var opponent=view.Players.Single(p=>p.Id!=view.ViewerId);
        var candidates=actions.Where(a=>a.Enabled && a.Type is not ("concede" or "wait"))
            .Select(a=>(Action:a,Command:Prepare(view,self,opponent,a)))
            .Where(x=>x.Command is not null)
            .Select(x=>(x.Action,Command:x.Command!,Score:Score(view,self,opponent,x.Action,x.Command!)))
            .OrderByDescending(x=>x.Score).ThenBy(x=>x.Action.Type,StringComparer.Ordinal)
            .ThenBy(x=>x.Command.CardId,StringComparer.Ordinal).ToArray();
        if(candidates.Length==0) return new(null,"No complete legal action was supplied for this decision.");
        var chosen=candidates[0];
        return new(chosen.Command,Explain(chosen.Action.Type));
    }

    private static GameCommand? Prepare(MatchView view,PlayerView self,PlayerView opponent,ActionOption action)
    {
        var command=action.Command;
        if(command is null)
        {
            command=action.Type switch
            {
                "attack" or "assignDefense" => new(action.Type,self.Id,view.Version,Assignments:[]),
                "pass" or "declineReaction" or "proclaim" or "endDynasty" => new(action.Type,self.Id,view.Version),
                "discardProvince" when action.CardId is not null => new(action.Type,self.Id,view.Version,CardId:action.CardId),
                "discardHand" => new(action.Type,self.Id,view.Version),
                "purchase" when action.Quote is { Error:null,Payments.Length:>0 } q => new(action.Type,self.Id,view.Version,q.CardId,q.Payments[0].SourceIds,ClanDiscount:q.ClanDiscount,ClanAllegiance:q.ClanAllegiance),
                "selectBattle" when action.Provinces is {Length:>0} p => new(action.Type,self.Id,view.Version,Province:p[0]),
                _=>null
            };
        }
        if(command is null || command.PlayerId!=view.ViewerId || command.ExpectedVersion!=view.Version) return null;
        if(action.Type is "attack" or "assignDefense")
        {
            var legalIds=action.TargetIds?.ToHashSet(StringComparer.Ordinal);
            var units=self.Home.Where(c=>c.Definition?.Type==CardType.Personality && !c.Bowed && (legalIds is null || legalIds.Contains(c.Id))).ToArray();
            var provinces=(action.Provinces??(action.Type=="attack"?opponent.Provinces:self.Provinces).Where(p=>!p.Destroyed).Select(p=>p.Index).ToArray());
            if(provinces.Length==0 || units.Length==0) return null;
            if(command.Assignments is {Count:>0} && action.Type=="attack") return command;
            var target=action.Type=="attack"
                ?opponent.Provinces.Where(p=>provinces.Contains(p.Index)).OrderBy(p=>p.Strength).ThenBy(p=>p.Index).First().Index
                :provinces.OrderByDescending(p=>ArmyForce(opponent.Army.Where(c=>c.ProvinceIndex==p))).ThenBy(p=>p).First();
            return command with { Assignments=units.ToDictionary(c=>c.Id,_=>target) };
        }
        if(action.Type=="discardHand")
        {
            var count=self.HandCount-8;
            if(count<=0 || self.Hand.Length!=self.HandCount) return null;
            return command with { CardIds=self.Hand.OrderBy(HandValue).ThenBy(c=>c.Id,StringComparer.Ordinal).Take(count).Select(c=>c.Id).ToArray() };
        }
        if(action.Type == "useAbility")
        {
            if(command.AbilityId is "border-first" or "border-game")
            {
                var ready=Production(self.Stronghold,self.Clan)+self.Home.Sum(c=>Production(c,self.Clan));
                var holdingCount=self.Home.Count(c=>c.Definition?.Type==CardType.Holding);
                var selected=self.Provinces.Where(p=>p.Card?.Definition is not null && (action.TargetIds??[]).Contains(p.Card.Id))
                    .Where(p=>p.Card!.Definition!.GoldCost>ready || self.TurnsTaken<=2 && holdingCount<3 && p.Card.Definition.Type==CardType.Personality || holdingCount>=7 && p.Card.Definition.Type==CardType.Holding)
                    .Select(p=>p.Card!.Id).ToArray();
                if(selected.Length==0) return null;
                return command with { CardIds=selected };
            }
            if(action.TargetIds is {Length:>0} legal)
            {
                var target=self.Home.Concat(self.Army).Concat(self.Hand).Concat(opponent.Home).Concat(opponent.Army)
                    .Where(c=>legal.Contains(c.Id)).OrderByDescending(c=>AbilityValue(command.AbilityId,c,self,opponent,view))
                    .ThenBy(c=>c.Id,StringComparer.Ordinal).FirstOrDefault();
                if(target is not null) return WithTarget(command,action,target.Id);
            }
        }
        if(action.Type is "equip" or "playStrategy" && action.TargetIds is {Length:>0} targets)
        {
            var targetCards=self.Home.Concat(self.Army).Concat(opponent.Army).Where(c=>targets.Contains(c.Id)).ToArray();
            if(targetCards.Length==0) return command;
            var ordered=targetCards.OrderBy(c=>c.Bowed).ThenByDescending(c=>c.Force);
            return WithTarget(command,action,ordered.ThenBy(c=>c.Id,StringComparer.Ordinal).First().Id);
        }
        return command;
    }

    /// <summary>Retargets an offered command. When the cost depends on the target (issue #303), the engine's
    /// own quote for that target supplies the payment, so the bot never submits sources priced for a
    /// different target; a target it cannot pay for is not a candidate.</summary>
    private static GameCommand? WithTarget(GameCommand command,ActionOption action,string targetId)
    {
        command=command with { TargetId=targetId };
        if(action.TargetGold is not null && action.TargetGold.TryGetValue(targetId,out var quote))
            return quote.Payments.Length==0 ? null : command with { SourceIds=quote.Payments[0].SourceIds };
        return command;
    }

    private static double Score(MatchView view,PlayerView self,PlayerView opponent,ActionOption action,GameCommand command)
    {
        var card=self.Provinces.Select(p=>p.Card).Concat(self.Hand).FirstOrDefault(c=>c?.Id==command.CardId);
        switch(action.Type)
        {
            case "chooseShinden":
                var threatened=self.Home.Concat(self.Army).FirstOrDefault(c=>c.Id==view.PendingChoice?.CardId);
                return command.Choice=="honor" ? (opponent.Honor<35 && threatened is {Bowed:false,Force:>0} ? 150 : 80) : 100;
            case "useAbility":
                if(command.AbilityId is "border-first" or "border-game") return command.AbilityId=="border-first"?140:35;
                if(command.AbilityId=="shunya-honor") return 100;
                // Deck search (issue #312): a modest bot uses Ikoma Shizuka's search when nothing more useful is on offer
                // (just above passing), which is what lets a whole AI-vs-AI match exercise the full search flow.
                if(command.AbilityId=="shizuka-search") return 12;
                var abilityTarget=self.Home.Concat(self.Army).Concat(self.Hand).Concat(opponent.Home).Concat(opponent.Army).FirstOrDefault(c=>c.Id==command.TargetId);
                return abilityTarget is null ? -20 : AbilityValue(command.AbilityId,abilityTarget,self,opponent,view);
            case "proclaim": return 1000;
            case "declineReaction": return 100;
            case "discardHand": return 100;
            case "selectBattle":
                return 100+ArmyForce(self.Army.Where(c=>c.ProvinceIndex==command.Province));
            case "purchase":
                var def=card?.Definition;
                if(def is null) return -100;
                var holdings=self.Home.Count(c=>c.Definition?.Type==CardType.Holding);
                if(def.Type==CardType.Holding)
                    return holdings<5?160-holdings*10:holdings<8?70-holdings:5;
                var baseScore=110+def.Force*2+def.PersonalHonor*2;
                if(action.Quote?.ProclaimEligible==true) baseScore+=20;
                if(command.ClanAllegiance && self.Honor>=def.HonorRequirement) baseScore-=20;
                if(command.ClanDiscount && !self.ProclaimUsedThisTurn) baseScore-=15;
                return baseScore-(action.Quote?.Cost??def.GoldCost);
            case "overlay":
                return card?.Definition is null ? -100 : 180+card.Definition.Force*2;
            case "discardProvince": return card?.Definition?.Type==CardType.Holding && self.Home.Count(c=>c.Definition?.Type==CardType.Holding)>=8?40:20;
            case "endDynasty": return 0;
            case "equip":
                var totalProduction=Production(self.Stronghold,self.Clan)+self.Home.Sum(c=>Production(c,self.Clan));
                return totalProduction>=6?60:5;
            case "playStrategy":
                var target=self.Army.Concat(opponent.Army).FirstOrDefault(c=>c.Id==command.TargetId);
                if(card?.Definition?.Effect=="bow-small") return target is {Bowed:false}?90+target.Force:0;
                if(target?.Bowed==true) return 0;
                return 70;
            case "attack":
                if(command.Assignments is not {Count:>0}) return -100;
                var attacking=self.Home.Where(c=>command.Assignments.ContainsKey(c.Id) || c.AttachedTo is not null && command.Assignments.ContainsKey(c.AttachedTo));
                var force=ArmyForce(attacking);
                var province=opponent.Provinces.First(p=>p.Index==command.Assignments.First().Value);
                var defense=ArmyForce(opponent.Home);
                return force>defense+province.Strength?90+force-defense:force>province.Strength && defense==0?80:0;
            case "assignDefense":
                var enemyForce=ArmyForce(opponent.Army);
                var ownForce=ArmyForce(self.Home);
                if(enemyForce==0) return 0;
                return ownForce>=enemyForce?95:ownForce+StrongestThreatProvinceStrength(self,opponent)>=enemyForce?60:self.Provinces.Count(p=>!p.Destroyed)==1?50:0;
            case "pass": return 10;
            // Deck search (issue #312): a modest bot never stalls in a search. It takes the first eligible card the engine
            // lists (the list is already sorted by type then name, and holds only eligible entries), passes when the
            // engine offers Pass (which it does only when nothing qualifies), attaches a found Region to the first
            // offered province, and discards its least useful hand card after a Terrain. Ties keep the engine's order.
            case "chooseSearchDeck": case "selectSearchCard": case "passSearch": case "chooseSearchProvince": return 100;
            // Look at the top N cards (issue #322): a modest bot answers a look with the first arrangement the engine offers -- the
            // simplest legal one (keep everything in order when that is legal, otherwise take the top card that may be taken) -- and
            // picks the first offered deck.
            case "chooseLookDeck": case "arrangeTopCards": return 100;
            case "chooseSearchDiscard":
                var discard=self.Hand.FirstOrDefault(c=>c.Id==command.TargetId);
                return discard is null?0:100-HandValue(discard);
            // A forced discard (Times of Strife, Daigotsu Bofana; issue #333): the same modest rule as the discard after a Terrain -- give up
            // the least useful card in hand.
            case "forcedDiscard":
                var forced=self.Hand.FirstOrDefault(c=>c.Id==command.TargetId);
                return forced is null?0:100-HandValue(forced);
            // Battle Phase timing (issue #310): the before-this-battle-resolves choices. Any legal answer is acceptable to a
            // modest practice opponent: keep its strongest Personality, bow the strongest enemy one, and take effects in offered order.
            case "encircledSelect":
                var kept=self.Army.FirstOrDefault(c=>c.Id==command.TargetId);
                return 100+(kept?.Force??0);
            case "encircledOrder": case "beforeBattleOrder": return 50;
            // A conditional effect choice (issue #337, Mirumoto Yumaru): a modest bot never stalls on it; every offered option is legal, so it
            // takes the first one the engine lists (ties keep the engine's order).
            case "chooseEffect": return 100;
            case "beforeBattleOptional":
                var optionalTarget=opponent.Army.FirstOrDefault(c=>c.Id==command.TargetId);
                return command.Choice=="use" ? 60+(optionalTarget?.Force??0) : 20;
            default: return -100;
        }
    }

    private static double AbilityValue(string? ability,CardView target,PlayerView self,PlayerView opponent,MatchView view)
    {
        var own=target.OwnerId==self.Id;
        return ability switch
        {
            "shinden-choice" => !own && !target.Bowed ? 70+target.Force : -20,
            "air-straighten" or "paid-straighten" => own && target.Bowed && !target.StraightenBlocked ? 75+target.Force : -20,
            "paid-bow" => !own && !target.Bowed ? 65+target.Force : -20,
            "prepared-death" => own && !target.Bowed && self.Id==view.ActivePlayerId ? 35+target.Force : -20,
            "kouki-force" or "costly-feud" => !own && !target.Bowed && target.Force>0 ? 75+target.Force : -20,
            "defensive-stance" => own && target.Bowed && !target.StraightenBlocked ? 95+target.Force : -20,
            "crushing-force" => own && !target.Bowed ? 70+target.Force : -20,
            "crushing-destroy" or "never-afraid" => !own ? 90+target.Force : -20,
            "incapacitated" or "block-supply" or "favor-battle" => !own ? 100+target.Force : -20,
            "lobby" => view.FavorPlayerId==self.Id ? -20 : 60-target.Force-(target.Definition?.Abilities?.Contains("shunya-honor")==true?25:0),
            "favor-cycle" => self.HandCount>=6 && !self.Home.Any(c=>!c.Bowed && c.Definition?.Abilities?.Contains("shunya-honor")==true) ? 25-HandValue(target) : -20,
            _ => -20
        };
    }

    private static int StrongestThreatProvinceStrength(PlayerView self,PlayerView enemy)
    {
        var target=self.Provinces.Where(p=>!p.Destroyed).OrderByDescending(p=>ArmyForce(enemy.Army.Where(c=>c.ProvinceIndex==p.Index))).FirstOrDefault();
        return target?.Strength??0;
    }
    private static int ArmyForce(IEnumerable<CardView> cards) => cards.Where(c=>!c.Bowed && c.Definition?.Type is CardType.Personality or CardType.Follower).Sum(c=>Math.Max(0,c.Force));
    private static int Production(CardView card,string clan) => card.Bowed?0:(card.Definition?.GoldProduction??0)+(card.Definition?.Effect=="crane-gold" && clan=="Crane" || card.Definition?.Effect=="dragon-gold" && clan=="Dragon"?1:0);
    private static int HandValue(CardView card)=>card.Definition?.Effect switch {"force-three"=>8,"bow-small"=>6,_=>card.Definition?.Type==CardType.Follower?5:4};
    private static string Explain(string type)=>type switch
    {
        "purchase"=>"Develop Gold production early, then recruit Personalities and preserve a useful Proclaim opportunity.",
        "overlay"=>"Recruit the higher-Experience Personality for free while preserving the existing unit.",
        "proclaim"=>"Take the available once-per-turn Honor gain after the Personality enters play.",
        "attack"=>"Commit the offered unbowed army when visible Force can threaten a province.",
        "assignDefense"=>"Defend the battlefield with the largest visible attacking army.",
        "playStrategy"=>"Use a legal Battle action to improve the visible Force comparison.",
        "equip"=>"Attach support to an available Personality using the offered payment.",
        "discardProvince"=>"Cycle remaining face-up cards after purchasing is complete.",
        "discardHand"=>"Keep more useful combat cards while returning to eight cards.",
        _=>"Continue the current legal action round."
    };
}
