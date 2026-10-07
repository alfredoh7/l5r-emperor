namespace L5R.Core;

// ======================================================================================================================================================================================
// WAVE 9601-9604 (issue #689): the cards the merged primitive P7 (PeriodWatches.cs, issue #683) unlocks. personality-wave-9601 (Bayushi Kazutoshi - exp2, Daigotsu Gyoken - exp3),
// follower-wave-9602 (Legion of the Fallen), spell-wave-9603 (Earth's Protection), strategy-wave-9604 (Avenging the Fallen, Lessons from the Heralds). Readings are in
// docs/decisions/2026-10-06-p7-card-wave.md; the short form:
//
//   * THE THREE PASSIVE NEGATIONS are standing watches (EngineHooks.StandingWatches): Kazutoshi (the first time each turn another player's card's effect destroys one of your Loyal Personalities,
//     negate it, then move him home, bow him, dishonor him), Gyoken and Legion (their own first destruction by another player's card each GAME). Kazutoshi's Sensei discard and Gyoken's / Legion's Honor loss are the existing entry traits (Effect "discard-other-sensei", CardDefinition.EntryHonorLoss).
//   * EARTH'S PROTECTION: the province bonus or penalty is a COST (it precedes the colon), so it is its own cost verb (Wave9601Abilities.ProvinceBargain), paid with the Bow of the Spell and kept even
//     when the effects are negated; the amount rides on the command's Choice ("1" up to the province's current strength) and the client lists one offered action per amount. The effect is the equal
//     Force bonus and, if the Shugenja is Earth, ONE shared negation for his next bowing or movement from another player's card's effect this turn (two linked watches; the first to fire removes the other).
//   * AVENGING THE FALLEN needs "another player's Battle action has destroyed one of your Personalities this battle": a P7 period counter fed from resolved action facts (Wave9601AfterAction).
//   * LESSONS FROM THE HERALDS needs "one of your Personalities has won a duel this turn": a P7 period counter fed from the duel resolution (Wave9601NoteDuelWon).
// ======================================================================================================================================================================================

public static class Wave9601Abilities
{
    public const string EarthsProtection = "w9601-earths-protection", Avenging = "w9601-avenging-the-fallen", Lessons = "w9601-lessons-from-the-heralds";
    /// <summary>The cost verb of Earth's Protection: "give the current battlefield's province a bonus, or a penalty if you are defending, from 1 up to its current strength".</summary>
    public const string ProvinceBargain = "w9601-province-bargain";

    public const string Kazutoshi = "oracle-l5r-10309", Gyoken = "oracle-l5r-10487", Ikeuchi = "oracle-l5r-10273", Legion = "oracle-l5r-10898";

    public static Dictionary<string, AbilityDefinition> AddWave9601(this Dictionary<string, AbilityDefinition> all)
    {
        // Earth's Protection (oracle-l5r-10349): "Battle: Bow this card and give the current battlefield's province a bonus, or a penalty if you are defending, from 1 up to its current strength: Give this
        // Shugenja an equal Force bonus. Negate his next bowing or movement from other players' cards' effects if he is Earth." The Shugenja is the Spell's wielder.
        all[EarthsProtection] = new("Battle", "Bow this card and give the current battlefield's province a bonus, or a penalty if you are defending, from 1 up to its current strength: Give this Shugenja an equal Force bonus. Negate his next bowing or movement from other players' cards' effects if he is Earth",
            "none", Performer: "own-wielder", Costs: [.. ActionCost.BowSource, new ActionCost(ProvinceBargain)]);
        // Avenging the Fallen (oracle-l5r-10362): "Battle: Choose your performing Personality: Straighten him. If another player's Battle action has destroyed one of your Personalities this battle,
        // negate the performer's next destruction from cards' effects (this turn)."
        all[Avenging] = new("Battle", "Choose your performing Personality: Straighten him. If another player's Battle action has destroyed one of your Personalities this battle, negate the performer's next destruction from cards' effects (this turn)",
            "none", Performer: "own-personality-at-battle");
        // Lessons from the Heralds (oracle-l5r-10736): "Battle: If one of your Personalities has won a duel this turn, look at the top three cards of your Fate deck. Put one into your hand. Put the rest back in any order."
        all[Lessons] = new("Battle", "If one of your Personalities has won a duel this turn, look at the top three cards of your Fate deck. Put one into your hand. Put the rest back in any order", "none", Look: Wave9601Looks.Lessons);
        return all;
    }
}

