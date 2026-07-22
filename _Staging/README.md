# _Staging — pre-written game code

This folder holds the complete Phase 0 + Phase 1 codebase, written before the Unity project exists (Unity Hub needs an empty folder to create a project, so the code waits here).

**Do not edit anything in here by hand.** Once the Unity project exists in `/Unity`, tell Claude Code:

> Merge _Staging/_Project into Unity/Assets/_Project, then delete the _Staging folder.

After that, `PLAYBOOK.md` Stage B tells you what to click and what you should see.

Contents: `_Project/Scripts/{Core, Gameplay, Bowling, UI, Editor}`, `_Project/Tests/EditMode`, assembly definitions, and unit tests for the bowling scorer.
