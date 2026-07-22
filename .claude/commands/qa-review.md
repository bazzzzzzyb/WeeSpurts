---
description: QA pass on recent changes — hallucinated APIs, network trust, edge cases
---

Use the qa-engineer agent to review $ARGUMENTS (default: everything changed since the last commit — check `git diff` and `git status`).

Required output:
1. **API audit** — every Unity/Mirror/Facepunch call that isn't verifiable in official docs, flagged with a link or a "confirm this exists" note.
2. **Trust audit** — any path where a client mutates shared state without going through the host.
3. **Edge cases** — disconnects at each state, 10th-frame scoring corners, zero-coin bets, whatever applies to this diff.
4. **A runnable checklist** for Tony: numbered editor/build steps with expected results.
5. Verdict: safe to merge / fix first (with the fix list ordered by risk).
