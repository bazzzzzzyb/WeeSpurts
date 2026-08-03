# Windows machine setup

Companion to `Docs/Onboarding.md` §2, which is the canonical environment list. This doc adds the
Windows-specific traps that §2 doesn't cover. Applies to Tony's PC and to Braeden.

**Order matters.** Steps 1–5 must happen before the clone. Getting them wrong means re-cloning.

---

## 0. Before anything — pick where the repo will live

**Clone to `C:\Dev\WeeSpurts`. Not Documents, not Desktop, not anywhere inside OneDrive.**

Windows redirects Documents and Desktop into OneDrive by default on most setups. A Unity project's
`Library/` folder is hundreds of thousands of small files that churn constantly on every import and
recompile. OneDrive will try to sync all of it, forever. Symptoms are file locks mid-import, corrupt
`Library/` states, Unity hanging on load, and a saturated upload. It is one of the most reliable ways
to break a Unity project on Windows.

```
mkdir C:\Dev
```

Check whether your Documents folder is OneDrive-backed before assuming it isn't: open File Explorer,
right-click Documents → Properties → Location. If the path contains `OneDrive`, it is.

## 1. Enable long paths

Windows caps paths at 260 characters by default. Unity's `Library/` and package folders nest deep
enough to blow past that, and the failure mode is a confusing mid-import error rather than a clear
message.

Open **PowerShell as Administrator**:

```powershell
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" `
  -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force
```

Then, after Git is installed (step 2), also run:

```
git config --global core.longpaths true
```

Both are needed — they're separate limits.

## 2. Git for Windows

Download from [git-scm.com](https://git-scm.com). During the installer, two choices matter:

- **Line endings:** choose *"Checkout as-is, commit Unix-style line endings"* (`core.autocrlf input`).
  Our `.gitattributes` pins `eol=lf` on `.unity`/`.prefab`/`.asset`/`.meta` but says nothing about
  `.cs`, so this keeps your PC consistent with the Mac and avoids whole-file diffs that are pure
  line-ending noise.
- **Credential manager:** leave Git Credential Manager enabled. It handles GitHub auth without
  personal access token wrangling.

Everything else: accept defaults.

## 3. Git LFS — install and initialise BEFORE cloning

```
git lfs install
```

This is non-negotiable and it is the one step with no cheap recovery. Our `.gitattributes` routes
every `.fbx`, `.png`, `.glb`, `.wav`, `.ttf` and more through LFS. Clone without LFS active and Git
hands you ~130-byte text pointer files where the art should be. Unity then fails to import the
characters, animations, and textures, and the errors it produces will not mention LFS. If this
happens: delete the folder and clone again with LFS installed. Don't try to repair it.

Verify before cloning:

```
git lfs version
```

## 4. Clone

```
cd C:\Dev
git clone https://github.com/bazzzzzzyb/WeeSpurts.git
```

Expect roughly 1GB+ and a slow finish — LFS downloads the binaries as a second pass after the main
clone completes. Let it finish before opening Unity.

Sanity check that LFS worked:

```
cd WeeSpurts
git lfs ls-files
```

Should list many files. Then confirm a real asset is a real asset, not a pointer — this should
report megabytes, not bytes:

```powershell
(Get-Item "Unity\Assets\_Project\Animations\Idle.fbx").Length
```

## 5. Windows Defender exclusion (optional, worth it)

Defender scans every file Unity writes during import and compile. Excluding the project folder is a
noticeable speed win.

Windows Security → Virus & threat protection → Manage settings → Exclusions → Add → Folder →
`C:\Dev\WeeSpurts`.

## 6. Unity Hub + Unity 6000.5.4f1 — exactly

Per `Onboarding.md` §2.1. Hub → Installs → Install Editor → **Archive** tab if 6000.5.4f1 isn't in
the default list. Verify against `Unity/ProjectSettings/ProjectVersion.txt`.

Modules to tick:

- **Windows Build Support (IL2CPP)** — this is the one the Mac can't do. Windows IL2CPP players must
  be built on Windows, which is the main reason this machine exists for the project.
- Your IDE of choice, or none — see step 8.

Then: Hub → sign in → activate a **Personal** licence. Easy to forget; Unity won't open without it.

Hub → **Add** → select the repo's `Unity/` folder → open. First import takes several minutes on a
project this size. Let it finish.

## 7. Verify the project, don't assume

From `Onboarding.md` §2.5–2.6 — project settings travel with the repo, so these should already be
correct. Check anyway:

- Project Settings → Tags and Layers → **User Layer 6 = `LocalPlayerModel`** (exact string).
- Project Settings → Player → Other Settings → **Active Input Handling = Both**.
- Window → General → Test Runner → EditMode → **Run All → all green** (72 as of 2026-07-26).
- Open `Assets/_Project/Scenes/TestVenue.unity` → Play. You should be in first person in the greybox
  alley: WASD + mouse look, walk to a lane console, `[E] Start Game` prompt, E starts the match.

If the tests are green and TestVenue plays, the machine is good.

## 8. Claude Code

Install per [code.claude.com](https://code.claude.com) and follow the current Windows instructions
there rather than any remembered ones — the Windows install path has changed over time. Git for
Windows (step 2) is a prerequisite either way, so it's already handled.

Then:

```
cd C:\Dev\WeeSpurts
claude
```

It reads `CLAUDE.md` automatically. Confirm it's working by asking it: *"what are this project's
golden rules?"* — it should answer from `CLAUDE.md` without being handed the file.

## 9. IDE (optional)

Unity's manifest references both Rider and Visual Studio. You only need one, and only for debugging
and autocomplete — Claude Code doesn't require it.

- **Visual Studio Community** — free, install the *"Game development with Unity"* workload.
- **Rider** — better Unity support, paid (free for non-commercial use at time of writing).

## 10. Steam

Install and sign in. Not needed for the Mirror + KCP spike, which is deliberately Steam-free, but
required from Stage D onward.

---

## Working across two machines

You now have the repo on a Mac and a PC. Same person, two checkouts — so the risk isn't merge
conflicts, it's forgetting to push.

- **Always `git push` before switching machines, and `git pull` on arrival.** Scene and prefab YAML
  does not merge (`CLAUDE.md`, Git section). Two divergent edits to the same scene means one of them
  is lost, and it doesn't matter that both were yours.
- Unity's `Library/` is gitignored and machine-local. Never copy it between machines; let each
  machine build its own.
- Expect the first Play on a freshly-pulled machine to be slow — Unity reimports what changed.
