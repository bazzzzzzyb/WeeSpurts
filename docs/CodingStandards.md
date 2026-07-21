# Wee Spurts — Coding Standards

Rules for humans and AI agents alike. If code and this document disagree, fix the code.

---

## Project layout

```
WeeSpurts/              ← repo root
├── GameBible.md        ← single source of truth
├── README.md
├── docs/               ← all design/reference docs
│   ├── CodingStandards.md  (this file)
│   ├── DefinitionOfDone.md
│   ├── Networking.md
│   ├── OpenQuestions.md
│   └── Roadmap.md
└── Unity/              ← Unity project root
    ├── Assets/
    │   ├── _Project/   ← ALL project-specific assets live here
    │   │   ├── Art/
    │   │   ├── Audio/
    │   │   ├── Prefabs/
    │   │   ├── Scenes/
    │   │   ├── Scripts/
    │   │   └── UI/
    │   └── ThirdParty/ ← imported packages/assets (do not edit)
    ├── Packages/
    └── ProjectSettings/
```

**Rules:**
- Never place project scripts outside `Assets/_Project/Scripts/`.
- Never edit files inside `Assets/ThirdParty/`.
- Keep scenes in `Assets/_Project/Scenes/`.

---

## Naming conventions

| Thing | Convention | Example |
|---|---|---|
| C# class | PascalCase | `BowlingBallController` |
| C# method | PascalCase | `ThrowBall()` |
| C# field (private) | _camelCase with underscore | `_currentSpeed` |
| C# field (public/serialized) | camelCase | `throwForce` |
| C# constant | ALL_CAPS | `MAX_PLAYERS` |
| Unity scene | PascalCase | `BowlingLane.unity` |
| Unity prefab | PascalCase | `BowlingPin.prefab` |
| Folder | PascalCase | `Scripts/Bowling/` |

---

## C# style rules

- **One class per file.** File name must match class name.
- **Namespaces:** `WeeSpurts.<SystemName>` (e.g., `WeeSpurts.Bowling`, `WeeSpurts.Network`).
- **No magic numbers** — use named constants or `[SerializeField]` inspector fields.
- **No `FindObjectOfType` in `Update`.** Cache references in `Awake`/`Start`.
- **Regions are banned.** If a file needs regions to be readable, split it into multiple classes.
- **Comments:** write *why*, not *what*. The code says what; you say why.

---

## AI agent session rules

- Every AI session is scoped to **ONE system** (as listed in `Roadmap.md`).
- Read `GameBible.md` at the start of every session. If Bible and code disagree, stop and flag it — do not silently reconcile.
- Do not add dependencies, packages, or new systems without updating `GameBible.md` §4 first.
- Do not leave TODO comments — either do it now or file a task.

---

## Git

- Commit messages: `[System] Short imperative description` — e.g., `[Bowling] Add pin reset logic`
- Branch per feature: `feature/system-description` — e.g., `feature/bowling-physics`
- Git LFS tracks: `*.png *.jpg *.fbx *.wav *.mp3 *.ogg *.anim *.controller *.mat *.asset`
- Never commit Unity `Temp/` or `Library/` folders — they are in `.gitignore`.

---

## Change log
- 2026-07-21 — Document created.
