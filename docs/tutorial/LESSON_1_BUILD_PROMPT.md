# L5R Tutorial, Lesson 1: build prompt

Lesson 1, "Cards and card types." You play the Lion Clan against a practice opponent playing the Crane Clan. This document is the instruction set for building the lesson: the exact words in the tutorial's note column, the move the player must make before each step advances, and how the table behaves while it is read. It is generated from tools/tutorial/lesson1_source.py; change that file, not this one.

## How to read a step

- Note column: the tutorial box at the left of the table. Each numbered page is exactly what one screen of the box shows. [Continue] is the button under it; the last page of a note uses its own button label.
- Page length: no page exceeds 560 characters. A longer paragraph is split at a sentence boundary, and every split adds a Continue click so nothing scrolls out of sight.
- Player must: the one move the table accepts. A move step shows a "To continue" block and no button, and advances by itself as soon as the move is made. Any other click is refused with a hint.
- Build: what the table does while the text is read (camera, animation, highlight, what the bot does). Engine: the change a button makes to the real game.
- Buttons appear only once any card or battle animation has finished. Reduced motion shows every reveal at once.

## Decisions this build follows

- Lion against Crane. You are Lion (Halls of Memory: 3 Gold, Province Strength 7, Honor 7) and go first; the practice opponent is Crane (Shinden Asahina: 4 Gold, Province Strength 6, Honor 6) and, going second, starts with the bowed Bamboo Harvesters.
- The opponent's table: its Stronghold, a Border Keep and the bowed Bamboo Harvesters. Stronghold (4) and Border Keep (2) produce 6 Gold, exactly the 8 - 2 (Clan Discount) that Doji Atsumichi costs, so the bot recruits him on its first turn by the rules.
- Your first-turn purchases: Small Farm (free), Akodo Uehara (3 Gold, Stronghold) and Copper Mine (2 Gold, Border Keep): the 5 Gold you can make, spent exactly. On turn two the Border Keep pays for Veteran Skirmishers (2 Gold) and the Copper Mine pays for Light Armor (3 Gold for a Lion Clan player, 1 lost).
- Ring of Fire has the Promotional-Emperor text on the Emperor Edition scan (ruling R-086).
- The Dishonor loss is the Comprehensive Rules': Family Honor of -20 or lower loses the game at the beginning of your next End phase, even if you recover first. Enlightenment: control all five Rings that entered play by their own text.
- Wording: American spelling throughout (Honor, Armor, Maneuver). Phases are named as on the phase bar: Straighten, Events, Action, Attack, Dynasty, End. "Battle" is the segment of an attack, never a phase.

## The table the lesson starts from

- Lion (you, seat 0): Halls of Memory; Border Keep; hand empty; four face-down provinces. Dynasty deck, top first: Willing Spirits, Matsu Hana, Ikoma Hakige, Kitsu Akai (provinces 1 to 4), Fallow Lands, Small Farm, Akodo Uehara, Copper Mine, Megumi's Guidance, then Large Farm and Small Farm. Fate deck, top first: Tell the Tale, Truce, Veteran Skirmishers, Light Armor, Ember's Final Fire, Ring of Fire, Defensive Stance, then filler.
- Crane (practice opponent, seat 1): Shinden Asahina; Border Keep; the bowed Bamboo Harvesters; hand empty; four face-down provinces. Dynasty deck, top first: Doji Atsumichi, then Small Farm, Large Farm and so on. Fate deck, top first: Never Afraid, then filler (its End-phase draw).
- Nobody has drawn Fate cards. The table is paused before the first turn. The real engine decides every rule; the lesson only chooses what is offered and when.
- The opponent is scripted: it passes every Action phase it is given, declines every attack, recruits Doji Atsumichi on its turn, defends the province you attack with him, bows Akodo Uehara with his action, plays Never Afraid on Veteran Skirmishers, and otherwise passes.
- The engine holds the first turn at the Straighten phase before the Events phase begins, so the table can stop there and then reveal the provinces one at a time.

## The lesson, step by step

### Step 1. Welcome to Legend of the Five Rings  (welcome)

**Page 1 of 6**

> Legend of the Five Rings is a medieval fantasy collectible card game set in the fictional, far-east empire of Rokugan. This tutorial will teach you how to play Legend of the Five Rings during the Emperor Edition. This game includes most of the cards that were released during this era of the card game. If you've never played before, this tutorial will teach you how to play. If you have played before, this tutorial will help you remember how the rules worked for this edition of the game.

[Continue]

**Page 2 of 6**

> In Emperor Edition, Rokugan is an empire in the truest sense of the word. The original seven clans (Crab, Crane, Dragon, Lion, Phoenix, Scorpion, and Unicorn) have been forced to accept two new entrants, the more well-established Mantis Clan and the newly formed Spider Clan, representing new territories ruled over by the imperial throne. In addition, a set of colonies in the Ivory Kingdoms have been established in the political and population vacuum of the devastating Destroyer Wars.

[Continue]

**Page 3 of 6**

> However, even as the great clans must swear fealty to the Imperial Throne, they vie with one another (and, in secret, often against the very emperor they are sworn to serve) for supremacy, honor, and to secure the future of their clan.

[Continue]

**Page 4 of 6**

> You will play as one of these clans struggling with one another for dominance in the contentious world of Rokugan and the Ivory Kingdoms. In this tutorial you are the Lion Clan, and your practice opponent is the Crane Clan. During the time of active publication, L5R was a narrative game, and players' wins and losses at their local stores and regional tournaments had a meaningful impact on the story.

[Continue]

**Page 5 of 6**

> As a result, many of them developed fierce loyalties to their clan, so if you encounter someone who played "back in the day," don't be surprised if they only play one clan. This can be a fun way to explore and enjoy the game.

[Continue]

**Page 6 of 6**

> However you interact with the game, we hope you have as much fun as we do with these cards.
>
> Press Continue when you're ready to proceed.

[Continue]

### Step 2. Your Stronghold  (stronghold)

**Page 1 of 6**

> This is your Stronghold, Halls of Memory. Your Stronghold is one of the ancestral homes of the great clan you choose to represent. Each clan has several different Strongholds, allowing you to creatively and flexibly make different decks. Your Stronghold has important information on it, and every Stronghold has at least one special ability.

[Continue]

**Page 2 of 6**

> On the upper left-hand side of your Stronghold there is a symbol called your Clan Mon. In this case, it is a Lion. This symbol is associated with every Lion Clan Personality and a number of other cards. Technically, it is artistic flair, but it also means that your Stronghold (and therefore your deck) belongs to the Lion Clan. Even if you had no Lion Clan Personalities or cards in your deck, it would still be a Lion Clan deck because your Stronghold is a Lion Stronghold.
>
> Your Stronghold has important information along the right side.

[Continue]

**Page 3 of 6**

> 1. The top number is your Province Strength. 7 is a strong Province Strength, showing that the Lion Clan is made of resilient people with robust fortifications. An invading army must bring considerable force to bear to destroy your provinces: it must have more than 7 Force. If all of your provinces are destroyed, you lose the game.

[Continue]

**Page 4 of 6**

> 2. The middle number, inside the gold coin, is how much Gold your Stronghold produces on its own. 3 Gold shows that the Lion are somewhat poorer than many of their rivals. Despite being the Right Hand of the Emperor, the Lion are less economically astute than many of the less honorable and less acclaimed clans.

[Continue]

