using System.Text.Json.Serialization;

namespace L5R.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardType { Stronghold, Holding, Personality, Strategy, Follower, Item, Spell, Event, Celestial, Region, Ring }
[JsonConverter(typeof(JsonStringEnumConverter))]
// AttackDeclaration/DefenseAssignment is the Infantry Maneuvers segment; CavalryDeclaration/
// CavalryDefense is the Cavalry Maneuvers segment that follows it (core rules Attack Phase, step C).
public enum Phase { Straighten, Events, Action, AttackDeclaration, DefenseAssignment, CavalryDeclaration, CavalryDefense, BattleSelection, Battle, Dynasty, DynastyDiscard, End, Finished }
[JsonConverter(typeof(JsonStringEnumConverter))]
// RemovedFromGame (issue #184 -- core rules Glossary): a card here can never be targeted or used
// again under any circumstance, distinct from Dead/DynastyDiscard/FateDiscard -- both Fate and
// Dynasty cards mix into this one zone, matching the single shared "Removed from Game" pile the
// client will render (a Dead/DynastyDiscard/FateDiscard card is always specifically one or the
// other; this one is neither, and both kinds of card can end up here).
// Focused (issue #204 -- Dueling): a Fate card played face-down next to a dueling Personality. Not
// "played" in the ordinary sense (no ability on it resolves except a "Focus Effect" one, later, in
// the duel's own resolution order) -- visible to its own owner immediately, to both players once the
// duel's own Struck reveal happens (PendingDuel.Struck), same shape as a private Hand card that
// becomes public.
// EventInPlay (Suspicions, the first Event): an Event's printed text normally resolves once, in the
// Events Phase, then goes straight to DynastyDiscard (core rules Glossary -- "resolves and is
// discarded"). An Event whose own text is an ongoing/"until the end of the game" effect instead sits
// here -- a physical reminder area next to a player's Personalities (the client's own future
// "sideboard" rendering, not yet built) -- rather than the discard pile, for as long as that effect
// stays active. Nothing yet moves a card OUT of this zone (Suspicions' own effect never ends), but the
// intended shape for a future card that does end it is: move to DynastyDiscard, same as an ordinary
// Event. Public zone, not hidden information, like RemovedFromGame.
// Region (the first being Low Market/Fallow Lands): attaches to a PROVINCE, not to another card --
// deliberately a distinct Zone from Attached (which always resolves its parent via
// CardInstance.AttachedTo/Card(...)) so Attachments()/DestroyAttachment()/AtBattle() never misfire on
// it. ProvinceState.RegionId is the province-side pointer (the province "owns" the Region, mirroring
// CardId for the Dynasty card sitting there); the Region's own AttachedTo stays null and its
// ProvinceIndex names which province it is attached to. Public, face-up, like a Holding/Personality
// once revealed. Visually renders like an ordinary attachment despite the different host.
// Celestial (the first being Lady Matsu's Rage): unlike Event/Region, enters play the instant it is
// revealed in ANY phase (not deferred to the controller's own next Events Phase) and never
// resolves-once-then-discards or attaches to anything -- it just stays in play, in the player's own
// right-side sideboard alongside Rings and reminders (a visual grouping only; Rings themselves still
// use Zone.Home internally), until something removes it. A player may control more than one Celestial
// only in passing -- entering play immediately discards every OTHER Celestial that same player
// controls, so at most one survives at rest.
public enum Zone { Stronghold, Home, Province, DynastyDeck, FateDeck, Hand, DynastyDiscard, FateDiscard, Dead, Battlefield, Attached, RemovedFromGame, Focused, EventInPlay, Region, Celestial }

public sealed record CardDefinition(string Id, string Name, CardType Type, string Clan = "Unaligned", int GoldCost = 0,
    int GoldProduction = 0, int Force = 0, int Chi = 0, int PersonalHonor = 0, int? HonorRequirement = null,
    int ProvinceStrength = 0, int StartingHonor = 0, string Effect = "none", string Text = "",
    bool ScenarioOnly = true, string? ArtworkKey = null, string[]? Keywords = null,
    string[]? Abilities = null, int Focus = 0,
    // Overrides Clan for a Personality aligned with more than one Clan; null means "just Clan".
    string[]? Clans = null,
    // An attachment's own "Will only/not attach to X" restriction (Item/Follower/Spell alike).
    // AttachRequiresKeyword/AttachExcludesKeyword usually name a printed keyword, checked against the
    // target Personality -- "Human" is a special case (core rules Glossary: no keyword is ever
    // printed for it, so it means "does not have Nonhuman"). AttachRequiresName is the rarer
    // named-Personality restriction (e.g. "Attaches to Yotsu Ueda").
    string? AttachRequiresKeyword = null, string? AttachExcludesKeyword = null, string? AttachRequiresName = null,
    // "This Personality has X" (Commander's Steed's shape): an attachment that grants its wielder a
    // keyword for as long as it stays attached -- reuses the existing GrantedKeyword("Attachment")
    // lifecycle (already cleaned up on detach/destroy; see DestroyAttachment), just never exercised by
    // a published card until now.
    string? AttachGrantsKeyword = null,
    // Seppun Heavy Elite's shape: grants its wielder the EFFECT Elite has (Force still counts while
    // bowed, see BattleForce's EliteEffect() check) without actually granting the Elite keyword
    // itself -- a real keyword grant would satisfy any future ability that checks specifically for
    // the printed Elite keyword (the same printed-vs-granted distinction Justice of the Crane's own
    // text draws for Magistrate). Deliberately not folded into AttachGrantsKeyword/GrantedKeywords.
    bool AttachGrantsEliteEffect = false,
    // Passive Ranged/Melee Attack-strength modifier applied to an incoming Attack TARGETING the
    // scoped card(s) -- Destrier's shape ("Melee and Ranged Attacks targeting this Follower have
    // -3 strength"), generalized. AttackDefenseKind is "Melee", "Ranged", or "Both" (null: grants
    // none); AttackDefenseAmount is signed (negative protects, matching every printed card seen so
    // far, but nothing here assumes it can't be positive, e.g. a card that makes attacks easier to
    // land against it). AttackDefenseScope is "Self" (only this card itself, Destrier's own shape),
    // "Wielder" (an Item's default when its text does not say "unit" -- conveys to the Personality
    // wielding it, not the Item itself), or "Unit" (every card in the unit -- Singh Remnants'/
    // Elephant Cavalry's shape). Which scope applies is decided per card when it is wired, from its
    // own printed text -- the engine does not infer it from Type.
    string? AttackDefenseKind = null, int AttackDefenseAmount = 0, string AttackDefenseScope = "Self",
    // Mandatory "after this card enters play" text with NO printed "Reaction:" prefix (core rules
    // convention, issue #210) -- e.g. every Spider Clan "Lose X Honor" card: applied automatically,
    // never offered as a choice, unlike an actual Reaction (which IS optional and must be offered).
    // Checked unconditionally in OpenEntryReactionWindow, the same way Destined's own mandatory draw
    // already is -- a data field rather than an ability id, since there is no decision to route
    // through UseAbility at all.
    int? EntryHonorLoss = null,
    // Mirumoto Yozo, Experienced 2's own second trait: "after your Iaijutsu action resolves, give
    // any Personality who performed it +2F" -- a passive, ongoing check (issue #207's own Dragon
    // Clan review follow-up), separate from her entry trait (discard-other-sensei), which already
    // occupies the single-valued Effect field above.
    bool GrantsIaijutsuBonus = false,
    // Mirumoto Hikaro's own second trait: "before you focus in a duel involving your Dragon Clan
    // Personality, once per duel, you may focus from your discard pile instead (revealed to the
    // opponent immediately; removed from the game after the duel ends)" -- same reasoning as above,
    // separate from her own entry trait.
    bool GrantsDiscardPileFocus = false,
    // Kitsu Akai's own Limited action (issue #257): this definition is minted at runtime by a card
    // effect (MatchEngine.CreatePersonality), never drawn from a deck or a Province -- the Lion
    // Ancestor's own printed text, "May not be included in decks." DeckWorkshop.Review flags a
    // CreatedOnly card if one somehow ends up selected anyway (the same soft-warning convention
    // every other deck-legality rule in this engine already uses, never a hard block). Also the
    // signal MatchEngine.DestroyUnits reads to redirect a destroyed created card to
    // Zone.RemovedFromGame instead of an ordinary discard pile once destruction otherwise finishes,
    // since a created card has no physical printed card to return to any pile.
    bool CreatedOnly = false,
    // Akodo Chiyo ("Crab Clan players may ignore Chiyo's Honor Requirement") and Kitsu Ririko
    // ("Ririko enters play ignoring Honor Requirement if you are a Unicorn Clan player") -- one shared
    // controller-clan-conditional waiver (issue #299, Batch E): when the player recruiting this
    // Personality belongs to the named Clan (PlayerState.Clan, the same value every existing
    // Clan-conditional passive -- crane-clan-discount, mantis-clan-discount, attacking-vs-dragon-bonus --
    // already compares against), MatchEngine.QuotePurchase skips its Family Honor >= Honor Requirement
    // check entirely, with no Clan-allegiance surcharge (that is a different, separate waiver route).
    // A data field rather than an Effect string, since Effect is single-valued and each of these cards
    // has its own separate reason to occupy it (or, like Ririko, none at all).
    string? HonorRequirementWaiverClan = null,
    // "You may include up to N copies of this card in your deck" (Fading Shadows 10; issue #337): the deck-construction copy
    // limit this card prints in place of the usual 3 (1 for a Unique card). Read only by DeckWorkshop's soft legality note;
    // null is every card without such a line. It is a per-card limit, so it never changes what any other card is held to.
    int? DeckCopyLimit = null);

