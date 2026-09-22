using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Presentation of permitted snapshots and legal choices only. No inferred reaction
    // windows, Naval priority, automatic passes or client-computed resolution.
    private bool RenderBattleFlow()
    {
        if (View.WinnerId is not null || !BattleTracker.InBattle(View.Phase) && _resolutionMessage is null && !PresentationBusy) return false;
        if (!PresentationBusy && (_selected is not null || _chosen is not null ||
            _selectedHomeReminder is not null && View.PendingChoice is null)) return false;
        _guide.Visible = false;
        _sidebarHeader.AddChild(new BattleTracker { Snapshot=View, ReducedMotion=_reducedMotion,
            PresentationInProgress=PresentationBusy, ActionOutcome=PresentationBusy && _actionOutcome,
            ResolutionMessage=PresentationBusy ? _actionOutcome?null:_battleOutcomeText : _resolutionMessage });
        if(!PresentationBusy && View.PendingChoice is { } pending)
        {
            FlowText(pending.Prompt,22,Palette.Paper);
            if(View.PriorityPlayerId==View.ViewerId)
                foreach(var option in Actions.Where(a=>a.Enabled && a.Command is not null && a.Type is not ("pass" or "declineReaction" or "proclaim" or "chooseShinden" or "concede"))) AddAction(_inspector,option);
            return true;
        }
        if (PresentationBusy || _resolutionMessage is not null)
        {
            FlowText(PresentationBusy && _actionOutcome ? _battleDefeatHeadline : _journal.Outcome,22,Palette.Gold,true);
            var forceResult=_journal.Exchanges.LastOrDefault(e=>e.Events.Any(x=>x.Type=="battleResolved"))?.Events.FirstOrDefault(e=>e.Type=="battleResolved");
            if(forceResult is not null) FlowText(forceResult.Message,17,Palette.PaperDim);
            foreach(var receipt in _journal.Aftermath)
                FlowCard(receipt.Name,receipt.Destination switch {"Dead"=>"Destroyed · moved to dead pile","Home · bowed"=>"Returned home bowed","Home"=>"Returned home",_=>"Moved to "+receipt.Destination},receipt.Destination=="Dead");
            var resolution = _journal.Exchanges.FindLastIndex(e=>e.Events.Any(x=>x.Type=="battleResolved"));
            if(resolution>=0)
                foreach(var e in _journal.Exchanges.Skip(resolution).SelectMany(e=>e.Events).Where(e=>e.Type=="honorChanged" || e.Type=="provinceDestroyed" && e.Message!=_journal.Outcome))
                    FlowText(e.Message,16,Palette.PaperDim);
            _inspector.AddChild(SidebarSkin.Button("Review battle",ShowBattleHistory));
            if(PresentationBusy) { FlowText(_actionOutcome?"Applying the effect…":"Applying battle results…",16,Palette.PaperDim); return true; }
            if(!BattleTracker.InBattle(View.Phase)) { FlowText("Battle complete · "+HumanPhase(View.Phase),16,Palette.PaperDim); return true; }
        }
        if(View.PriorityPlayerId!=View.ViewerId)
        {
            var clan=View.Players.Single(p=>p.Id==View.PriorityPlayerId).Clan;
            FlowText(clan+(View.Phase==Phase.Battle?" is choosing a Battle action.":View.Phase==Phase.BattleSelection?" is choosing the next battle.":" is making assignments."),20,Palette.Paper);
            RenderBattleExchanges(); return true;
        }
        if(View.Phase==Phase.BattleSelection)
        {
            FlowText("Choose the province to fight next.",22,Palette.Paper);
            foreach(var option in Actions.Where(a=>a.Type=="selectBattle" && a.Enabled))
                AddAction(_inspector,option with {Label=$"Province {option.Command!.Province+1}  ›"});
            FlowText("Selecting a province begins that battle.",15,Palette.PaperDim);
        }
        else if(View.Phase is Phase.AttackDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense)
        {
            var cavalry=View.Phase is Phase.CavalryDeclaration or Phase.CavalryDefense;
            var attack=View.Phase is Phase.AttackDeclaration or Phase.CavalryDeclaration;
            FlowText(attack?"You are attacking.":"You are defending.",20,Palette.Paper);
            var option=Actions.FirstOrDefault(a=>a.Type is "attack" or "assignDefense" && a.Enabled);
            if(option is not null)
            {
                FlowText($"Assign your {(cavalry?"Cavalry":"Infantry")} to provinces.",20,Palette.PaperDim);
                _inspector.AddChild(SidebarSkin.Button("Assign units  ›",()=>Choose(option)));
            }
            else FlowText("No eligible units to assign in this step.",18,Palette.PaperDim);
            // Committed units stay legible while the next maneuvers segment is considered.
            foreach(var player in View.Players)
                foreach(var group in player.Army.Where(c=>c.AttachedTo is null).GroupBy(c=>c.ProvinceIndex))
                    FlowCard($"Province {group.Key+1}",$"{player.Clan} · {group.Count()} committed unit{(group.Count()==1?"":"s")}");
        }
        else if(View.Phase==Phase.Battle)
        {
            FlowText("Choose a Battle action or pass.",22,Palette.Paper);
            // The engine's action set can grow (for example Reserve recruitment).
            // Omit controls already in the fixed footer and the menu's confirmed
            // Concede flow; conceding is not a Battle action.
            var options=Actions.Where(a=>a.Enabled && a.Command is not null &&
                a.Type is not ("pass" or "endDynasty" or "declineReaction" or "proclaim" or "chooseShinden" or "concede")).ToArray();
            RenderBattleExchanges(1);
            foreach(var option in options) AddBattleAction(option);
            if(options.Length==0) FlowText("No Battle action is currently available.",17,Palette.PaperDim);
        }
        return true;
    }

    private void AddBattleAction(ActionOption option)
    {
        AddAction(_inspector,option);
        var button=_inspector.GetChildren().OfType<Button>().Last();
        var source=option.CardId is { } id?FindCard(id)?.Definition?.Name:null;
        if(source is null)return;
        button.Text=source+"\n"+option.Label;
        foreach(var state in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color","font_disabled_color"})button.AddThemeColorOverride(state,Colors.Transparent);
        var margin=new MarginContainer {MouseFilter=Control.MouseFilterEnum.Ignore};button.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.OffsetLeft=20;margin.OffsetRight=-20;margin.OffsetTop=12;margin.OffsetBottom=-12;
        var column=new VBoxContainer {MouseFilter=Control.MouseFilterEnum.Ignore};margin.AddChild(column);
        var title=Palette.Wrapped(source,19);title.AddThemeFontOverride("font",SidebarSkin.Bold);title.MouseFilter=Control.MouseFilterEnum.Ignore;column.AddChild(title);
        var text=Palette.Wrapped(option.Label,16,Palette.PaperDim);text.MouseFilter=Control.MouseFilterEnum.Ignore;column.AddChild(text);
        button.CustomMinimumSize=new Vector2(0,110);
        column.MinimumSizeChanged+=()=> { if(IsInstanceValid(button))button.CustomMinimumSize=new Vector2(0,Math.Max(90,column.GetCombinedMinimumSize().Y+24)); };
    }

    private void FlowText(string text,int size,Color color,bool bold=false)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        var label=Palette.Wrapped(text,size,color);
        if(bold)label.AddThemeFontOverride("font",SidebarSkin.Bold);
        _inspector.AddChild(label);
    }
    private void FlowCard(string title,string detail,bool destroyed=false)
    {
        var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",SidebarSkin.Inset());
        var column=new VBoxContainer();panel.AddChild(column);
        var name=Palette.Wrapped(title,19);name.AddThemeFontOverride("font",SidebarSkin.Bold);column.AddChild(name);
        column.AddChild(Palette.Wrapped(detail,16,destroyed?new Color("d79683"):Palette.PaperDim));
        _inspector.AddChild(panel);
    }
    private void RenderBattleExchanges(int count=3)
    {
        var start=_journal.Exchanges.FindLastIndex(e=>e.Events.Any(x=>x.Type=="battleStarted"));
        var recent=_journal.Exchanges.Skip(Math.Max(0,start)).TakeLast(count).ToArray();
        if(recent.Length==0)return;
        _inspector.AddChild(new HSeparator());FlowText("Recent exchanges",17,Palette.PaperDim);
        foreach(var exchange in recent)
        {
            var actor=exchange.Player is int player?View.Players.Single(p=>p.Id==player).Clan:"Battle";
            var details=string.Join("\n",exchange.Events.Select(e=>e.Message));
            FlowCard(actor,details,exchange.Events.Any(e=>e.Type is "unitDestroyed" or "attachmentDestroyed"));
        }
        _inspector.AddChild(SidebarSkin.Button("Earlier exchanges  ›",ShowBattleHistory));
    }
    private void ShowBattleHistory()
    {
        var (_,body)=Dialog("Battle exchanges",780,740);
        body.AddChild(Palette.Text("Battle history",27,title:true));
        var scroll=new ScrollContainer {SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};body.AddChild(scroll);
        var rows=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(rows);
        foreach(var exchange in _journal.Exchanges.AsEnumerable().Reverse())
        {
            var label=Palette.Wrapped(exchange.Heading,20,Palette.Gold);label.AddThemeFontOverride("font",SidebarSkin.Bold);rows.AddChild(label);
            foreach(var e in exchange.Events.Skip(1))rows.AddChild(Palette.Wrapped(e.Message,17,Palette.PaperDim));
            rows.AddChild(new HSeparator());
        }
    }
}
