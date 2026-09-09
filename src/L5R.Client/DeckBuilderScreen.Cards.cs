using Godot;
namespace L5R.Client;

public partial class DeckBuilderScreen
{
    private Control MakeRow(CatalogueData.Card card,bool collection)
    {
        var row=new Control();
        var thumb=new DeckThumbnail(card,Scans);Put(row,thumb,2,4,44,47);
        var name=CatalogueArt.Button(card.Name,()=>ShowComparison([card]));name.Alignment=HorizontalAlignment.Left;name.ClipText=true;name.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;name.AddThemeFontSizeOverride("font_size",16);name.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());
        row.AddChild(name);var note=CatalogueArt.Label(card.Playable?string.Join(" · ",card.Types):"Not implemented",13);row.AddChild(note);
        var minus=CatalogueArt.Button("−",()=>Adjust(card,-1));minus.Disabled=Draft.Count(card.Name)==0;minus.AccessibilityName="Remove "+card.Name;row.AddChild(minus);
        var count=CatalogueArt.Label(Draft.Count(card.Name).ToString(),20);count.HorizontalAlignment=HorizontalAlignment.Center;
        if(Draft.Review().OverLimit.Contains(card.Name))count.AddThemeColorOverride("font_color",new Color("f37c64"));row.AddChild(count);
        var plus=CatalogueArt.Button("+",()=>Adjust(card,1));plus.AccessibilityName="Add "+card.Name;row.AddChild(plus);
        void Layout(){var w=row.Size.X;Place(name,52,1,w-150,29);Place(note,57,32,w-152,19);Place(minus,w-98,12,29,32);Place(count,w-65,12,30,32);Place(plus,w-32,12,29,32);}
        row.Resized+=Layout;name.MouseEntered+=()=>Hover(card,name);name.MouseExited+=HideHover;name.FocusEntered+=()=>Hover(card,name);name.FocusExited+=HideHover;
        return row;
    }
    private Control MakeTile(CatalogueData.Card card)
    {
        if(_list&&_setupSlot.Length==0)return MakeRow(card,false);
        var item=new Control();var face=new DeckFace(card,Scans){Copies=Draft.Count(card.Name),Selected=_compare.Contains(card.Id)||(_setupSlot.Length>0&&Draft.Count(card.Name)==1)};item.AddChild(face);
        face.MouseEntered+=()=>Hover(card,face);face.MouseExited+=HideHover;face.FocusEntered+=()=>Hover(card,face);face.FocusExited+=HideHover;face.Pressed+=()=>ShowComparison([card]);
        var note=CatalogueArt.Label(card.Playable?"":"Not implemented",13);note.HorizontalAlignment=HorizontalAlignment.Center;item.AddChild(note);
        if(_setupSlot.Length>0)
        {
            var selected=Draft.Count(card.Name)==1;var select=CatalogueArt.Button(selected?"Selected":"Select",()=>{
                Draft.SelectSetup(card.Name);Dirty=true;HideHover();BuildSetupStrip();BuildSetupChoices();UpdateStatus();
            });select.AccessibilityName="Select "+card.Name;select.Disabled=selected;item.AddChild(select);
            item.Resized+=()=>{var w=item.Size.X;Place(face,0,0,w,w*1.4f+8);Place(note,0,w*1.4f+9,w,18);Place(select,12,w*1.4f+30,w-24,36);};
        }
        else
        {
            var minus=CatalogueArt.Button("−",()=>Adjust(card,-1));minus.AccessibilityName="Remove "+card.Name;item.AddChild(minus);
            var count=CatalogueArt.Label(Draft.Count(card.Name).ToString(),22);count.HorizontalAlignment=HorizontalAlignment.Center;if(Draft.Review().OverLimit.Contains(card.Name))count.AddThemeColorOverride("font_color",new Color("f37c64"));item.AddChild(count);
            var plus=CatalogueArt.Button("+",()=>Adjust(card,1));plus.AccessibilityName="Add "+card.Name;item.AddChild(plus);
            var check=new CheckBox{Text=_compare.Contains(card.Id)?"☑ Compare":"☐ Compare",ButtonPressed=_compare.Contains(card.Id)};foreach(var state in new[]{"normal","hover","pressed","hover_pressed","focus"})check.AddThemeStyleboxOverride(state,new StyleBoxEmpty());check.AddThemeFontSizeOverride("font_size",15);check.Toggled+=value=>{
                check.Text=value?"☑ Compare":"☐ Compare";
                if(value&&_compare.Count>=3){check.SetPressedNoSignal(false);check.Text="☐ Compare";Notice("Compare up to three cards at a time. Remove a selection to compare another.");return;}
                if(value)_compare.Add(card.Id);else _compare.Remove(card.Id);face.Selected=value;face.QueueRedraw();UpdateStatus();
            };check.AccessibilityName="Compare "+card.Name;item.AddChild(check);
            item.Resized+=()=>{var w=item.Size.X;var y=w*1.4f+12;Place(face,0,0,w,w*1.4f+8);Place(note,0,y-4,w,18);Place(minus,w/2-59,y+13,32,29);Place(count,w/2-23,y+13,46,29);Place(plus,w/2+27,y+13,32,29);Place(check,Math.Max(0,w/2-53),y+44,110,26);};
        }
        return item;
    }
    private async void Hover(CatalogueData.Card card,Control target)
    {
        var ticket=++_hoverTicket;await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
        if(!IsInstanceValid(this)||!IsInsideTree()||ticket!=_hoverTicket||_overlay.Visible||!IsInstanceValid(target)||!target.IsInsideTree())return;
        Palette.Clear(_hover);var w=290f;var h=465f;
        var panel=new Panel();panel.AddThemeStyleboxOverride("panel",CatalogueArt.Frame(12));panel.MouseFilter=MouseFilterEnum.Ignore;Put(_hover,panel,0,0,w,h);
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
        var panel=new Panel();panel.AddThemeStyleboxOverride("panel",CatalogueArt.Frame(18));Put(_overlay,panel,52,40,_w-104,_h-80);
        Put(panel,CatalogueArt.Label(title,35,true),26,16,panel.Size.X-120,45);Put(panel,CatalogueArt.Button("×",CloseOverlay),panel.Size.X-66,17,40,38);
        return panel;
    }
    public void ShowComparison(CatalogueData.Card[] cards)
    {
        if(cards.Length==0)return;cards=cards.Take(3).ToArray();_inspectionCards=cards;var panel=BeginOverlay(cards.Length==1?"Card inspection":"Compare cards");
        var cell=(panel.Size.X-60)/cards.Length;var faceW=Math.Min(260,(panel.Size.Y-260)/1.4f);
        for(var i=0;i<cards.Length;i++)
        {
            var card=cards[i];var x=30+i*cell;
            Put(panel,CatalogueArt.Label(card.Name,23),x+8,68,cell-16,32);
            Put(panel,new DeckFace(card,Scans),x+(cell-faceW)/2,110,faceW,faceW*1.4f+8);
            var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};Put(panel,scroll,x+14,faceW*1.4f+128,cell-28,panel.Size.Y-(faceW*1.4f+268));
            var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(box);
            box.AddChild(CatalogueArt.Label(card.Playable?"Game printing rules":"Oracle reference · not implemented",16));
            var rules=card.Playable?card.Definition!.Text:CatalogueData.PlainText(card.CurrentOracleText);
            box.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(rules)?"No rules text recorded.":rules,19,CatalogueArt.Ivory));
            if(!card.Playable)box.AddChild(Palette.Wrapped("Reference text may contain later errata. No game effect is granted by adding this card.",15,CatalogueArt.Muted));
            var stats=card.Playable&&card.Definition is {} def ? (card.Types.Contains("Personality")||card.Types.Contains("Follower")?$"Force {def.Force}   ·   Chi {def.Chi}   ·   Gold {def.GoldCost}":$"Gold {def.GoldCost}") : string.Join("   ·   ",new[]{"force","chi","cost"}.Where(k=>card.Stats.ContainsKey(k)).Select(k=>k+" "+string.Join("/",card.Stats[k])));
            Put(panel,CatalogueArt.Label(stats,18),x+18,panel.Size.Y-118,cell-36,27);
            var y=panel.Size.Y-84;
            if(new L5R.Core.DeckCardInfo(card.Id,card.Name,card.Types,card.DeckTypes,card.Keywords,card.Playable).SetupSlot.Length==0)
            {
                var count=CatalogueArt.Label(Draft.Count(card.Name).ToString(),22);count.HorizontalAlignment=HorizontalAlignment.Center;
                Put(panel,CatalogueArt.Label("In deck",17),x+cell/2-150,y,78,34);
                var minus=CatalogueArt.Button("−",()=>{Adjust(card,-1);count.Text=Draft.Count(card.Name).ToString();});Put(panel,minus,x+cell/2-62,y,38,34);Put(panel,count,x+cell/2-18,y,44,34);
                Put(panel,CatalogueArt.Button("+",()=>{Adjust(card,1);count.Text=Draft.Count(card.Name).ToString();}),x+cell/2+32,y,38,34);
            }
        }
        Put(panel,CatalogueArt.Button("Back to deck",CloseOverlay),panel.Size.X/2-90,panel.Size.Y-43,180,34);
    }
    private void Notice(string message)
    {
        var dialog=new AcceptDialog{Title="Deck builder",DialogText=message,Theme=Theme};AddChild(dialog);dialog.Confirmed+=dialog.QueueFree;dialog.Canceled+=dialog.QueueFree;dialog.PopupCentered(new Vector2I(580,190));
    }
}
