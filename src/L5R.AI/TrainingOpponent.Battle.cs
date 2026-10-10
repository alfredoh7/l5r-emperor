using L5R.Core;

namespace L5R.AI;

// Issue #788 (Mendon's bot changes): how the practice bot behaves inside a battle. The pure rules are in BattlePolicy; this file applies them to the bot's candidate actions.
// Everything is read from the bot's own permitted view and the printed catalogue, so it can never act on hidden information.
public static partial class TrainingOpponent
{
    /// <summary>Score for an action the new battle rules rule out: below passing, so the bot passes instead.</summary>
    private const double BarredScore = PassScore - 1;

    /// <summary>The battle rules applied on top of an action's ordinary score. Only the actions a player takes in a battle (an ability, a Strategy, an attachment) are affected.</summary>
    private static double Adjust(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (!(x.Memory.UseBattlePolicy || x.Memory.UseEquipPolicy) || action.Type is not ("useAbility" or "playStrategy" or "equip")) return score;
        var intent = action.Type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        if (intent.Negates) return score; // a negating Reaction is always played (Mendon's earlier spec)
        // Equip (Battle) item 1: the additional action an attached spell grants is always used.
        if (x.Memory.UseEquipPolicy && action.Type == "useAbility" && UsesGrantedAttachmentAction(x, command)) return Math.Max(score, GrantedActionScore);
        var odds = x.Odds;
        var source = x.Card(command.CardId);
        var inPlay = source is not null && source.OwnerId == x.Self.Id && !x.Self.Hand.Any(h => h.Id == source.Id);

        // Rule 5: the first action of a combat taken through a Reaction (Naval, Sneak Attack, ...) is only worth claiming when the bot will have something to do with it,
        // that is when the battle is not already won. Then it prefers to destroy, bow, move, penalize, and last help, and above all a buff that lifts a unit past an Attack.
        if (x.Memory.UseBattlePolicy && x.View.PendingChoice?.Type == "engageReaction" && (intent.IsReaction || action.Type == "playStrategy"))
        {
            if (odds is { Satisfied: true }) return Math.Min(score, BarredScore);
            return 150 + EngageBonus(x, command, intent);
        }
        if (odds is null) return score;
        if (IsEquip(action, command)) return x.Memory.UseEquipPolicy ? EquipScore(x, action, command, score, odds) : score;
        if (!x.Memory.UseBattlePolicy) return score;

        var target = x.Card(command.TargetId);
        // Rule 4: a defender that is losing and cannot win may send its own units home, but only without handing the attacker the Province.
        if (!odds.IsAttacker && !odds.Winning && !odds.Satisfied && BattlePolicy.SendsHome(command.AbilityId) && OwnSentHome(x, command, intent, source, target) is { } sent)
            return SendHomeScore(x, odds, sent);

        if (intent.IsReaction) return score;

        if (odds.Satisfied)
        {
            // Rule 1: nothing more is needed, so the bot passes unless the action gains Honor, takes the opponent's, moves a unit that is not needed into an opposed battlefield,
            // or removes an enemy that could undo the result. In-play abilities first, then cards from the hand.
            if (CostCosts(x, command, source, odds)) return Math.Min(score, BarredScore);
            if (ThreatRemoval(x, command, intent, target)) return Math.Max(score, inPlay ? 180 : 140);
            if (intent.HonorGain && intent.Side != TargetSide.Enemy || intent.HonorLoss) return Math.Max(score, inPlay ? 130 : 110);
            if ((action.Movement is not null || BattlePolicy.Moves(command.AbilityId)) && SafeMove(x, action, command, intent, target, odds)) return Math.Max(score, 120);
            return Math.Min(score, BarredScore);
        }

        // Not yet winning: when the enemy cannot do much about it (unopposed, bowed or ability-less defenders, or a nearly empty hand) the bot reaches for what is already in play before
        // spending cards from its hand.
        if (!BattlePolicy.Unthreatened(EnemyArmyAt(x), x.Opponent.HandCount)) return score;
        return inPlay ? score + 25 : Math.Max(PassScore, score - 25);
    }

    /// <summary>Above every ordinary score: Mendon's "always use the additional action" for an attachment the bot has just equipped.</summary>
    private const double GrantedActionScore = 300;

