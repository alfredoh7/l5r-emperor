using Godot;
using L5R.Core;
namespace L5R.Client;

public static class DeckBuilderDialog
{
    public static void Show(Node parent)
    {
        var root=parent.GetTree().Root;if(root.GetNodeOrNull("DeckBuilderLayer") is not null)return;
        var layer=new CanvasLayer{Name="DeckBuilderLayer",Layer=85};root.AddChild(layer);
        try{var screen=new DeckBuilderScreen();layer.AddChild(screen);screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);screen.Closed+=layer.QueueFree;}
        catch(Exception e){layer.QueueFree();GD.PushError(e.Message);}
    }
}

public partial class DeckBuilderScreen : Control
{
    public event Action? Closed;
    public CatalogueData Data {get;}=new();
    public CatalogueScans Scans {get;}=new();
    public DeckWorkshop Draft {get;private set;}=null!;
    public DeckFileStore Store {get;private set;}=null!;
    public string? StorageDirectory;
    public DeckSurface Mat {get;private set;}=null!;
    public DeckSurface Collection {get;private set;}=null!;
    public LineEdit Search {get;private set;}=null!;
    public bool Dirty {get;private set;}
    public string Page {get;private set;}="decks";
    public string Family {get;private set;}="Dynasty";
    private readonly Control _canvas=new(),_header=new(),_setupStrip=new(),_left=new(),_toolbar=new(),_setupTools=new(),_library=new(),_overlay=new(),_hover=new();
    private readonly HashSet<string> _compare=[];
    private Dictionary<string,CatalogueData.Card> _byId=[];
    private Button _dynasty=null!,_fate=null!,_compareButton=null!,_collectionTab=null!,_setupTab=null!,_save=null!;
    private Label _status=null!,_dirtyLabel=null!,_count=null!,_empty=null!;
    private LineEdit _name=null!;
    private OptionButton _clan=null!,_type=null!,_keyword=null!,_gold=null!,_support=null!,_sort=null!,_group=null!;
    private string _setupSlot="",_setupClan="Crane",_savedFingerprint="",_savedName="";
    private string? _currentPath;
    private float _w=1672,_h=941;
    private bool _building,_list;
    private int _hoverTicket;
    private bool _previousEmbed;
    private Control _lines=new(){MouseFilter=MouseFilterEnum.Ignore};
    private CatalogueData.Card[] _inspectionCards=[];
    public override void _Ready()
    {
        _previousEmbed=GetViewport().GuiEmbedSubwindows;GetViewport().GuiEmbedSubwindows=true;
        Theme=CatalogueArt.Theme();
        foreach(var type in new[]{"Button","OptionButton"})foreach(var state in new[]{"normal","hover","pressed","disabled","focus"}){var box=(StyleBox)Theme.GetStylebox(state,type).Duplicate();box.ContentMarginTop=4;box.ContentMarginBottom=4;box.ContentMarginLeft=8;box.ContentMarginRight=8;Theme.SetStylebox(state,type,box);}
        MouseFilter=MouseFilterEnum.Stop;
        _byId=Data.Cards.ToDictionary(c=>c.Id);Draft=NewDraft();
        Store=new DeckFileStore(StorageDirectory??ProjectSettings.GlobalizePath("user://decks"));
        var bg=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/DeckBuilder/work-mat.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};AddChild(bg);bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_lines);_lines.Draw+=DrawFrames;
        AddChild(_canvas);foreach(var c in new[]{_header,_setupStrip,_left,_toolbar,_setupTools,_library})_canvas.AddChild(c);
        Collection=new DeckSurface{Rows=true,MakeCard=c=>MakeRow(c,true)};_canvas.AddChild(Collection);
        Mat=new DeckSurface{MakeCard=MakeTile};_canvas.AddChild(Mat);
        _status=CatalogueArt.Label("",17);_canvas.AddChild(_status);_count=CatalogueArt.Label("",17);_canvas.AddChild(_count);
        _empty=CatalogueArt.Label("",24);_empty.HorizontalAlignment=HorizontalAlignment.Center;_canvas.AddChild(_empty);
        BuildHeader();BuildCollectionControls();BuildToolbar();
        _canvas.AddChild(_overlay);_overlay.Visible=false;_canvas.AddChild(_hover);_hover.Visible=false;_hover.MouseFilter=MouseFilterEnum.Ignore;
        Resized+=Fit;TreeExiting+=Scans.Close;TreeExiting+=()=>GetViewport().GuiEmbedSubwindows=_previousEmbed;Fit();ShowDecks();
    }
    private DeckWorkshop NewDraft()=>new(Data.Cards.Select(c=>new DeckCardInfo(c.Id,c.Name,c.Types,c.DeckTypes,c.Definition?.Keywords?.ToArray()??c.Keywords,c.Playable)));
    private static void Put(Control parent,Control child,float x,float y,float w,float h)
    {parent.AddChild(child);child.Position=new Vector2(x,y);child.Size=new Vector2(w,h);}
    private static void Place(Control child,float x,float y,float w,float h){child.Position=new Vector2(x,y);child.Size=new Vector2(w,h);}
    private static OptionButton Option(string[] values,Action changed)
    {var o=new OptionButton{FitToLongestItem=false};foreach(var v in values)o.AddItem(v);o.ItemSelected+=_=>changed();return o;}
    private void BuildHeader()
    {
        Put(_header,CatalogueArt.Button("‹ My decks",()=>Guard(ShowDecks)),24,14,140,42);
        Put(_header,CatalogueArt.Label("Deck builder",43,true),198,12,355,48);
        _name=new LineEdit{PlaceholderText="Name your deck",MaxLength=80};Put(_header,_name,575,16,390,42);
        _name.TextChanged+=_=>{if(!_building){Dirty=true;UpdateStatus();}};
        _dirtyLabel=CatalogueArt.Label("",16);_header.AddChild(_dirtyLabel);
        _save=CatalogueArt.Button("Save draft",()=>SaveDraft());_header.AddChild(_save);
        foreach(var state in new[]{"normal","hover","pressed"}){_save.AddThemeStyleboxOverride(state,Palette.Box(new Color(state=="normal"?"ad8950":"c9a763"),new Color("e2bc76"),6,2));_save.AddThemeColorOverride("font_"+(state=="normal"?"color":state+"_color"),new Color("17140f"));}
        var copy=CatalogueArt.Button("Save a copy",SaveCopy);copy.Name="Copy";_header.AddChild(copy);
    }
    private void BuildCollectionControls()
    {
        _collectionTab=CatalogueArt.Button("Collection",()=>SetSetup(""));Put(_left,_collectionTab,12,6,148,40);
        _setupTab=CatalogueArt.Button("Setup",()=>SetSetup("Stronghold"));Put(_left,_setupTab,166,6,148,40);
        Search=new LineEdit{PlaceholderText="Search names or rules…",ClearButtonEnabled=true};Put(_left,Search,12,58,300,40);Search.TextChanged+=_=>ApplyFilters();
        _clan=Option(new[]{"All clans","Crab","Crane","Dragon","Lion","Mantis","Phoenix","Scorpion","Spider","Unicorn","Unaligned"},ApplyFilters);
        _type=Option(new[]{"All types","Personality","Holding","Region","Event","Celestial","Follower","Item","Spell","Strategy","Ring"},ApplyFilters);
        _keyword=Option(new[]{"Any keyword"}.Concat(Data.Cards.SelectMany(c=>c.Keywords).Distinct().Order()).ToArray(),ApplyFilters);
        _gold=Option(new[]{"Any Gold","0","1","2","3","4","5","6","7","8","9","10+"},ApplyFilters);
        _support=Option(new[]{"All cards","Implemented"},ApplyFilters);
        Put(_left,_clan,12,110,148,36);Put(_left,_type,166,110,148,36);Put(_left,_keyword,12,154,300,36);Put(_left,_gold,12,198,148,36);Put(_left,_support,166,198,148,36);
        Put(_left,CatalogueArt.Label("In deck",15),230,242,80,22);
    }
    private void BuildToolbar()
    {
        _group=Option(new[]{"Group: Type","Group: Gold","Group: None"},()=>RefreshMat());_sort=Option(new[]{"Name A–Z","Gold cost","Force","Chi"},()=>{ApplyFilters();RefreshMat();});
        Put(_toolbar,_group,8,8,162,38);Put(_toolbar,_sort,180,8,154,38);
        Put(_toolbar,CatalogueArt.Button("Mat",()=>{_list=false;RefreshMat();}),356,8,80,38);
        Put(_toolbar,CatalogueArt.Button("List",()=>{_list=true;RefreshMat();}),440,8,80,38);
        _compareButton=CatalogueArt.Button("Compare (0)",()=>ShowComparison(_compare.Select(id=>_byId[id]).ToArray()));Put(_toolbar,_compareButton,540,8,156,38);
        var label=CatalogueArt.Label("Card size",16);label.Name="SizeLabel";_toolbar.AddChild(label);
        var slider=new HSlider{Name="CardSize",MinValue=110,MaxValue=270,Step=2,Value=176,TooltipText="Resize card faces; controls remain readable."};slider.ValueChanged+=v=>{Mat.CardWidth=(float)v;HideHover();RefreshMat();};_toolbar.AddChild(slider);
    }
    private void Fit()
    {
        var scale=Math.Min(1f,Math.Min(Size.X/1500f,Size.Y/840f));if(scale<=0)return;
        _canvas.Scale=Vector2.One*scale;_w=Size.X/scale;_h=Size.Y/scale;_canvas.Size=new Vector2(_w,_h);
        Place(_header,0,0,_w,70);Place(_setupStrip,14,74,_w-28,76);Place(_left,16,164,326,278);
        Place(Collection,28,442,310,_h-492);Place(_toolbar,362,158,_w-380,58);Place(Mat,362,216,_w-380,_h-269);
        Place(_empty,392,360,_w-440,100);Place(_status,378,_h-43,_w-600,28);Place(_count,24,_h-40,325,25);
        Place(_dirtyLabel,_w-520,22,176,30);Place(_save,_w-330,15,145,42);Place(_header.GetNode<Control>("Copy"),_w-175,15,150,42);
        Place(_toolbar.GetNode<Control>("SizeLabel"),_toolbar.Size.X-280,16,85,25);Place(_toolbar.GetNode<Control>("CardSize"),_toolbar.Size.X-187,14,172,32);
        Place(_setupTools,362,158,_w-380,58);Place(_library,22,84,_w-44,_h-104);_overlay.Size=_canvas.Size;
        if(IsInsideTree()){BuildSetupStrip();if(Page=="decks")BuildDeckList();else if(_setupSlot.Length>0)BuildSetupChoices();}
        Collection.Relayout();Mat.Relayout();HideHover();if(_overlay.Visible&&_inspectionCards.Length>0)ShowComparison(_inspectionCards);_lines.QueueRedraw();
    }
    private void DrawFrames()
    {
        if(_canvas.Scale.X<=0)return;var s=_canvas.Scale.X;
        foreach(var y in new[]{70f,154f,_h-50})_lines.DrawLine(new Vector2(18,y)*s,new Vector2(_w-18,y)*s,new Color(.64f,.49f,.26f,.65f),1);
        if(Page!="decks")_lines.DrawLine(new Vector2(348,160)*s,new Vector2(348,_h-54)*s,new Color(.64f,.49f,.26f,.6f),1);
    }
    private void BuildSetupStrip()
    {
        Palette.Clear(_setupStrip);if(Draft is null)return;
        var x=10f;foreach(var slot in DeckWorkshop.SetupSlots)
        {
            var info=Draft.Cards.FirstOrDefault(c=>c.SetupSlot==slot);var card=info is null?null:_byId[info.Id];
            if(card is not null)Put(_setupStrip,new DeckThumbnail(card,Scans),x,9,45,55);
            var b=CatalogueArt.Button(slot+"\n"+(card?.Name??"Select…"),()=>SetSetup(slot));b.Alignment=HorizontalAlignment.Left;b.AddThemeFontSizeOverride("font_size",16);b.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());
            Put(_setupStrip,b,x+53,6,200,61);x+=259;
        }
        Put(_setupStrip,CatalogueArt.Button("Change setup",()=>SetSetup("Stronghold")),x,15,145,44);
        _dynasty=CatalogueArt.Button("",()=>SetFamily("Dynasty"));_fate=CatalogueArt.Button("",()=>SetFamily("Fate"));
        var tabW=Math.Max(185,(_w-985)/2);Put(_setupStrip,_dynasty,_w-2*tabW-42,7,tabW-8,62);Put(_setupStrip,_fate,_w-tabW-34,7,tabW-8,62);UpdateStatus();
    }
    public void SetFamily(string family){Family=family;SetSetup("");ApplyFilters();RefreshMat(true);}
    public void SetSetup(string slot)
    {
        _setupSlot=slot;HideHover();Palette.Clear(_left);BuildCollectionControls();
        _toolbar.Visible=slot.Length==0;_setupTools.Visible=slot.Length>0;Collection.Visible=slot.Length==0;
        if(slot.Length>0)BuildSetupChoices();else {ApplyFilters();RefreshMat(true);}
        _collectionTab.Modulate=slot.Length==0?Colors.White:CatalogueArt.Muted;_setupTab.Modulate=slot.Length>0?Colors.White:CatalogueArt.Muted;UpdateStatus();
    }
    private void BuildSetupChoices()
    {
        Palette.Clear(_left);
        Put(_left,CatalogueArt.Button("Collection",()=>SetSetup("")),12,6,148,40);Put(_left,CatalogueArt.Button("Setup",()=>{}),166,6,148,40);
        var clans=Option(new[]{"Crab","Crane","Dragon","Lion","Mantis","Phoenix","Scorpion","Spider","Unicorn","Unaligned"},()=>{});
        clans.Select(Array.IndexOf(new[]{"Crab","Crane","Dragon","Lion","Mantis","Phoenix","Scorpion","Spider","Unicorn","Unaligned"},_setupClan));
        clans.ItemSelected+=i=>{_setupClan=clans.GetItemText((int)i);BuildSetupChoices();};Put(_left,clans,12,62,300,42);
        var y=124f;foreach(var slot in DeckWorkshop.SetupSlots)
        {
            var info=Draft.Cards.FirstOrDefault(c=>c.SetupSlot==slot);var b=CatalogueArt.Button(slot+(info is null?"":"  ✓"),()=>SetSetup(slot));b.Alignment=HorizontalAlignment.Left;
            Put(_left,b,12,y,300,60);y+=76;
        }
        Put(_left,Palette.Wrapped("Choose one of each.\nKept outside both decks.\n\nBorder Keep starts in play.\n\nBamboo Harvesters starts bowed for the second player only.",19,CatalogueArt.Ivory),16,y+20,285,250);
        Palette.Clear(_setupTools);_setupTools.Visible=true;_toolbar.Visible=false;Put(_setupTools,CatalogueArt.Label("Choose your "+_setupSlot,32,true),14,6,_toolbar.Size.X-190,48);
        Put(_setupTools,CatalogueArt.Button("Back to deck",()=>SetSetup("")),_toolbar.Size.X-170,8,150,38);
        var infos=Data.Cards.Where(c=>new DeckCardInfo(c.Id,c.Name,c.Types,c.DeckTypes,c.Keywords,c.Playable).SetupSlot==_setupSlot);
        if(_setupSlot=="Stronghold")infos=infos.Where(c=>c.Clans.Contains(_setupClan)||(_setupClan=="Unaligned"&&c.Clans.Length==0));
        var cards=infos.OrderByDescending(c=>c.Playable).ThenBy(c=>c.Name).ToArray();_count.Text="Setup · kept outside deck totals";Mat.Setup=true;Mat.Rows=false;Mat.Reset(cards,true);_empty.Visible=cards.Length==0;_empty.Text="No setup choices for this Clan.";
    }
    public void ApplyFilters()
    {
        if(_building||Page=="decks"||_setupSlot.Length>0)return;
        var cards=Data.Filter(Search.Text,_clan.Selected==0?"All":_clan.Text,Family,_type.Selected==0?"":_type.Text,_keyword.Selected==0?"":_keyword.Text,_support.Selected==1,_sort.Text)
            .Where(c=>new DeckCardInfo(c.Id,c.Name,c.Types,c.DeckTypes,c.Keywords,c.Playable).SetupSlot.Length==0);
        if(_gold.Selected>0)cards=cards.Where(c=>_gold.Text=="10+"?c.Numeric("cost")>=10&&c.Numeric("cost")!=double.MaxValue:c.Numeric("cost")==double.Parse(_gold.Text));
        Collection.Reset(cards.ToArray(),true);_count.Text=$"{Collection.Cards.Length:N0} collection results";
    }
    public void RefreshMat(bool reset=false)
    {
        if(Page=="decks"||_setupSlot.Length>0)return;
        var ids=Draft.Cards.Where(c=>c.SetupSlot.Length==0&&c.Families.Contains(Family)).Select(c=>c.Id).ToHashSet();
        var cards=Data.Filter(sort:_sort.Text).Where(c=>ids.Contains(c.Id)).ToArray();
        Mat.Setup=false;Mat.Rows=_list;Mat.Group=_group.Selected switch{1=>"Gold",2=>"None",_=>"Type"};Mat.Reset(cards,reset);
        _empty.Visible=cards.Length==0;_empty.Text="Your "+Family+" mat is empty.\nAdd cards from the collection to begin.";
    }
    public void Adjust(CatalogueData.Card card,int delta)
    {Draft.Adjust(card.Name,delta);Dirty=true;HideHover();UpdateStatus();Collection.Relayout();RefreshMat();}
    private void UpdateStatus()
    {
        if(_status is null||Draft is null)return;var r=Draft.Review();
        if(_dynasty is not null){_dynasty.Text=$"DYNASTY  {r.Dynasty}\n40 minimum";_fate.Text=$"FATE  {r.Fate}\n40 minimum";_dynasty.Modulate=Family=="Dynasty"?Colors.White:CatalogueArt.Muted;_fate.Modulate=Colors.White;_dynasty.AddThemeStyleboxOverride("normal",Palette.Box(new Color(Family=="Dynasty"?"372c1b":"0c1113"),CatalogueArt.Gold,6,2));_fate.AddThemeStyleboxOverride("normal",Palette.Box(new Color(Family=="Fate"?"203c2c":"10251f"),CatalogueArt.Gold,6,2));}
        _status.Text=r.Warnings.Length>0?$"{r.Warnings.Length} deck checks to review  ·  Draft can still be saved":"Basic checks passed  ·  Draft can be saved";
        _status.TooltipText=string.Join("\n",r.Warnings);_status.MouseFilter=MouseFilterEnum.Stop;
        _dirtyLabel.Text=Dirty?"Unsaved changes":"Saved locally";_compareButton.Text=$"Compare ({_compare.Count})";_compareButton.Disabled=_compare.Count==0;
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Escape} && _overlay.Visible)
        {CloseOverlay();GetViewport().SetInputAsHandled();}
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(e is InputEventKey{Pressed:true,Echo:false} key)
        {
            if(key.Keycode==Key.F11)return;
            if(key.Keycode==Key.Escape){if(_overlay.Visible)CloseOverlay();else if(Page=="decks")Closed?.Invoke();else Guard(ShowDecks);}
            else if((key.CtrlPressed||key.MetaPressed)&&key.Keycode==Key.S&&Page!="decks")SaveDraft();
            else if((key.CtrlPressed||key.MetaPressed)&&key.Keycode==Key.F&&Page!="decks"&&_setupSlot.Length==0)Search.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if(e is InputEventMouseButton)GetViewport().SetInputAsHandled();
    }
}
