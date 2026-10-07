namespace L5R.Core;

public sealed partial class MatchEngine
{
    // #722: descriptions of windows the engine already opened. Never enumerate either player's hand or call
    // ReactionWindowOffers here: choosing a button label from the opponent's eligibility would reveal it before they act.
    private static (string Title, string Context, string Status)? ReactionMoment(string type) => type switch
    {
        "proclaim" => ("After entry", "Proclaim and entry responses", "The card entered play. Proclaim and any entry Reactions are optional."),
        "entryReaction" or "p3EntryReaction" => ("After entry", "Entry responses", "The card entered play. Any entry Reactions are optional."),
        "attackReaction" => ("Respond to the attack", "The pending attack", "The attack is waiting for responses before it resolves."),
        "beforeActionReaction" or "unimpeachableNameReaction" => ("Respond to the action", "The pending action", "The action is waiting for responses before its effects resolve."),
        "additionalActionReaction" => ("Respond to the additional action", "The pending additional action", "The additional action is waiting for responses before it resolves."),
        "announceReaction" => ("An action was announced", "The announced action", "The announced action is waiting for responses."),
        "engageReaction" => ("Before Battle actions", "Engage responses", "Engage actions and Reactions resolve before the first Battle action."),
        "bowReaction" => ("After a bow", "Responses to the bow", "The bow occurred. Reactions to that bow may now be taken."),
        "moveReaction" => ("After movement", "Responses to the movement", "The movement occurred. Reactions to that movement may now be taken."),
        "nikutaiReaction" => ("After a Follower bowed", "Responses to the Follower's bow", "The Follower bowed. Reactions to that bow may now be taken."),
        "afterResolutionReaction" => ("Battle aftermath", "After-battle responses", "The battle resolved. These responses happen before the winners return home."),
        "attackPhaseEndReaction" => ("At the end of the Attack phase", "End-of-Attack responses", "Reactions to the end of the Attack phase may now be taken."),
        "afterActionResolvedReaction" => ("After the action", "Responses to the resolved action", "The triggering action resolved. Reactions to its resolution may now be taken."),
        "battleActionReaction" => ("After the Battle action", "Responses to the resolved Battle action", "The triggering Battle action resolved. Reactions to its resolution may now be taken."),
        "proclaimNegateReaction" => ("Respond to Proclaim", "The pending Proclaim", "Proclaim is waiting for responses before it resolves."),
        "honorChangeReaction" => ("Before Honor changes", "The pending Honor change", "The action is waiting for responses before its Honor change applies."),
        "battleRecruit" => ("After-battle recruitment", "Recruitment responses", "The battle resolved. Any granted recruitment opportunities remain optional."),
        "beforeBattleReaction" => ("Before the battle resolves", "The next before-resolution effect", "This optional Reaction is one item in the ordered before-resolution effects."),
        "twistOfFateReaction" => ("Before Dynasty ends", "End-of-Dynasty responses", "The Dynasty phase is waiting for responses before it ends."),
        "p3BeforeEventReaction" => ("Respond to the Event", "The pending Event", "The Event is waiting for responses before it resolves."),
        "duelReaction" => ("Before the duel begins", "The pending duel", "The duel is waiting for responses before focusing begins."),
        "duelWonReaction" => ("After the duel", "Responses to the duel result", "The duel resolved. Reactions to its result may now be taken."),
        _ => null
    };

    private ReactionPresentationView? BuildReactionPresentation()
    {
        if (_state.WinnerId is not null) return null;
        var pending = _state.PendingChoice;
        var suspended = _state.SuspendedWindows.FirstOrDefault();
        // Proclaim and replayed Honor changes retain their interrupted choice in dedicated continuations,
        // rather than SuspendedWindows. They still belong to the response round that is waiting behind them.
        var parentChoice = _state.HeldProclaim?.Window ?? suspended?.Choice ?? _state.PendingHonorChange?.PriorChoice;
        var nested = parentChoice is not null;
        var moment = pending is null ? null : ReactionMoment(pending.Type);
        if (pending is { Type: "duelReaction" } && _state.PendingDuel is { Stage: "FocusEffectsReaction" })
            moment = ("Before Focus Effects resolve", "The pending Focus Effects", "Focusing finished. Focus Effects are waiting for responses before they resolve.");
        if (moment is null && (!nested || pending is null && _state.PendingDuel is null)) return null;
        var parent = parentChoice is not null ? ReactionContext(parentChoice,
            suspended?.NegatableAbility ?? suspended?.OutOfPlayActionReaction ?? suspended?.ForcedDiscardContinuation, suspended: true) : null;
        if (moment is null)
            return new("Reaction in progress", ReactionCommandContext(CurrentReactionCommand()) ?? "A choice within the reaction",
                "This choice must finish before the reaction continues. The original response round is waiting.", "", "", parent, true);

        var (title, _, status) = moment.Value;
        var context = ReactionContext(pending!);
        if (nested) status += " The original response round is waiting.";
        var (label, consequence) = ReactionDecline(pending!, nested);
        return new(title, context, status, label, consequence, parent, nested);
    }