/// <summary>One applied Force/Chi change, in application order, so a future card can negate a
/// specific prior effect instead of only ever resetting a flat running total.</summary>
public sealed record AppliedModifier(string Source, string Effect, string Stat, int Amount);

/// <summary>One negation a "before an action resolves" reaction has set on a card for the deferred command:
/// Kind is "destroy", "move", "straighten", "bow" (each consumed by its first match) or "all" (Champion of
/// Thunder: every effect the action applies to that card, live for the whole re-invocation).</summary>
public sealed record NegatedEffect(string Kind, string CardId);

/// <summary>A keyword granted by another card rather than printed. Duration is "Attachment"
/// (lasts until that attachment moves or is destroyed), "Turn" (cleared at turn end, the default
/// for an action-granted keyword), or "Permanent" (only when the granting text says so).</summary>
public sealed record GrantedKeyword(string Keyword, string Source, string Duration, string? CopiedFrom = null, string? CopiedFromName = null);

/// <summary>An ability a card has because another card's effect gave it (as opposed to one printed on it), the ability twin of
/// GrantedKeyword. Duration is "Permanent" (only when the granting text says so; it survives the card dying and returning),
/// "Turn" (cleared at turn end) or "Copy" (a copied ability, kept until the copying ability is used again -- Source is that
/// copying ability's id; CopiedFrom/CopiedFromName say which card it was copied from). See CardCopy.cs.</summary>
public sealed record GrantedAbility(string AbilityId, string Source, string Duration, string? CopiedFrom = null, string? CopiedFromName = null);

/// <summary>"Protection" (issue #212: Tamori Takeshi's "Effects of actions the target performs
/// will not be negated") -- while any Protection sits in a card's own list, no card of the
/// opposing kind (Effortless Counterattack's own Additional-Action negation, Attack-strength
/// negation) may negate an action performed by it. Same Duration convention as GrantedKeyword
/// ("Turn" is the default -- core rules: an effect lasts until the end of the active player's own
/// turn unless its own text says otherwise, e.g. "Permanent" or "until this battle ends"); cleared
/// the same place ("Turn") FinishTurn already clears GrantedKeywords -- regardless of the card's
/// own Zone, matching a Permanent GrantedKeyword's own survival through death and return.</summary>
public sealed record Protection(string Source, string Duration);

/// <summary>A physical marker on a card, not a card itself (core rules Glossary — Token). Type
/// identifies the kind of token (e.g. "Fire", "Madness") for other effects to count/reference; it
/// does not grant its host a keyword. Stat/Amount, when set, is a persistent bonus/penalty carried
/// by this specific token -- unlike a Modifier, it is not cleared at turn end, only when the token
/// itself is destroyed or its host card leaves play.</summary>
public sealed record CardToken(string Id, string Type, string Source, string? Stat = null, int Amount = 0);

public sealed class CardInstance
{
    public string Id { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public int OwnerId { get; set; }
    public Zone Zone { get; set; }
    public int? ProvinceIndex { get; set; }
    public bool FaceUp { get; set; }
    public bool Bowed { get; set; }
    public string? AttachedTo { get; set; }
    public List<AppliedModifier> Modifiers { get; set; } = [];
    public List<GrantedKeyword> GrantedKeywords { get; set; } = [];
    // Abilities gained from another card's effect (Fukuzo's copied ability; a permanent grant). Usable exactly like a printed
    // ability -- MatchEngine.AbilityIdsOf is the one place that adds them to Definition.Abilities.
    public List<GrantedAbility> GrantedAbilities { get; set; } = [];
    // Keywords removed from this specific card by permanent or ongoing effects. Overlay transfers
    // these removals just like granted keywords, so the replacement Personality retains its state.
    public List<string> RemovedKeywords { get; set; } = [];
    // Clan affiliations removed from this specific Personality by card effects. When every printed
    // affiliation is removed, the Personality is Unaligned for all rules checks.
    public List<string> RemovedClanAffiliations { get; set; } = [];
    public List<Protection> Protections { get; set; } = [];
    public List<CardToken> Tokens { get; set; } = [];
    public bool Dishonorable { get; set; }
    public int StraightenBlockedTurn { get; set; } = -1;
    // Tamori Tamoko's own Open action (issue #207 follow-up): "assigned to attack ... this turn" --
    // set once, in Assign(), the turn this card is assigned during Infantry/Cavalry Maneuvers.
    public int AssignedToAttackTurn { get; set; } = -1;
    public Dictionary<string, int> AbilityUses { get; set; } = [];
    // "Until your second turn from now begins" (issue #259 -- Rebuilding Kyuden Hida/Times of
    // Strife/War Weariness, the first Events with a bounded ongoing duration; Suspicions' own
    // "until the game ends" needs no expiry at all). Stamped with the OWNING player's own
    // PlayerState.TurnsTaken value, +2, when a qualifying Event enters Zone.EventInPlay -- a
    // per-player turn count, not the shared MatchState.TurnNumber (which advances once for EITHER
    // player's turn, so "your second turn from now" is +4 raw turns in this two-player alternating
    // game, not +2 -- TurnsTaken already counts only this player's own turns, avoiding that trap).
    // StartTurn discards the card once the owner's own TurnsTaken reaches it. Null for an Event with
    // no bounded duration (Suspicions, Formal Apology) or any non-Event card.
    public int? ExpiresAtTurnsTaken { get; set; }
}

public sealed class ProvinceState
{
    public int Index { get; set; }
    public int Strength { get; set; }
    // Times of Strife (issue #259): "all Provinces have a maximum strength equal to their base
    // strength" -- a clamp CEILING, distinct from Strength itself (which Ring of Earth/a Dueling win
    // bonus/etc. already mutate directly, in place). Set once at match setup, identically to Strength
    // there, and never touched again by anything -- the one stable value Times of Strife's own clamp
    // (EffectiveProvinceStrength) has to cap back down to.
    public int PrintedStrength { get; set; }
    public bool Destroyed { get; set; }
    public string? CardId { get; set; }
    // Reconnaissance (core rules Glossary): a per-player boolean at this specific province, indexed
    // by player id, defaulted to false. No ability removes it directly; it resets to false for both
    // players only once a battle at this province resolves (ResolveBattle), regardless of outcome.
    public bool[] Reconnaissance { get; set; } = new bool[2];
    // The Region currently attached to this province, if any (at most one -- ResolveRegion discards a
    // second Region revealed here rather than replacing the first). Mirrors CardId's own role for the
    // face-down/face-up Dynasty card, but for the Zone.Region card instead.
    public string? RegionId { get; set; }
}

public sealed class PlayerState
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Clan { get; set; } = "";
    public int Honor { get; set; }
    public int TurnsTaken { get; set; }
    public int ProclaimUsedTurn { get; set; } = -1;
    public bool DishonorLossPending { get; set; }
    // Utaku Chikako's own Limited ability (issue #186): "You may not declare an attack this turn" --
    // a broad restriction (not just her own printed card's interaction), so it blocks DeclareAttack
    // outright regardless of what grants the assignment opportunity. Reset every StartTurn.
    public bool MayNotAttackThisTurn { get; set; }
    // Ring of Air's own recruitment condition (issue #194): "after you resolve two or more Favor
    // actions in one turn." Incremented at each Favor-consuming ability's own execution case
    // (shunya-honor, favor-cycle, favor-battle); reset every StartTurn.
    public int FavorActionsThisTurn { get; set; }
    // Ring of Fire's own recruitment condition (issue #194, the "Ring of Flame" printing): "after you
    // resolve your second or later action in one turn which destroyed a Personality you did not
    // control." Incremented once per qualifying ResolveBattle destruction (a simplification: only
    // battle-driven destructions are counted, not every possible destroy path -- documented in
    // ROADMAP.md). Reset every StartTurn.
    public int DestroyedEnemyPersonalityActionsThisTurn { get; set; }
    // Ring of Water's own recruitment condition (issue #194): "a battle... in which you played a
    // Terrain, destroyed a Terrain, and destroyed any cards or provinces during resolution." Both
    // flags are battle-scoped (like NoNewAdditionalActionsThisBattle): set when this player's own
    // Terrain-play/Terrain-destroy actually happens, cleared at FinishBattleResolution.
    public bool TerrainPlayedThisBattle { get; set; }
    public bool TerrainDestroyedThisBattle { get; set; }
    public Dictionary<string, int> AbilityUses { get; set; } = [];
    public string StrongholdId { get; set; } = "";
    public List<string> DynastyDeck { get; set; } = [];
    public List<string> FateDeck { get; set; } = [];
    public List<string> Hand { get; set; } = [];
    public List<ProvinceState> Provinces { get; set; } = [];
}

