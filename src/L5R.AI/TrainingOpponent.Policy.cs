using L5R.Core;

namespace L5R.AI;

// Issue #642: the scoring half of the bot. Hand-tuned scores for abilities the bot already understood stay in TrainingOpponent.cs; everything else is read from
// AbilityIntent (the label and target category of the ability), never from a card's name, so an ability wired tomorrow is scored the moment it exists.
public static partial class TrainingOpponent
{
    /// <summary>How much more an ability is worth for what it does, in Mendon's order: destroy opponent cards, then bow them, then penalize them, then help itself.</summary>
    internal static double RankBonus(EffectRank rank) => rank switch
    {
        EffectRank.Destroy => 50,
        EffectRank.Bow => 40,
        EffectRank.Penalize => 25,
        EffectRank.Help => 10,
        _ => 0
    };

    private static double AbilityScore(Ctx x, ActionOption action, GameCommand command)
    {
        var id = command.AbilityId;
        if (id is "border-first" or "border-game") return id == "border-first" ? 140 : 35;
        if (id == "shunya-honor") return 100;
        // Deck search (issue #312): a modest bot uses Ikoma Shizuka's search when nothing more useful is on offer (just above passing), which is what lets a whole
        // AI-vs-AI match exercise the full search flow.
        if (id == "shizuka-search") return 12;
        var intent = AbilityIntent.Of(id);
        if (intent.TakesFavor) return FavorTakeScore(x, intent);
        if (intent.DiscardsFavor) return FavorSpendScore(x);
        // Mendon's spec: always play a Reaction that negates an effect; a Reaction that does not is a weighted choice (higher-ranked effects are likelier), rolled
        // deterministically from the match state so a replay makes the same choice. Declining a Reaction scores 100.
        if (intent.IsReaction)
        {
            if (intent.Negates) return 1500;
            var roll = BotContext.Roll(x.View.Version, id, command.CardId);
            var weight = 0.3 + 0.15 * (int)intent.Rank;
            return roll < weight ? 150 + RankBonus(intent.Rank) : 50;
        }
        // Hand-tuned values for the abilities the bot understood before issue #642 (they score the chosen target).
        if (x.Card(command.TargetId) is { } target && AbilityValue(id, target, x.Self, x.Opponent, x.View) is var known && known > 0) return known;
        return Math.Max(PassScore, GenericAbilityScore(x, intent));
    }

    /// <summary>The default for any ability the bot has no rule for: well above passing, higher for what it does, with Mendon's draw and Honor adjustments.</summary>
    private static double GenericAbilityScore(Ctx x, AbilityIntent intent)
    {
        var score = 30 + RankBonus(intent.Rank);
        // Cards that draw gain priority as the hand falls short of its maximum: the bigger the gap, the more it is worth.
        if (intent.Draws) score += 12 * Math.Max(0, x.MaxHand - x.Self.HandCount);
        // A deck that wants Honor for itself prefers abilities that give it Honor; one that wants to hurt the opponent prefers abilities that take it away.
        var gain = x.Profile.PrefersOwnGain(x.Self.Honor, x.Opponent.Honor);
        if (intent.HonorGain && gain) score += 25;
        if (intent.HonorLoss && !gain) score += 25;
        return score;
    }

    private static int Personalities(PlayerView p) => p.Home.Concat(p.Army).Count(c => c.Definition?.Type == CardType.Personality);

    /// <summary>
    /// Lobbying and other ways to take the Imperial Favor. With the higher Family Honor (Stronghold starting Honor) AND more Personalities the bot ALWAYS takes it;
    /// with an equal or lower number of Personalities it is a low priority unless taking it costs no bow (an Open action that simply takes it); in between it is
    /// a moderate priority.
    /// </summary>
    private static double FavorTakeScore(Ctx x, AbilityIntent intent)
    {
        if (x.View.FavorPlayerId == x.Self.Id) return PassScore;
        var ahead = (x.Self.Stronghold.Definition?.StartingHonor ?? 0) > (x.Opponent.Stronghold.Definition?.StartingHonor ?? 0) && Personalities(x.Self) > Personalities(x.Opponent);
        if (ahead) return 900;
        if (Personalities(x.Self) <= Personalities(x.Opponent)) return intent.TakesFavorBowing ? PassScore + 2 : 150;
        return 60;
    }