    private (string Label, string Consequence) ReactionDecline(PendingChoice pending, bool nested)
    {
        if (pending.Type == "beforeBattleReaction")
            return ("Decline this reaction", "Skips this optional Reaction and continues the ordered before-resolution effects.");
        if (pending.Type == "proclaimNegateReaction")
            return ("Continue Proclaim", "Declines this response and lets the pending Proclaim resolve inside its original entry round.");
        if (pending.Type == "attackPhaseEndReaction")
            return ("Finish Attack phase", "Declines these end-of-Attack responses and completes the Attack phase.");
        if (pending.Type == "twistOfFateReaction")
            return ("Finish Dynasty phase", "Declines these end-of-Dynasty responses and completes the Dynasty phase.");
        if (_state.ConsecutivePasses == 1)
        {
            if (nested) return ("Finish these responses", "Closes this response round. The pending reaction and the original round continue in order.");
            return pending.Type switch
            {
                "attackReaction" => ("Continue resolving attack", "Closes this response round and continues the pending attack, including any remaining choices."),
                "beforeActionReaction" or "additionalActionReaction" or "announceReaction" or "unimpeachableNameReaction" or "honorChangeReaction" or "proclaimNegateReaction"
                    => ("Continue resolving action", "Closes this response round and continues the pending action, including any remaining choices."),
                "duelReaction" when _state.PendingDuel is { Stage: "FocusEffectsReaction" }
                    => ("Continue resolving Focus Effects", "Closes this response round and continues the duel's Focus Effects, including any remaining choices."),
                "duelReaction" => ("Continue to the duel", "Closes this response round and continues the duel."),
                _ => ("Finish responses", "Closes this response round and continues play, including any remaining choices.")
            };
        }
        if (SettledReactionWindows.Contains(pending.Type) && _state.PendingNegatableAbility is null)
            return ("Pass this response", "Declines this opportunity. Any remaining responses happen before play continues.");
        return ("Pass this response", "Declines this opportunity and gives the other player a response opportunity.");
    }

    private GameCommand? CurrentReactionCommand() => _state.PendingHonorChange?.Command ?? _state.PendingAnnouncedAction
        ?? _state.PendingAdditionalAction ?? _state.PendingNegatableAbility ?? _state.PendingOutOfPlayActionReaction
        ?? _state.ForcedDiscardContinuation ?? _state.PendingAttack?.Command;

    private string ReactionContext(PendingChoice pending, GameCommand? held = null, bool suspended = false)
    {
        var fallback = ReactionMoment(pending.Type)?.Context ?? "The original response round";
        if (!suspended && pending.Type == "duelReaction" && _state.PendingDuel is { Stage: "FocusEffectsReaction" })
            return "The pending Focus Effects";
        // These CardIds identify a public trigger subject. A before-battle Reaction can still be in a hidden hand;
        // do not name that source, even for its owner, because this contract is identical for both viewers.
        if (pending.Type is "proclaim" or "entryReaction" or "p3EntryReaction" or "p3BeforeEventReaction" or "duelWonReaction")
            return PublicReactionCardName(pending.CardId) ?? fallback;
        if (pending.Type == "attackReaction" && _state.PendingAttack is { } attack && !suspended)
            return $"{attack.Kind} Attack" + (PublicReactionCardName(attack.TargetId) is { } target ? " · " + target : "");
        if (pending.Type == "proclaimNegateReaction" && _state.HeldProclaim is { } proclaim && !suspended)
            return "Proclaim" + (PublicReactionCardName(proclaim.Window.CardId) is { } card ? " · " + card : "");
        if (pending.Type == "afterActionResolvedReaction" && _state.LastResolvedAction is { } facts && !suspended)
            return ReactionCommandContext(new GameCommand("useAbility", facts.PlayerId, _state.Version,
                CardId: facts.SourceId, AbilityId: facts.AbilityId)) ?? fallback;
        if (pending.Type == "battleActionReaction" && !suspended)
            return PublicReactionCardName(_state.BattleActionReactionPerformerId) is { } performer
                ? "Battle action · " + performer : fallback;
        if (pending.Type is "beforeActionReaction" or "additionalActionReaction" or "announceReaction" or "unimpeachableNameReaction" or "honorChangeReaction")
            return ReactionCommandContext(held ?? (suspended ? null : CurrentReactionCommand())) ?? fallback;
        return fallback;
    }

    private string? ReactionCommandContext(GameCommand? command)
    {
        if (command is null) return null;
        var name = PublicReactionCardName(command.CardId);
        // A card's action label can identify its hidden source just as surely as the card name. Announced sources
        // not yet public therefore use the neutral context; no raw command, private ID, or option label crosses here.
        if (command.CardId is not null && name is null) return null;
        if (command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability))
            return name is null ? ability.Label : name + " · " + ability.Label;
        return name;
    }

    private string? PublicReactionCardName(string? id)
    {
        if (id is null || !_state.Cards.TryGetValue(id, out var card)) return null;
        var visible = card.Zone switch
        {
            Zone.Hand or Zone.DynastyDeck or Zone.FateDeck => false,
            Zone.Province or Zone.Focused or Zone.Underneath => card.FaceUp,
            _ => true
        };
        return visible ? Definition(card).Name : null;
    }
}
