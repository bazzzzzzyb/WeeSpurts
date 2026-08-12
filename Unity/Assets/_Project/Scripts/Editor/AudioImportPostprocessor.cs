using UnityEditor;
using UnityEngine;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// STANDING IMPORT RULE, same shape and reasoning as
    /// <see cref="ArtTexturePostprocessor"/>: any audio file landing under
    /// <c>Assets/_Project/Audio/</c> whose name says it's a long loop
    /// ("Ambience"/"Music") gets Streaming + Vorbis on first import, rather
    /// than Tony having to remember to flip those two dropdowns by hand every
    /// time. Short one-shot SFX are left at Unity's own defaults, which are
    /// already sane for anything a few seconds long.
    ///
    /// WHY THIS MATTERS HERE SPECIFICALLY: the exec-day audio drop included
    /// multiple 25-125MB ambience/music WAVs. Unity's default Load Type
    /// (Decompress On Load) decompresses the WHOLE clip into memory up front —
    /// fine for a gutter thunk, a serious problem for a two-minute room-tone
    /// loop. Streaming reads off disk in chunks instead, and Vorbis keeps the
    /// on-disk/import size down too.
    ///
    /// Verified against docs.unity3d.com/ScriptReference/AssetPostprocessor.OnPreprocessAudio.html
    /// (CLAUDE.md rule 3) — a real, documented callback, called once per clip
    /// before its FIRST import. A later Inspector hand-override by Tony sticks;
    /// this only ever sets the value on the way in.
    /// </summary>
    public class AudioImportPostprocessor : AssetPostprocessor
    {
        private const string AudioFolder = "Assets/_Project/Audio/";
        private static readonly string[] LoopNameHints = { "ambience", "music" };

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioFolder, System.StringComparison.Ordinal)) return;

            string fileNameLower = System.IO.Path.GetFileName(assetPath).ToLowerInvariant();
            bool isLongLoop = false;
            foreach (string hint in LoopNameHints)
            {
                if (!fileNameLower.Contains(hint)) continue;
                isLongLoop = true;
                break;
            }
            if (!isLongLoop) return;

            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            importer.defaultSampleSettings = settings;
        }
    }
}
