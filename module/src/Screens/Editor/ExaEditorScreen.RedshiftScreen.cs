using System;
using System.Reflection;
using Echopunks.Game;
using Echopunks.Localization;
using Echopunks.UI;
using Echopunks.UI.Graph;

namespace Echopunks.Screens
{
    public sealed partial class ExaEditorScreen
    {
        // ---- the console SCREEN as a sprite table (sandbox, armed only — the drawn screen
        // is dark while editing): one row per EXA with at least one pixel visible on the
        // 120x100 display, in reading order. The screen is only ever a composite of the live
        // EXAs' sprite state (GClass313.vmethod_3: bool_4 pattern at GX/GY, GZ parallax when
        // 3D), so the table IS the screen. Rows re-resolve per announce — browsing is polling
        // on demand; nothing is Live (positions change 30x/sec while the game plays, and play
        // mode stands the navigator down anyway — pause to orient, resume to play). A pattern
        // that matches one of the game's 100 font glyphs speaks as its character ("A",
        // "dot"), the rest as "custom shape, N pixels". ----

        private void BuildScreenStop(GraphBuilder b, EditorScreen e)
        {
            if (!SandboxMode(e)) return;
            bool armed = false;
            try { armed = e.method_0(); } catch { }
            if (!armed) return;
            var sim = TheSim(e);
            if (sim == null) return;
            var rows = new System.Collections.Generic.List<SimExa>();
            try
            {
                foreach (var entity in sim.list_1)
                {
                    var exa = entity as SimExa;
                    if (exa != null && VisiblePixels(exa) > 0) rows.Add(exa);
                }
            }
            catch { }
            if (rows.Count == 0) return;
            // Reading order, top-left first: GY 0 is the TOP of the drawn screen (glyphs load
            // top-down through the same index math the compositor draws with — verified live
            // against a screenshot, 2026-08-29), GX 0 the left.
            rows.Sort((a, b2) =>
            {
                int y = a.int_5.CompareTo(b2.int_5);
                return y != 0 ? y : a.int_4.CompareTo(b2.int_4);
            });
            b.BeginStop("screen");
            b.PushContext(Loc.T("editor.screen"), positions: false);
            foreach (var row in rows)
            {
                int n = 0;
                try { n = row.entityID_0.Number; } catch { }
                int entity = n;
                b.AddItem(ControlId.Structural("ed.scr." + entity), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => ExaDisplayName(FindExaByEntity(entity)),
                            kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => SpriteReadout(entity), kind: AnnouncementKinds.Value),
                    },
                });
            }
            b.PopContext();
        }

        /// <summary>Set pixels actually inside the 120x100 display at the sprite's current
        /// position — the compositor's own clip (an off-screen or all-clear sprite draws
        /// nothing, so it isn't "on the screen").</summary>
        private static int VisiblePixels(SimExa exa)
        {
            try
            {
                int count = 0;
                for (int py = 0; py < 10; py++)
                    for (int px = 0; px < 10; px++)
                    {
                        if (!exa.bool_4[py * 10 + px]) continue;
                        int x = exa.int_4 + px, y = exa.int_5 + py;
                        if (x >= 0 && x < 120 && y >= 0 && y < 100) count++;
                    }
                return count;
            }
            catch { return 0; }
        }

        private string SpriteReadout(int entity)
        {
            try
            {
                var e = Editor;
                var exa = FindExaByEntity(entity);
                if (e == null || exa == null) return null;
                string shape;
                int glyph = MatchGlyph(exa.bool_4);
                if (glyph >= 0)
                {
                    string ch = GlyphChar(glyph);
                    shape = ch != null ? CharSpeech(ch) : Loc.T("editor.screen.glyph", new { n = glyph });
                }
                else
                {
                    shape = Loc.T("editor.screen.custom", new { n = VisiblePixels(exa) });
                }
                string readout = Loc.T("editor.screen.sprite",
                    new { x = exa.int_4, y = exa.int_5, shape });
                // GZ only shows in 3D mode (the compositor zeroes the parallax otherwise).
                if (e.bool_10 && exa.int_6 != 0)
                    readout += ", " + Loc.T("editor.screen.depth", new { z = exa.int_6 });
                return readout;
            }
            catch { return null; }
        }

        // ---- glyph naming ----

        // The game's glyph cache: GClass313.dictionary_1 fills from redshift_font.png on the
        // first GP glyph load; smethod_0 (public) both fills and reads it, so probing glyph 0
        // into a scratch buffer forces the load without touching any EXA.
        private static readonly FieldInfo GlyphCacheField =
            Deobf.Field(typeof(SpecialPuzzleLogics.GClass313), "dictionary_1");

        private static System.Collections.Generic.Dictionary<int, bool[]> GlyphCache()
        {
            try
            {
                var cache = GlyphCacheField?.GetValue(null)
                    as System.Collections.Generic.Dictionary<int, bool[]>;
                if (cache != null && cache.Count == 0)
                    SpecialPuzzleLogics.GClass313.smethod_0(new bool[100], 0);
                return cache;
            }
            catch { return null; }
        }

        /// <summary>The font glyph this pattern IS, or -1. A pattern with no set pixel never
        /// matches (glyph 0 is the blank — an invisible sprite, never listed).</summary>
        private static int MatchGlyph(bool[] pattern)
        {
            try
            {
                var cache = GlyphCache();
                if (cache == null || pattern == null) return -1;
                bool any = false;
                for (int i = 0; i < pattern.Length && !any; i++) any = pattern[i];
                if (!any) return -1;
                foreach (var pair in cache)
                {
                    var g = pair.Value;
                    if (g == null || g.Length != pattern.Length) continue;
                    bool same = true;
                    for (int i = 0; i < pattern.Length && same; i++) same = g[i] == pattern[i];
                    if (same) return pair.Key;
                }
            }
            catch { }
            return -1;
        }

        // The font art transcribed (language-invariant, like NetworkLogos): the png is ten
        // 10-glyph columns read top-down — blank + A–I, J–S, T–Z + 0–2, 3–9 + . ? !, the
        // last six columns empty. So index i = (i/10)th column, (i%10)th row, linearizing to
        // this string; 40+ = blank. Verified live 2026-08-29 (GP 301 pattern-matched 'A').
        private const string GlyphChars = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.?!";

        private static string GlyphChar(int glyph)
        {
            if (glyph < 0 || glyph >= GlyphChars.Length) return null;
            return GlyphChars[glyph].ToString();
        }
    }
}
