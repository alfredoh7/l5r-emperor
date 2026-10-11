namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-EMPEROR PAYMENT SOURCES (issue #667, item-wave-44 and strategy-wave-113): Coin Pouch, Near Miss and Profits, one extension point for three cards.
//
// The Gold payment path already knows "payment tokens" (EmperorMechE.cs): a Reaction's Gold chosen BESIDE a card's own bow ("<card>#reaction"), Zlkyt's destroyed Holding, Rani's discount. This file adds
// four more kinds to the same list (PromoPaymentSourceTokens, appended by PaymentTokens), so the quote's Sources (and the client's payment panel), PaymentOptions, ValidatePayment and Pay all see them
// with no second code path:
//
//   "<pouch>#pouch"            Coin Pouch (an unbowed attached Item): "+1 Gold". It cannot produce Gold alone: it augments a Holding bowed for Gold in the SAME payment (Mendon: "if a player bows a holding
//                              to produce >0 gold, they may also bow the Coin Pouch to produce 1 gold toward the same cost"). It is never offered as a Reaction (its trigger is "paying-gold-cost", read by
//                              nothing but this file); the Pouch bows once the payment is made.
//   "<near miss>#near-miss"    Near Miss, IN HAND: "Reaction: When paying for an attachment: Produce 2 Gold." Offered only for a payment for an Item, Follower or Spell (PaymentKind.Equip); choosing it discards it.
//   "<profits>#profits-reduce" Profits in play: "Before you produce Gold to pay for a card with a base Gold Cost of 1 or greater: You may reduce the total Gold produced by 1" -- a source that produces -1.
//                              Once a turn for each Profits; the Wealth token is put on the Profits as the payment completes. Never suggested, only chosen.
//   "<profits>#profits-gold"   Profits in play with Wealth tokens: "Reaction: When paying a Gold cost, discard it: Produce Gold equal to the number of Wealth tokens on it". Choosing it discards it.
//
// Each Profits is its own card, so two Profits in play give two reduce sources, each highlighting its own card in the client. The reduce and the discard of one Profits conflict (one token per card per
// payment: RequirePaymentTokens). Readings built in: docs/decisions/2026-10-05-667-payments.md.
// ======================================================================================================================================================================================

public static class PromoPaymentSourceAbilities
{
    public const string PouchGold = "pay-coin-pouch-gold", NearMissNegate = "pay-near-miss-negate", NearMissGold = "pay-near-miss-gold",
        ProfitsEnter = "pay-profits-enter", ProfitsReduce = "pay-profits-reduce", ProfitsGold = "pay-profits-gold";

    /// <summary>The cards' Effect keys: Coin Pouch's static terms (+2 Gold Cost for its host, enters play bowed), and the identity of the two Strategies.</summary>
    public const string PouchEffect = "pay-coin-pouch", NearMissEffect = "pay-near-miss", ProfitsEffect = "pay-profits";

    public const string WealthToken = "Wealth";

    /// <summary>The suffix of each payment token's id (after the '#').</summary>
    public const string PouchKind = "pouch", NearMissKind = "near-miss", ProfitsReduceKind = "profits-reduce", ProfitsGoldKind = "profits-gold";

    public static Dictionary<string, AbilityDefinition> AddPromoPaymentSources(this Dictionary<string, AbilityDefinition> all)
    {
        // Coin Pouch (oracle-l5r-1427): "Reaction: When a Holding bows to produce Gold, bow this card: The Holding produces one additional Gold." Chosen inside a payment, never as a Reaction window.
        all[PouchGold] = new("Reaction", "When a Holding bows to produce Gold, bow this card: The Holding produces one additional Gold", "none", Trigger: "paying-gold-cost", UsageKey: "pay-coin-pouch");
        // Near Miss (oracle-l5r-5523): "Reaction: After another player's Battle action targets one of your attachments: Negate the attachment's destruction from the action's effects."
        all[NearMissNegate] = new("Reaction", "After another player's Battle action targets one of your attachments: Negate the attachment's destruction from the action's effects", "none",
            Trigger: ReactionTriggers.TargetedByAction, UsageKey: "pay-near-miss-negate");
        // "Reaction: When paying for an attachment: Produce 2 Gold." Chosen inside a payment from the hand, and discarded as the payment is made.
        all[NearMissGold] = new("Reaction", "When paying for an attachment: Produce 2 Gold", "none", Trigger: "paying-gold-cost", UsageKey: "pay-near-miss-gold");
        // Profits (oracle-l5r-6075): "Limited: Put this card into play." and its two payment terms.
        all[ProfitsEnter] = new("Limited", "Put this card into play", "none");
        all[ProfitsReduce] = new("Reaction", "Before you produce Gold to pay for a card with a base Gold Cost of 1 or greater: You may reduce the total Gold produced by 1; if you did, put a Wealth token on this card after the other card enters play", "none",
            Trigger: "paying-gold-cost", UsageKey: "pay-profits-reduce");
        all[ProfitsGold] = new("Reaction", "When paying a Gold cost, if this card is in play, discard it: Produce Gold equal to the number of Wealth tokens on it before you discarded it", "none",
            Trigger: "paying-gold-cost", UsageKey: "pay-profits-gold");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private static string PromoPaymentKindOf(string tokenId) => tokenId[(tokenId.IndexOf(PaymentTokenMark) + 1)..];

    private static bool PromoIsPouchToken(string id) => IsPaymentToken(id) && PromoPaymentKindOf(id) == PromoPaymentSourceAbilities.PouchKind;

    // A source the payer chooses deliberately and the engine never suggests: Profits' reduction lowers what the payment produces.
    // Issue #674 adds Bamboo Harvesters - exp2 with no Gold to add (WlHoldings.cs).
    private static bool PromoNeverSuggested(PaymentToken token) => token.AbilityId == PromoPaymentSourceAbilities.ProfitsReduce || WlNeverSuggested(token);

    // A source that costs its owner a card or an optional Reaction; a suggestion prefers any payment without one (Near Miss discarded, Profits discarded, the Pouch bowed).
    private static int PromoOptionalSourceCount(string[] ids) => ids.Count(id => IsPaymentToken(id) && PromoPaymentKindOf(id) is PromoPaymentSourceAbilities.PouchKind or PromoPaymentSourceAbilities.NearMissKind or PromoPaymentSourceAbilities.ProfitsGoldKind or Wave829Abilities.KokuKind);

    private bool PromoPaymentUsable(CardInstance card, string abilityId, bool reaction)
    {
        var ability = AbilityCatalog.All[abilityId];
        return !(reaction && BarredByCherryBlossom(abilityId)) && !AbilityUseSpent(card.AbilityUses, ability, ability.UsageKey ?? abilityId) && !AbilityDisabled(card, abilityId);
    }

    private bool PromoIsAttachment(CardInstance card) => Definition(card).Type is CardType.Item or CardType.Follower or CardType.Spell;

    /// <summary>PaymentTokens' extension: the Coin Pouch, Near Miss and Profits sources this payment may name (see the file header).</summary>
    private IEnumerable<PaymentToken> PromoPaymentSourceTokens(int playerId, PaymentSubject? paying)
    {
        if (paying is null) yield break; // like every Reaction Gold source: a payment with no subject (an internal one) has none
        var payingDefinition = Definition(paying.Card);
        var forAttachment = paying.Kind == PaymentKind.Equip && PromoIsAttachment(paying.Card);
        var forCard = paying.Kind is PaymentKind.Recruit or PaymentKind.Equip or PaymentKind.Play;
        bool? holdingProduces = null;
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var definition = Definition(card);
            var name = definition.Name;
            switch (definition.Effect)
            {
                case PromoPaymentSourceAbilities.PouchEffect when card.Zone == Zone.Attached && !card.Bowed && PromoPaymentUsable(card, PromoPaymentSourceAbilities.PouchGold, true):
                    holdingProduces ??= Producers(playerId, paying).Any(c => Definition(c).Type == CardType.Holding && GoldProduced(c.Id, playerId, paying) > 0);
                    if (holdingProduces == true)
                        yield return new(card.Id + PaymentTokenMark + PromoPaymentSourceAbilities.PouchKind, card, PromoPaymentSourceAbilities.PouchGold, 1, "reaction",
                            $"{name}: bow it, a Holding bowed in this payment produces 1 additional Gold", null, OnUse: static (engine, token, player) => engine.PromoUsePaymentToken(token, player));
                    break;
                case PromoPaymentSourceAbilities.NearMissEffect when card.Zone == Zone.Hand && forAttachment && card.Id != paying.Card.Id && PromoPaymentUsable(card, PromoPaymentSourceAbilities.NearMissGold, true):
                    yield return new(card.Id + PaymentTokenMark + PromoPaymentSourceAbilities.NearMissKind, card, PromoPaymentSourceAbilities.NearMissGold, 2, "reaction",
                        $"{name}: discard it from your hand, produce 2 Gold for the attachment", null, OnUse: static (engine, token, player) => engine.PromoUsePaymentToken(token, player), AllowedZones: [Zone.Hand]);
                    break;
                case PromoPaymentSourceAbilities.ProfitsEffect when card.Zone == Zone.Home:
                    if (forCard && payingDefinition.GoldCost >= 1 && PromoPaymentUsable(card, PromoPaymentSourceAbilities.ProfitsReduce, false))
                        yield return new(card.Id + PaymentTokenMark + PromoPaymentSourceAbilities.ProfitsReduceKind, card, PromoPaymentSourceAbilities.ProfitsReduce, -1, "reduce",
                            $"Use {name}: produce 1 less Gold; it gains a Wealth token once {payingDefinition.Name} enters play", null, OnUse: static (engine, token, player) => engine.PromoUsePaymentToken(token, player));
                    var wealth = card.Tokens.Count(t => t.Type == PromoPaymentSourceAbilities.WealthToken);
                    if (wealth > 0 && PromoPaymentUsable(card, PromoPaymentSourceAbilities.ProfitsGold, true))
                        yield return new(card.Id + PaymentTokenMark + PromoPaymentSourceAbilities.ProfitsGoldKind, card, PromoPaymentSourceAbilities.ProfitsGold, wealth, "reaction",
                            $"{name}: discard it, produce {wealth} Gold (its Wealth tokens)", null, OnUse: static (engine, token, player) => engine.PromoUsePaymentToken(token, player));
                    break;
            }
        }
    }

    // RequirePaymentTokens' extension: the Coin Pouch is only ever beside a Holding bowed for Gold in the same payment.
    private void RequirePromoPaymentSources(string[] ids)
    {
        if (!ids.Any(PromoIsPouchToken)) return;
        Require(ids.Any(id => !IsPaymentToken(id) && _state.Cards.TryGetValue(id, out var source) && Definition(source).Type == CardType.Holding),
            "The Coin Pouch adds Gold only to a Holding bowed for Gold in the same payment.");
    }

    // A suggestion that names the Pouch needs its Holding.
    private bool PromoPaymentOptionValid(string[] ids) => !ids.Any(PromoIsPouchToken) || ids.Any(id => !IsPaymentToken(id) && Definition(Card(id)).Type == CardType.Holding);

    /// <summary>UsePaymentTokens' step for these tokens; true when the token was one of this file's.</summary>
    private bool PromoUsePaymentToken(PaymentToken token, int playerId)
    {
        if (token.AbilityId is not (PromoPaymentSourceAbilities.PouchGold or PromoPaymentSourceAbilities.NearMissGold or PromoPaymentSourceAbilities.ProfitsReduce or PromoPaymentSourceAbilities.ProfitsGold)) return false;
        var ability = AbilityCatalog.All[token.AbilityId];
        var card = token.Card; var name = Definition(card).Name; var player = Player(playerId);
        card.AbilityUses[ability.UsageKey ?? token.AbilityId] = _state.TurnNumber;
        switch (token.AbilityId)
        {
            case PromoPaymentSourceAbilities.PouchGold:
                card.Bowed = true;
                Emit("goldSourceUsed", $"{name} bowed: a Holding produced 1 additional Gold.", playerId, card.Id);
                break;
            case PromoPaymentSourceAbilities.NearMissGold:
                Emit("goldSourceUsed", $"{name} was discarded to produce 2 Gold.", playerId, card.Id);
                DiscardFate(player, card);
                break;
            case PromoPaymentSourceAbilities.ProfitsReduce:
                CreateToken(card, PromoPaymentSourceAbilities.WealthToken, "paying for a card");
                Emit("goldSourceUsed", $"{name}: {player.Name} produced 1 less Gold and put a Wealth token on it.", playerId, card.Id);
                break;
            case PromoPaymentSourceAbilities.ProfitsGold:
                Emit("goldSourceUsed", $"{name} was discarded to produce {token.Amount} Gold.", playerId, card.Id);
                ResetCardState(card);
                card.Zone = Zone.FateDiscard; card.ProvinceIndex = null; card.FaceUp = true;
                break;
        }
        return true;
    }

    internal PaymentSource[] PaymentSourcesForTest(int playerId, PaymentSubject paying) => PaymentSourcesFor(playerId, paying);

    // ---- Coin Pouch's static terms ----------------------------------------------------------------------------------------------------------------------------------------------------

    // CardGoldCost's term: "This Personality has +2 Gold Cost" for each Coin Pouch attached to him.
    private int PromoPaymentHostGoldCost(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || !InPlay(card)) return 0;
        return 2 * Attachments(card.Id).Count(a => Definition(a).Effect == PromoPaymentSourceAbilities.PouchEffect);
    }

    // "This card enters play bowed": called where an attachment is put onto its Personality (Equip, the attach-by-ability path).
    private void PromoPaymentAttached(CardInstance attachment)
    {
        if (Definition(attachment).Effect == PromoPaymentSourceAbilities.PouchEffect) attachment.Bowed = true;
    }

    // ---- Near Miss's destruction-negating Reaction ----------------------------------------------------------------------------------------------------------------------------------------

    // "After another player's Battle action targets one of your attachments": the held action is a Battle action of the other player whose target is an Item, Follower or Spell of the reactor's,
    // and that could destroy it (an Attack on a Follower always could; an attachment-destroying ability is on the curated list).
    private bool PromoNearMissApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && IsBattleAction(subject.Command) && affected.OwnerId == card.OwnerId && affected.Zone == Zone.Attached && PromoIsAttachment(affected)
        && (subject.IsAttack || DestroysTarget(subject.Command.AbilityId!) || DestroysAttachmentTarget(subject.Command.AbilityId!));

    private void ValidatePromoPaymentSources(string id, CardInstance? source, PlayerState player)
    {
        if (id == PromoPaymentSourceAbilities.ProfitsEnter) Require(source!.Zone == Zone.Hand && player.Hand.Contains(source.Id), "This card is put into play from your hand.");
    }

    private bool? RunPromoPaymentSources(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case PromoPaymentSourceAbilities.NearMissNegate:
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            case PromoPaymentSourceAbilities.ProfitsEnter:
                // "Limited: Put this card into play": into the player's Holdings area (Zone.Home, like The Bite of Winter), unbowed, with no tokens.
                player.Hand.Remove(source!.Id); source.Zone = Zone.Home; source.FaceUp = true; source.ProvinceIndex = null;
                Emit("cardEnteredPlay", $"{player.Name} puts {Definition(source).Name} into play.", player.Id, source.Id);
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoPaymentSourceHooks()
    {
        EngineHooks.Abilities.Register("AddPromoPaymentSources", 12200, static abilities => abilities.AddPromoPaymentSources());
        EngineHooks.PaymentTokens.Register("P1PromoPaymentTokens", 16302, static (engine, context) => engine.PromoPaymentSourceTokens(context.PlayerId, context.Paying)); // issue #695: Coin Pouch, Near Miss, Profits
        EngineHooks.TargetedReaction.Register("PromoPaymentSourcesTargeted", 12200, static (engine, context) =>
            context.abilityId == PromoPaymentSourceAbilities.NearMissNegate && engine.PromoNearMissApplies(context.card, context.subject, context.affected));
        EngineHooks.ValidateMiddle.Register("ValidatePromoPaymentSources", 12200, static (engine, context) => engine.ValidatePromoPaymentSources(context.id, context.source, context.player));
        EngineHooks.RunEffects.Register("RunPromoPaymentSources", 12200, static (engine, context) => engine.RunPromoPaymentSources(context.id, context.command, context.player, context.source));
    }
}
