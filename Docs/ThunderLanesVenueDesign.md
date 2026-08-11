# Game Design

This document establishes the interior-architecture, art-direction, and game-feedback specifications for the retro bowling entertainment venue. It addresses the "strip-mall of boxes" layout issue by defining clear structural thresholds, re-massing the bar into an intimate lounge, and enclosing the arcade into a sensory-dense alcove. 

These specifications are framework-agnostic. They describe the visual, structural, and tactile intent for developers and artists to execute.

---

## UI Design (Spatial & Signage UI)

Rather than layering flat 2D HUDs over the first-person camera, the user interface is integrated into the 3D world as tactile, physical components—neon marquees, CRT terminal screens, glowing scoreboard monitors, and interactive kiosks.

### Color System
To maintain a high-contrast nightlife atmosphere, the spatial UI uses a dark, rich neutral base punctuated by vibrant neon emitters.
*   **Primary Base:** Rich walnut wood tone (`#3D2314`) and deep burgundy leather (`#4A121A`). Used for structural framing, terminal housings, and sign backings.
*   **Secondary Base:** Dark charcoal steel (`#1A1A1C`) and brushed brass (`#C5A059`). Used for metal enclosures, brackets, and trim.
*   **Accent (Interactive):** Gold/Warm Amber neon (`#FFB000`) for high-legibility transactional text, shoe size selection, and point-of-sale screens.
*   **Accent (Branding & Identity):** Vibrant Cyan neon (`#00E5FF`) and Hot Magenta neon (`#FF007F`). Reserved for room branding, signage outlines, and jackpot indicators.
*   **Intent Mapping:** 
    *   *Cyan/Magenta:* General navigation and branding.
    *   *Warm Gold:* Transactional prompts, interactive buttons, and functional kiosks.
    *   *Red-Alert:* "Lane Fault" screens, "Staff Only" back-of-house indicators, and emergency exits.

### Typography
Spatial typography must command attention at a distance while remaining legible on curved low-resolution CRT meshes.
*   **Headline/Display Font (Core):** Bold, heavy sans-serif with a geometric, retro-futuristic silhouette (reminiscent of late-90s arcade branding). Used for acrylic neon-backlit marquees, arcade headers, and room entry signs.
*   **Body/Data Font (Core):** Monospaced, high-legibility pixel/dot-matrix font. Used for overhead lane scoreboards, CRT terminal screens, and ticket counter readouts.
*   **Label Font (Optional):** Technical, clean sans-serif at small scales. Used for tiny instructional labels on kiosks, token slots, and POS terminals.
*   **Rule of Scale:** Exaggerate proportions. Large, blocky category headers (e.g., "SHOES" in 72pt equivalent) paired with tight, high-contrast values (e.g., "SIZE: 10" in 24pt monospaced gold).

### Layout & Depth
*   **Depth Strategy (Core):** All in-world UI screens must avoid being flat, printed textures. They must use a physically layered sandwich construction:
    1.  *Back Plate:* Dark, non-reflective metal or wood cabinet inset.
    2.  *Glow Layer:* Emissive CRT screen curved glass mask or physical neon tube geometry.
    3.  *Foreground Layer:* Bezel trim, protective acrylic glass, or brass framing that casts a physical shadow over the emissive element.
*   **Asymmetry (Optional):** Neon signs should feature asymmetrical mounting arms, projecting diagonally from columns or walls rather than sitting flush, breaking the sterile box grids.

### Components
*   **Scoreboard Monitors (Core):** Multi-layered overhead assemblies hung from structural ceiling beams at the lane approaches. Encased in rounded charcoal-grey plastic shells, featuring a dual-CRT layout with a prominent glass curvature and bright cyan-on-black scoring matrices.
*   **Interactive Kiosks (Core):** Inset walnut-clad pedestals with a 15-degree angled brass face plate. The screen itself is recessed 2cm below the plate to convey a sense of tactile thickness and depth.
*   **Neon Marquees (Core):** Dimensional channel letters with a dark metal backer plate, a middle layer of glowing neon tubing, and a front clear acrylic cap. 

---

## Asset Design