    /// <summary>
    /// Mendon's item 1: electing to attach a spell can grant an additional action to use its Battle ability; the bot always takes it. The engine offers declineAdditionalAction exactly
    /// while an optional grant is held, so an ability of a spell the bot has attached, offered beside that decline, is the granted action. Its target comes from the same battle-action priorities as any other.
    /// </summary>
    private static bool UsesGrantedAttachmentAction(Ctx x, GameCommand command) =>
        x.Actions.Any(a => a.Enabled && a.Type == "declineAdditionalAction")
        && x.Card(command.CardId) is { AttachedTo: not null, Definition.Type: CardType.Spell } spell && spell.OwnerId == x.Self.Id;

    private static bool IsEquip(ActionOption action, GameCommand command) => action.Type == "equip" || command.AbilityId == "equip-battle";

    /// <summary>The card an equip action attaches: Equip names it as the card, Equip (Battle) as its target.</summary>
    private static CardView? Attachment(Ctx x, ActionOption action, GameCommand command) => x.Card(action.Type == "equip" ? command.CardId : command.TargetId);

    /// <summary>
    /// Mendon's item 1: an attachment is valued like a Strategy, by the best thing its Battle abilities do (destroy, then bow, then penalize, then help), with the same base the generic
    /// ability score uses. Null when it prints no Battle ability worth using, in which case its ordinary score stands.
    /// </summary>
    private static double? AttachmentValue(CardView? attachment)
    {
        if (attachment is null) return null;
        var best = BattlePolicy.Printed(attachment).Where(p => p.Ability.Timing is "Battle" or "BattleOrOpen" && !p.Intent.IsReaction && !(p.Intent.Rank >= EffectRank.Penalize && p.Intent.Side == TargetSide.Own))
            .Select(p => p.Intent.Rank).DefaultIfEmpty(EffectRank.Neutral).Max();
        return best == EffectRank.Neutral && !BattlePolicy.Printed(attachment).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen") ? null : 30 + RankBonus(best);
    }

    /// <summary>The Force an attachment adds to its wielder's army this battle: its own printed Force plus the largest "+N F" its Battle abilities give.</summary>
    private static int EquipForce(CardView? attachment) =>
        attachment?.Definition is not { } d ? 0 : Math.Max(0, d.Force) + BattlePolicy.Printed(attachment).Where(p => p.Ability.Timing is "Battle" or "BattleOrOpen" && p.Intent.Side != TargetSide.Enemy)
            .Select(p => ForceBonus(p.Ability)).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Mendon's item 2. When the battle is already won (the defender holds, or the attacker razes the Province) and the bot would otherwise do nothing, it equips while its hand is at or above
    /// the hand limit, until it is below it. Otherwise, when equipping at most three cards would lift the outcome up the ladder (see <see cref="BattlePolicy.Odds.Tier"/>), the cards that do it are equipped.
    /// An equip that does neither keeps the score the attachment's own abilities earn it.
    /// </summary>
    private static double EquipScore(Ctx x, ActionOption action, GameCommand command, double score, BattlePolicy.Odds odds)
    {
        var attachment = Attachment(x, action, command);
        var value = AttachmentValue(attachment) is { } v ? Math.Max(v, action.Type == "equip" ? PassScore : 0) : score;
        if (odds.Satisfied)
            return x.Self.HandCount >= x.MaxHand ? 100 + Math.Min(value, 99) / 100.0 : x.Memory.UseBattlePolicy ? Math.Min(score, BarredScore) : value;
        if (attachment is not null && ChangesOutcome(x, odds).Contains(attachment.Id)) return Math.Max(value, 140);
        return value;
    }

    /// <summary>The attachments (at most three, within the Gold available) that together lift the battle's outcome; empty when equipping cannot change it.</summary>
    private static string[] ChangesOutcome(Ctx x, BattlePolicy.Odds odds)
    {
        var gold = Production(x.Self.Stronghold, x.Self.Clan) + x.Self.Home.Sum(c => Production(c, x.Self.Clan));
        var options = new List<(string Id, int Gain, int Cost)>();
        foreach (var offered in x.Actions.Where(a => a.Enabled && (a.Type == "equip" || a.Command?.AbilityId == "equip-battle")))
        {
            IEnumerable<string?> ids = offered.Type == "equip" ? new string?[] { offered.CardId } : offered.TargetIds ?? new string?[] { offered.Command?.TargetId };
            foreach (var id in ids.OfType<string>().Distinct())
                if (x.Card(id) is { Definition: { } d } card) options.Add((id, EquipForce(card), offered.Gold?.Cost ?? d.GoldCost));
        }
        var plan = BattlePolicy.EquipPlan(options, gold);
        var gain = options.Where(o => plan.Contains(o.Id)).Sum(o => o.Gain);
        return odds.Tier(gain) > odds.Tier() ? plan : [];
    }

