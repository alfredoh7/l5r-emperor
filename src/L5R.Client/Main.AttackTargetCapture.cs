using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _attackCaptureStarted,_attackReviewBusy;
    private MatchEngine _attackFixture=null!;
    private string _attackSource="",_attackCard="",_attackReactiveCard="",_attackFollower="",_attackFollowedHost="";
    private Button[] _attackReviewButtons=[];
    private void ProcessAttackTargetCapture(double delta)
    {_captureClock+=delta;if(_attackCaptureStarted||_captureClock<1.5)return;_attackCaptureStarted=true;_=CaptureAttackTargets();}
    private async Task PrepareAttackReview(string mode="ranged")
    {
        var state=MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase=Phase.Battle;state.ActivePlayerId=state.PriorityPlayerId=0;state.PendingChoice=null;
        state.CurrentBattleProvince=0;state.UnresolvedBattlefields=[0];
        var own=SupportFixtureCard(state,3636,0,Zone.Battlefield,0);
        var source=mode=="tensin"?SupportFixtureCard(state,3332,0,Zone.Battlefield,0):mode=="province"?SupportFixtureCard(state,"training-province-attacker",0,Zone.Battlefield,0):
            mode=="cost"?state.Cards[state.Players[0].StrongholdId]:SupportFixtureCard(state,mode=="melee"?7450:3456,0,Zone.Attached,0);
        if(source.Zone==Zone.Attached)source.AttachedTo=own.Id;
        if(mode=="tensin")
            foreach(var definition in new[]{3456,7450})SupportFixtureCard(state,definition,0,Zone.Attached,0).AttachedTo=source.Id;
        if(mode=="cost")source.DefinitionId="oracle-l5r-1218";
        _attackSource=source.Id;_attackCard=_attackReactiveCard=_attackFollower=_attackFollowedHost="";
        if(mode!="empty")
        {
            _attackCard=SupportFixtureCard(state,2060,1,Zone.Battlefield,0).Id;
            if(mode=="follower")
            {
                var host=SupportFixtureCard(state,3636,1,Zone.Battlefield,0);_attackFollowedHost=host.Id;
                var follower=SupportFixtureCard(state,3456,1,Zone.Attached,0);follower.AttachedTo=host.Id;_attackFollower=follower.Id;
            }
            else _attackReactiveCard=SupportFixtureCard(state,2862,1,Zone.Battlefield,0).Id;
        }
        _attackFixture=(MatchEngine)typeof(MatchEngine).GetMethod("FromTestState",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[state])!;
        await LoadSupportFixture(_attackFixture,0);
        var action=Actions.First(a=>a.CardId==source.Id&&a.Enabled&&a.NoTarget);
        Choose(action);
        if(_captureMode.Contains("review"))BuildAttackReviewControls();
        await EventFrames(20);
    }
    private void BuildAttackReviewControls()
    {
        if(_root.GetNodeOrNull<Control>("AttackReviewControls") is {} old){_root.RemoveChild(old);old.QueueFree();}
        var row=new HBoxContainer{Name="AttackReviewControls",ZIndex=91};row.AddThemeConstantOverride("separation",5);
        var buttons=new List<Button>();
        foreach(var (label,mode) in new[]{("Ranged","ranged"),("Melee","melee"),("Follower","follower"),("No cards","empty"),("Province demo","province"),("Cost","cost"),("Tensin","tensin")})
            buttons.Add(SidebarSkin.Button(label,()=>_=RunAttackReview(()=>PrepareAttackReview(mode))));
        buttons.Add(SidebarSkin.Button("Other seat",()=>_session.SwitchPracticeSeat()));_attackReviewButtons=buttons.ToArray();
        foreach(var button in buttons){button.CustomMinimumSize=new(0,44);button.AddThemeFontSizeOverride("font_size",14);button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(button);}
        Place(_root,row,344,114,1000,46);FitAttackReviewControls();
    }
    private void FitAttackReviewControls()
    {if(_root.GetNodeOrNull<Control>("AttackReviewControls") is {} row)row.Size=new(Math.Min(1120,_layoutWidth-SidebarWidth-22-344-18),46);}
    private async Task RunAttackReview(Func<Task> action)
    {
        if(_attackReviewBusy)return;_attackReviewBusy=true;foreach(var b in _attackReviewButtons)b.Disabled=true;
        try{await action();}catch(Exception e){SetNotice("Restart an attack example to prepare it again.");GD.PushWarning(e.ToString());}
        finally{_attackReviewBusy=false;foreach(var b in _attackReviewButtons)if(IsInstanceValid(b))b.Disabled=false;}
    }
    private Button AttackNoTargetButton()=>StabilizationButtons(_inspector).Single(b=>b.Name=="AttackNoTarget");
    private AttackTargetFace AttackCardFace(string id)=>StabilizationButtons(_inspector).OfType<AttackTargetFace>().Single(b=>b.Card.Id==id);
    private async Task ConfirmAttackTarget()
    {await SupportClick(_paymentConfirm!);await WaitDecisionPresentation();await EventFrames(45);}
    private void CheckAttackTab(string kind, string source)
    {
        SupportRequire(_attackTab.Visible && _attackTab.Current == new AttackCueView(kind,source) && _attackTab.Progress == 1,
            $"Missing settled {kind} source tab for {source}: {_attackTab.Current}, {_attackTab.Progress}");
        var body=_attackTab.GetGlobalRect();
        SupportRequire(_tableViewport.GetGlobalRect().Encloses(body),"Attack tab is outside the table");
        SupportRequire(!UnitBadges.OfType<UnitStandard>().Where(b=>b.IsVisibleInTree()).Any(b=>b.BodyRectOnViewport().Intersects(body)),"Attack tab covers a Force ribbon");
    }
    private void CheckAttackConfirmation()
    {
        SupportRequire(_paymentConfirm is {Disabled:false}&&GetViewport().GetVisibleRect().Encloses(_paymentConfirm.GetGlobalRect()),"Attack confirmation is disabled or offscreen");
        SupportRequire(GetViewport().GetVisibleRect().Encloses(_inspectorPanel.GetGlobalRect()),"Attack sidebar extends beyond the window");
        var clip=_tableViewport.GetGlobalRect();SupportRequire(IntentArrows().All(a=>clip.HasPoint(a.From)&&clip.HasPoint(a.To)),"Attack intent arrow is outside the table viewport");
    }
    private async Task CaptureAttackTargets()
    {
        try
        {
            // Limited renderer/controller path for a locked desktop. It never claims native table-input acceptance.
            var renderOnly=_captureMode.Contains("render");
            _preferences.ReducedMotion=_captureMode.Contains("reduced");ApplyPreferences(false);await PrepareAttackReview();
            if(_captureMode.EndsWith("-review",StringComparison.Ordinal)){GD.Print("ATTACK_TARGET_REVIEW_READY: real Ranged/Melee choices and prepared province/cost examples.");return;}
            CheckAttackTab("Ranged",_attackSource);
            // Drive the native control's timeline deterministically as well as the live
            // captures: reduced motion jumps; normal mode visibly slides both ways.
            _attackTab.SetProcess(false);_attackTab.Observe=null;_attackTab.Cancel();_attackTab.Desired=_chosen!.Attack;
            _attackTab._Process(.09);
            SupportRequire(_reducedMotion?_attackTab.Progress==1:_attackTab.Progress is >0 and <1,"Attack tab did not honor motion preference");
            await SupportCapture("-tab-entering");
            _attackTab._Process(.18);CheckAttackTab("Ranged",_attackSource);
            _attackTab.Desired=null;_attackTab._Process(.09);
            SupportRequire(_reducedMotion?!_attackTab.Visible:_attackTab.Progress is >0 and <1,"Attack tab did not retract");
            await SupportCapture("-tab-retracting");
            _attackTab._Process(.18);SupportRequire(!_attackTab.Visible,"Retracted tab remains visible");
            _attackTab.Observe=ObserveAttackTab;_attackTab.SetProcess(true);await EventFrames(20);CheckAttackTab("Ranged",_attackSource);
            SupportRequire(AttackTargetActive&&_attackChoice==AttackChoice.Unselected&&_paymentConfirm!.Disabled&&_chosen!.Command is {TargetId:null,Province:null},"Attack silently selected a card or No target");
            SupportRequire(!StabilizationButtons(_inspector).Any(b=>b.Name.ToString().StartsWith("AttackProvince")),"Ordinary Attack offered a province");
            if(_captureMode.Contains("review"))SupportRequire(!_root.GetNode<Control>("AttackReviewControls").GetGlobalRect().Intersects(_inspectorPanel.GetGlobalRect()),"Attack review controls overlap the sidebar");
            await SupportCapture("-initial");
            var noTarget=AttackNoTargetButton();noTarget.GrabFocus();await EventFrames(3);
            GetViewport().PushInput(new InputEventKey{Keycode=Key.Enter,Pressed=true});GetViewport().PushInput(new InputEventKey{Keycode=Key.Enter,Pressed=false});await EventFrames(12);
            CheckAttackConfirmation();SupportRequire(_attackChoice==AttackChoice.NoTarget&&View.Version==0&&!IntentArrows().Any(),"Keyboard No target changed authority or left an arrow");await SupportCapture("-no-target");
            await SupportClick(AttackCardFace(_attackCard));CheckAttackConfirmation();
            SupportRequire(_chosen!.Command!.TargetId==_attackCard&&_chosen.Command.Province is null&&IntentArrows().Count()==1&&View.Version==0,"Card face did not produce a local card-target draft");await SupportCapture("-card");
            var savedZoom=_cameraZoom;var savedCenter=_cameraCenter;
            ZoomTable(1.7f,_tableViewport.GetGlobalTransform().AffineInverse()*ScreenCenter(_tiles.Single(t=>t.Card.Id==_attackSource)));
            await EventFrames(20);CheckAttackTab("Ranged",_attackSource);await SupportCapture("-tab-close");
            _cameraZoom=savedZoom;_cameraCenter=savedCenter;ApplyTableCamera();await EventFrames(12);
            if(renderOnly){ChooseAttackTarget(AttackChoice.Card,_attackReactiveCard);await EventFrames(12);}
            else await SupportClick(_tiles.Single(t=>t.Card.Id==_attackReactiveCard));
            SupportRequire(_chosen.Command.TargetId==_attackReactiveCard,"Table retargeting did not follow the clicked legal card");
            CheckAttackTab("Ranged",_attackSource);
            await SupportClick(AttackNoTargetButton());SupportRequire(_chosen.Command.TargetId is null&&_chosen.Command.Province is null&&!IntentArrows().Any(),"No target retained the previous target/arrow");
            await SupportClick(StabilizationButtons(_decisionActions).Single(b=>b.Text=="Cancel attack"));
            SupportRequire(_chosen is null&&View.Version==0&&!IntentArrows().Any(),"Cancel executed the Attack or kept an arrow");
            await EventFrames(20);SupportRequire(!_attackTab.Visible,"Cancel left the attack tab");
            await PrepareAttackReview();await SupportClick(AttackNoTargetButton());_session.SwitchPracticeSeat();await EventFrames(15);
            SupportRequire(_chosen is null&&!AttackTargetActive,"A changed seat retained the attack draft");await SupportCapture("-waiting");
            SupportRequire(!_attackTab.Visible,"A changed seat retained the private attack tab");
            _session.SwitchPracticeSeat();await EventFrames(15);Choose(Actions.First(a=>a.CardId==_attackSource&&a.Enabled&&a.NoTarget));await EventFrames(12);
            SupportRequire(_attackChoice==AttackChoice.Unselected&&_paymentConfirm!.Disabled,"Seat return silently reused the choice");
            await SupportClick(AttackNoTargetButton());await ConfirmAttackTarget();
            SupportRequire(View.Version==1&&_attackFixture.State.PendingChoice is null&&_attackFixture.State.PendingAttack is null&&_attackFixture.State.Cards[_attackCard].Zone==Zone.Battlefield,"No-target Attack failed or triggered a card reaction/hit");
            SupportRequire(!_attackTab.Visible,"Resolved No target retained a tab");
            await PrepareAttackReview();await SupportClick(AttackCardFace(_attackCard));await ConfirmAttackTarget();
            SupportRequire(View.Events.Any(e=>e.Type=="unitDestroyed"&&e.CardId==_attackCard),"Confirmed card attack did not hit its chosen card");await SupportCapture("-hit");
            await PrepareAttackReview();await SupportClick(AttackCardFace(_attackReactiveCard));await ConfirmAttackTarget();
            SupportRequire(_attackFixture.State.PendingAttack is not null&&_attackFixture.State.PendingChoice?.Type=="attackReaction","Card-targeted reaction was bypassed");await SupportCapture("-reaction");
            CheckAttackTab("Ranged",_attackSource);
            _session.SwitchPracticeSeat();await EventFrames(20);ResetTableCamera();await EventFrames(30);
            CheckAttackTab("Ranged",_attackSource);await SupportCapture("-reaction-other-seat");
            await PrepareAttackReview("follower");SupportRequire(_chosen!.TargetIds!.Contains(_attackFollower)&&!_chosen.TargetIds.Contains(_attackFollowedHost),"Gallery overrode the engine's Follower target restriction");
            await SupportClick(AttackCardFace(_attackFollower));await SupportCapture("-follower");await ConfirmAttackTarget();
            SupportRequire(_attackFixture.State.Cards[_attackFollower].Zone==Zone.FateDiscard&&_attackFixture.State.Cards[_attackFollowedHost].Zone==Zone.Battlefield,"Follower attack destroyed the wrong card");
            foreach(var mode in new[]{"melee","empty"})
            {
                await PrepareAttackReview(mode);if(mode=="empty")SupportRequire(_chosen!.TargetIds is {Length:0},"Empty fixture retained a legal card");
                await EventFrames(20);CheckAttackTab(mode=="melee"?"Melee":"Ranged",_attackSource);
                await SupportClick(AttackNoTargetButton());CheckAttackConfirmation();await SupportCapture("-"+mode);await ConfirmAttackTarget();
                SupportRequire(View.Version==1&&_attackFixture.State.PendingAttack is null,"No-target "+mode+" could not resolve");
            }
            await PrepareAttackReview("province");var strength=_attackFixture.State.Players[1].Provinces[0].Strength;
            if(renderOnly){ChooseAttackTarget(AttackChoice.Province,province:0);await EventFrames(12);}
            else await SupportClick(_tiles.Single(t=>t.Card.OwnerId==1&&t.Card.Zone==Zone.Province&&t.Card.ProvinceIndex==0));
            SupportRequire(_attackChoice==AttackChoice.Province&&_chosen!.Command is {TargetId:null,Province:0}&&IntentArrows().Count()==1,"Table province chose its Dynasty card or left no arrow");
            await SupportClick(AttackCardFace(_attackCard));SupportRequire(_chosen!.Command!.Province is null,"Switching to a card retained the province");
            await SupportClick(StabilizationButtons(_inspector).Single(b=>b.Name=="AttackProvince0"));CheckAttackConfirmation();await SupportCapture("-province");await ConfirmAttackTarget();
            SupportRequire(_attackFixture.State.Players[1].Provinces[0].Strength==Math.Max(0,strength-3)&&View.Events.Any(e=>e.Type=="provinceAttacked")&&_attackFixture.State.PendingChoice is null,"Province Attack failed or opened a card-target reaction");
            await PrepareAttackReview("province");strength=_attackFixture.State.Players[1].Provinces[0].Strength;
            await SupportClick(AttackNoTargetButton());await ConfirmAttackTarget();SupportRequire(_attackFixture.State.Players[1].Provinces[0].Strength==strength,"No target silently hit the province");
            await PrepareAttackReview("cost");var performer=_chosen!.Command!.PerformerId!;var honor=Self.Honor;
            await SupportClick(AttackNoTargetButton());await SupportCapture("-cost");await ConfirmAttackTarget();
            SupportRequire(_attackFixture.State.Cards[performer].Bowed&&Self.Honor==honor&&_attackFixture.State.PendingAttack is null,"No target skipped its bow cost or awarded card-target Honor");
            foreach(var targetCard in new[]{false,true})
            {
                await PrepareAttackReview("tensin");
                var offered=Actions.Where(a=>a.Enabled&&a.CardId==_attackSource&&a.Command?.AbilityId=="tensin-follower-attack").ToArray();
                SupportRequire(offered.Length==2&&offered.All(a=>a.NoTarget&&a.AlternativeCost is null),"Tensin fixture must expose both performing Followers with a fixed bow cost");
                var firstPerformer=offered[0].Command!.PerformerId;Choose(offered[1]);await EventFrames(12);
                performer=_chosen!.Command!.PerformerId!;
                await ToSignal(GetTree().CreateTimer(.42),SceneTreeTimer.SignalName.Timeout);
                CheckAttackTab("Ranged",performer);
                SupportRequire(performer!=firstPerformer&&_attackChoice==AttackChoice.Unselected&&_paymentConfirm!.Disabled,"Changing the performing Follower retained a target draft");
                if(targetCard)await SupportClick(AttackCardFace(_attackCard));else await SupportClick(AttackNoTargetButton());
                CheckAttackConfirmation();
                SupportRequire(View.Version==0&&!_attackFixture.State.Cards[performer].Bowed&&_chosen!.Command!.TargetId==(targetCard?_attackCard:null),"Selecting Tensin's target paid the cost early or lost the target");
                await SupportCapture(targetCard?"-tensin-card":"-tensin-no-target");await ConfirmAttackTarget();
                SupportRequire(View.Version==1&&_attackFixture.State.Cards[performer].Bowed&&!_attackFixture.State.Cards[firstPerformer!].Bowed&&_attackFixture.State.PendingAttack is null,"Tensin bowed the wrong Follower or opened an unexpected reaction");
                SupportRequire(_attackFixture.State.Cards[_attackCard].Zone==(targetCard?Zone.Dead:Zone.Battlefield),"Tensin's explicit target was not respected");
            }
            GD.Print(renderOnly?"ATTACK_TARGET_RENDER_PASS: rendering, sidebar controls and real-engine outcomes checked; native table focus/input and desktop launcher acceptance remain pending.":
                "ATTACK_TARGET_PASS: native no-target/card/province choices, full faces, table and keyboard input, explicit confirm, cancel/seat reset, Follower legality, empty/Melee, real costs, Tensin fixed-cost/performer regression and correct reaction behavior.");GetTree().Quit();
        }
        catch(Exception e){await SupportCapture("-failure");GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
