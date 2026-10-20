namespace L5R.Core;

/// <summary>The tutorial (issue #935): the first turn and a half of a game, the pinned Lion deck (you) against the pinned Crane deck, taken step by step.
/// A *note* step has a button; a *move* step waits for the one move it names (everything else the table would offer is switched off). What a step does to the table when it is done
/// (lift a hold, let Crane play its part) is its <see cref="TutorialStep.Then"/>; Crane never moves unless a step says so, so the lesson can stop between its plays.
/// The words are here, and every number in them is checked against the catalogue and the engine by TutorialTests. The script only chooses what is offered and when; the engine decides every rule.</summary>
internal static class TutorialLessonOne
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? ability = null, string? card = null) =>
        (look, command) => command.Type == type && (ability is null || command.AbilityId == ability) && (card is null || look.Is(command.CardId, card));

    /// <summary>The province an attack names: the command carries it as the one assignment of the attacking unit.</summary>
    private static int? AttackedProvince(GameCommand command) => command.Assignments is { Count: 1 } assignments ? assignments.Values.Single() : command.Province;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d));
    private static Func<TutorialLookup, IEnumerable<string>> OwnProvinces(params int[] indices) => look => indices.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    /// <summary>True when the command pays with exactly these Gold sources.</summary>
    private static bool PaidBy(TutorialLookup look, GameCommand command, params string[] definitions) =>
        command.SourceIds is { Length: > 0 } sources && sources.Distinct().Count() == sources.Length && sources.Length == definitions.Length
        && definitions.All(d => sources.Any(id => look.Is(id, d)));

    private static bool FoeHas(MatchState state, string definition) =>
        state.Cards.Values.Any(c => c.OwnerId == 1 - TutorialRunner.Seat && c.DefinitionId == definition && c.Zone == Zone.Home);

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? show = null, bool slow = false) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, ShowDefinitionId = show, Slow = slow };

    private static TutorialStep Do(string id, string title, string body, string instruction, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, GameCommand, bool>? accept = null,
        string? hint = null, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string[]? glow = null, int[]? onlyProvinces = null) =>
        new() { Id = id, Title = title, Body = body, Instruction = instruction, Allow = allow, Accept = accept, RefusalHint = hint ?? "Do the move the lesson asks for.", Then = then, Highlight = highlight, Glow = glow, OnlyProvinces = onlyProvinces };

    private static readonly string[] Bar = [TutorialGlow.PhaseBar];

    public static readonly TutorialStep[] Steps =
    [
        Note("welcome", "Welcome to L5R", "Welcome to L5R, an Eastern Medieval Fantasy game set in the Empire of Rokugan where the Great Clans vie for power and sometimes join together to fight for their very existence."),
        Note("clans", "Lion against Crane", "You are playing as the Lion Clan. Your opponent is their historic rival, the Crane Clan."),
        Note("domination", "A game of domination", "L5R is a game of Domination. Dominate your opponent through Military Might, Honor and Dishonor, and Enlightenment to secure your Clan's preeminence!"),
        Note("military-victory", "Military Victory", "To win a Military Victory: destroy all of your opponent's Provinces."),
        Note("provinces", "Your Provinces", "These are your Provinces. You and your opponent each start with four.", highlight: OwnProvinces(0, 1, 2, 3), slow: true),
        Note("dynasty-cards", "Dynasty cards", "The black-backed cards in your Provinces are called Dynasty cards.", highlight: OwnProvinces(0, 1, 2, 3)),
        Note("personalities", "Personalities", "The Dynasty deck contains your generals, called Personalities. This is Ikoma Sugo.", highlight: OwnProvinces(0), show: TutorialCards.IkomaSugo),
        Note("holdings", "Holdings", "The other type of Dynasty card is a Holding. It represents an economic asset. This is Copper Mine.", then: c => c.Draw(6), highlight: OwnProvinces(1), show: TutorialCards.CopperMine),
        Note("fate-cards", "Fate cards", "The green-backed cards are called Fate cards. Let's draw your starting hand now. Mouse over the cards to inspect them. Click Continue when you're ready to proceed.",
            glow: [TutorialGlow.Hand]),
        Note("turns", "Taking turns", "The game is played by players taking turns until one player achieves a victory condition. Players alternate turns and, on one another's turn, have several opportunities to act even when it is not their turn."),
        Note("first-player", "Who goes first", "You are the Lion Clan and have seven starting Honor. The player with the highest Honor goes first. Crane has 6, so you will go first.",
            highlight: All(Own(TutorialCards.YourStronghold), Foe(TutorialCards.FoeStronghold))),
        Note("honor-victory", "Honor Victory", "If you ever start your turn with 40 or more Honor, you win an Honor Victory."),
        Note("dishonor", "Dishonor", "If your Honor ever falls to -20 or lower, you lose at the beginning of your next End phase, even if you recover first. Your opponent wins."),
        Note("enlightenment", "Enlightenment Victory", "If you ever start your turn controlling all five Rings, you win. This is pretty rare."),
        Note("sequence", "The sequence of play", "On your turn, you will flow through the sequence of play. It's listed at the top of the screen.", then: c => { c.HoldAt(Phase.Straighten, 0); c.StartTurn(); }, glow: Bar),
        Note("straighten", "Straighten phase", "First, we straighten bowed cards. Bowed cards are turned 90 degrees to the side and may not use the actions printed on them. They are straightened by rotating them back into their upright position. Notice that the Crane player has a bowed card, Bamboo Harvesters.",
            then: c => { c.HoldAt(Phase.Events, 0); c.Resume(); }, highlight: Foe(TutorialCards.BambooHarvesters), glow: Bar),
        Note("events", "Events phase", "In the Events phase, the cards in your Provinces are revealed. There are a few other types of Dynasty cards (including Events); we'll save those for later.", then: c => c.Resume(), glow: Bar),
        Note("action-phase", "Action phase", "The Action phase allows players to take actions. On your turn, you may take Limited actions. Prepared for Death has a Limited action. Mouse over it to inspect it.",
            highlight: Hand(TutorialCards.PreparedForDeath), glow: Bar),
        Note("equip", "Equipping", "You may also equip Items, Spells, and Followers during your Action phase. You have an Item, Light Armor, and a Follower, Veteran Skirmishers. Mouse over them to inspect them. Equipping them means placing them under a Personality. They are then considered \"attachments\" of their card type.",
            highlight: Hand(TutorialCards.LightArmor, TutorialCards.VeteranSkirmishers), glow: Bar),
        Do("pass-action", "Passing", "We don't have any Personalities to play our Limited action on, or to equip our Follower or Armor to, so we will move on to the next phase.",
            "Click Pass action to proceed to the Attack phase.", Move("pass"), glow: Bar),
        Do("decline-attack", "The Attack phase", "You don't technically need a Personality, Followers or Attachments to declare an attack, but it's best to wait until you have some. We'll wait until next turn, when we have recruited a Personality.",
            "Click Decline to attack.", Move("pass"), glow: Bar),
        Note("economy", "Economy", "Now it's time to improve your economy and recruit Personalities.", glow: Bar),
        Do("recruit-hachiro", "Recruiting", "Ikoma Sugo costs 8 (the gold coin). Matsu Hachiro costs 4. If you mouse over your Stronghold (Halls of Memory) and the Border Keep, you'll see they produce 3 Gold and 2 Gold, so 5 at most. That's not enough for Ikoma Sugo. But it is enough for Matsu Hachiro.",
            "Click Matsu Hachiro, choose the full cost (4 Gold), then click your Stronghold and the Border Keep to pay for him. Click Recruit to confirm.",
            Move("purchase", card: TutorialCards.MatsuHachiro),
            accept: (look, command) => !command.ClanDiscount && !command.ClanAllegiance && PaidBy(look, command, TutorialCards.YourStronghold, TutorialCards.BorderKeep),
            hint: "Recruit Matsu Hachiro at his full cost, paid by your Stronghold and your Border Keep.",
            then: c => { c.Auto("proclaim"); c.HoldAt(Phase.End, 0); c.HoldAt(Phase.Straighten, 1); },
            highlight: All(OwnProvinces(2), Own(TutorialCards.YourStronghold, TutorialCards.BorderKeep)), glow: Bar),
        Note("proclaimed", "Proclaiming", "You have now recruited your first Personality. Notice that your Honor has gone from 7 to 10. That is because you Proclaimed him, and his Personal Honor is 3 (Personal Honor is depicted in the fan next to the coin). Once per turn, when you bring out a Personality, you can add his Personal Honor to your family's Honor. You're now 25% of the way to an Honor Victory.",
            highlight: Own(TutorialCards.MatsuHachiro), glow: Bar),
        Do("end-dynasty", "Ending your turn", "You're out of Gold, so it's time to end your turn.", "Click End Dynasty Phase.", Move("endDynasty"), glow: Bar),
        Note("end-phase", "End phase", "This is the end of your turn. You draw a card and play moves to your opponent.", then: c => c.Resume(), glow: Bar),
        Note("crane-straighten", "Crane's turn", "It's now the Crane player's turn. Normally, cards straighten. Bamboo Harvesters can't straighten on its controller's first turn, so watch for it on Crane's second turn.",
            then: c => { c.HoldAt(Phase.Events, 1); c.Resume(); }, highlight: Foe(TutorialCards.BambooHarvesters), glow: Bar),
        Note("crane-events", "Crane's Events phase", "The Crane player also has some Holdings and Personalities in their Provinces. Let's see what they are.", then: c => c.Resume(), highlight: FoeProvinces(0, 1, 2, 3), glow: Bar, slow: true),
        Note("crane-passed", "Crane passes", "The Crane player passed their first Action. It is your turn to play or pass.", glow: Bar),
        Do("pass-open", "An Open action", "Paid Off is an Open action. That means it may be played during either player's Action phase. Limited actions are \"limited\" to the Action phase on your turn.",
            "Click Pass action to move to the next phase.", Move("pass"), highlight: Hand(TutorialCards.PaidOff), glow: Bar),
        Note("crane-attack", "Crane's Attack phase", "The Crane player has no Personalities and you have one. The Crane player would be wise not to declare an attack, for fear of a counter-attack!",
            then: c => c.Foe(s => s.Phase == Phase.Dynasty), glow: Bar),
        Note("crane-dynasty", "Crane's Dynasty phase", "Now, the Crane must choose what to buy.", then: c => c.Foe(s => FoeHas(s, TutorialCards.LargeFarm)), glow: Bar),
        Note("crane-kenshi", "Clan Discount", "The Crane player has used 4 Gold to recruit Daidoji Kenshi. When recruiting a Personality that shares your Clan, if you do not Proclaim them, you may pay 2 less Gold for them. She cost only 3, and Shinden Asahina produces 4 Gold. The extra Gold is lost.",
            highlight: Foe(TutorialCards.DaidojiKenshi, TutorialCards.FoeStronghold), glow: Bar),
        Note("crane-farms", "Free and cheap Holdings", "Small Farm costs 0 Gold, so it may be recruited for free. And Large Farm costs 1 Gold. Even though the Border Keep produces an extra Gold, it is lost and cannot be diverted to Daidoji Kenshi to Proclaim her.",
            then: c => c.Foe(), highlight: Foe(TutorialCards.SmallFarm, TutorialCards.LargeFarm), glow: Bar),
        Note("housekeeping", "Your second turn", "On a normal turn, the computer will do your housekeeping for you. It will only stop when you need to make a decision."),
        Do("equip-armor", "Raising an army", "It's time to raise an army and attack the Crane.",
            "Click Light Armor, then click Matsu Hachiro. A gold icon appears in the lower left of the screen. Light Armor costs 2 Gold: click your Stronghold to bow it for 3 Gold (one is lost). Click Attach to confirm.",
            Move("equip", card: TutorialCards.LightArmor), accept: (look, command) => look.Is(command.TargetId, TutorialCards.MatsuHachiro) && PaidBy(look, command, TutorialCards.YourStronghold),
            hint: "Equip Light Armor to Matsu Hachiro, paid by your Stronghold.", highlight: All(Hand(TutorialCards.LightArmor), Own(TutorialCards.MatsuHachiro, TutorialCards.YourStronghold))),
        Do("equip-skirmishers", "A Follower", "Now, let's give him a Follower.",
            "Click Veteran Skirmishers, then click Matsu Hachiro and see that they cost 2 Gold. Next, click the Border Keep to pay for them. Click Attach to confirm.",
            Move("equip", card: TutorialCards.VeteranSkirmishers), accept: (look, command) => look.Is(command.TargetId, TutorialCards.MatsuHachiro) && PaidBy(look, command, TutorialCards.BorderKeep),
            hint: "Equip Veteran Skirmishers to Matsu Hachiro, paid by your Border Keep.", highlight: All(Hand(TutorialCards.VeteranSkirmishers), Own(TutorialCards.MatsuHachiro, TutorialCards.BorderKeep))),
        Note("force", "Counting Force", "Look at the numbers on the left of Matsu Hachiro's unit. 3 for the Veteran Skirmishers' Force. 3 for the Light Armor. 1 for Matsu Hachiro. That's 7 Force. Do you have enough Force to attack and win?",
            highlight: Own(TutorialCards.MatsuHachiro)),
        Note("kenshi-force", "Their defender", "Matsu Hachiro's unit is pretty tough. Daidoji Kenshi has only 3 Force. There's a good chance you'll be able to defeat her in combat. But can you destroy a Province?", highlight: Foe(TutorialCards.DaidojiKenshi)),
        Note("province-strength", "Province Strength", "Mouse over the Crane Stronghold. There are three numbers on it. You already know that the 4 in the gold coin is its Gold production, and the 6 in the box beneath that is its starting Honor. That means the top value is the Province Strength, a reflection of how resilient the Crane's Provinces are.",
            highlight: Foe(TutorialCards.FoeStronghold)),
        Note("destroy-math", "Destroying a Province", "In order to destroy a Province, you must have more Force than the defending army and the Province Strength combined. So, to destroy the Province and kill Daidoji Kenshi, you need 10 Force: her 3, plus 6, and one more.", highlight: Foe(TutorialCards.DaidojiKenshi, TutorialCards.FoeStronghold)),
        Note("attack-bonus", "Attacking bonus", "Fortunately, Matsu Hachiro gains 2 Force when attacking, so his unit's 7 Force will become 9. That is still one short. To be sure, let's play Prepared for Death to give him an extra 2 Force (in case our Crane opponent has some shenanigans).",
            highlight: All(Own(TutorialCards.MatsuHachiro), Hand(TutorialCards.PreparedForDeath))),
        Do("prepared-for-death", "A Limited action", "Prepared for Death is a Limited action, so you may only play it on your turn, during your Action phase.",
            "Click Prepared for Death, then click Matsu Hachiro. Confirm to play it.",
            Move("useAbility", "prepared-death"), accept: (look, command) => look.Is(command.TargetId ?? command.PerformerId, TutorialCards.MatsuHachiro),
            hint: "Play Prepared for Death on Matsu Hachiro.", highlight: All(Hand(TutorialCards.PreparedForDeath), Own(TutorialCards.MatsuHachiro))),
        Do("pass-to-attack", "Both players pass", "The Crane player has passed. If you pass now, that's both of you passing consecutively, and it will end your Action phase.",
            "Click Pass action.", Move("pass")),
        Do("declare-attack", "Declare an attack", "Now it's time to attack.", "Click the red Declare an attack button.", Move("declareAttack")),
        Note("attack-bar", "The attack bar", "You've declared an attack. Now we will navigate the Attack phase and take you through a battle. There is a vertical bar that shows what's happening in the Attack phase now that you've declared an attack.",
            glow: [TutorialGlow.BattleBar]),
        Note("maneuvers", "Maneuvers", "First up is Maneuvers; that's where you assign attackers and the Crane player assigns defenders. In general, battles are fought at Provinces, and each of the defender's Provinces has its own battlefield, whether anyone is assigned to it or not.",
            glow: [TutorialGlow.BattleBar]),
        Do("assign-attacker", "Assign your attacker", "You're going to attack Province 1.",
            "Click and drag Matsu Hachiro to the Crane player's Province 1, or click Matsu Hachiro and then Province 1. Then click Confirm assignments.",
            (_, command) => command.Type == "attack" && AttackedProvince(command) == 0, hint: "Attack the Crane player's Province 1.", highlight: All(Own(TutorialCards.MatsuHachiro), FoeProvinces(0)),
            glow: [TutorialGlow.BattleBar], onlyProvinces: [0]),
        Note("defender", "Defenders", "The Crane player has assigned Daidoji Kenshi to defend.", highlight: Foe(TutorialCards.DaidojiKenshi), glow: [TutorialGlow.BattleBar]),
        Note("cavalry", "Cavalry", "Some cards have the keyword Cavalry. A Cavalry unit is assigned during the Cavalry maneuvers (after all Infantry units are assigned). Your units are Infantry, so we skip the Cavalry assignments.", glow: [TutorialGlow.BattleBar]),
        Do("engage", "Engagement", "Next is the Engagement step. That is where the attacker selects a battle to resolve first. There is nothing interesting at the three other battlefields.",
            "Click Fight at province 1.", (_, command) => command is { Type: "selectBattle", Province: 0 }, hint: "Resolve the battle at Province 1.", highlight: FoeProvinces(0), glow: [TutorialGlow.BattleBar]),
        Note("reactions", "Reactions", "Engagement is a trigger for many Reactions. Neither you nor your opponent has a Reaction to play. We'll move on to fighting the battle.", then: c => c.Foe(), glow: [TutorialGlow.BattleBar]),
        Note("never-afraid", "The first action", "Defenders get to take the first action. Crane played Never Afraid and destroyed your Veteran Skirmishers. Most destroyed cards are sent to a player's discard pile. Destroyed Personalities go to a Dead pile.",
            slow: true, glow: [TutorialGlow.BattleBar]),
        Note("battle-values", "The battle so far", "Notice that the battle bar has reduced your Force from 11 to 8 and the fire and sword emblem is gone. The Crane has 3 Force. This means you will have more Force than the opponent, so you will win the battle, but not destroy the Province.",
            glow: [TutorialGlow.BattleBar]),
        Note("short", "Two Force shy", "To destroy the Province you would need more than 9: Daidoji Kenshi's 3 Force and the Province Strength of 6. You have 8, so you are two Force shy. You'll have to settle for winning the battle, not destroying the Province.", glow: [TutorialGlow.BattleBar]),
        Do("incapacitated", "Sending her home", "There is a way: send Daidoji Kenshi away, and only the Province Strength is left to beat.",
            "Click Incapacitated, then click Daidoji Kenshi. Confirm to play it.", Move("useAbility", "incapacitated"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.DaidojiKenshi),
            hint: "Play Incapacitated on Daidoji Kenshi.", then: c => c.Foe(), highlight: All(Hand(TutorialCards.Incapacitated), Foe(TutorialCards.DaidojiKenshi)), glow: [TutorialGlow.BattleBar]),
        Do("pass-battle", "Sent home", "Daidoji Kenshi has been \"sent home\". Home is the generic space in your tableau that is not at a battlefield. Without a Personality in the fight, or a way to get one back, the Crane must pass.",
            "Click Pass battle action to proceed to the battle's resolution.", Move("pass"), glow: [TutorialGlow.BattleBar]),
        Note("resolved", "Resolution", "You now have enough Force to destroy the Province. The card is discarded and the Crane's Provinces move closer together to signify the loss of the Province.", glow: [TutorialGlow.BattleBar]),
        Note("congratulations", "Congratulations", "Congratulations, you now know the basics of how to play a turn and how to fight a battle. Remember, if you destroy an opponent's last Province, you win the game!", button: "End Tutorial"),
    ];

    /// <summary>The step that lays out the Provinces: the notes before it are read on an empty cloth.</summary>
    public static readonly int ProvincesStep = Array.FindIndex(Steps, s => s.Id == "provinces");
}
