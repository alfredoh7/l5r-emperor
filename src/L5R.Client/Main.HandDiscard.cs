using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #791: a required hand-limit discard is announced with the ink banner ("DISCARD TO X") and chosen by clicking cards in the hand, with no "choose to discard" step first.
// Clicking a card selects it, clicking a selected card deselects it, and Confirm appears only once exactly the required number are selected. Core still names the required count.
public partial class Main
{
    private readonly List<string> _discardSelected = [];
    private string? _discardAnnouncedKey;

    private bool DiscardActive => _chosen is { Type: "discardHand" };

    private ActionOption? DiscardOffer => View.PendingChoice is { Type: "discardHand" } pending && pending.PlayerId == View.ViewerId
        ? Actions.FirstOrDefault(a => a.Type == "discardHand" && a.Enabled && a.Command is { CardIds.Length: > 0 }) : null;

    /// <summary>The hand size the player is discarding down to.</summary>
    private int DiscardTarget(ActionOption offer) => Self.HandCount - offer.Command!.CardIds!.Length;

    /// <summary>When the player owes a discard, announce it once and start choosing cards directly; nothing is chosen for them.</summary>
    private void BeginDiscardIfPending()
    {
        if (DiscardOffer is not { } offer || _chosen is not null || View.WinnerId is not null) return;
        var key = $"{View.TurnNumber}/{View.PendingChoice!.PlayerId}/{offer.Command!.CardIds!.Length}/{Self.HandCount}";
        if (_discardAnnouncedKey != key) { _discardAnnouncedKey = key; _turnBloom.ReducedMotion = _reducedMotion; _turnBloom.Play("DISCARD TO " + DiscardTarget(offer)); }
        _discardSelected.Clear();
        Choose(offer);
    }

    private bool SelectDiscardCard(CardView card)
    {
        if (_chosen is not { Type: "discardHand", Command: { CardIds: { } required } }) return false;
        if (card.Zone != Zone.Hand || card.OwnerId != View.ViewerId) { SetNotice("Click cards in your hand to choose the ones to discard."); return true; }
        if (!_discardSelected.Remove(card.Id))
        {
            if (_discardSelected.Count >= required.Length) { SetNotice($"You are discarding {required.Length} card(s). Click a selected card to deselect it first."); return true; }
            _discardSelected.Add(card.Id);
        }
        RenderInspector(); Highlight(); return true;
    }

    private void RenderHandDiscard()
    {
        // The offered command already contains the engine's required number of cards. Keep its seat and version and replace only the player's explicit selection.
        var offered = _chosen?.Command;
        if (offered is not { Type: "discardHand", CardIds.Length: > 0 } || offered.PlayerId != View.ViewerId || offered.ExpectedVersion != View.Version)
        {
            _inspector.AddChild(Palette.Wrapped("Waiting for the current discard choice…", 17));
            return;
        }
        var count = offered.CardIds.Length;
        _discardSelected.RemoveAll(id => Self.Hand.All(h => h.Id != id));
        var target = Self.HandCount - count;
        _inspector.AddChild(Palette.Wrapped($"Discard to {target}. Click {count} card{(count == 1 ? "" : "s")} in your hand to choose {(count == 1 ? "it" : "them")}; click a selected card to deselect it.", 17));
        var status = Palette.Wrapped($"{_discardSelected.Count} of {count} selected", 16, _discardSelected.Count == count ? Palette.Gold : Palette.PaperDim);
        status.Name = "DiscardStatus"; _inspector.AddChild(status);
        if (_discardSelected.Count != count) return;
        var confirm = SidebarSkin.Button("Confirm discard", () => _ = Submit(offered with { CardIds = _discardSelected.ToArray() }), true);
        confirm.Name = "ConfirmDiscard"; confirm.Disabled = _session.Busy;
        _decisionActions.AddChild(confirm);
    }
}
