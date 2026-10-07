namespace L5R.Core;

// Honor and Treachery pass (issue #458): primitives shared by more than one card of the set, or too small to deserve their own file.
// Each is documented at its own use in CardAbilities.cs; this file holds only the execution/validation helpers.
public sealed partial class MatchEngine
{
    // The Towers of the Yogo (oracle-l5r-8392): "Each of your Scorpion Clan Personalities has the ability, 'Battle, bow: Bow a target enemy
    // Follower or Personality without Followers.'" A controller-wide STATIC ability grant -- unlike GrantAbility's durable per-instance grant,
    // this reaches every Scorpion Clan Personality of the Stronghold's controller (including one that enters play later), and disappears the
    // moment the Stronghold itself would leave play (which a Stronghold never does before the game ends, so in practice this never expires
    // early). Read live by AbilityIdsOf, the one place a card's current abilities are joined, exactly as StaticKeywordRules.cs's grants are
    // read live by Keyword(). Both the source (InPlay, the same guard StaticKeywordRuleSources uses) and the target card need it.
    private const string TowersOfYogoGrantEffect = "towers-of-yogo-grant";
    private const string TowersOfYogoAbilityId = "towers-of-yogo-bow";
    // The Ki-Rin's Path (oracle-l5r-8187, Embers of War pass, issue #460 batch 5): "Your Tacticians have the ability, 'Tactical Open: Give
    // this Personality +1F and Cavalry.'" Same controller-wide static ability grant shape as the Towers of the Yogo above, scoped to the
    // Tactician keyword instead of a Clan.
    private const string KiRinsPathGrantEffect = "ki-rins-path-grant";
    private const string KiRinsPathAbilityId = "ki-rins-path-boost";

    private IEnumerable<string> StaticGrantedAbilityIds(CardInstance card)
    {
        foreach (var p22 in FilterGrantedAbilityIds(card)) yield return p22; // P22 (issue #690, CreatedCopies690.cs): filter-scoped grants, for a card of any type
        if (Definition(card).Type != CardType.Personality || !InPlay(card)) yield break;
        if (HasClanAffiliation(card, "Scorpion") && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Effect == TowersOfYogoGrantEffect))
            yield return TowersOfYogoAbilityId;
        if (Keyword(card, "Tactician") && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Effect == KiRinsPathGrantEffect))
            yield return KiRinsPathAbilityId;
        if (WhiteShorePlainGrants(card)) yield return WhiteShorePlainAbilityId; // White Shore Plain (Emperor Edition remaining-cards pass, issue #492, group D).
        if (Definition(card).Effect == UnmeiMirrorEffect && (Keyword(card, "Samurai") || Keyword(card, "Shugenja")))
            yield return UnmeiAttackAbilityId; // Unmei (Emperor Edition Personality pass, issue #477, batch 3): the ability she has while she has Samurai or Shugenja.
        foreach (var p2 in P2StaticGrantedAbilityIds(card)) yield return p2; // issue #503: Brothers in Battle (EmperorP2.cs)
        foreach (var p3Granted in P3StaticGrantedAbilityIds(card)) yield return p3Granted; // Thunder Dragon - exp2 (issue #503)
    }

    // Shiba Tsukimi (oracle-l5r-10246): "Twice per turn, target a card in a unit: Straighten it. It may use its abilities a second time this
    // turn." The same "clear the per-turn use stamp" again-boost (issue #331, Kensai's Weapons) already does, generalized to any card's every
    // Battle ability rather than only a Kensai's own Weapons.
    private void RefreshBattleAbilities(CardInstance card)
    {
        foreach (var abilityId in AbilityIdsOf(card))
            if (AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Timing is "Battle" or "BattleOrOpen")
                card.AbilityUses.Remove(ability.UsageKey ?? abilityId);
    }

    // Bayushi Irezu (oracle-l5r-793): "Home Battle: If Irezu is at home, move home another target Scorpion Clan Personality you control. Move
    // Irezu to the current battlefield." A straight swap: the target's own move-home (wherever it currently stands) and Irezu's own move to the
    // battlefield are independent effects -- MovesToBattle (issue #331) exempts this ability's own AtBattle/presence checks for Irezu himself,
    // the same way Return to Action's own "your Personality at home" shape is exempted; the target's category (own-scorpion-personality-other-
    // than-irezu) carries no location restriction of its own, since "move home" is a no-op for a target already there.
    private void IrezuMove(GameCommand command, CardInstance irezu, CardInstance target, int battlefield)
    {
        MoveHome(target, false);
        TryMoveToBattlefield(irezu, battlefield);
    }
}
