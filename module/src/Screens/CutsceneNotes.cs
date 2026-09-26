using System;
using System.Collections.Generic;
using Echopunks.Localization;

namespace Echopunks.Screens
{
    /// <summary>
    /// Mod narration inserted into a game cutscene script: Moss recalling how EXODUS is laid
    /// out, in the mod's terms, during the Ghast visit that hands over the zine (user decision,
    /// 2026-09-26 — the first mod-authored prose inside the game; the text lives in ui.json under
    /// cutscene.notes.ghast1.*).
    ///
    /// Why this scene and this player: the visual-novel player (deob GClass255) is the one place
    /// where narration is native. Moss lines draw plate-less and italic, advance like any other
    /// line, and carry NO voice clip — the player picks clips by counting NON-Moss lines, so Moss
    /// insertions never shift the audio — and the mod's CutsceneScreen already speaks them bare.
    /// The EMBER comic player is NOT usable for this: a Moss line there is a pick-me reply bubble
    /// and the previous line is drawn in EMBER's speech slot (both verified live, 2026-09-26).
    ///
    /// Placement rules (from the player's code): never index 0 — the player reads the first
    /// line's door action to set the opening state; never after the game's last line — the seen
    /// flag and the zine-reader push fire when the player advances past it. The block goes in
    /// after Moss's "Apparently, it's something called TRASH WORLD NEWS..." and before Ghast
    /// resumes, and Ghast's "I can't hack like I used to." lands as the answer to its last line.
    ///
    /// Lifecycle: inserted in memory at the module's first tick (the campaign lists are built by
    /// game init), removed BY REFERENCE in Dispose so hot reloads never stack copies; a
    /// text-identity guard covers a generation whose Dispose failed. Nothing on disk changes.
    /// </summary>
    internal static class CutsceneNotes
    {
        private const string SceneId = "ghast-1";

        /// <summary>Parsed-script index the block is inserted at: the shipped script has Moss at
        /// index 6 (the TRASH WORLD NEWS line) and Ghast at 7. Verified against both before
        /// inserting, so a changed script skips loudly instead of landing mid-conversation.</summary>
        private const int InsertAt = 7;

        private static readonly string[] Keys =
        {
            "cutscene.notes.ghast1.01", "cutscene.notes.ghast1.02", "cutscene.notes.ghast1.03",
            "cutscene.notes.ghast1.04", "cutscene.notes.ghast1.05", "cutscene.notes.ghast1.06",
            "cutscene.notes.ghast1.07", "cutscene.notes.ghast1.08", "cutscene.notes.ghast1.09",
            "cutscene.notes.ghast1.10", "cutscene.notes.ghast1.11", "cutscene.notes.ghast1.12",
            "cutscene.notes.ghast1.13", "cutscene.notes.ghast1.14",
        };

        private static readonly List<GClass275> _inserted = new List<GClass275>();

        public static void Apply()
        {
            try
            {
                var lines = ScriptLines();
                if (lines == null) { Log.Warning("[cutscene notes] " + SceneId + " has no script; nothing inserted"); return; }
                if (lines.Count <= InsertAt
                    || lines[InsertAt - 1].vignetteCharacter_0 != VignetteCharacter.Moss
                    || lines[InsertAt].vignetteCharacter_0 != VignetteCharacter.Ghast)
                {
                    Log.Warning("[cutscene notes] " + SceneId + " script shape changed (" + lines.Count + " lines); nothing inserted");
                    return;
                }
                string first = Loc.T(Keys[0]);
                foreach (var line in lines)
                    if (line.vignetteCharacter_0 == VignetteCharacter.Moss && EnglishOf(line) == first)
                    {
                        Log.Info("[cutscene notes] already present in " + SceneId + "; nothing inserted");
                        return;
                    }
                for (int i = 0; i < Keys.Length; i++)
                {
                    var line = new GClass275(VignetteCharacter.Moss, MakeLocString(Loc.T(Keys[i])));
                    lines.Insert(InsertAt + i, line);
                    _inserted.Add(line);
                }
                Log.Info("[cutscene notes] inserted " + Keys.Length + " lines into " + SceneId + " at " + InsertAt);
            }
            catch (Exception ex) { Log.Error("[cutscene notes] insert failed", ex); }
        }

        public static void Remove()
        {
            try
            {
                var lines = ScriptLines();
                if (lines != null)
                    foreach (var line in _inserted) lines.Remove(line);
                _inserted.Clear();
            }
            catch (Exception ex) { Log.Error("[cutscene notes] removal failed", ex); }
        }

        private static List<GClass275> ScriptLines()
        {
            var item = GClass61.smethod_4(SceneId);
            if (!item.method_0()) return null;
            return item.method_2().vignette_0?.list_0;
        }

        private static string EnglishOf(GClass275 line)
        {
            if (line.list_0 == null || line.list_0.Count == 0 || line.list_0[0] == null) return null;
            string s;
            return line.list_0[0].dictionary_0.TryGetValue(Language.English, out s) ? s : null;
        }

        /// <summary>The game's LocString has NO language fallback (ToString indexes the current
        /// language and throws on a missing key), so every slot is filled — with the mod's own
        /// localized text, which is what the mod's loc layer has already picked.</summary>
        private static LocString MakeLocString(string text)
        {
            var ls = new LocString();
            foreach (Language lang in Enum.GetValues(typeof(Language)))
                ls.dictionary_0[lang] = text;
            return ls;
        }
    }
}