public sealed record PendingChoice(string Type, int PlayerId, string? CardId, string Prompt, int? ActingPlayerId = null);
public sealed record GameEvent(long Sequence, string Type, string Message, int? PlayerId = null, string? CardId = null, int? Amount = null);

/// <summary>A Ranged/Melee Attack awaiting resolution, open to reaction (core rules: "players must
/// have an opportunity to react"). Strength is deliberately mutable state, not a one-shot value --
/// either player's Reaction may raise, lower, or (by driving it to 0 or below) negate it before
/// Kind/Strength are compared against the target's current Force at actual resolution time.</summary>
public sealed class PendingAttack
{
    public string AbilityId { get; set; } = "";
    public int PlayerId { get; set; }
    // The Personality that made the attack (an attached Item/Weapon's own wielder, when the attack
    // came from one of those rather than a Personality directly) -- populated for every Attack, not
    // just the two cards that originally needed it for a card-specific post-attack effect (Tsuruchi
    // Isas' "Move Isas home"; False Ring of Water's own discard-from-hand case). Also the performer
    // Protection (issue #212) checks before letting a Reaction negate this Attack's own strength.
    public string? SourceId { get; set; }
    public string? TargetId { get; set; }
    public int Strength { get; set; }
    public string Kind { get; set; } = "";
    // Was this specific Attack itself taken as a consumed Additional Action grant (issue #181)? If so,
    // ResolveAttack's own NextAction call must know to keep priority with the attacker (grants
    // permitting), the same as any other Battle ability resolved as an additional action -- the
    // immediate (non-deferred) resolution path already gets this for free via ExecuteAbilityEffects'
    // own tail, so only the deferred/reaction-window path needs this carried across.
    public bool Additional { get; set; }
}

/// <summary>Conqueror (core rules Glossary): open only when the attacker won and more than one of
/// their attacking Personalities carries the Conqueror keyword -- a real choice for the attacker.
/// (Exactly one candidate resolves immediately, with no pending state, since there is nothing to
/// choose.) ProvinceIndex is enough to recompute the untouched attacking army once the choice lands;
/// nothing about it can change while this is the only legal next command (see ResolveConqueror).</summary>
public sealed class PendingConqueror
{
    public int PlayerId { get; set; }
    public int ProvinceIndex { get; set; }
    public string[] CandidateIds { get; set; } = [];
}

/// <summary>Before This Battle Resolves (issue #272, Encircled Terrain's own printed timing): "occur
/// after all players have passed but before resolution is determined... If there are multiple
/// 'before this battle resolves' effects in play at a battlefield, the player whose turn it is
/// chooses the order." Opened by ResolveBattle itself, before Army()/ArmyForces are ever computed --
/// deliberately BEFORE the battle's own participants are read, since Encircled Terrain's own effect
/// (sending every other Personality home) changes who actually fights. RemainingSourceIds holds every
/// not-yet-resolved source's own card id, still to be ordered by the active player (via the
/// "beforeBattleOrder" PendingChoice) whenever more than one remains; ActiveSourceId is whichever one
/// is currently resolving. The Encircled* fields are that one source's own sub-stage -- a future
/// second "before this battle resolves" card with a different shape gets its own such fields, never
/// reusing these by coincidence of type.</summary>
public sealed class PendingBeforeBattleResolve
{
    public int ProvinceIndex { get; set; }
    public int Attacker { get; set; }
    public int Defender { get; set; }
    public List<string> RemainingSourceIds { get; set; } = [];
    public string? ActiveSourceId { get; set; }
    // Encircled Terrain's own sub-stage while ActiveSourceId is its id: "ChooseOrder" (its own
    // controller picks who selects first) -> "FirstSelect" -> "SecondSelect" (each side chooses one
    // Personality of their own at this battlefield to keep) -> resolved (every other Personality here
    // sent home).
    public string? EncircledStage { get; set; }
    public string? EncircledFirstSelectedId { get; set; }
}

/// <summary>One claim on "the first opportunity to take a Battle action or pass" in a battle (Battle Phase
/// spec 4.3/4.4; issue #310). PlayerId is the BENEFICIARY -- who the card says gets the opportunity, not
/// necessarily who played it (Sneak Attack names "the Attacker" even if the Defender were to play it).
/// Kind is "Naval" (the Naval Invasion Reaction) or "Engage" (any other grant, e.g. Daidoji Yuki);
/// SourceId is the card that made the claim, when there is one.</summary>
public sealed record FirstOpportunityClaim(int PlayerId, string Kind, string? SourceId);

/// <summary>The Engage window (Battle Phase spec 4.2; issue #310): open from the moment the Attacker selects
/// a battlefield until both players pass in succession, while "Engage:" actions and "After engaging"
/// Reactions are played. Claims accumulate here in the order made and are resolved ONCE, when the window
/// closes (MatchEngine.ResolveFirstOpportunity) -- the Defender's claim always wins whatever the order,
/// otherwise the Attacker's, and any number of claims by one player are the same single opportunity.
/// NavalInvasionUsed enforces the printed "once per battle"; NavalPerformerId is the Personality that
/// Reaction's "which he must perform" binds the Attacker's first action to.</summary>
public sealed class PendingEngage
{
    public int ProvinceIndex { get; set; }
    public int Attacker { get; set; }
    public int Defender { get; set; }
    public List<FirstOpportunityClaim> Claims { get; set; } = [];
    public bool NavalInvasionUsed { get; set; }
    public string? NavalPerformerId { get; set; }
}