public static class Wave9601Looks
{
    /// <summary>Lessons from the Heralds: Banish All Doubt's shape with the rest left on top, in any order.</summary>
    public static LookSpec Lessons { get; } = new("w9601-lessons-from-the-heralds", "Look at the top three cards of your Fate deck", [new("Fate")], 3, [new(LookDisposition.Hand, 1, 1)], Reorder: true);
    public static IEnumerable<LookSpec> All { get; } = [Lessons];
}

public sealed partial class MatchEngine
{
    private const string WonDuelCounter = "w9601-won-duel:", BattleDestroyedCounter = "w9601-battle-destroyed:";

    // ---- the standing watches ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static StandingWatch[] Wave9601Watches() =>
    [
        // Kazutoshi - exp2: the first time each turn another player's card's effect would destroy one of his controller's Loyal Personalities (himself included, he is Loyal).
        new("w9601-kazutoshi-loyal", d => d.Id == Wave9601Abilities.Kazutoshi, WatchKind.Destroy, WatchScope.OwnersCards, TargetKeyword: "Loyal", TargetType: CardType.Personality,
            Source: WatchSource.OtherPlayers, Period: Period.Turn, OnNegated: static (engine, context, carrier) => engine.KazutoshiFollowUp(context.Target!, carrier)),
        // Daigotsu Gyoken - exp3: "Negate Gyoken's first destruction each game from another player's card."
        new("w9601-gyoken-first", d => d.Id == Wave9601Abilities.Gyoken, WatchKind.Destroy, WatchScope.Self, Source: WatchSource.OtherPlayers, Period: Period.Game),
        // Legion of the Fallen: "Before the first time this game another player's card destroys this Follower, negate the destruction."
        new("w9601-legion-first", d => d.Id == Wave9601Abilities.Legion, WatchKind.Destroy, WatchScope.Self, Source: WatchSource.OtherPlayers, Period: Period.Game),
    ];

    /// <summary>Kazutoshi's follow-up to a negated destruction: "Move him home. Bow him. Dishonor him." The three are his own trait's effects, so the controller's, not the other player's.</summary>
    private void KazutoshiFollowUp(CardInstance saved, CardInstance carrier)
    {
        var original = _runningCommand;
        _runningCommand = new("useAbility", carrier.OwnerId, _state.Version, CardId: carrier.Id);
        try
        {
            MoveHome(saved, false);
            if (!StalwartNegatesBow(saved, carrier.OwnerId)) saved.Bowed = true;
            Dishonor(saved, carrier.OwnerId);
        }
        finally { _runningCommand = original; }
    }

    // Resolved facts retain the originating action through Attack/Duel reaction pauses.
    private void Wave9601AfterAction(ResolvedActionFacts facts)
    {
        if (_state.CurrentBattleProvince is null || !IsBattleAction(facts.AbilityId) || !_state.Cards.Values.Any(c => c.DefinitionId == "oracle-l5r-10362")) return;
        foreach (var affected in facts.Affected.Where(a => a.Kind == DestroyedKind))
        {
            var card = Card(affected.CardId);
            if (Definition(card).Type == CardType.Personality && card.OwnerId != facts.PlayerId) AddPeriodCount(BattleDestroyedCounter + card.OwnerId, Period.Battle);
        }
    }

    /// <summary>The duel resolution's hook: the winner's controller has won a duel this turn.</summary>
    private void Wave9601NoteDuelWon(string? winnerId)
    {
        if (_state.Cards.Values.Any(c => c.DefinitionId == "oracle-l5r-10736") && winnerId is not null && _state.Cards.TryGetValue(winnerId, out var winner) && Definition(winner).Type == CardType.Personality) AddPeriodCount(WonDuelCounter + winner.OwnerId, Period.Turn);
    }

    // ---- Earth's Protection ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The current strength of the current battlefield's province (the Defender's row), or 0 when the battlefield has none.</summary>
    private int EarthsProtectionMaximum()
    {
        if (_state.CurrentBattleProvince is not int index) return 0;
        var province = Player(1 - _state.ActivePlayerId).Provinces.FirstOrDefault(p => p.Index == index && !p.Destroyed);
        return province is null ? 0 : EffectiveProvinceStrength(province);
    }

    /// <summary>The offered amounts: one action per number from 1 up to the province's current strength. Null when there is none to offer (no battle, or a battlefield without a province).</summary>
    private (string Key, string Label)[]? Wave9601ChoiceOptions(string abilityId, int playerId)
    {
        if (abilityId != Wave9601Abilities.EarthsProtection) return null;
        var maximum = EarthsProtectionMaximum();
        if (maximum < 1) return null;
        var defending = playerId != _state.ActivePlayerId;
        return Enumerable.Range(1, maximum).Select(n => (n.ToString(System.Globalization.CultureInfo.InvariantCulture), $"a bonus of {n}"))
            .Concat(defending ? Enumerable.Range(1, maximum).Select(n => ((-n).ToString(System.Globalization.CultureInfo.InvariantCulture), $"a penalty of {n}")) : []).ToArray();
    }