**Page 5 of 6**

> 3. The bottom number, in the banner, is the starting Honor of the Lion Clan. It is called starting Honor because it can go up and down. 7 is a high number, reflecting the historical duty to the Emperor and the dedication to samurai ideals of its warriors. If you ever begin your turn with 40 or more Honor, you win the game by virtue of the respect and deference that others must show you.

[Continue]

**Page 6 of 6**

> But beware: if your Honor ever falls to -20 or lower, you lose the game at the beginning of your next End phase, even if you manage to bring your Honor back above that threshold before then, because your clan becomes a shunned pariah in the empire.

[Continue]

*Build:* Show the Stronghold large on the screen with paused animation (card showcase, motion stopped).

*Build:* Point at the matching part of the card as each numbered page appears: the Clan Mon (upper left), then Province Strength, Gold and starting Honor down the right side.

### Step 3. Starting the game  (starting)

**Page 1 of 5**

> The player with the highest starting Honor takes the first turn. If there is a tie, the first player is decided by chance. Your Honor is 7 and your opponent's Crane Honor is 6, so you go first.

[Continue]

**Page 2 of 5**

> Look over the table; you'll notice a few things. Right now, there are two notable elements of the table. On the left, you'll see your Stronghold and a starting Holding called "Border Keep." Holdings usually represent valuable structures or servants (known as Retainers) that the clan relies on for economic productivity. Many Holdings and Retainers also have other useful abilities. For example, the Border Keep has two abilities allowing you to refresh the cards in your provinces.

[Continue]

**Page 3 of 5**

> But what is a province? If you look at the face-down cards in play, you'll note that there are two decks of cards and four individual face-down cards. This is the starting setup of play, and the four face-down cards are your provinces. It's really that simple: a province holds one Dynasty card (a black-backed card). They aren't exactly one and the same, but they're pretty closely related. The "province" is an abstract concept that exists and, while it exists, it holds a Dynasty card.

[Continue]

**Page 4 of 5**

> If a province is destroyed, the card in it is discarded and the Dynasty and Fate decks are pushed closer together to remove space for a province. The opposite is true if you gain a province: the decks are pushed apart to make room for an additional card.

[Continue]

**Page 5 of 5**

> Remember from before: if your last province is ever destroyed, you lose the game.

[Continue]

*Build:* Highlight the Stronghold and the Border Keep while the first page is read, then the four face-down provinces and the two decks while the second is read.

*Build:* Camera: the fitted table view, centered on the cards actually on the table (the lesson's table is centered between the note column and the right-hand panel).

### Step 4. Border Keep  (border-keep)

**Page 1 of 8**

> Let's take a closer look at your Border Keep. It reads: "Bow this card: Produce 2 Gold."

[Continue]

**Page 2 of 8**

> There are two important concepts on Border Keep expressed in this sentence. The first one is the word Bow. Bowing is a symbolic change in your cards to show that they are "bowed." Right now, the card is "unbowed" (sometimes referred to as straightened). The act of returning to unbowed is straightening. If you look at the top of the screen, you'll see that the first thing that happens on your turn is the Straighten phase, when most bowed cards are returned to unbowed status. To bow a card, simply rotate it 90 degrees to show that it is bowed.

[Continue]

**Page 3 of 8**

> Importantly, you may not use the printed ability on a card that is bowed unless it explicitly says you may use it even while bowed.

[Continue]

**Page 4 of 8**

> The second concept is the difference between a trait and an ability. Notice that there is additional text beneath "Bow this card: Produce 2 Gold." That additional text is preceded by a bolded "Limited:". This indicates that the card may perform an action, and the word Limited denotes when you may use it. However, because "Bow this card: Produce 2 Gold" is not preceded by a bolded word, it is known as a trait. Traits are abilities of cards that are "always on" and may be used as frequently as you are able to satisfy their conditions.

[Continue]

**Page 5 of 8**

> Sometimes you don't have a choice, and the trait is automatically applied. For the Border Keep, as long as it is straightened, you may bow it to produce 2 Gold.

[Continue]

**Page 6 of 8**

> Actions, on the other hand, are restricted to a single use per turn (or, in the case of Border Keep, because it explicitly says so, once per game!). If you use an action once on a turn, even if you can pay its costs again, you may not use it an additional time unless it tells you that you may. This is true even for cards that leave play and are returned to play on the same turn: their actions have memory until the next player's turn begins.

[Continue]

**Page 7 of 8**

> You may have noticed the word Unique on Border Keep. This is a third concept, a keyword. There are two types of keywords: functional keywords, which have extra rules just for them, and nonfunctional keywords, which are descriptive words. Many descriptive keywords have game effects that are triggered by their presence, but they have no special rules themselves. For now, all you need to know is that a card with Unique on it may only have one copy included in your deck.

[Continue]

**Page 8 of 8**

> Cards without Unique (often referred to as non-unique) may be included up to 3 times in your deck.

[Continue]

*Build:* Show the Border Keep large with paused animation.

### Step 5. Bamboo Harvesters  (bamboo)

**Page 1 of 1**

> You might have noticed that your opponent has an additional card in play called Bamboo Harvesters and that it's already bowed. The player who goes second is granted this additional Holding. It enters play bowed and explicitly won't straighten before that player's second turn. The only other thing this Holding does is produce 2 Gold.

[Continue]

*Build:* Highlight the opponent's Bamboo Harvesters (bowed). The opponent's table has its Stronghold, a Border Keep and the bowed Bamboo Harvesters.

### Step 6. Provinces and the Dynasty deck  (provinces)

**Page 1 of 4**

> In Legend of the Five Rings (L5R), your provinces represent the territories that your clan controls. The number of provinces you control is more than just symbolic. They functionally define how many cards you have access to each turn during your Dynasty phase. In effect, you have a hand of four Dynasty cards available to you when you start the game. What cards you choose to put in your Dynasty deck and how you use them will determine whether or not you win the game.

[Continue]

**Page 2 of 4**

> Losing a province means losing one of your four province slots, and with it a quarter of the Dynasty cards you can see and buy each turn, so guard them jealously!

[Continue]

**Page 3 of 4**

> There are two phases in the turn that regularly interact with Dynasty cards in your provinces. The first is the Events phase, where you will turn over any face-down provinces, one by one, resolving Events and attaching Regions. The other phase is the Dynasty phase, where you will purchase Holdings and recruit Personalities. There is only one other kind of Dynasty card: Celestials enter play immediately when they're revealed.

[Continue]

**Page 4 of 4**

> This is usually the Events phase, but may be any other phase where a face-down card in a province is flipped over (also called "revealed").

[Continue]

*Build:* Highlight the four provinces and the Dynasty deck.

### Step 7. The Fate deck  (fate-deck)

**Page 1 of 1**

> The green deck on the right-hand side of the table is the Fate deck. This other deck of cards plays a pivotal role in equipping your Personalities, raising your armies, performing devastating actions in battle, disrupting your enemy's plans, casting magical spells, and mastering the mystical elemental Rings. But most players don't play any Fate cards on their first turn, so we'll pretend they don't exist for a moment while we focus on the core mechanisms related to Dynasty cards.

[Continue]

*Build:* Highlight the Fate deck. The hand is empty and nobody has drawn.

### Step 8. Your first turn  (turn-phases)

**Page 1 of 8**

> Each player's turn has six phases that unfold in a specific order, as shown in the phase bar at the top of your screen. If you're ever confused about what's supposed to be happening, the phase bar will give you a clue. We'll go through the phases briefly here to help you understand the anatomy of a turn.

[Continue]

**Page 2 of 8**

> 1. Straighten - During the Straighten phase, cards in your Home that are bowed are returned to their unbowed status. This is also where "at the start of your turn" effects resolve. Sometimes there will be decisions to be made during this phase, but most of the time any bowed cards will be straightened automatically and the phase marker will move to the next phase.

[Continue]

**Page 3 of 8**

> 2. Events - In the Events phase, you flip over your provinces from left to right, one at a time, resolving any effects as you go. If a card leaves a province during the Events phase, it is replaced face down, and then the next card in order is revealed until there are no more provinces to reveal.

[Continue]

**Page 4 of 8**

> 3. Action - The Action phase is where the game begins to get interesting. Starting with the active player (the player whose turn it is), players alternate taking Limited or Open actions until both players pass consecutively. On your turn, you may take either Limited or Open actions. On your opponent's turn, you are restricted to Open actions.

[Continue]

**Page 5 of 8**

> 4. Attack - On your turn, you have the option to declare an attack or to decline to attack. If you declare an attack, it creates a battlefield at each of your opponent's provinces that you may (or may not) assign attackers to. Your opponent has an opportunity to assign defenders. These armies then fight using Battle actions until, like the Action phase, both players pass consecutively, and each battle resolves. After all battles are resolved, the active player moves into their Dynasty phase.

[Continue]

**Page 6 of 8**

> 5. Dynasty - In this phase, the active player may purchase Holdings and recruit Personalities from their provinces. Only the active player takes turns in the Dynasty phase, so when they choose to be done, the phase ends.

[Continue]

**Page 7 of 8**

> 6. End - The End phase is the cleanup phase. Most actions taken during a turn have a brief lifetime: the effect only lasts until the end of the turn. There are many exceptions to this, but they announce themselves. For example, they mention creating a card or a token, or they tell you explicitly how long the effect they produce lasts. There are many variants, but most things that resolve "at the end of a turn" will resolve or stop during the End phase. Once everything has been resolved and tidied up during the End phase, it's the next player's turn.

[Continue]

**Page 8 of 8**

> Note: when playing in person, it is conventional to bow to your opponent and tell them that the table is theirs to indicate that it is now their turn.

[Start my turn]

*Build:* As each phase is read, point at its name on the phase bar.

*Build:* The last page's button is "Start my turn" (not Continue). It begins the turn, and the table runs the Straighten phase and stops there with the phase bar on Straighten; the Events phase has not started.

*Engine:* Begin the player's turn; run the Straighten phase and hold before the Events phase (the engine's tutorial hold).

