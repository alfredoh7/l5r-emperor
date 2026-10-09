using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private DeckSearchPanel? _searchPanel;
    private string? _searchViewKey;
    private string? _searchFeedbackMatch;
    private int _searchFeedbackViewer = -1, _searchFeedbackSequence;
    private bool SearchPanelOpen => _searchPanel?.IsVisibleInTree() == true;
    private bool SearchDecisionActive => View.WinnerId is null && View.PendingChoice?.Type is "searchDeck" or "searchSelect" or "searchRegionProvince" or "searchDiscard";

    private void ClearSearchPanel()
    {
        if (_searchPanel is not null)
        {
            _searchPanel.Hide(); _root.RemoveChild(_searchPanel); _searchPanel.QueueFree(); _searchPanel = null;
        }
        _searchViewKey = null;
    }
    private void FitSearchPanel()
    {
        if (_searchPanel is null) return;
        var width = Math.Min(1520, _layoutWidth - 80);
        _searchPanel.Position = new((_layoutWidth - width) / 2, 168); _searchPanel.Size = new(width, 744);
    }
    private bool RenderSearchDecision()
    {
        if (_searchFeedbackMatch != _session.MatchKey || _searchFeedbackViewer != View.ViewerId)
        {
            _searchFeedbackMatch = _session.MatchKey; _searchFeedbackViewer = View.ViewerId;
            _searchFeedbackSequence = View.ShownCards?.LastOrDefault()?.Sequence ?? 0;
        }
        if (View.ShownCards?.LastOrDefault() is { } reveal && reveal.Sequence > _searchFeedbackSequence)
        {
            if (_searchPanel is not null && reveal.ShownByPlayerId == View.ViewerId)
                SetNotice(reveal.Definition.Name + " was shown to your opponent.");
            _searchFeedbackSequence = reveal.Sequence;
        }
        if (!SearchDecisionActive || PresentationBusy) { ClearSearchPanel(); return false; }
        _guide.Visible = false;
        var pending = View.PendingChoice!;
        var search = View.PendingSearch;
        // A public pending-choice prompt is not permission to show any remembered private entries.
        if (search is null || pending.PlayerId != View.ViewerId || View.PriorityPlayerId != View.ViewerId)
        {
            ClearSearchPanel();
            if (_chosen?.Type is "chooseSearchProvince" or "chooseSearchDiscard") { _chosen = null; _sources.Clear(); }
            _sidebarHeader.AddChild(Palette.Text("OPPONENT’S SEARCH", 27, title: true));
            _inspector.AddChild(Palette.Wrapped("Waiting for " + View.Players.Single(p => p.Id == pending.PlayerId).Name + " to complete their search.", 21));
            _inspector.AddChild(Palette.Wrapped("The deck list is private. Any card shown to you will appear in Shown cards.", 18, Palette.PaperDim));
            return true;
        }
        if (search.Stage is SearchStage.ChooseDeck or SearchStage.Select)
        {
            var key = $"{_session.MatchKey}/{View.ViewerId}/{View.Version}/{DecisionCanSubmit}";
            if (_searchViewKey != key)
            {
                ClearSearchPanel(); HideTableTooltip(); HideShownPreview(); ReleaseTableFocus();
                _searchPanel = new DeckSearchPanel { ZIndex = 90 }; _root.AddChild(_searchPanel); FitSearchPanel();
                _searchPanel.MinimumSizeChanged += () => { if (_searchPanel is { } panel) panel.SetDeferred(Control.PropertyName.Size, new Vector2(Math.Min(1520, _layoutWidth - 80), 744)); };
                _searchPanel.Populate(search, Actions, View.ViewerId, DecisionCanSubmit, FindCard(search.SourceCardId)); _searchViewKey = key;
                _searchPanel.Confirmed += action =>
                {
                    // P20: a set of cards is a draft the panel builds from the engine's default "selectSearchCards" action, so it is not itself in Actions.
                    if (!DecisionCanSubmit || _searchViewKey != key || action.Command is null || !(Actions.Contains(action) || action.Type == "selectSearchCards" && Actions.Any(a => a.Type == "selectSearchCards"))) return;
                    _ = Submit(action.Command);
                };
            }
            _sidebarHeader.AddChild(Palette.Text("PRIVATE SEARCH", 27, title: true));
            _inspector.AddChild(Palette.Wrapped(search.SourceName, 23));
            _inspector.AddChild(Palette.Wrapped("Choose in the search panel.", 19, Palette.PaperDim));
            return true;
        }
        ClearSearchPanel();
        var options = Actions.Where(a => a.Type is "chooseSearchProvince" or "chooseSearchDiscard").ToArray();
        var reviewing = _chosen is not null && options.Contains(_chosen);
        _sidebarHeader.AddChild(Palette.Text(search.Stage == SearchStage.ChooseProvince ? "CHOOSE A PROVINCE" : "CHOOSE A DISCARD", 25, title: true));
        if (search.Selected is { } shown)
        {
            _inspector.AddChild(Palette.Wrapped(shown.Name + " was shown to your opponent.", 19, Palette.Gold));
            if (!reviewing) _inspector.AddChild(CardPreview.Face(DeckSearchPanel.Printed(shown, View.ViewerId), 200, 146));
        }
        _inspector.AddChild(Palette.Wrapped(search.Prompt, 19));
        foreach (var option in options.Where(a => !reviewing || a == _chosen))
        {
            var button = SidebarSkin.Button((_chosen == option ? "✓  " : "") + option.Label, () => Choose(option));
            button.Disabled = !option.Enabled || !DecisionCanSubmit; _inspector.AddChild(button);
            if (option.Command?.TargetId is { } id && FindCard(id) is { } card)
                button.TooltipText = card.Definition?.Text ?? option.Label;
        }
        if (_chosen is { } selected && options.Contains(selected) && selected.Command is { } command)
        {
            if (command.TargetId is { } target && FindCard(target) is { } discard)
                _inspector.AddChild(CardPreview.Face(discard, 260, 190));
            var (required, ready) = RenderGoldPanel(selected);
            var version = View.Version; var viewer = View.ViewerId;
            var confirm = SidebarSkin.Button(required > 0 ? $"Confirm · {required} Gold" : "Confirm choice", () =>
            {
                if (DecisionCanSubmit && version == View.Version && viewer == View.ViewerId && Actions.Contains(selected))
                    _ = Submit(command with { SourceIds = required > 0 ? _sources.ToArray() : command.SourceIds });
            }, true);
            confirm.Disabled = !ready || !DecisionCanSubmit; _decisionActions.AddChild(confirm);
            _decisionActions.AddChild(SidebarSkin.Button("Clear selection", CancelChoice));
        }
        else _decisionActions.AddChild(Palette.Wrapped("Select one option, then confirm.", 17, Palette.PaperDim));
        return true;
    }
}
