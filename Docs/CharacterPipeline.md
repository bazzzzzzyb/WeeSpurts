# Character Pipeline — how characters get into Wee Spurts, permanently

**Written 2026-08-11.** This is the reference doc for anything involving the player character: rigs,
animation, emotes, faces, cosmetics. It exists because Tony has swapped characters three times and hit
the same wall each time. The wall is explained in §1, and §2 is the fix.

If a future decision contradicts this doc, update this doc. Don't work around it.

---

## 1. Why your character is gooey, precisely

I read the bone list straight out of `Meshy_AI_Bowling_Mascot_Rig_biped_Animation_Idle_3_withSkin.fbx`.
Here is the entire skeleton:

```
Hips → Spine → Spine01 → Spine02 → neck → Head → head_end
                          ├─ LeftShoulder  → LeftArm  → LeftForeArm  → LeftHand
                          └─ RightShoulder → RightArm → RightForeArm → RightHand
Hips → LeftUpLeg  → LeftLeg  → LeftFoot
     → RightUpLeg → RightLeg → RightFoot
```

**21 bones. No fingers. No toes. No blendshapes.**

Three facts follow from that, and they are the whole story:

**It has all 15 bones Humanoid requires.** So the conversion will map. The "unmapped optional bones"
warning you'll see is fingers and toes, and it is correct and harmless — a party game does not animate
fingers.

**It has no blendshapes, so facial expressions can never be done with this mesh the usual way.** Your
six face states (idle / effort / dread / triumph / despair / gloat) cannot be morph targets. That
sounds like bad news. It isn't — see §4, where it turns out the cheap method is also the *right* method
for your art direction.

**The gooey arms are a skinning problem, and it's baked into the mesh, not the rig.** Meshy generated
your character in a relaxed pose with the arms hanging close to the torso, then auto-generated skin
weights. When arm geometry sits that near chest geometry, every automatic weighting algorithm — Meshy's,
Mixamo's, Blender's — cannot tell an armpit vertex from a chest vertex, so it assigns it to both. Move
the arm, and the chest comes with it. That is literally what "gooey" means here.

**This is why swapping characters never fixed it.** You changed the mesh and the rig, but you kept
using auto-generated weights on AI-generated bodies posed the same way. Same input, same output.

---

## 2. The fix, in order. Stop at the first one that works.

### Step 1 — Re-rig with AccuRIG 2 (free, ~30 minutes, do this first)

Reallusion's AccuRIG is free, standalone, and its whole selling point is exactly your problem: it is
specifically good at shoulder and hip deformation, where Mixamo's rigger has not been meaningfully
updated since the late 2010s and is inconsistent around the pelvis. It produces production-usable
weights on standard humanoids without cleanup.

1. Export your mascot mesh (you can pull it out of the FBX in Blender, or re-download the base model
   from Meshy without animation).
2. Run it through AccuRIG. Place the joint markers — it will guess, you nudge.
3. Export FBX.
4. Drop it in `Assets/_Project/Characters/`, point `CharacterSetupTool`'s `CharacterModelPath` at it,
   re-run the tool.

**Why this is safe now and wasn't before:** you converted to Humanoid this morning. Humanoid
retargeting is bone-*name*-agnostic — it maps any skeleton onto a normalized rig. So AccuRIG naming its
bones differently from Meshy costs you nothing. Every animation you have keeps working. **That
conversion is what made the character swappable at all**, which is the real thing you bought today.

### Step 2 — If it's still gooey, the mesh itself is the problem

Then the arm geometry is genuinely fused or too close to the torso, and no rigger will save it. Two
ways out, cheapest first:

**Regenerate from Meshy in a T-pose.** Go back to Meshy and generate the mascot with arms straight out
to the sides, clearly separated from the body. This is the single most important thing you can do when
generating any character for a game, and nobody tells you: **an AI character posed with arms down is
unriggable; the same character in a T-pose rigs cleanly.** Same prompt, same look, one word about the
pose, completely different outcome.

**Or paint the weights in Blender.** ~2 hours including learning it, once, forever. Select the mesh,
Weight Paint mode, pick the `LeftArm` bone, and scrub the chest vertices back down to zero. It is
genuinely the most useful Blender skill for a game dev and it's less scary than it sounds. Worth
learning eventually regardless.

### Step 3 — If you're an hour in and losing the day, use a placeholder

Your Quaternius bodies (`Male_Casual`, `Male_Shirt`, `Male_Suit`, `Male_LongSleeve`) are already in the
repo, professionally skinned, and will convert to Humanoid cleanly. Point the tool at one, keep moving,
come back to the mascot on a day when it's the only thing you're doing.

**Because of §3, doing this costs you nothing later.** That's the entire point of the next section.

---

## 3. The architecture that makes this the last time you fight it

Four layers. Keep them separate and the character becomes a swappable part instead of a foundation.
Fuse any two and you're back here in a month.

### Layer 1 — The skeleton. One Humanoid rig, forever.

Every character mesh, every animation, retargets through Unity's Humanoid avatar. This layer **never
changes again**. It is the contract.

What it buys you: any animation from any source works on any character from any source. Mixamo clips on
a Synty body. A Meshy mascot doing a Quaternius walk. You are never again blocked on "will this
animation work with my character."

**The rule:** every FBX that enters this project is `animationType = Human`. No exceptions, no Generic,
no "just for this one." Generic is what made the last three months painful.

### Layer 2 — The body mesh. Swappable, disposable, not precious.

