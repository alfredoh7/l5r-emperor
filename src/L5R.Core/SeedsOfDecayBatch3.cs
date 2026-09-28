namespace L5R.Core;

// Seeds of Decay pass (issue #453), batch 3: the helpers behind this batch's Personalities. The ability definitions are at the end of AbilityCatalog.All, the
// execution cases at the end of RunAbilityEffects' switch, and the readings in the batch report. Three small general pieces live here beside the per-card
// helpers:
//   * an Exchange of two stats (core rules Glossary: "note both stats' current values, then simultaneously set each to the other's noted value"), as two
//     modifiers -- Asako Jukko, Tanemono;
//   * a per-card "the first time each turn another player's action moves this card, negate the movement" trait (Hida Kaiji), consumed where a movement is applied
//     (MoveHome, TryMoveToBattlefield) and stamped on the card so it fires once a turn;
//   * a per-card victory stamp, "in an attacking army when it won a battle" (Yoritomo Iwashi), left by battle resolution on every Personality of the winning
//     attacking army.
// The stamps are kept in CardInstance.AbilityUses (a turn/number per key), the one per-card dictionary that already follows a card through an overlay and is
// cleared when it leaves play, under keys no ability id uses.
public sealed partial class MatchEngine
{
    /// <summary>Hida Kaiji's Effect: "Before the first time each turn another player's action moves Kaiji: Negate his movement from that action."</summary>
    public const string NegateFirstEnemyMoveEachTurnEffect = "negate-first-enemy-move-each-turn";
    /// <summary>Tanemono's Effect: "Tanemono will not be controlled by a Phoenix Clan player." -- read with Loyal's own control check (LoyaltyAllowsControl).</summary>
    public const string TanemonoNoPhoenixControlEffect = "tanemono-no-phoenix-control";

    private const string EnemyMoveNegatedTurnKey = "trait:enemy-move-negated-turn";
    private const string AttackVictoryTurnKey = "trait:won-battle-attacking-turn";