### Step 9. Straighten and Events phase  (straighten-events)

**Page 1 of 1**

> There isn't anything interesting in the Straighten phase on your first turn, so we will quickly move to the Events phase, where your provinces are revealed to you.

[Continue]

*Build:* Hold the table on the Straighten phase.

*Build:* When Continue is pressed, release the hold: the Events phase runs and the provinces turn face up one at a time, left to right, with the presentation paused between reveals (reduced-motion players get the instant reveal).

*Engine:* Release the Events hold.

### Step 10. An Event  (event)

**Page 1 of 1**

> The left province has an Event, Willing Spirits. An Event resolves during the Events phase when its province would be revealed, and is then discarded; the empty province is refilled face down. Willing Spirits reads, "After your next turn begins, permanently give each of your provinces +2 strength," so your provinces will be tougher next turn. Regions and Celestials also take effect when revealed in the Events phase.
>
> Next we'll see what else is revealed in your provinces.

[Continue]

*Build:* Slow the reveal of the first province and show Willing Spirits with paused animation.

*Build:* The other three provinces are revealed afterward, one by one, and the Events phase ends in the Action phase.

### Step 11. What you can buy  (what-you-can-buy)

#### Step 11a: Note

**Page 1 of 1**

> Three Personalities. Let's meet a Personality.

[Continue]

*Build:* Show Matsu Hana large with animations off (still card).

#### Step 11b: Note

**Page 1 of 4**

> This is Matsu Hana. First and foremost, you can tell she belongs to the Lion Clan because she has the Lion Mon in the upper left corner of her card and because she has the Lion Clan keyword. She also has the Samurai and Unique keywords. These are functional keywords; the rest of her keywords are descriptive and, therefore, nonfunctional. Though, remember her Paragon keyword: there are many actions that require a Paragon to perform them.

[Continue]

**Page 2 of 4**

> Additionally, any cards that are conditional and may only be performed by or targeted by "your Lion Clan Personality," "your Unique Personality," "your Samurai," or "your Paragon" are all satisfied by Matsu Hana.

[Continue]

**Page 3 of 4**

> Now let's look at all of the numbers on her card. At the top, she has a 5 on the left and a 3 on the right. The 5 is her Force. Force represents the physical prowess of the individual and their personal retinue in combat. Force is important in battles and is summed up for each army to determine the winner. 5 Force makes her a strong Personality, well on her way to having enough Force to destroy a province.

[Continue]

**Page 4 of 4**

> The 3 on the right is her Chi. Chi represents the force of will and spirit a Personality has. It is commonly used as the primary stat in duels, but is referenced in numerous actions. If a Personality's Chi ever drops to zero, that Personality is destroyed.

[Continue]

*Build:* Keep the view on Matsu Hana, paused. Point at the Force (5) and Chi (3) as they are read.

#### Step 11c: Note

**Page 1 of 7**

> In the middle of her card are three numbers: 6, 9, and 4.
>
> The six, you'll notice, is in the same banner artwork as your family Honor as represented on your Stronghold. This is Matsu Hana's Honor Requirement. She will not allow herself to be recruited unless your total Family Honor is 6 or greater. Fortunately, you start with 7, so this isn't an issue. But what happens if your Honor were to fall to 5? Because she is a Lion Clan Personality and because you are a Lion Clan player, you could recruit her if you pay 2 additional Gold for her.

[Continue]

**Page 2 of 7**

> You cannot recruit a Personality that is not aligned to your clan if your Honor is below their Honor Requirement, so be mindful of which Personalities you include in your deck.

[Continue]

**Page 3 of 7**

> The 9 is her Gold cost. This is how much you must pay to recruit her. Personalities are expensive, but they're also necessary for you to build an army, as you cannot command legions without a general. Matsu Hana is too expensive right now, as your Stronghold and Border Keep, together, can only produce 5 Gold. However, there is another benefit of Matsu Hana being a Lion Clan Personality. Because she is aligned to your clan, you may invoke the family relationship and recruit her for what is known as a Clan Discount, or 2 less Gold.

[Continue]

**Page 4 of 7**

> 7 is still too expensive, so we won't be recruiting her this turn.

[Continue]

**Page 5 of 7**

> That brings us to the 4 inside the artwork of a fan. This is her Personal Honor. Personal Honor in Rokugan might be different from what you or I consider honorable. In Rokugan, it represents heroic deeds, adherence to samurai and courtly ideals, fidelity to one's code, and loyalty and duty to one's master. Like many Lion Personalities, Matsu Hana has a high Personal Honor.

[Continue]

**Page 6 of 7**

