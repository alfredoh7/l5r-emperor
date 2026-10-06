namespace L5R.Core;

// ======================================================================================================================================================================================
// THE "PERS-GATES-1" PERSONALITIES (issue #674, personality-wave-128): fourteen Gates of Chaos Personalities that the #668 assessment found wirable from existing mechanisms, each wired from the
// printed text of its scan.
//
//   * ASAHINA KITIARU - EXP: "Limited, bow: Search your discard pile, then Fate deck, for a Ring. Show it and put it in your hand. Discard a card." An ordered Ring search (R-011, R-012) whose card
//     goes to the hand and is followed by the discard of another card (Ikoma Shizuka's Terrain search shape: the card just found cannot be the one discarded).
//   * ASAKO IFUKUBE: Destined and Expendable are keywords; the card prints only their reminder text.
//   * ASAKO KARACHU - EXP: "Open: Give Karachu Cavalry, Conqueror, or Naval." Three Open actions sharing one use (Asako Hayate's shape); the keyword lasts until the turn ends (R-001).
//   * BOYOH MERCENARY: "After Boyoh Mercenary enters play, discard a card." and "After Boyoh Mercenary's army destroys a Province, draw a card." Two traits (no designator), so mandatory: the discard is
//     a forced discard queued as the card enters (Daigotsu Bofana's shape), the draw is read where a battle's resolution destroys a province.
//   * DAIDOJI TAMETAKA - EXP: "Tametaka loses any duel against Yoritomo Hiromi." (the duel's outcome is settled in ResolveDuel, WlPersGates1DuelOutcome) and "Battle: Reduce the Force of a target
//     enemy Personality or Follower to 0." (Matsu Kaido's delta-to-zero shape).
//   * DAIDOJI TOBEI: Destined, "After Tobei enters play, lose 1 Honor" (EntryHonorLoss) and "Battle, bow: Ranged 2 Attack."
//   * DOJI RAZAN: Invest 1 Gold for a +1C token, "Limited: Destroy a target Fudo card. Destroy Razan."
//   * HIDA GOJIRO / ISAWA HAJIME: Invest 4 Gold for two +1F tokens / Invest 3 Gold for two +1F Fire tokens (Togashi Noritada's shape), "After enters play, lose 1 Honor" (EntryHonorLoss).
//   * HOSHOKU-SHA: "Battle/Open: Destroy your target Personality. Give Hoshoku-sha +2F."
//   * IDE HINOBU: "Bow: Produce 3 Gold." is a printed production of 3 (Daidoji Soken's shape).
//   * KITSUNE YUKO: Invest 2 Gold to gain 1 Honor; "Limited, bow: Gain 1 Honor."
//   * LEGULUS: "After Legulus enters play, lose 2 Honor." (EntryHonorLoss), "Melee and Ranged Attacks targeting cards in this unit have -3 strength." (the AttackDefense fields, Shieldman's
//     shape) and "Limited: Search your deck for a Yodotai Legionnaire Follower. Show it. Put it in your hand."
//   * M'RIKA - EXP: "Open, bow: Bow a target Personality."
//
// Decision note: docs/decisions/2026-10-06-674-pers-gates-1.md.
// ======================================================================================================================================================================================

public static class WlPersGates1Abilities
{
    public const string KitiaruSearch = "wl1-kitiaru-search";
    public const string KarachuCavalry = "wl1-karachu-cavalry", KarachuConqueror = "wl1-karachu-conqueror", KarachuNaval = "wl1-karachu-naval";
    public const string TametakaZero = "wl1-tametaka-zero", TobeiAttack = "wl1-tobei-attack";
    public const string RazanInvest = "wl1-razan-invest", RazanDestroy = "wl1-razan-destroy", GojiroInvest = "wl1-gojiro-invest", HajimeInvest = "wl1-hajime-invest";
    public const string HoshokuDestroy = "wl1-hoshoku-destroy", YukoInvest = "wl1-yuko-invest", YukoHonor = "wl1-yuko-honor", LegulusSearch = "wl1-legulus-search", MrikaBow = "wl1-mrika-bow";

    /// <summary>The Effect keys of the cards whose static traits live in the engine (CardDefinition.Effect).</summary>
    public const string BoyohEffect = "wl1-boyoh", TametakaEffect = "wl1-tametaka";

