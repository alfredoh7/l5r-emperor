using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private enum Cinema {None,Opening,Ending,Aftermath}
    private Cinema _cinema;
    private double _cinemaTime;
    private bool _openingSeen,_cutPlayed;
    private Control _cinemaLayer=null!;
    private Label _cinemaTitle=null!;
    private ColorRect _cinemaShade=null!;
    private TextureRect _cinemaSlash=null!;
    private Button _skipCinema=null!;
    private DuelIntroStage? _introStage;
    private TextureRect? _masthead;
    internal bool MastheadVisible=>_masthead?.Visible==true && _masthead.Modulate.A>.01f;
    private void PlaceMasthead()
    {
        if(_masthead is null){_masthead=new TextureRect{Name="Duel masthead",Texture=GD.Load<Texture2D>("res://Art/DuelUi/duel-wordmark.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore};AddChild(_masthead);}
        _masthead.Position=_offset+new Vector2(616,15)*_scale;_masthead.Size=new Vector2(440,147)*_scale;
        // The title belongs to the introduction, never to the interactive arena/result.
        var alpha=_cinema==Cinema.Opening&&!ReducedMotion&&_cinemaTime>=StakesDuration?1-Ease((_cinemaTime-StakesDuration-2.5)/.75):0;
        _masthead.Modulate=new Color(1,1,1,alpha);_masthead.Visible=alpha>0;_masthead.MoveToFront();
    }
    private void ClearIntro()
    {
        if(_introStage is null)return;_cinemaLayer.RemoveChild(_introStage);_introStage.QueueFree();_introStage=null;
    }
    private AudioStreamPlayer _cinemaSound=null!,_windSound=null!,_paperSound=null!;
    public bool AmbienceEnabled {get;set;}=true;
    public bool DetailedAtmosphere {get=>_stage.DetailedAtmosphere;set=>_stage.DetailedAtmosphere=value;}
    internal double AtmosphereTime=>_stage.AtmosphereTime;
    internal int GrassInstances=>_stage.GrassInstances;
    internal double OpeningLeadIn {get;set;}
    private float _windStrength=1;
    public bool SoundEnabled {get;set;}=true;
    internal bool CinematicBusy=>_cinema!=Cinema.None;
    internal double CinematicTime=>_cinemaTime;
    internal string CinematicPhase=>_cinema switch {
        Cinema.Opening=>_cinemaTime<StakesDuration?"opening-stakes":_cinemaTime-StakesDuration<.7?"opening-hush":_cinemaTime-StakesDuration<1.6?"opening-challenger":_cinemaTime-StakesDuration<2.5?"opening-answer":"opening-standoff",
        Cinema.Aftermath=>"aftermath-effect",
        Cinema.Ending=>_cinemaTime<.7?"ending-reveal":_cinemaTime<.9?"ending-cut":"ending-consequence",_=>"idle"};
    internal int OpeningRuns {get;private set;}
    internal int EndingRuns {get;private set;}

    private void CreateCinema()
    {
        _cinemaLayer=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(_cinemaLayer);
        _cinemaShade=new ColorRect{Color=Colors.Transparent,MouseFilter=MouseFilterEnum.Ignore};_cinemaLayer.AddChild(_cinemaShade);
        _cinemaSlash=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/DuelUi/red-slash.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore};_cinemaLayer.AddChild(_cinemaSlash);
        _cinemaTitle=DuelUi.Text("",75,Palette.Paper,true);_cinemaTitle.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        _cinemaTitle.AddThemeColorOverride("font_shadow_color",Colors.Black);_cinemaTitle.AddThemeConstantOverride("shadow_offset_y",3);_cinemaLayer.AddChild(_cinemaTitle);
        _skipCinema=DuelUi.Button("Skip",SkipCinematic,fontSize:20);_skipCinema.Name="Skip cinematic";_skipCinema.TooltipText="Skip presentation · Space or Escape";_cinemaLayer.AddChild(_skipCinema);
        _cinemaSound=new AudioStreamPlayer{VolumeDb=-13};AddChild(_cinemaSound);
        _windSound=new AudioStreamPlayer{VolumeDb=-30,Stream=DuelAudio.Wind};AddChild(_windSound);
        _paperSound=new AudioStreamPlayer{VolumeDb=-12};AddChild(_paperSound);
        _=DuelAudio.Accent(true);_=DuelAudio.Accent(false);_=DuelAudio.Paper(-1);_=DuelAudio.Paper(1);_cinemaLayer.Hide();
    }
    private void ResetCamera()
    {
        if(_scene is null)return;_scene.Scale=Vector2.One;_scene.Position=Vector2.Zero;
        if(_uprights is not null)_uprights.Position=_offset;
    }
    private void ResetCinema()
    {
        ClearStory();_seenBeats.Clear();_activeBeats=[];
        _cinema=Cinema.None;_cinemaTime=0;_openingSeen=false;_cutPlayed=false;OpeningRuns=EndingRuns=0;
        ClearIntro();ResetCamera();_cinemaLayer?.Hide();_cinemaSound?.Stop();_windSound?.Stop();_paperSound?.Stop();
        if(_flights is not null)foreach(var child in _flights.GetChildren()){_flights.RemoveChild(child);child.QueueFree();}
    }
    private void StartOpening()
    {
        _openingSeen=true;OpeningRuns++;
        _cinema=Cinema.Opening;_cinemaTime=-OpeningLeadIn;HideCardHover();CloseGuidance();
        ClearIntro();
        // Prepare the public-card viewport under the stakes panel, before its camera cut.
        if(!ReducedMotion)
        {
            var record=Snapshot!.View.LastDuel!;
            _introStage=new DuelIntroStage();_cinemaLayer.AddChild(_introStage);_cinemaLayer.MoveChild(_introStage,0);
            _introStage.Build(record,Snapshot.View.ViewerId);
            _introStage.Advance(0,Size,record.Challenger.OwnerId,record.Challenged.OwnerId,_stage.AtmosphereTime,_windStrength,DetailedAtmosphere);
            _introStage.Hide();
        }
        _cinemaLayer.Show();
    }
    private void StartEnding()
    {
        EndingRuns++;PrepareBeats();
        if(ReducedMotion||Snapshot!.View.LastDuel!.Result!.Kind!="Resolved")
        {_aftermathPlayed=true;FinishBeats();RenderCards(false);RenderActions();return;}
        _cinema=Cinema.Ending;_cinemaTime=0;_cutPlayed=false;HideCardHover();CloseGuidance();_cinemaLayer.Show();RenderActions();
    }
    public void SkipCinematic()
    {
        if(!CinematicBusy)return;
        if(_cinema==Cinema.Ending){_aftermathPlayed=true;_cleanupClock=0;}
        FinishBeats();_cinema=Cinema.None;ClearIntro();ResetCamera();_cinemaLayer.Hide();_cinemaSound.Stop();
        foreach(var c in _flights.GetChildren()){_flights.RemoveChild(c);c.QueueFree();}
        RenderCards(false);RenderActions();
    }
    private static float Ease(double value){var t=Math.Clamp((float)value,0,1);return t*t*(3-2*t);}
    private void CameraAt(Vector2 target,float zoom)
    {
        _scene.Scale=Vector2.One*zoom;
        _scene.Position=Size/2-(_offset+target*_scale)*zoom;
    }
    private void AdvanceCinema(double delta)
    {
        if(InputLocked||_reading is not null)return;
        if(ReducedMotion&&_cinema!=Cinema.Opening){SkipCinematic();return;}
        _cinemaTime+=delta;var t=_cinemaTime;
        _cinemaLayer.MoveToFront();PlaceMasthead();_cinemaLayer.Size=Size;_cinemaShade.Size=Size;
        _skipCinema.Position=_offset+new Vector2(1500,24)*_scale;_skipCinema.Size=new Vector2(120,40)*_scale;
        _cinemaTitle.AddThemeFontSizeOverride("font_size",Math.Max(30,(int)(75*_scale)));
        _cinemaSlash.Visible=false;_cinemaTitle.Text="";_cinemaShade.Color=Colors.Transparent;
        if(_cinema==Cinema.Opening)
        {
            if(t<0)return;
            if(t<StakesDuration){ShowStakes();return;}
            ClearStory();
            if(ReducedMotion){SkipCinematic();return;}
            t-=StakesDuration;
            if(_introStage is null&&t<2.5)
            {
                _introStage=new DuelIntroStage();_cinemaLayer.AddChild(_introStage);_cinemaLayer.MoveChild(_introStage,0);
                _introStage.Build(Snapshot!.View.LastDuel!,Snapshot.View.ViewerId);
            }
            if(t<2.5&&_introStage is {Visible:false}){_introStage.Show();PlayCinemaSound(false);}
            var challenger=Snapshot!.View.LastDuel!.Challenger;var challenged=Snapshot.View.LastDuel.Challenged;
            if(t<2.5)_introStage?.Advance(t,Size,challenger.OwnerId,challenged.OwnerId,_stage.AtmosphereTime,_windStrength,DetailedAtmosphere);
            else
            {
                // A film cut to the exact playable composition avoids superimposed card faces.
                ClearIntro();CameraAt(DuelCinematicStage.Design/2,Mathf.Lerp(1.035f,1,Ease((t-2.5)/.9)));
            }
            if(t is >=.7 and <1.6)Title(challenger,t-.7);
            if(t is >=1.6 and <2.5)Title(challenged,t-1.6);
            foreach(var tile in _tiles.Values.Where(c=>c.Card.Zone==Zone.Hand))tile.Modulate=new Color(1,1,1,Ease((t-2.65)/.5));
            if(t>=3.4)SkipCinematic();
            void Title(CardView card,double elapsed)
            {
                _cinemaTitle.Text=card.Definition!.Name;
                _cinemaTitle.Size=new Vector2(540,110)*_scale;
                var anchor=_introStage!.NameAnchor(card.OwnerId)+Vector2.Right*(card.OwnerId==Snapshot!.View.ViewerId?-90:90)*_scale;
                var titleMin=_offset+new Vector2(30,110)*_scale;
                // Wrapped labels may retain their previous minimum height for one layout frame.
                var titleMax=(Size-_cinemaTitle.Size-Vector2.One*30*_scale).Max(titleMin);
                _cinemaTitle.Position=(anchor-_cinemaTitle.Size/2).Clamp(titleMin,titleMax);
                _cinemaTitle.HorizontalAlignment=HorizontalAlignment.Center;
                _cinemaTitle.AddThemeFontSizeOverride("font_size",Math.Max(30,(int)(68*_scale)));
                _cinemaTitle.Modulate=new Color(1,1,1,Ease((elapsed-.12)/.18));
                _cinemaSlash.Visible=true;_cinemaSlash.Position=_cinemaTitle.Position+new Vector2(165,74)*_scale;
                _cinemaSlash.Size=new Vector2(210,28)*_scale;_cinemaSlash.Modulate=_cinemaTitle.Modulate;
            }
        }
        else if(_cinema==Cinema.Aftermath)
        {
            var index=(int)(t/BeatDuration);
            if(index>=_activeBeats.Length){SkipCinematic();return;}
            ShowBeat(_activeBeats[index]);
        }
        else
        {
            if(t>=.7&&!_cutPlayed){_cutPlayed=true;PlayCinemaSound(true);}
            if(t>=.7&&t<.9)
            {
                var opacity=Mathf.Sin((float)((t-.7)/.2)*Mathf.Pi);
                _cinemaShade.Color=new Color(0,0,0,opacity*.22f);
                _cinemaSlash.Visible=true;_cinemaSlash.Position=_offset+new Vector2(704,360)*_scale;
                _cinemaSlash.Size=new Vector2(280,235)*_scale;_cinemaSlash.Modulate=new Color(1,1,1,opacity);
            }
            if(t>=.9&&!_aftermathPlayed)
            {_aftermathPlayed=true;_cleanupClock=.8;RenderCards();RenderActions();}
            if(t>=.9)
            {
                var record=Snapshot!.View.LastDuel!;var result=record.Result!;
                _cinemaTitle.Text=result.WinnerId is null?"Both lose":$"{(result.WinnerId==record.Challenger.Id?record.Challenger:record.Challenged).Definition!.Name} wins";
                _cinemaTitle.Position=_offset+new Vector2(606,315)*_scale;_cinemaTitle.Size=new Vector2(460,112)*_scale;
                _cinemaTitle.HorizontalAlignment=HorizontalAlignment.Center;_cinemaTitle.Modulate=Colors.White;
                _cinemaTitle.AddThemeFontSizeOverride("font_size",Math.Max(28,(int)(50*_scale)));
            }
            if(t>=1.0&&_activeBeats.Length>0)
            {
                var index=Math.Min((int)((t-1.0)/BeatDuration),_activeBeats.Length-1);ShowBeat(_activeBeats[index]);
            }
            if(t>=1.8+_activeBeats.Length*BeatDuration){_cleanupClock=0;SkipCinematic();}
        }
    }
    private void AdvanceCinemaSound(double delta)
    {
        var paused=InputLocked||_reading is not null;
        _windSound.StreamPaused=paused;_cinemaSound.StreamPaused=paused;_paperSound.StreamPaused=paused;
        var hush=_cinema==Cinema.Opening&&_cinemaTime>=0||(_cinema==Cinema.Ending&&_cinemaTime<1.5);
        if(!paused)_windStrength=Mathf.MoveToward(_windStrength,hush?.28f:1,(float)delta*.65f);
        _stage.WindStrength=_windStrength;
        if(!SoundEnabled){_cinemaSound.Stop();_paperSound.Stop();}
        if(!AmbienceEnabled||ReducedMotion||Snapshot?.View.LastDuel is null){_windSound.Stop();return;}
        if(!_windSound.Playing)_windSound.Play();
        _windSound.VolumeDb=Mathf.MoveToward(_windSound.VolumeDb,hush?-33:-20,(float)delta*12);
    }
    private void PlayCinemaSound(bool cut)
    {
        if(!SoundEnabled||ReducedMotion)return;
        _cinemaSound.Stream=DuelAudio.Accent(cut);_cinemaSound.Play();
    }
    private void PlayFocusSound(int owner)
    {
        if(!SoundEnabled||ReducedMotion)return;
        _paperSound.Stream=DuelAudio.Paper(owner==Snapshot!.View.ViewerId?-1:1);_paperSound.Play();
    }
}
