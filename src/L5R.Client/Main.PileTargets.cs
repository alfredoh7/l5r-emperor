using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Issue #965: cards in a discard or dead pile can be the target, candidate, performer or source of an action. The pile on the table is tinted while it holds such a card; opening it marks those cards, and a click on
    // one closes the pile and selects it exactly as a click on a card in play would.

    private bool IsOwnDiscardCard(string? id) => id is not null && FindCard(id) is { Zone: Zone.FateDiscard or Zone.DynastyDiscard } card && card.OwnerId == View.ViewerId;

    /// <summary>A card the player may play: in hand, or in their own discard pile for the actions that are played from there (Scandalous Gossip, Discipline).</summary>
    private bool IsOwnPlayableCard(string? id) => IsOwnHandCard(id) || IsOwnDiscardCard(id);

    /// <summary>Ids of cards the current situation lets the player pick by clicking, whichever zone they are in.</summary>
    private HashSet<string> PileActionableIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (_chosen is not null)
        {
            foreach (var id in _chosen.TargetIds ?? []) ids.Add(id);
            foreach (var id in PerformerClicks().Keys) ids.Add(id);
            foreach (var id in AlternativeCostCards()) ids.Add(id);
            if (MultiTargetActive) foreach (var slot in MultiUpFront.Where(s => s.Slot == _multiActiveSlot)) foreach (var id in slot.CardIds) ids.Add(id);
        }
        else if (GenericDecisionActive)
        {
            foreach (var option in Actions.Where(a => a.Enabled))
            {
                foreach (var id in option.DecisionCandidateIds ?? []) ids.Add(id);
                if (option.Command?.TargetId is { } target) ids.Add(target);
            }
        }
        else if (View.PendingChoice is null && View.WinnerId is null && View.PriorityPlayerId == View.ViewerId && DecisionCanSubmit)
        {
            // An action played from the discard pile: its card is the one to click.
            foreach (var card in View.Players.Single(p => p.Id == View.ViewerId).Discard.Where(c => c.Zone is Zone.FateDiscard or Zone.DynastyDiscard))
                if (PlayChoices(card).Length > 0) ids.Add(card.Id);
        }
        return ids;
    }

    private bool PileCardActionable(CardView card) => PileActionableIds().Contains(card.Id);

    /// <summary>Tints each pile on the table that holds a card the player may click now.</summary>
    private void HighlightPiles()
    {
        var actionable = PileActionableIds();
        foreach (var pile in _pileHud.GetChildren().OfType<TablePile>())
        {
            var holds = pile.PileZone is Zone.FateDiscard or Zone.DynastyDiscard && View.Players.Single(p => p.Id == pile.OwnerId).Discard.Any(c => c.Zone == pile.PileZone && actionable.Contains(c.Id));
            pile.Modulate = holds ? new Color(1.2f, 1.15f, .85f) : Colors.White;
        }
        foreach (var counter in _clanHud.GetChildren().OfType<FallenCounter>())
            counter.Modulate = View.Players.Single(p => p.Id == counter.Player.Id).Discard.Any(c => c.Zone == Zone.Dead && actionable.Contains(c.Id)) ? new Color(1.2f, 1.15f, .85f) : Colors.White;
    }
}