    /// <summary>The Invest abilities: a keyword trait with no designator (Mendon's R4 ruling: traits are not Reactions even though the engine models them with Reaction timing).</summary>
    private static readonly HashSet<string> Traits = new(StringComparer.Ordinal) { RazanInvest, GojiroInvest, HajimeInvest, YukoInvest };

    public static bool IsTrait(string abilityId) => Traits.Contains(abilityId);

    public static Dictionary<string, AbilityDefinition> AddWlPersGates1(this Dictionary<string, AbilityDefinition> all)
    {
        // Asahina Kitiaru - exp (oracle-l5r-10623).
        all[KitiaruSearch] = new("Limited", "Bow Kitiaru: search your discard pile, then Fate deck, for a Ring; show it, put it in your hand, discard a card", "none", Costs: ActionCost.BowSource,
            Search: WlPersGates1Searches.Kitiaru);
        // Asako Karachu - exp (oracle-l5r-10647): "Open: Give Karachu Cavalry, Conqueror, or Naval." One action with a choice of keyword (a shared use).
        all[KarachuCavalry] = new("Open", "Give Karachu Cavalry", "none", UsageKey: "wl1-karachu-give");
        all[KarachuConqueror] = new("Open", "Give Karachu Conqueror", "none", UsageKey: "wl1-karachu-give");
        all[KarachuNaval] = new("Open", "Give Karachu Naval", "none", UsageKey: "wl1-karachu-give");
        // Daidoji Tametaka - exp (oracle-l5r-10627): "Battle: Reduce the Force of a target enemy Personality or Follower to 0."
        all[TametakaZero] = new("Battle", "Target an enemy Personality or Follower: Reduce its Force to 0", "enemy-force");
        // Daidoji Tobei (oracle-l5r-10628): "Battle, bow: Ranged 2 Attack."
        all[TobeiAttack] = new("Battle", "Bow Tobei: Ranged 2 Attack", "attack", Costs: ActionCost.BowSource);
        // Doji Razan (oracle-l5r-10626): "Invest 1: Give Razan a +1C token." / "Limited: Destroy a target Fudo card. Destroy Razan."
        all[RazanInvest] = new("Reaction", "Invest 1 Gold: give this Personality a +1C token", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(1));
        all[RazanDestroy] = new("Limited", "Target a Fudo card: Destroy it. Destroy Razan", "non-stronghold-card-with-keyword", RequiredKeywords: ["Fudo"]);
        // Hida Gojiro (oracle-l5r-10617): "Invest 4: Give Gojiro two +1F tokens."
        all[GojiroInvest] = new("Reaction", "Invest 4 Gold: give this Personality two +1F tokens", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(4));
        // Isawa Hajime (oracle-l5r-10649): "Invest 3: Give Hajime two +1F Fire tokens."
        all[HajimeInvest] = new("Reaction", "Invest 3 Gold: give this Personality two +1F Fire tokens", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(3));
        // Hoshoku-sha (oracle-l5r-10670): "Battle/Open: Destroy your target Personality. Give Hoshoku-sha +2F."
        all[HoshokuDestroy] = new("BattleOrOpen", "Destroy your target Personality. Give Hoshoku-sha +2F", "own-personality");
        // Kitsune Yuko (oracle-l5r-10641): "Invest 2: Gain 1 Honor." / "Limited, bow: Gain 1 Honor."
        all[YukoInvest] = new("Reaction", "Invest 2 Gold: gain 1 Honor", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(2));
        all[YukoHonor] = new("Limited", "Bow Yuko: gain 1 Honor", "none", Costs: ActionCost.BowSource);
        // Legulus (oracle-l5r-10668): "Limited: Search your deck for a Yodotai Legionnaire Follower. Show it. Put it in your hand."
        all[LegulusSearch] = new("Limited", "Search your deck for a Yodotai Legionnaire Follower, show it, put it in your hand", "none", Search: WlPersGates1Searches.Legulus);
        // M'rika - exp (oracle-l5r-10661): "Open, bow: Bow a target Personality."
        all[MrikaBow] = new("Open", "Bow M'rika: Bow a target Personality", "personality", Costs: ActionCost.BowSource);
        return all;
    }
}

