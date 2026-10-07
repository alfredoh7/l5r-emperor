namespace L5R.Core;

// Issue #677 (created-card proxies): the creators whose created card had no published proxy scan until Mendon's ruling of 2026-10-06 ("Use the ivory frame for the cards, no other frame exists.
// Prefer the oracle's scans that are exact matches over my scans that are manufactured."), and the proxies that carry an action of their own. Every creator was read against its own scan; each
// uses only mechanisms that already exist (CreatedCardSpec and the entry sequence of CreatedCardEntry.cs, the Invest window, EntryHonorLoss, the after-action record, ScheduleTimedRemoval).
// Waves personality-wave-145, follower-wave-58, item-wave-58, spell-wave-65, strategy-wave-129 and holding-wave-63 (tools/content/package_created_proxies_677.py). Ability ids are "proxy677-".
//   Abdollah                 "After Abdollah enters play, lose 2 Honor and create a 2F/2C/0PH Nonhuman - Shadowlands - Undead Personality."   EntryHonorLoss 2 + proxy677-abdollah-entry (Zombie)
//   Kitsune Kichi - exp      "After Kichi enters play, lose 3 Honor and create a 4F/2C/0PH Badger ... with the ability, 'Battle, Bow: Melee 4 Attack.'"   EntryHonorLoss 3 + proxy677-kichi-exp-entry
//   Kuni Renyu - exp2        "After Renyu enters play, create a 1F Dog - Nonhuman Follower and attach it to him." / "Earth Battle: Bow a target enemy card with Shadowlands or without
//                            attachments."   proxy677-renyu-entry (Renyu's Dog), proxy677-renyu-bow
//   Hiruma Koru              "Invest 1: Create a 0F Follower and attach it to Koru."   proxy677-koru-invest (Disposable Troops; Clay Kshatriya's Invest window)
//   Kitsune Yamazaru         "Interrupt: After you Recruit Yamazaru, create a 3F/2C/3PH Mantis Clan - Monkey - Nonhuman - Spirit Personality with the ability ..."   proxy677-yamazaru-create
//                            (Kitsu Tamao's entering-play creation); the Monkey's action is proxy677-monkey-weaken (Moto Paikao's)
//   Komori Miyano            "Air Limited: Create a 0F/2C/0PH Bat - Nonhuman - Cavalry Personality. Remove it from the game after your next turn begins."   proxy677-miyano-bat
//   Daigotsu Hashibei        "Fear Open: Bow Hashibei and target your Personality: Create a 1F Follower and attach it to him."   proxy677-hashibei-create (Farmlands Conscripts)
//   Shinjo Tselu - exp2      "Limited, 2 Gold: Create a 1F Cavalry Follower and attach it to your target Personality."   proxy677-tselu-create (Dutiful Cavalry)
//   Goju Kaxt                "Other Ninja in this army have +1F." / "After an action resolves during which this card was destroyed: Create a 4F/3C/0PH Ninja Personality with your Clan
//                            alignment at any location."   proxy677-kaxt-ninja-force (the Force hooks); proxy677-kaxt-create-home and proxy677-kaxt-create-battle, one use between them
//   Shard of the Great Death's Bones   "After this Item enters play, lose 2 Honor, create three 1F ... Followers, and attach them to your Personalities."   EntryHonorLoss 2 +
//                            proxy677-shard-create (Bayushi Makubesu's host slot; Birds of Prey)
//   Yojimbo of Earth         "Earth Limited, Bow: Create a 1F Clay Soldier - Nonhuman - Yojimbo Follower and attach it to your target Shugenja without Followers."   proxy677-yojimbo-earth
//   Ashalan Ambush           "Limited, 8 Gold: Create a 4F/4C/0PH Ashalan - Nonhuman - Shugenja Personality."   proxy677-ashalan-create
//   New Cavalry Tactics      "Engage, 3 Gold: Target your Personality. Create and attach two, or three if the target is a Commander, 1F Elephant ... Followers to him."   proxy677-cavalry-tactics
//   A New Perspective        "Battle, 2 Gold: Create and attach a 1F Follower to each of your opposed Personalities at the current battlefield."   proxy677-new-perspective
//   Recruitment Effort       "Open: Destroy your target non-Unique Holding: Create a 3F/3C/2PH Samurai Personality."   proxy677-recruitment-effort (Hired Samurai; Basic Lesson's cost)
//   The Onyx Dueling Circle  "Limited, Bow: Destroy your target Shugenja. Create a 5F/2C/0PH Nonhuman - Oni - Shadowlands Personality. Destroy this Holding. Lose 4 Honor."   proxy677-onyx-circle
//   Kichi's Shadow Badger    "Battle, Bow: Melee 4 Attack."   proxy677-badger-melee
//   Never Without a Weapon   "Battle, 5 Gold: Discard a card to create a Weapon Item with a Force Modifier equal to the discarded card's Focus Value and attach it to your target
//                            Personality."   proxy677-never-without-weapon: the Unexpected Weapon, its printed Force set to the Focus Value (CardInstance.Printed, issue #405)
//   The Wind's Champion      "Air Open, Bow: Bow this Shugenja to create a 2C/0PH Illusion - Samurai - Spirit Personality with Force equal to this Shugenja's Chi, plus 1 if he is Air.
//                            Destroy this Spell."   proxy677-winds-champion: the Major Illusion, its printed Force set to the Shugenja's current Chi (plus 1 if he is Air)
// The last two set one printed value of the created card as it is made. Since P22 (issue #690) their specs say so (CreatedCardSpec.Values: a current-stat read of the wielder; the Focus
// Value of the card in the cost-only discard slot), and ExecuteCreate writes the value on the created card as the action resolves.
// A Strategy's printed Gold cost is its own Gold Cost, paid by the engine's rule for every Strategy (GoldQuotes.ImplicitGoldRule); the other Gold costs are on the ability.
// Readings, for Mendon to confirm or correct, are in docs/decisions/2026-10-06-677-created-card-proxies.md.
public static class CreatedProxies677Abilities
{
    public const string AbdollahEntry = "proxy677-abdollah-entry", KichiExpEntry = "proxy677-kichi-exp-entry", RenyuEntry = "proxy677-renyu-entry", KaxtEffect = "proxy677-kaxt-ninja-force";
    public const string KoruInvest = "proxy677-koru-invest",
        RenyuBow = "proxy677-renyu-bow", YamazaruCreate = "proxy677-yamazaru-create", MiyanoBat = "proxy677-miyano-bat", HashibeiCreate = "proxy677-hashibei-create",
        TseluCreate = "proxy677-tselu-create", KaxtCreateHome = "proxy677-kaxt-create-home", KaxtCreateBattle = "proxy677-kaxt-create-battle", ShardCreate = "proxy677-shard-create",
        YojimboEarth = "proxy677-yojimbo-earth", AshalanCreate = "proxy677-ashalan-create", CavalryTactics = "proxy677-cavalry-tactics", NewPerspective = "proxy677-new-perspective",
        RecruitmentEffort = "proxy677-recruitment-effort", OnyxCircle = "proxy677-onyx-circle", BadgerMelee = "proxy677-badger-melee", MonkeyWeaken = "proxy677-monkey-weaken",
        NeverWithoutWeapon = "proxy677-never-without-weapon", WindsChampion = "proxy677-winds-champion";