    private static int EarthsProtectionAmount(GameCommand command) => int.TryParse(command.Choice, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;

    private void ValidateWave9601(string id, GameCommand command, PlayerState player)
    {
        if (id == Wave9601Abilities.EarthsProtection)
        {
            Require(_state.CurrentBattleProvince is not null, "There is no current battlefield.");
            var maximum = EarthsProtectionMaximum();
            Require(maximum >= 1, "The current battlefield's province has no strength to give or take.");
            var amount = EarthsProtectionAmount(command);
            Require(amount != int.MinValue && Math.Abs(amount) >= 1 && Math.Abs(amount) <= maximum && (amount > 0 || player.Id != _state.ActivePlayerId), $"Choose a bonus or penalty from 1 up to the province's current strength ({maximum}).");
        }
        if (id == Wave9601Abilities.Lessons)
            Require(PeriodCountOf(WonDuelCounter + player.Id, Period.Turn) > 0, "None of your Personalities has won a duel this turn.");
    }

    /// <summary>PayAbilityCosts' hook for the ProvinceBargain verb: the Defender's province at the current battlefield has the amount added (you are attacking) or taken away (you are defending) for the turn.</summary>
    private void Wave9601PayProvinceBargain(GameCommand command, PlayerState player, CardInstance? source)
    {
        var amount = EarthsProtectionAmount(command);
        if (amount == 0 || _state.CurrentBattleProvince is not int index) return;
        var defenderId = 1 - _state.ActivePlayerId;
        GiveProvinceStrengthThisTurn(defenderId, index, amount, source is null ? "Earth's Protection" : Definition(source).Name);
    }

    private bool? RunWave9601(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case Wave9601Abilities.EarthsProtection:
            {
                var wielder = Card(command.PerformerId!);
                var amount = EarthsProtectionAmount(command);
                ApplyModifier(wielder, source!.Id, id, "Force", Math.Abs(amount));
                if (Keyword(wielder, "Earth"))
                {
                    // "his next bowing or movement": ONE negation in all -- two linked watches, the first to fire removes the other (an "exclusive:" Arg, PeriodWatches.cs).
                    var group = $"exclusive:{id}:{wielder.Id}:{_state.TurnNumber}";
                    foreach (var kind in new[] { WatchKind.Bow, WatchKind.Move })
                        AddNegationWatch(new NegationWatch { OwnerPlayerId = player.Id, Kind = kind, TargetCardId = wielder.Id, Source = WatchSource.OtherPlayers, Remaining = 1, LastTurn = _state.TurnNumber, Arg = group });
                }
                return false;
            }
            case Wave9601Abilities.Avenging:
            {
                var performer = Card(command.PerformerId!);
                Straighten(performer);
                if (PeriodCountOf(BattleDestroyedCounter + player.Id, Period.Battle) > 0)
                {
                    AddNegationWatch(new NegationWatch { OwnerPlayerId = player.Id, Kind = WatchKind.Destroy, TargetCardId = performer.Id, Source = WatchSource.Any, Remaining = 1, LastTurn = _state.TurnNumber });
                    Emit("destructionWatch", $"{Definition(performer).Name}'s next destruction from cards' effects this turn will be negated.", player.Id, performer.Id);
                }
                return false;
            }
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave9601Hooks()
    {
        EngineHooks.AfterActionTraits.Register("Wave9601AfterAction", 12600, (engine, facts) => engine.Wave9601AfterAction(facts));
        EngineHooks.Abilities.Register("AddWave9601", 12600, static abilities => abilities.AddWave9601());
        EngineHooks.Looks.Register("Wave9601Looks", 12600, static () => Wave9601Looks.All);
        EngineHooks.ValidateEarly.Register("ValidateWave9601", 12600, static (engine, context) => engine.ValidateWave9601(context.id, context.command, context.player));
        EngineHooks.RunEffects.Register("RunWave9601", 12600, static (engine, context) =>
        {
            if (context.id == Wave9601Abilities.Lessons)
            {
                engine.BeginLook(context.ability.Look!, context.source!, context.player.Id, "ability", context.bowedBeforeAction);
                return true;
            }
            return engine.RunWave9601(context.id, context.command, context.player, context.source);
        });
        var rules = Wave9601Watches();
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            EngineHooks.StandingWatches.Register(rule.Id, 12600 + i, () => rule);
        }
    }
}
