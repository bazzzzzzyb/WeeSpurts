using UnityEngine;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// Forces a material into alpha-blended transparency. Lifted out of
    /// GreyboxSceneBuilder so CharacterSetupTool can use the same
    /// hard-won settings rather than growing a second, subtly-different copy —
    /// the greybox capsule and the rigged character must go see-through the
    /// same way or "roughly 50-70% opaque" means two different things.
    /// </summary>
    internal static class MaterialTransparency
    {
        /// <summary>
        /// Applies unconditionally, every call — unlike a color passed at
        /// material-creation time (which only ever takes effect the one time the
        /// .mat asset doesn't exist yet), so this also repairs a material that
        /// already exists on disk in a half-configured state, and re-applies a
        /// changed alpha on a re-run.
        ///
        /// Covers both shaders our tools may have picked — URP's
        /// "Universal Render Pipeline/Lit" (or Unlit) and the Built-in Standard
        /// shader — since Set*/Enable/DisableKeyword calls for a property or
        /// keyword that doesn't exist on the material's actual shader are
        /// documented no-ops, not errors, so applying both is safe.
        ///
        /// WHAT WE ACTUALLY CONTROL (verified against URP 17.5's Lit.shader and
        /// the resulting .mat, not assumed — an earlier version of this comment
        /// asserted the opposite and was wrong):
        ///
        ///   We set _Surface = 1 (Transparent) and _Blend = 0 (Alpha). URP
        ///   DERIVES the rest from those on material validation and will
        ///   overwrite what we put in the blend-state properties. In this
        ///   project it resolves to PREMULTIPLIED alpha with _SrcBlend = One and
        ///   _ALPHAPREMULTIPLY_ON enabled, because URP's Lit defaults
        ///   _BlendModePreserveSpecular to on. That is correct 60%-opaque
        ///   blending with specular preserved — it looks right, it is simply not
        ///   the SrcAlpha state the explicit sets below request.
        ///
        ///   So: treat _Surface/_Blend as the real controls and everything below
        ///   them as a best-effort fallback for the Built-in Standard shader
        ///   (Rendering Mode = Fade, _Mode = 2) and for any shader URP doesn't
        ///   post-process. Don't reason about final blend state from this file
        ///   alone — read the generated .mat.
        ///
        /// DEPTH WRITE IS DELIBERATELY NOT A KNOB HERE. Transparent URP
        /// materials always end up with _ZWrite = 0, so the character is
        /// see-through into ITSELF (far arm through torso, back of skull through
        /// the face). That is what alpha blending does to a closed mesh, it is
        /// what Wii Sports looks like, and it is not fixable by flipping a
        /// property: URP/Lit has no _ZWriteControl (that only exists on
        /// ShaderGraph-authored URP shaders), and URP's own shader GUI forces
        /// ZWrite off for any transparent surface. A previous version of this
        /// file exposed a depth-write toggle that silently did nothing. If the
        /// self-layering ever reads as broken, the lever is ALPHA — raise it
        /// toward 0.75 — not depth.
        /// </summary>
        public static void Apply(Material mat, float alpha)
        {
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
            // URP Lit reads the tint from _BaseColor; mat.color maps to _Color,
            // which URP keeps in sync for the main shaders but not for every
            // variant. Setting both makes the alpha stick regardless of which
            // property the imported material actually exposes.
            if (mat.HasProperty("_BaseColor"))
            {
                Color b = mat.GetColor("_BaseColor");
                b.a = alpha;
                mat.SetColor("_BaseColor", b);
            }

            // ----- URP Lit/Unlit -----
            mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);   // 0 = Alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // that's Premultiply mode, not Alpha
            mat.DisableKeyword("_ALPHAMODULATE_ON");    // that's Multiply mode, not Alpha

            // ----- Built-in Standard -----
            mat.SetFloat("_Mode", 2f); // 2 = Fade (matches SrcAlpha/OneMinusSrcAlpha below)
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // ----- Shared blend state (both pipelines read these via the same names) -----
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
