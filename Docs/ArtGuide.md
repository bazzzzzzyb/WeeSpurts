# Art Direction

## Style 🔒
- **Low-poly, stylized, colorful.** Readable silhouettes, flat or simple materials, minimal texture detail.
- Why: matches the Wii/Mii vibe, hides AI 3D-gen weaknesses, and is the cheapest style to make look intentional. Realistic art kills indie projects.

## Pipeline
- **Prototype:** grey-box everything with primitives + Kenney CC0 packs. Zero custom art until the game is fun.
- **2D (strong for AI):** UI, logos, icons, skyboxes, textures, character concepts — generate freely.
- **3D (weak for AI):** buy/borrow low-poly packs first. Use text/image-to-3D (Meshy, Tripo, Rodin) only for one-off filler props, and expect cleanup in Blender.
- **Animation:** Mixamo for humanoid rigs; exaggerate for comedy.

## Characters 🔒 (decided 2026-07-21)
**Bean-people ragdolls** — primitive-built capsule bodies, physics-driven (no rigs, no Mixamo), faces on a quad, hats for cosmetics. Full build spec in `ContentPlan.md` §2. The fun gate may still tune proportions/floppiness, but the direction is locked.

## Rules
- Log every asset + license in `/Assets/README.md`.
- Palette is defined and LAW: see `ContentPlan.md` §1 (bright Wii-clean, decided 2026-07-21).
- Silhouette test: if you can't tell what something is from its black outline, redesign it.
