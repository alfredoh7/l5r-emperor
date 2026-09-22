using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class DuelScreen
{
    // Result cards are retained public evidence, never relocated gameplay cards.
    private bool _resultArranged;
    internal double[] CardTransitionTimes=>_flights.GetChildren().OfType<DuelCardTransition>().Select(f=>f.Elapsed).ToArray();
    private Control? _verdict;
    private string? _verdictKey;
    private readonly List<(Control Node,double Start)> _verdictFades=[];
    private const double VerdictCut=2.25, ConsequencesStart=3.35;
    internal int RetainedFocusCount=>_resultArranged?_tiles.Values.Count(t=>t.Card.Zone==Zone.Focused&&t.Card.FaceUp):0;
    internal string ResultExplanation=>Snapshot?.View.LastDuel is {Result:{} result} record?VerdictReason(record,result):"";
    private double EndingDuration=>ConsequencesStart+Math.Max(1,_activeBeats.Length)*BeatDuration+.65;
    private static Vector2 ResultFocusPoint(int side,int index,int count)=>count>2
        ?new((side==0?480:1200)+(index%2-.5f)*160,720+index/2*96)
        :new((side==0?480:1200)+(index-(count-1)/2f)*180,760);
    private void ResetVerdict()
    {
        if(_verdict is not null){_verdict.GetParent()?.RemoveChild(_verdict);_verdict.QueueFree();}
        _verdict=null;_verdictKey=null;_verdictFades.Clear();
    }
    private string VerdictReason(DuelRecord record,DuelResult result)
    {
        if(result.Kind=="Refused")return record.Challenge?.RefusalTerms??"The challenge ended without a duel.";
        var reason=result.ResolutionReason??(result.ChallengerTotal==result.ChallengedTotal?"Equal duel totals":"Higher duel total");
        return $"{reason} · {result.ChallengerTotal} to {result.ChallengedTotal}";
    }
    private void RefreshVerdict()
    {
        if(!_resultArranged||Snapshot?.View.LastDuel is not {Result:{} result} record){ResetVerdict();return;}
        var ending=_cinema==Cinema.Ending;
        var beats=record.Beats??[];
        var shown=ending?Math.Clamp((int)((_cinemaTime-ConsequencesStart)/BeatDuration)+1,0,beats.Length):beats.Length;
        if(ending&&_cinemaTime<ConsequencesStart)shown=0;
        var key=$"{record.Id}:{Snapshot.View.ViewerId}:{result.ResolutionVersion}:{shown}:{ending}:{Size}:{_scale}";
        if(_verdictKey!=key)
        {
            ResetVerdict();_verdictKey=key;
            _verdict=new Control{Name="Duel verdict",Position=_offset,Size=DuelCinematicStage.Design,Scale=Vector2.One*_scale,MouseFilter=MouseFilterEnum.Ignore};
            AddChild(_verdict);MoveChild(_verdict,GetChildren().ToList().IndexOf(_hud));
            // A light effect, not a new placard: smooth opacity falls away before the cards.
            var shadow=new ColorRect{Position=new Vector2(603,555),Size=new Vector2(468,344),MouseFilter=MouseFilterEnum.Ignore,
                Material=new ShaderMaterial{Shader=GD.Load<Shader>("res://Shaders/duel_summary_shadow.gdshader")}};
            _verdict.AddChild(shadow);_verdictFades.Add((shadow,VerdictCut+.4));
            if(result.Kind=="Resolved")
            {
                foreach(var card in new[]{record.Challenger,record.Challenged})
                {
                    var side=card.OwnerId==Snapshot.View.ViewerId?0:1;var x=side==0?480:1200;
                    var challenger=card.Id==record.Challenger.Id;
                    var total=challenger?result.ChallengerTotal:result.ChallengedTotal;
                    var stat=challenger?result.ChallengerStat:result.ChallengedStat;
                    var winner=card.Id==result.WinnerId;
                    if(winner)
                    {
                        var halo=new ColorRect{Position=new Vector2(x-105,514),Size=new Vector2(210,132),MouseFilter=MouseFilterEnum.Ignore,
                            Material=new ShaderMaterial{Shader=GD.Load<Shader>("res://Shaders/duel_number_glow.gdshader")}};
                        _verdict.AddChild(halo);_verdictFades.Add((halo,VerdictCut));
                    }
                    var number=Words(total.ToString(),new Rect2(x-145,515,290,120),134,winner?new Color("ffe09a"):Palette.Paper,true,.55);
                    if(winner)
                    {
                        number.AddThemeColorOverride("font_shadow_color",new Color(.8f,.4f,.06f,.6f));number.AddThemeConstantOverride("shadow_outline_size",2);
                        number.AddThemeConstantOverride("shadow_offset_x",0);number.AddThemeConstantOverride("shadow_offset_y",0);
                    }
                    Words($"{stat} {StatName(record.Stat)} + {total-stat} Focus",new Rect2(x-215,628,430,43),29,Palette.Paper,false,.85);
                    if(result.LoserIds.Contains(card.Id))
                    {
                        // Reuse the approved Japanese brush texture. Its entire rectangle ends
                        // above the arithmetic, for either seat and for a double loss.
                        var lossStroke=new TextureRect{Name=$"Defeat stroke {side}",Texture=GD.Load<Texture2D>("res://Art/DuelUi/red-slash.png"),
                            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
                            Position=new Vector2(x-220,308),Size=new Vector2(440,166),SelfModulate=new Color(1,.85f,.85f,.42f),MouseFilter=MouseFilterEnum.Ignore};
                        _verdict.AddChild(lossStroke);_verdictFades.Add((lossStroke,VerdictCut+.32));
                    }
                    var down=IsFallen(card.Id);
                    Words(winner?"VICTORY":result.LoserIds.Contains(card.Id)?"DEFEAT":"",new Rect2(x-220,down?170:4,440,80),winner?74:51,winner?new Color("ffe8b7"):Palette.PaperDim,true,VerdictCut+.18);
                    if(winner)
                    {
                        var slash=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/DuelUi/red-slash.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,Position=new Vector2(x-110,79),Size=new Vector2(225,24),MouseFilter=MouseFilterEnum.Ignore};
                        _verdict.AddChild(slash);_verdictFades.Add((slash,VerdictCut+.3));
                    }
                }
            }
            var name=result.Kind=="Refused"?"Challenge refused":result.WinnerId is null?"Both lose":$"{(result.WinnerId==record.Challenger.Id?record.Challenger:record.Challenged).Definition!.Name} wins";
            Words(name,new Rect2(614,598,444,49),37,new Color("fff0c4"),false,VerdictCut+.4);
            Words(VerdictReason(record,result),new Rect2(620,651,432,52),23,Palette.Paper,false,VerdictCut+.55);
            // Present all beats in execution order. The held summary keeps the first two;
            // every remaining source/line stays available in the existing detailed account.
            var first=ending?Math.Max(0,shown-2):0;
            var account=ending?beats:beats.Where(b=>b.Lines.Any(l=>l!="No change from this effect.")).ToArray();
            if(account.Length==0)account=beats;
            var selected=account.Skip(first).Take(Math.Min(2,shown)).ToArray();
            var y=703f;
            foreach(var beat in selected)
            {
                var start=ConsequencesStart+Array.IndexOf(beats,beat)*BeatDuration;
                var heading=beat.Source?.Definition?.Name??beat.SourceLabel??(beat.Kind=="Aftermath"?"After the duel":"Consequence");
                Words(heading,new Rect2(621,y,430,32),26,new Color("fff0c4"),false,start);
                var lines=beat.Lines.Take(2).ToArray();
                Words(string.Join("\n",lines),new Rect2(621,y+32,430,48),21,Palette.Paper,false,start+.13);
                y+=lines.Length>1?90:75;
            }
            if(!ending&&beats.Length>2)Words($"{beats.Length-2} more consequences in Duel details",new Rect2(621,865,430,24),16,Palette.PaperDim,false,0);
            IgnoreHover(_verdict);
        }
        foreach(var (node,start) in _verdictFades)
        {
            var alpha=ending?Ease((_cinemaTime-start)/.65):1;
            node.Modulate=new Color(1,1,1,alpha);
        }
        var glow=ending?Ease((_cinemaTime-VerdictCut)/.85):1;
        foreach(var tile in _tiles.Values.OfType<DuelCardTile>())
        {
            var winningOwner=result.WinnerId==record.Challenger.Id?record.Challenger.OwnerId:record.Challenged.OwnerId;
            var winner=result.WinnerId is not null&&(tile.Card.Id==result.WinnerId||record.Attachments.Any(a=>a.Id==tile.Card.Id&&a.AttachedTo==result.WinnerId)||tile.Card.Zone==Zone.Focused&&tile.Card.OwnerId==winningOwner);
            tile.WinnerGlow=winner?glow:0;tile.QueueRedraw();
        }
        Label Words(string text,Rect2 bounds,int size,Color color,bool brush,double start)
        {
            var label=DuelUi.Text(text,size,color,brush);label.Position=bounds.Position;label.Size=bounds.Size;
            label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;
            label.AutowrapMode=TextServer.AutowrapMode.WordSmart;
            label.AddThemeColorOverride("font_shadow_color",new Color(0,0,0,.95f));label.AddThemeConstantOverride("shadow_offset_y",2);
            _verdict!.AddChild(label);_verdictFades.Add((label,start));return label;
        }
    }
    private bool IsFallen(string id)=>Snapshot!.View.Players.SelectMany(p=>p.Discard.Concat(p.RemovedFromGame)).Any(c=>c.Id==id&&c.Zone is Zone.Dead or Zone.FateDiscard or Zone.DynastyDiscard or Zone.RemovedFromGame);
}