The visual identity targets a retro-luxe late-90s/early-2000s nightlife entertainment complex. Every asset must look like a physical, heavy object that belongs in a cohesive, dimly lit world.

### Visual Identity
*   **Style:** Retro-luxe. Think polished wood, heavy brass, padded leather, and rich patterns meets high-voltage kinetic electricity. No clean, minimalist, flat-white modernism.
*   **Color Temperature:** Extremely warm ambient base (2200K–2700K halogens, wood-toned reflections) contrasted with highly saturated, cold neon pools (cyan, magenta, ultraviolet).
*   **Detail Level:** Medium-to-high textural detail on touchable surfaces (grain on walnut, scuffs on leather, brushed grain on brass). Keep the geometric silhouette of walls and columns simple and bold, focusing detail on furniture and fixtures.

### Material & Color Palette
The material palette is strictly controlled across three categories:

| Category | Dominant (60%) | Secondary (30%) | Accent (10%) |
| :--- | :--- | :--- | :--- |
| **Architecture** | Matte Charcoal Drywall, Exposed Brick | Dark Walnut Wood Panels | Polished Brass Trim, Neon Tubing |
| **Furniture** | Burgundy/Charcoal Padded Leather | Dark Walnut Timber | Brushed Brass Footrails, Grommets |
| **Arcade & Play** | Cosmic Confetti Carpet | Matte Black Plastic Cabinet Casings | Glowing CRT Screens, Cyan/Magenta T-Molding |

### Composition & Spatial Partitioning (Architectural Fixes)

#### A. West Wall Structural Redesign: The "Anti-Strip-Mall" Enclosure Strategy (Core)
To eliminate the flat "strip-mall food court" appearance, the West Wing (X≈-13 to -30) is divided into a sequence of architectural pockets using deep partitions, portal entries, ceiling offsets, and floor transitions:
1.  **Deep Spatial Partitions:** Erect full-height structural partition walls projecting from the west exterior wall (X≈-30) out to X≈-13. These partitions are placed at key boundary lines:
    *   *Z = -23.5:* Separates Entrance Vestibule from the Front Desk.
    *   *Z = -6.25:* Separates Front Desk from the Snack Shack.
    *   *Z = 10.0:* Separates Snack Shack from the Bar Lounge.
    *   *Z = 25.0:* Separates Bar Lounge from the Casino.
2.  **Portal & Sightline Management:** To prevent these spaces from feeling like dark, isolated boxes, cut large arched portal openings (3.5m wide, 4.0m high) in each partition wall along the Z-axis. Frame these portals with thick walnut molding and brass insets. This creates a spectacular "layered telescope" view when looking down the wing from north to south, while physically bounding each room.
3.  **Ceiling Height Variations:** Introduce a stepped ceiling hierarchy to contract and expand space dynamically:
    *   *Main Concourse:* High, open ceiling at Y = 5.5m with exposed structural steel roof trusses painted matte charcoal.
    *   *Front Desk:* Lowered to Y = 4.0m with a warm, flat walnut-plank ceiling.
    *   *Snack Shack:* Lowered to Y = 3.6m with bright, square metal ceiling tiles.
    *   *Bar Lounge:* Lowered to Y = 3.2m using a deep, suspended drywall soffit.
    *   *Casino:* Double-tier coffered ceiling rising to Y = 4.8m with a central circular gold neon light cove.
4.  **Floor Material Transitions:** Ground each zone with distinct flooring, meeting at clean brass transition thresholds aligned with the colonnade line (X = -9.8):
    *   *Concourse:* Dark speckled terrazzo with inlaid geometric brass lines.
    *   *Front Desk & Casino:* Dense, patterned burgundy/charcoal carpet.
    *   *Snack Shack:* 12"x12" black-and-white checkerboard linoleum tiles.
    *   *Bar Lounge:* Warm, dark-stained walnut herringbone parquet wood floor.