/// <summary>A limit on the FIRST Battle action a player takes in a battle, set when the Engage window closes
/// and consumed by that player's next accepted command: Naval Invasion's "which he must perform"
/// (PerformerId), or Rebuilding Kyuden Hida's "the Attacker may not announce a Battle action before the
/// Defender's first normal opportunity to act or pass" (MustPass).</summary>
public sealed record FirstActionRestriction(int PlayerId, string? PerformerId, bool MustPass);

/// <summary>An Additional Action grant (core rules Glossary; issue #181): one Battle action the
/// named player may take out of the normal back-and-forth alternation, without ceding priority to
/// their opponent in between. ScopeCardIds null means any of the player's own cards (Again!'s
/// shape); a specific set restricts it to those cards only (Two Hands' "from cards in his unit").
/// Consumed one at a time by NextAction/ConsumeAdditionalActionGrant; battle-scoped -- cleared at
/// FinishBattleResolution, never carried into a later battle or turn. FromDiscard: the one action is the play of the
/// single scoped card FROM A DISCARD PILE (Daigotsu Bofana - exp: "take an additional action to play it ... After it
/// resolves, remove the card from the game"); it makes that card's Battle actions available, and consumed, like any
/// grant.</summary>
public sealed record AdditionalActionGrant(int PlayerId, string[]? ScopeCardIds, bool FromDiscard = false);

/// <summary>A discard a player owes: they choose the card from their hand (Times of Strife, Daigotsu Bofana - exp's entry).
/// Reason names the card or rule in the prompt and the event.</summary>
public sealed record ForcedDiscard(int PlayerId, string Reason);

/// <summary>Force redirection (issue #216, Togashi Noboru): TargetId's own Force (not its unit's
/// Followers) contributes to ToPlayerId's own army total instead of the target's own controller's,
/// while ToPlayerId still has at least one unit in their own current army -- re-checked live at
/// resolution, not a one-time snapshot.</summary>
public sealed record ForceRedirect(string TargetId, int ToPlayerId);

/// <summary>Tamori Tamoko's own Open action (issue #207 follow-up): "gain 1 Honor if [TargetId]
/// was not assigned to attack your provinces this turn" -- BeneficiaryPlayerId is Tamoko's own
/// controller (the one who gains the Honor), not the targeted Personality's own controller.</summary>
public sealed record TamokoHonorCheck(string TargetId, int BeneficiaryPlayerId);

/// <summary>Lady Matsu's Rage (the first Celestial): "Bow the target. After the next time (this
/// turn) it straightens, destroy it." Checked reactively wherever Straighten() actually flips
/// Bowed to false, not polled -- consumed (removed) the instant it fires, or discarded unconsumed at
/// EndTurn if the target never straightens this same turn. ActingPlayerId is whoever performed the
/// original Battle action (Lady Matsu's Rage's own controller at the time), carried through to the
/// delayed destroy so her own destroy-redirect trait still attributes correctly even though she has
/// already been discarded as this ability's own cost by the time it fires.</summary>
public sealed record LadyMatsusRageWatch(string TargetId, int ActingPlayerId);

/// <summary>Yasuki Tsujiken (oracle-l5r-9469): one mark placed by his own "After Tsujiken enters
/// play, target a Holding" Reaction. See MatchState.TsujikenWatches for the full shape/lifecycle.</summary>
public sealed record TsujikenWatch(string TsujikenId, string HoldingId);

/// <summary>Fields of Mercy (oracle-l5r-2530): the per-player destroyed-Personality counter for one
/// specific in-play copy of the card. See MatchState.FieldsOfMercyWatches for the full lifecycle.</summary>
public sealed class FieldsOfMercyWatch
{
    public string CardId { get; set; } = "";
    public int[] DestroyedCount { get; set; } = new int[2];
}

/// <summary>Dueling (core rules; issue #204): a mini-game that pauses whatever the outer game was
/// doing (a duel-issuing ability's own execution sets this up and returns without ceding priority,
/// same shape as PendingAttack) until it resolves, then hands priority back exactly where the
/// pausing ability's own NextAction call would have. Stage drives which commands are legal next:
/// "AwaitingAccept" (Refusable only) -> "Reaction" (a real PendingChoice("duelReaction",...) window,
/// same machinery as attackReaction) -> "Focusing" (challenged goes first; either side may Strike,
/// ending it for both; a player who has focused four times must Strike on their turn) ->
/// "DuelistRefocus" (each Duelist-controlling side, in DuelistRefocusOrder, may discard one focused
/// card and focus once more) -> "OrderingEffects" (the OUTER game's ActivePlayerId orders any
/// Focus-Effect-bearing focused cards) -> resolved (PendingDuel set back to null).</summary>
public sealed class PendingDuel
{
    public string AbilityId { get; set; } = "";
    // The player who issued the challenge -- NextAction is called with this once the duel fully
    // resolves, so priority returns exactly where it would have without the interjecting duel.
    public int ChallengerPlayerId { get; set; }
    public int ChallengedPlayerId { get; set; }
    public string ChallengerId { get; set; } = "";
    public string ChallengedId { get; set; } = "";
    // "Chi" (default), "Force", or "PersonalHonor" -- the ability's own printed duel stat, or Chi if
    // none is named.
    public string Stat { get; set; } = "Chi";
    public bool Refusable { get; set; }
    public string Stage { get; set; } = "AwaitingAccept";
    // Whose turn to Focus or Strike, alternating, starting with ChallengedPlayerId.
    public int TurnPlayerId { get; set; }
    public bool Struck { get; set; }
    public List<string> ChallengerFocused { get; set; } = [];
    public List<string> ChallengedFocused { get; set; } = [];
    // Player ids still owed a Duelist discard-and-refocus decision, in the order they decide (the
    // OUTER game's ActivePlayerId first if both sides have Duelist) -- popped one at a time.
    public List<int> PendingDuelistDecisions { get; set; } = [];
    // The active player's own chosen resolution order for every focused card carrying a
    // Timing=="FocusEffect" ability, set once (by ResolveDuelFocusEffectsInOrder) just before
    // ResolveDuel actually applies them.
    public List<string> OrderedFocusEffects { get; set; } = [];
    // Mirumoto Hikaro's own second trait: "once per duel, you may focus from your discard pile
    // instead" -- per player (either side could independently control their own Hikaro), and which
    // already-focused card ids came from a discard pile this way (ResolveDuel removes those from
    // the game instead of the ordinary end-of-duel FateDiscard sweep every other focused card gets).
    public bool[] HikaroDiscardFocusUsed { get; set; } = new bool[2];
    public List<string> DiscardFocusedIds { get; set; } = [];
}

