namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION MECHANICS, GROUP E (issue #501): GOLD SOURCES, PAYMENT AND THE IMPERIAL FAVOR COST -- Mendon's items 16-19 and 22-26. Every card was read letter by letter against its scan (the scan wins over
// the catalogue's Oracle text). Wired: Kakita Kae, Moto Rani - exp, Yoritomo Singh - exp2, Zlkyt (personality-wave-95), Greasing the Wheels, The Heir's Wrath, The Host's Advantage (strategy-wave-81),
// Seaside Bazaar (holding-wave-28).
//
// New primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md; none conflicts with it):
//   * PAYMENT TOKENS: a payment source that is not a card's own production, named in GameCommand.SourceIds (and in the suggested PaymentOptions) as "<card id>#<mode>" beside the plain card ids:
//       - "#reaction": a Gold-producing Reaction of a card that also bows for Gold (Library of Rebirth, Luxurious Silk, Seaside Bazaar). Its own card id is always its printed production (bowing it); the
//         token is the Reaction, which does not bow the card, is once a turn, and may be used IN THE SAME PAYMENT as the bow (Seaside Bazaar's reminder text; Mendon's item 18). Before this pass the card's
//         own id meant "the Reaction" whenever the Reaction applied, so a card could never give both.
//       - "#destroy:<Holding>": Zlkyt's "When paying a Gold cost, destroy your target Mine or Farm: Produce Gold equal to its base Gold Cost" (item 19) -- one token per Mine or Farm its controller controls
//         that can be destroyed, listed last, highest Gold Cost first; choosing one destroys that Holding (a Holding bowed for Gold in the same payment may be the one destroyed).
//       - "#discount:<dead Personality>": Moto Rani - exp's "When paying for a Spell attaching to Rani, remove a dead Personality in your discard pile from the game: Reduce the Spell's Gold Cost by the
//         Personality's Chi" (item 17) -- a reaction played at the time an attachment is paid for; PaymentSubject now names the Wielder of an Equip so a reaction can ask "attaching to whom".
//     At most one token of a card per payment (each is that card's once-a-turn Reaction). GoldQuote.Sources / PurchaseQuote.Sources list the tokens with a label and the Gold each produces (client lane).
//     The quote's suggestions include them: a token that destroys a Holding only when nothing else pays; of two equal payments, the one that bows fewer cards.
//   * NON-BOWING CONDITIONAL GOLD SOURCES (item 18): Yoritomo Singh - exp2's "When paying an action's Gold cost: Produce 3 Gold" is Yasuki Tanimura's shape (AlternateGoldSource with PaymentActionsOnly and
//     PaymentBows false); Seaside Bazaar's Reaction is a "#reaction" token for any Gold Cost (PaymentWhen "any-gold-cost").
//   * THE IMPERIAL FAVOR DISCARD AS A COST VERB (items 16, 24, 25): CostVerbs.DiscardFavor, declared in Costs (mandatory: "Discard the Imperial Favor and ...") or as one of CostOptions (the alternative:
//     "bow him or discard the Imperial Favor"). Validation (RequireFavorDiscard) and payment (DiscardImperialFavorCost, with Kyuden Otomo - exp's Favor token in its place) are the existing Favor-discard
//     path, which now reads the declaration: the hard-coded Favor-discard id list is gone, its fourteen abilities declare the verb instead (FavorDiscardMigrated, below).
//   * KAKITA KAE'S TRAIT (item 16): a targeted-by-action trait that triggers like a Reaction but is not one (TraitReactions): repeatable (no per-turn limit), usable while Kae is bowed, not barred by Cherry
//     Blossom Festival; its cost is the Favor discard and it negates Kae's destruction, bowing and movement from the action's effects.
//   * AN OPTIONAL PAYMENT AT RESOLUTION WITH AN "OTHERWISE" BRANCH (items 22-23, Greasing the Wheels' Battle action): "You may pay Gold equal to his unit's total Gold Cost minus 6. If you did, move him
//     home. Otherwise, straighten his unit." The cost floors at 0 (a unit total of 0 to 6 costs 0); the choice rides on the command (Choice "pay" or "decline"), each offered as its own action; a
//     declined payment (even of 0) straightens the unit. The payment is part of the resolution (GoldCostRule.AtResolution): a negated action pays nothing. The same fix makes Yoritomo Kanaye's optional
//     at-resolution payment actually charged (it was validated but never paid).
//   * GREASING THE WHEELS' FOCUS EFFECT (item 22): after the duel ends its winner's Gold Cost is lowered to 0 until the end of the turn.
//   * THE HEIR'S WRATH (item 24): the Favor discard or the bow of one of the acting player's Personalities with 5 or more Force (two actions sharing one use), a target Personality of another player's whose
//     unit's total Gold Cost is paid through the ordinary Gold flow (targets the player cannot pay for are not offered), "lose 2 Honor and destroy" -- these effects will not be negated (the whole action is
//     protected, and during its resolution no negation of a bow, destruction, movement or Honor loss applies: Suitengu's Torch's mark).
// ======================================================================================================================================================================================

/// <summary>An authoritative payment method: its opaque GameCommand.SourceIds entry, source card, kind ("bow", "produce", "reaction", "destroy", "discount"), label,
/// the Gold it produces, and the card it destroys or removes (null for a plain Reaction).</summary>
public sealed record PaymentSource(string Id, string CardId, string Kind, string Label, int Produced, string? SubjectId = null, string[]? ConflictingSourceIds = null);

public static class EmperorMechEAbilities
{
    public const string KaeNegate = "kae-favor-negate", KaeMoveHome = "kae-move-home";
    public const string RaniDiscount = "rani-exp-spell-discount", RaniStraighten = "rani-exp-straighten";
    public const string SinghGold = "singh-exp2-gold", SinghDestroy = "singh-exp2-destroy";
    public const string ZlkytGold = "zlkyt-destroy-gold";
    public const string GreasingFocus = "greasing-wheels-focus", GreasingBattle = "greasing-wheels-battle";
    public const string HeirsWrathFavor = "heirs-wrath-favor", HeirsWrathBow = "heirs-wrath-bow";
    public const string HostsLimited = "hosts-advantage-destroy", HostsBattle = "hosts-advantage-bow";
    public const string BazaarGold = "seaside-bazaar-gold";
    /// <summary>Seaside Bazaar's "When paying a Gold Cost": any payment.</summary>
    public const string AnyGoldCostWhen = "any-gold-cost";
    /// <summary>The Heir's Wrath's second way to pay: "bow a Personality with 5 or more Force" (one of the acting player's, anywhere in play, unbowed).</summary>
    public const string OwnUnbowedPersonalityForce5 = "mech-e-own-unbowed-personality-force-5";
    public const string PayChoice = "pay", DeclineChoice = "decline";

    /// <summary>The abilities whose "discard the Imperial Favor" was a hard-coded id list (DiscardsImperialFavorAsCost / IsFavorDiscardAbility) and now declares CostVerbs.DiscardFavor.</summary>
    public static readonly string[] FavorDiscardMigrated =
    [
        "shunya-honor", "favor-cycle", "favor-cycle-kurohito", "favor-battle", "jakuei-bow-favor", "bonds-of-coin-draw", "insulting-gesture-shame", "baiko-shame",
        "accidental-confession-dishonor", "celestial-sword-crane-favor", "sosuke-tariffs", "rengetsu-disable", "tadanobu-draw", "solid-defense-negate-favor",
    ];

    private static readonly ActionCost DiscardFavor = new(CostVerbs.DiscardFavor);

    public static Dictionary<string, AbilityDefinition> AddEmperorMechE(this Dictionary<string, AbilityDefinition> all)
    {
        // ---- Personalities (personality-wave-95) --------------------------------------------------------------------------------------------------------------------------------------------------
        // Kakita Kae (oracle-l5r-4179): "After a Battle action targets Kae, you may discard the Imperial Favor: If you did, negate Kae's destruction, bowing, and movement from the action's effects."
        // A trait (no designator; Mendon's item 16: it triggers like a Reaction but is not one, so it is repeatable) offered in the targeted-by-action windows; the Favor discard is its cost.
        all[KaeNegate] = new("Reaction", "After a Battle action targets Kae, you may discard the Imperial Favor: If you did, negate Kae's destruction, bowing, and movement from the action's effects", "none",
            Trigger: ReactionTriggers.TargetedByAction, UsesPerTurn: AbilityUseLimit.Unlimited, Costs: [DiscardFavor]);
        // "Battle: Move Kae home."
        all[KaeMoveHome] = new("Battle", "Move Kae home", "none", Move: new(MoveSpec.Source, MoveSpec.Home));
        // Moto Rani - exp (oracle-l5r-5361): "Reaction: When paying for a Spell attaching to Rani, remove a dead Personality in your discard pile from the game: Reduce the Spell's Gold Cost by the
        // Personality's Chi." A payment token ("#discount:"), never an action of its own. "Battle: Even if Rani is bowed: Straighten her. You may move her home."
        all[RaniDiscount] = new("Reaction", "When paying for a Spell attaching to Rani, remove a dead Personality in your discard pile from the game: Reduce the Spell's Gold Cost by the Personality's Chi", "none",
            Trigger: "paying-gold-cost");
        all[RaniStraighten] = new("Battle", "Even if Rani is bowed: Straighten her. You may move her home", "none",
            Branch: new(BranchCondition.Always, [new("move", "Move her home", [BranchEffect.MoveSourceHome]), new("stay", "Do not move her", [])], new("stay", "Do not move her", [])));
        // Yoritomo Singh - exp2 (oracle-l5r-9630): "Reaction: When paying an action's Gold cost: Produce 3 Gold." (Yasuki Tanimura's shape: no bow, only for an action.)
        all[SinghGold] = new("Reaction", "When paying an action's Gold cost: Produce 3 Gold", "none", Trigger: "paying-gold-cost", PaymentProduction: 3, PaymentActionsOnly: true, PaymentBows: false);
        // "Battle: Target an enemy card without attachments and pay Gold equal to its Gold Cost: Destroy it." (Kojinrue's target, the target's current Gold Cost.)
        all[SinghDestroy] = new("Battle", "Target an enemy card without attachments and pay Gold equal to its Gold Cost: Destroy it", "enemy-card-without-attachments", Cost: GoldCostRule.TargetCurrent());
        // Zlkyt (oracle-l5r-9828): "Reaction: When paying a Gold cost, destroy your target Mine or Farm: Produce Gold equal to its base Gold Cost." A payment token ("#destroy:").
        all[ZlkytGold] = new("Reaction", "When paying a Gold cost, destroy your target Mine or Farm: Produce Gold equal to its base Gold Cost", "none", Trigger: "paying-gold-cost");

        // ---- Strategies (strategy-wave-81) --------------------------------------------------------------------------------------------------------------------------------------------------------
        // Greasing the Wheels (oracle-l5r-2896): "As a Focus Effect: After this duel ends, lower the Gold Cost of its winner to 0."
        all[GreasingFocus] = new("FocusEffect", "As a Focus Effect: After this duel ends, lower the Gold Cost of its winner to 0", "none");
        // "Battle: Choose your performing unbowed Courtier or Magistrate and target a Personality: You may pay Gold equal to his unit's total Gold Cost minus 6. If you did, move him home. Otherwise,
        // straighten his unit."
        all[GreasingBattle] = new("Battle", "Choose your performing unbowed Courtier or Magistrate and target a Personality: You may pay Gold equal to his unit's total Gold Cost minus 6. If you did, move him home. Otherwise, straighten his unit", "personality",
            Performer: "own-unbowed-personality-with-any-keyword-at-battle", RequiredKeywords: ["Courtier", "Magistrate"], Cost: GoldCostRule.TargetUnitTotal(-6) with { AtResolution = true });
        // The Heir's Wrath (oracle-l5r-8127, the scan Mendon supplied): "Favor Imperial Political Limited, [Gold]: Discard the Imperial Favor or bow a Personality with 5 or more Force to lose 2 Honor and
        // destroy another player's target Personality whose unit has a total Gold Cost equal to or less than what you paid for this action; these effects will not be negated." Two actions sharing one
        // use, one per way to pay (Kurohito's Perfection's shape); the Gold paid is the target unit's total (Mendon's item 24).
        const string heirsLabel = "lose 2 Honor and destroy another player's target Personality whose unit has a total Gold Cost equal to or less than what you paid for this action; these effects will not be negated";
        all[HeirsWrathFavor] = new("Limited", "Favor Imperial Political Limited: Discard the Imperial Favor to " + heirsLabel, "enemy-personality",
            UsageKey: "heirs-wrath", ActionKeywords: ["Favor", "Imperial", "Political"], Cost: GoldCostRule.TargetUnitTotal(), Costs: [DiscardFavor]);
        all[HeirsWrathBow] = new("Limited", "Favor Imperial Political Limited: Bow a Personality with 5 or more Force to " + heirsLabel, "enemy-personality",
            Performer: OwnUnbowedPersonalityForce5, UsageKey: "heirs-wrath", ActionKeywords: ["Favor", "Imperial", "Political"], Cost: GoldCostRule.TargetUnitTotal(), Costs: ActionCost.BowPerformer);
        // The Host's Advantage (oracle-l5r-8141; the scan prints "Limited:" and "Battle:" with no Favor/Political/Winter designators, the card's keywords are Political and Winter):
        // "Limited: Discard the Imperial Favor and target an attachment: Destroy it."
        all[HostsLimited] = new("Limited", "Discard the Imperial Favor and target an attachment: Destroy it", "attachment", Costs: [DiscardFavor]);
        // "Battle: Choose your performing Courtier at any location, bow him or discard the Imperial Favor, and target an enemy Personality: Bow him. His controller loses 2 Honor."
        all[HostsBattle] = new("Battle", "Choose your performing Courtier at any location, bow him or discard the Imperial Favor, and target an enemy Personality: Bow him. His controller loses 2 Honor", "enemy-personality",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Courtier"],
            CostOptions: [new("bow-courtier", "Bow him", CostVerb.Bow, CostSubject.Performer), new("discard-favor", "Discard the Imperial Favor", CostVerbs.DiscardFavor, CostSubject.Source)]);

        // ---- Holdings (holding-wave-28) -----------------------------------------------------------------------------------------------------------------------------------------------------------
        // Seaside Bazaar (oracle-l5r-6529): "Bow this card: Produce 2 Gold." is its printed production. "Reaction: When paying a Gold Cost: Produce 1 Gold. (This can be used together with bowing this card
        // to produce Gold.)" -- a "#reaction" token (not "even if bowed": the card must be unbowed when the payment is made; the bow and the Reaction may be the same payment).
        all[BazaarGold] = new("Reaction", "When paying a Gold Cost: Produce 1 Gold", "none", Trigger: "paying-gold-cost", PaymentProduction: 1, PaymentBows: false, PaymentWhen: AnyGoldCostWhen);

        // ---- The Favor discard as data (the migration) --------------------------------------------------------------------------------------------------------------------------------------------
        foreach (var id in FavorDiscardMigrated)
        {
            var ability = all.TryGetValue(id, out var found) ? found : throw new InvalidOperationException($"Favor-discard ability '{id}' is not in the catalogue.");
            if (ability.Costs?.Any(c => c.Verb == CostVerbs.DiscardFavor) != true) all[id] = ability with { Costs = [.. ability.Costs ?? [], DiscardFavor] };
        }
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ==== PAYMENT TOKENS ================================================================================================================================================================================

    private const char PaymentTokenMark = '#';
    // Issue #695 (P1, PaymentHooks695.cs): a token may also say it Bows its card as it is used, name the zones its card may be in, require something of the OTHER sources chosen with it
    // (RequiresOtherSources: engine, the paying player, every chosen source id) and run an OnUse callback (engine, the token, the paying player) -- the channel every feature's tokens now share (EngineHooks.PaymentTokens).
    internal sealed record PaymentToken(string Id, CardInstance Card, string AbilityId, int Amount, string Kind, string Label, CardInstance? Subject,
        bool Bows = false, Func<MatchEngine, int, string[], bool>? RequiresOtherSources = null, Action<MatchEngine, PaymentToken, int>? OnUse = null, Zone[]? AllowedZones = null, string? RequirementMessage = null);

    private static bool IsPaymentToken(string id) => id.IndexOf(PaymentTokenMark) >= 0;
    private static bool IsDestroyingPaymentToken(string id) => id.Contains("#destroy:", StringComparison.Ordinal);
    private static string PaymentTokenCardId(string id) => id[..id.IndexOf(PaymentTokenMark)];

    // A payment Reaction of a card in play that is not "even if bowed": unbowed, not barred by Cherry Blossom Festival, its once-a-turn use unspent, not disabled.
    private bool PaymentReactionUsable(CardInstance card, string abilityId)
    {
        var ability = AbilityCatalog.All[abilityId];
        return !card.Bowed && !BarredByCherryBlossom(abilityId) && !AbilityUseSpent(card.AbilityUses, ability, ability.UsageKey ?? abilityId) && !AbilityDisabled(card, abilityId);
    }

    // Zlkyt's "your target Mine or Farm": the Holdings his controller has in play with either keyword that can be destroyed, highest base Gold Cost first.
    private IEnumerable<CardInstance> ZlkytHoldings(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Holding && (Keyword(c, "Mine") || Keyword(c, "Farm")) && !WillNotBeDestroyed(c))
            .OrderByDescending(c => Definition(c).GoldCost).ThenBy(c => c.Id, StringComparer.Ordinal);

    /// <summary>Every payment token the player may name for this payment: the Reactions' Gold first (in card order), then the tokens every feature file registers on EngineHooks.PaymentTokens (issue #695: Moto Rani's
    /// discounts and Zlkyt's destructions here in EmperorMechE.cs, Jade Mine and Slave Pits, Coin Pouch, Near Miss, Profits, Bamboo Harvesters, each in its own file).</summary>
    private List<PaymentToken> PaymentTokens(int playerId, PaymentSubject? paying)
    {
        var reactions = new List<PaymentToken>();
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield or Zone.Celestial or Zone.Attached).OrderBy(c => c.Id, StringComparer.Ordinal)) // a Celestial (Kenro-ji-jin's Bounty) and an attached Item (Mantle of the Jade Champion), issue #653
        {
            if (ReactiveGoldSource(card, paying) is { } reactive)
            {
                var adjustment = Definition(card).Type == CardType.Holding ? ShadowsEmbraceBatch6Production(card, playerId) : 0;
                var amount = Math.Max(0, reactive.Amount + adjustment);
                reactions.Add(new(card.Id + PaymentTokenMark + "reaction", card, reactive.AbilityId, amount, "reaction", $"{Definition(card).Name}: Reaction, produce {amount} Gold{(card.Bowed ? " (while bowed)" : " (without bowing)")}", null));
            }
        }
        return [.. reactions, .. HookPaymentTokens(playerId, paying)];
    }

    // Moto Rani's discounts, then Zlkyt's destructions (highest Gold Cost first): EngineHooks.PaymentTokens' entry for these two cards (issue #695; they were hard-coded in PaymentTokens).
    private IEnumerable<PaymentToken> MechEPaymentTokens(int playerId, PaymentSubject? paying)
    {
        var discounts = new List<PaymentToken>(); var destroys = new List<PaymentToken>();
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield or Zone.Celestial or Zone.Attached).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var name = Definition(card).Name;
            foreach (var abilityId in AbilityIdsOf(card))
            {
                if (abilityId == EmperorMechEAbilities.ZlkytGold && PaymentReactionUsable(card, abilityId))
                    foreach (var holding in ZlkytHoldings(playerId))
                        destroys.Add(new($"{card.Id}{PaymentTokenMark}destroy:{holding.Id}", card, abilityId, Definition(holding).GoldCost, "destroy",
                            $"{name}: destroy {Definition(holding).Name} and produce {Definition(holding).GoldCost} Gold", holding, OnUse: static (engine, token, player) => engine.MechEUseToken(token, player)));
                if (abilityId == EmperorMechEAbilities.RaniDiscount && PaymentReactionUsable(card, abilityId)
                    && paying is { Kind: PaymentKind.Equip, Wielder: { } wielder } && wielder.Id == card.Id && Definition(paying.Card).Type == CardType.Spell)
                    foreach (var dead in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Dead && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal))
                        discounts.Add(new($"{card.Id}{PaymentTokenMark}discount:{dead.Id}", card, abilityId, Definition(dead).Chi, "discount",
                            $"{name}: remove {Definition(dead).Name} from the game, the Spell costs {Definition(dead).Chi} less", dead, OnUse: static (engine, token, player) => engine.MechEUseToken(token, player)));
            }
        }
        return [.. discounts, .. destroys];
    }

    // Zlkyt's and Rani's tokens are spent: the card's Reaction is used for the turn; Zlkyt destroys its Holding, Rani removes its dead Personality from the game.
    private void MechEUseToken(PaymentToken token, int playerId)
    {
        var ability = AbilityCatalog.All[token.AbilityId];
        token.Card.AbilityUses[ability.UsageKey ?? token.AbilityId] = _state.TurnNumber;
        var name = Definition(token.Card).Name;
        switch (token.Kind)
        {
            case "destroy":
                Emit("goldSourceUsed", $"{name} destroyed {Definition(token.Subject!).Name} to produce {token.Amount} Gold.", playerId, token.Card.Id);
                if (InPlay(token.Subject!)) DestroyUnits([token.Subject!], false, playerId);
                break;
            case "discount":
                Emit("goldSourceUsed", $"{name} removed {Definition(token.Subject!).Name} from the game: the Spell costs {token.Amount} less.", playerId, token.Card.Id);
                if (token.Subject!.Zone == Zone.Dead) RemoveFromGame(token.Subject);
                break;
        }
    }

    private HashSet<string> PaymentSourceIds(int playerId, PaymentSubject? paying) =>
        Producers(playerId, paying).Select(c => c.Id).Concat(PaymentTokens(playerId, paying).Select(t => t.Id)).ToHashSet(StringComparer.Ordinal);

    private int PaymentTokenAmount(string id, int playerId, PaymentSubject? paying) => PaymentTokens(playerId, paying).FirstOrDefault(t => t.Id == id)?.Amount ?? 0;

    // The most the tokens can add to one payment: each card's best token.
    private int MaxPaymentTokenProduction(int playerId, PaymentSubject? paying) => PaymentTokens(playerId, paying).Where(t => t.Amount > 0 /* issue #667: Profits' reduction produces less */).GroupBy(t => t.Card.Id).Sum(g => g.Max(t => t.Amount) - TfAltOverlap(g, playerId, paying));

    // One token of a card per payment; two tokens never destroy or remove the same card; a card another of the action's costs bows or destroys is not destroyed again.
    private void RequirePaymentTokens(string[] ids, IReadOnlySet<string>? exclude)
    {
        var tokens = ids.Where(IsPaymentToken).ToArray();
        Require(tokens.GroupBy(PaymentTokenCardId).All(g => g.Count() == 1), "A card's Reaction produces Gold only once in a payment.");
        RequirePromoPaymentSources(ids); // Coin Pouch (issue #667, PromoPaymentSources.cs): only beside a Holding bowed in the same payment
        RequireTfAltExclusive(ids, exclude); // Jade Mine, Slave Pits (issue #622, TfHoldings.cs): a second way to bow is chosen instead of the card's own
        var subjects = tokens.Where(t => t.Contains(':')).Select(t => t[(t.IndexOf(':') + 1)..]).ToArray();
        Require(subjects.Distinct(StringComparer.Ordinal).Count() == subjects.Length, "A card can be destroyed or removed only once in a payment.");
        Require(exclude is null || !subjects.Any(exclude.Contains), "A card bowed or destroyed as part of the action's cost cannot also be destroyed for its Gold.");
    }

    // The tokens a payment names, read before anything is paid (a Holding bowed in the same payment is still the one Zlkyt destroys).
    private List<PaymentToken> PaymentTokensChosen(string[] ids, int playerId, PaymentSubject? paying)
    {
        if (!ids.Any(IsPaymentToken)) return [];
        var available = PaymentTokens(playerId, paying);
        return ids.Where(IsPaymentToken).Select(id => available.First(t => t.Id == id)).ToList();
    }

    // Pay's step for the tokens: each is its card's Reaction, spent for the turn; Zlkyt's destroys its Holding, Rani's removes its dead Personality from the game; a Reaction's card is not bowed.
    private void UsePaymentTokens(List<PaymentToken> tokens, int playerId)
    {
        foreach (var token in tokens)
        {
            // Issue #695: a token from EngineHooks.PaymentTokens bows its card when it says so and runs its own OnUse (Zlkyt, Rani, Jade Mine, Slave Pits, Coin Pouch, Near Miss, Profits, Bamboo Harvesters).
            if (token.Bows) token.Card.Bowed = true;
            if (token.OnUse is not null) { token.OnUse(this, token, playerId); continue; }
            if (AbilityCatalog.All.TryGetValue(token.AbilityId, out var ability)) token.Card.AbilityUses[ability.UsageKey ?? token.AbilityId] = _state.TurnNumber; // a hook token with no catalogue ability has no use to spend
            if (token.Kind == "reaction") Emit("goldSourceUsed", $"{Definition(token.Card).Name} produced {token.Amount} Gold{(token.Card.Bowed ? " while bowed" : "")}.", playerId, token.Card.Id);
        }
    }

    /// <summary>Authoritative methods for this specific payment, including ordinary production.
    /// IDs are opaque; conflicts describe the same token constraints enforced by ValidatePayment.</summary>
    private PaymentSource[] PaymentSourcesFor(int playerId, PaymentSubject? paying, IReadOnlySet<string>? exclude = null)
    {
        var tokens = PaymentTokens(playerId, paying).Where(t => exclude is null || t.Subject is null || !exclude.Contains(t.Subject.Id)).ToArray();
        var ordinary = Producers(playerId, paying).Where(c => exclude is null || !exclude.Contains(c.Id)).Select(card =>
        {
            var alternate = Definition(card).GoldProduction == 0 ? AlternateGoldSource(card, paying) : null;
            var bows = alternate is null || AbilityCatalog.All[alternate.Value.AbilityId].PaymentBows;
            var amount = GoldProduced(card.Id, playerId, paying);
            return new PaymentSource(card.Id, card.Id, bows ? "bow" : "produce",
                $"{(bows ? "Bow this card" : "Produce without bowing")}: {amount} Gold", amount, null, TfAltConflicts(card, tokens)); // issue #622: its own second way to bow (Jade Mine, Slave Pits)
        });
        return ordinary.Concat(tokens.Select(t => new PaymentSource(t.Id, t.Card.Id, t.Kind, t.Label, t.Amount, t.Subject?.Id,
            tokens.Where(other => other.Id != t.Id && (other.Card.Id == t.Card.Id ||
                t.Subject is not null && other.Subject?.Id == t.Subject.Id)).Select(other => other.Id).Concat(TfIsAltToken(t) || t.Bows ? [t.Card.Id] : []).ToArray()))).ToArray(); // issue #622: an alternative conflicts with its card's own bow
    }

    // Seaside Bazaar's payment condition (ReactivePaymentConditionMet's fall-through): "When paying a Gold Cost" -- any payment.
    private static bool MechEPaymentConditionMet(string when, PaymentSubject paying) => when == EmperorMechEAbilities.AnyGoldCostWhen;

    // ==== THE IMPERIAL FAVOR DISCARD AS A COST ==========================================================================================================================================================

    /// <summary>The ability declares the Favor discard among its (mandatory) Costs.</summary>
    private static bool DeclaresFavorDiscard(string abilityId) =>
        AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Costs?.Any(c => c.Verb == CostVerbs.DiscardFavor) == true;

    /// <summary>The command pays with an alternative-cost option that discards the Favor ("bow him or discard the Imperial Favor").</summary>
    private bool ChoseFavorDiscardOption(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.CostOptions is null) return false;
        try { return ChosenCostOption(ability, command).Verb == CostVerbs.DiscardFavor; }
        catch (RuleViolation) { return false; }
    }

    // Kyuden Otomo - exp: an alternative Favor discard may discard a Favor token instead (one more offered action).
    private IEnumerable<CostOption?> FavorTokenAlternative(AbilityDefinition ability, int playerId) =>
        ability.CostOptions?.Any(o => o.Verb == CostVerbs.DiscardFavor) == true && FavorTokenSource(playerId) is not null ? [FavorTokenOption] : [];

    // ==== KAKITA KAE ===================================================================================================================================================================================

    // "After a Battle action targets Kae": any player's Battle action (an Attack included) naming Kae, while her controller can discard the Favor (or a Favor token).
    private bool MechETargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected) =>
        abilityId == EmperorMechEAbilities.KaeNegate && affected.Id == card.Id && IsBattleAction(subject.Command)
        && (_state.FavorPlayerId == card.OwnerId || FavorTokenSource(card.OwnerId) is not null);

    // "Even if Rani is bowed"; Kae's trait (a trait is usable while its card is bowed).
    private static bool MechEEvenIfBowed(string abilityId) => abilityId is EmperorMechEAbilities.RaniStraighten or EmperorMechEAbilities.KaeNegate;

    // ==== PERFORMERS ===================================================================================================================================================================================

    private bool MechEPerformerAllows(AbilityDefinition ability, CardInstance performer) => ability.Performer == EmperorMechEAbilities.OwnUnbowedPersonalityForce5
        && Definition(performer).Type == CardType.Personality && !performer.Bowed && CardForce(performer) >= 5;

    private string?[]? MechEPerformers(AbilityDefinition ability, int playerId) => ability.Performer == EmperorMechEAbilities.OwnUnbowedPersonalityForce5
        ? _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && MechEPerformerAllows(ability, c)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray()
        : null;

    // ==== GREASING THE WHEELS ==========================================================================================================================================================================

    // "You may pay ... Otherwise, straighten his unit": the two answers, each its own offered action (the command's Choice).
    private static (string Key, string Label)[]? MechEChoiceOptions(string abilityId) => abilityId == EmperorMechEAbilities.GreasingBattle
        ? [(EmperorMechEAbilities.PayChoice, "pay the Gold: move him home"), (EmperorMechEAbilities.DeclineChoice, "do not pay: straighten his unit")]
        : null;

    // A declined payment has no Gold cost at all (AbilityGoldCost's hook).
    private static bool MechEPaymentDeclined(GameCommand command) => command.AbilityId == EmperorMechEAbilities.GreasingBattle && command.Choice == EmperorMechEAbilities.DeclineChoice;

    private void ValidateEmperorMechE(string id, GameCommand command)
    {
        if (id == EmperorMechEAbilities.GreasingBattle)
            Require(command.Choice is null or EmperorMechEAbilities.PayChoice or EmperorMechEAbilities.DeclineChoice, "Choose to pay the Gold or to straighten his unit.");
    }

    // The Focus Effect (ApplyDuelFocusEffectCore's hook): "After this duel ends, lower the Gold Cost of its winner to 0" -- until the end of the turn (an ongoing effect's default duration).
    private void ApplyMechEFocusEffect(string abilityId)
    {
        if (abilityId != EmperorMechEAbilities.GreasingFocus || _state.PendingDuel?.WinnerId is not string winnerId || !InPlay(Card(winnerId))) return;
        var winner = Card(winnerId);
        ReduceGoldCostToZero(winner, abilityId, abilityId);
        Emit("goldCostLowered", $"Greasing the Wheels: {Definition(winner).Name}'s Gold Cost is 0 until the end of the turn.", winner.OwnerId, winner.Id);
    }

    // An optional payment made as the action resolves (GoldCostRule.AtResolution): false, and nothing paid, when its sources can no longer make it.
    private bool TryPayAtResolution(GameCommand command, PlayerState player)
    {
        try { PayForAbility(command, player, atResolution: true); return true; }
        catch (RuleViolation failure)
        {
            Emit("paymentFailed", $"{player.Name} could not pay as the action resolved: {failure.Message}", player.Id, command.CardId);
            return false;
        }
    }

    // The Heir's Wrath: its effects will not be negated, but nothing says they will not be delayed (CannotBeDelayed reads this).
    private bool _notNegatedButDelayable;

    // ==== CURATED LISTS ================================================================================================================================================================================

    private static bool MechEDestroysTarget(string abilityId) => abilityId is EmperorMechEAbilities.SinghDestroy or EmperorMechEAbilities.HeirsWrathFavor or EmperorMechEAbilities.HeirsWrathBow;

    // "These effects will not be negated" (The Heir's Wrath): the whole action is protected.
    private static bool MechEUnnegatable(string abilityId) => abilityId is EmperorMechEAbilities.HeirsWrathFavor or EmperorMechEAbilities.HeirsWrathBow;

    // ==== EXECUTION: null when the id is not this group's; false when its effects are done; true when a decision opened =====================================================================================

    private bool? RunEmperorMechE(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case EmperorMechEAbilities.KaeNegate:
                NegateEffects(Card(HeldAction().TargetId!), "destroy", "bow", "move");
                return false;
            case EmperorMechEAbilities.KaeMoveHome:
                ExecuteMove(command, ability, source, target);
                return false;
            case EmperorMechEAbilities.RaniStraighten:
                Straighten(source!);
                return BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            case EmperorMechEAbilities.SinghDestroy:
                DestroyTargetCard(target!, player.Id);
                return false;
            case EmperorMechEAbilities.GreasingBattle:
                if (command.Choice == EmperorMechEAbilities.DeclineChoice)
                {
                    Emit("paymentDeclined", $"{player.Name} did not pay: {Definition(target!).Name}'s unit straightens.", player.Id, target!.Id);
                    StraightenUnit(target!);
                }
                else if (TryPayAtResolution(command, player)) MoveHome(target!, false); // "You may pay": part of the resolution, so a negated action pays nothing
                else StraightenUnit(target!); // the payment could not be made after all (its sources bowed in a window since the announcement): he did not pay
                return false;
            case EmperorMechEAbilities.HeirsWrathFavor:
            case EmperorMechEAbilities.HeirsWrathBow:
                {
                    // "These effects will not be negated": while they apply, no negation of a bow, a destruction, a movement or an Honor loss stops them (Suitengu's Torch's mark, Unnatural Flood's shape).
                    // The card does not say "or delayed", so a delay (Spirit of the Berserker, the R2 store) still applies (_notNegatedButDelayable).
                    var previousKey = _state.UnnegatableActionKey;
                    _state.UnnegatableActionKey = ActionKey(command); _notNegatedButDelayable = true;
                    try
                    {
                        GainHonor(player.Id, -2, Definition(source!).Name, source);
                        if (InPlay(target!)) DestroyTargetCard(target!, player.Id);
                    }
                    finally { _state.UnnegatableActionKey = previousKey; _notNegatedButDelayable = false; }
                    return false;
                }
            case EmperorMechEAbilities.HostsLimited:
                DestroyTargetCard(target!, player.Id);
                return false;
            case EmperorMechEAbilities.HostsBattle:
                if (InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonor(target!.OwnerId, -2, Definition(source!).Name, source);
                return false;
            default:
                return null;
        }
    }
}

