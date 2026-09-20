using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _decisionCaptureStarted;
    private string _decisionReviewCase="yumaru";
    private void ProcessDecisionCapture(double delta)
    {
        _captureClock+=delta;
        if(_decisionCaptureStarted || _captureClock<1.5)return;
        _decisionCaptureStarted=true;
        _=CaptureDecision();
    }
    private static void DecisionRequire(bool ok,string message)
    { if(!ok)throw new InvalidOperationException("Decision review: "+message); }
    private Button DecisionButton(string text) => StabilizationButtons(_root).Single(b=>b.IsVisibleInTree() && b.Text==text);
    private async Task PrepareDecisionReview(string kind,int? seat=null)
    {
        _decisionReviewCase=kind;ResetDecisionSelection();
        var engine=MatchEngine.CreateDecisionShowcase(kind);
        await LoadSupportFixture(engine,seat??engine.State.PriorityPlayerId);
        if(kind.StartsWith("fukui",StringComparison.Ordinal) && (seat??engine.State.PriorityPlayerId)==0)
        {
            SelectCard(Self.Army.Single(c=>c.Definition?.Id=="oracle-l5r-4375"));
            if(_captureMode.StartsWith("published-decisions-review",StringComparison.Ordinal))
                Choose(Actions.First(a=>a.Enabled&&a.AlternativeCost?.AbilityId=="fukui-bow"));
        }
        Render();await EventFrames(8);
    }
    private void AddDecisionReviewNavigation()
    {
        if(!_captureMode.StartsWith("published-decisions-review",StringComparison.Ordinal))return;
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",6);
        var restart=SidebarSkin.Button("Restart demo",()=>_ = PrepareDecisionReview(_decisionReviewCase));
        var next=SidebarSkin.Button("Next demo",()=>_ = PrepareDecisionReview(_decisionReviewCase switch {"yumaru"=>"fukui","fukui"=>"fukui-bowed",_=>"yumaru"}));
        restart.SizeFlagsHorizontal=next.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        restart.CustomMinimumSize=next.CustomMinimumSize=new(0,44);
        row.AddChild(restart);row.AddChild(next);_decisionActions.AddChild(row);
        _decisionActions.AddChild(Palette.Wrapped("Review demo · "+(_decisionReviewCase=="yumaru"?"Yumaru: choose who moves home":_decisionReviewCase=="fukui"?"Fukui: bow or destroy the Ancestor":"Fukui: a bowed Ancestor cannot pay the bow cost"),13,Palette.PaperDim));
    }
    private async Task WaitDecisionPresentation()
    {
        await EventFrames(5);
        for(var i=0;i<900 && PresentationBusy;i++)await EventFrames(1);
        DecisionRequire(!PresentationBusy,"Presentation did not finish");
        await EventFrames(5);
    }
    private async Task CaptureDecision()
    {
        try
        {
            if(_captureMode.Contains("reduced")){_preferences.ReducedMotion=true;ApplyPreferences(false);}
            if(_captureMode.EndsWith("-generic",StringComparison.Ordinal))
            {
                foreach(var generic in new[]{"preserving","rin","conqueror","straighten","discard"})
                {
                    await PrepareDecisionReview(generic);
                    DecisionRequire(GenericDecisionActive && DecisionButton("Confirm choice").Disabled,"Generic decision missing or defaulted: "+generic);
                    var before=View.Version;var option=Actions.First(a=>a.Enabled&&a.Command is not null&&a.Type!="concede");
                    await SupportCapture("-"+generic);await SupportClick(DecisionButton(option.Label));
                    DecisionRequire(View.Version==before,"Selection changed the game: "+generic);
                    await SupportClick(DecisionButton("Confirm choice"));await WaitDecisionPresentation();
                    DecisionRequire(View.Version==before+1,"Generic answer did not execute once: "+generic);
                    GD.Print("GENERIC_DECISION_PASS: "+generic);
                }
                GD.Print("DECISION_CHOOSER_PASS: "+_captureMode);GetTree().Quit();return;
            }
            var kind=_captureMode.Contains("fukui-bowed")?"fukui-bowed":_captureMode.Contains("fukui")?"fukui":"yumaru";
            var waiting=_captureMode.Contains("waiting");
            await PrepareDecisionReview(kind,waiting?1:0);
            if(_captureMode.Contains("review"))
            {GD.Print("DECISION_REVIEW_READY: use Next demo for Yumaru, Fukui and bowed-Ancestor cases.");return;}
            var version=View.Version;
            if(waiting)
            {
                DecisionRequire(Actions.All(a=>a.Type is "wait" or "concede"),"Opponent has hidden decisions");
                DecisionRequire(!StabilizationButtons(_root).Any(b=>b.IsVisibleInTree()&&b.Text=="Confirm choice"),"Opponent has confirmation");
            }
            else if(kind=="yumaru")
            {
                DecisionRequire(GenericDecisionActive && _selected is null,"Decision depends on selecting source");
                DecisionRequire(DecisionButton("Confirm choice").Disabled,"A default choice was selected");
                DecisionRequire(_tiles.Any(t=>t.Card.Id==View.PendingChoice!.CardId&&t.LegalTarget),"Source lacks highlight");
                await SupportCapture("");
                var label=_captureMode.Contains("target-only")?"Move the targeted Personality home":_captureMode.Contains("self-only")?"Move Yumaru home":"Move both home";
                var option=Actions.Single(a=>a.Label==label);
                var button=DecisionButton(label);button.GrabFocus();
                GetViewport().PushInput(new InputEventKey {Keycode=Key.Enter,Pressed=true},true);
                GetViewport().PushInput(new InputEventKey {Keycode=Key.Enter,Pressed=false},true);
                await EventFrames(5);
                DecisionRequire(View.Version==version && _decisionSelection==option,"Keyboard selection did not remain a draft");
                await SupportCapture("-selected");await SupportClick(DecisionButton("Confirm choice"));await WaitDecisionPresentation();
                DecisionRequire(View.Version==version+1 && View.PendingChoice is null,"Selected choice did not resolve exactly once");
                DecisionRequire(View.Events.Any(e=>e.Type=="effectChosen"&&e.Message.Contains(label)),"Chosen consequence absent from public log");
            }
            else
            {
                var grouped=StabilizationButtons(_inspector).Where(b=>b.HasMeta("alternative_cost_group")).ToArray();
                DecisionRequire(grouped.Length==1,"Cost variants were not grouped");
                await SupportClick(grouped[0]);await EventFrames(5);
                await SupportClick(DecisionButton("Review cost choices"));await EventFrames(5);
                DecisionRequire(DecisionButton("Confirm choice").Disabled,"Alternative cost has implicit default");
                DecisionRequire(DecisionButton("Bow the Ancestor").Disabled==(kind=="fukui-bowed"),"Incorrect disabled bow option");
                var selected=Actions.Single(a=>a.AlternativeCost?.Id=="destroy");
                await SupportCapture("");await SupportClick(DecisionButton("Destroy the Ancestor"));
                DecisionRequire(View.Version==version,"Selecting cost mutated state");
                await SupportCapture("-selected");await SupportClick(DecisionButton("Confirm choice"));await WaitDecisionPresentation();
                DecisionRequire(View.Version==version+1,"Cost did not execute once");
                DecisionRequire(FindCard(selected.AlternativeCost!.PerformerId!)?.Zone==Zone.RemovedFromGame,"Created Ancestor not removed after destruction");
                DecisionRequire(FindCard(selected.Command!.TargetId!)?.Bowed==true,"Target not bowed");
            }
            await SupportCapture("-result");
            GD.Print("DECISION_CHOOSER_PASS: "+_captureMode);GetTree().Quit();
        }
        catch(Exception e){await SupportCapture("-failure");GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