```
       WEST EXTERIOR WALL (X = -30)
 _________________________________________________
|             |             |             |       |
|   CASINO    | BAR LOUNGE  | SNACK SHACK | FRONT |
|   (Raised)  |  (J-Bar)    | (Diner Tile)| DESK  |
|_____________|_____________|_____________|_______| Z = -6.25
        Z = 25.0      Z = 10.0      Colonnade (X = -9.8)
================== PORTAL ARCHWAYS ================
   [Column]      [Column]      [Column]   [Column]
                                              
               CENTRAL CONCOURSE (X = 0)
```

#### B. The Bar Lounge Redesign (Core)
Rework the straight 12-meter concession counter into an intimate, high-end destination lounge at Z≈17.5:
*   **Faceted J-Shape Counter:** Break the straight line. The counter should be a faceted "J" shape, starting from the back wall (X = -25, Z = 13.5), projecting east to X = -16, running north, and returning west to X = -25 at Z = 21.5. This wraps the bar area and creates a cozy alcove for the bartender.
*   **Bar Detailing:** Clad the bar front in vertically-grooved walnut tambour wood, topped with a generous, rounded burgundy-leather padded armrest and supported by a continuous polished brass footrail.
*   **Suspended Soffit:** Hang a suspended walnut-veneer soffit directly above the counter at Y = 3.5m, tracing the J-shape of the bar. It houses recessed low-voltage warm amber pinspots that cast pool-like highlights on the counter.
*   **Back-Bar Display:** The back wall features a symmetrical arched mahogany display cabinet with mirror backing and glass shelving, illuminated by concealed warm gold LED strip lighting.
*   **Seating Clusters:** Remove the single straight row of stools. Instead, place 6 heavy swivel barstools (burgundy leather, brass bases) at the counter. In the surrounding lounge area, arrange:
    *   *High-Top Tables (Optional):* Three round walnut cocktail tables with brass legs and high-back charcoal leather stools near the concourse border.
    *   *Cozy Booths (Core):* Two semi-circular, high-backed burgundy leather booths tucked into the corners under custom brass dome pendant lights.

#### C. The Arcade Alcove Redesign (Core)
Transform the open arcade quadrant (X:[15, 28], Z:[-25, 0]) into a dark, immersive "sensory cocoon":
*   **Portal Entrance Enclosure:** Build a full-height wall along X = 12, enclosing the arcade. Cut a dramatic, angled portal entrance at Z = -15 to -10. The portal frame is lined with dual concentric rows of cyan and magenta neon tubing, drawing players in.
*   **Blacklight Cosmic Mood:**
    *   *Floor:* Custom fluorescent "cosmic carpet"—black backing with glowing neon confetti shapes (splashes, stars, triangles) that react intensely to ultraviolet light.
    *   *Walls & Ceiling:* Painted deep, non-reflective midnight blue/matte charcoal to absorb stray light and maximize screen-glow contrast.
    *   *Ambient Light:* Purely indirect. Ambient light is provided by blacklight UV tubes concealed behind overhead wall valances, making the floor and cabinet graphics fluoresce.
*   **Inward-Facing Cabinet Layout:** Position the 12 cabinets back-to-back or flush against the outer walls, facing inward toward the center of the room. This layout traps the dynamic, flickering CRT light within the alcove, preventing screen glare and creating a swirling vortex of color.
*   **Prize Wall & Ticket Center:** The back wall (X ≈ 27) acts as the focal centerpiece. A low, glass-top display counter with glowing magenta trim houses high-tier prizes. Behind it, a massive black pegboard wall displays retro electronics, giant plush toys, and neon clocks, lit by overhead directional warm spotlamps.

---

## Game Feedback

Atmosphere is maintained through dramatic, sensory-rich feedback. Player interactions must feel tactile, heavy, and physically grounded.

### Genre Profile
*   **Profile:** Atmospheric / Exploration.
*   **Rationale:** As a first-person walkthrough, the feedback must feel physical and spatial. The contrast between loud, bright zones and dim, quiet corners drives the player's emotional journey. Silence and lighting transitions are utilized as primary narrative drivers.