    // Target categories of this file (TargetAllows). They are not named "proxy...": a creator's target never names the card it creates (CreatedCardTests checks the word).
    public const string OwnShugenjaWithoutFollowers = "created677-own-shugenja-without-followers", OwnNonUniqueHolding = "created677-own-non-unique-holding",
        EnemyShadowlandsOrUnattached = "created677-enemy-shadowlands-or-unattached";

    /// <summary>The mark an action's record carries for a Goju Kaxt destroyed while it was in progress, alone or with his Personality (AfterCardDestroyed).</summary>
    public const string KaxtDestroyedKind = "proxy677-kaxt-destroyed";

    // The created cards, as their creators print them. Each names its scan, and the scan must still fit the printed type, stats, keyword set and ability text (CreatedCards.Resolve).
    public static readonly CreatedCardSpec Zombie = new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 0, Keywords: ["Nonhuman", "Shadowlands", "Undead"], ProxyId: "oracle-l5r-11349");
    public static readonly CreatedCardSpec ShadowBadger = new(CardType.Personality, Force: 4, Chi: 2, PersonalHonor: 0, Keywords: ["Badger", "Nonhuman", "Shadowlands", "Spirit"],
        AbilityText: "Battle, Bow: Melee 4 Attack", ProxyId: "oracle-l5r-11335");
    public static readonly CreatedCardSpec Dog = new(CardType.Follower, Force: 1, Keywords: ["Dog", "Nonhuman"], ProxyId: "oracle-l5r-10815") { HostIsSource = true };
    public static readonly CreatedCardSpec Retinue = new(CardType.Follower, Force: 0, Keywords: [], ProxyId: "oracle-l5r-9745") { HostIsSource = true };
    public static readonly CreatedCardSpec Conscripts = new(CardType.Follower, Force: 1, Keywords: [], ProxyId: "oracle-l5r-10004");
    public static readonly CreatedCardSpec Monkey = new(CardType.Personality, Force: 3, Chi: 2, PersonalHonor: 3, Keywords: ["Mantis Clan", "Monkey", "Nonhuman", "Spirit"],
        AbilityText: "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value", ProxyId: "oracle-l5r-11327");
    public static readonly CreatedCardSpec Bat = new(CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 0, Keywords: ["Bat", "Nonhuman", "Cavalry"], ProxyId: "oracle-l5r-10809");
    public static readonly CreatedCardSpec DutifulCavalry = new(CardType.Follower, Force: 1, Keywords: ["Cavalry"], ProxyId: "oracle-l5r-10003");
    public static readonly CreatedCardSpec Kaxt = new(CardType.Personality, Force: 4, Chi: 3, PersonalHonor: 0, Keywords: ["Ninja"], AbilityText: "This card has your Clan alignment keyword",
        ProxyId: "oracle-l5r-10813");
    public static readonly CreatedCardSpec BirdsOfPrey = new(CardType.Follower, Force: 1, Keywords: ["Nonhuman", "Raptor", "Shadowlands", "Undead", "Cavalry"], ProxyId: "oracle-l5r-9712", Count: 3);
    public static readonly CreatedCardSpec ClayYojimbo = new(CardType.Follower, Force: 1, Keywords: ["Clay Soldier", "Nonhuman", "Yojimbo"], ProxyId: "oracle-l5r-11332");
    public static readonly CreatedCardSpec Ashalan = new(CardType.Personality, Force: 4, Chi: 4, PersonalHonor: 0, Keywords: ["Ashalan", "Nonhuman", "Shugenja"], ProxyId: "oracle-l5r-11328");
    public static readonly CreatedCardSpec Elephants = new(CardType.Follower, Force: 1, Keywords: ["Elephant", "Nonhuman", "Cavalry"], ProxyId: "oracle-l5r-11337", Count: 2);
    public static readonly CreatedCardSpec HiredSamurai = new(CardType.Personality, Force: 3, Chi: 3, PersonalHonor: 2, Keywords: ["Samurai"], ProxyId: "oracle-l5r-10811");
    public static readonly CreatedCardSpec OnyxOni = new(CardType.Personality, Force: 5, Chi: 2, PersonalHonor: 0, Keywords: ["Nonhuman", "Oni", "Shadowlands"], ProxyId: "oracle-l5r-11343");
    // The two whose Force is set as they are made: the spec names no Force, and the scan prints "*". The value is declared (P22, issue #690): the Focus Value of the card in the
    // cost-only discard slot; the current Chi of the Shugenja the Spell is attached to, plus 1 if he is Air.
    public static readonly CreatedCardSpec UnexpectedWeapon = new(CardType.Item, Chi: 0, Keywords: ["Weapon"], AbilityText: "This Item's Force Modifier is equal to the discarded card's Focus Value",
        ProxyId: "oracle-l5r-11348")
    { HostSlot = "target", Values = [new(CreatedStat.Force, CreatedSubject.Slot, Read: CreatedStat.Focus) { Slot = "discard" }] };
    public static readonly CreatedCardSpec MajorIllusion = new(CardType.Personality, Chi: 2, PersonalHonor: 0, Keywords: ["Illusion", "Samurai", "Spirit"],
        AbilityText: "This Personality's Force is equal to the Chi of the caster, plus 1 if he was Air", ProxyId: "oracle-l5r-11338")
    { Values = [new(CreatedStat.Force, CreatedSubject.Wielder, Read: CreatedStat.Chi, Keyword: "Air", PlusWithKeyword: 1) { Current = true }] };

    /// <summary>Invest, Goju Kaxt's creation and the Shard's creation print no "Reaction:" designator: they are traits modelled with Reaction timing, so a rule that bars Reactions does not touch
    /// them (Mendon's R4 ruling; TraitReactions.Is asks this). Kitsune Yamazaru's "Interrupt:" and New Cavalry Tactics' "Engage" are printed designators, not traits.</summary>
    public static bool IsTrait(string abilityId) => abilityId is KoruInvest or KaxtCreateHome or KaxtCreateBattle or ShardCreate;

    public static Dictionary<string, AbilityDefinition> AddCreatedProxies677(this Dictionary<string, AbilityDefinition> all)
    {
        all[KoruInvest] = new("Reaction", "Invest 1 Gold: Create a 0F Follower and attach it to Koru", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(1), Creates: Retinue);
        all[RenyuBow] = new("Battle", "Earth Battle: Bow a target enemy card with Shadowlands or without attachments", EnemyShadowlandsOrUnattached, ActionKeywords: ["Earth"]);
        all[YamazaruCreate] = new("Reaction", "Interrupt: After you Recruit Yamazaru, create a 3F/2C/3PH Mantis Clan · Monkey · Nonhuman · Spirit Personality", "none", Trigger: "own-enters-play",
            Creates: Monkey);
        all[MonkeyWeaken] = new("Battle", "Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value", "slots",
            Slots: [new("discard", "hand", "A card from your hand, to discard (its Focus Value is the Force penalty)") { CostOnly = true },
                new("target", "enemy-force", "An enemy Personality or Follower")],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))]);
        all[MiyanoBat] = new("Limited", "Air Limited: Create a 0F/2C/0PH Bat · Nonhuman · Cavalry Personality. Remove it from the game after your next turn begins", "none",
            ActionKeywords: ["Air"], Creates: Bat);
        all[HashibeiCreate] = new("Open", "Fear Open: Bow Hashibei and target your Personality: Create a 1F Follower and attach it to him", "own-personality",
            ActionKeywords: ["Fear"], Costs: ActionCost.BowSource, Creates: Conscripts);
        all[TseluCreate] = new("Limited", "2 Gold: Create a 1F Cavalry Follower and attach it to your target Personality", "own-personality", Cost: GoldCostRule.Fixed(2), Creates: DutifulCavalry);
        // "At any location": his controller's home, or the current battlefield when there is one. One printed trait with a choice of place, so two abilities that share one use.
        all[KaxtCreateHome] = new("Reaction", "After an action resolves during which this card was destroyed: Create a 4F/3C/0PH Ninja Personality with your Clan alignment in your home", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, FromDiscard: true, UsageKey: KaxtCreateHome, Creates: Kaxt);
        all[KaxtCreateBattle] = new("Reaction", "After an action resolves during which this card was destroyed: Create a 4F/3C/0PH Ninja Personality with your Clan alignment at the current battlefield",
            "none", Trigger: AfterActionReactionTriggers.AfterActionResolved, FromDiscard: true, UsageKey: KaxtCreateHome, Creates: Kaxt with { AtBattlefield = true });
        all[ShardCreate] = new("Reaction", "After this Item enters play: Create three 1F Nonhuman · Raptor · Shadowlands · Undead · Cavalry Followers and attach them to your Personalities", "slots",
            Trigger: "own-enters-play", Slots: [new("hosts", "own-personality", "One to three of your Personalities (the three Followers are dealt to them in turn)", 1, 3)], Creates: BirdsOfPrey);
        all[YojimboEarth] = new("Limited", "Earth Limited, bow this card: Create a 1F Clay Soldier · Nonhuman · Yojimbo Follower and attach it to your target Shugenja without Followers",
            OwnShugenjaWithoutFollowers, ActionKeywords: ["Earth"], Costs: ActionCost.BowSource, Creates: ClayYojimbo);
        all[AshalanCreate] = new("Limited", "8 Gold: Create a 4F/4C/0PH Ashalan · Nonhuman · Shugenja Personality", "none", Creates: Ashalan);
        all[CavalryTactics] = new("Reaction", "Engage, 3 Gold: Target your Personality. Create and attach two, or three if the target is a Commander, 1F Elephant · Nonhuman · Cavalry Followers to him",
            "own-personality", Trigger: "after-engaging", Creates: Elephants);
        all[NewPerspective] = new("Battle", "2 Gold: Create and attach a 1F Follower to each of your opposed Personalities at the current battlefield", "none");
        all[RecruitmentEffort] = new("Open", "Destroy your target non-Unique Holding: Create a 3F/3C/2PH Samurai Personality", OwnNonUniqueHolding,
            Costs: [new(CostVerbs.Sacrifice, CostSubject.Target)], Creates: HiredSamurai);
        all[OnyxCircle] = new("Limited", "Bow this card: Destroy your target Shugenja. Create a 5F/2C/0PH Nonhuman · Oni · Shadowlands Personality. Destroy this Holding. Lose 4 Honor",
            "own-personality-with-keyword", RequiredKeywords: ["Shugenja"], Costs: ActionCost.BowSource, Creates: OnyxOni);
        all[BadgerMelee] = new("Battle", "Bow this card: Melee 4 Attack", "attack", Costs: ActionCost.BowSource);
        // The discard is named in a cost-only slot listed before the target (Moto Paikao's shape); the created Weapon goes to the target slot's Personality.
        all[NeverWithoutWeapon] = new("Battle", "5 Gold: Discard a card to create a Weapon Item with a Force Modifier equal to the discarded card's Focus Value and attach it to your target Personality",
            "slots", Cost: GoldCostRule.Fixed(5),
            Slots: [new("discard", "hand", "A card from your hand, to discard (its Focus Value is the Weapon's Force Modifier)") { CostOnly = true },
                new("target", "own-personality", "Your Personality, to carry the Weapon")],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))], Creates: UnexpectedWeapon);
        all[WindsChampion] = new("Open", "Air Open, bow this card: Bow this Shugenja to create a 2C/0PH Illusion · Samurai · Spirit Personality with Force equal to this Shugenja's Chi, plus 1 if he is Air. Destroy this Spell",
            "none", ActionKeywords: ["Air"], Costs: [new(CostVerb.Bow), new(CostVerb.Bow, CostSubject.Wielder)], Creates: MajorIllusion);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- target legality ----

    private bool CreatedProxies677TargetAllows(string category, PlayerState player, CardInstance target)
    {
        var type = Definition(target).Type;
        var own = target.OwnerId == player.Id;
        return category switch
        {
            // "your target Shugenja without Followers".
            CreatedProxies677Abilities.OwnShugenjaWithoutFollowers => own && type == CardType.Personality && Keyword(target, "Shugenja")
                && !Attachments(target.Id).Any(a => Definition(a).Type == CardType.Follower),
            // "your target non-Unique Holding".
            CreatedProxies677Abilities.OwnNonUniqueHolding => own && type == CardType.Holding && !Keyword(target, "Unique"),
            // "a target enemy card with Shadowlands or without attachments": a card of a unit with the Shadowlands keyword, or a Follower or a Personality with no attachment
            // (the engine's reading of "a card without attachments": enemy-card-without-attachments).
            CreatedProxies677Abilities.EnemyShadowlandsOrUnattached => !own && (type is CardType.Personality or CardType.Follower or CardType.Item or CardType.Spell && Keyword(target, "Shadowlands")
                || type == CardType.Follower || type == CardType.Personality && !HasAttachments(target)),
            _ => false
        };
    }

    // ---- effects ----

    private bool? RunCreatedProxies677(RunHookContext c)
    {
        var (id, ability, command, player, source, target) = (c.id, c.ability, c.command, c.player, c.source, c.target);
        switch (id)
        {
            case CreatedProxies677Abilities.RenyuBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case CreatedProxies677Abilities.MonkeyWeaken:
                // The discard was paid as a cost; the penalty is the discarded card's Focus Value (its definition is unchanged in the discard pile), until the turn ends.
                ApplyModifier(target!, source!.Id, id, "Force", -FocusValueOf(SlotCards(command, "discard").Single()));
                return false;
            case CreatedProxies677Abilities.BadgerMelee:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, c.consumedGrant);
            case CreatedProxies677Abilities.MiyanoBat:
                {
                    var name = Definition(source!).Name;
                    return ExecuteCreate(ability, ability.Creates!, command, player, () =>
                    {
                        var created = Card($"created-{_state.CreatedCardSequence:D4}"); // the Bat ExecuteCreate has just minted
                        ScheduleTimedRemoval(created, TimedRemovalWhen.PlayersNextTurnBegins, player.Id, name);
                    });
                }
            // The Strategies: the played card is spent before the creation, because a created card's entering-play window ends this case early (Exotic Farmlands' order).
            // Never Without a Weapon: the discard was paid as a cost; the Weapon's Force Modifier is the discarded card's Focus Value, read by its spec (its definition is unchanged in the pile).
            case CreatedProxies677Abilities.AshalanCreate or CreatedProxies677Abilities.RecruitmentEffort or CreatedProxies677Abilities.NeverWithoutWeapon:
                SpendHandPlayedCard(player, source);
                return ExecuteCreate(ability, ability.Creates!, command, player);
            case CreatedProxies677Abilities.CavalryTactics:
                SpendHandPlayedCard(player, source);
                // "two, or three if the target is a Commander".
                return ExecuteCreate(ability, ability.Creates! with { Count = Keyword(target!, "Commander") ? 3 : 2 }, command, player);
            case CreatedProxies677Abilities.NewPerspective:
                {
                    SpendHandPlayedCard(player, source);
                    // "each of your opposed Personalities at the current battlefield": none when the other side has no unit there, and then nothing is created.
                    // A Personality the Follower may not be attached to gets none (the rule a chosen host is held to, ValidateCreate).
                    var scan = CreatedCards.Resolve(CreatedProxies677Abilities.Conscripts);
                    var hosts = _state.CurrentBattleProvince is int battle
                        ? Army(player.Id, battle).Where(p => Opposed(p) && CreatedProxies677MayAttach(scan, p, player.Id)).OrderBy(p => p.Id, StringComparer.Ordinal).ToArray() : [];
                    if (hosts.Length == 0) return false;
                    return CreateAttachments(player.Id, scan, null, CreatedProxies677Abilities.Conscripts with { PerHost = true }, hosts, false, null);
                }
            case CreatedProxies677Abilities.OnyxCircle:
                {
                    // Four sentences, in the printed order: the Shugenja, the Oni, the Holding, the Honor. Each is an effect, so one that fails does not stop the next.
                    var name = Definition(source!).Name;
                    DestroyTargetCard(target!, player.Id);
                    return CreatePersonality(player.Id, CreatedCards.Resolve(ability.Creates!), false, null, out _, null, () =>
                    {
                        if (InPlay(source!)) DestroyUnits([source!], false, player.Id);
                        GainHonor(player.Id, -4, name, source);
                    });
                }
            case CreatedProxies677Abilities.WindsChampion:
                // Both bows were paid as costs. "Force equal to this Shugenja's Chi, plus 1 if he is Air": his Chi as it stands now, written on the Illusion as it is made
                // (the scan: "plus 1 if he was Air"), by the spec's current-value read. Then "Destroy this Spell".
                if (WielderOf(source!) is null) return false;
                return ExecuteCreate(ability, ability.Creates!, command, player, () =>
                {
                    if (InPlay(source!)) DestroyAttachment(source!);
                });
            // Koru's Invest, Yamazaru's Monkey, Hashibei's and Tselu's Followers, Kaxt, the Shard's Birds and the Clay Yojimbo need no case: the default arm runs the
            // ability's Creates (CreatedCardEntry.cs), inside the entry window for the entering-play ones.
            default:
                return null;
        }
    }

    // Whether a created attachment could be equipped to this Personality: the card does not exist yet, so a stand-in with its definition is judged (ValidateCreate's own test).
    private bool CreatedProxies677MayAttach(CardDefinition scan, CardInstance host, int playerId)
    {
        try { RequireAttachable(new CardInstance { Id = "created-preview", DefinitionId = scan.Id, OwnerId = playerId, Zone = Zone.Hand, FaceUp = true }, host, playerId); return true; }
        catch (RuleViolation) { return false; }
    }

    // ---- mandatory traits ----

    // "After [this card] enters play, ... create ...": a trait with no designator, run with the card's other mandatory entry effects (ApplyMandatoryEntryEffects), after its EntryHonorLoss.
    // Made inside the entry, so the created card opens no window of its own (Soshi Kodanshi's and The Great Death's shape).
    private void ApplyCreatedProxies677EntryEffects(int playerId, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            case CreatedProxies677Abilities.AbdollahEntry:
                CreatePersonality(playerId, CreatedCards.Resolve(CreatedProxies677Abilities.Zombie), true, null, out _);
                break;
            case CreatedProxies677Abilities.KichiExpEntry:
                CreatePersonality(playerId, CreatedCards.Resolve(CreatedProxies677Abilities.ShadowBadger), true, null, out _);
                break;
            case CreatedProxies677Abilities.RenyuEntry when InPlay(card) && Definition(card).Type == CardType.Personality
                && CreatedProxies677MayAttach(CreatedCards.Resolve(CreatedProxies677Abilities.Dog), card, playerId):
                CreateAttachments(playerId, CreatedCards.Resolve(CreatedProxies677Abilities.Dog), null, CreatedProxies677Abilities.Dog, [card], true, null);
                break;
        }
    }

    // Goju Kaxt, "Other Ninja in this army have +1F": +1F for each Goju Kaxt in the same army (the units of one player at one battlefield) other than the card itself. No army at home.
    private int CreatedProxies677NinjaForce(CardInstance card)
    {
        var host = card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var wielder) ? wielder : card;
        if (host.Zone != Zone.Battlefield || host.ProvinceIndex is not int battlefield) return 0;
        var count = 0;
        foreach (var other in _state.Cards.Values)
            if (other.Zone == Zone.Attached && other.Id != card.Id && Definition(other).Effect == CreatedProxies677Abilities.KaxtEffect && other.AttachedTo is { } otherHostId
                && _state.Cards.TryGetValue(otherHostId, out var otherHost) && otherHost.OwnerId == host.OwnerId && otherHost.Zone == Zone.Battlefield && otherHost.ProvinceIndex == battlefield)
                count++;
        return count > 0 && Keyword(card, "Ninja") ? count : 0;
    }

    // AfterCardDestroyed's call: a Goju Kaxt that is destroyed, alone or with his Personality, is marked on the record of the action in progress (DestroyUnits records only the Personality).
    private void NoteCreatedProxies677Destroyed(CardInstance destroyed)
    {
        if (Definition(destroyed).Effect == CreatedProxies677Abilities.KaxtEffect) RecordActionEffect(destroyed.Id, CreatedProxies677Abilities.KaxtDestroyedKind);
    }

    // AfterActionResolvedApplies' term. "After an action resolves during which this card was destroyed": Kaxt is in his owner's Fate discard pile, the action that has just resolved carries
    // his mark, and it destroyed him or the Personality he was attached to. The second half keeps a mark left by a destruction that was no action's (a battle's resolution) from counting.
    private bool? CreatedProxies677AfterActionApplies(CardInstance source, string abilityId, ResolvedActionFacts facts) =>
        abilityId is not (CreatedProxies677Abilities.KaxtCreateHome or CreatedProxies677Abilities.KaxtCreateBattle) ? null
        : source.Zone == Zone.FateDiscard && facts.Affected.Any(a => a.Kind == CreatedProxies677Abilities.KaxtDestroyedKind && a.CardId == source.Id)
            && facts.Affected.Any(a => a.Kind == DestroyedKind && (a.CardId == source.Id || source.LastHostId is not null && a.CardId == source.LastHostId));

    private static int? CreatedProxies677AttackStrength(string abilityId) => abilityId == CreatedProxies677Abilities.BadgerMelee ? 4 : null;

    private static string? CreatedProxies677AttackKind(string abilityId) => abilityId == CreatedProxies677Abilities.BadgerMelee ? "Melee" : null;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterCreatedProxies677Hooks()
    {
        EngineHooks.Abilities.Register("Proxy677Abilities", 13200, static abilities => abilities.AddCreatedProxies677());
        EngineHooks.RunEffects.Register("Proxy677RunEffects", 13200, static (engine, context) => engine.RunCreatedProxies677(context));
        EngineHooks.TargetAllows.Register("Proxy677TargetAllows", 13200, static (engine, context) => engine.CreatedProxies677TargetAllows(context.ability.Target, context.player, context.target));
        EngineHooks.AttackStrengthBefore.Register("Proxy677AttackStrength", 13200, static (engine, context) => CreatedProxies677AttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("Proxy677AttackKind", 13200, static context => CreatedProxies677AttackKind(context.abilityId));
        EngineHooks.BowsTarget.Register("Proxy677BowsTarget", 13200, static context => context.abilityId == CreatedProxies677Abilities.RenyuBow);
        EngineHooks.DestroysTarget.Register("Proxy677DestroysTarget", 13200, static context => context.abilityId == CreatedProxies677Abilities.OnyxCircle);
        EngineHooks.PersonalityForce.Register("Proxy677PersonalityForce", 13200, static (engine, context) => engine.CreatedProxies677NinjaForce(context.card));
        EngineHooks.FollowerForce.Register("Proxy677FollowerForce", 13200, static (engine, context) => engine.CreatedProxies677NinjaForce(context.card));
    }
}
