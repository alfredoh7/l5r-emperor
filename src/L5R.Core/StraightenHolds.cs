namespace L5R.Core;

// Timed straighten holds (issue #415, Mendon's rulings of 2026-09-21): one primitive for "negate its straightening until ...".
//
// A hold names WHOSE turn counts (Owner; null = the turn in progress, whoever's), WHICH point of that turn releases it (StraightenPoint), and how many of those points must go
// by first (Skip). While a card has any hold it is not straightened -- not by the Straighten phase, not by an effect. The cards it covers:
//   Hibernation             "until your next turn begins"                  Owner = controller, TurnBegins, Skip 0 (the Straighten phase of that turn straightens it)
//   Bamboo Harvesters       "will not straighten before your second turn"  seeded from the printed trait: TurnBegins, Skip 1 before the first turn, 0 after it begins
//   Visitation              "until after you reveal cards in your Provinces at the start of your next turn"   EventsRevealEnds
//   Aramasu's Legacy, Egg of P'an Ku   "until after your next turn begins"   TurnBegins
//   Jade Mine               "until after your next Action Phase"           ActionPhaseEnds
//   Unseasonable Weather, Goju Asuka, Yasuki Daito   "during his controller's next / this / the next Straighten Phase"   StraightenPhaseEnds
//   Fortress of Lidless Eyes, Bonds of Hospitality   "while this card remains in play"   WhileInPlay
// The "this turn" forms (Basic Lesson's Stronghold, Shinden Asahina, Soshi Kodanshi) are TurnEnds holds with no Owner in this model; the existing per-turn fields
// StraightenLockedTurn and StraightenBlockedTurn keep them, and "negate his NEXT straightening (this turn)" (Quagmire, Hizumi, Sutigu ...) is a one-shot, not a timed hold,
// so it stays there too.
public sealed partial class MatchEngine
{
    // "This card will not straighten before your Nth turn": the number of its controller's turn from which it may straighten.
    private static int PrintedStraightenTurn(CardDefinition definition) => definition.Effect switch
    {
        "skip-first-straighten" => 2,
        // Temple to Fudo (Seeds of Decay, issue #453): "This Holding will not straighten before your third turn."
        TempleToFudoEffect => 3,
        // Bamboo Harvesters - experienced (Forgotten Legacy pass, issue #467): "This card will not straighten before your third turn."
        BambooHarvestersExpEffect => 3,
        _ => 0
    };

    private void AddStraightenHold(CardInstance card, string source, int? owner, StraightenPoint until, int skip = 0, string? whileInPlay = null)
    {
        card.StraightenHolds.Add(new StraightenHold { Source = source, Owner = owner, Until = until, Skip = skip, WhileInPlay = whileInPlay });
        Emit("straightenHeld", $"{Definition(card).Name} will not straighten until {DescribePoint(until, owner)}.", card.OwnerId, card.Id);
    }

    private string DescribePoint(StraightenPoint point, int? owner)
    {
        var who = owner is int id ? $"{Player(id).Name}'s " : "the ";
        return point switch
        {
            StraightenPoint.TurnBegins => $"{who}next turn begins",
            StraightenPoint.StraightenPhaseEnds => $"{who}next Straighten Phase ends",
            StraightenPoint.EventsRevealEnds => $"{who}cards are revealed at the start of the next turn",
            StraightenPoint.ActionPhaseEnds => $"{who}next Action Phase ends",
            StraightenPoint.PhaseEnds => "this phase ends",
            _ => "this turn ends"
        };
    }

    // The holds a printed trait implies ("will not straighten before your second turn"), created the first time anything looks at the card -- a card put into play by a
    // scenario or a deck build never went through an entry hook. Skip counts the controller's turns still to begin with the hold standing: before their first turn it is N-1 (the
    // start of the game: "1", then 0 once that first turn has begun), and a card that came into play after the turns it waits for have gone by needs no hold at all.
    private void SeedPrintedStraightenHolds(CardInstance card, bool turnBeginning = false)
    {
        if (card.PrintedHoldsSeeded || !InPlay(card)) return;
        var turn = PrintedStraightenTurn(Definition(card));
        if (turn <= 0) return;
        card.PrintedHoldsSeeded = true;
        var skip = turn - 1 - Player(card.OwnerId).TurnsTaken + (turnBeginning ? 1 : 0);
        if (skip >= 0) card.StraightenHolds.Add(new StraightenHold { Source = Definition(card).Name, Owner = card.OwnerId, Until = StraightenPoint.TurnBegins, Skip = skip });
    }

    private bool StraightenHeld(CardInstance card)
    {
        SeedPrintedStraightenHolds(card);
        card.StraightenHolds.RemoveAll(h => h.WhileInPlay is { } source && !(_state.Cards.TryGetValue(source, out var held) && InPlay(held)));
        return card.StraightenHolds.Count > 0;
    }

    // A card the effect cannot straighten right now: for the legality of an action that would only straighten it.
    private bool StraightenRefused(CardInstance card) => card.StraightenLockedTurn == _state.TurnNumber || StraightenHeld(card) || IsTerrainCard(card); // issue #492 (ruling R5): a Terrain may not be straightened

    // The same for the view, which must not change the state: a printed hold not yet seeded counts when it would be created standing.
    private bool StraightenRefusedInView(CardInstance card) =>
        card.StraightenLockedTurn == _state.TurnNumber || IsTerrainCard(card)
        || card.StraightenHolds.Any(h => h.WhileInPlay is not { } source || _state.Cards.TryGetValue(source, out var held) && InPlay(held))
        || !card.PrintedHoldsSeeded && InPlay(card) && PrintedStraightenTurn(Definition(card)) is > 0 and var turn && turn - 1 - Player(card.OwnerId).TurnsTaken >= 0;

    // A point of the active player's turn has come: the holds waiting for it count it, and a hold whose count is spent is released.
    private void AdvanceStraightenHolds(StraightenPoint point)
    {
        var active = _state.ActivePlayerId;
        foreach (var card in _state.Cards.Values.ToArray())
        {
            if (!InPlay(card)) { card.StraightenHolds.Clear(); card.PrintedHoldsSeeded = false; continue; }
            if (point == StraightenPoint.TurnBegins && card.OwnerId == active) SeedPrintedStraightenHolds(card, turnBeginning: true);
            if (card.StraightenHolds.Count == 0) continue;
            foreach (var hold in card.StraightenHolds.Where(h => h.Until == point && (h.Owner is null || h.Owner == active)).ToArray())
            {
                if (hold.Skip > 0) { hold.Skip--; continue; }
                card.StraightenHolds.Remove(hold);
                Emit("straightenReleased", $"{Definition(card).Name} may straighten again.", card.OwnerId, card.Id);
            }
        }
    }
}
