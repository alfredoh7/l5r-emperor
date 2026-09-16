using Godot;
using L5R.Core;
using System.Text.Json;
namespace L5R.Client;

public partial class Main
{
    private int _guidanceProbe;
    private long _guidanceVersion;
    private long? _cinemaSkipVersion;
    private void ProcessDuelGuidanceCapture(double delta)
    {
        _captureClock+=delta;if(_captureClock<.65)return;_captureClock=0;
        IEnumerable<Node> Descendants(Node n){foreach(var c in n.GetChildren()){yield return c;foreach(var d in Descendants(c))yield return d;}}
        var host=_duelDemo;
        Button Find(string name)=>Descendants(host!).OfType<Button>().First(b=>(b.Name==name||b.Text==name)&&b.IsVisibleInTree());
        void Native(Vector2 p){Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p});foreach(var pressed in new[]{true,false})Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=pressed});}
        void Press(string text)=>Native(Find(text).GetGlobalRect().GetCenter());
        void Check(bool ok,string message){if(!ok)throw new Exception("DUEL_GUIDANCE_CHECK: "+message);GD.Print("DUEL_GUIDANCE_CHECK: "+message);}
        void Capture(string suffix)=>GetViewport().GetTexture().GetImage().SavePng(_capturePath+"-"+suffix+".png");
        bool Saved(){var preferences=new MenuPreferences(_preferencesProfile);preferences.Load();return preferences.DuelGuidance;}
        if(_cinemaSkipVersion is {} skippedVersion)
        {
            Check(!host!.Screen.CinematicBusy&&!host.Screen.MastheadVisible&&host.Session.Snapshot!.View.Version==skippedVersion,"Skip ends the cinematic and removes the DUEL title without submitting a command");_cinemaSkipVersion=null;
        }
        if(host?.Screen.CinematicBusy==true)
        {
            _cinemaSkipVersion=host.Session.Snapshot!.View.Version;Capture("skip-control");Press("Skip cinematic");Input.FlushBufferedEvents();return;
        }
        switch(_guidanceProbe)
        {
            case 0:_preferences.ReducedMotion=_captureMode.Contains("reduced");ApplyPreferences(false);_titleMenu!.ShowPlay();break;
            case 1:Native(_titleMenu!.Entries.GetChildren().OfType<Button>().Single(b=>b.Text=="DUEL DEMO").GetGlobalRect().GetCenter());break;
            case 2:Press("Manual · control both seats");break;
            case 3:Press("Begin Duel");break;
            case 4:Press("Menu");break;
            case 5:Press("Duel guidance…");break;
            case 6:
                Check(host!.Screen.AnchoredHelpVisible&&!host.Screen.CoachingVisible,"help is available while waiting without a false focus choice");
                Check(host.Screen.GuidanceTitle=="This moment","help follows the viewer’s legal options");Capture("waiting-help");Press("Close duel guidance");break;
            case 7:Press("Menu");break;
            case 8:Press("Switch seat");break;
            case 9:Check(!host!.Screen.Visible,"seat handoff hides cards and guidance");Press("Reveal this seat");break;
            case 10:
                Check(host!.Screen.CoachingVisible,"quiet coaching appears for the active seat");
                Capture("quiet");GD.Print("GUIDANCE_RECT: "+host.Screen.GuidanceRect);
                Check(host.Screen.GuidanceRect.Size.Y<200&&host.Screen.GuidanceRect.End.Y<GetViewport().GetVisibleRect().Size.Y*.40f,"quiet coaching stays compact above the card surface");
                Check(!Find("HideDuelGuidance").ButtonPressed,"do not show again starts unchecked");
                _guidanceVersion=host.Session.Snapshot!.View.Version;Capture("quiet");Press("HideDuelGuidance");break;
            case 11:
                Check(host!.Screen.GuidanceEnabled&&_preferences.DuelGuidance,"checking alone does not persist suppression");
                Check(host.Session.Snapshot!.View.Version==_guidanceVersion,"checkbox input does not click through to gameplay");Press("Got it");break;
            case 12:
                Check(!host!.Screen.GuidanceEnabled&&!Saved(),"explicit dismissal persists in the existing profile");
                Check(!host.Screen.CoachingVisible&&host.Screen.EssentialStatus.Length>0,"opt-out hides coaching and retains essential turn status");Capture("hidden");Press("Menu");break;
            case 13:Press("Duel guidance…");break;
            case 14:
                Check(host!.Screen.AnchoredHelpVisible&&!host.Screen.GuidanceEnabled,"manual help remains available after opt-out");
                Check(host.Screen.GuidanceTitle=="Choose your focus","first help anchors to a legal hand card");Capture("hand-help");Press("Next duel guidance");break;
            case 15:
                Check(host!.Screen.GuidanceTitle=="Focus unseen","deck help explains blind commitment");Capture("deck-help");
                Native(host.Screen.GuidanceRect.GetCenter());break;
            case 16:
                Check(host!.Session.Snapshot!.View.Version==_guidanceVersion,"reading a guide does not submit a command");Press("Next duel guidance");break;
            case 17:
                Check(host!.Screen.GuidanceTitle=="Make your stand","Strike help matches the legal action");Capture("strike-help");Find("Next duel guidance").GrabFocus();
                foreach(var pressed in new[]{true,false})Input.ParseInputEvent(new InputEventKey{Keycode=Key.Enter,Pressed=pressed});break;
            case 18:Check(host!.Session.Snapshot!.View.Version==_guidanceVersion,"keyboard help dismissal does not Strike");Check(!host.Screen.AnchoredHelpVisible&&!host.Screen.CoachingVisible,"finishing optional help keeps opt-out");Press("Menu");break;
            case 19:Press("Show quiet coaching");break;
            case 20:Check(host!.Screen.GuidanceEnabled&&Saved(),"Menu restores and persists quiet coaching");Press("Resume");break;
            case 21:Check(host!.Screen.CoachingVisible,"restored coaching is immediately available");Press("Got it");break;
            case 22:Check(host!.Screen.GuidanceEnabled&&Saved()&&!host.Screen.CoachingVisible,"unchecked Got it dismisses only this encounter");Press("Menu");break;
            case 23:Press("Restart");break;
            case 24:Press("Begin Duel");break;
            case 25:
                Check(host!.Screen.CoachingVisible,"restart restores optional coaching for a new encounter");Capture("restart");Press("HideDuelGuidance");break;
            case 26:Press("Got it");break;
            case 27:Press("Menu");break;
            case 28:Press("Return to menu");break;
            case 29:
                _preferences.Load();Check(!_preferences.DuelGuidance,"suppression survives a profile reload");_titleMenu!.ShowPlay();break;
            case 30:Native(_titleMenu!.Entries.GetChildren().OfType<Button>().Single(b=>b.Text=="DUEL DEMO").GetGlobalRect().GetCenter());break;
            case 31:Check(!host!.Screen.GuidanceEnabled,"reopened demo uses the persisted setting");Press("Manual · control both seats");break;
            case 32:Press("Begin Duel");break;
            case 33:Press("Menu");break;
            case 34:Press("Switch seat");break;
            case 35:Press("Reveal this seat");break;
            case 36:Press("Menu");break;
            case 37:Press("Duel guidance…");break;
            case 38:
                Check(host!.Screen.AnchoredHelpVisible&&!host.Screen.GuidanceEnabled,"manual help works again after reopening the demo");Press("Close duel guidance");break;
            case 39:
                Check(host!.Session.Snapshot!.View.Version==1,"help, settings and restart perform no extra game commands");
                Press("Menu");break;
            case 40:Press("Challenge details…");break;
            case 41:
                var text=string.Join("\n",Descendants(host!).OfType<Label>().Select(l=>l.Text));
                Check(text.Contains("cannot be refused")&&text.Contains("losing unit is destroyed"),"challenge facts remain available after Skip and guidance opt-out");
                Capture("challenge-details");Press("Inspect Steel on Steel");break;
            case 42:
                Check(Descendants(host!).OfType<CardTile>().Any(c=>c.Card.Definition?.Id=="oracle-l5r-7483"),"challenge source opens the shared full-card inspection");
                Capture("challenge-source");Press("Close inspection");break;
            case 43:
                Check(host!.Session.Snapshot!.View.Version==1,"challenge and source inspection submit no command");
                System.IO.File.WriteAllText(_capturePath+".json",JsonSerializer.Serialize(new{Passed=true,Steps=_guidanceProbe,ReducedMotion=_preferences.ReducedMotion,Resolution=GetViewportRectSize(),ProfileIsolated=_preferencesProfile.StartsWith("menu-capture-")}));
                GetTree().Quit();return;
        }
        _guidanceProbe++;
    }
}
