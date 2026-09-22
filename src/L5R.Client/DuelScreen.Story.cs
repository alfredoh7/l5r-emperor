using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private Control? _story;
    private string? _storyKey;
    private readonly HashSet<int> _seenBeats=[];
    private DuelBeat[] _activeBeats=[];
    private const double StakesDuration=3.4, BeatDuration=3.2;
    internal string StoryText {get;private set;}="";
    internal int PresentedBeatCount=>_seenBeats.Count;
    private static string StatName(string stat)=>stat=="PersonalHonor"?"Personal Honor":stat;

    private void ClearStory()
    {
        if(_story is not null&&GodotObject.IsInstanceValid(_story)){_story.GetParent()?.RemoveChild(_story);_story.QueueFree();}
        _story=null;
        _storyKey=null;StoryText="";
        foreach(var tile in _tiles.Values)tile.Modulate=Colors.White;
    }
    private void ShowStory(string key, string heading, CardView? source, string[] lines, bool compact=false, string? sourceLabel=null)
    {
        if(_storyKey==key)return;
        ClearStory();_storyKey=key;StoryText=heading+"\n"+string.Join("\n",lines);
        var design=compact?new Vector2(410,source is null?180:245):new Vector2(790,300);
        var origin=compact?new Vector2(631,65):new Vector2(441,320);
        _story=new Control{Name="Duel story",Position=_offset+origin*_scale,Size=design,Scale=Vector2.One*_scale,MouseFilter=MouseFilterEnum.Ignore};
        Control parent=CinematicBusy?_cinemaLayer:this;parent.AddChild(_story);
        var panel=DuelUi.Surface(_story,compact?20:24);panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);panel.MouseFilter=MouseFilterEnum.Ignore;
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",compact?12:22);panel.AddChild(row);
        if(source?.Definition is not null)
        {
            var height=compact?155:235;
            var slot=new Control{CustomMinimumSize=new Vector2(height*.74f,height),MouseFilter=MouseFilterEnum.Ignore};row.AddChild(slot);
            var card=new CardTile(source with {Bowed=false},true,_=>{},_=>{}){Size=Vector2.One*(height+12),FaceHeight=height,ShowTokenRail=false,ReducedMotion=true,Material=CardPaper.Reading,MouseFilter=MouseFilterEnum.Ignore};
            card.Position=new Vector2((slot.CustomMinimumSize.X-card.Size.X)/2,0);slot.AddChild(card);
        }
        var words=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};words.AddThemeConstantOverride("separation",compact?9:12);row.AddChild(words);
        words.AddChild(DuelUi.Wrapped(heading,compact?18:20,Palette.PaperDim));
        var title=DuelUi.Wrapped(source?.Definition?.Name??sourceLabel??"Duel",compact?34:38);title.AddThemeFontOverride("font",DuelUi.Heading);words.AddChild(title);
        foreach(var line in lines.Take(compact?3:4))words.AddChild(DuelUi.Wrapped(line,compact?20:24));
        if(compact&&lines.Length>3)words.AddChild(DuelUi.Wrapped("More in Duel details",15,Palette.PaperDim));
        IgnoreHover(_story);
    }
    private void ShowStakes(bool compact=false)
    {
        if(Snapshot?.View.LastDuel is not {Challenge:{} challenge} record)return;
        var fixture=challenge.Source?.Definition?.Id.StartsWith("training-",StringComparison.Ordinal)==true;
        ShowStory("stakes:"+compact,fixture?"Training challenge":"The challenge",challenge.Source,
            [$"{StatName(record.Stat)} duel",challenge.RefusalTerms,challenge.LossTerms],compact);
    }
    private void ShowBeat(DuelBeat beat)
    {
        _seenBeats.Add(beat.Index);
        ShowStory("beat:"+beat.Index,beat.Kind switch {"FocusEffect"=>"Focus Effect","Aftermath"=>"After the duel",_=>"The consequence"},beat.Source,beat.Lines,true,beat.SourceLabel);
        foreach(var tile in _tiles.Values)
            tile.Modulate=beat.AffectedCardIds.Contains(tile.Card.Id)?new Color(1.18f,1.1f,.92f):Colors.White;
    }
    private void PrepareBeats()=>_activeBeats=(Snapshot?.View.LastDuel?.Beats??[]).Where(b=>!_seenBeats.Contains(b.Index)).ToArray();
    private void FinishBeats(){foreach(var beat in _activeBeats)_seenBeats.Add(beat.Index);_activeBeats=[];ClearStory();}
    private void StartAftermathStory()
    {
        PrepareBeats();if(_activeBeats.Length==0)return;
        if(ReducedMotion){FinishBeats();RenderActions();return;}
        _cinema=Cinema.Aftermath;_cinemaTime=0;HideCardHover();CloseGuidance();_cinemaLayer.Show();RenderActions();
    }
    public void OpenChallenge()
    {
        if(Snapshot?.View.LastDuel is not {Challenge:{} challenge} record)return;
        var box=Panel("The challenge",600);
        box.AddChild(DuelUi.Wrapped($"{record.Challenger.Definition!.Name} challenges {record.Challenged.Definition!.Name}.",22));
        box.AddChild(DuelUi.Wrapped($"{StatName(record.Stat)} duel · {challenge.RefusalTerms}",20));
        box.AddChild(DuelUi.Wrapped(challenge.LossTerms,22));
        box.AddChild(DuelUi.Wrapped(challenge.AbilityLabel,18,Palette.PaperDim));
        if(challenge.Source is {} source)Button($"Inspect {source.Definition!.Name}",()=>Inspect(source),box);
        Button("Back",ClearReading,box);
    }
}