public static class WlPersGates1Searches
{
    private static readonly SearchCriteria RingCard = new("Ring", [CardType.Ring]);
    private static readonly SearchCriteria LegionnaireCard = new("Yodotai Legionnaire Follower", [CardType.Follower], Titles: ["Yodotai Legionnaire"]);

    /// <summary>Asahina Kitiaru - exp: "Search your discard pile, then Fate deck, for a Ring. Show it and put it in your hand. Discard a card." Ordered (R-011); the card then discarded is another one.</summary>
    public static SearchSpec Kitiaru { get; } = new("wl1-kitiaru-ring", "Search your discard pile, then Fate deck, for a Ring",
        [new(SearchDeck.FateDiscard, RingCard, SearchEffect.ToHandThenDiscard), new(SearchDeck.Fate, RingCard, SearchEffect.ToHandThenDiscard)], Ordered: true);

    /// <summary>Legulus: "Search your deck for a Yodotai Legionnaire Follower. Show it. Put it in your hand." A Follower is a Fate card: the Fate deck.</summary>
    public static SearchSpec Legulus { get; } = new("wl1-legulus-legionnaire", "Search your Fate deck for a Yodotai Legionnaire Follower",
        [new(SearchDeck.Fate, LegionnaireCard, SearchEffect.ToHand)]);

    public static IEnumerable<SearchSpec> All => [Kitiaru, Legulus];
}

public sealed partial class MatchEngine
{
    // ---- entering play and province destruction (traits, so mandatory) ----------------------------------------------------------------------------------------------------------------------

