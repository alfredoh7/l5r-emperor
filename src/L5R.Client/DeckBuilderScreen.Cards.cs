using Godot;
namespace L5R.Client;

public partial class DeckBuilderScreen
{
    private Control MakeRow(CatalogueData.Card card,bool collection)
    {
        var row=new Control{MouseFilter=MouseFilterEnum.Pass};
        var thumb=new DeckThumbnail(card,Scans);row.AddChild(thumb);
        var name=CatalogueArt.Button(card.Name,()=>ShowComparison([card]));name.Alignment=HorizontalAlignment.Left;name.ClipText=true;name.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;name.AddThemeFontSizeOverride("font_size",collection?24:18);
        foreach(var state in new[]{"normal","hover","pressed"})name.AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        row.AddChild(name);var note=CatalogueArt.Label(string.Join(" · ",card.Types),collection?18:13);row.AddChild(note);
        var support=CatalogueArt.Label(card.Playable?"":"Not implemented",16);row.AddChild(support);support.Visible=collection;
        var minus=CatalogueArt.Button("−",()=>Adjust(card,-1));minus.Disabled=Draft.Count(card.Id)==0;minus.AccessibilityName="Remove "+card.Name;row.AddChild(minus);
        var count=CatalogueArt.Label(Draft.Count(card.Id).ToString(),20);count.HorizontalAlignment=HorizontalAlignment.Center;
        if(Draft.Review().OverLimit.Contains(new L5R.Core.DeckCardInfo(card.Id,card.Name,card.Types,card.DeckTypes,card.Keywords,card.Playable).DeckName))count.AddThemeColorOverride("font_color",new Color("f37c64"));row.AddChild(count);
        var plus=CatalogueArt.Button("+",()=>Adjust(card,1));plus.AccessibilityName="Add "+card.Name;row.AddChild(plus);
        void Layout()
        {
            var w=row.Size.X;var h=row.Size.Y;
            if(collection){Place(thumb,2,6,132,h-12);Place(name,150,12,w-260,36);Place(note,155,49,w-265,25);Place(support,155,76,w-265,24);}
            else{Place(thumb,2,4,44,47);Place(name,52,1,w-150,29);Place(note,57,32,w-152,19);}
            Place(minus,w-98,(h-32)/2,29,32);Place(count,w-65,(h-32)/2,30,32);Place(plus,w-32,(h-32)/2,29,32);row.QueueRedraw();
        }
        row.Draw+=()=>{
            row.DrawLine(new Vector2(2,row.Size.Y-1),new Vector2(row.Size.X-3,row.Size.Y-1),new Color(.62f,.47f,.25f,.6f),1);
            if(name.IsHovered()||name.HasFocus())row.DrawRect(new Rect2(0,1,row.Size.X,row.Size.Y-2),DeckSkin.Gold,false,1);
        };
        row.Resized+=Layout;name.MouseEntered+=()=>{row.QueueRedraw();_ = Hover(card,name);};name.MouseExited+=()=>{row.QueueRedraw();HideHover();};name.FocusEntered+=()=>_ = Hover(card,name);name.FocusExited+=HideHover;
        row.GuiInput+=e=>{if(e is InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Left})ShowComparison([card]);};
        return row;
    }
    private Control MakeTile(CatalogueData.Card card)
    {
        if(_list&&_setupSlot.Length==0)return MakeRow(card,false);
        var item=new Control();var face=new DeckFace(card,Scans){Copies=Draft.Count(card.Id),Selected=_compare.Contains(card.Id)||(_setupSlot.Length>0&&Draft.Count(card.Id)==1)};item.AddChild(face);
        face.MouseEntered+=()=>_ = Hover(card,face);face.MouseExited+=HideHover;face.FocusEntered+=()=>_ = Hover(card,face);face.FocusExited+=HideHover;face.Pressed+=()=>ShowComparison([card]);
        var note=CatalogueArt.Label(card.Playable?"":"Not implemented",13);note.HorizontalAlignment=HorizontalAlignment.Center;item.AddChild(note);
        if(_setupSlot.Length>0)
        {
            var selected=Draft.Count(card.Id)==1;var select=CatalogueArt.Button(selected?"Selected":"Select",()=>{
                Draft.SelectSetup(card.Id);Dirty=true;HideHover();BuildSetupStrip();BuildSetupChoices();UpdateStatus();
            });select.AccessibilityName="Select "+card.Name;select.Disabled=selected;select.Icon=DeckSkin.Icon(selected?"selected":"radio");select.AddThemeConstantOverride("h_separation",12);
            foreach(var state in new[]{"normal","disabled","hover"})select.AddThemeStyleboxOverride(state,new StyleBoxEmpty());
            select.AddThemeColorOverride("font_disabled_color",new Color("edca79"));select.AddThemeColorOverride("icon_disabled_color",Colors.White);select.AddThemeFontSizeOverride("font_size",22);item.AddChild(select);
            item.Resized+=()=>{var w=item.Size.X;Place(face,0,0,w,w*1.4f+8);Place(note,0,w*1.4f+9,w,18);Place(select,w/2-75,w*1.4f+30,150,36);};
        }
        else
        {
            var minus=CatalogueArt.Button("−",()=>Adjust(card,-1));minus.AccessibilityName="Remove "+card.Name;item.AddChild(minus);
            var count=CatalogueArt.Label(Draft.Count(card.Id).ToString(),22);count.HorizontalAlignment=HorizontalAlignment.Center;if(Draft.Review().OverLimit.Contains(new L5R.Core.DeckCardInfo(card.Id,card.Name,card.Types,card.DeckTypes,card.Keywords,card.Playable).DeckName))count.AddThemeColorOverride("font_color",new Color("f37c64"));item.AddChild(count);
            var plus=CatalogueArt.Button("+",()=>Adjust(card,1));plus.AccessibilityName="Add "+card.Name;item.AddChild(plus);
            var check=new CheckBox{Text="Compare",ButtonPressed=_compare.Contains(card.Id)};foreach(var state in new[]{"normal","hover","pressed","hover_pressed","focus"})check.AddThemeStyleboxOverride(state,new StyleBoxEmpty());check.AddThemeFontSizeOverride("font_size",15);check.Toggled+=value=>{

                if(value&&_compare.Count>=3){check.SetPressedNoSignal(false);Notice("Compare up to three cards at a time. Remove a selection to compare another.");return;}
                if(value)_compare.Add(card.Id);else _compare.Remove(card.Id);face.Selected=value;face.QueueRedraw();UpdateStatus();
            };check.AccessibilityName="Compare "+card.Name;item.AddChild(check);
            item.Resized+=()=>{var w=item.Size.X;var y=w*1.4f+12;Place(face,0,0,w,w*1.4f+8);Place(note,0,y-4,w,18);Place(minus,w/2-59,y+13,32,29);Place(count,w/2-23,y+13,46,29);Place(plus,w/2+27,y+13,32,29);Place(check,Math.Max(0,w/2-53),y+44,110,26);};
        }
        return item;
    }
    private async Task Hover(CatalogueData.Card card,Control target)
    {
        var ticket=++_hoverTicket;await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
        if(!IsInstanceValid(this)||!IsInsideTree()||ticket!=_hoverTicket||_overlay.Visible||!IsInstanceValid(target)||!target.IsInsideTree())return;
        Palette.Clear(_hover);var w=290f;var h=465f;
        var panel=new Panel();panel.AddThemeStyleboxOverride("panel",new DeckPanelStyle());panel.MouseFilter=MouseFilterEnum.Ignore;Put(_hover,panel,0,0,w,h);
        var face=new DeckFace(card,Scans){MouseFilter=MouseFilterEnum.Ignore};Put(panel,face,16,12,258,370);
        var label=Palette.Wrapped(card.Name+"\n"+(card.Playable?"Game printing":"Reference · not implemented")+"\nClick to inspect",16,CatalogueArt.Ivory);label.MouseFilter=MouseFilterEnum.Ignore;Put(panel,label,16,386,258,68);
        var r=target.GetGlobalRect();var p=(r.End/_canvas.Scale.X);var x=p.X+12;if(x+w>_w-16)x=r.Position.X/_canvas.Scale.X-w-12;
        _hover.Position=new Vector2(Math.Clamp(x,12,_w-w-12),Math.Clamp(r.Position.Y/_canvas.Scale.Y,12,_h-h-12));_hover.Size=new Vector2(w,h);_hover.Show();
    }
    private void HideHover(){_hoverTicket++;_hover.Hide();}
    private void CloseOverlay(){_overlay.Hide();Palette.Clear(_overlay);RefreshMat();Collection.Relayout();}
    private Panel BeginOverlay(string title)
    {
        HideHover();Palette.Clear(_overlay);_overlay.Show();
        var shade=new ColorRect{Color=new Color(0,0,0,.72f)};Put(_overlay,shade,0,0,_w,_h);
        var panel=new Panel();panel.AddThemeStyleboxOverride("panel",new DeckPanelStyle{FullArt=true});Put(_overlay,panel,0,0,_w,_h);
        Put(panel,CatalogueArt.Label(title,35,true),26,16,panel.Size.X-120,45);Put(panel,CatalogueArt.Button("×",CloseOverlay),panel.Size.X-66,17,40,38);
        return panel;
    }
    public void ShowComparison(CatalogueData.Card[] cards)
    {
        if(cards.Length==0)return;cards=cards.Take(3).ToArray();_inspectionCards=cards;
        var panel=BeginOverlay(cards.Length==1?"Card inspection":"Compare cards");
        var heading=panel.GetChildren().OfType<Label>().First();heading.HorizontalAlignment=HorizontalAlignment.Center;Place(heading,200,14,panel.Size.X-400,46);
        var back=CatalogueArt.Button("‹ Back to deck",CloseOverlay);Put(panel,back,22,18,172,36);
        var selected=CatalogueArt.Label($"{cards.Length} selected",17);selected.HorizontalAlignment=HorizontalAlignment.Right;Put(panel,selected,panel.Size.X-235,22,145,30);
        var n=cards.Length;var margin=n==2?80f:24f;var cell=(panel.Size.X-margin*2)/n;
        var faceH=Math.Min(panel.Size.Y-410,Math.Min(380,cell-70)*1.4f);
        var faceW=(faceH-8)/1.4f;var faceY=112f;var paperY=faceY+faceH+45;var paperH=panel.Size.Y-paperY-44;
        for(var i=0;i<n;i++)
        {
            var card=cards[i];var x=margin+i*cell;var inset=n==2?(i==0?40f:-40f):0;var center=x+cell/2+inset;
            var title=CatalogueArt.Label(card.Name,28);title.HorizontalAlignment=HorizontalAlignment.Center;title.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;title.TooltipText=card.Name;
            Put(panel,title,x+12+inset,69,cell-24,36);
            var face=new DeckFace(card,Scans){MouseFilter=MouseFilterEnum.Ignore};Put(panel,face,center-faceW/2,faceY,faceW,faceH);
            if(new L5R.Core.DeckCardInfo(card.Id,card.Name,card.Types,card.DeckTypes,card.Keywords,card.Playable).SetupSlot.Length==0)
            {
                var y=faceY+faceH+5;var count=CatalogueArt.Label(Draft.Count(card.Id).ToString(),22);count.HorizontalAlignment=HorizontalAlignment.Center;
                Put(panel,CatalogueArt.Label("In deck",18),center-132,y,78,32);
                Put(panel,CatalogueArt.Button("−",()=>{Adjust(card,-1);count.Text=Draft.Count(card.Id).ToString();}),center-45,y,32,32);
                Put(panel,count,center-7,y,38,32);
                Put(panel,CatalogueArt.Button("+",()=>{Adjust(card,1);count.Text=Draft.Count(card.Id).ToString();}),center+37,y,32,32);
            }
            var paper=new Panel{Name="RulesParchment"+i};paper.AddThemeStyleboxOverride("panel",DeckSkin.Parchment());Put(panel,paper,x+12,paperY,cell-24,paperH);
            var material=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/DeckBuilder/parchment-clip.gdshader")};material.SetShaderParameter("panel_size",paper.Size);paper.Material=material;
            var ink=new Color("282015");var types=CatalogueArt.Label(string.Join(" · ",card.Types).ToUpperInvariant(),22);types.AddThemeColorOverride("font_color",ink);Put(paper,types,24,12,paper.Size.X-195,34);
            var support=CatalogueArt.Label(card.Playable?"Game printing":"Not implemented",16);support.HorizontalAlignment=HorizontalAlignment.Right;support.AddThemeColorOverride("font_color",ink);Put(paper,support,paper.Size.X-179,17,153,28);
            var line=new ColorRect{Color=new Color("9d7d42"),MouseFilter=MouseFilterEnum.Ignore};Put(paper,line,24,50,paper.Size.X-48,1);
            var scroll=new ScrollContainer{Name="RulesScroll",HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};Put(paper,scroll,24,63,paper.Size.X-48,paper.Size.Y-83);
            var rules=card.Playable?card.Definition!.Text:CatalogueData.PlainText(card.CurrentOracleText);
            var text=Palette.Wrapped(string.IsNullOrWhiteSpace(rules)?"No rules text recorded.":rules,23,ink);text.SizeFlagsHorizontal=SizeFlags.ExpandFill;text.AddThemeConstantOverride("line_spacing",5);scroll.AddChild(text);
        }
        var footer=CatalogueArt.Label(cards.Any(c=>!c.Playable)?"Reference text may include later errata. Unimplemented cards do not gain effects.":"Game printing · full card faces",16);footer.HorizontalAlignment=HorizontalAlignment.Center;
        Put(panel,footer,40,panel.Size.Y-36,panel.Size.X-80,28);
    }
    private void Notice(string message)
    {
        var dialog=new AcceptDialog{Title="Deck builder",DialogText=message,Theme=Theme};AddChild(dialog);dialog.Confirmed+=dialog.QueueFree;dialog.Canceled+=dialog.QueueFree;dialog.PopupCentered(new Vector2I(580,190));
    }
}
