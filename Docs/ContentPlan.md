# Content Plan — everything that makes it a 3D game

Assets, characters, animations, UI, audio, VFX: what we need, exactly where each comes from, who produces it (🤖 AI / 🧑 you), and when it enters the project. Companion to `ArtGuide.md` (style law) and `PLAYBOOK.md` (when).

**Decided 2026-07-21 (Tony, revised same day):** characters are **Phasmo-style low-poly humanoids** (normal-ish janky humans, bright-clean not realistic-dark); the world is **bright Wii-clean**. Placeholders = free CC0 rigged characters; Tony's AI-made models swap into the same slot later. Beans demoted to fallback. Recorded in the Bible. Tony's hands-on asset guide: `AssetWorkbench.md`.

---

## 1. The look, made concrete

Bright Wii-clean: daylight scenes, saturated flat colors, zero grime, chunky readable shapes. It lives or dies on *discipline* — cheap assets look intentional only if the palette is enforced everywhere.

**Palette (law — use these, log exceptions):**

| Role | Hex |
|---|---|
| Sky / background | `#7EC8F2` |
| Lane wood | `#F2C879` |
| Primary accent (UI, buttons) | `#FF6B57` |
| Secondary accent | `#3EC97E` |
| Highlight / coins | `#FFD447` |
| Neutral light | `#F7F4EE` |
| Neutral dark (outlines, text) | `#3A3A46` |

