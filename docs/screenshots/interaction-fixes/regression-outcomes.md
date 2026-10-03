## Exact regression outcomes

### core-before.log

```text
FAIL hand-discard-interaction/OfferedDiscards: Offered discards use modified limits 7, 8 and 10 for both seats; exact defaults execute
FAIL hand-discard-interaction/Boundaries: Below/at/above-limit End Phase boundaries use the same current limit
PASS hand-discard-interaction/InvalidSelections: Wrong count, duplicates, foreign cards, wrong seat and stale version reject atomically
PASS hand-discard-interaction/AlternateSelection: A nondefault valid selection discards exactly the chosen cards; private views stay private
2/4 hand-discard-interaction tests passed.
```

### core-after.log

```text
PASS hand-discard-interaction/OfferedDiscards: Offered discards use modified limits 7, 8 and 10 for both seats; exact defaults execute
PASS hand-discard-interaction/Boundaries: Below/at/above-limit End Phase boundaries use the same current limit
PASS hand-discard-interaction/InvalidSelections: Wrong count, duplicates, foreign cards, wrong seat and stale version reject atomically
PASS hand-discard-interaction/AlternateSelection: A nondefault valid selection discards exactly the chosen cards; private views stay private
4/4 hand-discard-interaction tests passed.
```

### discard-regression.log

```text
ERROR: INTERACTION_CHECK_FAIL: System.Exception: One selection wrongly satisfies a two-card discard
```

### privacy-mutation.log

```text
ERROR: INTERACTION_CHECK_FAIL: System.Exception: Window close switched seats or left the original seat covered
```

### native-1920x1080.log

```text
HANDOFF_PASS: seat 0 Escape, close, cleanup, explicit readiness, session replacement
HANDOFF_PASS: seat 1 Escape, close, cleanup, explicit readiness, session replacement
DISCARD_PASS: limit 7, seat 0, exact native selection and engine result
DISCARD_PASS: limit 7, seat 1, exact native selection and engine result
DISCARD_PASS: limit 8, seat 0, exact native selection and engine result
DISCARD_PASS: limit 8, seat 1, exact native selection and engine result
DISCARD_PASS: limit 10, seat 0, exact native selection and engine result
DISCARD_PASS: limit 10, seat 1, exact native selection and engine result
INTERACTION_CHECK_PASS: published-interactions-check
```

### native-1280x800.log

```text
HANDOFF_PASS: seat 0 Escape, close, cleanup, explicit readiness, session replacement
HANDOFF_PASS: seat 1 Escape, close, cleanup, explicit readiness, session replacement
DISCARD_PASS: limit 7, seat 0, exact native selection and engine result
DISCARD_PASS: limit 7, seat 1, exact native selection and engine result
DISCARD_PASS: limit 8, seat 0, exact native selection and engine result
DISCARD_PASS: limit 8, seat 1, exact native selection and engine result
DISCARD_PASS: limit 10, seat 0, exact native selection and engine result
DISCARD_PASS: limit 10, seat 1, exact native selection and engine result
INTERACTION_CHECK_PASS: published-interactions-check-reduced
```