public sealed partial class MatchEngine
{
    private static bool MechEHostsBattleBows(string abilityId) => abilityId == EmperorMechEAbilities.HostsBattle;
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorMechEHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorMechE", 2200, static abilities => abilities.AddEmperorMechE());
        EngineHooks.PaymentTokens.Register("P1MechEPaymentTokens", 16300, static (engine, context) => engine.MechEPaymentTokens(context.PlayerId, context.Paying)); // issue #695: Moto Rani's discounts, Zlkyt's destructions
        EngineHooks.PerformerAllows.Register("MechEPerformerAllows", 600, static (engine, context) => engine.MechEPerformerAllows(context.ability, context.performer));
        EngineHooks.PerformerCandidates.Register("MechEPerformers", 600, static (engine, context) => engine.MechEPerformers(context.ability, context.playerId));
        EngineHooks.DestroysTarget.Register("MechEDestroysTarget", 400, static context => MechEDestroysTarget(context.abilityId));
        EngineHooks.BowsTarget.Register("MechEHostsBattleBows", 600, static context => MechEHostsBattleBows(context.abilityId));
        EngineHooks.TargetedReaction.Register("MechETargetedReactionApplies", 600, static (engine, context) => engine.MechETargetedReactionApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorMechE", 1000, static (engine, context) => engine.ValidateEmperorMechE(context.id, context.command));
        EngineHooks.RunEffects.Register("RunEmperorMechE", 2000, static (engine, context) => engine.RunEmperorMechE(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