public sealed class MatchState
{
    public long Version { get; set; }
    public int TokenSequence { get; set; }
    // Kitsu Akai's own Limited action (issue #257): a persistent sequence for minting fresh
    // CardInstance.Id values at runtime, the same replay-determinism reasoning as TokenSequence
    // above -- match setup's own local `nextId` closure only ever runs once, at Create(), and does
    // not survive into gameplay.
    public int CreatedCardSequence { get; set; }
    public int? FavorPlayerId { get; set; }
    public int TurnNumber { get; set; } = 1;
    public int ActivePlayerId { get; set; }
    public int PriorityPlayerId { get; set; }
    public Phase Phase { get; set; }
    public int ConsecutivePasses { get; set; }
    public PendingChoice? PendingChoice { get; set; }
    public PendingAttack? PendingAttack { get; set; }
    public PendingConqueror? PendingConqueror { get; set; }
    public PendingDuel? PendingDuel { get; set; }
    // Deck search (issue #312): see DeckSearch.cs. Non-null exactly while one of the "search*" PendingChoice
    // types is open; it holds the engine-private context (which deck, which card was picked) that the
    // player's own MatchView.PendingSearch is derived from, and is never sent as-is to anyone.
    public PendingSearch? PendingSearch { get; set; }
    // Look at the top N cards (issue #322): see DeckLook.cs. Non-null exactly while a "look*" PendingChoice is open; the
    // player's own MatchView.PendingLook is derived from it and nobody else sees anything of it.
    public PendingLook? PendingLook { get; set; }
    // Conditional effect choice (issue #337): see EffectChoice.cs. Non-null exactly while a "effectChoice" PendingChoice is open; it holds the
    // resolving action (source, target, performer, the negations a reaction set) the chosen option's effects are then applied to.
    public PendingEffectChoice? PendingEffectChoice { get; set; }
    // Deck search (issue #312): the append-only, PUBLIC record of every card a player has shown the other
    // player (a search shows what it found). Exposed to both players as MatchView.ShownCards.
    public List<ShownCard> ShownCards { get; set; } = [];
    // Deck search (issue #312): the persisted shuffle generator (SplitMix64, see MatchEngine.StableRandom).
    // Setup continues the very sequence the seed started (so every existing setup is byte-for-byte
    // unchanged), and every mid-game Shuffle reads and advances this one value. It is engine-private: it
    // is part of MatchState only so State/Copy()/Execute rollback carry it, and it appears in no MatchView.
    public ulong RngState { get; set; }
    // Events Phase reveal cursor: the index of the next province ContinueEventsReveal should reveal. A
    // reveal that defers to a player decision (Harsh Choices, Imperial Gift's search) resumes from here,
    // so the card that province refilled with is not revealed in the same phase it was placed.
    public int EventsRevealNext { get; set; }
    public int? CurrentBattleProvince { get; set; }
    public List<int> UnresolvedBattlefields { get; set; } = [];
    // Before This Battle Resolves (issue #272): see PendingBeforeBattleResolve's own doc comment.
    // Non-null only while ResolveBattle's own before-resolution sequence is still being sorted out
    // (ordering choices, Encircled Terrain's own selection sub-stages); cleared before the battle's
    // actual winner/casualties are ever computed.
    public PendingBeforeBattleResolve? PendingBeforeBattleResolve { get; set; }
    // Battle Phase timing (issue #310). PendingEngage is non-null exactly while the Engage window is open
    // (PendingChoice "engageReaction"). FirstActionPlayerId/FirstActionReason record WHO holds the first
    // opportunity in the current battle and WHY ("Default", "Naval", "EngageGrant" or "DefenderOverride"),
    // set the instant the battle starts (Default) and again when the Engage window resolves its claims;
    // cleared when the battle resolves. FirstActionRestriction is the one-shot limit on the holder's first
    // action (see FirstActionRestriction). All three are plain data so a replay reproduces them exactly.
    public PendingEngage? PendingEngage { get; set; }
    public int? FirstActionPlayerId { get; set; }
    public string? FirstActionReason { get; set; }
    public FirstActionRestriction? FirstActionRestriction { get; set; }
    // After This Battle Resolves (issue #272, Looting the Ruins' own printed timing): "after the
    // resolution of the current battle but before the next battlefield is chosen." FinishBattleResolution
    // sets this to the province that just resolved (its own provinceIndex parameter, otherwise lost --
    // CurrentBattleProvince is already cleared by the time this hook fires, and the Overconfident
    // draw window may defer the whole tail across a command boundary first, same reasoning
    // PendingConqueror.ProvinceIndex already documents). FinishBattleReturnHomeAndTransition reads and
    // clears it once its own after-resolve effects have run.
    public int? ResolvedBattleProvinceIndex { get; set; }
    // Your Clan Needs You! (oracle-l5r-9687, issue #295): "After the battle ends, if he is in play,
    // discard all his attachments and put him into one of your provinces, face-up, discarding any
    // card there" -- the recruited Personality's own id, watched from the moment he enters play via
    // this ability until this same battle's own resolution checks it
    // (MatchEngine.CheckYourClanNeedsYouReturn), then removed from this list regardless of outcome.
    // A List (not a single nullable id), since more than one copy could resolve within one battle.
    public List<string> YourClanNeedsYouWatchIds { get; set; } = [];
    // Ashigaru Elite's shape: a per-player counter of the current battle's Force boost to each of the
    // performer's own Ashigaru-keyword Personalities/Followers at this battlefield, incremented once
    // per use (stacks, tireless). Reset to zero whenever a battle resolves (FinishBattleResolution) --
    // "for the remainder of the battle," not the turn -- since a turn can contain more than one battle.
    public int[] AshigaruBoost { get; set; } = new int[2];
    // Village Guardian (oracle-l5r-9162, issue #285): "Draw one card for each player who has had a
    // province destroyed this game" -- a game-wide, per-player, match-lifetime flag, set once and
    // never cleared (unlike AshigaruBoost above, which resets every battle). Indexed by player id,
    // the same shape as AshigaruBoost/DestroyedCount, set at DestroyProvince's own single choke
    // point. Deliberately NOT FieldsOfMercyWatches' shape (a List of per-Terrain-instance, per-player
    // Personality-destruction counters) -- this is a plain per-player boolean, scoped to the whole
    // match rather than any one card instance, and to Province destruction rather than Personality
    // destruction.
    public bool[] ProvinceEverDestroyed { get; set; } = new bool[2];
    // Ohsuki's Sparrow (oracle-l5r-10023, issue #299): "If none of your provinces were destroyed since
    // your last turn ended" -- unlike ProvinceEverDestroyed above (match-lifetime, never cleared), this
    // one is per-player and WINDOWED: set at the same single DestroyProvince choke point, but cleared
    // for a player at the end of THAT player's own turn (FinishTurn), so it reads true exactly when one
    // of their provinces was destroyed at any point since their previous turn ended -- most commonly
    // during the opponent's turn, but also by anything that destroys it during their own. Reset at
    // turn END, not StartTurn: clearing at the start of a turn would erase a destruction that happened
    // on the opponent's turn just before it, which is precisely the case the printed text asks about.
    // Never true before any province has been destroyed, so a fresh game starts with it usable.
    public bool[] ProvinceDestroyedSinceTurnEnd { get; set; } = new bool[2];
    // Additional Action (issue #181): every currently-available grant across both players, and a
    // command snapshot awaiting the "additionalActionReaction" window (Effortless Counterattack's
    // shape) before it actually resolves. Two Hands' own restriction ("other actions will not give
    // you NEW additional Battle actions this battle") is a separate flag -- it blocks future GRANTS,
    // it does not touch grants already sitting in AdditionalActions. All battle-scoped: cleared at
    // FinishBattleResolution, never carried into a later battle or turn.
    public List<AdditionalActionGrant> AdditionalActions { get; set; } = [];
    public GameCommand? PendingAdditionalAction { get; set; }
    public bool NoNewAdditionalActionsThisBattle { get; set; }
    // Attack-vs-Followers exceptions granted by another card (AttackFollowerException shape (e), Yoritomo
    // Ebumi): standing, one-shot permissions for a player's NEXT Attack action of the covered kind. Battle-
    // scoped like AdditionalActions -- consumed when that Attack executes, cleared at FinishBattleResolution.
    public List<AttackTargetGrant> AttackTargetGrants { get; set; } = [];
    // "Before an action resolves" reactions (Mirumoto Rokai's destruction-negation, Tamori Tamoko's
    // movement-or-straightening-negation, Champion of Thunder, Brothers of Goemon, Akodo Kakihara's Force
    // bonus): the whole triggering command is stashed here (never partially applied) while an alternating
    // "beforeActionReaction" window lets EITHER player use any number of applicable reactions -- the active
    // player first, then the other, alternating until both pass consecutively (issue #318) -- before
    // re-invoking ExecuteAbilityEffects on the SAME command. NegatedEffects is the negation signal those
    // reactions set: one entry per negation played, so several of different kinds compose on one command
    // (Champion's "all" on one card, Rokai's "destroy" on another, Brothers' "bow"). An entry is consumed --
    // read then removed -- by whichever of MoveHome/Straighten/DestroyUnits/StalwartNegatesBow actually runs
    // first and matches it; "all" is never consumed (see NegatesEffect), and the whole list is wiped right
    // after the re-invoked command returns.
    public GameCommand? PendingNegatableAbility { get; set; }
    public List<NegatedEffect> NegatedEffects { get; set; } = [];
    // Force redirection (issue #216, Togashi Noboru): "the target's Force contributes to [Noboru's
    // controller's] army instead of his own controller's" -- battle-scoped ("for the duration of the
    // battle," core rules' own default for an in-battle condition), so cleared at
    // FinishBattleResolution like AdditionalActions above, never carried into a later battle. Live
    // re-checked at ACTUAL resolution time (ResolveBattle), not baked in when granted -- "while you
    // have at least one unit in your current army" is a continuously-checked condition, not a
    // snapshot: only the target Personality's own Force moves (never its Followers', which always
    // stay with their own controller, since a Follower's owner is always its host's own owner).
    public List<ForceRedirect> ForceRedirects { get; set; } = [];
    // Brash (core rules keyword, issue #216): "the Defender may draw a card after a Brash card is
    // assigned to attack" -- once independently per Brash card in ONE attack declaration, so this is
    // a plain countdown (every offer is identical: draw or don't), not per-card state. Non-zero only
    // while PendingChoice.Type=="brashDraw" is open; the deferred phase transition (which of
    // DefenseAssignment/CavalryDefense DeclareAttack would otherwise have moved to) resumes once it
    // reaches zero -- read off _state.Phase itself (still AttackDeclaration/CavalryDeclaration until
    // then), so no separate field is needed to remember it.
    public int PendingBrashDraws { get; set; }
    // Bow reaction (issue #216, Togashi Hizumi): while PendingChoice.Type=="bowReaction" is open on
    // Hizumi's own behalf, the specific newly-bowed personality/personalities at his own battlefield
    // this action just bowed -- his own Target category checks membership here rather than
    // re-deriving "was this bowed by that specific action" after the fact (nothing else changes a
    // card's own Bowed status in between). Unused, and safe to leave stale, for Tamori Ruya's own
    // window (a different, unrelated Target shape -- see PersonalityAtSourceLocation).
    public List<string> BowReactionCandidateIds { get; set; } = [];
    // Overconfident (core rules keyword, issue #223): "each other player may draw a card after an
    // Overconfident card enters or leaves play" -- both triggers just queue the ONE beneficiary
    // player id here (QueueOverconfidentDraw), drained one at a time (OpenOverconfidentDrawWindow)
    // at every point the engine is about to hand priority back normally, whether that is because no
    // other reaction window ever opened, or because one that did just closed (see PassReaction).
    public List<int> PendingOverconfidentDraws { get; set; } = [];
    // Times of Strife (issue #259): "another player who controls no units at the current battlefield
    // announces a Battle action" -- a queue of discards owed, the same drain-one-at-a-time shape
    // PendingOverconfidentDraws above already uses (see OpenOverconfidentDrawWindow, which drains this queue
    // first), so it composes for free with every existing deferred-window call site instead of needing its own.
    // Generalized (issue #333) from a list of player ids to (player, reason): Daigotsu Bofana - exp's entry queues
    // the same forced discard for each other player.
    public List<ForcedDiscard> PendingForcedDiscards { get; set; } = [];
    // Daigotsu Bofana (issue #333): a forced discard opened at the moment an action is announced (a trait resolves
    // before any Reaction to the same trigger) holds the announced command here until the discard is answered, when
    // UseAbility carries on with it exactly where it stopped.
    public GameCommand? ForcedDiscardContinuation { get; set; }
    // The card whose mandatory "after this enters play" effects wait for its open Proclaim window (see ApplyPendingEntryEffects).
    public string? PendingEntryEffectsCardId { get; set; }
    // War Weariness (issue #259): "the first action each phase that one or more Personalities
    // performed" -- cleared only when Action/AttackDeclaration/Dynasty is entered (see SetPhase),
    // never by every individual Phase.Battle re-entry (once per battlefield) -- the user's own
    // clarification is that the whole Attack-Declaration-through-battle-resolution sequence counts as
    // one "battle phase" for this purpose, not each battlefield separately.
    public bool WarWearinessBowedThisPhase { get; set; }
    // Stormless Fury (issue #276 follow-up): the general "after a Battle action resolves" primitive.
    // The just-resolved Battle action's own performer, valid only while a "battleActionReaction"
    // window is open -- stormless-fury-bow's own implicit Target (no separate selection needed, since
    // there is only ever one performer to name).
    public string? BattleActionReactionPerformerId { get; set; }
    // Which Stormless-Fury-shaped card ids already used their own out-of-play "attach as Reaction"
    // half (stormless-fury-attach) against the CURRENTLY open battleActionReaction window's own
    // action -- the same card's own in-play "bow" half (stormless-fury-bow) may not also fire against
    // that same action (the card owner's own errata ruling). Overwritten (cleared) every time a fresh
    // battleActionReaction window opens for a new action, so it never leaks into a later, unrelated
    // one -- the small transient piece of state the primitive needs, nothing more.
    public List<string> BattleActionReaction1UsedCardIds { get; set; } = [];
    // Tsuruchi Yakusho (issue #276 follow-up): the general "after you announce an action" primitive.
    // The whole triggering command, stashed here (never partially applied) while an "announceReaction"
    // window offers the one eligible watching card's own controller a chance to react before
    // ExecuteAbilityEffects actually runs -- the same defer-then-reexecute shape PendingNegatableAbility
    // already uses for beforeActionReaction.
    public GameCommand? PendingAnnouncedAction { get; set; }
    // True for the remainder of the currently-resolving Ranged Attack's own resolution once Tsuruchi
    // Yakusho's Reaction fires -- checked by every existing Attack-strength-reducing Reaction (Fallow
    // Lands, Training's own negate) before it applies, and cleared unconditionally once that specific
    // Attack actually resolves (ResolveAttackNow). A single flag, not a per-attack set, is correct and
    // sufficient here: only one PendingAttack is ever open at a time in this engine.
    public bool RangedAttackStrengthReductionImmune { get; set; }
    // Tamori Tamoko's own Open action (issue #207 follow-up): "target an unbowed Personality
    // controlled by the active player: before the turn ends, gain 1 Honor if he was not assigned to
    // attack your provinces this turn" -- a delayed, mandatory (no further choice) check, queued
    // here and drained unconditionally in EndTurn, the same turn it was queued (Tamoko's own Open
    // Timing is only ever usable "if it is not your turn," so this always resolves before the SAME
    // active player's own turn actually ends).
    public List<TamokoHonorCheck> PendingTamokoHonorChecks { get; set; } = [];
    // Lady Matsu's Rage (the first Celestial): reactive, checked inside Straighten() itself -- see
    // LadyMatsusRageWatch's own doc comment. Cleared unconditionally in EndTurn (same call site as
    // PendingTamokoHonorChecks above) for whichever entries never fired this turn.
    public List<LadyMatsusRageWatch> PendingLadyMatsusRageWatches { get; set; } = [];
    // Legion of the Bat (issue #279): "after the resolution of an action that moved a target enemy
    // Infantry Personality to this card's battlefield" -- MoveToBattlefield itself (the sole "an
    // action moved a card to a battlefield" primitive; ordinary Attack/Defense assignment goes
    // through Assign() instead, and RecruitReserve is explicitly excluded per the card's own rule)
    // queues the card that just arrived here; OpenMoveReactionWindow (the same "after this action"
    // timing OpenBowReactionWindow already uses) drains it once the whole ability finishes
    // resolving. MoveReactionTargetId is then the one card actually offered by the open window,
    // read by legion-of-bat-bow's own execution (Target "none": the affected card is auto-
    // determined, not separately chosen, the same shape PendingNegatableAbility's own resolution
    // already uses).
    public string? PendingMoveReactionCardId { get; set; }
    public string? MoveReactionTargetId { get; set; }
    // Nikutai (issue #279): "Even if this card is bowed, after a Follower in this unit bows:
    // Straighten it" -- the one sibling Follower actually offered by an open "nikutaiReaction"
    // window, read by nikutai-straighten's own execution (Target "none", same auto-determined shape
    // as MoveReactionTargetId above).
    public string? NikutaiReactionTargetId { get; set; }
    // Unimpeachable Name (oracle-l5r-8989): an out-of-play (hand/discard) Reaction offered before
    // ANY action resolves -- broader than PendingNegatableAbility above (which only ever watches a
    // curated destroy/move/straighten Target on an IN-PLAY card). The whole triggering command is
    // stashed here, same defer-then-execute shape, while a "unimpeachableNameReaction" window offers
    // the opponent a chance to play it (or pass) before the command actually resolves. See
    // CardAbilities.ResolveOrDeferUnimpeachableName / AbilityMayCauseHonorLoss.
    public GameCommand? PendingOutOfPlayActionReaction { get; set; }
    // Set to the reacting player's own id for the duration of resolving PendingOutOfPlayActionReaction
    // only when Unimpeachable Name was actually played (not merely offered) -- consumed (amount
    // floored toward 0, never past it) by every negative GainHonor call for that same player while
    // active, satisfying "reduce EACH Honor loss from its effects by 3" for a command that causes more
    // than one. Cleared unconditionally once that one command finishes resolving.
    public int? HonorLossReductionPlayerId { get; set; }
    // Yasuki Tsujiken (oracle-l5r-9469): each entry is one "mark" placed by his own entering-play
    // Reaction on a specific Holding -- a list, not a dictionary, since the SAME Holding may be marked
    // more than once (by separate copies of Tsujiken; nothing printed forbids it), and each mark is
    // independently worth 1 Honor lost per future bow. A mark stays here even after its own Tsujiken
    // leaves play (removed instead at that point -- see MatchEngine's own destroy/discard paths) rather
    // than being filtered live by InPlay(), so a client can enumerate exactly which stacked marks are
    // still active without re-deriving it. TsujikenId names the specific CardInstance that placed the
    // mark (Alfredo's own future signifier can key its rendering off this -- see the decision doc).
    public List<TsujikenWatch> TsujikenWatches { get; set; } = [];
    // Fields of Mercy (oracle-l5r-2530): one entry per currently-in-play copy, created when it enters
    // play (never earlier -- "the count of destroyed personalities does not start until Fields of
    // Mercy are played") and removed when it leaves (DiscardTerrain), so a later copy entering play
    // always starts its own count fresh. DestroyedCount is indexed by player id: how many of that
    // player's own Personalities have already been fully destroyed while this specific copy has been
    // in play. See CardAbilities.FieldsOfMercyNegatesPersonalityDestruction.
    public List<FieldsOfMercyWatch> FieldsOfMercyWatches { get; set; } = [];
    // Copying keywords and abilities (issue #338, Fukuzo): non-null exactly while a "copyKeyword"/"copyAbility" PendingChoice is
    // open. Holds the snapshot of what may be copied and the steps still to answer; see CardCopy.cs. The chooser's own
    // MatchView.PendingCopy is derived from it.
    public PendingCopy? PendingCopy { get; set; }
    // The opposing player's Either/Or choice (issue #338, Yoritomo Sakuma, Unfortunate Hesitation): non-null exactly while an
    // "eitherOr" PendingChoice is open. See EitherOr.cs; MatchView.PendingEitherOr is derived from it for BOTH players.
    public PendingEitherOr? PendingEitherOr { get; set; }
    // Whole-action negation (issue #338, Yoritomo Sakuma): set on the deferred command's window while a reaction has negated the
    // action's effects; the window's close then resolves it with everything but its costs skipped (ActionNegation.cs).
    public bool DeferredActionNegated { get; set; }
    // Unfortunate Hesitation: players who "must pass at [their] next opportunity this phase to take a Battle, Limited, or Open
    // action". Each entry is consumed by that player's next pass at such an opportunity, and lapses when its phase ends.
    public List<ForcedPass> ForcedPasses { get; set; } = [];
    public int? WinnerId { get; set; }
    public string? VictoryReason { get; set; }
    public PlayerState[] Players { get; set; } = [];
    public Dictionary<string, CardInstance> Cards { get; set; } = [];
    public List<GameEvent> Events { get; set; } = [];
}

