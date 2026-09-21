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
            return new(card.Id,Copy(def),card.OwnerId,card.Zone,card.FaceUp,card.Bowed,card.ProvinceIndex,card.AttachedTo,force,def.Type==CardType.Personality?PersonalityChi(card):def.Chi,card.StraightenBlockedTurn==_state.TurnNumber || StraightenRefusedInView(card),card.Modifiers.ToArray(),card.GrantedKeywords.ToArray(),card.Dishonorable,
                def.Type==CardType.Personality && card.Zone is Zone.Home or Zone.Battlefield ? BattleForce(card) : null,card.Tokens.ToArray(),
                GrantedAbilityViews(card),
                GoldSourceProduction(card),ActionLocked(card),card.CreatedCopy);
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
            BuildSearchView(playerId),BuildShownCards(),BuildLookView(playerId),BuildCopyView(playerId),BuildEitherOrView(playerId),BuildOpponentPaymentView(playerId));
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
            // "Choose a player" (issue #389): the player is a command parameter (TargetPlayerId), not a card, so a Reaction whose Target is a player is offered
            // once per legal player -- either player (you, or your opponent), each carrying the id the command needs -- and validated as quoted.
            var chosenPlayers=AbilityCatalog.All[abilityId].Target=="player" ? new int?[]{playerId,1-playerId} : new int?[]{null};
            foreach(var chosenPlayer in chosenPlayers)
            {
                var command=new GameCommand("useAbility",playerId,_state.Version,CardId:card.Id,TargetId:targetId,TargetPlayerId:chosenPlayer,AbilityId:abilityId);
                var gold=QuoteAbilityGold(command);
                if(gold is not null) command=command with {SourceIds=gold.Payments.FirstOrDefault()?.SourceIds};
                var why=reason ?? (gold is null ? null : gold.Error ?? (gold.Payments.Length==0 ? "Insufficient unbowed Gold production." : null));
                if(why is null && (validate || chosenPlayer is not null)) why=AbilityRejection(command);
                // "May not perform actions" (issue #336): a Reaction is an action, so a locked performer is listed disabled with the reason.
                why??=ActionLockRejection(command);
                var text=label??AbilityOptionLabel(abilityId,AbilityCatalog.All[abilityId],null,card);
                if(chosenPlayer is int who) text+=who==playerId ? " · you" : " · your opponent";
                Add("useAbility",text,command,why,card.Id,gold:gold);
            }
        }
        // Targeted-by-action reactions (issue #335): one option per legal new target for Blade of Champions, else one; unusable ones are listed disabled.
        void AddTargetedActionReactions()
        {
            if(OpenReactionSubject() is not { } held) return;
            foreach(var (card,reactionId) in TargetedActionReactionCandidates().Where(x=>x.Card.OwnerId==playerId && TargetedActionReactionApplies(x.Card,x.AbilityId,held)).ToArray())
                foreach(var (candidate,label) in TargetedReactionChoices(card,reactionId,held))
                    AddReaction(card,reactionId,targetId:candidate,label:label,validate:true);
        }
        // Order Seppuku is an Open action (issue #368): offered for any Dishonorable Samurai/Courtier/Shugenja the querying player controls, but
        // ONLY at their own turn to act in the Action Phase with nothing pending -- the same single predicate the command handler uses
        // (SeppukuTiming.cs), so an offered Seppuku is never one the engine would refuse for its timing.
        if(SeppukuTimingReason(playerId) is null)
            foreach(var card in _state.Cards.Values.Where(c=>c.OwnerId==playerId && InPlay(c) && c.Dishonorable && Definition(c).Type==CardType.Personality &&
                (Keyword(c,"Samurai") || Keyword(c,"Courtier") || Keyword(c,"Shugenja"))))
                Add("orderSeppuku",$"Order Seppuku · {Definition(card).Name}",OwesForcedPass(playerId)?null:Command("orderSeppuku",card.Id),
                    OwesForcedPass(playerId)?ForcedPassReason:ActionLocked(card)?$"{Definition(card).Name} may not perform actions this turn.":null,card.Id);
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
                // Every "own enters play" Reaction the card prints (issue #389: Horume's gain AND lose; before, only the first was listed). One that shares a
                // once-per-window usage key with another (Horume's "gain or lose") is used once for the pair.
                foreach(var reactionId in (Definition(enteringCard).Abilities??[]).Where(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger=="own-enters-play"))
                {
                    var used=enteringCard.AbilityUses.ContainsKey(AbilityCatalog.All[reactionId].UsageKey??reactionId);
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
                .SelectMany(c=>AbilityIdsOf(c).Select(a=>(Card:c,Id:a)))
                .Where(x=>AbilityCatalog.All.TryGetValue(x.Id,out var def) && def.Timing=="Reaction" && def.Trigger=="before-attack-resolves"))
            {
                var reaction=AbilityCatalog.All[reactionId];
                var used=AbilityUseSpent(card.AbilityUses,reaction,reaction.UsageKey??reactionId);
                // Akodo Kakihara (issue #305): an in-play Attack reactor may carry a printed condition of its own
                // (only when HE is the target), so a listed reaction is only enabled while ValidateAbility
                // actually accepts it -- otherwise every unrelated Attack would offer a button that then errors.
                AddReaction(card,reactionId,used?"This reaction has already been used.":null,validate:true);
            }
            AddTargetedActionReactions();
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
                    string?[] candidates=reaction.Target=="own-unit-member"?UnitMembers(card).Select(c=>(string?)c.Id).ToArray()
                        // Issue #336 fixture: one option per Personality the deferred action would move.
                        :reaction.Target=="deferred-action-mover"?ActionMovers(deferredAction).Select(c=>(string?)c).ToArray():[null];
                    foreach(var candidate in candidates)
                        AddReaction(card,reactionId,targetId:candidate,label:candidate is null?null:$"{reaction.Label} · {Definition(Card(candidate)).Name} ({candidate})",validate:true);
                }
            AddTargetedActionReactions();
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"additionalActionReaction" })
        {
            // Additional Action (issue #181): open to either side, same shape as attackReaction above
            // -- list this player's own eligible before-additional-action-resolves Reactions, played
            // from where they can be (Effortless Counterattack's shape: hand, not in play; a Discipline one
            // also from a discard pile), the same list whose existence opened this window.
            foreach(var (card,reactionId) in OutOfPlayReactions(playerId,"before-additional-action-resolves"))
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
                foreach(var reactionId in AbilityIdsOf(card).Where(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger=="after-battle-action-resolves"))
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
                foreach(var reactionId in AbilityIdsOf(card).Where(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger=="own-ranged-action-announced"))
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
        else if(_state.PendingChoice is { Type:"bowReaction" } bowPending)
        {
            // After an action bowed a Personality (issue #216 Hizumi and Ruya, issue #417 Quagmire): the one watching card's own Reaction, one option per Personality it may target
            // when its target is one of those the action bowed, else the plain option; then the pass.
            if(bowPending.PlayerId==playerId && bowPending.CardId is not null)
            {
                var watcher=Card(bowPending.CardId);
                foreach(var reactionId in AbilityIdsOf(watcher).Where(a=>AbilityCatalog.All.TryGetValue(a,out var def) && def.Timing=="Reaction" && def.Trigger is "bowed-personality-at-own-battlefield" or "bowed-personality-near-region" or "own-action-bowed-personality"))
                {
                    var used=AbilityUseSpent(watcher.AbilityUses,AbilityCatalog.All[reactionId],AbilityCatalog.All[reactionId].UsageKey??reactionId);
                    if(AbilityCatalog.All[reactionId].Target=="bow-reaction-candidate")
                        foreach(var candidateId in _state.BowReactionCandidateIds)
                            AddReaction(watcher,reactionId,used?"This reaction has already been used.":null,targetId:candidateId,label:$"{AbilityCatalog.All[reactionId].Label} · {Definition(Card(candidateId)).Name} ({candidateId})",validate:true);
                    else AddReaction(watcher,reactionId,used?"This reaction has already been used.":null);
                }
            }
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"afterResolutionReaction" })
        {
            // After a battle's resolution (issue #417): this player's Reactions that name it, from a hand, a discard pile or play, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"battleRecruit" })
        {
            // After a battle resolved (issue #379): the recruit grants of this battle this player still holds, then the pass.
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
        else if(_state.PendingChoice is { Type:"beforeBattleReaction" })
        {
            // A printed "Reaction: Before a battle resolves" (Steady Resolve, Akodo Dairuko): its owner's plays of it, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction","Pass reaction",Command("declineReaction"));
        }
        else if(_state.PendingChoice is { Type:"beforeBattleOrder" })
        {
            // The active player orders the simultaneous "before this battle resolves" effects (issue #272/#310).
            foreach(var id in _state.PendingBeforeBattleResolve!.RemainingSourceIds)
                Add("beforeBattleOrder",$"Resolve {BeforeResolutionItemLabel(id)} next",Command("beforeBattleOrder",target:id),cardId:_state.Cards.ContainsKey(id)?id:null);
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
        else if(_state.PendingChoice is { } copyPending && IsCopyChoice(copyPending.Type))
        {
            // Copying a keyword or an ability (issue #338): one option per candidate; the player always has a legal answer.
            result.AddRange(CopyActions(playerId));
        }
        else if(_state.PendingChoice is { Type:"eitherOr" })
        {
            // The opposing player's Either/Or choice (issue #338): one option per option the engine offers, the unavailable ones
            // listed disabled with the reason.
            result.AddRange(EitherOrActions(playerId));
        }
        else if(_state.PendingChoice is { Type:"opponentPayment" })
        {
            // The opposing player's variable payment (issue #373): one action carrying the variable quote; the paying player always has the legal answer of paying nothing.
            result.AddRange(OpponentPaymentActions(playerId));
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
                // Equip (Limited) on the active player's own turn, and -- with a player-level timing grant (Foothills Keep, TimingGrants.cs) -- an
                // Open equip of a Weapon in ANY player's action segment. The per-attachment timing is decided in EquipActions.
                result.AddRange(EquipActions(playerId));
                Add("pass","Pass action",Command("pass"));
                break;
            case Phase.AttackDeclaration:
                Add("declareAttack","Declare an attack",Command("declareAttack"),player.MayNotAttackThisTurn?"You may not declare an attack this turn.":null);
                Add("pass","Decline to attack",Command("pass"));
                break;
            case Phase.InfantryDeclaration:
            case Phase.DefenseAssignment:
            case Phase.CavalryDeclaration:
            case Phase.CavalryDefense:
                var cavalrySegment=_state.Phase is Phase.CavalryDeclaration or Phase.CavalryDefense;
                var units=_state.Cards.Values.Where(c=>c.OwnerId==playerId && c.Zone==Zone.Home && !c.Bowed && Definition(c).Type==CardType.Personality && IsCavalryUnit(c)==cavalrySegment).Select(c=>c.Id).ToArray();
                var provinces=Player(1-_state.ActivePlayerId).Provinces.Where(p=>!p.Destroyed).Select(p=>p.Index).ToArray();
                var attack=_state.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration;
                var actionType=attack?"attack":"assignDefense";
                foreach(var province in provinces)
                    Add(actionType,$"{(attack?"Attack":"Defend")} province {province+1} · {units.Length} {(cavalrySegment?"Cavalry":"Infantry")} units",
                        Command(actionType,assignments:units.ToDictionary(id=>id,_=>province)),units.Length==0?$"No unbowed {(cavalrySegment?"Cavalry":"Infantry")} Personalities at home.":null,
                        targets:units,provinces:provinces);
                Add("pass",attack?(cavalrySegment?"Assign no Cavalry attackers":"Assign no Infantry attackers"):(cavalrySegment?"Assign no Cavalry defenders":"Assign no defenders"),Command("pass"));
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
                // A Battle-timing equip granted by a card in play (Foothills Keep, TimingGrants.cs): a Weapon from hand to a Personality at the battlefield.
                result.AddRange(EquipActions(playerId));
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
        // Unfortunate Hesitation (issue #338): what a player who owes a pass cannot do at this opportunity is listed disabled.
        return ApplyForcedPassToOptions(playerId,result);
    }
}
