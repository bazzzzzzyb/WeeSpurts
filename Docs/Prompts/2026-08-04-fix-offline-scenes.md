# Prompt — fix the offline scenes the Mirror spike broke

Paste the block below into Claude Code. It follows the session ritual in
`.claude/commands/build-system.md`: Claude restates the task, lists assumptions
and files, and stops for approval before writing code.

---

```
/build-system Player: restore offline play in TestVenue and BowlingAlley after the Mirror spike, using the existing IsOffline house rule.

Read first: Docs/spikes/2026-08-03-mirror-kcp-findings.md (especially the
"Migration cost" bullet), Docs/NextSteps.md line 75, Docs/Networking.md,
Docs/CodingStandards.md, and the matching Docs/DefinitionOfDone.md items.

THE DIAGNOSIS IS ALREADY DONE — do not re-derive it, but do verify it before
you build on it:

- The 2026-08-03 spike turned PlayerAvatar, BowlingMatchFlow and
  BowlingPresentation into NetworkBehaviours. Only SpikeNetKcp.unity and
  SpikeNetKcpPlayer.prefab were given NetworkIdentity components.
- TestVenue.unity has a scene GameObject `Player` (PlayerAvatar,
  FirstPersonController, PlayerCameraDirector, PlayerInteractor,
  InteractionPromptHud) and a scene GameObject `BowlingGame`
  (BowlingMatchFlow, BowlingPresentation, BallLauncher, and others). Neither
  has a NetworkIdentity, and the scene has no NetworkManager at all.
  BowlingAlley.unity has the same problem on `BowlingGame`.
- Result: Mirror's OnValidate logs "requires a NetworkIdentity" for all three
  components, and PlayerAvatar throws NullReferenceException every frame
  because `isLocalPlayer` dereferences a null netIdentity — PlayerAvatar.cs
  line 115 (Update) and line 197 (ApplyMode).
- The findings doc predicted this exactly and left it unfixed on purpose.

SCOPE — this is NOT the Stage E port:

Do NOT add NetworkManager or a transport to these scenes, do NOT convert the
scene Player into a spawned prefab, and do NOT delete the "B" debug trigger,
CmdRequestStartBowling, or the `requiresAuthority = false` on
BowlingMatchFlow.CmdThrow. Those all belong to the NextSteps.md line 75 port
and stay exactly as they are this session. If you think the task needs any of
them, stop and say so instead of doing it.

The one job: these two scenes play solo again, offline, with a clean console,
without weakening the spike's fail-closed networking when a session IS running.

THE APPROACH TO EVALUATE (propose something better if you have it, but justify
it against these rules):

BowlingMatchFlow.cs line 354 already solves this problem for the throw path:

    private bool IsOffline => !NetworkClient.active && !NetworkServer.active;

Read its doc comment — it is the precedent. Extend that same rule to the
player/camera path rather than inventing a second mechanism:

1. Lift IsOffline into one shared place (a small static helper under
   Scripts/Core) so the rule is not copy-pasted a third time, and point
   BowlingMatchFlow's private copy at it.
2. Give PlayerAvatar a public property along the lines of
   `IsThisMachinesPlayer => NetSession.IsOffline || isLocalPlayer`. The
   short-circuit matters: offline it must return before evaluating
   isLocalPlayer, or it NREs on the null netIdentity.
3. Use it at PlayerAvatar.cs lines 115, 197, 198 and 206, at
   BowlingPresentation.cs line 69 (`_thrower.isLocalPlayer`), and at
   PlayerCameraDirector.cs line 90 (`avatar.isLocalPlayer`).

Explain in a comment why this is fail-open ONLY when there is no session at
all, so nobody later reads it as undoing the fail-closed decision recorded in
PlayerAvatar's class comment. When a session is running, behaviour must be
byte-for-byte what it is today.

CONSTRAINTS:

- Never edit .unity or .prefab files. The NetworkIdentity components on
  `Player` and `BowlingGame` are MY job in the editor — give me the exact
  clicks and tell me what a good result looks like.
- NetworkClient.active and NetworkServer.active must be confirmed real Mirror
  API before you rely on them (mirror-networking.gitbook.io). Flag anything
  else you are not certain exists.
- Small diff. Roughly one new file and four edited ones. If it is growing
  past that, stop and tell me why.
- Keep the EditMode tests green (there are 72). Add a test only if the logic
  is testable without Play mode.

FINISH WITH:

- The Unity editor steps for me, in order, with expected results — including
  which scenes to open and what a clean console looks like.
- Which DefinitionOfDone boxes this ticks and which stay open.
- A one-line Docs/GameBible.md change-log entry recording the offline-fallback
  rule, plus a note that the findings doc's pre-placed-sceneId house rule
  becomes relevant to these scenes when Stage E makes them networked — flag
  it, do not solve it now.
```

---

Then, because this touches networking:

```
/qa-review
```
