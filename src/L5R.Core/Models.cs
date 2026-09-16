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
public enum Zone { Stronghold, Home, Province, DynastyDeck, FateDeck, Hand, DynastyDiscard, FateDiscard, Dead, Battlefield, Attached, RemovedFromGame, Focused, EventInPlay }

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
    bool GrantsDiscardPileFocus = false);

/// <summary>One applied Force/Chi change, in application order, so a future card can negate a
/// specific prior effect instead of only ever resetting a flat running total.</summary>
public sealed record AppliedModifier(string Source, string Effect, string Stat, int Amount);

/// <summary>A keyword granted by another card rather than printed. Duration is "Attachment"
/// (lasts until that attachment moves or is destroyed), "Turn" (cleared at turn end, the default
/// for an action-granted keyword), or "Permanent" (only when the granting text says so).</summary>
public sealed record GrantedKeyword(string Keyword, string Source, string Duration);

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
}

public sealed class ProvinceState
{
    public int Index { get; set; }
    public int Strength { get; set; }
    public bool Destroyed { get; set; }
    public string? CardId { get; set; }
    // Reconnaissance (core rules Glossary): a per-player boolean at this specific province, indexed
    // by player id, defaulted to false. No ability removes it directly; it resets to false for both
    // players only once a battle at this province resolves (ResolveBattle), regardless of outcome.
    public bool[] Reconnaissance { get; set; } = new bool[2];
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

/// <summary>An Additional Action grant (core rules Glossary; issue #181): one Battle action the
/// named player may take out of the normal back-and-forth alternation, without ceding priority to
/// their opponent in between. ScopeCardIds null means any of the player's own cards (Again!'s
/// shape); a specific set restricts it to those cards only (Two Hands' "from cards in his unit").
/// Consumed one at a time by NextAction/ConsumeAdditionalActionGrant; battle-scoped -- cleared at
/// FinishBattleResolution, never carried into a later battle or turn.</summary>
public sealed record AdditionalActionGrant(int PlayerId, string[]? ScopeCardIds);

/// <summary>Force redirection (issue #216, Togashi Noboru): TargetId's own Force (not its unit's
/// Followers) contributes to ToPlayerId's own army total instead of the target's own controller's,
/// while ToPlayerId still has at least one unit in their own current army -- re-checked live at
/// resolution, not a one-time snapshot.</summary>
public sealed record ForceRedirect(string TargetId, int ToPlayerId);

/// <summary>Tamori Tamoko's own Open action (issue #207 follow-up): "gain 1 Honor if [TargetId]
/// was not assigned to attack your provinces this turn" -- BeneficiaryPlayerId is Tamoko's own
/// controller (the one who gains the Honor), not the targeted Personality's own controller.</summary>
public sealed record TamokoHonorCheck(string TargetId, int BeneficiaryPlayerId);

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
    public int? CurrentBattleProvince { get; set; }
    public List<int> UnresolvedBattlefields { get; set; } = [];
    // Ashigaru Elite's shape: a per-player counter of the current battle's Force boost to each of the
    // performer's own Ashigaru-keyword Personalities/Followers at this battlefield, incremented once
    // per use (stacks, tireless). Reset to zero whenever a battle resolves (FinishBattleResolution) --
    // "for the remainder of the battle," not the turn -- since a turn can contain more than one battle.
    public int[] AshigaruBoost { get; set; } = new int[2];
    // Additional Action (issue #181): every currently-available grant across both players, and a
    // command snapshot awaiting the "additionalActionReaction" window (Effortless Counterattack's
    // shape) before it actually resolves. Two Hands' own restriction ("other actions will not give
    // you NEW additional Battle actions this battle") is a separate flag -- it blocks future GRANTS,
    // it does not touch grants already sitting in AdditionalActions. All battle-scoped: cleared at
    // FinishBattleResolution, never carried into a later battle or turn.
    public List<AdditionalActionGrant> AdditionalActions { get; set; } = [];
    public GameCommand? PendingAdditionalAction { get; set; }
    public bool NoNewAdditionalActionsThisBattle { get; set; }
    // "Before an action resolves, negate one of its own named effects" (Mirumoto Rokai's own
    // destruction-negation; Tamori Tamoko's own movement-or-straightening-negation) -- the same
    // defer-then-reexecute shape PendingAdditionalAction uses above: the whole triggering command is
    // stashed here (never partially applied) while a "beforeActionReaction" window offers the one
    // eligible watching card's controller a chance to use its own negating Reaction before
    // re-invoking ExecuteAbilityEffects on the SAME command. NegatedEffectKind/NegatedEffectCardId
    // (set by that Reaction's own execution, consumed -- read then cleared -- by whichever of
    // MoveHome/Straighten/DestroyUnits actually runs first matching them) are the single shared
    // negation signal; only one can ever be pending at a time, since resolution is synchronous.
    public GameCommand? PendingNegatableAbility { get; set; }
    public string? NegatedEffectKind { get; set; }
    public string? NegatedEffectCardId { get; set; }
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
    // Tamori Tamoko's own Open action (issue #207 follow-up): "target an unbowed Personality
    // controlled by the active player: before the turn ends, gain 1 Honor if he was not assigned to
    // attack your provinces this turn" -- a delayed, mandatory (no further choice) check, queued
    // here and drained unconditionally in EndTurn, the same turn it was queued (Tamoko's own Open
    // Timing is only ever usable "if it is not your turn," so this always resolves before the SAME
    // active player's own turn actually ends).
    public List<TamokoHonorCheck> PendingTamokoHonorChecks { get; set; } = [];
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
    int? TargetPlayerId = null);