> Personal Honor is particularly important for two reasons. The first is that recruiting Personalities is a valuable mechanism for improving your Family Honor. To do so, instead of recruiting your Personality with the Clan Discount, you announce your intention to Proclaim your Personality and then pay the price listed here (+2 if your Family Honor is below their Honor Requirement). You then recruit the Personality (placing them in play in your Home), and you then add their Personal Honor to your Family Honor.

[Continue]

**Page 7 of 7**

> The second reason is that a Personality may cost you Honor if they die dishonorably. When a Personality is dishonored, they are rotated 180 degrees and their maximum Personal Honor is treated as zero. If they die before becoming rehonored, they become dishonorable dead, and you lose Honor equal to their printed Personal Honor, even if they aren't aligned to your clan. Recruiting Personalities can be risky, so be sure to protect their reputations and keep them honorable, if you can.

[Continue]

*Build:* Keep the view on Matsu Hana, paused. Point at the Honor Requirement (6), the Gold cost (9) and the Personal Honor (4) as each is read.

#### Step 11d: Note

**Page 1 of 1**

> Look at the other two Personalities you revealed.
>
> Ikoma Hakige requires 12 Family Honor and costs 8 Gold, and Kitsu Akai costs 10 Gold. We won't spend much time on them, because every Personality you've revealed is too expensive for your first turn. Each of these Personalities is powerful but, if you can't afford them, they won't do you any good. It's time to move on to the Action phase and do something about this unlucky draw.

[Continue]

*Build:* Close the Matsu Hana view and finish the remainder of the Events phase; on Continue, advance to the Action phase.

### Step 12. The Action phase  (action-phase)

**Page 1 of 1**

> In the Action phase, the players take turns using an action or passing. On your turn, you may play Limited actions or Open actions. On your opponent's turn, you may only play Open actions. The phase ends when both players pass consecutively. If you pass but your opponent does not, you may resume playing actions on your next opportunity to do so.

[Continue]

*Build:* Show the Border Keep with paused animation.

### Step 13. Refresh your provinces  (border-refresh)

#### Step 13: Move (border-refresh)

**Page 1 of 2**

> Border Keep has two actions. The first, "Limited: If it is your first turn: Put one or more cards in your provinces at the bottom of your deck, refilling the provinces face up," is what we'll use now, as it is your first turn. If you get a bad draw a second time, the other ability is worth using but, remember, it's once per game, so use it strategically.

[Continue]

**Page 2 of 2 (shown with the "To continue" block)**

> The three face-up province cards are no use to you right now, so send them to the bottom of the deck and refill those provinces face up. Leave the face-down card alone. You can replace a face-down card, but we'll hold off on that for now.

**Player must:** Click your Border Keep. Click the three face-up province cards, then press Confirm in the right-hand sidebar.

*Build:* Only the Border Keep's first-turn refresh is offered, and only the three face-up province cards may be chosen; any other click is refused with a hint.

*Build:* After the move, the opponent (practice bot) passes.

### Step 14. Much better  (refreshed)

**Page 1 of 1**

> Small Farm (a free Holding), Akodo Uehara (a Personality) and Copper Mine (a Clan Holding) are face up now, and all three are cards you can use. Your opponent passed, so the next move is yours.

[Continue]

*Build:* Highlight the three refreshed provinces.

### Step 15. Passing in the Action phase  (pass-action)

#### Step 15: Move (pass-action)

**Page 1 of 3**

> If you're anything like most new players, you're thinking that you'd like to recruit Akodo Uehara and purchase that free Small Farm right now. Unfortunately, the Events phase only reveals Dynasty cards; you can't buy them until your Dynasty phase. However, the Events phase is crucial to planning your turn. You'll need to make sure that you save your resources for your priorities, and there may be many circumstances where your opponent's actions require you to pay a cost or suffer a consequence.

[Continue]

**Page 2 of 3**

> It's a good idea to have a contingency plan in case something goes wrong.

[Continue]

**Page 3 of 3 (shown with the "To continue" block)**

> For now, there is nothing else to do in the Action phase, so pass. When you and your opponent pass one after the other, the phase ends.

**Player must:** Press "Pass action" in the right-hand panel.

*Build:* Only Pass action is offered. The opponent passes and the Action phase ends.

### Step 16. The Attack phase  (decline-attack)

#### Step 16: Move (decline-attack)

**Page 1 of 1 (shown with the "To continue" block)**