### Spatial Interaction Map

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Input | Rationale |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Entering Vestibule** | `(core)` | Minor | — | — | — | — | Dynamic audio transition: muffled bass hum becomes crisp spatial lobby track. | — | Establishes the boundary between the outside world and the venue. |
| **Transition into Arcade** | `(core)` | Medium | Slow, subtle camera zoom inward (field-of-view narrows by 5%) to simulate tunnel vision. | — | — | Global color grading shifts: desaturate warm yellows, heavily boost cyan, magenta, and ultraviolet. | Muffled electronic arcade chiptunes and mechanical coin drops fade up. | Slight movement dampening (-10% walk speed) to encourage looking around. | Sells the "sensory-overload cocoon" transition. |
| **Transition into Casino** | `(optional)` | Medium | — | — | Height change: player steps up +1.2m steps. | Warm gold light pools wash screen; spotlit green felt tables stand out. | Sudden swell of coin clinks, electronic slot fanfares, and soft jazz back-track. | — | Enhances the feeling of stepping onto an exclusive, high-stakes platform. |
| **Striking All Pins (Strike)** | `(core)` | Critical | High-frequency, rapid decay camera shake (0.15s duration). | Brief hitstop (0.05s freeze frame) at the exact moment of pin impact. | Pins explode outwards with high-velocity physics. | Dynamic neon sequence: overhead lane marquees flash cyan/magenta; pin deck strobe flashes white (2 frames). | A booming wooden "CRACK" of the ball hitting pins, followed by a roaring crowd cheering sample (retro synth chip style). | Haptic rumble pulse (exponential decay, 1.0s) on controller. | The ultimate gameplay payoff; must feel incredibly visceral and heavy. |
| **Inserting Token to Cabinet** | `(optional)` | Minor | Tiny, quick 1-degree camera nod downward. | — | Kiosk coin-slot slider physically retracts and springs back. | High-intensity flash on the cabinet's CRT screen; coin slot LED flashes green. | A metallic "clink-clonk" coin-drop sound, followed by an iconic 8-bit cabinet start fanfare. | Short, sharp haptic click. | Reinforces the tactile, physical reality of the machines. |

### Architectural Assets Needed

| Asset Name | Tier | Type | Style Reference | Palette (Dominant/Accent) | Usage Context | Approximate In-Scene Size |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Arched West Portal** | `(core)` | Mesh (Modular Wall) | Existing mahogany doorways | Dark Walnut / Polished Brass | Placed at Z partitions along the west wing to divide spaces. | 4.5m (W) x 4.5m (H) x 0.6m (D) |
| **J-Bar Counter Assembly** | `(core)` | Mesh (Complex Furniture) | 1990s hotel lounges | Walnut Tambour / Burgundy Leather | Primary interactive bar counter in the Lounge wing (Z≈17.5). | 6.0m (W) x 1.1m (H) x 4.5m (D) |
| **Arcade Portal Header** | `(core)` | Mesh (Signage) | Classic neon entertainment facades | Dark Charcoal Steel / Neon Cyan & Magenta | Suspended frame capping the entrance to the Arcade Alcove. | 5.0m (W) x 1.5m (H) x 0.8m (D) |
| **Cosmic Carpet Texture** | `(core)` | Material | 90s Laser-Tag/Arcade flooring | Black Wool / Neon Cyan, Magenta, Gold | Flooring across the entire Arcade Alcove space. | Tilable 2m x 2m |
| **Suspended Bar Soffit** | `(core)` | Mesh (Ceiling) | Modernist-retro drop ceilings | Walnut Veneer / Amber LED Glow | Ceiling structure hung above the J-Bar. | 6.0m (W) x 0.4m (H) x 4.5m (D) |
| **Arched Back-Bar Cabinet** | `(optional)` | Mesh (Furniture) | Luxury retro cocktail lounges | Mahogany Wood / Gold LED Emitter | Main display wall behind the bartender. | 4.0m (W) x 3.0m (H) x 0.5m (D) |
| **Casino Balustrade** | `(optional)` | Mesh (Partition) | High-end retro cruise ship casinos | Polished Brass / Burgundy Leather padding | Border railing wrapping the raised Casino platform (+1.2m). | Modular 2.0m panels |

### Event Feedback Sequences

