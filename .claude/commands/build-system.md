---
description: Start a properly-scoped build session on ONE system or feature
---

We're working on: $ARGUMENTS

Run the session ritual:

1. Read `Docs/GameBible.md`, the doc for this system (`Docs/Networking.md`, `Docs/UI.md`, etc.), and the matching `Docs/DefinitionOfDone.md` items.
2. Restate the task in one sentence and confirm it is ONE system/feature. If it isn't, propose the smallest useful slice and stop for approval.
3. List your assumptions and the exact files you'll add/change. If anything touches `Docs/OpenQuestions.md` territory, ask instead of deciding.
4. Build it in small steps. After each step, tell Tony exactly what to do in the Unity editor and what he should see. Wait for confirmation before stacking more changes.
5. Finish with: which DefinitionOfDone boxes this ticks, what's still open, and a one-line Bible change-log entry if any decision was made.

Never edit `.unity` or `.prefab` files directly — give editor instructions instead.
