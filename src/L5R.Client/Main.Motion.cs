using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private MatchView? _motionView;
    private readonly List<CardFlight> _flights = [];
    private record MotionFrame(MatchView View, Dictionary<string,Vector2[]> Cards,
        Dictionary<string,Vector2> TableOrigins,
        Dictionary<(int,Zone),(Vector2[] Corners,float Count,Vector2 Origin)> Piles);
    private IEnumerable<TablePile> TablePiles => _pileHud.GetChildren().OfType<TablePile>();
    private Vector2[] PileCorners(TablePile pile)
    {
        var r = pile.TopFace;
        return new[] { r.Position, new Vector2(r.End.X,r.Position.Y),r.End,new Vector2(r.Position.X,r.End.Y) }
            .Select(p => _tableSurface.ProjectCanvas(pile.GetGlobalTransform()*p) / _root.Scale.X).ToArray();
    }
    private MotionFrame? BeginTableMotion()
    {
        foreach(var flight in _flights) if(IsInstanceValid(flight)) flight.Finish();
        _flights.Clear();
        if(_motionView is null || _motionView.ViewerId != View.ViewerId || _motionView.Scenario != View.Scenario ||
           View.Version <= _motionView.Version || _reducedMotion) return null;
        return new(_motionView, _tiles.Where(IsInstanceValid).GroupBy(t=>t.Card.Id)
            .ToDictionary(g=>g.Key,g=>g.First().FaceCornersOnViewport().Select(p=>p/_root.Scale.X).ToArray()),
            _tiles.Where(t => IsInstanceValid(t) && _world.IsAncestorOf(t)).ToDictionary(t => t.Card.Id,
                t => _world.GetGlobalTransform().AffineInverse() * t.GlobalPosition),
            TablePiles.ToDictionary(p=>(p.OwnerId,p.PileZone),p=>(PileCorners(p),p.ShownCount,
                _world.GetGlobalTransform().AffineInverse() * p.GlobalPosition)));
    }
    private static IEnumerable<CardView> VisibleCards(MatchView view) => view.Players.SelectMany(p => p.Home.Concat(p.Army)
        .Concat(p.Hand).Concat(p.Discard).Concat(p.Provinces.Where(v=>v.Card is not null).Select(v=>v.Card!)).Append(p.Stronghold));
    private void EndTableMotion(MotionFrame? before)
    {
        _motionView = View;
        if(before is null) return;
        var old = VisibleCards(before.View).GroupBy(c=>c.Id).ToDictionary(g=>g.Key,g=>g.First());
        var next = VisibleCards(View).GroupBy(c=>c.Id).ToDictionary(g=>g.Key,g=>g.First());
        foreach(var pile in TablePiles)
            if(before.Piles.TryGetValue((pile.OwnerId,pile.PileZone),out var prior))
            {
                pile.SettleFrom(prior.Count,_reducedMotion);
                var start = pile.GetParent<Control>().GetGlobalTransform().AffineInverse() * (_world.GetGlobalTransform() * prior.Origin);
                if (start.DistanceTo(pile.Position) > .5f)
                    pile.AddChild(new TableCardReflow { Card = pile, From = start, To = pile.Position, ReducedMotion = () => _reducedMotion });
            }
        foreach(var tile in _tiles.Where(t=>t.Card.Zone==Zone.Hand))
        {
            if(!before.Cards.TryGetValue(tile.Card.Id,out var corners) || old.GetValueOrDefault(tile.Card.Id)?.Zone!=Zone.Hand) continue;
            var destination=tile.Position;
            var center=corners.Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
            tile.Position=center-_hand.Position-tile.Size/2;
            tile.CreateTween().TweenProperty(tile,"position",destination,.32).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        var stagger = 0;
        // A visible province card that disappears without entering another public zone
        // was returned to the Dynasty deck (Border Keep). Its known face may travel
        // back, but the deck never supplies an identity or an ordering to this layer.
        foreach(var card in old.Values.Where(c=>c.Zone==Zone.Province && c.Definition is not null && !next.ContainsKey(c.Id)))
        {
            if(!before.Cards.TryGetValue(card.Id,out var from)) continue;
            var pile=TablePiles.First(p=>p.OwnerId==card.OwnerId && p.PileZone==Zone.DynastyDeck);
            var texture=CardTile.Artwork(card.Definition!);
            if(texture is null) continue;
            var flight=new CardFlight { From=from,Destination=()=>PileCorners(pile),Face=TablePile.DynastyBack,Back=texture,Delay=stagger++*.075 };
            _root.AddChild(flight);_flights.Add(flight);
        }
        foreach(var card in next.Values)
        {
            old.TryGetValue(card.Id,out var previous);
            var moved = previous is not null && (previous.Zone != card.Zone || previous.ProvinceIndex != card.ProvinceIndex || previous.AttachedTo != card.AttachedTo);
            var draw = previous is null && card.Zone is Zone.Hand or Zone.Province;
            var publicDiscard = previous is null && card.Zone==Zone.FateDiscard && card.OwnerId!=View.ViewerId;
            var restingTile = _tiles.FirstOrDefault(t => t.Card.Id == card.Id && _world.IsAncestorOf(t));
            if (!moved && !draw && previous is not null && restingTile is not null &&
                before.TableOrigins.TryGetValue(card.Id, out var oldOrigin))
            {
                // Resting control origins exclude hover growth and bow geometry.
                // A tray card has no TableOrigins entry, so folding cannot fly it onto the mat.
                var start = restingTile.GetParent<Control>().GetGlobalTransform().AffineInverse() *
                    (_world.GetGlobalTransform() * oldOrigin);
                if (start.DistanceTo(restingTile.Position) > .5f)
                    restingTile.AddChild(new TableCardReflow { Card = restingTile, From = start, To = restingTile.Position, ReducedMotion = () => _reducedMotion });
            }
            if(!moved && !draw && !publicDiscard) continue;
            Vector2[]? from = before.Cards.GetValueOrDefault(card.Id);
            if(draw && card.Zone==Zone.Province)
            {
                var priorPlayer=before.View.Players.Single(p=>p.Id==card.OwnerId);
                var nowPlayer=View.Players.Single(p=>p.Id==card.OwnerId);
                var hidden=priorPlayer.Provinces.FirstOrDefault(p=>p.Index==card.ProvinceIndex)?.Card;
                if(hidden?.Definition is null && hidden is not null && priorPlayer.DynastyDeckCount==nowPlayer.DynastyDeckCount)
                    from=before.Cards.GetValueOrDefault(hidden.Id);
            }
            var fromZone = draw ? card.Zone == Zone.Hand ? Zone.FateDeck : Zone.DynastyDeck : publicDiscard ? Zone.Hand : previous!.Zone;
            from ??= before.Piles.GetValueOrDefault((card.OwnerId,fromZone)).Corners;
            if(from is null && publicDiscard && before.Piles.TryGetValue((card.OwnerId,Zone.FateDeck),out var handOrigin))
                from=handOrigin.Corners.Select(p=>p+new Vector2(0,-110)).ToArray();
            if(from is null) continue;
            var tile = _tiles.FirstOrDefault(t=>t.Card.Id==card.Id);
            var pile = TablePiles.FirstOrDefault(p=>p.OwnerId==card.OwnerId && p.PileZone==card.Zone);
            if(tile is null && pile is null) continue;
            var texture = card.Definition is {} def ? CardTile.Artwork(def) : null;
            var back = GD.Load<Texture2D>(card.Zone==Zone.Hand || fromZone==Zone.FateDeck ? "res://Art/original-fate-back.png" : "res://Art/original-dynasty-back.png");
            texture ??= back;
            if(tile is not null) tile.SelfModulate = new Color(1,1,1,0);
            Func<Vector2[]> destination = tile is not null ? ()=>tile.FaceCornersOnViewport().Select(p=>p/_root.Scale.X).ToArray() : ()=>PileCorners(pile!);
            var flight = new CardFlight { From=from, Destination=destination, Face=texture, Back=draw && card.Definition is not null ? back : null,
                Delay=stagger++*.075, Landed=()=>{if(tile is not null && IsInstanceValid(tile)) tile.SelfModulate=Colors.White;} };
            _root.AddChild(flight); _flights.Add(flight);
        }
        // Opponent draws are represented using only public counts and the green back.
        foreach(var player in View.Players.Where(p=>p.Id!=View.ViewerId))
        {
            var prior=before.View.Players.Single(p=>p.Id==player.Id);
            var lastEvent=before.View.Events.LastOrDefault()?.Sequence ?? -1;
            var draws=View.Events.Count(e=>e.Sequence>lastEvent && e.Type=="cardDrawn" && e.PlayerId==player.Id);
            if(!before.Piles.TryGetValue((player.Id,Zone.FateDeck),out var source)) continue;
            for(var i=0;i<draws;i++)
            {
                var end=source.Corners.Select(p=>p+new Vector2(0,-110)).ToArray();
                var flight=new CardFlight { From=source.Corners,Destination=()=>end,Face=GD.Load<Texture2D>("res://Art/original-fate-back.png"),Delay=i*.09,FadeOnArrival=true };
                _root.AddChild(flight);_flights.Add(flight);
            }
        }
        if(_flights.Count>0) GD.Print($"CARD_MOTION: {_flights.Count} permitted card transfers at version {View.Version}.");
    }
}