// DeckPosition ("Top"/"Bottom") is the acting player's own upfront choice of where a card goes in
// its owner's deck (e.g. Goju House Guard's own cost, issue #192) -- unlike Choice, which resolves a
// separate, later decision command, this is supplied on the initiating command itself, the same way
// TargetId or SourceIds are.
public sealed record GameCommand(string Type, int PlayerId, long ExpectedVersion, string? CardId = null,
    string[]? SourceIds = null, int? Province = null, bool ClanDiscount = false, bool ClanAllegiance = false,
    Dictionary<string, int>? Assignments = null, string[]? CardIds = null, string? TargetId = null, string? AbilityId = null, string? PerformerId = null, string? Choice = null, string? DeckPosition = null,
    // "Choose a player" (issue #210): a player, not a card, is the target -- Kitsuki Horume's own
    // shape ("Choose a player. He gains or loses 1 Honor."). Distinct from PlayerId (the ACTING
    // player submitting this command).
    int? TargetPlayerId = null,
    // Generalized Gold payment (issue #303): the amount of Gold the acting player chooses to pay for an
    // ability whose cost is a player-chosen variable ("pay zero or more Gold"). Null for every fixed or
    // derived cost (the engine's own GoldQuote decides those) and read only for a variable-cost ability,
    // where it must lie inside that quote's MinAmount..MaxAmount; null there means the minimum.
    int? GoldAmount = null,
    // Alternative cost (issue #337): which of the ability's CostOptions the acting player pays ("bow" or "destroy" the
    // performing Ancestor). Chosen up front, like the performer, because a cost is paid before anything resolves; null is
    // only accepted when the ability has a single option, and never for an ability with none.
    string? CostOption = null);
