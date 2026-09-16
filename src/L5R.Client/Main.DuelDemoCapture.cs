using Godot;
using L5R.Core;
using System.Text.Json;
namespace L5R.Client;

public partial class Main
{
    private int _duelProbeStep;
    private readonly List<double> _duelFrames=[];
    private ulong _duelFrameStamp;
    private string _preservedPractice="";
    private Vector2 _duelLastPlacement;
    private string? _deferredDuelButton;
    private int _focusInspectionStep,_resultPileStep,_uprightProbe,_selectionProbe;
    private readonly HashSet<string> _cinemaCaptures=[];
    private int _openingMovieFrame,_endingMovieFrame;
    private string? _inspectionBeforeOpponentClick;
    private void ProcessDuelDemoCapture(double delta)
    {
        if(_captureMode.StartsWith("menu-duel-guidance")) { ProcessDuelGuidanceCapture(delta); return; }
        if(_captureMode=="menu-duel-stages") { ProcessDuelStageCapture(delta); return; }
        if(_duelDemo?.Screen is {} cinema && cinema.CinematicBusy)
        {
            var phase=cinema.CinematicPhase;
            if(System.Environment.GetEnvironmentVariable("L5R_DUEL_MOVIE")=="1")
            {
                var opening=phase.StartsWith("opening");var directory=_capturePath+(opening?"-opening-frames":"-ending-frames");System.IO.Directory.CreateDirectory(directory);
                var index=opening?_openingMovieFrame++:_endingMovieFrame++;
                GetViewport().GetTexture().GetImage().SaveJpg(System.IO.Path.Combine(directory,$"{index:D5}.jpg"),.95f);
            }
            var threshold=phase switch {"opening-hush"=>.35,"opening-challenger"=>1.12,"opening-answer"=>2.05,"opening-standoff"=>3.18,"ending-reveal"=>.35,"ending-cut"=>.76,_=>1.38};
            if(cinema.CinematicTime>=threshold && _cinemaCaptures.Add(phase))GetViewport().GetTexture().GetImage().SavePng(_capturePath+"-"+phase+".png");
        }
        var stamp=Time.GetTicksUsec();
        if(_duelProbeStep is >=4 and <=18 && _duelFrameStamp>0)_duelFrames.Add((stamp-_duelFrameStamp)/1000d);
        _duelFrameStamp=stamp;
        _captureClock+=delta;if(_captureClock<.7)return;_captureClock=0;
        var host=_duelDemo;
        IEnumerable<Node> Descendants(Node n) {foreach(var child in n.GetChildren()){yield return child;foreach(var item in Descendants(child))yield return item;}}
        Button Find(string text)=>Descendants(host!).OfType<Button>().First(b=>(b.Text==text||b.Name==text)&&b.IsVisibleInTree());
        void Move(Vector2 point)
        {
            // Keep native and injected pointer coordinates aligned during windowed probes.
            GetViewport().WarpMouse(point);
            Input.ParseInputEvent(new InputEventMouseMotion{Position=point,GlobalPosition=point});
        }
        void Native(Vector2 point)
        {
            Move(point);
            Input.ParseInputEvent(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true});
            Input.ParseInputEvent(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false});
        }
        void Press(string text)
        {
            if(text=="Switch seat"){Native(Find("Menu").GetGlobalRect().GetCenter());_deferredDuelButton=text;return;}
            if(text=="Focus from deck"){Native(host!.Screen.PileClickPoint(host.Session.Snapshot!.View.ViewerId,Zone.FateDeck));_deferredDuelButton=text;return;}
            var point=text is "Focus from deck" or "Focus selected card"?host!.Screen.LandingClickPoint:Find(text).GetGlobalRect().GetCenter();
            if(text is "Focus from deck" or "Focus selected card")_duelLastPlacement=point;
            Native(point);
        }
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("DUEL_DEMO_CHECK: "+message);GD.Print("DUEL_DEMO_CHECK: "+message);}
        void Capture(string name){var path=_capturePath!+"-"+name+".png";System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);GetViewport().GetTexture().GetImage().SavePng(path);}
        if(host?.Screen.PresentationBusy==true)return;
        if(_deferredDuelButton is {} deferred)
        {
            _deferredDuelButton=null;
            var point=deferred=="Focus from deck"?host!.Screen.LandingClickPoint:Find(deferred).GetGlobalRect().GetCenter();
            Native(point);if(deferred=="Focus from deck")Native(point);return;
        }
        switch(_duelProbeStep)
        {
            case 0:
                _preferences.ReducedMotion=_captureMode.Contains("reduced");ApplyPreferences(false);
                _session.StartPractice(false,true);_preservedPractice=_session.ExportPracticeReplay()!;
                ShowMainMenu();_titleMenu!.ShowPlay();break;
            case 1:
                Native(_titleMenu!.Entries.GetChildren().OfType<Button>().Single(b=>b.Text.Equals("Duel Demo",StringComparison.OrdinalIgnoreCase)).GetGlobalRect().GetCenter());break;
            case 2:
                Check(host is not null,"Play menu opens in-app demo");Capture("setup");Press("Manual · control both seats");break;
            case 3:Press("Begin Duel");break;
            case 4:
                Check(host!.Session.Snapshot!.View.PendingDuel?.Stage=="Focusing","Begin Duel submits the real challenge");
                Check(host.Screen.OpeningRuns==1,"opening plays once and submits no extra command");
                Check(host.Screen.UprightCount==2&&host.Screen.VisibleDiscardPiles==0,"upright champions with no decorative corner discard piles");
                var champion=host.Session.Snapshot.View.LastDuel!.Challenger;
                if(_uprightProbe==0){var point=host.Screen.CardClickPoint(champion.Id);Move(point);_uprightProbe++;return;}
                if(_uprightProbe==1){Check(host.Screen.HoverPreviewId==champion.Id,"upright champion hover reuses main-game card inspection");Capture("upright-hover");Native(host.Screen.CardClickPoint(champion.Id));_uprightProbe++;return;}
                if(_uprightProbe==2){Check(host.Screen.HasPrivateInspection,"upright champion click opens full inspection");Native(host.Screen.CardClickPoint(champion.Id));Move(new Vector2(20,200));_uprightProbe++;return;}
                Check(!host.Screen.HasPrivateInspection&&host.Session.Snapshot.View.Version==1,"second champion click dismisses inspection without a command");
                Capture("challenge");Press("Switch seat");break;
            case 5:
                Check(!host!.Screen.Visible&&host.Screen.HoverPreviewId is null,"private handoff hides the table and hover preview");Capture("handoff");Press("Reveal this seat");break;
            case 6:Press("Focus from deck");Native(_duelLastPlacement);break;
            case 7:
                Check(host!.Session.Snapshot!.View.Version==2,"rapid duplicate focus advances once");
                if(_focusInspectionStep==0){Check(host.Screen.SelectedInspectionId is null,"duplicate placement does not select the newly committed card");var point=host.Screen.FocusedClickPoint(host.Session.Snapshot.View.ViewerId);Move(point);_focusInspectionStep++;return;}
                if(_focusInspectionStep==1){Check(host.Screen.HoverPreviewId is not null,"hover restores the main-game preview for own committed focus");Check(new Rect2(Vector2.Zero,host.Screen.Size).Encloses(host.Screen.HoverPreviewRect),"hover preview stays within the viewport");Capture("own-focus-hover");Native(host.Screen.FocusedClickPoint(host.Session.Snapshot.View.ViewerId));_focusInspectionStep++;return;}
                if(_focusInspectionStep==2){Press("Inspect selected card");_focusInspectionStep++;return;}
                if(_focusInspectionStep==3){Check(host.Screen.HasPrivateInspection,"blind focus is privately inspectable after commitment");Capture("own-focus-inspection");Press("Close inspection");_focusInspectionStep++;return;}
                Press("Switch seat");break;
            case 8:Press("Reveal this seat");break;
            case 9:
                Capture("quiet-crane");
                var courtesy=host!.Session.Snapshot!.View.Players[0].Hand.Single(c=>c.Definition!.Id=="oracle-l5r-7996");
                Native(host.Screen.CardClickPoint(courtesy.Id));break;
            case 10:
                var selected=host!.Session.Snapshot!.View.Players[0].Hand.Single(c=>c.Definition!.Id=="oracle-l5r-7996");
                if(_selectionProbe==0){Native(host.Screen.CardClickPoint(selected.Id));_selectionProbe++;return;}
                if(_selectionProbe==1){Check(host.Screen.SelectedInspectionId is null&&host.Screen.LandingClickPoint==Vector2.Zero&&host.Session.Snapshot.View.Version==2,"second hand click deselects and removes placement without a command");Native(host.Screen.CardClickPoint(selected.Id));_selectionProbe++;return;}
                if(_selectionProbe==2){Move(new Vector2(20,200));_selectionProbe++;return;}
                Check(!Find("Focus selected card").Disabled,"projected card input selects a legal hand card");Capture("placement-target");Press("Inspect selected card");break;
            case 11:Capture("inspection");Press("Close inspection");break;
            case 12:
                _duelLastPlacement=host!.Screen.LandingClickPoint;
                if(_preferences.ReducedMotion)
                {
                    Input.ParseInputEvent(new InputEventKey{Keycode=Key.Enter,Pressed=true});
                    Input.ParseInputEvent(new InputEventKey{Keycode=Key.Enter,Pressed=false});
                }
                else
                {
                    var from=host.Screen.CardClickPoint(host.Screen.SelectedInspectionId!);
                    Input.ParseInputEvent(new InputEventMouseButton{Position=from,GlobalPosition=from,ButtonIndex=MouseButton.Left,Pressed=true});
                    Input.ParseInputEvent(new InputEventMouseMotion{Position=_duelLastPlacement,GlobalPosition=_duelLastPlacement,Relative=_duelLastPlacement-from});
                    Input.ParseInputEvent(new InputEventMouseButton{Position=_duelLastPlacement,GlobalPosition=_duelLastPlacement,ButtonIndex=MouseButton.Left,Pressed=false});
                }
                Input.FlushBufferedEvents();
                Check(host.Session.Snapshot!.View.Version==3,"direct drag/keyboard placement submits before duplicate click");
                Native(_duelLastPlacement);break;
            case 13:
                Check(host!.Session.Snapshot!.View.Version==3,"drag/keyboard focus and duplicate click advance once");
                if(_focusInspectionStep==4){_inspectionBeforeOpponentClick=host.Screen.SelectedInspectionId;var point=host.Screen.FocusedClickPoint(1-host.Session.Snapshot.View.ViewerId);Move(point);_focusInspectionStep++;return;}
                if(_focusInspectionStep==5){Check(host.Screen.HoverPreviewId is null,"hover never inspects an anonymous opponent focus");Native(host.Screen.FocusedClickPoint(1-host.Session.Snapshot.View.ViewerId));_focusInspectionStep++;return;}
                Check(host.Screen.SelectedInspectionId==_inspectionBeforeOpponentClick&&!host.Screen.HasPrivateInspection,"opponent focused backs expose no inspection");
                Capture("focused");Press("Switch seat");break;
            case 14:Press("Reveal this seat");break;
            case 15:Press("Strike");break;
            case 16:
                if(host!.Screen.PresentationBusy)return;
                var result=host.Session.Snapshot!.View.LastDuel!.Result!;
                Check(host.Screen.VisibleFocusedCount==0,"resolved commitments leave the focus rows");
                if(_resultPileStep==0){Press("Menu");_resultPileStep++;return;}
                if(_resultPileStep==1){Press("Public piles…");_resultPileStep++;return;}
                if(_resultPileStep==2){Press("Your Fate discard");_resultPileStep++;return;}
                if(_resultPileStep==3){Check(host.Screen.HasPrivateInspection,"Fate discard opens an inspectable public spread");Capture("discard-spread");Press("Back");_resultPileStep++;return;}
                if(_resultPileStep==4){Native(host.Screen.FallenClickPoint(host.Session.Snapshot.View.ViewerId));_resultPileStep++;return;}
                if(_resultPileStep==5){Check(host.Screen.HasPrivateInspection,"dead personalities remain separate from Fate discards");Capture("fallen-spread");Press("Back");_resultPileStep++;return;}
                if(_resultPileStep==6){ReadingTooltip.LastText=null;var point=host.Screen.DuelValuePoint(0);Move(point);_resultPileStep++;return;}
                if(_resultPileStep==7){Check(ReadingTooltip.LastText?.Contains("Final duel total:")==true,"brush numeral hover explains final total versus stat contribution");Capture("stat-breakdown");Move(new Vector2(20,200));_resultPileStep++;return;}
                Check(result.ChallengerTotal==8&&result.ChallengedTotal==7,"native UI reproduces canonical 8–7");
                var left=host.Session.Snapshot.View.LastDuel.Challenger.OwnerId==host.Session.Snapshot.View.ViewerId;
                Check(host.Screen.VisibleDuelValues.SequenceEqual(new[]{(left?result.ChallengerTotal:result.ChallengedTotal).ToString(),(left?result.ChallengedTotal:result.ChallengerTotal).ToString()}),"large result numerals match authoritative totals for this seat");
                host.Screen.Present(host.Session.Snapshot);
                Check(!host.Screen.PresentationBusy,"repeated snapshots do not replay the reveal");
                Check(host.Screen.EndingRuns==1,"victory sequence plays once and holds the authoritative result");
                Check(host.Screen.UprightCount==1,"destroyed participant settles out of the upright champion position");
                Check(host.Screen.CanContinue,"result held for Continue");Capture("result");Press("Duel details");break;
            case 17:Capture("consequences");Press("Back");break;
            case 18:Press("Continue");break;
            case 19:
                Check(host!.Session.Snapshot!.View.LastDuel is null,"Continue returns to prepared setup");Press("Return to menu");break;
            case 20:
                Check(_duelDemo is null && MenuVisible,"demo exits to normal L5R menu");
                Check(_session.ExportPracticeReplay()==_preservedPractice,"existing offline practice is preserved unchanged");
                var frames=_duelFrames.Order().ToArray();
                double Percentile(double p)=>frames[Math.Clamp((int)(frames.Length*p),0,frames.Length-1)];
                System.IO.File.WriteAllText(_capturePath!+".json",JsonSerializer.Serialize(new{Passed=true,Steps=_duelProbeStep,ReducedMotion=_preferences.ReducedMotion,Resolution=GetViewportRectSize(),FrameCount=frames.Length,P50Ms=Percentile(.5),P95Ms=Percentile(.95),P99Ms=Percentile(.99)}));
                GetTree().Quit();return;
        }
        _duelProbeStep++;
    }
    private string GetViewportRectSize()=>GetViewport().GetVisibleRect().Size.ToString();
}
