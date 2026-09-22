# Matsu Rika: "Bushido Virtue" is a plain card keyword, not a printed action-type prefix (issue #449)

Mendon, 2026-09-22: "Bushido virtue is a keyword that appears on some strategy cards, like my enemy's mercy and is conditional, only." This answered the open question behind Matsu Rika's (oracle-l5r-9827) blocker note, which had called for a "'Bushido Virtue action resolved' classification-dispatch Reaction trigger (same unbuilt family as 'Tactical action resolved')" -- written before two pieces the engine has since gained made it, in fact, already closed.

**Result: directly wirable, no new engine mechanics.** Her text: "After your Bushido Virtue action resolves, three times per turn: Give Rika +1F."

- **Bushido Virtue is a card keyword**, confirmed against the catalogue: My Enemy's Mercy (oracle-l5r-5457) carries `Keywords: ["Bushido Virtue", "Compassion"]`, the same shape as any other printed keyword -- not a `Timing` prefix like "Tactical Battle:"/"Political Open:" (`AbilityDefinition.ActionKeywords`). Whether an action "carries" a printed-Timing-prefix classification or a plain source-card keyword is already one question: `ActionCarriesKeyword(ability, source, keyword)` (issue #321) checks `ability.ActionKeywords` OR `Keyword(source, keyword)`, exactly the fallback a Bushido Virtue action needs.
- **The trigger itself** ("after your ... action resolves") is the after-action-resolved Reaction window (`ActionEffectTracking.cs`, issue #437): `AfterActionResolvedApplies` now has a `"rika-bushido-virtue-bonus"` case reading `ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], facts.SourceId is null ? null : Card(facts.SourceId), "Bushido Virtue")` against `MatchState.LastResolvedAction`.
- **The cap** ("three times per turn") is `AbilityDefinition.UsesPerTurn: 3`, the same mechanism Hida Mochitoko's own "twice per turn" Reaction (`mochitoko-negate`) already uses for an otherwise mandatory-worded, N-times-per-turn effect -- which is also why this is wired as a `Reaction` (an offered, opt-in-to-use ability whose own per-turn use count the engine already tracks) rather than through `AfterOwnActionTraits`' mandatory-Trait shape (Daidoji Ebizo/Kobi's own "Give +1F" Traits): the Trait dictionary has no per-turn cap field, and Mochitoko is direct precedent that "N times per turn, no 'you may' in the printed text" is still built as a capped Reaction in this codebase.

No new `Pending*` state, no new trigger constant, no new classification predicate -- `rika-bushido-virtue-bonus` is one `AbilityCatalog` entry, one `AfterActionResolvedApplies` case and one dispatch case applying `+1F` to herself.

## Narrowed, not closed: Akodo Tamisu (oracle-l5r-294)

Her second sentence -- "After your Tactical, Bushido Virtue, or Terrain action resolves: Give a Lion Clan Personality +1F" -- reads through the exact same three-way `ActionCarriesKeyword` check. What's still missing is narrower than the blocker note previously stated: not the classification-trigger family (now built), but (a) a "choose a target Lion Clan Personality" shape for a Reaction whose own printed effect is mandatory once triggered -- Rika's card sidesteps this by only ever buffing herself -- and (b) her separate entry effect, "discard all your other Sensei Personalities" (also blocking Ikoma Aimi's base printing, oracle-l5r-10061). Her blocker note is updated to reflect this narrower remaining gap; not wired in this pass.

Mirumoto Yozo, Experienced 2's own "after an Iaijutsu action resolves" generalization was already published earlier (`IsIaijutsuAction`, issue #207 follow-up) -- the stale prose blaming it on this same unbuilt classification family (build_card_inventory.py, Dragon Clan Personality review comment) is corrected to say so.

## Regenerated

`docs/CARD_INVENTORY.xlsx` (2165 rows, 602 wired).
