using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private CopyChoicePanel? _copyPanel;
    private string? _copyViewKey,_copyProgressKey,_copyNoticeMatch,_copyNotice;
    private int _copyTotal;
    private bool _copyNoticeCompact;
    private long _copyNoticeSequence=-1,_copyNoticeVersion=-1;
    private bool CopyPanelOpen=>_copyPanel?.IsVisibleInTree()==true;
    private bool CopyDecisionActive=>View.WinnerId is null&&View.PendingChoice?.Type is "copyKeyword" or "copyAbility";
    private void ClearCopyPanel(bool resetProgress=false)
    {
        if(_copyPanel is not null){_copyPanel.Hide();_root.RemoveChild(_copyPanel);_copyPanel.QueueFree();_copyPanel=null;}
        _copyViewKey=null;if(resetProgress){_copyProgressKey=null;_copyTotal=0;}
    }
    private void FitCopyPanel()
    {
        if(_copyPanel is null)return;
        _copyPanel.Position=new(344,168);_copyPanel.Size=new(Math.Min(1120,_layoutWidth-SidebarWidth-22-344-18),650);
    }
    private void ObserveCopyResults()
    {
        var sequence=View.Events.LastOrDefault()?.Sequence??0;
        if(_copyNoticeMatch!=_session.MatchKey){_copyNoticeMatch=_session.MatchKey;_copyNoticeSequence=sequence;_copyNotice=null;}
        if(_copyNoticeVersion!=View.Version)_copyNotice=null;
        var events=View.Events.Where(e=>e.Sequence>_copyNoticeSequence&&e.Type is "copyNothing" or "keywordCopied" or "abilityCopied" or "copyEnded").ToArray();
        if(events.Length>0)
        {
            _copyNotice=string.Join("\n",events.Select(e=>e.Message));
            _copyNoticeCompact=events.All(e=>e.Type is "keywordCopied" or "abilityCopied");
            // Once the composed action finishes, leave its receiver inspected so normal copied actions are discoverable.
            if(View.PendingChoice is null&&events.LastOrDefault(e=>e.CardId is not null)?.CardId is {} receiver)
                _selected=FindCard(receiver);
        }
        _copyNoticeSequence=sequence;_copyNoticeVersion=View.Version;
    }
    private bool RenderCopyTray()
    {
        if(_copyNotice is not null&&!CopyDecisionActive)
        {
            var feedback=new PanelContainer();feedback.AddThemeStyleboxOverride("panel",SidebarSkin.Inset());
            var notice=Palette.Wrapped(_copyNoticeCompact?"Copy complete · inspect the recipient to review.":_copyNotice,17);
            notice.MouseFilter=Control.MouseFilterEnum.Stop;notice.TooltipText=_copyNotice;
            feedback.AddChild(notice);_sidebarHeader.AddChild(feedback);
        }
        if(!CopyDecisionActive||PresentationBusy){ClearCopyPanel(!CopyDecisionActive);return false;}
        _guide.Visible=false;
        var copy=View.PendingCopy;
        if(copy is null||View.PendingChoice!.PlayerId!=View.ViewerId||View.PriorityPlayerId!=View.ViewerId)
        {
            ClearCopyPanel(true);
            _sidebarHeader.AddChild(Palette.Text("OPPONENT IS COPYING",24,title:true));
            _inspector.AddChild(Palette.Wrapped(View.PendingChoice!.Prompt,20));
            _inspector.AddChild(Palette.Wrapped("Waiting for "+View.Players.Single(p=>p.Id==View.PendingChoice.PlayerId).Name+" to choose.",18,Palette.Crane));
            if(!_session.Online&&!_autoOpponent)_decisionActions.AddChild(SidebarSkin.Button("Pass device to other player",SwitchSeat));
            return true;
        }
        var progress=$"{_session.MatchKey}/{View.ViewerId}/{copy.ReceiverId}/{copy.SourceDefinitionId}";
        if(_copyProgressKey!=progress){_copyProgressKey=progress;_copyTotal=copy.RemainingSteps.Length+1;}
        var receiver=FindCard(copy.ReceiverId);
        var key=$"{progress}/{View.Version}/{DecisionCanSubmit}";
        if(_copyViewKey!=key)
        {
            ClearCopyPanel();HideTableTooltip();HideShownPreview();ReleaseTableFocus();
            _copyPanel=new CopyChoicePanel {ZIndex=90};_root.AddChild(_copyPanel);FitCopyPanel();
            _copyPanel.MinimumSizeChanged+=()=>{if(_copyPanel is {} panel)panel.SetDeferred(Control.PropertyName.Size,new Vector2(Math.Min(1120,_layoutWidth-SidebarWidth-22-344-18),650));};
            _copyPanel.Populate(copy,Actions,DecisionCanSubmit,receiver,_copyTotal);_copyViewKey=key;
            _copyPanel.Confirmed+=action=>{if(DecisionCanSubmit&&_copyViewKey==key&&Actions.Contains(action)&&action.Command is not null)Submit(action.Command);};
            _copyPanel.Inspect+=card=>{var (_,body)=Dialog(card.Definition!.Name,720,720);body.AddChild(CardPreview.Face(card,510));body.AddChild(Palette.Wrapped(card.Definition.Text,17));};
        }
        _sidebarHeader.AddChild(Palette.Text("COPY TO "+copy.ReceiverName,25,title:true));
        if(receiver is not null&&CardChanges.Entries(receiver).Any(e=>e.Copied))
            _inspector.AddChild(CardChangePanel.Create(receiver,compact:true,copiedOnly:true));
        else
        {
            _inspector.AddChild(Palette.Text("CURRENTLY COPIED",20,Palette.Gold));
            _inspector.AddChild(Palette.Wrapped("No copied keyword or ability yet.",18,Palette.PaperDim));
        }
        _inspector.AddChild(Palette.Wrapped("Choose in the copy tray. Confirmed copies appear here.",17,Palette.PaperDim));
        return true;
    }
}
