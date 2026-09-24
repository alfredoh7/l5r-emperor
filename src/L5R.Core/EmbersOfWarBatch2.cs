namespace L5R.Core;

// Embers of War pass (issue #460), batch 2: primitives shared by more than one card of this batch, or too small to
// deserve their own file. Each is documented at its own use in CardAbilities.cs; this file holds only the
// execution/validation helpers. Mirrors HonorAndTreacheryPass.cs's own shape.
public sealed partial class MatchEngine
{
    // Yasuki Makoto (oracle-l5r-9449): "Your Holdings enter play for 1 less Gold." The same live, per-source-in-play
    // shape Mukudori's own "Your other Fudo cards enter play for 1 less Gold" already gives KeywordEntryDiscount
    // (StaticKeywordRules.cs), narrowed to CardType.Holding instead of a keyword (there is exactly one Makoto, so
    // "per Makoto in play" and "while any Makoto is in play" agree; the shape stays the count-based one for
    // consistency with its own precedent). Read at both of QuotePurchase's and EquipGoldCost's own call sites.
    private const string MakotoHoldingDiscountEffect = "makoto-holding-discount";
    private int HoldingEntryDiscount(CardInstance entering, int playerId) =>
        Definition(entering).Type == CardType.Holding
            ? _state.Cards.Values.Count(c => c.Id != entering.Id && c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == MakotoHoldingDiscountEffect)
            : 0;

    // Rokku - experienced (oracle-l5r-6376): "Rokku has +1F while a Lion Clan player controls him." A term of
    // PersonalityForce, the same Effect-keyed shape Matsu Kaido's own plain "+1F while attacking" (attacking-one)
    // uses, but unconditional on attacking -- gated only by the card's own controller's Stronghold Clan.
    private int RokkuClanForceBonus(CardInstance card) => Definition(card).Effect == "rokku-lion-force-bonus" && Player(card.OwnerId).Clan == "Lion" ? 1 : 0;

    // Rata (oracle-l5r-6165): "Reaction: After an action resolves that Rata performed and that destroyed a
    // Personality: Rata sacrifices his victim's corpse. Draw a card." AfterActionReactionTriggers.AfterActionResolved
    // already scopes to "the resolution of an action" (training-after-action-resolved-gain's own precedent, which
    // reads any of the reacting player's own Personalities destroyed); RataDestroyedAPersonality narrows further to
    // an action RATA HIMSELF performed (LastResolvedAction.PerformerId, not merely his controller's action) that
    // destroyed a Personality of EITHER player's (his own printed text names no owner, unlike the training fixture).
    private bool RataDestroyedAPersonality(CardInstance rata) =>
        _state.LastResolvedAction is { } facts && facts.PerformerId == rata.Id
        && facts.Affected.Any(a => a.Kind == DestroyedKind && Definition(Card(a.CardId)).Type == CardType.Personality);

    // Sakti (oracle-l5r-6454): "Battle: Target an enemy card with lower Force: Bow it. If it is a Personality, you
    // may move it home." The bow is the ability's own plain effect (BowsTarget-style, wired in the execution
    // switch); the optional move-home is conditioned on the target's own printed type here.
    private void SaktiBow(CardInstance target, int playerId)
    {
        if (!StalwartNegatesBow(target, playerId)) target.Bowed = true;
        if (Definition(target).Type == CardType.Personality) MoveHome(target, false);
    }

    // Soshi Yoshihara - experienced (oracle-l5r-7373): "Bow Yoshihara and target a Personality: Dishonor him. If you
    // are a Scorpion Clan or Crab Clan player, his controller loses 1 Honor." The acting player's own Stronghold
    // Clan (Player.Clan), not the target's -- the printed condition is about who is USING the ability.
    private void YoshiharaDishonor(CardInstance target, int playerId)
    {
        Dishonor(target);
        if (Player(playerId).Clan is "Scorpion" or "Crab") GainHonor(target.OwnerId, -1, "Soshi Yoshihara");
    }

    // Sugihara (oracle-l5r-7615): "Battle: Target an enemy Personality: Give him -4F. If his Force is now 0, you may
    // take an additional Battle action which Sugihara must perform." The additional action is granted scoped to
    // Sugihara's own id (GrantAdditionalAction's own scopeCardIds parameter), so only he may take it.
    private void SugiharaPenalty(CardInstance target, CardInstance source, int playerId)
    {
        ApplyModifier(target, source.Id, "sugihara-penalty", "Force", -4);
        if (CardForce(target) == 0) GrantAdditionalAction(playerId, [source.Id]);
    }

    // Rokku - experienced (oracle-l5r-6376): "Reaction: Even if Rokku is bowed, after engaging, if any enemy units
    // are at the current battlefield: Move Rokku there. Straighten his unit if he moved." MovesToBattle already
    // performs the "move Rokku there" half (his own AtBattle/presence checks are waived, the same shape Bayushi
    // Irezu's Home Battle already uses); this reads whether he was NOT already there before the move (so
    // "straighten his unit if he moved" fires only on an actual move) and the live enemy presence at the engaged
    // battlefield off _state.PendingEngage.
    // "If any enemy units are at the current battlefield" -- relative to ROKKU'S OWN controller, not the Attacker/Defender role (a defending
    // Rokku checks the Attacker's presence; an attacking Rokku checks the Defender's).
    private bool RokkuEnemyPresentAtEngagedBattlefield(int rokkuOwnerId) => _state.PendingEngage is { } engage && Army(1 - rokkuOwnerId, engage.ProvinceIndex).Any();

    private void RokkuMove(CardInstance rokku)
    {
        if (RokkuEnemyPresentAtEngagedBattlefield(rokku.OwnerId) && !AtBattle(rokku) && TryMoveToBattlefield(rokku, _state.PendingEngage!.ProvinceIndex)) StraightenUnit(rokku);
    }

    // Yoritomo Kanaye (oracle-l5r-9579): "Battle: Target an enemy card without attachments, and you may pay Gold
    // equal to its Gold Cost: Bow it. If you paid the Gold, destroy it." Cost: GoldCostRule.Variable(atResolution:
    // true) offers zero or more Gold; ValidateAbility narrows the legal amounts to exactly zero or the target's own
    // Gold Cost (a binary choice, not an open range) with a per-id Require next to Irezu's/Hawado's own precedent.
    private void KanayeBow(CardInstance target, GameCommand command, int playerId)
    {
        if (!StalwartNegatesBow(target, playerId)) target.Bowed = true;
        if (PaidGoldAmount(command) > 0) DestroyTargetCard(target, playerId);
    }
}