> Choosing whether or not to attack can be a difficult choice. Every time you attack, or are attacked, any Personalities you commit to battle (and sometimes even those you don't!) are at risk of death. However, on your first turn, the decision is straightforward: you control no Personalities, so you have no reason to attack.
>
> As a reminder, you may declare an attack and choose not to assign any attacking Personalities.

**Player must:** Press "Decline to attack" in the right-hand panel.

*Build:* Only Decline to attack is offered.

### Step 17. The Dynasty phase  (dynasty-phase)

**Page 1 of 1**

> It was just a few moments ago that we were considering recruiting Akodo Uehara and the Small Farm. Now's our chance. Once you're further into the game, the distance between the Events phase and the Dynasty phase is likely to grow as you play actions, attack and resolve battles. For now, we'll buy a Holding and recruit Akodo Uehara by producing Gold from our Stronghold and Holdings.

[Continue]

### Step 18. Buying a Holding  (buy-holding)

#### Step 18: Move (buy-farm)

**Page 1 of 1 (shown with the "To continue" block)**

> Holdings, like Personalities, also have statistics. They are much simpler, however. All Holdings have a cost, represented by the gold coin in the middle of the card. Small Farm costs 0 Gold, so you can bring it into play for free. You can always see how much total Gold you can still produce by looking next to the coin on your clan placard in the bottom left of the screen. This amount declines as you spend your Gold.
>
> A word of warning: if you overpay for a card, you lose any excess Gold. It cannot be used to pay for subsequent purchases.

**Player must:** Click Small Farm in your provinces, then press "Recruit Small Farm".

*Build:* Only the Small Farm can be bought. Point at the Gold total on the clan placard.

### Step 19. Anatomy of a Personality  (personality-anatomy)

**Page 1 of 1**

> On to Akodo Uehara. He has 1 Force and 3 Chi. He's not a very tough Personality, but he only costs 5 Gold, and his Honor Requirement is 6, lower than your current Family Honor, so you could recruit him for as little as 3 Gold. That's a pretty good deal when you consider that you can't raise armies, attack or defend your provinces if you don't have any Personalities.

[Continue]

*Build:* Show Akodo Uehara large (paused animation) and point at his Force, Chi, Honor Requirement, Gold cost and Personal Honor.

### Step 20. Keywords, a trait and an ability  (keywords-trait-ability)

**Page 1 of 10**

> Akodo Uehara is the full package. He has keywords, a trait and a printed ability. We'll walk through each of them to help you get a better sense of what you're looking at. As you learn the game, you'll come to be able to quickly assess a card at a glance. Until then, we'll remind you.

[Continue]

**Page 2 of 10**

> Keywords: Lion Clan Samurai means that he belongs to your clan. You may recruit him for 2 less Gold because he shares your clan alignment. You may pay 2 additional Gold to recruit him if your Family Honor is lower than his Honor Requirement (these two features may be combined). And you may pay his full cost to Proclaim him (add his Personal Honor to your Family Honor when you recruit him). None of these features are available for Akodo Uehara to players who aren't Lion Clan.

[Continue]

**Page 3 of 10**

> They MUST satisfy his Honor Requirement, pay full price, and may not recruit him if their Family Honor is too low.

[Continue]

**Page 4 of 10**

> Samurai: A Samurai is a formally trained warrior, expected to follow a code. If a Samurai, Courtier or Shugenja is ever dishonorable, their controller has an ability: "Open: Order your dishonorable Personality to commit seppuku." Seppuku is ritual suicide. The Personality is rehonored, then becomes honorable dead. It's a tough choice to make, but sometimes it's necessary to save the rest of the family from the disgrace the Personality could bring upon it.

[Continue]

**Page 5 of 10**

> If a dishonorable Personality dies, regardless of their clan affiliation, you lose Honor equal to their printed Personal Honor.

[Continue]

**Page 6 of 10**

> And, finally, Tactician. This is a powerful keyword. In battle, you may discard a card from your Fate hand to grant a Tactician a Force bonus equal to the Focus Value of the discarded card (once per Tactician each turn). We're getting ahead of ourselves, but Focus Values range from 0 to 5, so Akodo Uehara can become quite dangerous on the battlefield. His 1 Force isn't looking particularly bad now.

[Continue]

**Page 7 of 10**

> Looking below his keywords, he has a trait: he gains +1F while he opposes a Phoenix Clan Personality. Traits are "always on." That means that if there is ever one or more Personalities with the Phoenix Clan keyword in an opposing army, Akodo Uehara gets +1 Force.

[Continue]

**Page 8 of 10**

> Force and Chi are often abbreviated as F and C. Gold Cost is occasionally abbreviated as GC, and Personal Honor is abbreviated as PH. You might see a created Personality described as a 2F/2C/2PH Lion Clan Samurai. This would mean that the Personality would be considered to have 2 Force, 2 Chi, 2 Personal Honor and, if not mentioned, a Gold cost of 0. The created Personality would have two keywords, Lion Clan and Samurai, with all of their attendant rules.

[Continue]

**Page 9 of 10**

> Finally, he has an action printed on him: "Limited: Look at the top card of your Fate deck. You may leave it there, discard it, or put it at the bottom of the deck." In order to use an action printed on a card in play (a card is "in play" if it is in your tableau, often referred to as your Home), it must be unbowed, and it must be the appropriate phase to take the action. Limited actions may be taken only on your turn during the Action phase (they are "limited" to your turn). Open actions may be taken during either player's Action phase.

[Continue]

**Page 10 of 10**

> Battle actions may be taken during a battle. And Reactions may be taken when their triggering condition is met.

[Continue]

*Build:* Keep Akodo Uehara on screen (paused). Point at the keyword line, then the trait line, then the action line as each page is read.

### Step 21. Buying a Personality  (buy-personality)

#### Step 21: Move (buy-uehara)

**Page 1 of 1 (shown with the "To continue" block)**

> Akodo Uehara is Lion Clan, like you, so you may use the Clan Discount: a Personality of your own clan costs 2 less, here 3 Gold. That is exactly what your Stronghold produces on its own, so pay with the Stronghold alone and keep the Border Keep.

**Player must:** Click Akodo Uehara in your provinces. In the purchase options choose "3 Gold · Clan discount". Click your Stronghold (Halls of Memory) to pay, then press "Recruit".

*Build:* Only Akodo Uehara can be bought, at the Clan Discount, paid by the Stronghold alone.

### Step 22. A Clan Holding  (clan-holding)

#### Step 22: Move (buy-mine)

**Page 1 of 1 (shown with the "To continue" block)**

> You have one more purchase to make this turn. Copper Mine costs 2 Gold and reads, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Lion Clan player." This Holding rewards your clan: for the Lion, it produces 3 Gold, one more than for anyone else. Your Border Keep makes exactly 2 Gold, so use it to pay for the Copper Mine.

**Player must:** Click Copper Mine in your provinces. Click your Border Keep to pay, then press "Recruit".

*Build:* Only the Copper Mine can be bought, paid by the Border Keep alone (2 Gold). Point at the Lion Clan condition in its text.

### Step 23. We forgot something!  (forgotten-draw)

**Page 1 of 1**

> You have not drawn any Fate cards, and we've already mentioned them for Akodo Uehara's Tactician ability. If you're going to fight a battle with him, you'll need some of those.
>
> At the start of a normal game, each player draws 6 cards from the Fate deck. We avoided this to keep everything simple for you, but you're a Dynasty card pro now, so let's take a look at the hand of Fate cards you're about to draw.

[Draw 6 Fate cards]

*Build:* The last page's button is "Draw 6 Fate cards". The six cards drawn, top of the deck first: Tell the Tale, Truce, Veteran Skirmishers, Light Armor, Ember's Final Fire, Ring of Fire; the seventh card, Defensive Stance, is drawn at the end of this turn.

*Engine:* Draw six cards for the player.

### Step 24. Your hand  (hand)

**Page 1 of 2**

> These six cards are your hand, and only you can see them. Each is a different kind of Fate card. We will go through them one at a time.
>
> Fate cards are similar to Dynasty cards in their layout. They have a title at the top, a Gold cost (if any) in the middle, and effects in the text box at the bottom. Some Fate cards have their own Force; some have Force bonuses and Chi bonuses. We'll discuss those as we get to them.

[Continue]

**Page 2 of 2**

> Finally, every Fate card has a Focus Value (FV) at the bottom of the card. The Focus Value is important for duels, the Tactician ability, and a few other circumstances that refer to Focus Values.

[Continue]

*Build:* Highlight the whole hand, then the Focus Value on one card.

### Step 25. A Strategy: Limited action  (strategy-limited)

**Page 1 of 1**

> Strategies are red-bordered cards that are played from your hand and then discarded. Tell the Tale says "Limited: Gain 1 Honor." It's pretty simple: during your Action phase, you may play Tell the Tale to gain one Honor.

[Continue]

*Build:* Highlight Tell the Tale in the hand.

### Step 26. A Strategy: Reaction  (strategy-reaction)

**Page 1 of 1**

> Truce says, "Reaction: When paying for a Personality with a Clan alignment different than your own: Produce 2 Gold." A Reaction is played in response to the exact moment it names, even on your opponent's turn.

[Continue]

*Build:* Highlight Truce in the hand.

### Step 27. A Follower  (follower)

**Page 1 of 3**

> Veteran Skirmishers is a Follower, a unit of troops. It attaches to a Personality, adding its Force (3) to his unit, and costs 2 Gold to attach. A Personality and everything attached to him are one unit. Attachments such as Followers, Items or Spells may be placed under a Personality to "attach" them to him during the Action phase. When you purchase an attachment, you select a Personality to attach the card to, pay any costs, then place it under the Personality you wish to attach it to.

[Continue]

**Page 2 of 3**

> Followers, Items and Spells may not be brought into play without a Personality to attach to.

[Continue]

**Page 3 of 3**

> You may have noticed that many cards are played during your Action phase and that you cannot recruit Personalities until the Dynasty phase. Much of L5R is a game of planning and execution. Timing your purchases strategically will give you the best chance to win the game.

[Continue]

*Build:* Highlight Veteran Skirmishers in the hand.

### Step 28. An Item  (item)

**Page 1 of 1**

> Light Armor is an Item. It attaches to a Personality too and gives him a Force modifier, here +3, and costs 2 Gold. Its keyword, Armor, means a Personality may only have one Armor attached, and usually may only have one Weapon. Personalities with the Kensai keyword may attach two Weapons. Items not designated as a Weapon or Armor have no attachment restrictions of their own.

[Continue]

*Build:* Highlight Light Armor in the hand.

### Step 29. A Spell  (spell)

**Page 1 of 2**

> Ember's Final Fire is a Spell. Spells attach only to a Shugenja, and you control none for now. It has the keyword Fire, a trait ("While this Shugenja is Fire, he has +1F") and a Battle ability that destroys an enemy attachment.

[Continue]

**Page 2 of 2**

> Items grant Force and Chi bonuses at the title line of the card. Spells grant Force bonuses as traits. Followers have their own Force. A Follower's Force is independent from the Personality, so cards that reference a Personality's Force only refer to the Force printed on the Personality plus any modifiers from Spells, Items and tokens on the Personality.

[Continue]

*Build:* Highlight Ember's Final Fire in the hand.

### Step 30. A Ring  (ring)

**Page 1 of 2**

> Ring of Fire is a Ring, one of the five elements. A Ring enters play when its condition is met (this one, after a battle in which you destroy something), or you can discard it from your hand for a one-time effect. Players who control all five Rings win by Enlightenment.
>
> This is the last victory condition. It's rare, but a wonderful feeling to win by Enlightenment.

[Continue]

**Page 2 of 2**

> As a reminder, the other ways to win are:
> Honor: start your turn with 40 or more Honor.
> Military: destroy all of your opponent's provinces.
> Dishonor: your Honor falls to -20 or lower, and you lose at the beginning of your next End phase, even if you recover first.
> Enlightenment: control all five Rings that entered play by their own text.

[Continue]

*Build:* Highlight Ring of Fire in the hand. The Ring's text on the card is the Promotional-Emperor reading on the Emperor Edition scan (ruling R-086).

### Step 31. Keywords  (keywords)

**Page 1 of 1**

> Every text box starts with keywords, like Fire or Samurai. Some keywords give a Personality rules of their own. Others only matter because another card names them, such as "Target a Naval Personality." A few are pure flavor. Strategies, Items, Spells, Followers and Rings all may have keywords, just like Dynasty cards.

[Continue]

### Step 32. Functional keywords  (functional-keywords)

#### Step 32a: Note

**Page 1 of 1**

> Let's discuss functional keywords.
>
> Duelist: wins ties in a duel against a non-Duelist and may swap a focused card once per duel. We'll get to duels later.

[Continue]

#### Step 32b: Note

**Page 1 of 1**

> Tactician: can discard a card in battle to gain Force equal to its Focus Value, once a turn.

[Continue]

#### Step 32c: Note

**Page 1 of 1**

> Conqueror: after a battle as an attacker, you may choose one of your Conqueror Personalities, straighten his unit, and his unit does not bow as a result of battle resolution.

[Continue]

#### Step 32d: Note

**Page 1 of 1**

> Cavalry: can assign in the separate Cavalry maneuvers.

[Continue]

#### Step 32e: Note

**Page 1 of 1**

> Naval: can claim the first Battle action when attacking with more Naval cards than the defenders.

[Continue]

#### Step 32f: Note

**Page 1 of 1**

> Kensai: may attach two Weapons.

[Continue]

#### Step 32g: Note

**Page 1 of 1**

> Clan keywords (Lion Clan, Crane Clan and so on): may be recruited with the Clan Discount, may be recruited ignoring the Honor Requirement (for 2 more Gold), and may be Proclaimed.

[Continue]

#### Step 32h: Note

**Page 1 of 1**

> Samurai, Courtier, Shugenja: may be ordered to commit seppuku. Shugenja may also attach Spells.

[Continue]

### Step 33. Keywords with no rules of their own  (label-keywords)

**Page 1 of 1**

> Magistrate, Monk, Shadowlands, Paragon, Berserker and Scout are some keywords without a rule of their own, but plenty of cards care about them: a card might protect a Magistrate or punish Shadowlands. That is how most keywords work: they are labels that other cards call. You'll see this as "choose your performing Tattooed Personality" or "Target a Shadowlands card." This isn't an exhaustive list of keywords, just some of the more frequently mentioned.

[Continue]

### Step 34. Ending your Dynasty phase  (end-dynasty)

#### Step 34: Move (end-dynasty)

**Page 1 of 1 (shown with the "To continue" block)**

> You bought what you can afford. Ending the Dynasty phase brings the End phase, where you draw a Fate card, and then it is your opponent's turn.

**Player must:** Press "End Dynasty Phase" in the right-hand panel.

*Build:* Only End Dynasty Phase is offered. The End phase draws Defensive Stance.

*Build:* Then the opponent takes its turn, scripted: its Events phase reveals Doji Atsumichi; it passes in the Action phase, declines to attack, and in its Dynasty phase recruits Doji Atsumichi at the Clan Discount (8 - 2 = 6 Gold) paid by its Stronghold (4) and Border Keep (2). Its Bamboo Harvesters stays bowed. The bot never plays anything else; the player's own passes are made for them.

### Step 35. Your second turn  (second-turn)

**Page 1 of 5**

> Your opponent's turn passed quietly (we passed for you), and they recruited Doji Atsumichi. Your Fate draw gave you Defensive Stance. Your second Events phase has revealed a Region and a Celestial: a Region attaches to the province it was revealed in (Fallow Lands now strengthens it), and a Celestial enters play at once (Megumi's Guidance). Willing Spirits has now fully resolved and made each of your provinces +2 stronger. You should see this bonus shown beneath each of your provinces.

