# Project Overview

- **Game Title:** ThunderLanes (working title)
- **High-Level Concept:** A retro late-90s/early-2000s bowling entertainment venue — bowling lanes plus casino, bar/lounge, snack shack, arcade, party room, and back-of-house mechanical room — explored in first person.
- **Players:** Single player (walkable first-person exploration)
- **Inspiration / Reference Games / Refs:** Late-90s/2000s bowling-and-arcade complexes (think Main Event / AMF / Dave & Buster's era), retro-luxe neon lounges.
- **Tone / Art Direction:** Retro 90s–2000s entertainment venue — warm walnut + brass + burgundy, tasteful neon signage, arcade blacklight glow. Nightlife mood, NOT daylight.
- **Target Platform:** PC (StandaloneOSX active)
- **Screen Orientation / Resolution:** Landscape, first-person
- **Render Pipeline:** URP (PC_RPAsset active)

> This plan assumes the existing `ThunderLanes_Root` hierarchy in `Assets/Scenes/SampleScene.unity` is kept and refined — NOT rebuilt. Everything is reversible (rollback available).

---

# Diagnosis — Why It Doesn't Read As A Venue

The venue is **not** an unfinished graybox. It is a genuinely detailed build (611 meshes, 8 lanes, casino, bar, snack, arcade, mechanical catwalk, restrooms, party room, DJ stage) with a full authored material palette. Three categories of problems make it fail to read as an entertainment venue:

### A. Finishing layer is switched OFF (fast, high-impact bugs)
1. **All neon/screen/sign emission is DISABLED.** `Mat_Neon_CyanGlow` (`0,1.7,2.0`), `Mat_Neon_GoldGlow`, `Mat_Neon_MagentaGlow`, `Mat_Screen_TVStrike`, `Mat_Sign_RedExit` all have HDR emission colors authored but the `_EMISSION` keyword is off — so **nothing glows**. This is the single biggest reason the space looks flat and dead.
2. **Lit like daylight.** Ambient = Default blue Skybox, a 0.85-intensity directional "sun" with soft shadows, and **no ceiling** (0 geometry above y=5). Reads as an outdoor daylight model.
3. **Glass is secretly opaque.** `Mat_Glass_Translucent` has alpha 0.4 authored but Surface Type = Opaque — every glass door/wall/display case is a solid slab.
4. **Camera** is orthographic, parked outside the east wall — irrelevant once we go first-person, but needs a proper player/eye camera.

### B. Architectural placement problems (the "strip mall of boxes" — user's core complaint)
5. **West wall = four identical counters in a row.** Front Desk (z≈-15) → Snack (z≈2.5) → Bar (z≈17.5) → Casino (z≈32.5), all flat against the west exterior wall, all separated only by knee-high half-walls. No real rooms, no thresholds, no ceilings. This is the strip-mall feeling.
6. **The Bar doesn't belong where it is / how it's massed.** A long straight concession-style counter reads as a snack stand, not a lounge. It needs to feel like a destination lounge with its own enclosure, seating clusters, lowered ceiling/soffit, and separation from the open concourse.
7. **Arcade is an open quadrant, not an alcove.** 12 cabinets in two open rows in the SE. User wants it to feel like an **alcove you step into** — recessed, darker, blacklight-saturated, framed by a portal.
8. **Rooms are open-plan** with only half-walls, so casino/bar/snack/front-desk visually bleed together instead of reading as distinct spaces.

### C. Enclosure & immersion (needed for first-person)
9. **No ceiling anywhere** — first-person needs a ceiling plane, soffits over feature zones, and interior lighting hung from it.
10. Minor: z-fighting risk on stacked floor overlays (carpet at +0.01/+0.02 over subfloor), and decor pieces (bottles, pins, plushies) carry colliders that would snag a first-person controller.

---

# Game Mechanics

## Core Gameplay Loop
First-person walkthrough / exploration of the venue. Player enters through the vestibule, moves down the central concourse, and can step into each distinct room (front desk, snack shack, bar lounge, casino, arcade alcove, party room, lanes, and — for flavor — glimpse the mechanical catwalk). The loop is atmosphere-driven: each room should read instantly as its own destination through architecture, lighting, and signage.

## Controls and Input Methods
- New Input System (project default). A first-person controller (WASD + mouse look) driven by an `InputActionAsset`.
- Interaction is out of scope for this pass unless requested; focus is on the space reading and functioning as a navigable venue.

---

# UI
No HUD/menu work in this pass (environment/architecture focus). If desired later: a simple crosshair + room-name subtitles when entering a zone.

---

# Art Direction Target (Retro 90s–2000s Entertainment Venue)

- **Palette anchor:** keep existing walnut/brass/burgundy/charcoal. Neon used as *signage and accent*, not wall-to-wall.
- **Mood:** dim warm ambient, pools of light under fixtures, glowing signage and screens as the primary "life."
- **Per-room identity via architecture + light color:**
  - Front Desk: warm neutral, brass + backlit signage.
  - Snack Shack: warm diner glow, illuminated menu board, order-counter lighting.
  - Bar Lounge: moody amber/burgundy, lowered soffit, back-bar glow, intimate seating clusters.
  - Casino: gold + magenta, spotlit tables, marquee glow, slightly elevated (already is).
  - Arcade: recessed alcove, dark cosmic floor, cyan/magenta blacklight, glowing cabinet screens.
  - Lanes: cool masked pin-deck glow + warm settee pits.
  - Mechanical: utilitarian caged warm light (already good — leave as reference for "correct").

---

# Key Assets & Context

**Scene:** `Assets/Scenes/SampleScene.unity` → root `ThunderLanes_Root`
**Key groups:** `Floors`, `Walls_and_Partitions`, `WestWing_Zones` (Casino/Bar/Backroom/Snack/FrontDesk/Vestibule), `CenterSpine_Concourse` (DJ stage + cocktail tables), `EastWing_Zones` (Party, Mechanical, 8 lanes, Arcade, Restrooms), `Venue_Lighting`.

**Materials to fix (existing, `Assets/` — URP/Lit):**
- Emissive (enable `_EMISSION` + keep authored HDR color): `Mat_Neon_CyanGlow`, `Mat_Neon_GoldGlow`, `Mat_Neon_MagentaGlow`, `Mat_Screen_TVStrike`, `Mat_Sign_RedExit`, and the "illuminated" `Menu_Board` / `Console_Screen_Glow` / `Token_Dispenser_Screen` / slot `Glowing_Screen` materials.
- Transparency fix: `Mat_Glass_Translucent` → Surface Type = Transparent.

**Coordinate anchors (verified):**
- Building interior footprint ≈ X:[-30,+30], Z:[-40,+40], wall height 5.5 (top ≈ y=5.5).
- Concourse colonnade columns at X=±9.8, Z ∈ {-32,-16,0,16,32} — natural mounting points for portals/soffits/ceiling beams.
- West rooms centered at X≈-20, back wall at X≈-25/-30. Bar at Z≈17.5, Snack Z≈2.5, Front Desk Z≈-15, Casino Z≈32.5.
- Arcade currently X:[10,30], Z:[-25,0].

**Emissive enable snippet (URP):**
```csharp
mat.EnableKeyword("_EMISSION");
mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
mat.SetColor("_EmissionColor", authoredHDRColor); // preserve existing value
```
**Transparent surface snippet (URP/Lit):**
```csharp
mat.SetFloat("_Surface", 1); // Transparent
mat.SetFloat("_Blend", 0);   // Alpha
mat.SetOverrideTag("RenderType","Transparent");
mat.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
mat.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
mat.SetInt("_ZWrite",0);
mat.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
```

---

# Implementation Steps

### Phase 1 — Finishing bugs (fastest wins, no layout change)

**Step 1 — Enable emission on all neon/screen/sign materials.**
- Description: Turn on `_EMISSION` + `RealtimeEmissive` GI flag on all neon, TV/screen, exit-sign, menu-board, slot-screen, and console-glow materials, preserving each authored HDR color. Verify signage/screens now glow.
- Assigned role: developer
- Dependencies: None
- Parallelizable: Yes

**Step 2 — Fix glass transparency.**
- Description: Set `Mat_Glass_Translucent` (and any other glass/display-case materials) to Transparent surface so doors, vestibule walls, party-room glass, and prize case read as glass.
- Assigned role: developer
- Dependencies: None
- Parallelizable: Yes

**Step 3 — Nightlife lighting & atmosphere overhaul.**
- Description: Switch ambient from Skybox to a dark warm Gradient/Color; set camera clear to solid dark (or a night skybox); drop the Directional Light to a low fill (~0.15–0.25) or convert to subtle ceiling ambient; enable/enable-tune the existing zone lights (lanes, concourse, casino spot, mech cages) so light pools read; add missing accent lights for Bar, Snack, Arcade, Front Desk, Party. Add subtle URP fog/bloom via the existing Global Volume so emission blooms.
- Assigned role: developer
- Dependencies: Step 1 (emission must be on to balance bloom/exposure)
- Parallelizable: No

### Phase 2 — Enclosure for first-person immersion

**Step 4 — Add ceiling + soffits.**
- Description: Add a ceiling plane at y≈5.5 across the interior; add lowered soffits/coffers over Bar, Casino, Arcade, and Front Desk to give each room a "ceiling of its own"; mount interior light fixtures to it. Use the X=±9.8 colonnade as beam/soffit anchors.
- Assigned role: developer
- Dependencies: Step 3 (lighting plan informs fixture placement)
- Parallelizable: No

**Step 5 — First-person player controller + input.**
- Description: Add a first-person controller (CharacterController + New Input System WASD/mouse-look) with an eye-height camera; place spawn at the vestibule entrance facing the concourse. Replace reliance on the orthographic showcase camera.
- Assigned role: developer
- Dependencies: None (can be built in parallel with Phase 1)
- Parallelizable: Yes

**Step 6 — Collider cleanup for navigation.**
- Description: Remove/disable colliders on small decor (bottles, pins, plushies, toy boxes, oil cans, screens) that would snag the controller; ensure walls/counters/furniture keep blocking colliders; fix z-fighting on stacked floor overlays by nudging overlay Y or disabling their colliders.
- Assigned role: developer
- Dependencies: Step 5 (test against the controller)
- Parallelizable: No

### Phase 3 — Architectural redesign (the core "strip mall → real venue" fix)

**Step 7 — Define real rooms on the west wall.**
- Description: Replace knee-high concourse half-walls with proper room enclosures + framed portal thresholds so Front Desk, Snack Shack, Bar, and Casino each read as a distinct room. Add partial walls, doorway headers/portals (reuse existing Doorway prefab pattern: Post/Post/Header), and per-room floor + ceiling treatment. Keep sightlines from the concourse (glass/openings) so it still feels connected, not sealed.
- Assigned role: developer
- Dependencies: Step 4 (ceiling/soffit system), Step 3 (lighting)
- Parallelizable: No

**Step 8 — Re-mass the Bar into a lounge.**
- Description: Rework the bar so it no longer reads as a concession stand: reshape/relocate the counter (e.g., L-shape or island rather than a 12m straight run), add a lowered soffit + back-bar glow, cluster seating (booths/high-tops) instead of a single stool line, and give it a lounge threshold off the concourse. Ensure its placement/scale suits the space (user's specific concern).
- Assigned role: developer
- Dependencies: Step 7
- Parallelizable: No

**Step 9 — Convert the Arcade into an alcove.**
- Description: Recess the arcade into a defined alcove: enclose it with a framed portal entrance, lower/darken its ceiling, lean into the cosmic floor + cyan/magenta blacklight, and arrange cabinets to face inward around the space rather than in two open strip rows. Prize/ticket counter becomes the alcove's back feature wall.
- Assigned role: developer
- Dependencies: Step 7, Step 1 (glowing cabinet screens)
- Parallelizable: No (can overlap Step 8 if two developers)

### Phase 4 — Hero polish

**Step 10 — Feature set-pieces & signage.**
- Description: Add glowing lane-approach accents, dynamic/animated signage where cheap, ceiling feature lighting over the concourse/DJ stage, and per-room marquee signage that now glows. Final atmosphere balance pass.
- Assigned role: developer
- Dependencies: Steps 1–9
- Parallelizable: No

---

# Verification & Testing

- **Emission:** After Step 1, confirm each neon/screen/sign material has `_EMISSION` on and a non-black `_EmissionColor`; visually confirm glow in Scene view.
- **Glass:** After Step 2, confirm glass materials report Surface=Transparent and render see-through.
- **Lighting:** After Step 3, scene reads as a dim nightlife venue with legible light pools; no blown-out daylight; bloom visible on signage.
- **Enclosure:** After Step 4, ceiling present at y≈5.5; no holes visible from eye height; soffits present over Bar/Casino/Arcade.
- **Player:** After Step 5–6, first-person controller walks the full concourse and enters every room without getting stuck on decor; walls/counters block correctly; no floor z-fighting.
- **Architecture:** After Steps 7–9, each west room reads as its own space (portal + ceiling + light identity); Bar reads as a lounge, not a counter; Arcade reads as a recessed alcove. Sanity-check against reference: "would an interior designer accept these placements?"
- **Console:** Zero new errors/warnings introduced (check Unity Console after each phase).
- **Rollback:** Confirm scene is under version control / a duplicate scene backup exists before Phase 3 structural edits.

---

# Open Questions / Assumptions
- Assumed we keep the existing single scene and edit in place (backup/rollback available).
- Assumed no gameplay systems (scoring, interaction) in this pass — environment + navigability only.
- The exact new bar shape and arcade layout will be proposed visually during Phase 3; if you have a specific reference image, share it before Step 8.
