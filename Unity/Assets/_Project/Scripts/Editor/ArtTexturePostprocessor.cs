using UnityEditor;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// STANDING IMPORT RULES for Docs/ArtGuide.md's texture policy. Every
    /// texture that lands under one of the folders in <see cref="Rules"/> gets
    /// capped, compressed, and mipmapped automatically the moment Unity first
    /// imports it — nothing for Tony to remember to set by hand.
    ///
    /// WHY A POSTPROCESSOR AND NOT A ONE-OFF INSPECTOR TWEAK: Tony asked for
    /// these to be STANDING rules, not one-time fixes on today's assets — so
    /// they have to apply automatically to whatever textures land in these
    /// folders next, without anyone re-running a tool. OnPreprocessTexture is
    /// Unity's own hook for exactly this (called once per texture, right
    /// before the FIRST import, letting the importer's settings be set before
    /// Unity generates anything) — verified as a real, documented
    /// AssetPostprocessor callback rather than assumed (CLAUDE.md rule 3).
    ///
    /// WHY CHARACTERS GET MORE THAN PROPS: a prop is background dressing seen
    /// at lane distance, so 512 is generous. The character is the hero asset —
    /// the throw camera puts him close to the lens every single turn — so he
    /// gets 1024. Both are still a long way under the 4096 Meshy ships, which
    /// is the actual point: a 4096 character texture is ~16x the VRAM for
    /// detail nobody can see at this art direction.
    ///
    /// ONLY RUNS ON FIRST IMPORT (or after the .meta is deleted) — the same
    /// caveat every OnPreprocess* callback has. If Tony hand-overrides a
    /// texture's settings in the Inspector later, THAT sticks; this only ever
    /// sets the values once, on the way in, exactly like a sane default should.
    ///
    /// (Formerly PropTexturePostprocessor — renamed when the character rule
    /// landed, rather than adding a second near-identical file.)
    /// </summary>
    public class ArtTexturePostprocessor : UnityEditor.AssetPostprocessor
    {
        /// <summary>
        /// Folder prefix -> max texture size. Longest-prefix-wins is not needed
        /// today because these folders don't nest; if that ever changes, order
        /// this most-specific-first and the loop below already does the right
        /// thing (it stops at the first match).
        /// </summary>
        private static readonly (string Folder, int MaxSize)[] Rules =
        {
            ("Assets/_Project/Art/Props/", 512),
            ("Assets/_Project/Characters/", 1024),
        };

        private void OnPreprocessTexture()
        {
            foreach ((string folder, int maxSize) in Rules)
            {
                if (!assetPath.StartsWith(folder, System.StringComparison.Ordinal)) continue;

                var importer = (TextureImporter)assetImporter;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.mipmapEnabled = true;
                return;
            }
        }
    }
}
