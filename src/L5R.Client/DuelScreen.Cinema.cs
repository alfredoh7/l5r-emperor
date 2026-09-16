using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private enum Cinema {None,Opening,Ending}
    private Cinema _cinema;
    private double _cinemaTime;
    private bool _openingSeen,_cutPlayed;
    private Control _cinemaLayer=null!;
    private Label _cinemaTitle=null!;
    private ColorRect _cinemaShade=null!;
    private TextureRect _cinemaSlash=null!;
    private Button _skipCinema=null!;
    private AudioStreamPlayer _cinemaSound=null!,_windSound=null!;
    private static AudioStreamWav? _windClip;
    public bool SoundEnabled {get;set;}=true;
    internal bool CinematicBusy=>_cinema!=Cinema.None;
    internal double CinematicTime=>_cinemaTime;
    internal string CinematicPhase=>_cinema switch {
        Cinema.Opening=>_cinemaTime<.7?"opening-hush":_cinemaTime<1.6?"opening-challenger":_cinemaTime<2.5?"opening-answer":"opening-standoff",
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
        _cinemaSound=new AudioStreamPlayer{VolumeDb=-22};AddChild(_cinemaSound);
        _windSound=new AudioStreamPlayer{VolumeDb=-48,Stream=_windClip??=BuildWind()};AddChild(_windSound);_cinemaLayer.Hide();
    }
    private void ResetCamera()
    {
        if(_scene is null)return;_scene.Scale=Vector2.One;_scene.Position=Vector2.Zero;
        if(_uprights is not null)_uprights.Position=_offset;
    }
    private void ResetCinema()
    {
        _cinema=Cinema.None;_cinemaTime=0;_openingSeen=false;_cutPlayed=false;OpeningRuns=EndingRuns=0;
        ResetCamera();_cinemaLayer?.Hide();_cinemaSound?.Stop();_windSound?.Stop();
        if(_flights is not null)foreach(var child in _flights.GetChildren()){_flights.RemoveChild(child);child.QueueFree();}
    }
    private void StartOpening()
    {
        _openingSeen=true;OpeningRuns++;
        if(ReducedMotion)return;
        _cinema=Cinema.Opening;_cinemaTime=0;HideCardHover();CloseGuidance();
        _cinemaLayer.Show();PlayCinemaSound(false);
    }
    private void StartEnding()
    {
        EndingRuns++;
        if(ReducedMotion||Snapshot!.View.LastDuel!.Result!.Kind!="Resolved")
        {_aftermathPlayed=true;RenderCards(false);RenderActions();return;}
        _cinema=Cinema.Ending;_cinemaTime=0;_cutPlayed=false;HideCardHover();CloseGuidance();_cinemaLayer.Show();RenderActions();
    }
    public void SkipCinematic()
    {
        if(!CinematicBusy)return;
        if(_cinema==Cinema.Ending){_aftermathPlayed=true;_cleanupClock=0;}
        _cinema=Cinema.None;ResetCamera();_cinemaLayer.Hide();_cinemaSound.Stop();
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
        if(ReducedMotion){SkipCinematic();return;}
        _cinemaTime+=delta;var t=_cinemaTime;
        _cinemaLayer.MoveToFront();_cinemaLayer.Size=Size;_cinemaShade.Size=Size;
        _skipCinema.Position=_offset+new Vector2(1500,24)*_scale;_skipCinema.Size=new Vector2(120,40)*_scale;
        _cinemaTitle.AddThemeFontSizeOverride("font_size",Math.Max(30,(int)(75*_scale)));
        _cinemaSlash.Visible=false;_cinemaTitle.Text="";_cinemaShade.Color=Colors.Transparent;
        if(_cinema==Cinema.Opening)
        {
            var center=DuelCinematicStage.Design/2;var left=new Vector2(700,325);var right=new Vector2(975,325);
            if(t<.7)
            {CameraAt(center+Vector2.Down*20,1.035f);_uprights.Position=_offset+Vector2.Down*(1-Ease(t/.7))*65*_scale;}
            else
            {
                _uprights.Position=_offset;
                var challenger=Snapshot!.View.LastDuel!.Challenger;var challenged=Snapshot.View.LastDuel.Challenged;
                var first=challenger.OwnerId==Snapshot.View.ViewerId?left:right;
                var second=challenged.OwnerId==Snapshot.View.ViewerId?left:right;
                Vector2 target;float zoom;
                if(t<1.6){var p=Ease((t-.7)/.28);target=center.Lerp(first,p);zoom=Mathf.Lerp(1.035f,1.55f,p);Title(challenger,first==left,t-.7);}
                else if(t<2.5){var p=Ease((t-1.6)/.30);target=first.Lerp(second,p);zoom=1.55f;Title(challenged,second==left,t-1.6);}
                else{var p=Ease((t-2.5)/.65);target=second.Lerp(center,p);zoom=Mathf.Lerp(1.55f,1,p);}
                CameraAt(target,zoom);
            }
            var active=t is >=.7 and <1.6?Snapshot!.View.LastDuel!.Challenger.OwnerId:t is >=1.6 and <2.5?Snapshot!.View.LastDuel!.Challenged.OwnerId:-1;
            foreach(var tile in _tiles.Values.Where(c=>c.HasMeta("upright")))
            {
                var emphasis=active<0||tile.Card.OwnerId==active?1f:.72f;tile.PivotOffset=tile.Size/2;
                tile.Scale=Vector2.One*Mathf.Lerp(tile.Scale.X,emphasis,Math.Min(1,(float)delta*10));
                var light=active<0||tile.Card.OwnerId==active?1f:.57f;tile.Modulate=new Color(light,light,light,1);
            }
            foreach(var tile in _tiles.Values.Where(c=>c.Card.Zone==Zone.Hand))tile.Modulate=new Color(1,1,1,Ease((t-2.8)/.5));
            if(t>=3.4)SkipCinematic();
            void Title(CardView card,bool leftSide,double elapsed)
            {
                _cinemaTitle.Text=card.Definition!.Name;_cinemaTitle.Position=_offset+new Vector2(leftSide?990:140,120)*_scale;
                _cinemaTitle.Size=new Vector2(540,185)*_scale;_cinemaTitle.Modulate=new Color(1,1,1,Ease(elapsed/.25));
                _cinemaSlash.Visible=true;_cinemaSlash.Position=_cinemaTitle.Position+new Vector2(90,78)*_scale;
                _cinemaSlash.Size=new Vector2(210,32)*_scale;_cinemaSlash.Modulate=_cinemaTitle.Modulate;
            }
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
            if(t>=1.8){_cleanupClock=0;SkipCinematic();}
        }
    }
    private void AdvanceCinemaSound(double delta)
    {
        if(!SoundEnabled||ReducedMotion||Snapshot?.View.LastDuel is null)
        {_windSound.Stop();if(!SoundEnabled)_cinemaSound.Stop();return;}
        if(!_windSound.Playing)_windSound.Play();
        var hush=_cinema==Cinema.Opening||(_cinema==Cinema.Ending&&_cinemaTime<1.5);
        _windSound.VolumeDb=Mathf.MoveToward(_windSound.VolumeDb,hush?-56:-35,(float)delta*18);
    }
    private static AudioStreamWav BuildWind()
    {
        const int rate=22050,samples=rate*12;var bytes=new byte[samples*2];var random=new Random(20517);double low=0;
        for(var i=0;i<samples;i++)
        {
            low=low*.976+(random.NextDouble()*2-1)*.024;
            var phase=i/(double)samples;var seam=Math.Min(1,Math.Min(phase,1-phase)*24);
            var gust=.65+.2*Math.Sin(phase*Math.Tau*3)+.15*Math.Sin(phase*Math.Tau*7);
            var pcm=(short)(low*gust*seam*22000);bytes[i*2]=(byte)(pcm&255);bytes[i*2+1]=(byte)((pcm>>8)&255);
        }
        return new AudioStreamWav{Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=rate,Stereo=false,Data=bytes,LoopMode=AudioStreamWav.LoopModeEnum.Forward,LoopEnd=samples};
    }
    private void PlayCinemaSound(bool cut)
    {
        if(!SoundEnabled||ReducedMotion)return;
        // Quiet deterministic paper/air accents. No engine event is inferred from audio.
        const int rate=22050;var length=cut?.19:.65;var samples=(int)(rate*length);var bytes=new byte[samples*2];
        var random=new Random(cut?2058:2054);double low=0;
        for(var i=0;i<samples;i++)
        {
            var t=i/(double)rate;var noise=random.NextDouble()*2-1;low=low*.86+noise*.14;
            var envelope=Math.Sin(Math.PI*i/samples)*Math.Exp(-t*(cut?14:3));
            var value=(cut?noise*.5+low*.5:low)*envelope;
            var pcm=(short)(value*16000);bytes[i*2]=(byte)(pcm&255);bytes[i*2+1]=(byte)((pcm>>8)&255);
        }
        _cinemaSound.Stream=new AudioStreamWav{Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=rate,Stereo=false,Data=bytes};_cinemaSound.Play();
    }
}