#### The Arcade Entry Sequence
`Player crosses portal plane (X = 12.0) → Sound level of main concourse drops by 50% using low-pass filter (0ms) → Cosmic carpet textures begin to fluoresce under simulated UV lighting (100ms) → Ambient screen wash on player's face turns cyan/magenta (200ms) → Spatialized arcade machine loops fade to full volume (300ms).`

#### The Bowling Strike Celebration Sequence
`Ball impacts head pin (0ms) → Game freezes for 0.05s (Hitstop) (0ms) → Visceral wooden impact audio plays (0ms) → Camera executes high-frequency directional shake (50ms) → Overhead CRT score monitors play a pixelated "STRIKE!" flashing animation (100ms) → Cyan/Magenta neon lane tracers flash in a cascading chasing sequence towards the pin deck (150ms) → Synthetic crowd cheers swell and peak (300ms) → Sweep-sweep mechanical arm drops to clear dead wood (1000ms).`

---

## Developer Execution Checklist

This prioritized list is organized into executable milestones. Core tasks establish the minimum spatial recognition, while Optional tasks layer on fidelity.

### Phase 1: Spatial & Architectural Boundaries (Core)
*   [ ] **West Wing Partitions:** Model and place four full-height partition walls at Z = -23.5, Z = -6.25, Z = 10.0, and Z = 25.0 from X = -30 to X = -13. `(core)`
*   [ ] **Walnut Portal Frames:** Cut 3.5m x 4.0m openings in the partitions along the Z-axis and apply the walnut/brass-molded arched portal meshes. `(core)`
*   [ ] **West Floor Separation:** Re-assign flooring materials: Checkerboard linoleum tiles for the Snack Shack (Z≈2.5) and herringbone wood parquet for the Bar (Z≈17.5). `(core)`
*   [ ] **Arcade Portal & Enclosure:** Erect the enclosure wall at X = 12 spanning Z = -25 to 0, cut the angled entryway, and mount the physical neon tube portal headers. `(core)`

### Phase 2: Bar & Arcade Re-Massing (Core)
*   [ ] **J-Shape Bar Counter:** Remove the 12m straight bar mesh. Replace it with the J-shaped modular walnut-tambour counter centered at Z = 17.5. `(core)`
*   [ ] **Suspended Bar Soffit:** Model and hang the walnut ceiling soffit at Y = 3.5m directly over the new J-bar footprint. `(core)`
*   [ ] **Arcade Inward Layout:** Rearrange the 12 cabinet meshes to face inward along the arcade boundaries, removing the sterile row alignment. `(core)`
*   [ ] **Arcade Blackout Materials:** Apply deep charcoal paint to the arcade walls/ceiling and assign the Cosmic Carpet material to the floor. `(core)`

### Phase 3: Lighting & Atmospheric Tuning (Core)
*   [ ] **Enable Emissives:** Enable emission keywords and restore authored HDR colors on neon, TVs, and screen materials. `(core)`
*   [ ] **Ceiling Plane & Soffit Fixtures:** Add a solid ceiling plane at Y = 5.5m. Place recessed warm lights in the lowered ceiling bays and UV blacklights in the arcade ceiling valances. `(core)`
*   [ ] **Nightlife Ambient Shift:** Drop the Global Directional light intensity to 0.15. Adjust ambient lighting to a dark warm gradient. `(core)`

### Phase 4: Ergonomic & Spatial Polish (Optional)
*   [ ] **Casino Brass Balustrade:** Replace the Casino low half-walls with the polished brass and padded burgundy leather balustrade. `(optional)`
*   [ ] **Arched Back-Bar Display:** Add the arched mahogany mirror-back display behind the J-bar. `(optional)`
*   [ ] **Lounge Seating Clusters:** Place the semi-circular burgundy booths in the corners of the bar lounge and high-tops near the concourse edge. `(optional)`
*   [ ] **Arcade Prize Wall:** Assemble the prize ticket counter and pegboard prize wall at X ≈ 27. `(optional)`
*   [ ] **Spatial Audio Triggers:** Place audio triggers at the vestibule, bar, and arcade portals to crossfade audio tracks and apply low-pass filters. `(optional)`