[Continue]

**Page 2 of 5**

> Celestials usually have a trait and a single-use action that you can take advantage of by discarding them. You may only have one Celestial in play at a time, so consider their use carefully before including more than one in your deck. The arrival of a new Celestial forces any existing Celestials you control to be discarded.

[Continue]

**Page 3 of 5**

> Megumi's Guidance gives you a powerful Force bonus when you're opposed. It means that you're more likely to win a battle and very unlikely to tie at zero Force. Ties always result in the destruction of both armies, even when they're tied at 0.

[Continue]

**Page 4 of 5**

> Fallow Lands is a Region. It attaches to the province it appears in, if able. A province may only have one Region attached, so any other Regions revealed are discarded. Be mindful of this when building your deck: too many Regions and your provinces will be clogged with forced discards. Discard piles are not shuffled back in to be drawn again; cards discarded without effect are usually lost.

[Continue]

**Page 5 of 5**

> Any Battle abilities listed on a Region may only be performed at a battlefield associated with the province it's attached to. Since Fallow Lands' Reaction triggers off a Battle action, its Reaction may only be taken at a battlefield associated with its province, where the trigger event occurs (a Melee or Ranged Attack is targeted).
>
> Limited or Open actions from a Region may be taken during their respective phases without restrictions.

[Continue]

*Build:* Highlight the Region on province 1, the Celestial in the player's Home to the right of the provinces, and the +2 strength shown under each province.

### Step 36. A Limited action  (limited-action)

#### Step 36a: Move (tell-tale)

**Page 1 of 1 (shown with the "To continue" block)**

> Tell the Tale is a Limited action: you gain 1 Honor.

**Player must:** Click Tell the Tale in your hand, then press "Confirm play".

*Build:* Only Tell the Tale can be played.

#### Step 36b: Note

**Page 1 of 1**

> Notice that your Family Honor has now increased to 8. Not the most powerful card, but we're 1 point closer to an Honor victory.

[Continue]

*Build:* Point at the Honor on the clan placard.

### Step 37. Attachments  (attachments)