public sealed record CommandResult(bool Accepted, string? Error, long Version, GameEvent[] Events);
public sealed record PaymentOption(string[] SourceIds, int Produced, int Cost, int Excess);
// Basis (issue #303): the human-readable derivation of Cost, e.g. "Training samurai cost 5 - 2 (Clan
// discount)". Null only for quotes serialized by an older engine.
public sealed record PurchaseQuote(string CardId, int Cost, bool ClanDiscount, bool ClanAllegiance,
    bool ProclaimEligible, string? Error, PaymentOption[] Payments, string? Basis = null);
public sealed record OverlayQuote(string CardId, string TargetId, int HigherLevel, int LowerLevel, string? Error);
/// <summary>The one authoritative Gold quote (issue #303) for any action that costs Gold: recruiting,
/// equipping, playing a Strategy and every costed ability. The client renders this and never derives a
/// cost itself. Kind is "fixed" (printed on the card or fixed in an ability), "derived" (computed from a
/// chosen target: another card's cost, a unit's grouped total, a recruit quote, plus a modifier and floor)
/// or "variable" (the player chooses the amount, MinAmount..MaxAmount, sent as GameCommand.GoldAmount).
/// Cost is what the action costs now (for "variable", the default amount, i.e. MinAmount). Payments are
/// the engine's suggested and alternative source sets for Cost, best first; for "variable" quotes
/// PaymentsByAmount holds them for every legal amount (keyed by amount) so the client can change the
/// amount without a round trip. Error, when set, means this quote cannot be paid or used as it stands.</summary>
public sealed record GoldQuote(int Cost, string Kind, string Basis, string? Error, PaymentOption[] Payments,
    int? MinAmount = null, int? MaxAmount = null, Dictionary<int, PaymentOption[]>? PaymentsByAmount = null);
/// <param name="Gold">The quote for this option's own command (its first legal target when the cost is
/// target-derived). Null when the action costs no Gold.</param>
/// <param name="TargetGold">Per-target quotes (target id to quote) for a target-derived cost: the cost
/// changes when the player picks a different target, so every legal target ships its own quote and the
/// client swaps between them without a round trip. Null for fixed and variable costs.</param>
public sealed record ActionOption(string Type, string Label, bool Enabled, string? Reason,
    GameCommand? Command = null, string? CardId = null, string[]? TargetIds = null,
    int[]? Provinces = null, PurchaseQuote? Quote = null, GoldQuote? Gold = null,
    Dictionary<string, GoldQuote>? TargetGold = null);