Player colors: red, blue, green, yellow, purple, orange, pink, cyan — max saturation, instantly tell-apart (that's also the netcode-cheap "customization v1").

Readability rules (from ArtGuide, now enforced): silhouette test on every model; no textures where a flat color works; if a screenshot doesn't read at phone-thumbnail size, it fails — that's the clip test again.

## 2. Characters — Phasmo-style humanoids 🧑🤖

Direction: normal-ish low-poly humans doing dumb physics (the Lethal Company / R.E.P.O. / Phasmo lineage) rendered bright and clean to match our world. The jank is the charm; the realism stays out.

Pipeline (full hand-held steps in `AssetWorkbench.md` §2):

- **Placeholder now**: Quaternius **Universal Base Characters** (CC0, rigged low-poly humans) — in the game in ~30 minutes.
- **Animations**: Mixamo (free) retargets thousands of clips onto any humanoid rig — idle, walk, cheer, drunk stumble, taunt, fall. 🤖 Claude Code wires the Animator; 🧑 you shop for funny clips.
- **Ragdoll comedy**: 🤖 built from the humanoid rig (joints per bone) — big hits flip animation → ragdoll. Works identically on every future character swap.
- **Your models later**: any rigged humanoid (AI-generated then Mixamo auto-rigged, or downloaded) drops into the same slot, same animations, zero code changes.
- **Customization v1**: material color tint + hats on a head bone anchor.

**Fallback (parked):** the bean-ragdoll spec survives as plan B if humanoid ragdolls fight us for more than a week — beans need no rig at all (capsule body, physics hop, upright-spring floppiness dial, face on a quad). Cheap to prototype, proven genre-adjacent (PEAK), but Tony prefers humans — so beans only on evidence.

## 3. Asset shopping list (all free, log every one in `Assets/README.md`)

| Need | Source (exact) | License | Owner | Enters at |
|---|---|---|---|---|
| Ball, pins, lane dressing (real models replacing greybox) | [Poly Pizza](https://poly.pizza) — search "bowling"; [Kenney 3D kits](https://kenney.nl/assets/category:3D) | CC0 / CC-BY (check per model) | 🧑 download, 🤖 import + material fix | Art pass (after fun gate) |
| Alley props: seats, tables, plants, bar counter, arcade cabinet | Kenney **Furniture Kit**, **Food Kit** (kenney.nl) | CC0 | 🧑 download, 🤖 place via scene builder | Art pass |
| Hats/cosmetics | Kenney 3D kits accessories + Poly Pizza "hat" | CC0/CC-BY | 🧑 pick favorites, 🤖 anchor system | Slop layer |
| Filler one-off props (trophy, neon sign, weird bets) | AI 3D gen (Meshy/Tripo) per ArtGuide — filler ONLY | per tool ToS | 🤖 generate, 🧑 approve | Art pass |
| UI sprites: buttons, panels, sliders | Kenney **[UI Pack](https://kenney.nl/assets/ui-pack)** (430 assets) | CC0 | 🤖 | Stage F |
| Icons (coins, bets, emotes) | Kenney **Game Icons** + AI 2D gen | CC0 / — | 🤖 | Stage F |
| Font — display/headers | **Fredoka** or **Luckiest Guy** (fonts.google.com) | OFL | 🤖 via TextMeshPro | Stage F |
| Font — body/numbers | **Baloo 2** (fonts.google.com) | OFL | 🤖 | Stage F |
| SFX: pin crash, ball roll, UI blips, crowd | Kenney audio packs (**Impact Sounds**, **Interface Sounds**) + ElevenLabs SFX gen | CC0 / ElevenLabs ToS | 🤖 wire, 🧑 taste-check | Feel pass + Stage F |
| Taunt voice lines | ElevenLabs (comedy takes) or record yourselves (funnier, free) | ToS / yours | 🧑 record, 🤖 wire | Slop layer |
| Music (menu + alley loop) | Pixabay Music / FreePD (CC0) — upbeat, goofy | CC0 | 🧑 pick 2–3, 🤖 wire | Stage F |
| Logo + capsule art | AI concepts 🤖 → **commissioned final** (see Marketing.md — don't ship an AI capsule) | contract | 🧑 | Store page |
| VFX: pin-strike confetti, dust puffs, drunk bubbles, coin bursts | Unity particle systems, built in-editor | n/a | 🤖 | Feel + slop |
| Skybox | Flat gradient sky (shader or Unity procedural) — matches Wii-clean | n/a | 🤖 | Art pass |

**License rule (unchanged, sacred):** every asset gets a row in `Assets/README.md` *before* it's committed. CC-BY needs the author credited in-game — keep a Credits screen list from day one.

## 4. Pipelines, hand-held 🧑

**Downloading a Kenney pack:** kenney.nl → pack page → Download → unzip → in Unity, drag the `Models/` (FBX or OBJ+MTL) or `PNG/` folder into `Assets/_Project/Art/<PackName>/` in the Project window → done (Kenney models import with materials that just work; if anything imports pink, tell Claude Code: `fix the materials on the pack I just imported for URP`).

**Poly Pizza model:** search → pick model → check the license shown on the page → Download (glTF or FBX) → same drag-in. If it's CC-BY, tell Claude Code to add the author to the Credits screen.

**Google Font → game:** fonts.google.com → search font → Get font → Download all → unzip → drag the `.ttf` into `Assets/_Project/Art/Fonts/` → Window → TextMeshPro → Font Asset Creator → select the ttf → Generate Font Atlas → Save. (First time, Unity offers "Import TMP Essentials" — say yes.) Or just tell Claude Code to walk you through it live.

**AI expression faces / 2D:** generate at 512×512, flat colors, transparent background, consistent line weight; save to `Assets/_Project/Art/Faces/`; 🤖 wires the swap system.

## 5. When content lands (maps to PLAYBOOK stages)

The iron rule stands: **greybox until the fun gate** — with exactly two exceptions, because they ARE the gameplay: the player characters (placeholder rigged humans, Stage C/G) and juice VFX/SFX (feel pass). Everything else waits.

1. **Stage C (feel):** placeholder character walks the alley (walkable-alley test), pin-crash SFX, hit particles. Game still greybox.
2. **Stage F (UI):** Kenney UI Pack + Fredoka/Baloo 2 + palette = the real menus/HUD. UI is cheap to make good early because it's also your store screenshots' frame.
3. **Stage G (slop):** hats, taunt audio + emotes, coin/bet VFX.
4. **Art pass (new stage, after "first funny game" gate, before store page):** replace greybox alley with Kenney/Poly Pizza dressing (and Tony's AI props), skybox, lighting pass, character color-picker in lobby. Budget: **two weeks max** — this style is deliberately cheap; polish goes into juice, not geometry.
5. **Store page:** commissioned capsule; screenshots staged from the art-passed game.

## 6. What this plan deliberately excludes

Custom rigging/weight-painting (Mixamo auto-rig does it), modeling characters from scratch (start from packs or AI gen), Blender beyond cleanup (until after the fun gate), realistic anything, and any paid asset. If a future feature seems to need one of these, that's a `/qa-review`-grade decision — surface it, don't drift into it.