    private static AbilityIntent StrategyIntent(CardView? card) => card?.Definition?.Effect switch
    {
        "bow-small" => AbilityIntent.Of(new AbilityDefinition("Battle", "Bow a target enemy Personality", "enemy-personality")),
        "force-three" => AbilityIntent.Of(new AbilityDefinition("Battle", "Give a target Personality +3F", "own-personality")),
        _ => AbilityIntent.None
    };

    private static IEnumerable<CardView> EnemyArmyAt(Ctx x)
    {
        var province = x.View.BattleStep?.BattlefieldProvince ?? x.View.CurrentBattleProvince;
        return province is int p ? x.Opponent.Army.Where(c => c.ProvinceIndex == p) : x.Opponent.Army;
    }

    /// <summary>The ordering of Mendon's rule 5: destroy, bow, move, penalize, everything else, with a buff for the bot's own army on top when it lifts a unit past an enemy Attack.</summary>
    /// <summary>Issue #788 (Mendon): the attack that cannot raze a Province but destroys the defending army, or trades well in a tie. Null when the rule is off or no such attack exists.</summary>
    private static BattlePolicy.Trade? TradeFor(Ctx x, CardView[] units) =>
        x.Memory.UseBattlePolicy && x.Memory.UseTradeAttacks && !HonorRace(x) ? BattlePolicy.PlanTrade(units, x.Self.Home, x.Opponent.Home) : null;

    /// <summary>The trade the already-chosen assignment makes, so a plan the engine or the planner supplied is scored by the same rule.</summary>
    private static BattlePolicy.Trade? TradeOf(Ctx x, IEnumerable<string> sentIds)
    {
        if (!x.Memory.UseBattlePolicy || !x.Memory.UseTradeAttacks || HonorRace(x)) return null;
        var sent = x.Self.Home.Where(c => sentIds.Contains(c.Id)).ToArray();
        var defenders = x.Opponent.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        return BattlePolicy.Evaluate(sent, x.Self.Home, BattlePolicy.Force(x.Opponent.Home), defenders.Sum(d => BattlePolicy.UnitValue(d, x.Opponent.Home)));
    }

    /// <summary>Between a pass (10) and a razing attack (90 and up): a worthwhile trade, better the more Gold it nets.</summary>
    private static double TradeScore(BattlePolicy.Trade trade) => 70 + Math.Min(trade.Net, 15);

    private static double EngageBonus(Ctx x, GameCommand command, AbilityIntent intent)
    {
        var moves = BattlePolicy.Moves(command.AbilityId) || BattlePolicy.SendsHome(command.AbilityId);
        var bonus = intent.Rank switch
        {
            EffectRank.Destroy => 50,
            EffectRank.Bow => 40,
            _ when moves && intent.Side != TargetSide.Own => 32,
            EffectRank.Penalize => 25,
            EffectRank.Help => 10,
            _ => 0
        };
        if (intent.Rank == EffectRank.Help && intent.Side != TargetSide.Enemy && x.Card(command.TargetId) is { } buffed && AbilityCatalog.All.TryGetValue(command.AbilityId ?? "", out var ability))
        {
            var gain = BattlePolicy.AttackAugment(ability) is > 0 and var g ? g : ForceBonus(ability);
            var threat = EnemyMaxAttack(x);
            bonus += 15 + (gain > 0 && buffed.Force <= threat && buffed.Force + gain > threat ? 30 : 0);
        }
        return bonus;
    }

