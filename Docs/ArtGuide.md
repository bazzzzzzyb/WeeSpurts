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
**Phasmo-style low-poly humanoids** — normal-ish janky humans, bright-clean (never realistic-dark). Placeholder: Quaternius Universal Base Characters (CC0) + Mixamo animations; ragdoll comedy built from the rig; Tony's AI-made characters swap in later via the same humanoid slot. Pipeline: `ContentPlan.md` §2 + `AssetWorkbench.md`. (Bean-ragdoll spec parked as fallback.)

## Rules
- Log every asset + license in `/Assets/README.md`.
- Palette is defined and LAW: see `ContentPlan.md` §1 (bright Wii-clean, decided 2026-07-21).
- Silhouette test: if you can't tell what something is from its black outline, redesign it.

## Prop textures 🔒 (decided 2026-08-11 — standing rule, not a one-off)
AI 3D-gen (Meshy etc.) ships every prop with 4096×4096 PBR-style texture sheets, which is wrong on
both counts for this project: too big for a flat-stylized readable-at-a-lane-length look, and PBR is
the wrong art direction entirely (see Style, above — "flat or simple materials").

- **Source textures checked into the repo: max 1024×1024.** Downsize before the file ever lands in
  `Assets/_Project/Art/Props/` — don't commit a 4096 source and rely on the import setting alone to
  hide it; Git LFS still stores the full source blob either way.
- **In-engine (post-import) max: 512×512, compressed, mipmaps on.** Enforced automatically by
  `ArtTexturePostprocessor.cs` for anything under `Assets/_Project/Art/Props/` — this is a standing
  import rule, not a per-asset Inspector tweak someone has to remember. It only fires on first import;
  a deliberate hand-override later is left alone.
- **Characters get 1024×1024, same compression and mipmaps** — enforced by the same postprocessor for
  anything under `Assets/_Project/Characters/`. A prop is background dressing seen at lane distance;
  the character is the hero asset the throw camera puts next to the lens every turn, so he earns the
  extra detail. Still 4× smaller than the 4096 Meshy ships, which is the point.
- **No metallic/roughness maps on stylized props.** The art direction is flat, not PBR — a
  MetallicRoughness sheet has no home here. Don't import or wire one to a material's texture slots,
  even when the source (Meshy, etc.) ships one alongside the base color map.
