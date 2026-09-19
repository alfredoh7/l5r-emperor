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
            var force=def.Type switch { CardType.Personality=>PersonalityForce(card), CardType.Follower=>FollowerForce(card), _=>Math.Max(0,def.Force+StatModifier(card,"Force")) };
            return new(card.Id,Copy(def),card.OwnerId,card.Zone,card.FaceUp,card.Bowed,card.ProvinceIndex,card.AttachedTo,force,def.Type==CardType.Personality?PersonalityChi(card):def.Chi,card.StraightenBlockedTurn==_state.TurnNumber,card.Modifiers.ToArray(),card.GrantedKeywords.ToArray(),card.Dishonorable,
                def.Type==CardType.Personality && card.Zone is Zone.Home or Zone.Battlefield ? BattleForce(card) : null,card.Tokens.ToArray(),ActionLocked(card));
        }
        var players=_state.Players.Select(player=>
        {
            var owned=_state.Cards.Values.Where(c=>c.OwnerId==player.Id).ToArray();
            // A card attached to a Personality stands in that Personality's unit, whoever owns it (issue #337: a Follower Moto Jun-Ni attached from the
            // opponent's discard pile; Cultist Infiltrator on an enemy Personality): it is listed with the host's controller so a client that groups a unit
            // from its own player's cards finds it. Its CardView.OwnerId is still its owner's, and it returns to that player's pile when it leaves play.
            var units=_state.Cards.Values.Where(c=>(c.Zone==Zone.Attached && c.AttachedTo is string hostId ? Card(hostId).OwnerId : c.OwnerId)==player.Id).ToArray();
            return new PlayerView(player.Id,player.Name,player.Clan,player.Honor,player.TurnsTaken,
                ViewCard(Card(player.StrongholdId)),
                units.Where(c=>c.Zone==Zone.Home || c.Zone==Zone.Attached && Card(c.AttachedTo).Zone==Zone.Home).Select(c=>ViewCard(c)).ToArray(),
                units.Where(c=>c.Zone==Zone.Battlefield || c.Zone==Zone.Attached && Card(c.AttachedTo).Zone==Zone.Battlefield).Select(c=>ViewCard(c)).ToArray(),
                player.Provinces.Select(p=>new ProvinceView(p.Index,EffectiveProvinceStrength(p),p.Destroyed,
                    p.CardId is null?null:ViewCard(Card(p.CardId),Card(p.CardId).FaceUp),
                    p.RegionId is null?null:ViewCard(Card(p.RegionId)))).ToArray(),
                player.Id==playerId?player.Hand.Select(id=>ViewCard(Card(id))).ToArray():[],player.Hand.Count,
                player.DynastyDeck.Count,player.FateDeck.Count,
                owned.Where(c=>c.Zone is Zone.DynastyDiscard or Zone.FateDiscard or Zone.Dead).Select(c=>ViewCard(c)).ToArray(),
                player.ProclaimUsedTurn==_state.TurnNumber,player.DishonorLossPending,
                owned.Where(c=>c.Zone==Zone.RemovedFromGame).Select(c=>ViewCard(c)).ToArray(),
                DeadCount(player.Id),
                // Dueling (issue #204): a Focused card is private to its own owner until the duel's
                // own Struck reveal -- omitted entirely from an opponent's own view until then (Hand's
                // own convention; a placeholder would need its own stable-but-anonymous id scheme,
                // unlike a face-down province card's ProvinceIndex-keyed one, since a duel's own
                // focused-card list has no comparable positional slot).
                owned.Where(c=>c.Zone==Zone.Focused && (player.Id==playerId || (_state.PendingDuel?.Struck ?? false))).Select(c=>ViewCard(c)).ToArray(),
                owned.Where(c=>c.Zone==Zone.EventInPlay).Select(c=>ViewCard(c)).ToArray(),
                owned.Where(c=>c.Zone==Zone.Celestial).Select(c=>ViewCard(c)).ToArray());
        }).ToArray();
        var duel=_state.PendingDuel;
        return new(ContentVersion,_scenario,_state.Version,_state.TurnNumber,playerId,_state.ActivePlayerId,_state.PriorityPlayerId,
            _state.Phase,_state.PendingChoice,_state.CurrentBattleProvince,_state.UnresolvedBattlefields.ToArray(),
            _state.WinnerId,_state.VictoryReason,players,_state.Events.ToArray(),_scenario is PublishedContent.Scenario or PublishedContent.TestScenario?["Published Lion/Crane playtest: all 40 definitions supported under the pinned Emperor profile.","Other imported decks remain reference-only; this profile is not tournament certification."]:[..Limits],_state.FavorPlayerId,
            duel is null?null:new DuelView(duel.ChallengerPlayerId,duel.ChallengedPlayerId,duel.ChallengerId,duel.ChallengedId,duel.Stat,duel.Stage,duel.TurnPlayerId,duel.Struck),
            BuildBattleStepView(),
            // Deck search (issue #312): the deck viewer is private to the searcher; the shown-cards record is public.
            BuildSearchView(playerId),BuildShownCards(),BuildLookView(playerId));
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
        void Add(string type,string label,GameCommand? command=null,string? reason=null,string? cardId=null,string[]? targets=null,int[]? provinces=null,PurchaseQuote? quote=null,
            GoldQuote? gold=null,Dictionary<string,GoldQuote>? targetGold=null)
            =>result.Add(new(type,label,reason is null,reason,command,cardId,targets,provinces,quote,gold,targetGold));
        // A Reaction offered inside a reaction window (an Invest, Singh Remnants' attach, ...): same one Gold
        // quote as every other costed action (issue #303), so the client shows the payment panel and the
        // command carries the engine's suggested sources. An unpayable cost is offered disabled with its
        // reason instead of as an action that could only be rejected.
        // validate (Akodo Kakihara, issue #305): a reaction with a printed condition of its own (only when HE is
        // the target) is listed disabled, with ValidateAbility's own reason, while that condition does not
        // hold -- checked against the command AS QUOTED, so a payment the engine just suggested is never itself
        // the reason. targetId/label serve a reaction whose own Target is a choice (Champion of Thunder's
        // "own-unit-member": one option per unit member); both default to the plain single-option shape.
        void AddReaction(CardInstance card,string abilityId,string? reason=null,string? targetId=null,string? label=null,bool validate=false)
        {
            var command=new GameCommand("useAbility",playerId,_state.Version,CardId:card.Id,TargetId:targetId,AbilityId:abilityId);
            var gold=QuoteAbilityGold(command);
            if(gold is not null) command=command with {SourceIds=gold.Payments.FirstOrDefault()?.SourceIds};
            reason??=gold is null ? null : gold.Error ?? (gold.Payments.Length==0 ? "Insufficient unbowed Gold production." : null);
            if(reason is null && validate) reason=AbilityRejection(command);
            // "May not perform actions" (issue #336): a Reaction is an action, so a locked performer is listed disabled with the reason.
            reason??=ActionLockRejection(command);
            Add("useAbility",label??AbilityCatalog.All[abilityId].Label,command,reason,card.Id,gold:gold);
        }
        // Order Seppuku is an Open action: available to any Dishonorable Samurai/Courtier/Shugenja
        // the querying player controls during the Action Phase, independent of who holds priority.
        if(_state.Phase==Phase.Action && _state.PendingSearch is null && _state.PendingLook is null)
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
                    AddReaction(enteringCard,reactionId,used?"This reaction has already been used.":null);
                }
                // Singh Remnants' shape: a DIFFERENT card (hand or discard pile) watching this entry,
                // only relevant once a Personality is what entered.
                if(Definition(enteringCard).Type==CardType.Personality)
                    foreach(var (card,watchId) in _state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone is Zone.Hand or Zone.FateDiscard)
                        .SelectMany(c=>(Definition(c).Abilities??[]).Select(a=>(Card:c,Id:a)))
                        .Where(x=>AbilityCatalog.All.TryGetValue(x.Id,out var def) && def.Timing=="Reaction" && def.Trigger=="any-personality-enters-play"))
                        AddReaction(card,watchId);
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
                // Akodo Kakihara (issue #305): an in-play Attack reactor may carry a printed condition of its own
                // (only when HE is the target), so a listed reaction is only enabled while ValidateAbility
                // actually accepts it -- otherwise every unrelated Attack would offer a button that then errors.
                AddReaction(card,reactionId,used?"This reaction has already been used.":null,validate:true);
            }
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"beforeActionReaction" } beforeActionPending)
        {
            // Rokai / Tamoko / Champion of Thunder / Brothers of Goemon / Akodo Kakihara, and any future watcher:
            // the general in-play "before an action resolves" window is an alternating window open to EITHER
            // player (issue #318), so whoever holds priority is offered every reaction of THEIR OWN that applies
            // to the deferred action right now, from any number of cards, plus Pass. A reaction whose own
            // Target is a choice among the watcher's unit (Champion's "own-unit-member") gets one option per
            // member; every other one is offered once. Each is validated exactly like a submitted command, so a
            // reaction that cannot legally be used right now (spent, bowed) is listed disabled with its reason
            // rather than hidden or offered blind. Recomputed on every query, so what a played reaction changed
            // (a bowed or destroyed source, a spent use) is already reflected at the next opportunity.
            if(_state.PendingNegatableAbility is { } deferredAction)
                foreach(var (card,reactionId) in BeforeActionReactionCandidates().Where(x=>x.Card.OwnerId==playerId && BeforeActionReactionApplies(x.Card,x.AbilityId,deferredAction)).ToArray())
                {
                    var reaction=AbilityCatalog.All[reactionId];
                    string?[] candidates=reaction.Target=="own-unit-member"?UnitMembers(card).Select(c=>(string?)c.Id).ToArray():[null];
                    foreach(var candidate in candidates)
                        AddReaction(card,reactionId,targetId:candidate,label:candidate is null?null:$"{reaction.Label} · {Definition(Card(candidate)).Name} ({candidate})",validate:true);
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
                AddReaction(card,reactionId);
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"battleActionReaction" } battleActionPending)
        {
            // Stormless Fury (issue #276 follow-up): scoped to the one specific offered card by id,
            // same single-watcher shape as entryReaction/bowReaction above.
            if(battleActionPending.PlayerId==playerId)
            {
                var card=Card(battleActionPending.CardId!);
                foreach(var reactionId in (Definition(card).Abilities??[]).Where(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger=="after-battle-action-resolves"))
                    AddReaction(card,reactionId);
            }
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"announceReaction" } announcePending)
        {
            // Tsuruchi Yakusho (issue #276 follow-up): scoped to the one specific offered card by id.
            if(announcePending.PlayerId==playerId)
            {
                var card=Card(announcePending.CardId!);
                foreach(var reactionId in (Definition(card).Abilities??[]).Where(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger=="own-ranged-action-announced"))
                    AddReaction(card,reactionId);
            }
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"engageReaction" })
        {
            // Battle Phase timing (issue #310, spec 4.2): every legal "Engage:" action and "After engaging" Reaction this
            // player holds (in play, or a Strategy in hand, or Naval Invasion), then the pass that ends their opportunity.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"beforeBattleOptional" } optionalPending)
        {
            // Before This Battle Resolves, an optional ("you may") effect: decline it, or name its one target.
            Add("beforeBattleOptional","Decline",Command("beforeBattleOptional") with {Choice="decline"},cardId:optionalPending.CardId);
            foreach(var target in Army(1-playerId,_state.CurrentBattleProvince!.Value))
                Add("beforeBattleOptional",$"Bow {Definition(target).Name}",Command("beforeBattleOptional",target:target.Id) with {Choice="use"},cardId:target.Id);
        }
        else if(_state.PendingChoice is { Type:"beforeBattleOrder" })
        {
            // The active player orders the simultaneous "before this battle resolves" effects (issue #272/#310).
            foreach(var id in _state.PendingBeforeBattleResolve!.RemainingSourceIds)
                Add("beforeBattleOrder",$"Resolve {Definition(Card(id)).Name} next",Command("beforeBattleOrder",target:id),cardId:id);
        }
        else if(_state.PendingChoice is { Type:"encircledOrder" })
        {
            Add("encircledOrder","The Attacker selects a Personality first",Command("encircledOrder") with {Choice="attacker"});
            Add("encircledOrder","The Defender selects a Personality first",Command("encircledOrder") with {Choice="defender"});
        }
        else if(_state.PendingChoice is { Type:"encircledSelect" })
        {
            foreach(var card in Army(playerId,_state.PendingBeforeBattleResolve!.ProvinceIndex))
                Add("encircledSelect",$"Keep {Definition(card).Name} at this battlefield",Command("encircledSelect",target:card.Id),cardId:card.Id);
        }
        else if(_state.PendingChoice is { Type:"shinden" })
        {
            Add("chooseShinden","Bow and prevent straightening this turn",Command("chooseShinden") with {Choice="bow"});
            Add("chooseShinden","Allow opponent to gain 1 Honor",Command("chooseShinden") with {Choice="honor"});
        }
        else if(_state.PendingChoice is { Type:"preservingHonor" })
        {
            Add("choosePreservingHonor","Bow him",Command("choosePreservingHonor") with {Choice="bow"});
            Add("choosePreservingHonor","Move him home",Command("choosePreservingHonor") with {Choice="move"});
        }
        else if(_state.PendingChoice is { Type:"rikateDetach" } rikatePending)
        {
            // Asako Rikate (issue #301): any subset of the unit's attachments may go to hand. Offered:
            // keep them all, return them all, or return exactly one (every subset is legal to submit
            // directly through CardIds; these are the choices a client can enumerate without a combinatorial
            // explosion, the same convention declineStraighten's own single-card options follow).
            var attachments=Attachments(rikatePending.CardId!).Select(a=>a.Id).ToArray();
            Add("chooseRikateDetach","Keep every attachment",Command("chooseRikateDetach",cardIds:[]),targets:attachments);
            Add("chooseRikateDetach","Put every attachment into your hand",Command("chooseRikateDetach",cardIds:attachments),targets:attachments);
            if(attachments.Length>1)
                foreach(var id in attachments)
                    Add("chooseRikateDetach",$"Put only {Definition(Card(id)).Name} into your hand",Command("chooseRikateDetach",cardIds:[id]),cardId:id);
        }
        else if(_state.PendingChoice is { Type:"rinShame" })
        {
            Add("chooseRinShame","Dishonor him",Command("chooseRinShame") with {Choice="dishonor"});
            Add("chooseRinShame","Refuse: he is bowed and cannot straighten this turn",Command("chooseRinShame") with {Choice="decline"});
        }
        else if(_state.PendingChoice is { Type:"yourClanNeedsYouProvince" })
            foreach(var p in Player(playerId).Provinces.Where(p=>!p.Destroyed))
                Add("chooseYourClanNeedsYouProvince",$"Place into province {p.Index+1}"+(p.CardId is not null?$" (discarding {Definition(Card(p.CardId)).Name})":""),
                    Command("chooseYourClanNeedsYouProvince",province:p.Index));
        else if(_state.PendingChoice is { Type:"conqueror" })
        {
            foreach(var id in _state.PendingConqueror!.CandidateIds)
                Add("chooseConqueror",$"Straighten {Definition(Card(id)).Name} instead of returning home bowed",Command("chooseConqueror",target:id),cardId:id);
        }
        else if(_state.PendingChoice is { } lookPending && IsLookChoice(lookPending.Type))
        {
            // Look at the top N cards (issue #322): the viewer always has a legal answer (see LookActions).
            result.AddRange(LookActions(playerId));
        }
        else if(_state.PendingChoice is { } searchPending && IsSearchChoice(searchPending.Type))
        {
            // Deck search (issue #312): the searcher always has a legal answer (see SearchActions), so no bot or
            // legal-actions-driven client can stall in a search.
            result.AddRange(SearchActions(playerId));
        }
        else if(_state.PendingChoice is { Type:"declineStraighten" })
        {
            var optional=EligibleForOptionalStraighten(playerId);
            Add("declineStraighten","Straighten all eligible cards",Command("declineStraighten",cardIds:[]),targets:optional);
            foreach(var id in optional)
                Add("declineStraighten",$"Leave {Definition(Card(id)).Name} bowed",Command("declineStraighten",cardIds:[id]),cardId:id);
        }
        else if(_state.PendingChoice is { Type:"forcedDiscard" })
        {
            // A discard owed (Times of Strife, Daigotsu Bofana): one option per card of the discarding player's own hand -- only
            // they hold priority here, so the other player is told to wait and never sees these -- except a card the announced
            // action itself uses. Nothing to pick means no choice was ever opened (see OpenOverconfidentDrawWindow).
            foreach(var id in player.Hand.Where(id=>_state.ForcedDiscardContinuation is not { } held || !CommandNamesCard(held,id)))
                Add("forcedDiscard",$"Discard {Definition(Card(id)).Name}",Command("forcedDiscard",target:id),cardId:id);
        }
        else if(_state.PendingChoice is { Type:"effectChoice" })
        {
            // Conditional effect choice (issue #337): one option per effect the rules let the acting player pick (see EffectChoice.cs).
            // Only they hold priority here, so the other player is told to wait and is offered nothing.
            result.AddRange(EffectChoiceActions(playerId));
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
                        // One Gold quote per candidate wielder (issue #303): Katagi's discount depends on the
                        // wielder, so the price can differ per target and the client swaps quotes on retarget.
                        var equipQuotes=legalTargets.ToDictionary(id=>id,id=>QuoteEquipGold(playerId,card,Card(id)));
                        var affordable=legalTargets.Where(id=>equipQuotes[id].Payments.Length>0).ToArray();
                        var reason=legalTargets.Length==0?"No Personality meets the attachment requirements.":affordable.Length==0?"Insufficient unbowed Gold production.":null;
                        var firstTarget=affordable.FirstOrDefault();
                        var firstQuote=firstTarget is null?null:equipQuotes[firstTarget];
                        Add("equip",$"Equip {def.Name} · {firstQuote?.Cost??def.GoldCost} Gold",reason is null?Command("equip",card.Id,firstQuote!.Payments[0].SourceIds,target:firstTarget):null,
                            reason,card.Id,reason is null?affordable:legalTargets,gold:firstQuote,
                            targetGold:affordable.Any(id=>equipQuotes[id].Kind==GoldCostKind.Derived)?affordable.ToDictionary(id=>id,id=>equipQuotes[id]):null);
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
                    // A Strategy played with playStrategy pays its printed cost (issue #303): one quote, with
                    // the engine's suggested sources pre-filled, and an unpayable cost is a disabled option.
                    var strategyGold=QuoteStrategyGold(playerId,card);
                    var strategyPay=strategyGold?.Payments.FirstOrDefault()?.SourceIds;
                    var strategyReason=strategyGold is not null && strategyGold.Payments.Length==0?"Insufficient unbowed Gold production.":null;
                    if(Keyword(card,"Terrain"))
                    {
                        var blocked=_state.Cards.Values.Any(c=>c.Zone==Zone.Battlefield && c.ProvinceIndex==_state.CurrentBattleProvince && Keyword(c,"Terrain"));
                        var terrainReason=!Army(playerId,_state.CurrentBattleProvince!.Value).Any()?"You have no unit at the current battlefield."
                            :blocked?"This battlefield already has a Terrain in play.":strategyReason;
                        Add("playStrategy",$"Play {def.Name}",terrainReason is null?Command("playStrategy",card.Id,strategyPay):null,terrainReason,card.Id,[],gold:strategyGold);
                        continue;
                    }
                    var targets=_state.Cards.Values.Where(c=>c.Zone==Zone.Battlefield && c.ProvinceIndex==_state.CurrentBattleProvince && Definition(c).Type==CardType.Personality &&
                        (def.Effect=="force-three"?c.OwnerId==playerId:c.OwnerId!=playerId && PersonalityForce(c)<=3)).Select(c=>c.Id).ToArray();
                    var reason=!Army(playerId,_state.CurrentBattleProvince!.Value).Any()?"You have no unit at the current battlefield.":targets.Length==0?"No legal target at this battlefield.":strategyReason;
                    Add("playStrategy",$"Play {def.Name}",reason is null?Command("playStrategy",card.Id,strategyPay,target:targets[0]):null,reason,card.Id,targets,gold:strategyGold);
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
                            reason is null?Command("recruitReserve",card.Id,payment!.SourceIds,discount:discount,allegiance:allegiance):null,reason,card.Id,quote:quote,gold:ToGoldQuote(quote));
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
                                reason is null?Command("purchase",card.Id,payment!.SourceIds,discount:discount,allegiance:allegiance):null,reason,card.Id,quote:quote,gold:ToGoldQuote(quote));
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
