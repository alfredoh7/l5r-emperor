using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private bool _followupPreparing,_followupReady;
    private double _followupClock;
    private bool ProcessDuelFollowupReview(double delta)
    {
        if(!_followupPreparing&&!_followupReady)return false;
        if(_followupReady)return _capturePath is not null&&RunDuelFollowupProbe(delta);
        _followupClock+=delta;
        if(_matchDuel is null||_matchDuelPreparing||_matchDuel.Screen.PresentationBusy||_session.Busy||_followupClock<.25)return true;
        _followupClock=0;
        var snapshot=_session.Snapshot!;
        if(_matchProbeScenario=="concede"||snapshot.View.PendingChoice?.Type=="focusEffectChoice")
        {
            _followupPreparing=false;_followupReady=true;
            GD.Print("DUEL_FOLLOWUP_READY: "+_matchProbeScenario);
            return true;
        }
        GameCommand? Choose(MatchView view,ActionOption[] actions)
        {
            var legal=actions.Where(a=>a.Enabled&&a.Command is not null).ToArray();
            if(view.PendingDuel?.Stage=="Focusing")
            {
                var hand=view.Players[view.ViewerId].Hand;
                var own=legal.FirstOrDefault(a=>a.Type=="focus"&&hand.Any(c=>c.Id==a.Command!.CardId&&(c.Definition!.Abilities??[]).Any(id=>AbilityCatalog.All.TryGetValue(id,out var def)&&def.Timing=="FocusEffect")));
                if(view.ViewerId==0&&own is not null)return own.Command;
                if(view.ViewerId==1&&view.Players[1].FocusCount<(_matchProbeScenario=="choice-multiple"?2:1))return legal.FirstOrDefault(a=>a.Type=="focus"&&a.Command!.Choice=="deck")?.Command;
                return legal.FirstOrDefault(a=>a.Type=="strike")?.Command;
            }
            return legal.FirstOrDefault(a=>a.Type is "orderDuelFocusEffects" or "acceptDuel" or "declineReaction")?.Command;
        }
        if(snapshot.View.PriorityPlayerId!=snapshot.View.ViewerId)_session.StepPracticeOpponent(Choose);
        else if(Choose(snapshot.View,snapshot.Actions) is {} command)_ = _session.Submit(command);
        else throw new InvalidOperationException("Prepared duel could not reach its offered Focus Effect choice.");
        return true;
    }
}
