using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly BattleJournal _journal = new();
    private MatchView? _journalView;
    private string? _journalRoom;
    private BattleJournalPanel _journalPanel = null!;
    private Button _journalToggle = null!;
    private BattleDefeatBanner _defeatBanner = null!;
    private bool _journalCollapsed;
    private int _journalRevision=-1;
    private readonly Dictionary<int,double> _bloodStarts=[];
    private readonly Dictionary<int,CrimsonCourt> _bloodCourts=[];
    private (int Turn,int Attacker)? _bloodBattle;
    private bool JournalAvailable => _session.Snapshot is not null && (BattleJournal.BattlePhase(_session.Snapshot.View.Phase) || _journal.Exchanges.Count>0);
    private bool JournalOpen => JournalAvailable && !_journalCollapsed;

    private void BuildBattleJournal()
    {
        _journalPanel=new BattleJournalPanel(); _root.AddChild(_journalPanel);
        _journalToggle=Palette.Button("Battle exchanges",()=>{
            _journalCollapsed=!_journalCollapsed; RenderBattleJournal(); FitTableViewport();
        });
        _journalToggle.TooltipText="Battle exchanges · public actions, reactions and outcomes. Collapse to make more room for the table.";
        _root.AddChild(_journalToggle);
        _defeatBanner=new BattleDefeatBanner(); _root.AddChild(_defeatBanner);
    }
    private void ObserveBattleJournal()
    {
        var snapshot=_session.Snapshot!; var next=snapshot.View;
        var fresh=_journalView is null || _journalRoom!=snapshot.RoomCode || _journalView.ViewerId!=next.ViewerId || _journalView.Scenario!=next.Scenario || next.Version<_journalView.Version;
        if(fresh)
        {
            _journal.Reset(); _journalView=null; _bloodStarts.Clear(); _bloodBattle=null;
            _staticOutcomeUntil=0;
            _journalCollapsed=!BattleJournal.BattlePhase(next.Phase); _defeatBanner.Visible=false;
        }
        if(_journalView is not null && !BattleJournal.BattlePhase(_journalView.Phase) && BattleJournal.BattlePhase(next.Phase)) _journalCollapsed=false;
        _journalRoom=snapshot.RoomCode;
        var key=(next.TurnNumber,next.ActivePlayerId);
        if(_bloodBattle!=key) { _bloodBattle=key; _bloodStarts.Clear(); }
        // Only engine-confirmed attacking roots activate blood. Drafts and defenders
        // are intentionally absent; activation survives later deaths/returns in this battle.
        foreach(var card in next.Players.Single(p=>p.Id==next.ActivePlayerId).Army.Where(c=>c.AttachedTo is null && c.ProvinceIndex is not null))
            _bloodStarts.TryAdd(card.ProvinceIndex!.Value,fresh ? -10 : _battleVisualTime);
        _journal.Observe(next,_journalView);
        _journalView=next;
    }
    private void RenderBattleJournal()
    {
        _journalToggle.Visible=JournalAvailable;
        _journalToggle.Text=(_journalCollapsed?"＋  ":"−  ")+"Battle exchanges";
        _journalToggle.Position=new Vector2(18,145); _journalToggle.Size=new Vector2(256,34);
        _journalPanel.Visible=JournalOpen; _journalPanel.Position=new Vector2(16,181); _journalPanel.Size=new Vector2(260,738);
        if(_journalRevision!=_journal.Revision)
        { _journalPanel.Present(_journal,_session.Snapshot!.View); _journalRevision=_journal.Revision; }
        _defeatBanner.Position=new Vector2(_tableViewport.Position.X+Math.Max(0,(_tableViewport.Size.X-680)/2),180);
        _defeatBanner.Size=new Vector2(Math.Min(680,_tableViewport.Size.X),112);
        _defeatBanner.Visible=PresentationBusy && _battlePresentationStage==1 || _battleVisualTime<_staticOutcomeUntil;
        _defeatBanner.Headline=_battleDefeatHeadline;
        _defeatBanner.Cause=_battleDefeatCause;
        _defeatBanner.QueueRedraw();
    }
    private void SyncBloodCourts()
    {
        foreach(var index in _bloodCourts.Keys.ToArray())
            if(!_battleCourts.ContainsKey(index) || !_bloodStarts.ContainsKey(index)) { _bloodCourts[index].QueueFree(); _bloodCourts.Remove(index); }
        foreach(var (index,rect) in _battleCourts)
        {
            if(!_bloodStarts.TryGetValue(index,out var start)) continue;
            var bay=_matTargets.FirstOrDefault(s=>s.Id==$"province:{1-View.ActivePlayerId}:{index}");
            if(bay is null) continue;
            if(!_bloodCourts.TryGetValue(index,out var court))
            { court=new CrimsonCourt(); _battleDecor.AddChild(court); _bloodCourts[index]=court; }
            court.Present(rect,bay.Bounds.GetCenter().Y<rect.GetCenter().Y,start<0?8+_battleVisualTime:_battleVisualTime-start,_reducedMotion,View.CurrentBattleProvince==index);
        }
    }
}

