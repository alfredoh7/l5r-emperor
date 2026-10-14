namespace L5R.Core;

/// <summary>Lesson 1, "Cards and card types" (issue #935): the first two turns of a game against the practice opponent, taught card by card.
/// The wording follows the Emperor Edition core rules (docs/rules/EMPEROR_EDITION_CORE_RULES.md); every number quoted is checked against the catalogue by a test, so the
/// text cannot drift from the cards. The script only chooses what is offered and when; the engine decides every rule.</summary>
internal static class TutorialLessonOne
{
    private const string Continue = "Continue";

    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? ability = null, string? card = null) =>
        (look, command) => command.Type == type && (ability is null || command.AbilityId == ability) && (card is null || look.Is(command.CardId, card));

    /// <summary>The province an attack names: the command carries it as the one assignment of the attacking unit.</summary>
    private static int? AttackedProvince(GameCommand command) => command.Assignments is { Count: 1 } assignments ? assignments.Values.Single() : command.Province;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d));
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> Provinces(params int[] indices) => look => indices.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));

    private static TutorialStep Say(string id, string title, string body, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string button = Continue, Action<MatchEngine>? then = null) =>
        new() { Id = id, Title = title, Body = body, Button = button, Highlight = highlight, OnContinue = then };

    private static TutorialStep Do(string id, string title, string body, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        Func<TutorialLookup, GameCommand, bool>? accept = null, string? hint = null, Func<TutorialLookup, IEnumerable<string>>? targets = null, int[]? onlyProvinces = null, Func<GameCommand, bool>? variant = null) =>
        new() { Variant = variant, Id = id, Title = title, Body = body, Instruction = Instructions[id], Allow = allow, Accept = accept, Highlight = highlight, Targets = targets, OnlyProvinces = onlyProvinces, RefusalHint = hint ?? "Do the highlighted move the lesson asks for." };

    private static Func<TutorialLookup, IEnumerable<string>> Both(Func<TutorialLookup, IEnumerable<string>> first, Func<TutorialLookup, IEnumerable<string>> second) => look => first(look).Concat(second(look));

    /// <summary>What to click, step by step: the whole of what is needed for the lesson to move on. A test requires one for every action step.</summary>
    internal static readonly Dictionary<string, string> Instructions = new()
    {
        ["border-refresh"] = "Click your Border Keep and choose \"First-turn province refresh\". Click the three face-up province cards, then press Confirm.",
        ["pass-action"] = "Press \"Pass action\" in the right-hand panel.",
        ["decline-attack"] = "Press \"Decline to attack\" in the right-hand panel.",
        ["buy-farm"] = "Click Small Farm in your provinces, then press \"Recruit Small Farm\".",
        ["buy-uehara"] = "Click Akodo Uehara in your provinces. The purchase options read \"3 Gold · Clan discount\". Click your Stronghold (Halls of Memory) to pay, then press \"Recruit\".",
        ["end-dynasty"] = "Press \"End Dynasty Phase\" in the right-hand panel.",
        ["tell-tale"] = "Click Tell the Tale in your hand, then press \"Confirm play\".",
        ["equip-follower"] = "Click Veteran Skirmishers in your hand and press \"Confirm play\". Click Akodo Uehara, then click your Border Keep to pay. Press \"Attach\".",
        ["equip-item"] = "Click Light Armor in your hand and press \"Confirm play\". Click Akodo Uehara, then click your Stronghold to pay. Press \"Attach\".",
        ["pass-turn-two"] = "Press \"Pass action\" in the right-hand panel.",
        ["declare-attack"] = "Press \"Declare an attack\" in the right-hand panel.",
        ["assign-attacker"] = "Click Akodo Uehara, then click your opponent's first province. Press \"Confirm assignments\".",
        ["pick-battle"] = "Press \"Fight at province 1\" in the right-hand panel.",
        ["battle-action"] = "Click Crushing Strength in your hand and press \"Confirm play\". Click Akodo Uehara, then press \"Play Crushing Strength\".",
        ["pass-battle"] = "Press \"Pass battle action\" in the right-hand panel.",
        ["ring-reaction"] = "Click Ring of Fire and choose \"Use Ring of Fire\", or press \"Skip Reaction\".",
    };

    public static readonly TutorialStep[] Steps =
    [
        // ---- The table before the first turn ----------------------------------------------------------------------------------------------------------------------------
        Say("welcome", "Welcome to Legend of the Five Rings",
            "You will learn the cards by playing a short game against a practice opponent. You are the Lion Clan and your opponent is the Crane Clan, a classic matchup. Take your time: press Continue whenever you are ready. " +
            "Only the move a step asks for will work, so you cannot go wrong."),
        Say("stronghold", "Your Stronghold",
            "This is your Stronghold, Halls of Memory. It never leaves play, and the three icons on its right set your game: Gold 3 (what it produces when it bows), " +
            "Province Strength 7 (how hard each of your provinces is to destroy) and starting Family Honor 7 (your score).", Own(TutorialCards.YourStronghold)),
        Say("honor", "Family Honor",
            "Honor is the heart of the game. Yours starts at 7 and the Crane Stronghold starts at 6, so the player with the higher Honor, you, goes first. You win by destroying all four of your opponent's " +
            "provinces, or by starting your turn with 40 Honor. There are two more victories, which we will meet later.", Own(TutorialCards.YourStronghold)),
        Say("border-keep", "Border Keep",
            "Border Keep is a Holding, a card that makes Gold. Bow it (turn it sideways) to produce 2 Gold. Bowed cards straighten again at the start of your turn. " +
            "It also has a trick for your first turn, which you will use in a moment.", Own(TutorialCards.BorderKeep)),
        Say("bamboo", "Going second",
            "The player who goes second starts with a bonus: Bamboo Harvesters, another Holding that produces 2 Gold. It starts bowed, so it will not straighten before that player's second turn. " +
            "It belongs to your opponent, because you are going first.", Foe(TutorialCards.BambooHarvesters)),
        Say("provinces", "Provinces and the Dynasty deck",
            "Your four provinces sit in a row, each holding one face-down card from your Dynasty deck. Every province has the Province Strength of your Stronghold, 7. " +
            "When a province is emptied, it is refilled at once from the top of the Dynasty deck.", Provinces(0, 1, 2, 3)),
        Say("dynasty-cards", "Dynasty cards",
            "The Dynasty deck holds your Personalities (characters), Holdings (Gold), Events, Regions and Celestials. A card in a province is face down, a secret even from you, " +
            "until your Events phase turns it face up. Only face-up cards can be bought.", Provinces(0, 1, 2, 3)),
        Say("fate-deck", "The Fate deck",
            "Your other deck is the Fate deck, which makes your hand: Strategies, Followers, Items, Spells and Rings. Your hand is empty for now.", null),
        Say("start-turn", "Your first turn",
            "A turn has six phases: Straighten, Events, Action, Attack, Dynasty and End. Press the button to begin yours.", null, "Start my turn", then: engine => engine.TutorialBeginTurn()),

        // ---- Events phase and the Border Keep -------------------------------------------------------------------------------------------------------------------------------
        Say("events", "The Events phase",
            "Straighten would turn your bowed cards upright, though none of yours are bowed yet. Then the Events phase flipped your provinces face up from left to right, and whatever appeared took effect. " +
            "Anything that is not a Holding or a Personality does something right now.", Provinces(0, 1, 2, 3)),
        Say("event-card", "An Event",
            "The left province held an Event, Willing Spirits. An Event resolves the moment it is revealed and is then discarded; the empty province refilled face down. Willing Spirits reads " +
            "\"After your next turn begins, permanently give each of your provinces +2 strength\", so your provinces will be tougher next turn. Regions and Celestials also take effect when revealed.",
            Provinces(0)),
        Say("face-up", "What you can buy",
            "The other three provinces are face up. Matsu Hana costs 9 Gold (7 with the Clan discount for your own Clan, which we will meet), Ikoma Hakige needs 12 Family Honor and you have 7, and Kitsu Akai costs 10 Gold. " +
            "You have 5 Gold to spend (the Stronghold's 3 and the Border Keep's 2), so you cannot buy any of them.", Provinces(1, 2, 3)),
        Say("action-phase", "The Action phase",
            "In the Action phase the players take turns using an action or passing, and the phase ends when both pass in a row. The Border Keep has an action for this exact moment: " +
            "on your first turn it puts province cards you choose on the bottom of your Dynasty deck and refills those provinces face up.", Own(TutorialCards.BorderKeep)),
        Do("border-refresh", "Refresh your provinces",
            "The three face-up province cards are no use to you right now, so send them to the bottom of the deck and refill those provinces face up. Leave the face-down card alone.",
            Move("useAbility", "border-first"), Own(TutorialCards.BorderKeep),
            accept: (look, command) => command.CardIds is { Length: 3 } ids && new[] { 1, 2, 3 }.All(i => ids.Contains(look.Province(TutorialRunner.Seat, i))),
            hint: "Choose the three face-up province cards (provinces two to four) and nothing else.",
            targets: look => new[] { 1, 2, 3 }.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>()),
        Say("refreshed", "Much better",
            "Small Farm (a free Holding), Akodo Uehara (a Personality) and Large Farm are face up now, and all three are cards you can use. Your opponent passed, so the next move is yours.",
            Provinces(1, 2, 3)),
        Do("pass-action", "Pass",
            "You have nothing else to do in this Action phase, so pass. When you and your opponent pass one after the other, the phase ends.",
            Move("pass")),
        Do("decline-attack", "The Attack phase",
            "Next is the optional Attack phase, where your Personalities fight for your opponent's provinces. You have no Personalities yet, so decline.",
            Move("pass")),

        // ---- Dynasty phase: a Holding and a Personality -------------------------------------------------------------------------------------------------------------------------
        Say("dynasty-phase", "The Dynasty phase",
            "Now you bring face-up Holdings and Personalities into play by paying their Gold cost. Gold comes from your Stronghold and your unbowed Holdings, which bow when they pay. " +
            "A source pays all at once: extra Gold from it is lost, so choose what you spend wisely.", Provinces(1, 2, 3)),
        Do("buy-farm", "Buy a Holding",
            "Small Farm costs no Gold and will produce 1 Gold each turn. Holdings always enter play bowed, so it starts working next turn.",
            Move("purchase", card: TutorialCards.SmallFarm), Provinces(1)),
        Say("personality-anatomy", "Anatomy of a Personality",
            "Akodo Uehara is a Personality. Force 1 is his strength in battle. Chi 3 is his resilience: a Personality with 0 Chi is destroyed. Honor Requirement 6: your Family Honor must be at least " +
            "this to bring him into play, and yours is 7. Gold cost 5. Personal Honor 2 is how honorable he is himself, which Proclaim and other cards use.", Provinces(2)),
        Say("personality-text", "Keywords, a trait and an ability",
            "Under his name are his keywords: Lion Clan, Samurai, Tactician. Then comes a trait, \"+1F while he opposes a Phoenix Clan Personality\", which is always on. " +
            "Last is an ability, \"Limited: Look at the top card of your Fate deck...\", which you choose to use. We will compare these again soon.", Provinces(2)),
        Do("buy-uehara", "Buy a Personality",
            "Akodo Uehara is Lion Clan, like you, so you may use the Clan discount: a Personality of your own Clan costs 2 less, here 3 Gold. That is exactly what your Stronghold produces on its own, so pay with the Stronghold alone and keep the Border Keep.",
            Move("purchase", card: TutorialCards.AkodoUehara), Both(Provinces(2), Own(TutorialCards.YourStronghold)),
            accept: (look, command) => command is { ClanDiscount: true, ClanAllegiance: false } && command.SourceIds is [var source] && source == look.Stronghold(TutorialRunner.Seat),
            hint: "Buy Akodo Uehara with the Clan discount (3 Gold) and pay with the Stronghold alone.", variant: command => command is { ClanDiscount: true, ClanAllegiance: false }),

        // ---- The forgotten Fate draw ---------------------------------------------------------------------------------------------------------------------------------------------
        Say("forgot", "We forgot something!",
            "You have not drawn any Fate cards. At the start of a real game, each player draws 6 from the Fate deck. Let's pretend you just did.", null, "Draw 6 Fate cards",
            then: engine => engine.TutorialDraw(TutorialRunner.Seat, 6)),
        Say("hand", "Your hand",
            "These six cards are your hand, and only you can see them. Each is a different kind of Fate card. We will go through them one at a time.",
            Hand(TutorialCards.TellTheTale, TutorialCards.Truce, TutorialCards.VeteranSkirmishers, TutorialCards.LightArmor, TutorialCards.EmbersFinalFire, TutorialCards.RingOfFire)),
        Say("strategy-limited", "A Strategy: Limited action",
            "Strategies are played from your hand and then discarded. Tell the Tale says \"Limited: Gain 1 Honor\". A Limited action can only be used by the player whose turn it is, during the Action phase. " +
            "The number at the bottom, its Focus Value, matters in duels.", Hand(TutorialCards.TellTheTale)),
        Say("strategy-reaction", "A Strategy: Reaction",
            "Truce says \"Reaction: When paying for a Personality with a Clan alignment different than your own: Produce 2 Gold\". A Reaction is played in response to the exact moment it names, even on your opponent's turn.", Hand(TutorialCards.Truce)),
        Say("follower", "A Follower",
            "Veteran Skirmishers is a Follower, a unit of troops. It attaches to a Personality, adding its Force (3) to his unit, and costs 2 Gold to attach. A Personality and everything attached to him are one unit.",
            Hand(TutorialCards.VeteranSkirmishers)),
        Say("item", "An Item",
            "Light Armor is an Item. It attaches to a Personality too and gives him a Force modifier, here +3, and costs 2 Gold. Its keyword, Armor, means a Personality can wear only one.",
            Hand(TutorialCards.LightArmor)),
        Say("spell", "A Spell",
            "Ember's Final Fire is a Spell. Spells attach only to a Shugenja, and you control none, so it stays in your hand for now. It has the keyword Fire, a trait (\"While this Shugenja is Fire, he has +1F\") " +
            "and a Battle ability that destroys an enemy attachment.", Hand(TutorialCards.EmbersFinalFire)),
        Say("ring", "A Ring",
            "Ring of Fire is a Ring, one of the five elements. A Ring enters play when its condition is met (this one, after a battle in which you destroy something), or you can discard it from your hand for a one-time effect. " +
            "Players who control all five Rings win by Enlightenment.", Hand(TutorialCards.RingOfFire)),

        // ---- Keywords, traits and abilities --------------------------------------------------------------------------------------------------------------------------------------
        Say("keywords", "Keywords",
            "Every text box starts with keywords, like Fire or Samurai. Some keywords give a Personality rules of their own. Others only matter because another card names them, such as \"Target a Naval Personality\". " +
            "A few are pure flavor."),
        Say("keywords-duty", "Samurai, Courtier and Shugenja",
            "Samurai, Courtier and Shugenja are the three Personalities with a duty. When one of them is dishonorable, his controller can order him to commit seppuku: he is rehonored and then destroyed. " +
            "Shugenja are also the only Personalities who can attach Spells."),
        Say("keywords-battle", "Keywords that change a battle",
            "Duelist: wins ties in a duel against a non-Duelist and can focus one extra card. Tactician: can discard a card in battle to gain Force equal to its Focus Value, once a turn. Conqueror: after a won battle, " +
            "his unit straightens and does not bow. Cavalry: can move in the separate Cavalry maneuvers. Naval: can claim the first battle action when attacking with more Naval cards than the defenders."),
        Say("keywords-flavor", "Keywords with no rules of their own",
            "Magistrate, Monk, Shadowlands, Paragon, Berserker and Scout are keywords without a rule of their own, but plenty of cards care about them: a card might protect a Magistrate or punish Shadowlands. " +
            "That is how most keywords work: they are labels that other cards read."),
        Say("trait-action", "Traits and actions",
            "A trait is printed text with no designator: it is always on, or it happens automatically when its trigger occurs. An action starts with Limited, Open, Battle or Reaction, and happens only when you choose to take it. " +
            "Followers use their own actions, while the actions of Items and Spells are taken by the Personality they are attached to."),
        Do("end-dynasty", "End your Dynasty phase",
            "You bought what you can afford. Ending the Dynasty phase brings the End phase, where you draw a Fate card, and then it is your opponent's turn.", Move("endDynasty")),

        // ---- Turn two ------------------------------------------------------------------------------------------------------------------------------------------------------------
        Say("turn-two", "Your second turn",
            "Your opponent's turn passed quietly: they bought Daidoji Kenshi, and your Fate draw gave you Crushing Strength. Your second Events phase has revealed a Region and a Celestial: a Region attaches to the province it was revealed in " +
            "(Fallow Lands now strengthens it), and a Celestial enters play at once (Megumi's Guidance). Willing Spirits also made each of your provinces +2 stronger.",
            Provinces(0, 1)),
        Say("phase-bar", "The turn's phases",
            "The phase bar along the table shows where you are: Straighten, Events, Action, Attack, Dynasty, End. You start your Action phase now, with more Gold: your Stronghold and Border Keep straightened, " +
            "and Small Farm joined in."),
        Do("tell-tale", "A Limited action",
            "Tell the Tale is a Limited action: you gain 1 Honor.", Move("useAbility", "tell-the-tale-honor"), Hand(TutorialCards.TellTheTale)),
        Say("attachments", "Attachments",
            "Next, attach your Follower and Item to Akodo Uehara. Attaching costs Gold and is a Limited action you can take any number of times a turn. A Spell would not be offered: Uehara is not a Shugenja. " +
            "A Follower takes its own actions, but an Item's or Spell's action is taken by the Personality it is attached to.", Own(TutorialCards.AkodoUehara)),
        Do("equip-follower", "Attach the Follower",
            "Attaching costs the Follower's 2 Gold, paid by bowing a Gold source. Pay with your Border Keep.",
            Move("equip", card: TutorialCards.VeteranSkirmishers), Both(Hand(TutorialCards.VeteranSkirmishers), Own(TutorialCards.BorderKeep))),
        Do("equip-item", "Attach the Item",
            "The Item costs 2 Gold as well. Pay with your Stronghold this time; its 3 Gold covers it and the extra 1 is lost.",
            Move("equip", card: TutorialCards.LightArmor), Both(Hand(TutorialCards.LightArmor), Own(TutorialCards.YourStronghold))),
        Do("pass-turn-two", "Pass the Action phase",
            "Uehara's unit now has 1 + 3 + 3 = 7 Force. That is enough to fight with.", Move("pass")),
        Do("declare-attack", "Declare an attack",
            "In the Attack phase you fight over your opponent's provinces. Every province becomes a battlefield; you send units to attack, then the Defender sends units to defend.", Move("declareAttack")),
        Do("assign-attacker", "Send your unit",
            "Your opponent has one Personality, Daidoji Kenshi, who will defend that province.",
            (_, command) => command.Type == "attack" && AttackedProvince(command) == 0, Both(Own(TutorialCards.AkodoUehara), FoeProvinces(0)),
            hint: "Attack the opponent's first province.", onlyProvinces: [0]),
        Do("pick-battle", "Fight the battle",
            "Your opponent defended with Daidoji Kenshi. The Attacker chooses the order of battles, and there is one to fight.", (_, command) => command is { Type: "selectBattle", Province: 0 }, FoeProvinces(0)),
        Say("battle-values", "Reading a battle",
            "A battle starts with a round of Battle actions, beginning with the Defender, who passed. Then both armies' Force is added up. Your army has 9 Force: Uehara 1, Skirmishers 3, Light Armor 3, and +2 from Megumi's Guidance, " +
            "which boosts an opposed army. Kenshi has 3. To destroy the province you must beat the defender by more than its Province Strength, 6, and 9 beats 3 by exactly 6. It would hold."),
        Do("battle-action", "A Battle action",
            "A Battle action can only be used during a battle. Crushing Strength reads \"Target your opposed Personality: Give him +3F\".",
            Move("useAbility", "crushing-force"), Hand(TutorialCards.CrushingStrength)),
        Do("pass-battle", "End the Combat segment",
            "Your army now has 12 Force, and your opponent has nothing to answer with. When both players pass in a row the Combat segment ends and the battle resolves.", Move("pass")),
        Say("resolved", "The province falls",
            "12 beats 3 by 9, which is more than 6, so your army destroyed Daidoji Kenshi and the province. For the rest of the game your opponent has one fewer province to draw Dynasty cards from, " +
            "and destroying all four wins the game. You also gained Honor: twice the number of enemy cards you destroyed.", null),
        Do("ring-reaction", "A Ring enters play",
            "Your battle destroyed enemy cards, which is the condition on your Ring of Fire. Rings enter play from your hand as a Reaction.",
            (_, command) => command is { Type: "useAbility", AbilityId: "ring-of-fire-recruit" } or { Type: "declineReaction" }, Hand(TutorialCards.RingOfFire)),
        Say("done", "Lesson 1 complete",
            "You now know the Stronghold, Holdings, Personalities, Events, Regions, Celestials, Strategies, Followers, Items, Spells and Rings; keywords, traits and actions; and how a turn and a battle go. " +
            "In the next lesson you will play more of a real game.", null, "Finish"),
    ];
}
