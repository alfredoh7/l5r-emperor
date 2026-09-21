using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _lockCaptureStarted,_lockReviewBusy;
    private MatchEngine _lockFixture=null!;
    private string _lockHero="",_lockItem="";
    private Button[] _lockReviewButtons=[];
    private static string StabilizationText(Node root)=>string.Join("\n",root.FindChildren("*","Label",true,false).OfType<Label>().Select(label=>label.Text));
    private void ProcessActionLockCapture(double delta)
    {
        _captureClock+=delta;if(_lockCaptureStarted||_captureClock<1.5)return;
        _lockCaptureStarted=true;_=CaptureActionLock();
    }
    private async Task PrepareLockReview(bool crowded=false,bool bowed=false)
    {
        var state=MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase=Phase.Action;state.ActivePlayerId=state.PriorityPlayerId=0;state.PendingChoice=null;
        var hotako=SupportFixtureCard(state,1726,1,Zone.Home);
        var hero=SupportFixtureCard(state,10292,0,Zone.Home,bowed:bowed);_lockHero=hero.Id;
        var item=SupportFixtureCard(state,10118,0,Zone.Attached);item.AttachedTo=hero.Id;_lockItem=item.Id;
        if(crowded)
        {
            hero.Dishonorable=true;
            var follower=CardCatalog.Cards.Values.First(d=>!d.ScenarioOnly&&d.Type==CardType.Follower);
            SupportFixtureCard(state,follower.Id,0,Zone.Attached,bowed:true).AttachedTo=hero.Id;
            for(var i=0;i<6;i++)SupportFixtureCard(state,1645,i%2,Zone.Home,bowed:i%3==0);
        }
        _lockFixture=(MatchEngine)typeof(MatchEngine).GetMethod("FromTestState",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[state])!;
        await LoadSupportFixture(_lockFixture,0);
        if(!bowed)SupportRequire(Actions.Any(a=>a.CardId==_lockHero&&a.Enabled&&a.Command?.AbilityId=="shizuka-search"),"Published action missing before lock");
        await _session.Submit(Actions.Single(a=>a.Enabled&&a.Type=="pass").Command!);await WaitDecisionPresentation();
        var ability=_lockFixture.QueryActions(1).Single(a=>a.Enabled&&a.Command?.AbilityId=="hotako-fear-lock"&&a.CardId==hotako.Id);
        await _session.Submit(ability.Command! with {TargetId=hero.Id});await WaitDecisionPresentation();
        SelectCard(FindCard(_lockHero)!);HideTableTooltip();ReleaseTableFocus();
        if(_captureMode.Contains("review"))BuildLockReviewControls();
        await EventFrames(12);
    }
    private void BuildLockReviewControls()
    {
        if(_root.GetNodeOrNull<Control>("LockReviewControls") is {} old){_root.RemoveChild(old);old.QueueFree();}
        var row=new HBoxContainer {Name="LockReviewControls"};row.AddThemeConstantOverride("separation",6);
        var inspect=SidebarSkin.Button("Inspect attachment",()=>
        {
            if(FindCard(_lockItem) is {} attachment)SelectCard(attachment);
            else SetNotice("That attachment has left the table. Restart demo to prepare it again.");
        });
        var end=SidebarSkin.Button("Advance turn",()=>_=RunLockReview(AdvanceLockTurn));
        var restart=SidebarSkin.Button("Restart demo",()=>_=RunLockReview(()=>PrepareLockReview()));
        _lockReviewButtons=[inspect,end,restart];
        foreach(var button in _lockReviewButtons){button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;button.CustomMinimumSize=new(0,44);row.AddChild(button);}
        Place(_root,row,350,114,830,50);
    }
    private async Task RunLockReview(Func<Task> action)
    {
        if(_lockReviewBusy)return;_lockReviewBusy=true;foreach(var b in _lockReviewButtons)b.Disabled=true;
        try{await action();}catch(Exception e){SetNotice("The demo has changed. Use Restart demo to prepare it again.");GD.PushWarning(e.Message);}
        finally{_lockReviewBusy=false;foreach(var b in _lockReviewButtons)if(IsInstanceValid(b))b.Disabled=false;}
    }
    private async Task AdvanceLockTurn()
    {
        if(!FindCard(_lockHero)!.ActionLocked){SetNotice("The restriction has expired. Restart demo to see it again.");return;}
        var turn=_lockFixture.State.TurnNumber;
        for(var step=0;step<32&&_lockFixture.State.TurnNumber==turn;step++)
        {
            var actor=_lockFixture.State.PendingChoice?.PlayerId??_lockFixture.State.PriorityPlayerId;
            var next=_lockFixture.QueryActions(actor).First(a=>a.Enabled&&a.Command is not null&&a.Type is "pass" or "endDynasty" or "declineAttack" or "declineReaction");
            await _session.Submit(next.Command!);await WaitDecisionPresentation();
        }
        SupportRequire(_lockFixture.State.TurnNumber>turn,"Demo did not advance the turn");
        SelectCard(FindCard(_lockHero)!);SetNotice("A new turn has begun. The action restriction has expired.");
    }
    private async Task ClickLockSeal(ActionLockSeal seal)
    {
        DeselectCard();
        var point=seal.CenterOnViewport();PushCaptureMouse(new InputEventMouseMotion {Position=point,GlobalPosition=point});await EventFrames(8);
        PushCaptureMouse(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true});
        PushCaptureMouse(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false});await EventFrames(12);
    }
    private async Task CaptureActionLock()
    {
        try
        {
            _preferences.ReducedMotion=_captureMode.Contains("reduced");ApplyPreferences(false);
            await PrepareLockReview();
            if(_captureMode.Contains("review")){GD.Print("ACTION_LOCK_REVIEW_READY: Hotako has restricted Shizuka; inspect attachment or advance turn.");return;}
            var hero=_tiles.Single(t=>t.Card.Id==_lockHero);var version=View.Version;
            SupportRequire(hero.RestrictionSeal is not null&&FindCard(_lockItem)!.ActionLocked,"Public lock not presented on Personality or attachment");
            await ClickLockSeal(hero.RestrictionSeal!);
            SupportRequire(_selected?.Id==_lockHero&&View.Version==version,"Seal click must only inspect the locked card");
            SupportRequire(StabilizationText(_inspector).Contains(ActionLockStatus.Message),"Inspector restriction missing");
            var blocked=Actions.Single(a=>a.CardId==_lockHero&&a.Type=="useAbility");
            SupportRequire(!blocked.Enabled&&blocked.Reason!.Contains("may not perform actions")&&StabilizationButtons(_inspector).Any(b=>b.Text==blocked.Label&&b.Disabled),"Engine-disabled action lost its disabled presentation");
            var button=StabilizationButtons(_inspector).Single(b=>b.Text==blocked.Label);
            SupportRequire(_inspectorPanel.GetGlobalRect().Encloses(button.GetGlobalRect()),"Disabled action is outside the visible inspector");
            await SupportCapture("-table");
            DeselectCard();var pointer=ScreenCenter(hero);PushCaptureMouse(new InputEventMouseMotion {Position=pointer,GlobalPosition=pointer});await EventFrames(45);
            SupportRequire(_tableTooltip is not null&&StabilizationText(_tableTooltip).Contains(ActionLockStatus.Message),"Full-card hover did not explain the restriction");
            SupportRequire(GetViewport().GetVisibleRect().Encloses(_tableTooltip!.GetGlobalRect()),"Restricted-card hover leaves the window");await SaveShownCapture("-hover");HideTableTooltip();
            await ClickLockSeal(hero.RestrictionSeal!);
            _expandedUnits.Add(_lockHero);RenderBoard();await EventFrames(15);
            hero=_tiles.Single(t=>t.Card.Id==_lockHero);await ClickLockSeal(hero.RestrictionSeal!);
            await SupportCapture("-detail");
            await SupportClick(_tiles.Single(t=>t.Card.Id==_lockItem));
            SupportRequire(StabilizationText(_inspector).Contains(ActionLockStatus.Message),"Attachment inspection lost the public restriction");await SupportCapture("-attachment");
            _session.SwitchPracticeSeat();await EventFrames(20);
            hero=_tiles.Single(t=>t.Card.Id==_lockHero);SupportRequire(hero.RestrictionSeal is not null&&hero.Card.ActionLocked,"Other seat lost public seal");
            await ClickLockSeal(hero.RestrictionSeal!);SupportRequire(_selected?.Id==_lockHero,"Other seat cannot inspect restriction");await SupportCapture("-opponent");
            _session.SwitchPracticeSeat();await EventFrames(12);
            _expandedUnits.Clear();RenderBoard();await AdvanceLockTurn();await EventFrames(20);
            SupportRequire(!FindCard(_lockHero)!.ActionLocked&&!FindCard(_lockItem)!.ActionLocked&&_tiles.Single(t=>t.Card.Id==_lockHero).RestrictionSeal is null&&!StabilizationText(_inspector).Contains(ActionLockStatus.Message),"Expired restriction remained visible");await SupportCapture("-expired");
            await PrepareLockReview(crowded:true,bowed:true);await SupportCapture("-crowded-bowed");
            _expandedUnits.Add(_lockHero);RenderBoard();await EventFrames(15);await SupportCapture("-bowed-detail");
            SupportRequire(_tiles.Where(t=>ActionLockStatus.VisibleOn(t.Card)&&!t.UprightPreview).All(t=>t.RestrictionSeal is not null),"Bowed or crowded Personality lost its marker");
            GD.Print("ACTION_LOCK_PASS: real Hotako lock, native seal input, disabled action, both seats, attachments, expiry, bowed/dishonorable crowded rendering");GetTree().Quit();
        }
        catch(Exception e){await SupportCapture("-failure");GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
