using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private TopCardPanel? _lookPanel;
    private string? _lookViewKey;
    private Window? _lookInspection;
    private bool LookPanelOpen => _lookPanel?.IsVisibleInTree() == true;
    private bool LookDecisionActive => View.WinnerId is null && View.PendingChoice?.Type is "lookDeck" or "lookArrange";
    private void ClearLookPanel()
    {
        if (_lookInspection is not null && IsInstanceValid(_lookInspection)) { _lookInspection.Hide(); _lookInspection.QueueFree(); }
        _lookInspection = null;
        if (_lookPanel is not null) { _lookPanel.CancelPrivateDrag(); _lookPanel.Hide(); _root.RemoveChild(_lookPanel); _lookPanel.QueueFree(); _lookPanel = null; }
        _lookViewKey = null;
    }
    private void FitLookPanel()
    {
        if (_lookPanel is null) return;
        var width = Math.Min(1800, _layoutWidth - 64);
        _lookPanel.Position = new((_layoutWidth - width) / 2, 168); _lookPanel.Size = new(width, 884);
    }
    private bool RenderLookDecision()
    {
        if (!LookDecisionActive || PresentationBusy) { ClearLookPanel(); return false; }
        _guide.Visible = false;
        var pending = View.PendingChoice!;
        if (View.PendingLook is not {} look || pending.PlayerId != View.ViewerId || View.PriorityPlayerId != View.ViewerId)
        {
            ClearLookPanel();
            _sidebarHeader.AddChild(Palette.Text("PRIVATE DECK VIEW", 26, title: true));
            _inspector.AddChild(Palette.Wrapped("Waiting for " + View.Players.Single(p => p.Id == pending.PlayerId).Name + " to finish viewing the deck.", 21));
            _inspector.AddChild(Palette.Wrapped("The cards and their arrangement are private. Any shown card will appear in Shown cards.", 18, Palette.PaperDim));
            if (!_session.Online && !_autoOpponent) _decisionActions.AddChild(SidebarSkin.Button("Pass device to other player", SwitchSeat));
            return true;
        }
        var key = $"{_session.MatchKey}/{View.ViewerId}/{View.Version}/{DecisionCanSubmit}";
        if (_lookViewKey != key)
        {
            ClearLookPanel(); ClearSearchPanel(); ClearCopyPanel(true); HideTableTooltip(); HideShownPreview(); ReleaseTableFocus();
            _lookPanel = new TopCardPanel { ZIndex = 90, ReducedMotion = _reducedMotion, PointerPosition = () => _lastTableMouse }; _root.AddChild(_lookPanel); FitLookPanel();
            _lookPanel.Populate(look, DecisionCanSubmit, View.ViewerId); _lookViewKey = key;
            _lookPanel.Confirmed += tokens =>
            {
                if (!DecisionCanSubmit || key != _lookViewKey || View.PendingLook is null) return;
                var option = Actions.FirstOrDefault(a => a.Enabled && a.Type == "arrangeTopCards" && a.Command is not null);
                if (option?.Command is {} command) Submit(command with { CardIds = tokens });
            };
            _lookPanel.DeckChosen += deck =>
            {
                if (!DecisionCanSubmit || key != _lookViewKey) return;
                var option = Actions.FirstOrDefault(a => a.Enabled && a.Type == "chooseLookDeck" && a.Command?.Choice == deck.Deck && a.Command.TargetPlayerId == deck.OwnerId);
                if (option?.Command is {} command) Submit(command);
            };
            _lookPanel.Inspected += card =>
            {
                if (key != _lookViewKey || View.PendingLook is null) return;
                var (window, body) = Dialog(card.Definition!.Name, 650, 780); _lookInspection = window;
                body.AddChild(CardPreview.Face(card, 610));
            };
        }
        return true;
    }
}
