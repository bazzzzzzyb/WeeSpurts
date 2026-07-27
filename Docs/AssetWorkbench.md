# Asset Workbench — Tony's hands-on guide

You make the graphics; this doc is your direction. Work through it in any order, any time — asset work runs *parallel* to code stages and never blocks them, because the game always runs on placeholders. Every finished asset: drop it in `/Assets` (repo root, NOT Unity's), log it in `Assets/README.md`, then tell Claude Code to import and wire it.

**The mental model:** every visual/audio thing in the game is a slot. Slots ship with free placeholders on day one. Your job is replacing placeholders with your own (mostly AI-made) versions whenever you feel like it. No deadline, no blocking, no waste — if a model isn't ready, the placeholder ships.

---

## 1. The style contract (read before generating anything)

Everything you make must obey these, or it'll look like clipart soup:

- **Palette**: use the 7 hexes in `ContentPlan.md` §1. Paste them into every AI prompt.
- **Look**: bright, clean, low-poly / flat-color, thick shapes, zero grime, zero realism.
- **Silhouette test**: black-silhouette it (squint) — if you can't tell what it is, regenerate.
- **Thumbnail test**: still reads at phone-thumbnail size? Ship it. This is also the clip test.

## 2. Characters — Phasmo-style humans (decided, replaces the bean plan)

Direction: normal-ish low-poly humans doing dumb physics — the Lethal Company / R.E.P.O. / Phasmo lineage, but bright and clean, not realistic-dark (realistic clashes with our world and is the one style that makes cheap assets look bad).

**Now (placeholder, ~30 min):** download **Universal Base Characters** from [quaternius.com](https://quaternius.com) (CC0, rigged low-poly humans) → unzip → drag the FBX folder into `Unity/Assets/_Project/Art/Characters/` → tell Claude Code: `Set up the Quaternius character as our player character: Humanoid rig import settings, Animator with the Mixamo clips in Art/Animations, and a PlayerCharacter prefab.`

**Animations (the magic free tool):** [mixamo.com](https://www.mixamo.com) (free Adobe account) retargets animations onto ANY humanoid rig:
1. Upload the character FBX → it auto-rigs → pick an animation from thousands.
2. Download: FBX Binary, **With Skin** for the first one, **Without Skin** for the rest.
3. Drop into `Unity/Assets/_Project/Art/Animations/` → in each file's import settings, Rig tab → Animation Type: **Humanoid** → Apply. Claude Code wires the Animator — never do that by hand.

**Starter animation shopping list (search these on Mixamo):** Idle, Walking, Excited, Defeat, Drunk Walk, Silly Dancing, Taunt, Cheering, Falling — plus a bowling-ish throw (search "throw"; we'll fake the rest with physics).

**Later (your AI lane):** when AI 3D tools give you a character you love, run it through Mixamo's auto-rigger — auto-rigged characters import as true Humanoid and drop into the slot cleanly. (The Quaternius placeholder itself is Generic — its Blender IK rig fails Humanoid import; see `OpenQuestions.md`. `CharacterSetupTool` handles it either way.) Ragdoll-on-impact still happens (Claude Code builds ragdolls from any humanoid rig), so the physics comedy survives every reskin.

## 3. Your AI 2D queue (start today, all safe from redesign)

Generate at 1024×1024+, transparent background where it applies, palette pasted in. Prompt template:

> *flat vector game asset, low-poly party game style, thick rounded shapes, bold outlines, colors: #7EC8F2 #F2C879 #FF6B57 #3EC97E #FFD447 #F7F4EE #3A3A46, white/transparent background, no text, no gradients, no realism — [THE THING]*

Priority order (top = most useful soonest):

1. **Logo concepts** — "Wee Spurts" wordmark, chunky, bowling-ball O. Make 10, pick 2, iterate.
2. **UI icon set** — coin, bet slip, beer mug, strike/spare/gutter symbols, taunt megaphone, crown, ready-check. Consistent set = generate them in one session.
3. **Lane decals** — arrows, foul line, cartoon oil-slick shine, "WEE SPURTS LANES" floor logo.
4. **Signage** — scoreboard frame, "BAR", "SLOTS", hanging pennants (bright, not neon — Wii-clean).
5. **Skybox / backdrop** — simple gradient sky + soft clouds through big alley windows.
6. **Face/expression sheet** — only if we texture faces onto characters; park it until the character look settles.
7. **Steam capsule concepts** — mood/composition studies only; the final is commissioned (`Marketing.md`).

Import: PNG → `Unity/Assets/_Project/Art/UI/` (or `Art/Decals/`) → tell Claude Code what it's for.

## 4. Your AI 3D lane (props and eventually characters)

Text/image-to-3D (Meshy, Tripo, etc. — Bible tech table): great for **props** (trophy, mug, arcade cabinet, weird bet items), currently mediocre for characters and anything that must deform.

Workflow: generate → download **GLB/FBX** → drop into Unity → check: is it under ~10k triangles, does it match the palette, does the silhouette read? If it's oversized/off-center/messy → tell Claude Code `clean up the import settings on <file>` first; only open Blender if the *mesh itself* needs surgery.

**Blender (optional, later):** worth learning for cleanup + simple props once the game is fun. When you're ready: Blender's official "Donut" beginner series, then low-poly prop tutorials. Do not detour here before the fun gate — it's a famous project-killer.

## 5. Audio you can make

- **Taunt lines**: record yourself + Braeden being idiots into a phone. Genuinely funnier than AI voices, and it's *your* game's voice. ElevenLabs for anything you can't perform.
- **SFX**: ElevenLabs SFX / any generator: pin crash variants, comedy boings, crowd "OOOH", slot machine, drink glug. Kenney's CC0 packs fill the boring gaps (UI clicks).
- **Music**: pick 2–3 goofy upbeat loops from Pixabay Music / FreePD (CC0). One menu, one alley, one results sting.

Drop in `/Assets/Audio/` → log license → Claude Code wires it through AudioManager.

## 6. The rules that keep this from going wrong

1. **Log every asset + license in `Assets/README.md` before committing.** One bad file can block the Steam release. AI-generated: note the tool. CC-BY: author goes on the Credits screen.
2. **Placeholders are honorable.** The game must always run; swap art only when yours is better.
3. **Don't restyle mid-genre.** If you fall in love with a different look, that's a Bible edit first (`GameBible.md` change log), then a planned re-pass — not a folder of mismatched files.
4. **When in doubt, generate 10 and pick 1.** AI art is cheap; taste is the scarce resource, and that job is yours.