    // ApplyMandatoryEntryEffects' hook. Boyoh Mercenary: "After Boyoh Mercenary enters play, discard a card." The discard is a forced discard (the entering player chooses a card of his own hand; an
    // empty hand owes nothing), queued like Daigotsu Bofana's so a Proclaim or entry window already open is never overwritten.
    private void ApplyWlPersGates1EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != WlPersGates1Abilities.BoyohEffect) return;
        _state.PendingForcedDiscards.Add(new(playerId, Definition(card).Name));
    }

    // AfterProvinceDestroyedByResolution's hook. Boyoh Mercenary: "After Boyoh Mercenary's army destroys a Province, draw a card." Every Boyoh in the attacking army that stands at the
    // battlefield as the province is destroyed draws a card for his controller.
    private void WlPersGates1AfterProvinceDestroyed(int attacker, CardInstance[] attackingArmy)
    {
        foreach (var boyoh in attackingArmy.Where(c => c.Zone == Zone.Battlefield && c.OwnerId == attacker && Definition(c).Effect == WlPersGates1Abilities.BoyohEffect))
        {
            Draw(Player(attacker));
            Emit("boyohDraw", $"{Definition(boyoh).Name}: {Player(attacker).Name} drew a card as the army destroyed a province.", attacker, boyoh.Id);
        }
    }

    // ---- Daidoji Tametaka - exp: "Tametaka loses any duel against Yoritomo Hiromi." ---------------------------------------------------------------------------------------------------------

    // ResolveDuel's hook, after the totals (and the Duelist tie-break) have named a result. When Tametaka is a duelist and the other is a Yoritomo Hiromi (either version), a result in which Tametaka
    // would not lose is replaced: he loses and Hiromi, the other duelist, wins. A tie (both lose) already has Tametaka losing and is left alone; a win by Hiromi is left alone.
    private (string? WinnerId, List<string> LoserIds, string Reason) WlPersGates1DuelOutcome(PendingDuel duel, string? winnerId, List<string> loserIds, string reason)
    {
        foreach (var (mine, other) in new[] { (duel.ChallengerId, duel.ChallengedId), (duel.ChallengedId, duel.ChallengerId) })
        {
            var tametaka = Card(mine); var opponent = Card(other);
            if (Definition(tametaka).Effect != WlPersGates1Abilities.TametakaEffect || !InPlay(tametaka) || winnerId != tametaka.Id) continue;
            if (Experienced.BaseTitle(Definition(opponent).Name) != "Yoritomo Hiromi") continue;
            Emit("duelAutoLoss", $"{Definition(tametaka).Name} loses any duel against Yoritomo Hiromi.", tametaka.OwnerId, tametaka.Id);
            return (opponent.Id, [tametaka.Id], $"{Definition(tametaka).Name} loses any duel against Yoritomo Hiromi");
        }
        return (winnerId, loserIds, reason);
    }

    // ---- the actions ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int? WlPersGates1AttackStrength(string abilityId) => abilityId == WlPersGates1Abilities.TobeiAttack ? 2 : null;

    private static string? WlPersGates1AttackKind(string abilityId) => abilityId == WlPersGates1Abilities.TobeiAttack ? "Ranged" : null;

    private static bool WlPersGates1DestroysTarget(string abilityId) => abilityId is WlPersGates1Abilities.RazanDestroy or WlPersGates1Abilities.HoshokuDestroy;

    // null: not this cluster's. false: the effects are done. true: a decision opened (a search) and the action finishes from there.
    private bool? RunWlPersGates1(RunHookContext context)
    {
        var (id, command, player, source, target) = (context.id, context.command, context.player, context.source, context.target);
        switch (id)
        {
            case WlPersGates1Abilities.KitiaruSearch:
                BeginSearch(WlPersGates1Searches.Kitiaru, source!, player.Id, "ability", context.bowedBeforeAction);
                return true;
            case WlPersGates1Abilities.LegulusSearch:
                BeginSearch(WlPersGates1Searches.Legulus, source!, player.Id, "ability", context.bowedBeforeAction);
                return true;
            case WlPersGates1Abilities.KarachuCavalry: GrantKeyword(source!, "Cavalry", source!.Id, GrantDuration.Turn); return false;
            case WlPersGates1Abilities.KarachuConqueror: GrantKeyword(source!, "Conqueror", source!.Id, GrantDuration.Turn); return false;
            case WlPersGates1Abilities.KarachuNaval: GrantKeyword(source!, "Naval", source!.Id, GrantDuration.Turn); return false;
            case WlPersGates1Abilities.TametakaZero:
                ApplyModifier(target!, source!.Id, id, "Force", -CardForce(target!));
                return false;
            case WlPersGates1Abilities.TobeiAttack:
                return ResolveOrDeferAttack(id, player, command.TargetId, ActionPerformer(context.ability, command, source).Id, context.consumedGrant);
            case WlPersGates1Abilities.RazanInvest:
                CreateToken(source!, "Invest", id, "Chi", 1);
                return false;
            case WlPersGates1Abilities.RazanDestroy:
                // Two sentences, both done: the target is destroyed, then Razan.
                DestroyTargetCard(target!, player.Id);
                if (InPlay(source!)) DestroyTargetCard(source!, player.Id);
                return false;
            case WlPersGates1Abilities.GojiroInvest:
                CreateToken(source!, "Invest", id, "Force", 1);
                CreateToken(source!, "Invest", id, "Force", 1);
                return false;
            case WlPersGates1Abilities.HajimeInvest:
                CreateToken(source!, "Fire", id, "Force", 1);
                CreateToken(source!, "Fire", id, "Force", 1);
                return false;
            case WlPersGates1Abilities.HoshokuDestroy:
                // "Destroy your target Personality. Give Hoshoku-sha +2F." Two sentences, both done (the target may be Hoshoku-sha himself, who then has nothing to give it to).
                DestroyTargetCard(target!, player.Id);
                if (InPlay(source!)) ApplyModifier(source!, source!.Id, id, "Force", 2);
                return false;
            case WlPersGates1Abilities.YukoInvest:
            case WlPersGates1Abilities.YukoHonor:
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            case WlPersGates1Abilities.MrikaBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlPersGates1Hooks()
    {
        EngineHooks.Abilities.Register("AddWlPersGates1", 14100, static abilities => abilities.AddWlPersGates1());
        EngineHooks.Searches.Register("WlPersGates1Searches", 14100, static () => WlPersGates1Searches.All);
        EngineHooks.RunEffects.Register("RunWlPersGates1", 14100, static (engine, context) => engine.RunWlPersGates1(context));
        EngineHooks.AttackStrengthAfter.Register("WlPersGates1AttackStrength", 14100, static (engine, context) => WlPersGates1AttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("WlPersGates1AttackKind", 14100, static context => WlPersGates1AttackKind(context.abilityId));
        EngineHooks.DestroysTarget.Register("WlPersGates1DestroysTarget", 14100, static context => WlPersGates1DestroysTarget(context.abilityId));
        EngineHooks.BowsTarget.Register("WlPersGates1BowsTarget", 14100, static context => context.abilityId == WlPersGates1Abilities.MrikaBow);
    }
}