    private static int ForceBonus(AbilityDefinition ability)
    {
        var m = System.Text.RegularExpressions.Regex.Match(AbilityIntent.EffectText(ability.Label ?? ""), @"\+\s?(\d+)\s?F");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    /// <summary>The largest Force an enemy Ranged or Melee Attack in play could destroy.</summary>
    private static int EnemyMaxAttack(Ctx x) =>
        x.Opponent.Army.Concat(x.Opponent.Home).SelectMany(BattlePolicy.Printed).Select(p => BattlePolicy.AttackStrength(p.Id) ?? 0).DefaultIfEmpty(0).Max();

    /// <summary>The ability's cost bows a unit that is carrying the win: it would give the result back.</summary>
    private static bool CostCosts(Ctx x, GameCommand command, CardView? source, BattlePolicy.Odds odds)
    {
        if (source is null || source.OwnerId != x.Self.Id || source.Bowed || source.ProvinceIndex is null) return false;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return false;
        var label = ability.Label ?? "";
        var colon = label.LastIndexOf(':');
        if (colon <= 0 || !label[..colon].Contains("bow", StringComparison.OrdinalIgnoreCase)) return false;
        return BattlePolicy.Force(x.Self.Army.Where(c => c.Id == source.Id || c.AttachedTo == source.Id)) > odds.Margin;
    }

    /// <summary>Destroying, bowing, moving or sending home an enemy Personality that can destroy, bow or move cards in the bot's army.</summary>
    private static bool ThreatRemoval(Ctx x, GameCommand command, AbilityIntent intent, CardView? target)
    {
        if (target is null || target.OwnerId == x.Self.Id || !BattlePolicy.IsThreat(target)) return false;
        return intent.Rank is EffectRank.Destroy or EffectRank.Bow || BattlePolicy.SendsHome(command.AbilityId) || BattlePolicy.Moves(command.AbilityId);
    }

    /// <summary>Moving a unit that is not needed (its Force fits inside the spare margin) into a battlefield where the enemy is present.</summary>
    private static bool SafeMove(Ctx x, ActionOption action, GameCommand command, AbilityIntent intent, CardView? target, BattlePolicy.Odds odds)
    {
        var moverIds = (action.Movement?.FixedMoverIds ?? []).ToList();
        if (target is not null && (action.Movement is null || action.Movement.TargetMoves)) moverIds.Add(target.Id);
        var movers = moverIds.Select(x.Card).OfType<CardView>().ToArray();
        if (movers.Length == 0 || movers.Any(m => m.OwnerId != x.Self.Id)) return false;
        var force = BattlePolicy.Force(x.Self.Army.Where(c => movers.Any(m => m.Id == c.Id || m.Id == c.AttachedTo)));
        if (force > odds.Margin) return false;
        return command.Province is int destination ? EnemyForceAt(x, destination) > 0 : action.Provinces?.Any(p => EnemyForceAt(x, p) > 0) == true;
    }

    private static int EnemyForceAt(Ctx x, int province) => BattlePolicy.Force(x.Opponent.Army.Where(c => c.ProvinceIndex == province));

    /// <summary>The units a defender's own send-home ability would send away, or null when it sends something else.</summary>
    private static CardView[]? OwnSentHome(Ctx x, GameCommand command, AbilityIntent intent, CardView? source, CardView? target)
    {
        var unit = target is not null ? target : intent.Side == TargetSide.None ? source : null;
        if (unit is null || unit.OwnerId != x.Self.Id || unit.ProvinceIndex is null) return null;
        return x.Self.Army.Where(c => c.Id == unit.Id || c.AttachedTo == unit.Id).ToArray();
    }

    private static double SendHomeScore(Ctx x, BattlePolicy.Odds odds, CardView[] sent)
    {
        var unit = sent.FirstOrDefault(c => c.AttachedTo is null) ?? sent[0];
        var force = BattlePolicy.Force(sent);
        // A unit that adds nothing to the fight (bowed, or no Force) is always worth saving from a lost battle, unless the bot can straighten it and put it back to work.
        if (force == 0)
        {
            var straightens = x.Actions.Any(a => a.Enabled && a.Command?.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var d)
                && AbilityIntent.EffectText(d.Label ?? "").Contains("straighten", StringComparison.OrdinalIgnoreCase) && (a.TargetIds?.Contains(unit.Id) == true || a.Command.TargetId == unit.Id));
            return straightens ? BarredScore : 130;
        }
        // A unit that is fighting leaves only when that does not hand the attacker the Province, and when the enemy's own units cannot be sent home instead.
        if (odds.WouldFallAfterLosing(force)) return BarredScore;
        var enemyCanBeSent = x.Actions.Any(a => a.Enabled && BattlePolicy.SendsHome(a.Command?.AbilityId) && a.Command?.TargetId is { } t && x.Card(t) is { } c && c.OwnerId != x.Self.Id);
        return enemyCanBeSent ? BarredScore : 90;
    }