public partial class BattleJournalPanel : Control
{
    private VBoxContainer _entries=null!;
    private ScrollContainer _scroll=null!;
    private readonly HashSet<long> _expanded=[];
    private long _lastOutcome=-1;
    public void ShowLatest() => _scroll.ScrollVertical=0;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Stop;
        var margin=new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach(var side in new[]{"left","right"}) margin.AddThemeConstantOverride("margin_"+side,22);
        margin.AddThemeConstantOverride("margin_top",56); margin.AddThemeConstantOverride("margin_bottom",28);
        _scroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };
        margin.AddChild(_scroll); _entries=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill }; _entries.AddThemeConstantOverride("separation",14); _scroll.AddChild(_entries);
    }
    public override void _Draw()
    {
        var art=BattleEscalationArt.Journal;
        // Source margins and destination margins are distinct: retain detailed
        // worn fittings without letting source-resolution corners consume the text.
        var sx=new[]{0f,80f,709f,789f}; var sy=new[]{0f,160f,1270f,1370f};
        var dx=new[]{0f,20f,Size.X-20,Size.X}; var dy=new[]{0f,40f,Size.Y-25,Size.Y};
        for(var y=0;y<3;y++) for(var x=0;x<3;x++)
            DrawTextureRectRegion(art,new Rect2(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),new Rect2(sx[x],sy[y],sx[x+1]-sx[x],sy[y+1]-sy[y]),new Color(.77f,.74f,.70f));
    }
    public void Present(BattleJournal journal,MatchView view)
    {
        var scroll=_scroll.ScrollVertical;
        var outcome=journal.Exchanges.LastOrDefault(e=>e.Events.Any(x=>x.Type is "battleResolved" or "unitDestroyed" or "attachmentDestroyed"))?.Sequence??-1;
        if(outcome!=_lastOutcome) { scroll=0; _lastOutcome=outcome; }
        Palette.Clear(_entries);
        var title=Palette.Text("Battle exchanges",19,Palette.Gold,true); _entries.AddChild(title);
        if(view.CurrentBattleProvince is int province) _entries.AddChild(Palette.Text("Province "+(province+1),16,Palette.PaperDim));
        if(journal.Exchanges.Count==0) _entries.AddChild(Palette.Wrapped("Committed movements and public battle actions will appear here.",16,Palette.PaperDim));
        if(journal.Aftermath.Count>0 || journal.Outcome.Length>0)
        {
            _entries.AddChild(Palette.Text("Aftermath",18,Palette.Error,true));
            if(journal.Outcome.Length>0) _entries.AddChild(Palette.Wrapped(journal.Outcome,17,Palette.Paper));
            foreach(var item in journal.Aftermath)
            {
                var row=new HBoxContainer(); row.AddThemeConstantOverride("separation",9); _entries.AddChild(row);
                var card=BattleJournal.Visible(view).FirstOrDefault(c=>c.Id==item.CardId && c.FaceUp);
                if(card?.Definition is { } definition && CardTile.Artwork(definition) is { } face)
                {
                    var thumb=new TextureRect { Texture=face,CustomMinimumSize=new Vector2(35,51),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore };
                    row.AddChild(thumb);
                }
                var copy=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill }; row.AddChild(copy);
                copy.AddChild(Palette.Wrapped(item.Name,16,Palette.Paper));
                copy.AddChild(Palette.Wrapped(item.Destination,15,item.Destination=="Dead"?Palette.Error:Palette.Gold));
                row.TooltipText=item.Name+" → "+item.Destination;
            }
            _entries.AddChild(new HSeparator());
        }
        foreach(var exchange in journal.Exchanges.AsEnumerable().Reverse())
        {
            var column=new VBoxContainer(); column.AddThemeConstantOverride("separation",4); _entries.AddChild(column);
            if(exchange.Player is int player)
            {
                var owner=view.Players.Single(p=>p.Id==player);
                var actor=new HBoxContainer(); actor.AddThemeConstantOverride("separation",8); column.AddChild(actor);
                var emblem=ClanPlaque.Emblem(owner.Clan,new Vector2(30,30)); emblem.CustomMinimumSize=new Vector2(30,30); actor.AddChild(emblem);
                var name=Palette.Text(owner.Name,15,player==view.ViewerId?Palette.Crane:Palette.Gold); name.SizeFlagsVertical=SizeFlags.ShrinkCenter; actor.AddChild(name);
            }
            column.AddChild(Palette.Wrapped(exchange.Heading,17,exchange.Events.Any(e=>e.Type is "battleResolved" or "unitDestroyed")?Palette.Error:Palette.Paper));
            if(exchange.Events.Count>1)
            {
                var more=new Button { Text=_expanded.Contains(exchange.Sequence)?"Hide effects":"Show effects · "+(exchange.Events.Count-1), Flat=true,Alignment=HorizontalAlignment.Left };
                more.AddThemeFontSizeOverride("font_size",14); column.AddChild(more);
                more.Pressed+=()=>{ if(!_expanded.Add(exchange.Sequence)) _expanded.Remove(exchange.Sequence); Present(journal,view); };
                if(_expanded.Contains(exchange.Sequence)) foreach(var e in exchange.Events.Skip(1)) column.AddChild(Palette.Wrapped(e.Message,15,Palette.PaperDim));
            }
            column.AddChild(new HSeparator());
        }
        _scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical,scroll);
        QueueRedraw();
    }
}

public partial class BattleDefeatBanner : Control
{
    public string Headline { get; set; }="";
    public string Cause { get; set; }="";
    private Label _headline=null!,_cause=null!;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore; ZIndex=45; Visible=false;
        _headline=Palette.Wrapped("",34,new Color("e8b6a0")); _headline.AddThemeFontOverride("font",Palette.Title); _headline.HorizontalAlignment=HorizontalAlignment.Center; AddChild(_headline);
        _cause=Palette.Wrapped("",17,Palette.PaperDim); _cause.HorizontalAlignment=HorizontalAlignment.Center; AddChild(_cause);
    }
    public override void _Draw()
    {
        DrawTextureRect(BattleEscalationArt.Announcement,new Rect2(Vector2.Zero,Size),false);
        _headline.Text=Headline; _headline.Position=new Vector2(40,15); _headline.Size=new Vector2(Size.X-80,58);
        _cause.Text=Cause; _cause.Position=new Vector2(45,75); _cause.Size=new Vector2(Size.X-90,34);
    }
}