    /// <summary>
    /// An ability that discards the Imperial Favor as a cost. The bot spends the Favor as many times as it can while it still ends the turn holding it: unlimited
    /// when it can take the Favor back with an Open action (it need not worry about keeping it), otherwise once, and only when it will certainly Lobby again
    /// this turn (the always-lobby case). When it could not get it back the ability is worth no more than passing.
    /// </summary>
    private static double FavorSpendScore(Ctx x)
    {
        if (x.View.FavorPlayerId != x.Self.Id) return PassScore;
        if (OwnFreeFavorTakers(x).Any()) return 140;
        var ahead = (x.Self.Stronghold.Definition?.StartingHonor ?? 0) > (x.Opponent.Stronghold.Definition?.StartingHonor ?? 0) && Personalities(x.Self) > Personalities(x.Opponent);
        return ahead && !x.Memory.LobbiedThisTurn(x.View.TurnNumber) ? 140 : PassScore;
    }

    /// <summary>The bot's own cards in play that can take the Favor with a non-Reaction action that does not bow anything.</summary>
    private static IEnumerable<CardView> OwnFreeFavorTakers(Ctx x) =>
        x.Self.Home.Concat(x.Self.Army).Append(x.Self.Stronghold).Where(c => c.Definition?.Abilities?.Any(a =>
        {
            var i = AbilityIntent.Of(a);
            return i.TakesFavor && !i.TakesFavorBowing && !i.IsReaction;
        }) == true);

    /// <summary>Proclaim is taken whenever the profile does not deprioritize it; a deprioritized Proclaim (a low-Honor deck short of spare Gold, or an in-between deck
    /// that trails by more than 5) ranks just under declining the window.</summary>
    private static double ProclaimActionScore(Ctx x)
    {
        var ready = Production(x.Self.Stronghold, x.Self.Clan) + x.Self.Home.Sum(c => Production(c, x.Self.Clan));
        return x.Profile.StanceFor(x.Self.Honor, x.Opponent.Honor, ready >= 3) == ProclaimStance.Deprioritize ? 40 : 1000;
    }

    /// <summary>"Choose a player who gains or loses N Honor": each answer is scored from the Honor profile.</summary>
    private static double EffectChoiceScore(Ctx x, GameCommand command)
    {
        var gain = x.Profile.PrefersOwnGain(x.Self.Honor, x.Opponent.Honor);
        return command.Choice switch
        {
            "self-gain" => gain ? 130 : 70,
            "opponent-lose" => gain ? 70 : 130,
            "opponent-gain" or "self-lose" => 20,
            _ => 100
        };
    }

    private static double DeclareScore(Ctx x)
    {
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var provinces = x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        var plan = PlanAttack(x, units, provinces);
        if (plan is null) return units.Length == 0 ? -100 : 0;
        var force = ArmyForce(x.Self.Home.Where(c => plan.ContainsKey(c.Id) || c.AttachedTo is not null && plan.ContainsKey(c.AttachedTo)));
        return 90 + force - ArmyForce(x.Opponent.Home);
    }

    /// <summary>Remembers the one thing the bot must carry to the next decision: that it has lobbied this turn (Lobby is once a turn).</summary>
    private static void Remember(Ctx x, GameCommand command)
    {
        if (command.Type != "useAbility") return;
        var intent = AbilityIntent.Of(command.AbilityId);
        if (!intent.TakesFavor) return;
        if (command.AbilityId == "lobby") x.Memory.NoteLobby(x.View.TurnNumber);
        x.Memory.NoteFavorTaken(x.View.TurnNumber);
    }
}
