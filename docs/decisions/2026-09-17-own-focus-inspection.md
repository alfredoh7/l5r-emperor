# Inspect every own focused card

Accepted request, September 17, 2026: Alfredo relayed Mendon’s request that each player can inspect their own focused cards regardless of their source, before wiring the main-game transition. Alfredo also accepted the preceding verdict/motion implementation at `9465324`.

Core already provides each owner with the definition of every committed focus. The UI now opens the shared full-face inspector directly on click during focusing, including off-turn and for blind deck commitments; hover remains available. The inspector explicitly displays Focus Value and private/public context. A card becomes inspectable only after it is committed from the deck. Its face-down mat presentation and the opponent’s anonymous card back remain unchanged. Already-public discard focuses remain public.

Duelist replacement is the sole selection exception: select a legally eligible focus to replace it; hover or the existing Inspect button reads it. Selection eligibility comes from engine action queries. No new Core visibility, transport schema, reveal command or legality change is required.

Regression coverage includes source privacy through serialization, replay and session seat changes; native hand/deck/discard inspection; unchanged replacement controls; reduced motion. The normal-match transition remains a separate task in PR #266. Related issue #205, implementation PR #220; UI lead alfredoh7, mechanics request relayed from mdornbrook.