    // The per-id conditions of this batch's cards, in ValidateAbility after every other per-id Require (a slot candidate never reaches here: ValidateAbility returns
    // for it first, so these judge the whole command).
    private void ValidateSeedsOfDecayBatch3(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // "If there are now more units there on the enemy side than on yours, you may target an enemy unit and move it home." -- judged for the position after his own
            // move: he joins the army when he would be opposed and is not there yet.
            case "chukage-move" when SlotCards(command, "enemy").Any():
            {
                var battle = _state.CurrentBattleProvince!.Value;
                var enemies = Army(1 - player.Id, battle).Count();
                var joins = enemies > 0 && !(source!.Zone == Zone.Battlefield && source.ProvinceIndex == battle) ? 1 : 0;
                Require(enemies > Army(player.Id, battle).Count() + joins, "The enemy must have more units at the battlefield than you once Chukage has moved.");
                break;
            }
            // "if Hawado is at the current battlefield, target a Personality that performed it".
            case "hawado-exp-move-home":
            case "hawado-exp-move-battlefield":
                Require(AtBattle(source!), "Shosuro Hawado must be at the current battlefield.");
                Require(_state.BattleActionReactionPerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer) && InPlay(performer)
                    && Definition(performer).Type == CardType.Personality, "The Battle action had no Personality performer in play to move.");
                break;
            // "If Iwashi has been in an attacking army when it won a battle since your last turn began" -- his own victory stamp (MarkAttackVictory) from this or the
            // previous turn of this player's; the turn counter advances once per player turn, so "your last turn" began at TurnNumber - 2.
            case "iwashi-bow":
                Require(WonBattleAttackingSinceLastTurnBegan(source!), "Iwashi has not been in an attacking army that won a battle since your last turn began.");
                break;
        }
    }

    // Exchange (core rules Glossary): the card's current Force and Chi are noted, then each is set to the other's noted value -- two modifiers whose deltas are the
    // differences (Chi first: a Personality whose Chi becomes 0 is destroyed by ApplyModifier, exactly as any Chi penalty would).
    private void SwitchForceAndChi(CardInstance card, CardInstance source, string effect)
    {
        var force = CardForce(card);
        var chi = PersonalityChi(card);
        Emit("statsSwitched", $"{Definition(card).Name}'s Force and Chi are switched ({force}F/{chi}C becomes {chi}F/{force}C).", card.OwnerId, card.Id);
        if (chi != force)
        {
            ApplyModifier(card, source.Id, effect, "Chi", force - chi);
            if (InPlay(card)) ApplyModifier(card, source.Id, effect, "Force", chi - force);
        }
    }

    // Matsu Koyama - exp: "Give each other Lion Clan Personality in your army a Force bonus equal to his own Personal Honor" -- each Personality's own current Personal Honor.
    private void KoyamaBoost(CardInstance source, int playerId)
    {
        foreach (var ally in Army(playerId, _state.CurrentBattleProvince!.Value).Where(c => c.Id != source.Id && HasClanAffiliation(c, "Lion")).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            var bonus = CardPersonalHonor(ally);
            if (bonus > 0) ApplyModifier(ally, source.Id, "koyama-exp-boost", "Force", bonus);
        }
    }

    // "Destroy it": an attachment alone, a Personality with its unit -- the ordinary destruction paths, as the acting player's action.
    private void DestroyUnitCard(CardInstance card, int actingPlayerId)
    {
        if (card.Zone == Zone.Attached) DestroyAttachment(card);
        else DestroyUnits([card], actingPlayerId: actingPlayerId);
    }

    // Hida Kaiji's trait, asked where a movement is applied (MoveHome, TryMoveToBattlefield): another player's action is resolving (the running command's player is not
    // his controller), his trait has not fired this turn, and the movement is not one that "will not be negated" (MovementCannotBeNegated). The stamp makes it once a turn.
    private bool EnemyActionMovementAutoNegated(CardInstance card)
    {
        // Twin Forks City (The Shadow's Embrace pass, issue #476 batch 6): a one-shot shield against another player's card's effect moving him.
        if (BlessingsShieldNegatesMove(card)) return true; // Blessings of the Shi-Tien Yen-Wang (issue #503).
        if (_runningCommand is { } mover && !MovementCannotBeNegated(mover) && TwinForksNegates(card, mover.PlayerId, "movement")) return true;
        if (Definition(card).Effect != NegateFirstEnemyMoveEachTurnEffect || !InPlay(card) || HiromiUnnegatable()) return false; // Yoritomo Hiromi (issue #477).
        if (_runningCommand is not { } acting || acting.PlayerId == card.OwnerId || MovementCannotBeNegated(acting)) return false;
        if (card.AbilityUses.TryGetValue(EnemyMoveNegatedTurnKey, out var turn) && turn == _state.TurnNumber) return false;
        card.AbilityUses[EnemyMoveNegatedTurnKey] = _state.TurnNumber;
        Emit("moveNegated", $"{Definition(card).Name}'s movement from another player's action was negated (the first time this turn).", card.OwnerId, card.Id);
        return true;
    }

    // Battle resolution, the attacker-won branch: every Personality of the winning attacking army remembers the turn (Yoritomo Iwashi).
    private void MarkAttackVictory(CardInstance[] attackingArmy)
    {
        foreach (var card in attackingArmy) card.AbilityUses[AttackVictoryTurnKey] = _state.TurnNumber;
    }

    private bool WonBattleAttackingSinceLastTurnBegan(CardInstance card) =>
        card.AbilityUses.TryGetValue(AttackVictoryTurnKey, out var turn) && turn >= _state.TurnNumber - 2;

    // "[Card] will not be controlled by a [Clan] player": a Loyal-like refusal keyed on the controlling player's Clan, read wherever Loyal is (recruiting, overlaying).
    private static bool ControlRefusedByClan(CardInstance card, string clan) => Definition(card).Effect == TanemonoNoPhoenixControlEffect && clan == "Phoenix";
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSeedsOfDecayBatch3_Rung4b3()
    {
        EngineHooks.ValidateLate.Register("ValidateSeedsOfDecayBatch3", 100, static (engine, context) => engine.ValidateSeedsOfDecayBatch3(context.id, context.command, context.ability, context.player, context.source));
    }
}
