namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Only this detached view may cross the trust boundary to a player.</summary>
    public MatchView GetView(int playerId)
    {
        if(playerId is <0 or >1) throw new ArgumentOutOfRangeException(nameof(playerId));
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
                player.ProclaimUsedTurn==_state.TurnNumber,player.DishonorLossPending,
                owned.Where(c=>c.Zone==Zone.RemovedFromGame).Select(c=>ViewCard(c)).ToArray(),
                DeadCount(player.Id),
                // Counts are public; identities are private unless already publicly focused from
                // discard or revealed by Strike. Anonymous backs need no stable hidden card IDs.
                owned.Where(c=>c.Zone==Zone.Focused && (player.Id==playerId || c.FaceUp)).Select(c=>ViewCard(c)).ToArray(),
                EventsInPlay:owned.Where(c=>c.Zone==Zone.EventInPlay).Select(c=>ViewCard(c)).ToArray(),
                FocusCount:owned.Count(c=>c.Zone==Zone.Focused));
        }).ToArray();
        var duel=_state.PendingDuel;
        return new(ContentVersion,_scenario,_state.Version,_state.TurnNumber,playerId,_state.ActivePlayerId,_state.PriorityPlayerId,
            _state.Phase,_state.PendingChoice,_state.CurrentBattleProvince,_state.UnresolvedBattlefields.ToArray(),
            _state.WinnerId,_state.VictoryReason,players,_state.Events.Select(e=>e.Type=="duelFocused" && e.PlayerId!=playerId ? e with {CardId=null} : e).ToArray(),_scenario.StartsWith("duel-demo/", StringComparison.Ordinal)?["Prepared Duel Demo position; not a playable deck profile.",DuelDemoScenarios.Get(_scenario[10..]).Description]:_scenario is PublishedContent.Scenario or PublishedContent.TestScenario?["Published Lion/Crane playtest: all 40 definitions supported under the pinned Emperor profile.","Other imported decks remain reference-only; this profile is not tournament certification."]:[..Limits],_state.FavorPlayerId,
            duel is null?null:new DuelView(duel.ChallengerPlayerId,duel.ChallengedPlayerId,duel.ChallengerId,duel.ChallengedId,duel.Stat,duel.Stage,_state.PriorityPlayerId,duel.Struck,duel.DuelId),_state.LastDuel is null?null:Copy(_state.LastDuel));
    }

    private CardView ViewCard(CardInstance card,bool visible=true)
    {
        if(!visible)
            return new($"hidden-p{card.OwnerId}-province{card.ProvinceIndex}",null,card.OwnerId,card.Zone,false,false,card.ProvinceIndex,null,0);
        var def=Definition(card);
        var force=def.Type switch { CardType.Personality=>PersonalityForce(card), CardType.Follower=>FollowerForce(card), _=>Math.Max(0,def.Force+StatModifier(card,"Force")) };
        return new(card.Id,Copy(def),card.OwnerId,card.Zone,card.FaceUp,card.Bowed,card.ProvinceIndex,card.AttachedTo,force,def.Type==CardType.Personality?PersonalityChi(card):def.Chi,card.StraightenBlockedTurn==_state.TurnNumber,card.Modifiers.ToArray(),card.GrantedKeywords.ToArray(),card.Dishonorable,
            def.Type==CardType.Personality && card.Zone is Zone.Home or Zone.Battlefield ? BattleForce(card) : null,card.Tokens.ToArray());
    }

    /// <summary>Returns exact proposed commands plus customization targets. Command validation remains authoritative.</summary>
    public ActionOption[] QueryActions(int playerId)
    {
        if(playerId is <0 or >1) return [];
        if(_state.WinnerId is not null) return [];
        if (_state.PendingDuel is not null || _state.PendingChoice?.Type is "duelWonReaction" or "overconfidentDraw")
            return QueryDuelActions(playerId);
        var result=new List<ActionOption>();
        GameCommand Command(string type,string? id=null,string[]? sources=null,int? province=null,bool discount=false,bool allegiance=false,
            Dictionary<string,int>? assignments=null,string[]? cardIds=null,string? target=null)
            =>new(type,playerId,_state.Version,id,sources,province,discount,allegiance,assignments,cardIds,target);
        void Add(string type,string label,GameCommand? command=null,string? reason=null,string? cardId=null,string[]? targets=null,int[]? provinces=null,PurchaseQuote? quote=null)
            =>result.Add(new(type,label,reason is null,reason,command,cardId,targets,provinces,quote));
        // Order Seppuku is an Open action: available to any Dishonorable Samurai/Courtier/Shugenja
        // the querying player controls during the Action Phase, independent of who holds priority.
        if(_state.Phase==Phase.Action)
            foreach(var card in _state.Cards.Values.Where(c=>c.OwnerId==playerId && InPlay(c) && c.Dishonorable && Definition(c).Type==CardType.Personality &&
                (Keyword(c,"Samurai") || Keyword(c,"Courtier") || Keyword(c,"Shugenja"))))
                Add("orderSeppuku",$"Order Seppuku · {Definition(card).Name}",Command("orderSeppuku",card.Id),cardId:card.Id);
        if(playerId!=_state.PriorityPlayerId)
        {
            Add("wait",$"Waiting for {Player(_state.PriorityPlayerId).Name}",reason:"The other player has the action opportunity.");
            Add("concede","Concede",Command("concede"));
            return result.ToArray();
        }
        var player=Player(playerId);
        if(_state.PendingChoice is { Type:"proclaim" or "entryReaction" } pending)
        {
            if(pending.PlayerId==playerId)
            {
                if(pending.Type=="proclaim")
                {
                    var honor=Definition(Card(pending.CardId)).PersonalHonor;
                    Add("proclaim",$"Proclaim · gain {honor} Honor",Command("proclaim"),player.ProclaimUsedTurn==_state.TurnNumber?"Already used Proclaim this turn.":null,pending.CardId);
                }
                var enteringCard=Card(pending.CardId);
                var reactionId=(Definition(enteringCard).Abilities??[]).FirstOrDefault(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger=="own-enters-play");
                if(reactionId is not null)
                {
                    var used=enteringCard.AbilityUses.ContainsKey(reactionId);
                    Add("useAbility",AbilityCatalog.All[reactionId].Label,
                        new("useAbility",playerId,_state.Version,CardId:enteringCard.Id,AbilityId:reactionId),
                        used?"This reaction has already been used.":null,enteringCard.Id);
                }
                // Singh Remnants' shape: a DIFFERENT card (hand or discard pile) watching this entry,
                // only relevant once a Personality is what entered.
                if(Definition(enteringCard).Type==CardType.Personality)
                    foreach(var (card,watchId) in _state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone is Zone.Hand or Zone.FateDiscard)
                        .SelectMany(c=>(Definition(c).Abilities??[]).Select(a=>(Card:c,Id:a)))
                        .Where(x=>AbilityCatalog.All.TryGetValue(x.Id,out var def) && def.Timing=="Reaction" && def.Trigger=="any-personality-enters-play"))
                        Add("useAbility",AbilityCatalog.All[watchId].Label,
                            new("useAbility",playerId,_state.Version,CardId:card.Id,AbilityId:watchId),
                            cardId:card.Id);
            }
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"attackReaction" })
        {
            // Open to either side -- list this player's own eligible before-attack-resolves Reactions.
            foreach(var (card,reactionId) in _state.Cards.Values.Where(c=>c.OwnerId==playerId && InPlay(c) && !c.Bowed)
                .SelectMany(c=>(Definition(c).Abilities??[]).Select(a=>(Card:c,Id:a)))
                .Where(x=>AbilityCatalog.All.TryGetValue(x.Id,out var def) && def.Timing=="Reaction" && def.Trigger=="before-attack-resolves"))
            {
                var used=card.AbilityUses.ContainsKey(reactionId);
                Add("useAbility",AbilityCatalog.All[reactionId].Label,
                    new("useAbility",playerId,_state.Version,CardId:card.Id,AbilityId:reactionId),
                    used?"This reaction has already been used.":null,card.Id);
            }
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"additionalActionReaction" })
        {
            // Additional Action (issue #181): open to either side, same shape as attackReaction above
            // -- list this player's own eligible before-additional-action-resolves Reactions, played
            // from hand (Effortless Counterattack's shape), not in play.
            foreach(var (card,reactionId) in _state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone==Zone.Hand)
                .SelectMany(c=>(Definition(c).Abilities??[]).Select(a=>(Card:c,Id:a)))
                .Where(x=>AbilityCatalog.All.TryGetValue(x.Id,out var def) && def.Timing=="Reaction" && def.Trigger=="before-additional-action-resolves"))
                Add("useAbility",AbilityCatalog.All[reactionId].Label,
                    new("useAbility",playerId,_state.Version,CardId:card.Id,AbilityId:reactionId),
                    cardId:card.Id);
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"shinden" })
        {
            Add("chooseShinden","Bow and prevent straightening this turn",Command("chooseShinden") with {Choice="bow"});
            Add("chooseShinden","Allow opponent to gain 1 Honor",Command("chooseShinden") with {Choice="honor"});
        }
        else if(_state.PendingChoice is { Type:"conqueror" })
        {
            foreach(var id in _state.PendingConqueror!.CandidateIds)
                Add("chooseConqueror",$"Straighten {Definition(Card(id)).Name} instead of returning home bowed",Command("chooseConqueror",target:id),cardId:id);
        }
        else if(_state.PendingChoice is { Type:"declineStraighten" })
        {
            var optional=EligibleForOptionalStraighten(playerId);
            Add("declineStraighten","Straighten all eligible cards",Command("declineStraighten",cardIds:[]),targets:optional);
            foreach(var id in optional)
                Add("declineStraighten",$"Leave {Definition(Card(id)).Name} bowed",Command("declineStraighten",cardIds:[id]),cardId:id);
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
                        var legalTargets=targets.Where(t=>AttachmentAllowed(card,t) && AttachmentLegalTarget(def,t)).Select(t=>t.Id).ToArray();
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
            case Phase.CavalryDeclaration:
            case Phase.CavalryDefense:
                var cavalrySegment=_state.Phase is Phase.CavalryDeclaration or Phase.CavalryDefense;
                var units=_state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone==Zone.Home && !c.Bowed && Definition(c).Type==CardType.Personality && IsCavalryUnit(c)==cavalrySegment).Select(c=>c.Id).ToArray();
                var provinces=Player(1-_state.ActivePlayerId).Provinces.Where(p=>!p.Destroyed).Select(p=>p.Index).ToArray();
                var attack=_state.Phase is Phase.AttackDeclaration or Phase.CavalryDeclaration;
                var actionType=attack?"attack":"assignDefense";
                foreach(var province in provinces)
                    Add(actionType,$"{(attack?"Attack":"Defend")} province {province+1} · {units.Length} {(cavalrySegment?"Cavalry":"Infantry")} units",
                        Command(actionType,assignments:units.ToDictionary(id=>id,_=>province)),units.Length==0?$"No unbowed {(cavalrySegment?"Cavalry":"Infantry")} Personalities at home.":null,
                        targets:units,provinces:provinces);
                Add("pass",attack?(cavalrySegment?"Skip Cavalry Maneuvers":"Skip attack"):(cavalrySegment?"Assign no Cavalry defenders":"Assign no defenders"),Command("pass"));
                break;
            case Phase.BattleSelection:
                foreach(var province in _state.UnresolvedBattlefields) Add("selectBattle",$"Fight at province {province+1}",Command("selectBattle",province:province));
                break;
            case Phase.Battle:
                foreach(var card in player.Hand.Select(Card).Where(c=>Definition(c).Type==CardType.Strategy && Definition(c).ScenarioOnly))
                {
                    var def=Definition(card);
                    if(Keyword(card,"Terrain"))
                    {
                        var blocked=_state.Cards.Values.Any(c=>c.Zone==Zone.Battlefield && c.ProvinceIndex==_state.CurrentBattleProvince && Keyword(c,"Terrain"));
                        var terrainReason=!Army(playerId,_state.CurrentBattleProvince!.Value).Any()?"You have no unit at the current battlefield."
                            :blocked?"This battlefield already has a Terrain in play.":null;
                        Add("playStrategy",$"Play {def.Name}",terrainReason is null?Command("playStrategy",card.Id):null,terrainReason,card.Id,[]);
                        continue;
                    }
                    var targets=_state.Cards.Values.Where(c=>c.Zone==Zone.Battlefield && c.ProvinceIndex==_state.CurrentBattleProvince && Definition(c).Type==CardType.Personality &&
                        (def.Effect=="force-three"?c.OwnerId==playerId:c.OwnerId!=playerId && PersonalityForce(c)<=3)).Select(c=>c.Id).ToArray();
                    var reason=!Army(playerId,_state.CurrentBattleProvince!.Value).Any()?"You have no unit at the current battlefield.":targets.Length==0?"No legal target at this battlefield.":null;
                    Add("playStrategy",$"Play {def.Name}",reason is null?Command("playStrategy",card.Id,target:targets[0]):null,reason,card.Id,targets);
                }
                // Reserve (issue #184): an absent battle action -- no presence of the recruiting
                // player's own required, only a battlefield at the card's own province with enemy
                // presence there. Offered across every one of the player's own provinces, not just the
                // currently-resolving battlefield.
                foreach(var card in player.Provinces.Where(p=>p.CardId is not null).Select(p=>Card(p.CardId)).Where(c=>c.FaceUp && Definition(c).Type==CardType.Personality && Keyword(c,"Reserve")))
                {
                    var def=Definition(card);
                    var provinceIndex=card.ProvinceIndex!.Value;
                    var eligible=_state.UnresolvedBattlefields.Contains(provinceIndex) && Army(1-playerId,provinceIndex).Any();
                    var modes=HasClanAffiliation(card,player.Clan) ? new[] {(true,false),(false,false),(true,true),(false,true)} : [(false,false)];
                    foreach(var (discount,allegiance) in modes)
                    {
                        var quote=QuotePurchase(playerId,card.Id,discount,allegiance);
                        var payment=quote.Payments.FirstOrDefault();
                        var reason=!eligible?"There is no battlefield here with enemy presence.":quote.Error??(payment is null?"Insufficient unbowed Gold production.":null);
                        var mode=discount?allegiance?"discount + allegiance":"Clan discount":allegiance?"Clan allegiance":"full cost";
                        Add("recruitReserve",$"Recruit {def.Name} from Reserve · {quote.Cost} Gold · {mode}",
                            reason is null?Command("recruitReserve",card.Id,payment!.SourceIds,discount:discount,allegiance:allegiance):null,reason,card.Id);
                    }
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
                        var modes=def.Type==CardType.Personality && HasClanAffiliation(card,player.Clan) ? new[] {(true,false),(false,false),(true,true),(false,true)} : [(false,false)];
                        foreach(var (discount,allegiance) in modes)
                        {
                            var quote=QuotePurchase(playerId,card.Id,discount,allegiance);
                            var payment=quote.Payments.FirstOrDefault();
                            var reason=quote.Error ?? (payment is null?"Insufficient unbowed Gold production.":null);
                            var mode=discount?allegiance?"discount + allegiance":"Clan discount":allegiance?"Clan allegiance":"full cost";
                            Add("purchase",$"Buy {def.Name} · {quote.Cost} Gold{(def.Type==CardType.Personality?$" · {mode}":"")}",
                                reason is null?Command("purchase",card.Id,payment!.SourceIds,discount:discount,allegiance:allegiance):null,reason,card.Id,quote:quote);
                        }
                        if(def.Type==CardType.Personality)
                        {
                            foreach(var target in _state.Cards.Values.Where(c=>c.OwnerId==playerId && InPlay(c) && Definition(c).Type==CardType.Personality
                                && string.Equals(Experienced.BaseTitle(Definition(c).Name),Experienced.BaseTitle(def.Name),StringComparison.OrdinalIgnoreCase)))
                            {
                                var overlay=QuoteOverlay(playerId,card.Id,target.Id);
                                Add("overlay",$"Overlay {def.Name} · Experience {overlay.HigherLevel} over {overlay.LowerLevel} · no Gold",
                                    overlay.Error is null?Command("overlay",card.Id,target:target.Id):null,overlay.Error,card.Id,[target.Id]);
                            }
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
