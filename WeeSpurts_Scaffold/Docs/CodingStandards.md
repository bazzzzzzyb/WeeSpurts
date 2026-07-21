# Coding Standards

Rules exist so that AI-generated code from different sessions fits together. Paste this doc into any coding session.

## Language & structure
- **C#**, targeting the Unity LTS version in `GameBible.md`.
- One class per file; filename == class name.
- Prefer composition and small MonoBehaviours over giant "god" scripts.
- Use **ScriptableObjects** for config/data (ball stats, lane setups, bet options) so designers (Tony) can tweak without touching code.
- Managers are singletons only when there's genuinely one (GameManager, SteamManager, AudioManager). Everything else is instanced.

## Naming
| Thing | Convention | Example |
|---|---|---|
| Class / file | PascalCase | `BowlingBall.cs` |
| Method | PascalCase | `LaunchBall()` |
| Public field / property | PascalCase | `public float Spin;` |
| Private field | camelCase, `_` prefix | `private int _framesLeft;` |
| Constant | UPPER_SNAKE | `MAX_PLAYERS` |
| Unity scene | PascalCase | `Lobby.unity`, `BowlingAlley.unity` |
| Prefab | PascalCase | `Pin.prefab` |

## Folders inside Unity `/Assets`
```
_Project/
  Scripts/      (Core, Steam, Gameplay, UI, Slop — one subfolder per system)
  Prefabs/
  ScriptableObjects/
  Scenes/
  Art/
  Audio/
  Materials/
```
The `_Project/` prefix keeps your code separate from imported asset packs.

## AI collaboration rules
- **One system per session.** Never "build the game." Say: "In the Gameplay system, implement 10-frame scoring per `DefinitionOfDone.md`."
- **Always paste the Bible + relevant doc first.** Context in, consistency out.
- **Ask the AI to state assumptions before coding** if the task is ambiguous.
- **Never let AI invent APIs.** If it references a Mirror/Facepunch/Unity method, confirm it exists. Beginners can't always tell — when unsure, ask the AI to cite the official docs.
- **Small diffs.** Prefer "add this one script" over "refactor everything."

## Git rules
- `main` stays working. Do feature work on branches: `feat/bowling-scoring`, `fix/lobby-desync`.
- Commit small and often with clear messages.
- **Scene & prefab merge conflicts are the #1 two-person Unity hazard.** Coordinate who edits a shared scene; prefer prefabs each person owns. Enable Unity's Smart Merge (UnityYAMLMerge) in `.gitconfig`.
- Run `git lfs install` and track binaries (see root `.gitignore` note) before committing art.
- Never commit the `Library/` folder (it's ignored).
