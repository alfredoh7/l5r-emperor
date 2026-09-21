using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private EventDecisionSidebar _eventSidebar=null!;
    private EventDecisionHandBar _eventHandBar=null!;
    private string? _eventDiscardId;
    private long _eventSelectionVersion=-1;
    private bool EventDecisionActive=>View.WinnerId is null && (View.PendingChoice?.Type is "harshChoices" or "overconfidentDraw" or "brashDraw" || View.PendingChoice is {Type:"forcedDiscard",CardId:null} discard && discard.Prompt.StartsWith("Times of Strife:",StringComparison.Ordinal));
    private bool OwnEventDecision=>EventDecisionActive && View.PendingChoice!.PlayerId==View.ViewerId && View.PriorityPlayerId==View.ViewerId;
    private bool CanCommitEvent=>_session.Snapshot is not null && !_session.Busy && !PresentationBusy && (!_session.Online || _session.Connected && !(_session.Snapshot?.PlayersConnected?.Any(p=>!p)??false));
    private ActionOption? SelectedEventDiscard=>OwnEventDecision?Actions.FirstOrDefault(a=>a.Enabled && a.Type=="forcedDiscard" && a.Command?.TargetId==_eventDiscardId):null;
    private void BuildEventDecisions()
    {
        _eventSidebar=new EventDecisionSidebar {Visible=false};_root.AddChild(_eventSidebar);
        _eventHandBar=new EventDecisionHandBar {Visible=false};_root.AddChild(_eventHandBar);
    }
    private void FitEventDecisions()
    {
        if(_eventSidebar is null)return;
        _eventSidebar.Position=_inspectorPanel.Position;_eventSidebar.Size=_inspectorPanel.Size;
        _eventHandBar.Position=new(_hand.Position.X,_hand.Position.Y-143);
        _eventHandBar.Size=new(_hand.Size.X,122);
    }
    private bool RenderEventDecision()
    {
        var active=EventDecisionActive&&!PresentationBusy;
        _inspectorPanel.Visible=!active;_eventSidebar.Visible=active;
        _eventHandBar.Visible=active && OwnEventDecision && View.PendingChoice!.Type=="forcedDiscard";
        if(_eventSelectionVersion!=View.Version || !OwnEventDecision)
        { _eventDiscardId=null;_eventSelectionVersion=View.Version; }
        if(!active){_eventDiscardId=null;return false;}
        _chosen=null;_sources.Clear();_guide.Visible=false;
        var pending=View.PendingChoice!;
        CardView? source=pending.CardId is { } id?FindCard(id):null;
        if(pending.Type=="forcedDiscard")
            source=View.Players.SelectMany(p=>p.EventsInPlay??[]).FirstOrDefault(c=>c.OwnerId!=pending.PlayerId&&c.Definition?.Id=="oracle-l5r-8494")
                ?? new CardView("event-reference",CardCatalog.Get("oracle-l5r-8494"),1-pending.PlayerId,Zone.EventInPlay,true,false,null,null,0);
        _eventSidebar.Present(View,Actions,source,CanCommitEvent,CommitEvent);
        if(_session.Online&&!_session.Connected)
            _eventSidebar.PauseForConnection(true,()=>_ = Reconnect());
        else if(_session.Online&&(_session.Snapshot!.PlayersConnected?.Any(p=>!p)??false))
            _eventSidebar.PauseForConnection(false,()=>{});
        else if(!_session.Online&&!_autoOpponent&&!OwnEventDecision)
            _eventSidebar.ContextAction("Pass device to other player",SwitchSeat);
        if(_eventHandBar.Visible)_eventHandBar.Present(SelectedEventDiscard,CanCommitEvent,()=>{if(SelectedEventDiscard is { } action)CommitEvent(action);},ClearEventSelection);
        FitEventDecisions();Highlight();return true;
    }
    private void CommitEvent(ActionOption option)
    {
        // Check the current permitted action as well as its captured version. No inferred/default answer.
        if(!OwnEventDecision||!CanCommitEvent||option.Command is not { } command||command.ExpectedVersion!=View.Version)return;
        if(!Actions.Any(a=>a.Enabled&&a.Command==command))return;
        Submit(command);
    }
    private void ClearEventSelection()
    {
        _eventDiscardId=null;_selected=null;HideTableTooltip();RenderEventDecision();Highlight();
    }
    private bool SelectEventCard(CardView card)
    {
        if(!EventDecisionActive)return false;
        if(OwnEventDecision && View.PendingChoice!.Type=="forcedDiscard" &&
            Actions.Any(a=>a.Enabled&&a.Type=="forcedDiscard"&&a.Command?.TargetId==card.Id))
        {
            _eventDiscardId=_eventDiscardId==card.Id?null:card.Id;
            _eventSelectionVersion=View.Version;
            _selected=_eventDiscardId is null?null:card;
            RenderEventDecision();Highlight();
        }
        // Other public cards remain hover-inspectable; clicking never replaces the decision.
        return true;
    }
}