**Page 1 of 2**

> Next, attach your Follower and Item to Akodo Uehara. Attaching costs Gold and is a Limited action you can take any number of times a turn.
>
> Like Personalities, Followers also have an Honor Requirement. However, their Honor Requirement refers to the Personal Honor of the Personality to which they are attaching. Particularly honorable Followers will only follow honorable Personalities.

[Continue]

**Page 2 of 2**

> In the case of Veteran Skirmishers, their Honor Requirement is 0, so we can ignore it. 0 is the lowest a Personality can have (either because they have no honor or because they are dishonorable), so Veteran Skirmishers will attach to any Personality that doesn't explicitly bar having Followers.

[Continue]

### Step 38. Attaching a Follower  (attach-follower)

#### Step 38: Move (equip-follower)

**Page 1 of 1 (shown with the "To continue" block)**

> Attaching costs the Follower's 2 Gold, paid by bowing a Gold source. Your Border Keep makes exactly 2 Gold, so use it. If you paid with your Stronghold, which produces 3 Gold, you would lose the extra 1 Gold. Sometimes you have to overpay, but it's best to avoid it whenever possible.

**Player must:** Click Veteran Skirmishers in your hand, then click Akodo Uehara. Click your Border Keep to pay and press "Attach".

*Build:* Only Veteran Skirmishers, onto Akodo Uehara, paid by the Border Keep.

### Step 39. Attaching an Item  (attach-item)

#### Step 39: Move (equip-item)

**Page 1 of 1 (shown with the "To continue" block)**

> Akodo Uehara's Unit Force (a measure of his Force plus the Force of all of his Followers) is now 4. It's not enough to destroy a province, but with his Tactician ability, you're within striking distance.
>
> Light Armor costs 2 Gold as well. Pay with your Copper Mine this time, and attach it to Akodo Uehara. Because you are a Lion Clan player, the Copper Mine produces 3 Gold, so you will lose 1 Gold to overpaying. That is a fair price for keeping your Stronghold unbowed.

**Player must:** Click Light Armor in your hand, then click Akodo Uehara. Click your Copper Mine to pay and press "Attach".

*Build:* Only Light Armor, onto Akodo Uehara, paid by the Copper Mine (3 Gold; 1 lost).

### Step 40. Passing the Action phase  (pass-action-two)

#### Step 40: Move (pass-turn-two)

**Page 1 of 1 (shown with the "To continue" block)**

> Uehara's unit now has 1 + 3 + 3 = 7 Force. That is enough to destroy a province outright. You don't need to have enough Force to destroy a province to declare an attack, but it helps. If you attack, and your opponent does not defend, then you will probably destroy the province.

**Player must:** Press "Pass action" in the right-hand panel.

*Build:* Only Pass action is offered. The opponent passes.

### Step 41. Declaring an attack  (declare-attack)

#### Step 41a: Note

**Page 1 of 15**

> The Attack phase is its own mini-turn. When you choose to attack, you select the Declare an attack button. Declaring an attack may trigger Reactions from your opponent (including counterattacks that could destroy your provinces!), so don't declare an attack that you're not prepared to carry through.

[Continue]

**Page 2 of 15**

> Once you declare an attack, it creates a battlefield at each of your opponent's provinces. From there, a sequence of events will unfold that is the same for every Attack phase.

[Continue]

**Page 3 of 15**

> 1. Assign Infantry attackers.
>
> A unit is Infantry if its Personality or any of its Followers lacks the Cavalry keyword. Here, the attacker assigns Infantry attackers. Cavalry units may also assign in the Infantry Maneuvers segment. It usually doesn't make sense, but sometimes you might assign Cavalry in the Infantry Maneuvers segment to bait your opponent into defending.

[Continue]

**Page 4 of 15**

> 2. Assign Infantry defenders.
>
> After attacking Infantry are assigned, the defender chooses who to defend with. The advantage of the attacker is that you might be able to destroy a province. The advantage of the defender is having information about where your opponent is attacking.

[Continue]

**Page 5 of 15**

> 3. Assign attacking Cavalry.
>
> After Infantry defenders are assigned, attacking Cavalry are assigned. This reflects their superior maneuverability. Cavalry cards are typically more expensive for the same Force and abilities for this very reason. Maneuverability is valuable, and building decks around Cavalry is a classic strategy.

[Continue]

**Page 6 of 15**

> 4. Assign defending Cavalry.
>
> Defenders still get the last opportunity to defend, so it's worthwhile to have Cavalry to defend with. Defending Cavalry are the most maneuverable and always assign last. Doji Atsumichi has the Cavalry keyword, so he is Cavalry. If you attack, your opponent may choose to assign him to defend during either the Infantry or Cavalry maneuvers segments.

[Continue]

**Page 7 of 15**

> 5. Engage.
>
> Once all attackers and defenders are assigned, the attacker chooses which battle to resolve first. There is a battlefield at every province, and each battle must be resolved. There are many cards that move Personalities between provinces, so the order that you select to resolve each battlefield may matter. Often, players who do not intend to move anyone around will start with opposed provinces and then tell their opponent that they pass at the rest.

[Continue]

**Page 8 of 15**

> In this game, you'll have the option to "pass all battles" to move quickly through battlefields without opposing units.

[Continue]

**Page 9 of 15**

> Engage is a common Reaction window, so watch for Reactions that say "After engaging" or, in later releases, just "Engage:". These are Reactions that may be taken once a battle is selected for resolution.

[Continue]

**Page 10 of 15**

> 6. Battle!

[Continue]

**Page 11 of 15**

> During the Battle segment, starting with the defender, players may choose to play alternating actions until both players pass. This is just like the Action phase, except it occurs for each battlefield. It ends when both players pass in succession. A player who has passed may resume playing actions after their opponent has played an action. This often occurs where a player has more Force than their opponent and waits for their opponent to play actions until they have superior Force. Fate cards are limited in supply, so be mindful of how you play them.

[Continue]

**Page 12 of 15**

> Actions are usually limited to players who have Personalities at a battlefield. However, some actions allow a player to take an action even if no one they control is present (called Presence). An action that moves a Personality to a battlefield may always be played without Presence as long as, if the action resolves, the player will have Presence once the action resolves.

[Continue]

**Page 13 of 15**

> 7. Resolution.
>
> Once both players have passed in succession, the battle resolves. First, you resolve any effects that say "before this battle's resolution," then add up the Force of each card contributing Force to its army, and determine winners and losers. In resolution, losers are destroyed, and winners gain 2 Honor for each card they destroyed. No Honor is gained for cards that were destroyed during combat, only cards destroyed during resolution.

[Continue]

**Page 14 of 15**

> In L5R, if something is a tie, everyone involved suffers the consequences of losing. If two armies tie, each player earns 1 Honor for each card in their opponent's army.
>
> Finally, apply any "after this battle's resolution" effects.

[Continue]

**Page 15 of 15**

> 8. Select the next battlefield to engage at and repeat steps 5 to 7 for each battlefield.

[Continue]

*Build:* Each page waits for Continue. After the last page the "Declare an attack" move is asked for.

#### Step 41b: Move (declare-attack)

**Page 1 of 1 (shown with the "To continue" block)**

> Now declare your attack.

**Player must:** Press "Declare an attack" in the right-hand panel.

*Build:* Only Declare an attack is offered.

### Step 42. Sending your attacker  (send-attacker)

