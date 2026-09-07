namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Only this detached view may cross the trust boundary to a player.</summary>
    public MatchView GetView(int playerId)
    {
        if(playerId is <0 or >1) throw new ArgumentOutOfRangeException(nameof(playerId));
        CardView ViewCard(CardInstance card,bool visible=true)
        {
            if(!visible)
                return new($"hidden-p{card.OwnerId}-province{card.ProvinceIndex}",null,card.OwnerId,card.Zone,false,false,card.ProvinceIndex,null,0);
            var def=Definition(card);
            var force=def.Type==CardType.Personality?PersonalityForce(card):Math.Max(0,def.Force+StatModifier(card,"Force"));
            return new(card.Id,Copy(def),card.OwnerId,card.Zone,card.FaceUp,card.Bowed,card.ProvinceIndex,card.AttachedTo,force,def.Type==CardType.Personality?PersonalityChi(card):def.Chi,card.StraightenBlockedTurn==_state.TurnNumber,card.Modifiers.ToArray());
        }
        var players=_state.Players.Select(player=>
        {
            var owned=_state.Cards.Values.Where(c=>c.OwnerId==player.Id).ToArray();
            return new PlayerView(player.Id,player.Name,player.Clan,player.Honor,player.TurnsTaken,
                ViewCard(Card(player.StrongholdId)),
                owned.Where(c=>c.Zone==Zone.Home || c.Zone==Zone.Attached && Card(c.AttachedTo).Zone==Zone.Home).Select(c=>ViewCard(c)).ToArray(),
                owned.Where(c=>c.Zone==Zone.Battlefield || c.Zone==Zone.Attached && Card(c.AttachedTo).Zone==Zone.Battlefield).Select(c=>ViewCard(c)).ToArray(),
                player.Provinces.Select(p=>new ProvinceView(p.Index,p.Strength,p.Destroyed,p.CardId is null?null:ViewCard(Card(p.CardId),Card(p.CardId).FaceUp))).ToArray(),
                player.Id==playerId?player.Hand.Select(id=>ViewCard(Card(id))).ToArray():[],player.Hand.Count,
                player.DynastyDeck.Count,player.FateDeck.Count,
                owned.Where(c=>c.Zone is Zone.DynastyDiscard or Zone.FateDiscard or Zone.Dead).Select(c=>ViewCard(c)).ToArray(),
                player.ProclaimUsedTurn==_state.TurnNumber,player.DishonorLossPending);
        }).ToArray();
        return new(ContentVersion,_scenario,_state.Version,_state.TurnNumber,playerId,_state.ActivePlayerId,_state.PriorityPlayerId,
            _state.Phase,_state.PendingChoice,_state.CurrentBattleProvince,_state.UnresolvedBattlefields.ToArray(),
            _state.WinnerId,_state.VictoryReason,players,_state.Events.ToArray(),_scenario==PublishedContent.Scenario?["Published Lion/Crane playtest: all 40 definitions supported under the pinned Emperor profile.","Other imported decks remain reference-only; this profile is not tournament certification."]:[..Limits],_state.FavorPlayerId);
    }

    /// <summary>Returns exact proposed commands plus customization targets. Command validation remains authoritative.</summary>
    public ActionOption[] QueryActions(int playerId)
    {
        if(playerId is <0 or >1) return [];
        if(_state.WinnerId is not null) return [];
        var result=new List<ActionOption>();
        GameCommand Command(string type,string? id=null,string[]? sources=null,int? province=null,bool discount=false,bool allegiance=false,
            Dictionary<string,int>? assignments=null,string[]? cardIds=null,string? target=null)
            =>new(type,playerId,_state.Version,id,sources,province,discount,allegiance,assignments,cardIds,target);
        void Add(string type,string label,GameCommand? command=null,string? reason=null,string? cardId=null,string[]? targets=null,int[]? provinces=null,PurchaseQuote? quote=null)
            =>result.Add(new(type,label,reason is null,reason,command,cardId,targets,provinces,quote));
        if(playerId!=_state.PriorityPlayerId)
        {
            Add("wait",$"Waiting for {Player(_state.PriorityPlayerId).Name}",reason:"The other player has the action opportunity.");
            Add("concede","Concede",Command("concede"));
            return result.ToArray();
        }
        var player=Player(playerId);
        if(_state.PendingChoice is { Type:"proclaim" } pending)
        {
            if(pending.PlayerId==playerId)
            {
                var honor=Definition(Card(pending.CardId)).PersonalHonor;
                Add("proclaim",$"Proclaim · gain {honor} Honor",Command("proclaim"),player.ProclaimUsedTurn==_state.TurnNumber?"Already used Proclaim this turn.":null,pending.CardId);
            }
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"shinden" })
        {
            Add("chooseShinden","Bow and prevent straightening this turn",Command("chooseShinden") with {Choice="bow"});
            Add("chooseShinden","Allow opponent to gain 1 Honor",Command("chooseShinden") with {Choice="honor"});
        }
        else if(_state.PendingChoice is { Type:"discardHand" })
        {
            var count=player.Hand.Count-8;
            Add("discardHand",$"Discard {count} card(s)",Command("discardHand",cardIds:player.Hand.Take(count).ToArray()),targets:player.Hand.ToArray());
        }
        else switch(_state.Phase)
        {
            case Phase.Action:
                if(playerId==_state.ActivePlayerId)
                {
                    var targets=_state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone==Zone.Home && Definition(c).Type==CardType.Personality).ToArray();
                    foreach(var card in player.Hand.Select(Card).Where(c=>Definition(c).Type is CardType.Item or CardType.Follower))
                    {
                        var def=Definition(card);
                        var legalTargets=targets.Where(t=>AttachmentAllowed(card,t) && (def.Type!=CardType.Follower || def.HonorRequirement is null || Definition(t).PersonalHonor>=def.HonorRequirement)).Select(t=>t.Id).ToArray();
                        var payment=PaymentOptions(playerId,def.GoldCost).FirstOrDefault();
                        var reason=legalTargets.Length==0?"No Personality meets the attachment requirements.":payment is null?"Insufficient unbowed Gold production.":null;
                        Add("equip",$"Equip {def.Name} · {def.GoldCost} Gold",reason is null?Command("equip",card.Id,payment!.SourceIds,target:legalTargets[0]):null,
                            reason,card.Id,legalTargets);
                    }
                }
                Add("pass","Pass action",Command("pass"));
                break;
            case Phase.AttackDeclaration:
            case Phase.DefenseAssignment:
                var units=_state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone==Zone.Home && !c.Bowed && Definition(c).Type==CardType.Personality).Select(c=>c.Id).ToArray();
                var provinces=Player(1-_state.ActivePlayerId).Provinces.Where(p=>!p.Destroyed).Select(p=>p.Index).ToArray();
                var attack=_state.Phase==Phase.AttackDeclaration;
                var actionType=attack?"attack":"assignDefense";
                foreach(var province in provinces)
                    Add(actionType,$"{(attack?"Attack":"Defend")} province {province+1} · {units.Length} units",
                        Command(actionType,assignments:units.ToDictionary(id=>id,_=>province)),units.Length==0?"No unbowed Personalities at home.":null,
                        targets:units,provinces:provinces);
                Add("pass",attack?"Skip attack":"Assign no defenders",Command("pass"));
                break;
            case Phase.BattleSelection:
                foreach(var province in _state.UnresolvedBattlefields) Add("selectBattle",$"Fight at province {province+1}",Command("selectBattle",province:province));
                break;
            case Phase.Battle:
                foreach(var card in player.Hand.Select(Card).Where(c=>Definition(c).Type==CardType.Strategy && Definition(c).ScenarioOnly))
                {
                    var def=Definition(card);
                    var targets=_state.Cards.Values.Where(c=>c.Zone==Zone.Battlefield && c.ProvinceIndex==_state.CurrentBattleProvince && Definition(c).Type==CardType.Personality &&
                        (def.Effect=="force-three"?c.OwnerId==playerId:c.OwnerId!=playerId && PersonalityForce(c)<=3)).Select(c=>c.Id).ToArray();
                    var reason=!Army(playerId,_state.CurrentBattleProvince!.Value).Any()?"You have no unit at the current battlefield.":targets.Length==0?"No legal target at this battlefield.":null;
                    Add("playStrategy",$"Play {def.Name}",reason is null?Command("playStrategy",card.Id,target:targets[0]):null,reason,card.Id,targets);
                }
                Add("pass","Pass battle action",Command("pass"));
                break;
            case Phase.Dynasty:
            case Phase.DynastyDiscard:
                foreach(var card in player.Provinces.Where(p=>p.CardId is not null).Select(p=>Card(p.CardId)).Where(c=>c.FaceUp))
                {
                    var def=Definition(card);
                    if(_state.Phase==Phase.Dynasty)
                    {
                        var modes=def.Type==CardType.Personality && def.Clan==player.Clan ? new[] {(true,false),(false,false),(true,true),(false,true)} : [(false,false)];
                        foreach(var (discount,allegiance) in modes)
                        {
                            var quote=QuotePurchase(playerId,card.Id,discount,allegiance);
                            var payment=quote.Payments.FirstOrDefault();
                            var reason=quote.Error ?? (payment is null?"Insufficient unbowed Gold production.":null);
                            var mode=discount?allegiance?"discount + allegiance":"Clan discount":allegiance?"Clan allegiance":"full cost";
                            Add("purchase",$"Buy {def.Name} · {quote.Cost} Gold{(def.Type==CardType.Personality?$" · {mode}":"")}",
                                reason is null?Command("purchase",card.Id,payment!.SourceIds,discount:discount,allegiance:allegiance):null,reason,card.Id,quote:quote);
                        }
                    }
                    Add("discardProvince",$"Discard {def.Name}",Command("discardProvince",card.Id),cardId:card.Id);
                }
                Add("endDynasty","End Dynasty Phase",Command("endDynasty"));
                break;
        }
        if(_state.PendingChoice is null && _state.Phase is Phase.Action or Phase.Battle) result.AddRange(AbilityActions(playerId));
        Add("concede","Concede",Command("concede"));
        return result.ToArray();
    }
}