public sealed record CommandResult(bool Accepted, string? Error, long Version, GameEvent[] Events);
public sealed record PaymentOption(string[] SourceIds, int Produced, int Cost, int Excess);
public sealed record PurchaseQuote(string CardId, int Cost, bool ClanDiscount, bool ClanAllegiance,
    bool ProclaimEligible, string? Error, PaymentOption[] Payments);
public sealed record OverlayQuote(string CardId, string TargetId, int HigherLevel, int LowerLevel, string? Error);
public sealed record ActionOption(string Type, string Label, bool Enabled, string? Reason,
    GameCommand? Command = null, string? CardId = null, string[]? TargetIds = null,
    int[]? Provinces = null, PurchaseQuote? Quote = null);
/// <param name="UnitForce">Authoritative effective unit contribution, including qualifying Followers.
/// Null for hidden/non-unit/out-of-play cards and older payloads; never substitute a client calculation.</param>
public sealed record CardView(string Id, CardDefinition? Definition, int OwnerId, Zone Zone, bool FaceUp,
    bool Bowed, int? ProvinceIndex, string? AttachedTo, int Force, int Chi = 0, bool StraightenBlocked = false,
    AppliedModifier[]? Modifiers = null, GrantedKeyword[]? GrantedKeywords = null, bool Dishonorable = false, int? UnitForce = null,
    CardToken[]? Tokens = null);
public sealed record ProvinceView(int Index, int Strength, bool Destroyed, CardView? Card);
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
    CardView[]? EventsInPlay = null);
/// <summary>Nothing here is hidden information -- both players already know who challenged whom, on
/// what stat, and whose turn it is; a focused card's own identity is still gated the normal way,
/// through PlayerView.Focused's own per-viewer visibility.</summary>
public sealed record DuelView(int ChallengerPlayerId, int ChallengedPlayerId, string ChallengerId,
    string ChallengedId, string Stat, string Stage, int TurnPlayerId, bool Struck);
public sealed record MatchView(string ContentVersion, string Scenario, long Version, int TurnNumber,
    int ViewerId, int ActivePlayerId, int PriorityPlayerId, Phase Phase, PendingChoice? PendingChoice,
    int? CurrentBattleProvince, int[] UnresolvedBattlefields, int? WinnerId, string? VictoryReason,
    PlayerView[] Players, GameEvent[] Events, string[] Limitations, int? FavorPlayerId = null,
    DuelView? PendingDuel = null);
public sealed record ReplayRecord(string ContentVersion, string Scenario, ulong Seed, GameCommand[] Commands);