#### Step 42: Move (assign-attacker)

**Page 1 of 1 (shown with the "To continue" block)**

> Your opponent has one Personality, Doji Atsumichi, who may defend that province.

**Player must:** Click Akodo Uehara, then click your opponent's first province. Press "Confirm assignments".

*Build:* Only Akodo Uehara may be assigned, and only to the opponent's first province. The opponent defends that province with Doji Atsumichi.

### Step 43. Choosing the battle  (choose-battle)

#### Step 43: Move (pick-battle)

**Page 1 of 1 (shown with the "To continue" block)**

> Your opponent defended with Doji Atsumichi. The attacker chooses the order of battles, and there is one to fight.

**Player must:** Press "Fight at province 1" in the right-hand panel.

*Build:* Only the first province's battle can be chosen.

### Step 44. Reading a battle  (reading-battle)

#### Step 44a: Note

**Page 1 of 2**

> A battle starts with a round of Battle actions, beginning with the defender. Doji Atsumichi has an ability printed on him, and he has just used it to bow Akodo Uehara!

[Continue]

**Page 2 of 2**

> When a Personality in battle is bowed, they do not contribute Force to their army. You're still winning this combat thanks to your Veteran Skirmishers and Megumi's Guidance, but you don't have enough Force to destroy the province. You could play an action, but you don't need to. Let's see what this Crane player has up its sleeve.

[Continue]

*Build:* The bot (Crane) opens the battle: Doji Atsumichi bows Akodo Uehara (Uehara's Personal Honor 2 is lower than Atsumichi's 4). Show the bow with paused animation; keep the battle summary box on screen.

#### Step 44b: Move (pass-for-bot)

**Page 1 of 1 (shown with the "To continue" block)**

> Press Pass and watch what your opponent does.

**Player must:** Press "Pass battle action" in the right-hand panel.

*Build:* Only Pass battle action is offered. The bot then plays Never Afraid and destroys the Veteran Skirmishers.

#### Step 44c: Note

**Page 1 of 4**

> Your opponent has played Never Afraid to destroy your Veteran Skirmishers. You now have a bowed Personality who contributes 0 Force to his army and no Followers. Thanks to Megumi's Guidance, you'll have +2F at resolution, but that's still not enough to defeat Doji Atsumichi, who has 3. You're now losing the battle!

[Continue]

**Page 2 of 4**

> A quick note: Akodo Uehara has 4 Force, even when bowed. He just doesn't contribute any Force to his army during resolution if he remains bowed. Straightening him is crucial to being competitive in this battle. Items and Spells still contribute their bonuses (and penalties) to Personalities while they're bowed, so you still count them up when evaluating a Personality's Force.

[Continue]

**Page 3 of 4**

> Another quick note: cards that are bowed generally may not perform actions printed on them unless the action explicitly states they may be performed while bowed. If a Personality is bowed but its attachments are not, you may still perform the action on the card even though, for Spells and Items, the Personality is considered to be the performer.

[Continue]

**Page 4 of 4**

> It's time to play a quick game of catch-up and swing this battle in your favor. Play Defensive Stance to straighten Akodo Uehara and give him +3F.

[Continue]

*Build:* Keep the battle summary box in view while the totals are read.

#### Step 44d: Move (defensive-stance)

**Page 1 of 1 (shown with the "To continue" block)**

> Play Defensive Stance and target Akodo Uehara.

**Player must:** Click Defensive Stance in your hand, then click Akodo Uehara and press "Play Defensive Stance".

*Build:* Only Defensive Stance, targeting Akodo Uehara, is offered.

#### Step 44e: Note

**Page 1 of 1**

> You're now winning the battle decisively. Akodo Uehara's unit is 7 Force, and Megumi's Guidance will grant you +2F. However, the Crane province's strength is 6. In order to destroy a province, you must have more Force than the defending army plus the province's strength, and you're 1F shy.

[Continue]

*Build:* The bot has already passed (it has nothing else to play); the Continue button appears once its pass has been shown.

#### Step 44f: Note

**Page 1 of 1**

> The bot has passed, so we need to come up with at least 1 more Force. You don't have any cards in your hand that grant a Force bonus. However, if you remember Akodo Uehara's keywords, Tactician allows him to, once per turn, discard a card as a Battle action to grant him a Force bonus equal to the Focus Value of the discarded card.

[Continue]

### Step 45. A Battle action  (tactician)

#### Step 45: Move (tactician)

**Page 1 of 1 (shown with the "To continue" block)**

> You've already played a card as a Battle action. However, you haven't used a Tactician ability. Select Akodo Uehara and select his Tactician ability to grant him a Force bonus. Then select Truce and confirm its discard to grant him an additional 2 Force.

**Player must:** Click Akodo Uehara and choose his Tactical Advantage ability. Click Truce to discard it, then press "Use ability" to grant him +2F.

*Build:* Only Akodo Uehara's Tactician ability, discarding Truce, is offered.

### Step 46. Ending the Combat segment  (end-combat)

#### Step 46: Move (pass-battle)

**Page 1 of 1 (shown with the "To continue" block)**

> Your army now has 11 Force, and your opponent has nothing to answer with. When both players pass in a row, the Combat segment ends and the battle resolves. Note that a flame-and-sword icon has appeared in the battle summary box on the left. This indicates that you have enough Force to destroy the province.

**Player must:** Press "Pass battle action" in the right-hand panel.

*Build:* Show the flame-and-sword icon in the battle summary box once the Force is enough to destroy the province. Only Pass battle action is offered.

### Step 47. A province falls  (province-falls)

**Page 1 of 1**

> 11 beats 3 by 8, which is more than 6, so your army destroyed Doji Atsumichi and the province. For the rest of the game, your opponent has one fewer province to draw Dynasty cards from, and destroying all four wins the game. You also gained Honor: twice the number of enemy cards you destroyed.

[Continue]

*Build:* Show the destroyed province and the Honor gain.

### Step 48. A Ring enters play  (ring-enters)

#### Step 48: Move (ring-reaction)

**Page 1 of 1 (shown with the "To continue" block)**

> Your battle destroyed enemy cards, which is the condition on your Ring of Fire. Rings enter play from your hand as a Reaction. Permanent cards in play that are not Holdings (Celestials, Rings, and a few other things, as well as reminders) enter your Home to the right of your provinces. They often have useful abilities, so remember to check them and use them as they suit your strategy.

**Player must:** Click Ring of Fire in your hand, then press "Play Reaction".

*Build:* Only Ring of Fire's Reaction is offered (the Skip Reaction button is not).

### Step 49. Lesson 1 complete  (done)

**Page 1 of 1**

> You now know the Stronghold, Holdings, Personalities, Events, Regions, Celestials, Strategies, Followers, Items, Spells and Rings; keywords, traits and actions; and how a turn and a battle go. In the next lesson you will play more of a real game.

[Finish]

*Engine:* Finish the lesson and return to the menu.

## Checks the build must pass

- bash tools/check.sh --skip-client, including the tutorial tests (dotnet run --project tests/L5R.Core.Tests -- --filter tutorial): the whole lesson plays through using only what each step offers.
- bash tools/check-tutorial.sh: the lesson played through the real table controls, ending with a mid-lesson exit through the table menu.
- python3 tools/tutorial/build_lesson1.py --check: this document and the game's text are generated from the same source.
- Every page is at most 560 characters; every number the text quotes is checked against the catalogue.