The mesh is a skin over the skeleton. Changing it should be one line in `CharacterSetupTool` and a
re-run. If changing your character ever requires touching animation, gameplay, or camera code, that's a
bug in this layer.

**The rule:** nothing outside `CharacterSetupTool` may reference the character mesh, its materials, or
its bone names. Gameplay code asks the `Animator` for what it needs and never reaches into the
hierarchy by path.

### Layer 3 — Attachments. Hats, held items, props — parented to bones, never modelled in.

Anything a character wears or holds is a separate prefab attached at runtime to a bone you fetch with
`Animator.GetBoneTransform(HumanBodyBones.Head)` (or `RightHand`, `Spine`, etc.). This is a real Unity
API, it works on any Humanoid, and it is bone-name-agnostic — so it survives every character swap in
Layer 2.

This one API gets you, with no extra systems:

- Hats, crowns, the dunce cap, the humiliation cosmetics
- The bowling ball in his hand, and every other carried item in Block 6
- A drink he's holding, cards at the blackjack table, the mop
- Anything you think of later

**The rule:** an attachment is `(itemId, HumanBodyBones, offset)`. Stored as ints and an enum, which
replicates over Mirror for free. Never an object reference. This is `SlopLayerPlan.md` Rule 3 applied
to the character.

### Layer 4 — Look. Tints and faces, via material properties. Never new materials.

Colour customization is a `MaterialPropertyBlock` set on the renderer — **not** a new material
instance. `renderer.material` silently clones the material every time you touch it, which breaks
batching and leaks one material per player per session. Use `MaterialPropertyBlock` and
`renderer.SetPropertyBlock()`.

Faces are §4.

**The rule:** a player's look is a small struct of ints and colours. It goes over the wire as that
struct. Every client resolves it locally into meshes and property blocks.

---

## 4. Faces — the six states, and why blendshapes are the wrong answer for you

Your mascot has no blendshapes, so the usual approach is off the table. Good, because it was the wrong
approach anyway.

**Do faces as a texture atlas.** One texture, a grid of face expressions — idle, effort, dread,
triumph, despair, gloat. The head material's UV offset selects which cell shows. Changing expression is
setting two floats in a `MaterialPropertyBlock`.

Why this is genuinely better for Wee Spurts and not a compromise:

- **It matches your art direction.** Miis, Animal Crossing, Fall Guys, Among Us — the entire readable-
  stylized register uses flat drawn faces. Wii Sports is your stated reference and this is what Wii
  Sports does.
- **It reads at distance.** A blendshape frown is invisible from across the alley. A drawn one is legible
  from the far end of the lane, which is exactly where your reaction director will be cutting from.
- **It is free.** Six drawings and a UV offset, versus a facial rig you cannot make and Meshy cannot
  give you.
- **It survives character swaps.** The atlas is Layer 4. Change the body, keep the faces.
- **It expands trivially.** A seventh expression is one more cell. A whole alternate face set — a
  different character's personality — is one more texture.
- **It replicates as an int.** Which is what `DesignTerritories.md` §7's reaction director needs.

**Do this when you get to the reaction director, not today.** But make the head its own material now, or
at least know that it needs to be, so the atlas has somewhere to live.

---

## 5. What this means for your stated goals

| You want | It lives in | Cost, once §3 exists |
| --- | --- | --- |
| New emotes and taunts | Layer 1 | Drop an FBX in `Animations/Mixamo/`, add an animator state. Minutes. |
| Six face states | Layer 4 | Six drawings, one atlas, one property block. Hours. |
| Hats and cosmetics | Layer 3 | A prefab + an id + a bone. Minutes each. |
| Colour tints | Layer 4 | Already free. |
| Humiliation cosmetics (winner dresses loser) | Layer 3 + 4 | It's just an attachment id set by someone else. The system doesn't care who chose it. |
| Ball, drinks, cards, mop in hand | Layer 3 | Same system as hats. Build it once in Block 6. |
| Replacing the mascot entirely | Layer 2 | One path change, one re-run. **Nothing else breaks.** |
| Different body types / modular clothing | — | The one thing this doesn't give you. Needs a modular character set (Synty and similar ship them). Not v1 — `Projects.md` §E says hats and tints are v1, and that's the right call. |

---

## 6. The two rules that would have saved you three months

1. **Generate and rig characters in a T-pose, arms clearly away from the body.** Every auto-rigger fails
   on arms-down meshes and none of them warn you.
2. **Everything is Humanoid.** It costs one checkbox and it makes the character a swappable part instead
   of a decision you're stuck with.

---

## 7. Today's order of operations

1. Finish reading the console from this morning's `Set Up Player Character` run. Confirm the 15 required
   bones mapped. Fingers and toes unmapped is expected and fine.
2. Fix `MascotConfig.DisplayScale` — it's `0.7`, which puts him at ~2.2 m. Use the number the tool now
   prints for a 1.75 m target.
3. Download the Mixamo clips (`Docs/Prompts/2026-08-11-executive-day-plan.md` §4) and re-run. **Judge the
   arms after this**, not before — you're still playing stiff Meshy clips right now, so the current look
   is not the verdict.
4. If the arms are still gooey with good clips on them: AccuRIG, §2 Step 1.
5. If AccuRIG doesn't fix it: regenerate from Meshy in a T-pose, §2 Step 2.

Do not let 4 and 5 eat the day. The rest of the plan does not depend on the character being pretty.