    /// <summary>Rule 2: the Attack target that is the highest Force the Attack can destroy, counting every augment the bot still has.</summary>
    private static CardView? PickAttackTarget(Ctx x, string[] ids, CardView? source)
    {
        var strength = AttackStrengthFor(x, source);
        var all = x.Cards.Values.ToArray();
        return ids.Select(x.Card).OfType<CardView>().OrderByDescending(c => BattlePolicy.AttackTargetScore(c, strength, all)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>The printed strength of the source's Attack (a card with several uses the largest) plus every augment the bot could still apply, none that has been spent this turn.</summary>
    private static int AttackStrengthFor(Ctx x, CardView? source)
    {
        var printed = source is null ? [] : BattlePolicy.Printed(source).Select(p => BattlePolicy.AttackStrength(p.Id)).OfType<int>().ToArray();
        var strength = printed.Length > 0 ? printed.Max() : Math.Max(0, source?.UnitForce ?? source?.Force ?? 0);
        var augmenters = x.Self.Home.Concat(x.Self.Army).Append(x.Self.Stronghold).Concat(x.Self.Hand).Concat(x.Cards.Values.Where(c => c.OwnerId == x.Self.Id && c.AttachedTo is not null));
        foreach (var card in augmenters.DistinctBy(c => c.Id).Where(c => !c.Bowed && !c.ActionLocked))
            foreach (var (id, ability, _) in BattlePolicy.Printed(card))
                if (BattlePolicy.AttackAugment(ability) is > 0 and var bonus && x.Memory.UsesThisTurn(x.View.TurnNumber, id, card.Id) == 0) strength += bonus;
        return strength;
    }

    /// <summary>Scores the engine's own "choose the Attack's target" step by the same ranking, so the choice does not depend on the order the targets are listed.</summary>
    private static double AttackTargetChoiceScore(Ctx x, GameCommand command)
    {
        if (command.TargetId is null) return 50;
        if (!x.Memory.UseBattlePolicy || x.Card(command.TargetId) is not { } target) return 100;
        var strength = AttackStrengthFor(x, x.Card(command.CardId));
        return 100 + BattlePolicy.AttackTargetScore(target, strength, x.Cards.Values) / 100.0;
    }

    /// <summary>Rule 3: the bow target, best first among those the ability can legally take.</summary>
    private static CardView? PickBowTarget(Ctx x, string[] ids, string? abilityId)
    {
        var candidates = ids.Select(x.Card).OfType<CardView>().Where(c => c.OwnerId != x.Self.Id).ToArray();
        if (candidates.Length == 0) return null;
        var mayHaveAttachments = !(abilityId is not null && AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.RequireNoAttachments);
        return BattlePolicy.BowOrder(candidates, x.Cards.Values, mayHaveAttachments).FirstOrDefault();
    }

    /// <summary>Rule 6: the Border Keep's once-per-game refresh is worth using when the bot has 22 or more Gold to spend and the provinces it would cycle are mostly holdings or unknown cards.</summary>
    private static bool BorderRefreshWanted(Ctx x)
    {
        if (!x.Memory.UseBattlePolicy || x.View.Phase != Phase.Action || x.View.ActivePlayerId != x.Self.Id) return false;
        var gold = Production(x.Self.Stronghold, x.Self.Clan) + x.Self.Home.Sum(c => Production(c, x.Self.Clan));
        if (gold < 22) return false;
        var live = x.Self.Provinces.Where(p => !p.Destroyed).ToArray();
        var stocked = live.Count(p => p.Card is { } c && (c.Definition?.Type == CardType.Holding || !c.FaceUp || c.Definition is null));
        return live.Length <= 2 ? stocked == live.Length : stocked >= 3;
    }

    /// <summary>The province cards the refresh should cycle: holdings and cards the bot cannot see, leaving a Personality it could buy.</summary>
    private static string[] BorderRefreshCards(Ctx x, string[] offered) =>
        x.Self.Provinces.Where(p => !p.Destroyed && p.Card is { } c && offered.Contains(c.Id) && (c.Definition?.Type == CardType.Holding || !c.FaceUp || c.Definition is null))
            .Select(p => p.Card!.Id).ToArray();
}
