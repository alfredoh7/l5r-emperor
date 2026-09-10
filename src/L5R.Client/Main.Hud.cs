using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private void RenderClanHud()
    {
        Palette.Clear(_clanHud);
        foreach(var player in View.Players)
        {
            var production=player.Home.Concat(player.Army).Append(player.Stronghold).Where(c=>!c.Bowed).Sum(c=>Production(c,player));
            var own = player.Id == View.ViewerId;
            var turn = View.WinnerId is int winner ? (winner == player.Id ? "VICTORY" : "MATCH ENDED") :
                View.ActivePlayerId == player.Id ? (own ? "YOUR TURN" : "OPPONENT'S TURN") : "";
            var action = own ? player.Name : player.Name == player.Clan ? "Opponent" : player.Name;
            Place(_clanHud,new ClanPlaque(player.Clan,player.Honor,production,View.FavorPlayerId==player.Id,()=>ShowPile(player.Id,Zone.Dead),
                action, turn, View.WinnerId is null && View.PriorityPlayerId == player.Id, View.WinnerId is null && View.ActivePlayerId == player.Id)
                { ReducedMotion = _reducedMotion },16,own?938:14,270,120);
        }
    }
    private readonly Dictionary<(int,Zone), CardView> _discardPreview = [];
    private void RenderPiles()
    {
        Palette.Clear(_pileHud);
        foreach(var player in View.Players)
        {
            var y=ProvinceRowY(player);
            Zone[] zones=[Zone.DynastyDiscard,Zone.DynastyDeck,Zone.FateDeck,Zone.FateDiscard];
            int[] slots=[0,1,6,7];
            for(var i=0;i<zones.Length;i++)
            {
                var zone=zones[i];
                var key=(player.Id,zone);
                if(zone is Zone.DynastyDiscard or Zone.FateDiscard)
                {
                    var cards=player.Discard.Where(c=>c.Zone==zone).ToArray();
                    var prior=_motionView?.Players.FirstOrDefault(p=>p.Id==player.Id)?.Discard.Select(c=>c.Id).ToHashSet() ?? [];
                    var latest=cards.LastOrDefault(c=>!prior.Contains(c.Id));
                    if(latest is not null) _discardPreview[key]=latest;
                    if(_discardPreview.TryGetValue(key,out var preview) && !cards.Any(c=>c.Id==preview.Id)) _discardPreview.Remove(key);
                }
                Place(_pileHud,new TablePile(player,zone,()=>ShowPile(player.Id,zone),_discardPreview.GetValueOrDefault(key)?.Definition),SlotX(player,slots[i])+31,y+2,142,PlaneHeight(CardHeight)-4);
            }
        }
    }
    private void ShowTableMenu()
    {
        var (dialog,body)=Dialog("The table",560,900);
        dialog.GetOkButton().Hide();
        body.AddThemeConstantOverride("separation", 9);
        body.AddChild(Palette.Text("Emperor",32,Palette.Gold,true));
        body.AddChild(Palette.Wrapped(_status.Text,16,Palette.PaperDim));
        foreach(var (text,action) in new (string,Action)[]{("Practice",ShowPractice),("Private match",ShowOnline),("Card library",()=>LibraryDialog.Show(this)),("Match history",ShowHistory),("Settings",ShowSettings),("Main menu",ShowMainMenu),("Export practice replay",ExportReplay),("Rules coverage",ShowCoverage),("Concede…",ShowConcede)})
            {
            var button = SidebarSkin.Button(text,()=>{dialog.QueueFree(); action();});
            button.CustomMinimumSize = new Vector2(0,48);
            foreach(var state in new[]{"normal","hover","pressed"}) { var skin = SidebarSkin.Surface(true); skin.ContentMarginTop=skin.ContentMarginBottom=8; button.AddThemeStyleboxOverride(state,skin); }
            body.AddChild(button);
        }
        body.AddChild(SidebarSkin.Button("Return to game",dialog.QueueFree,true));
    }
    private void ShowPile(int owner, Zone zone)
    {
        var player=View.Players.Single(p=>p.Id==owner);
        var deck=zone is Zone.FateDeck or Zone.DynastyDeck;
        var name=zone switch { Zone.FateDeck=>"Fate deck",Zone.DynastyDeck=>"Dynasty deck",Zone.FateDiscard=>"Fate discard",Zone.DynastyDiscard=>"Dynasty discard",_=>"Fallen cards" };
        var (_,body)=Dialog(player.Clan+" · "+name,880,720);
        body.AddChild(Palette.Text(player.Clan+" · "+name,27,Palette.Gold,true));
        if(deck)
        {
            body.AddChild(Palette.Wrapped($"{(zone==Zone.FateDeck?player.FateDeckCount:player.DynastyDeckCount)} cards remain. Deck contents and order are hidden.",19));
            if(zone==Zone.FateDeck) body.AddChild(Palette.Text($"{player.HandCount} cards in hand",18));
            return;
        }
        if(zone==Zone.Dead)
        {
            var lost=player.Provinces.Where(p=>p.Destroyed).Select(p=>(p.Index+1).ToString()).ToArray();
            body.AddChild(Palette.Wrapped(lost.Length==0?"No provinces lost.":"Lost provinces: "+string.Join(", ",lost),17,Palette.PaperDim));
        }
        var cards=player.Discard.Where(c=>c.Zone==zone).ToArray();
        body.AddChild(Palette.Text(cards.Length==0?"No cards in this pile.":$"{cards.Length} cards · hover to read",16,Palette.PaperDim));
        var scroll=new ScrollContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        var grid=new GridContainer { Columns=4,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation",12); grid.AddThemeConstantOverride("v_separation",15); scroll.AddChild(grid);
        foreach(var card in cards)
        {
            var tile=new CardTile(card,true,_=>{},_=>{}) { CustomMinimumSize=new Vector2(185,257),FaceHeight=245,ReducedMotion=true,ContextHelp=player.Clan+" · "+name };
            grid.AddChild(tile);
        }
    }
}
