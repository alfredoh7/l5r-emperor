# Multiple-target chooser review — #345

Implementation in progress on `codex/345-multi-targets`, stacked on #428 at `dd204714`, with main through `9876402c`. Alfredo approved A board selection plus C expandable target groups. No main merge is authorized.

Read only engine-supplied `ActionOption.Slots`: the up-front slots are independently chosen with engine counts and candidates, and their selected order is preserved. At-resolution slots are informational until the engine opens `effectChoice`. The board highlights and arrows and a keyboard-accessible sidebar must represent the same local draft. No defaults may silently substitute for player choices. Private views and drafts must reset across seats/versions and connection changes.

Implementation, prepared launcher, native evidence and review delivery are pending. Human acceptance is not claimed. The inherited stabilization rules candidates remain unapproved.
