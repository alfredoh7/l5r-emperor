using Godot;
namespace L5R.Client;

/// <summary>The approved open gallery and on-demand inspector, inside the existing game window.</summary>
public static class LibraryDialog
{
    public static void Show(Node parent)
    {
        if(parent.GetTree().Root.GetNodeOrNull<CanvasLayer>("CatalogueLayer") is not null)return;
        var layer=new CanvasLayer {Name="CatalogueLayer",Layer=80};parent.GetTree().Root.AddChild(layer);
        try
        {
            var screen=new CatalogueScreen();layer.AddChild(screen);screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            screen.Closed+=layer.QueueFree;
        }
        catch(Exception e){layer.QueueFree();GD.PushError("Catalogue: "+e.Message);var alert=new AcceptDialog {Theme=SidebarSkin.Theme(),DialogText="The catalogue could not be loaded. Please verify the game content bundle.",Title="Card library"};parent.AddChild(alert);alert.CloseRequested+=alert.QueueFree;alert.Confirmed+=alert.QueueFree;alert.Canceled+=alert.QueueFree;alert.PopupCentered();}
    }
}

public partial class CatalogueScreen : Control
{
    public event Action? Closed;
    public CatalogueData Data {get;}=new();
    public CatalogueScans Scans {get;}=new();
    public CatalogueGrid Grid {get;private set;}=null!;
    public LineEdit Search {get;private set;}=null!;
    public Rect2 InspectorRect=>_detail.GetGlobalRect();
    public string SelectedId=>_selected?.Id??"";
    private readonly Control _canvas=new(),_header=new(),_clans=new(),_filters=new(),_detail=new();
    private Control _backdrop=null!,_detailFrame=null!;
    private readonly Dictionary<string,Button> _clanButtons=[],_familyButtons=[];
    private OptionButton _type=null!,_keyword=null!,_sort=null!,_set=null!;
    private Button _allCards=null!,_playable=null!,_clear=null!,_back=null!,_closeDetail=null!;
    private Label _count=null!,_footer=null!;
    private ScrollContainer _detailScroll=null!;
    private VBoxContainer _detailBody=null!;
    private TextureRect? _face;
    private Label? _scanState;
    private CatalogueData.Card? _selected;
    private CatalogueData.Printing? _printing;
    private PopupPanel? _advanced;
    private string _clan="All",_family="All";
    private bool _playableOnly,_loading;
    private float _w,_h;
    private int _browseScroll;
    public override void _Ready()
    {
        Theme=CatalogueArt.Theme();MouseFilter=MouseFilterEnum.Stop;FocusMode=FocusModeEnum.All;
        _backdrop=CatalogueArt.Backdrop();AddChild(_backdrop);_backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_canvas);_canvas.AddChild(_header);_canvas.AddChild(_clans);_canvas.AddChild(_filters);
        Grid=new CatalogueGrid(Scans);_canvas.AddChild(Grid);Grid.Selected=SelectCard;
        _canvas.AddChild(_detail);_detailFrame=CatalogueArt.Backdrop();_detail.AddChild(_detailFrame);_detailFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _detail.Visible=false;
        _back=CatalogueArt.Button("‹  Back",()=>Closed?.Invoke());Put(_header,_back,28,17,130,44);_back.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());
        Put(_header,CatalogueArt.Label("Card library",44,true),202,12,390,60);
        Search=new LineEdit {PlaceholderText="Search cards, keywords, or rules…",ClearButtonEnabled=true};Put(_header,Search,618,20,600,44);Search.TextChanged+=_=>ApplyFilter();
        _clear=CatalogueArt.Button("Clear filters",ResetFilters);_header.AddChild(_clear);
        var clanNames=new[]{"All","Crab","Crane","Dragon","Lion","Mantis","Phoenix","Scorpion","Spider","Unicorn","Unaligned"};
        foreach(var clan in clanNames)
        {
            var b=CatalogueArt.Button("",()=>{_clan=clan;ApplyFilter();});b.AccessibilityName=clan+" cards";b.TooltipText=clan=="All"?"All clans":clan+" cards";_clans.AddChild(b);_clanButtons[clan]=b;
            b.AddChild(CatalogueArt.Clan(clan));b.AddChild(CatalogueArt.Label(clan,18));
        }
        foreach(var family in new[]{"All","Dynasty","Fate","Strongholds"})
        {var b=CatalogueArt.Button(family,()=>{_family=family;ApplyFilter();});_filters.AddChild(b);_familyButtons[family]=b;}
        _type=Option("Type",Data.Cards.SelectMany(c=>c.Types).Distinct().Order().ToArray());_filters.AddChild(_type);
        _keyword=Option("Keywords",Data.Cards.SelectMany(c=>c.Keywords).Distinct().Order().ToArray());_filters.AddChild(_keyword);
        var more=CatalogueArt.Button("More filters",ShowAdvanced);more.Name="MoreFilters";_filters.AddChild(more);
        _allCards=CatalogueArt.Button("◉  Entire catalogue",()=>{_playableOnly=false;ApplyFilter();});_filters.AddChild(_allCards);
        _playable=CatalogueArt.Button("○  Playable now",()=>{_playableOnly=true;ApplyFilter();});_filters.AddChild(_playable);
        _sort=Option("",["Name A–Z","Name Z–A","Gold cost","Force","Chi","Set"]);_filters.AddChild(_sort);
        _count=CatalogueArt.Label("",16);_canvas.AddChild(_count);
        _footer=CatalogueArt.Label("Click a card to inspect  ·  Esc to return",16);_footer.HorizontalAlignment=HorizontalAlignment.Center;_canvas.AddChild(_footer);
        var density=Option("",["Small cards","Medium cards","Large cards"]);density.Name="Density";density.Selected=1;density.ItemSelected+=i=>{Grid.Density=(int)i;Grid.LayoutCards();};_canvas.AddChild(density);
        _closeDetail=CatalogueArt.Button("×",CloseInspector);_closeDetail.TooltipText="Close card details · Esc";Put(_detail,_closeDetail,0,0,44,44);
        _detailScroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_detail.AddChild(_detailScroll);
        _detailBody=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};_detailBody.AddThemeConstantOverride("separation",12);_detailScroll.AddChild(_detailBody);
        _set=Option("All printings",Data.Cards.SelectMany(c=>c.Printings).Select(p=>p.Set).Where(s=>s!="Game printing").Distinct().Order().ToArray());
        AddChild(_set);_set.Visible=false;
        Resized+=Fit;Scans.Changed+=UpdateFace;TreeExiting+=Scans.Close;
        ApplyFilter();Fit();GrabFocus();
    }
    private static void Put(Control parent,Control child,float x,float y,float w,float h){parent.AddChild(child);child.Position=new Vector2(x,y);child.Size=new Vector2(w,h);}
    private OptionButton Option(string first,string[] values)
    {
        var o=new OptionButton {FitToLongestItem=false,ClipText=true};if(first!="")o.AddItem(first);foreach(var value in values)o.AddItem(value);o.GetPopup().AllowSearch=true;o.GetPopup().MaxSize=new Vector2I(700,500);o.TooltipText="Type while this menu is open to jump to an option.";o.ItemSelected+=_=>ApplyFilter();return o;
    }
    private string Value(OptionButton o)=>o.Selected<=0?"":o.GetItemText(o.Selected);
    public void ApplyFilter()
    {
        if(_loading||Grid is null||_sort is null)return;
        var results=Data.Filter(Search.Text,_clan=="All"?null:[_clan],_family,Value(_type)==""?null:[Value(_type)],Value(_keyword)==""?null:[Value(_keyword)],_playableOnly,_sort.GetItemText(_sort.Selected),_set is null?"":Value(_set));
        Grid.SetCards(results);_count.Text=$"{results.Length:N0} {(results.Length==1?"result":"results")}  ·  {Data.Cards.Length:N0} cards";
        foreach(var (key,b) in _clanButtons)Highlight(b,key==_clan);
        foreach(var (key,b) in _familyButtons)Highlight(b,key==_family);
        _allCards.Text=(_playableOnly?"○":"◉")+"  Entire catalogue";_playable.Text=(_playableOnly?"◉":"○")+"  Playable now";
        Highlight(_allCards,!_playableOnly);Highlight(_playable,_playableOnly);
        _clear.Visible=Search.Text!=""||_clan!="All"||_family!="All"||Value(_type)!=""||Value(_keyword)!=""||_playableOnly||(_set is not null&&Value(_set)!="");
        _footer.Text=results.Length==0?"No matching cards · Clear filters or try a shorter search":"Click a card to inspect  ·  Esc to return";
        if(_selected is not null&&!results.Any(c=>c.Id==_selected.Id))CloseInspector();
    }
    private void Highlight(Button b,bool active)
    {
        var clan=_clanButtons.ContainsValue(b);
        var style=Palette.Box(active?new Color("30271a"):clan?Colors.Transparent:new Color("0a1115"),active?new Color("d2aa66"):new Color("51442d"),6,2);
        if(clan&&!active){style.BorderWidthLeft=style.BorderWidthTop=style.BorderWidthBottom=0;}
        if(active){style.ShadowColor=new Color(.86f,.65f,.31f,.22f);style.ShadowSize=6;style.BorderWidthLeft=style.BorderWidthRight=style.BorderWidthTop=style.BorderWidthBottom=2;}
        b.AddThemeStyleboxOverride("normal",style);
    }
    public void ResetFilters()
    {_loading=true;Search.Text="";_clan="All";_family="All";_playableOnly=false;_type.Selected=0;_keyword.Selected=0;_set.Selected=0;_loading=false;ApplyFilter();}
    private void Fit()
    {
        if(Grid is null||Size.X<100)return;
        // Scale only below 1440 to keep small windows usable; the grid genuinely reflows.
        var scale=Math.Min(1f,Size.X/1440f);scale=Math.Max(.66f,scale);
        _canvas.Scale=Vector2.One*scale;_w=Size.X/scale;_h=Size.Y/scale;_canvas.Size=new Vector2(_w,_h);
        _header.Size=new Vector2(_w,78);Search.Size=new Vector2(Math.Max(300,_w-1028),44);_clear.Position=new Vector2(_w-195,20);_clear.Size=new Vector2(150,44);
        var dw=Math.Clamp(_w*.275f,360,460);var galleryWidth=_w-88-(_detail.Visible?dw+24:0);
        _clans.Position=new Vector2(44,82);_clans.Size=new Vector2(galleryWidth,78);
        var cw=galleryWidth/11;
        var i=0;foreach(var b in _clanButtons.Values){b.Position=new Vector2(i++*cw,0);b.Size=new Vector2(cw-2,78);var icon=b.GetChild<Control>(0);icon.Position=new Vector2((cw-42)/2,5);icon.Size=new Vector2(40,40);var label=b.GetChild<Label>(1);label.Position=new Vector2(0,46);label.Size=new Vector2(cw,26);label.HorizontalAlignment=HorizontalAlignment.Center;}
        _filters.Position=new Vector2(44,180);_filters.Size=new Vector2(_w-88,40);var x=0f;
        foreach(var (key,b) in _familyButtons){var width=key=="Strongholds"?130:84;b.Position=new Vector2(x,0);b.Size=new Vector2(width,40);x+=width;}
        x+=18;foreach(var b in new Control[]{_type,_keyword,_filters.GetNode<Button>("MoreFilters")}){b.Position=new Vector2(x,0);b.Size=new Vector2(126,40);x+=134;}
        x+=12;_allCards.Position=new Vector2(x,0);_allCards.Size=new Vector2(180,40);x+=185;_playable.Position=new Vector2(x,0);_playable.Size=new Vector2(150,40);
        _sort.Position=new Vector2(_w-278,0);_sort.Size=new Vector2(190,40);
        if(_detail.Visible){_allCards.Position=new Vector2(0,49);_playable.Position=new Vector2(190,49);_sort.Position=new Vector2(galleryWidth-190,49);}
        var gridTop=_detail.Visible?287:238;
        Grid.Position=new Vector2(36,gridTop);Grid.Size=new Vector2(galleryWidth+16,_h-gridTop-52);
        _detail.Position=new Vector2(_w-dw-28,82);_detail.Size=new Vector2(dw,_h-129);
        _closeDetail.Position=new Vector2(dw-62,15);_closeDetail.Size=new Vector2(42,42);
        _detailScroll.Position=new Vector2(22,65);_detailScroll.Size=new Vector2(dw-44,_detail.Size.Y-88);
        _detailBody.CustomMinimumSize=new Vector2(dw-64,0);
        if(_face is not null)_face.CustomMinimumSize=new Vector2(0,Math.Min((dw-70)*1.4f,_detail.Size.Y-370));
        _count.Position=new Vector2(45,_h-38);_count.Size=new Vector2(340,27);
        _footer.Position=new Vector2(390,_h-38);_footer.Size=new Vector2(_w-1000,27);
        var density=_canvas.GetNode<OptionButton>("Density");density.Position=new Vector2(_w-255,_h-44);density.Size=new Vector2(210,34);
        Grid.LayoutCards();QueueRedraw();
    }
    public override void _Draw()
    {
        var s=_canvas.Scale.X;foreach(var y in new[]{78f,163f,229f})DrawLine(new Vector2(25,y*s),new Vector2((y>78&&_detail.Visible?_detail.Position.X-12:_w-25)*s,y*s),new Color(.56f,.43f,.22f,.6f),1);
    }
    public void SelectCard(CatalogueData.Card card)
    {
        if(_selected?.Id==card.Id){CloseInspector();return;}
        if(!_detail.Visible)_browseScroll=Grid.ScrollVertical;
        _selected=card;_printing=card.Printings.FirstOrDefault();_detail.Visible=true;Grid.Select(card.Id);BuildInspector();Fit();Callable.From(()=>{if(_selected?.Id==card.Id)Grid.EnsureCard(card.Id);}).CallDeferred();
    }
    public void CloseInspector()
    {_selected=null;_printing=null;_detail.Visible=false;Grid.Select(null);Fit();Grid.ScrollVertical=_browseScroll;Grid.GrabFocus();}
    private void BuildInspector()
    {
        if(_selected is null)return;var card=_selected;Palette.Clear(_detailBody);
        foreach(var label in _detail.GetChildren().OfType<Label>().ToArray()){_detail.RemoveChild(label);label.QueueFree();}
        Put(_detail,CatalogueArt.Label("Card details",26,true),22,20,275,34);
        _face=new TextureRect {ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,TextureFilter=TextureFilterEnum.LinearWithMipmaps};_detailBody.AddChild(_face);
        _scanState=CatalogueArt.Label("",15);_scanState.AutowrapMode=TextServer.AutowrapMode.WordSmart;_detailBody.AddChild(_scanState);
        var status=CatalogueArt.Label(card.Playable?"✓  Game version playable":"Not yet playable",18);status.HorizontalAlignment=HorizontalAlignment.Center;
        var statusPanel=new PanelContainer();statusPanel.AddThemeStyleboxOverride("panel",Palette.Box(card.Playable?new Color("203327"):new Color("2e2720"),new Color("776644"),8));statusPanel.AddChild(status);_detailBody.AddChild(statusPanel);
        _detailBody.AddChild(CatalogueArt.Label(card.Name,23));
        var printing=new OptionButton {FitToLongestItem=false};foreach(var p in card.Printings)printing.AddItem(p.Label);
        if(card.Printings.Length==0){printing.AddItem("No scan indexed");printing.Disabled=true;}
        printing.ItemSelected+=i=>{_printing=card.Printings[(int)i];UpdateFace();};_detailBody.AddChild(printing);
        AddDisclosure("Rules text",()=>{
            var body=new VBoxContainer();body.AddChild(Palette.Wrapped("Current Oracle reference",14,CatalogueArt.Gold));body.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(card.CurrentOracleText)?"No rules text recorded in the catalogue.":CatalogueData.PlainText(card.CurrentOracleText),18,CatalogueArt.Ivory));
            if(card.Definition is {} def){body.AddChild(new HSeparator());body.AddChild(Palette.Wrapped("Game printing / implemented profile",14,CatalogueArt.Gold));body.AddChild(Palette.Wrapped(def.Text.Length>0?def.Text:"No printed rules text in the implemented definition.",18,CatalogueArt.Ivory));}
            body.AddChild(Palette.Wrapped("Oracle wording can include later errata. Alternate scans do not change the game's pinned printing.",14,CatalogueArt.Muted));return body;});
        AddDisclosure("Printing details",()=>{
            var body=new VBoxContainer();body.AddChild(Palette.Wrapped(string.Join(" · ",card.Types.Concat(card.Clans)),17,CatalogueArt.Ivory));
            body.AddChild(Palette.Wrapped(string.Join(" · ",card.Keywords),16,CatalogueArt.Gold));
            body.AddChild(Palette.Wrapped(string.Join("   ",card.Stats.Select(s=>s.Key+" "+string.Join(" / ",s.Value))),16,CatalogueArt.Ivory));
            body.AddChild(Palette.Wrapped("Catalogue membership: Age of Conquest (Emperor). Scan selection is a reference, not certification of historical legality.",14,CatalogueArt.Muted));
            var source=CatalogueArt.Button("Open Oracle source ↗",()=>{if(card.SourceUrl.StartsWith("https://oracleofthevoid.com/",StringComparison.Ordinal))OS.ShellOpen(card.SourceUrl);});body.AddChild(source);
            var scan=CatalogueArt.Button("Open selected scan source ↗",()=>{if(_printing?.SourceUrl.StartsWith("https://images.oracleofthevoid.com/",StringComparison.Ordinal)==true)OS.ShellOpen(_printing.SourceUrl);});body.AddChild(scan);
            var retry=CatalogueArt.Button("Retry missing scan",()=>{Scans.Retry();UpdateFace();});body.AddChild(retry);return body;});
        _detailScroll.ScrollVertical=0;UpdateFace();
    }
    private void AddDisclosure(string label,Func<Control> create)
    {
        var box=new VBoxContainer {Visible=false};var button=CatalogueArt.Button(label+"  ›",()=>{if(box.GetChildCount()==0)box.AddChild(create());box.Visible=!box.Visible;if(box.Visible)Callable.From(()=>_detailScroll.EnsureControlVisible(box)).CallDeferred();});box.VisibilityChanged+=()=>button.Text=label+(box.Visible?"  ▾":"  ›");button.Alignment=HorizontalAlignment.Left;button.CustomMinimumSize=new Vector2(0,42);_detailBody.AddChild(button);_detailBody.AddChild(box);
    }
    private void UpdateFace(){if(_face is null||_selected is null)return;_face.Texture=Scans.Get(_printing);if(_scanState is not null){_scanState.Visible=_face.Texture is null;_scanState.Text=Scans.State(_printing);} }
    private void ShowAdvanced()
    {
        if(_advanced is null)
        {
            _advanced=new PopupPanel {Theme=Theme};AddChild(_advanced);var box=new VBoxContainer();box.AddThemeConstantOverride("separation",14);_advanced.AddChild(box);
            box.AddChild(CatalogueArt.Label("More filters",22,true));box.AddChild(CatalogueArt.Label("Printing set",17));_set.CustomMinimumSize=new Vector2(330,42);_set.Reparent(box);_set.Visible=true;
            box.AddChild(CatalogueArt.Label("All catalogue artwork is installed locally.\nNo connection is needed to browse.",16));
            box.AddChild(CatalogueArt.Button("Imported decks & source archive",()=>{_advanced.Hide();LegacyLibraryDialog.Show(this);}));
            box.AddChild(CatalogueArt.Button("Done",()=>_advanced.Hide()));
        }
        _advanced.PopupCentered(new Vector2I(410,330));
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(e is InputEventKey {Pressed:true,Echo:false} key)
        {
            if(key.Keycode==Key.F11)return;
            if(key.Keycode==Key.Escape){if(_detail.Visible)CloseInspector();else Closed?.Invoke();}
            else if((key.CtrlPressed||key.MetaPressed)&&key.Keycode==Key.F)Search.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if(e is InputEventMouseButton)GetViewport().SetInputAsHandled();
    }
}
