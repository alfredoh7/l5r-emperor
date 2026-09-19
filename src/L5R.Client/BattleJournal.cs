using L5R.Core;

namespace L5R.Client;

public sealed record BattleExchange(long Sequence, string Heading, int? Player, List<GameEvent> Events);
public sealed record BattleReceipt(string Name, string Destination, string CardId);

/// <summary>Public-event presentation only. Never reads engine internals or calculates outcomes.</summary>
public sealed class BattleJournal
{
    public List<BattleExchange> Exchanges { get; } = [];
    public List<BattleReceipt> Aftermath { get; } = [];
    public string Outcome { get; private set; } = "";
    public long LastSequence { get; private set; } = -1;
    public int Revision { get; private set; }
    private bool _inBattle;
    public void Reset()
    { Exchanges.Clear(); Aftermath.Clear(); Outcome=""; LastSequence=-1; _inBattle=false; Revision++; }
    public static bool BattlePhase(Phase p) => p is Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.BattleSelection or Phase.Battle;
    public void Observe(MatchView next, MatchView? previous)
    {
        var incoming=next.Events.Where(e=>e.Sequence>LastSequence).ToArray();
        var currentBatch=false;
        if (LastSequence<0) _inBattle=BattlePhase(next.Phase);
        foreach(var e in incoming)
        {
            if(e.Type=="phaseChanged" && Enum.TryParse<Phase>(e.Message.Replace("Phase: ","").TrimEnd('.'),out var phase))
            { _inBattle=BattlePhase(phase); continue; }
            if(e.Type is "unitAssigned" or "attackDeclared" or "battleStarted") _inBattle=true;
            if(!_inBattle) continue;
            if(e.Type is "attackDeclared" or "battleStarted") { Aftermath.Clear(); Outcome=""; }
            var anchor=e.Type is "unitAssigned" or "attackDeclared" or "battleStarted" or "battleResolved" or "abilityUsed" or "strategyPlayed" or "passed" or "reactionPassed" or "reactionWindow" or "reactionClosed";
            if(anchor || Exchanges.Count==0 || !currentBatch)
            { Exchanges.Add(new(e.Sequence,e.Message,e.PlayerId,[e])); currentBatch=true; }
            else Exchanges[^1].Events.Add(e);
            if(e.Type=="battleResolved") Outcome=e.Message;
            if(e.Type=="provinceDestroyed") Outcome=e.Message;
            Revision++;
        }
        if(previous is not null && incoming.Any(e=>e.Type is "battleResolved" or "unitDestroyed" or "attachmentDestroyed"))
        {
            Aftermath.Clear();
            var old=Visible(previous).Where(c=>c.FaceUp && c.Definition is not null).DistinctBy(c=>c.Id).ToDictionary(c=>c.Id);
            foreach(var card in Visible(next).Where(c=>c.FaceUp && c.Definition is not null).DistinctBy(c=>c.Id))
            {
                if(!old.TryGetValue(card.Id,out var before) || before.Zone==card.Zone) continue;
                var destination=card.Zone switch {
                    Zone.Dead=>"Dead", Zone.FateDiscard=>"Fate discard", Zone.DynastyDiscard=>"Dynasty discard",
                    Zone.Home when before.Zone==Zone.Battlefield=>card.Bowed?"Home · bowed":"Home",
                    _=>null };
                if(destination is not null) Aftermath.Add(new(card.Definition!.Name,destination,card.Id));
            }
            Revision++;
        }
        LastSequence=Math.Max(LastSequence,next.Events.LastOrDefault()?.Sequence??0);
        _inBattle=BattlePhase(next.Phase);
        if(Exchanges.Count>80) Exchanges.RemoveRange(0,Exchanges.Count-80);
    }
    public static IEnumerable<CardView> Visible(MatchView view) => view.Players.SelectMany(p=>p.Home.Concat(p.Army).Concat(p.Hand).Concat(p.Discard).Concat(p.Provinces.Where(q=>q.Card is not null).Select(q=>q.Card!)));
}