/// <param name="UnitForce">Authoritative effective unit contribution, including qualifying Followers.
/// Null for hidden/non-unit/out-of-play cards and older payloads; never substitute a client calculation.</param>
public sealed record CardView(string Id, CardDefinition? Definition, int OwnerId, Zone Zone, bool FaceUp,
    bool Bowed, int? ProvinceIndex, string? AttachedTo, int Force, int Chi = 0, bool StraightenBlocked = false,
    AppliedModifier[]? Modifiers = null, GrantedKeyword[]? GrantedKeywords = null, bool Dishonorable = false, int? UnitForce = null,
    CardToken[]? Tokens = null,
    // Abilities this card has by another card's effect (a copied ability, a permanent grant), with their labels; null when none.
    GrantedAbilityView[]? GrantedAbilities = null,
    // Authoritative Gold this card can produce for a payment right now (a printed producer, or a card that can bow to produce Gold
    // while a cost is being paid: Yoritomo Sakuma); null when it cannot. The client shows this and never derives it.
    int? GoldProduced = null);
/// <summary>One granted ability as the client shows it: the id, the engine's label for it, and where it came from.</summary>
public sealed record GrantedAbilityView(string AbilityId, string Label, string Timing, string Source, string Duration, string? CopiedFrom, string? CopiedFromName);
// Strength is the EFFECTIVE strength (printed + any attached Region's static bonus, e.g. Fallow
// Lands) -- what a player actually needs to beat to destroy the province, not the raw mutable field.
public sealed record ProvinceView(int Index, int Strength, bool Destroyed, CardView? Card, CardView? Region);
// DeadCount (issue #221): the authoritative count of this player's own Zone.Dead Personalities --
// already present, individually, within Discard (each CardView's own Zone tells a dead card apart
// from a merely-discarded one), but computed here as a single number the same way UnitForce is, so
// a UI can show it directly and a future card can read it without re-deriving its own filter.
public sealed record PlayerView(int Id, string Name, string Clan, int Honor, int TurnsTaken,
    CardView Stronghold, CardView[] Home, CardView[] Army, ProvinceView[] Provinces,
    CardView[] Hand, int HandCount, int DynastyDeckCount, int FateDeckCount, CardView[] Discard,
    bool ProclaimUsedThisTurn, bool DishonorLossPending, CardView[] RemovedFromGame,
    int DeadCount, CardView[]? Focused = null,
    // Zone.EventInPlay (Suspicions): public, like RemovedFromGame -- no visibility gate needed.
    CardView[]? EventsInPlay = null,
    // Zone.Celestial (Lady Matsu's Rage): also public -- no visibility gate needed.
    CardView[]? Celestials = null);
/// <summary>Nothing here is hidden information -- both players already know who challenged whom, on
/// what stat, and whose turn it is; a focused card's own identity is still gated the normal way,
/// through PlayerView.Focused's own per-viewer visibility.</summary>
public sealed record DuelView(int ChallengerPlayerId, int ChallengedPlayerId, string ChallengerId,
    string ChallengedId, string Stat, string Stage, int TurnPlayerId, bool Struck);
/// <summary>The names <see cref="BattleStepView"/> speaks in, one per step of the rules lead's formal Battle
/// Phase (issue #310). A client keys its sidebar off these constants and never infers timing itself.</summary>
public static class BattleSteps
{
    // Segments (spec steps 1-5). "None" is any moment outside the Attack Phase. The spec's Declaration
    // Segment is not a separate decision in this engine: the attacker's Infantry assignment prompt IS the
    // declaration (an empty assignment declines) -- see docs/decisions/2026-09-18-battle-timing-primitives.md.
    public const string None = "None", InfantryManeuvers = "InfantryManeuvers", CavalryManeuvers = "CavalryManeuvers",
        FightBattles = "FightBattles", Resolution = "Resolution";
    // Steps. AttackerAssigns/DefenderAssigns are the Maneuvers decisions; ChooseBattle is 4.1; Engage is 4.2;
    // BattleActions is 4.5 (the first-action holder of 4.3/4.4 is in FirstActionPlayerId); BeforeResolution and
    // Aftermath are the two observable halves of Resolution (5) -- the Force comparison itself is instantaneous.
    public const string AttackerAssigns = "AttackerAssigns", DefenderAssigns = "DefenderAssigns", ChooseBattle = "ChooseBattle",
        Engage = "Engage", BattleActions = "BattleActions", BeforeResolution = "BeforeResolution", Aftermath = "Aftermath";
    // FirstActionReason values.
    public const string ReasonDefault = "Default", ReasonNaval = "Naval", ReasonEngageGrant = "EngageGrant", ReasonDefenderOverride = "DefenderOverride";
    // FirstActionLimit values.
    public const string LimitMustPass = "MustPass", LimitPerformerOnly = "PerformerOnly";
}

/// <summary>Public, read-only Battle Phase timing (issue #310): everything a battle sidebar needs, derived from
/// authoritative state on every GetView and identical for both viewers (nothing here is hidden information).
/// Read: <c>Segment</c>/<c>Step</c> (what the phase is doing), <c>DecisionPlayerId</c> (whose decision it is),
/// <c>BattlefieldProvince</c> (the selected battle), <c>FirstActionPlayerId</c>/<c>FirstActionReason</c> (who acts
/// first and why; while the Engage window is open they are provisional, <c>FirstActionResolved</c> false, and
/// <c>EngageClaims</c> lists the claims so far), <c>FirstActionLimit</c>/<c>FirstActionPerformerId</c> (a limit on
/// that first action), <c>ConsecutivePasses</c> (0 or 1; two ends the segment), <c>BeforeResolutionSourceIds</c>
/// (cards whose "before this battle resolves" trait is still to apply) and <c>Window</c> (the open PendingChoice
/// type, if any).</summary>
public sealed record BattleStepView(string Segment, string Step, int? DecisionPlayerId, int? AttackerPlayerId,
    int? BattlefieldProvince, int? FirstActionPlayerId, string? FirstActionReason, bool FirstActionResolved,
    FirstOpportunityClaim[] EngageClaims, string? FirstActionLimit, string? FirstActionPerformerId,
    int ConsecutivePasses, string[] BeforeResolutionSourceIds, string? ActiveBeforeResolutionSourceId, string? Window);
public sealed record MatchView(string ContentVersion, string Scenario, long Version, int TurnNumber,
    int ViewerId, int ActivePlayerId, int PriorityPlayerId, Phase Phase, PendingChoice? PendingChoice,
    int? CurrentBattleProvince, int[] UnresolvedBattlefields, int? WinnerId, string? VictoryReason,
    PlayerView[] Players, GameEvent[] Events, string[] Limitations, int? FavorPlayerId = null,
    DuelView? PendingDuel = null, BattleStepView? BattleStep = null,
    // Deck search (issue #312), see DeckSearch.cs. PendingSearch is PRIVATE to the searching player (the deck
    // viewer: remaining cards grouped with counts, eligibility per entry) and null in everyone else's view.
    // ShownCards is PUBLIC: the append-only record of every card either player has shown, identical in both views.
    SearchView? PendingSearch = null, ShownCardView[]? ShownCards = null,
    // Look at the top N cards (issue #322), see DeckLook.cs: PRIVATE to the viewing player (the top cards in order and what each
    // may become) and null in everyone else's view, including the deck owner's when the viewer looks at the OPPONENT's deck.
    LookView? PendingLook = null,
    // Copying keywords and abilities (issue #338, see CardCopy.cs): PRIVATE to the choosing player, null in everyone else's view.
    CopyView? PendingCopy = null,
    // The opposing player's Either/Or choice (issue #338, see EitherOr.cs): PUBLIC -- both players see what is being decided and
    // by whom; only the deciding player's view carries the payment quotes.
    EitherOrView? PendingEitherOr = null);
public sealed record ReplayRecord(string ContentVersion, string Scenario, ulong Seed, GameCommand[] Commands);
