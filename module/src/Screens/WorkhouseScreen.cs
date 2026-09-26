using System;
using System.Collections.Generic;
using System.Reflection;
using Echopunks.Game;
using Echopunks.Localization;
using Echopunks.UI;
using Echopunks.UI.Graph;

namespace Echopunks.Screens
{
    /// <summary>
    /// The Workhouse (deob GClass50) — the one-time receipt-transcription story minigame, and the
    /// mod's first TEXT ENTRY screen. TYPING-FIRST: field nodes are NodeVtable.TextEntry — landing
    /// on one (arrows, Tab, Enter-advance) arms the game's own field via method_2, so printable
    /// characters flow immediately through the TEXTINPUT channel (never suppressed) and Backspace
    /// passes through the suppression seam; the navigator echoes typed/deleted characters. The
    /// receipt is nine read-only rows mirroring the game's grading literal — the same nine lines
    /// sighted players read off the receipt art. SUBMIT and EXIT (mouse-only in the game) get
    /// keyboard activation by replicating the click paths exactly (decompile-verified, including
    /// the click handler's normalize-then-grade order); the art-only dialogs (exit confirm /
    /// rejected / success) become tiny button graphs with mirrored prompt lines. Escape stays
    /// native throughout (field unfocus → exit dialog → dialog dismiss).
    /// </summary>
    public sealed class WorkhouseScreen : Screen
    {
        public WorkhouseScreen() { Wrap = true; }

        public override string Key => "workhouse";
        public override string ScreenName => GameText.T("WorkHouse");
        public override bool IsActive() => GameState.TopScreen() is GClass50;
        public override object InitialFocusStop => "receipt";

        private const int Rows = 12; // the game's int_0
        private const int DescMaxLen = 15, PriceMaxLen = 9;

        // The game's grading key (a literal in GClass50's submit handler) — also exactly what the
        // receipt art shows. Mirrored because the art is the only other place it exists.
        private const string Answer = "VC CHAMPAGNE#140.00#DP CABERNET#90.00#DP CABERNET#90.00#OYSTER PL#35.00#NY STRIP#70.00#ADD TRUFFLES#20.00#FILET MIGNON#80.00#CHC TORTE#12.00#SPECIAL ORDER#200.00";
        private static readonly string[] AnswerParts = Answer.Split('#');

        // GClass50's private half (renamed type — Deobf's T-row bridge resolves it).
        private static readonly FieldInfo DescField = Deobf.Field(typeof(GClass50), "string_0");
        private static readonly FieldInfo PriceField = Deobf.Field(typeof(GClass50), "string_1");
        private static readonly FieldInfo FocusField = Deobf.Field(typeof(GClass50), "maybe_0");
        private static readonly FieldInfo ExitDialogField = Deobf.Field(typeof(GClass50), "bool_0");
        private static readonly FieldInfo RejectedField = Deobf.Field(typeof(GClass50), "bool_1");
        private static readonly FieldInfo SuccessField = Deobf.Field(typeof(GClass50), "bool_2");
        private static readonly FieldInfo LoadClockField = Deobf.Field(typeof(GClass50), "float_0");
        private static readonly FieldInfo SuccessClockField = Deobf.Field(typeof(GClass50), "float_1");
        private static readonly FieldInfo RejectClockField = Deobf.Field(typeof(GClass50), "float_2");
        private static readonly MethodInfo FocusMethod = Deobf.Method(typeof(GClass50), "method_2");
        private static readonly MethodInfo NormalizeMethod = Deobf.Method(typeof(GClass50), "method_3");
        private static readonly MethodInfo DistanceMethod = Deobf.Method(typeof(GClass50), "method_4");

        private static GClass50 Workhouse => GameState.TopScreen() as GClass50;

        // ---- typed views over the private state (every read/write guarded) ----

        private static string[] Fields(GClass50 s, bool desc)
        {
            try { return (desc ? DescField : PriceField)?.GetValue(s) as string[]; }
            catch { return null; }
        }

        private static string FieldText(GClass50 s, int row, bool desc)
        {
            var a = Fields(s, desc);
            return a != null && row < a.Length ? a[row] : null;
        }

        private static int FocusedIndex(GClass50 s)
        {
            try
            {
                var m = (Maybe<int>)FocusField.GetValue(s);
                return m.method_0() ? m.method_2() : -1;
            }
            catch { return -1; }
        }

        private static void ClearFieldFocus(GClass50 s)
        {
            // The game's any-click path: normalize, then drop the field focus.
            try
            {
                NormalizeMethod?.Invoke(s, null);
                FocusField?.SetValue(s, (Maybe<int>)GStruct10.gstruct10_0);
            }
            catch (Exception ex) { Log.Error("[workhouse] clear focus failed", ex); }
        }

        private static void ArmField(GClass50 s, int index)
        {
            // The game's own keyboard path: normalize every field, then focus (primes the widget).
            try
            {
                NormalizeMethod?.Invoke(s, null);
                FocusMethod?.Invoke(s, new object[] { index });
            }
            catch (Exception ex) { Log.Error("[workhouse] field focus failed", ex); }
        }

        private static bool Flag(GClass50 s, FieldInfo f)
        {
            try { return f != null && (bool)f.GetValue(s); }
            catch { return false; }
        }

        private static void SetFlag(GClass50 s, FieldInfo f, bool value)
        {
            try { f?.SetValue(s, value); }
            catch (Exception ex) { Log.Error("[workhouse] flag write failed", ex); }
        }

        private static float Clock(GClass50 s, FieldInfo f)
        {
            try { return f != null ? (float)f.GetValue(s) : 0f; }
            catch { return 0f; }
        }

        private static void ClickSound() { try { GClass45.soundsNamespace_0.sound_46.smethod_1(1f); } catch { } }
        private static void CloseSound() { try { GClass45.soundsNamespace_0.sound_34.smethod_1(1f); } catch { } }

        // ---- graph ----

        public override void Build(GraphBuilder b)
        {
            var s = Workhouse;
            if (s == null) return;

            if (Flag(s, SuccessField))
            {
                // Success dialog: OK = the click at (1611,710) — jump the timer past the dialog.
                Button(b, "wh.success.ok", () => Loc.T("workhouse.continue"), () =>
                {
                    ClickSound();
                    try { SuccessClockField?.SetValue(s, 1000f); } catch { }
                });
            }
            else if (Flag(s, RejectedField))
            {
                Button(b, "wh.reject.ok", () => Loc.T("workhouse.continue"), () =>
                {
                    ClickSound();
                    SetFlag(s, RejectedField, false);
                });
            }
            else if (Flag(s, ExitDialogField))
            {
                Button(b, "wh.exit.keep", () => Loc.T("workhouse.keep"), () =>
                {
                    ClickSound();
                    SetFlag(s, ExitDialogField, false);
                });
                Button(b, "wh.exit.confirm", () => Loc.T("workhouse.exit"), () =>
                {
                    ClickSound(); CloseSound();
                    GameApi.PopScreen();
                });
            }
            else if (Clock(s, LoadClockField) < 4f)
            {
                // The fake-loading phase: only the EXIT button exists (it pops directly here).
                Button(b, "wh.load.exit", () => Loc.T("workhouse.exit"), () =>
                {
                    ClickSound(); CloseSound();
                    GameApi.PopScreen();
                });
            }
            else
            {
                BuildReceipt(b);
                BuildForm(b, s);

                b.BeginStop("submit");
                Button(b, "wh.submit", () => Loc.T("workhouse.submit"), () => Submit(s));

                b.BeginStop("exit");
                Button(b, "wh.exit", () => Loc.T("workhouse.exit"), () =>
                {
                    // The form-phase EXIT opens the confirm dialog, like the game's button.
                    ClickSound();
                    SetFlag(s, ExitDialogField, true);
                });

                BuildInfo(b, s);
            }
        }

        // The flavor panels (job briefing left, Workhouse welcome right) — all art; mirrored so
        // blind players get the same satire sighted players read. The balance is the screen's one
        // real live string.
        private void BuildInfo(GraphBuilder b, GClass50 s)
        {
            b.BeginStop("info");
            b.PushContext(Loc.T("workhouse.info"), positions: false); // prose, not a list — no "n of m"
            InfoRow(b, "wh.info.job", "workhouse.job.title", "workhouse.job.body");
            InfoRow(b, "wh.info.pitch", "workhouse.job.pitch", null);
            InfoRow(b, "wh.info.welcome", "workhouse.welcome.title", "workhouse.welcome.body");
            InfoRow(b, "wh.info.slogan", "workhouse.slogan", null);
            b.AddItem(ControlId.Structural("wh.info.balance"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("workhouse.balance"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => BalanceText(Workhouse), live: true, kind: AnnouncementKinds.Value),
                },
            });
            b.PopContext();
        }

        private static void InfoRow(GraphBuilder b, string id, string labelKey, string valueKey)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T(labelKey), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => valueKey == null ? null : Loc.T(valueKey), kind: AnnouncementKinds.Value),
                },
            });
        }

        // Mirrors the drawn balance exactly: $0.10 only once the success dialog has shown.
        private static string BalanceText(GClass50 s)
        {
            if (s == null) return null;
            return !Flag(s, SuccessField) || Clock(s, SuccessClockField) <= 1.5f ? "$0.00" : "$0.10";
        }

        private static void Button(GraphBuilder b, string id, Func<string> label, Action activate)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[] { new NodeAnnouncement(label, kind: AnnouncementKinds.Label) },
                OnActivate = activate,
            });
        }

        private void BuildReceipt(GraphBuilder b)
        {
            b.BeginStop("receipt");
            b.PushContext(Loc.T("workhouse.receipt"));
            // The task header bar ("Task #01287472 / Transcribe the items from this receipt") is
            // art, like all this screen's flavor — mirrored (see the info stop for the rest).
            b.AddItem(ControlId.Structural("wh.task"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("workhouse.task.number"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => Loc.T("workhouse.task.instruction"), kind: AnnouncementKinds.Value),
                },
            });
            for (int i = 0; i + 1 < AnswerParts.Length; i += 2)
            {
                string desc = AnswerParts[i], price = AnswerParts[i + 1];
                b.AddItem(ControlId.Structural("wh.rcpt." + (i / 2)), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => desc, kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => price, kind: AnnouncementKinds.Value),
                    },
                    // Enter spells the row out — the abbreviations ("DP CABERNET", "OYSTER PL")
                    // have to be transcribed letter-exact, and TTS reads them as words.
                    OnActivate = () => Speech.Tts.Speak(Spell(desc) + ", " + Spell(price)),
                });
            }
            b.PopContext();
        }

        // Character by character; a bare space or "." is TTS silence, so both are named.
        private static string Spell(string text)
        {
            var parts = new List<string>();
            foreach (char c in text)
                parts.Add(c == ' ' ? Loc.T("text.space") : c == '.' ? Loc.T("text.dot") : c.ToString());
            return string.Join(", ", parts);
        }

        private void BuildForm(GraphBuilder b, GClass50 s)
        {
            b.BeginStop("form");
            b.PushContext(Loc.T("workhouse.form"));
            for (int i = 0; i < Rows; i++)
            {
                b.StartRow("wh");
                FieldNode(b, s, i, desc: true);
                FieldNode(b, s, i, desc: false);
                b.EndRow();
            }
            b.PopContext();
        }

        private void FieldNode(GraphBuilder b, GClass50 s, int row, bool desc)
        {
            int index = row * 2 + (desc ? 0 : 1);
            string id = (desc ? "wh.desc." : "wh.price.") + row;
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.TextField,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T(desc ? "workhouse.desc" : "workhouse.price", new { n = row + 1 }),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() =>
                    {
                        string t = FieldText(Workhouse, row, desc);
                        return string.IsNullOrEmpty(t) ? Loc.T("text.blank") : t;
                    }, kind: AnnouncementKinds.Value),
                },
                TextEntry = true,
                TextValue = () => FieldText(Workhouse, row, desc),
                TextEchoCaps = false, // the widget uppercases every insert — no case exists to report
                Selected = () => FocusedIndex(Workhouse) == index, // Tab lands on the armed field
                OnSelect = () => ArmField(Workhouse, index),
                // Enter = next field in typing order (the game's own Tab rhythm), clamped at the last.
                OnActivate = () =>
                {
                    int next = Math.Min(index + 1, Rows * 2 - 1);
                    int nrow = next / 2;
                    Navigation.FocusNode(ControlId.Structural((next % 2 == 0 ? "wh.desc." : "wh.price.") + nrow));
                },
            });
        }

        // The SUBMIT click path, replicated from the decompile: any click first normalizes and
        // unfocuses (method_3 + clear), then the button collects non-empty fields in row order,
        // grades with the game's own Levenshtein (method_4) against the answer literal, and flips
        // the success/rejected state (+ the same GClass254 notifications).
        private static void Submit(GClass50 s)
        {
            try
            {
                ClickSound();
                ClearFieldFocus(s);
                var list = new List<string>();
                var desc = Fields(s, true);
                var price = Fields(s, false);
                if (desc == null || price == null) return;
                for (int j = 0; j < Rows; j++)
                {
                    if (desc[j].Length > 0) list.Add(desc[j]);
                    if (price[j].Length > 0) list.Add(price[j]);
                }
                int dist = (int)DistanceMethod.Invoke(s, new object[] { Answer, string.Join("#", list) });
                if (dist < 20)
                {
                    GameLogic.gameLogic_0.gclass254_0.method_13();
                    SetFlag(s, SuccessField, true);
                }
                else
                {
                    GameLogic.gameLogic_0.gclass254_0.method_12();
                    SetFlag(s, RejectedField, true);
                    RejectClockField?.SetValue(s, 0f);
                }
            }
            catch (Exception ex) { Log.Error("[workhouse] submit failed", ex); }
        }

        // ---- per-frame: phase/dialog announcements + field-focus housekeeping ----

        private object _instance;
        private int _phase = -1;
        private bool _exitDialog, _rejected, _success;
        private bool _wasTyping;

        public override void OnUpdate()
        {
            var s = Workhouse;
            if (s == null) return;
            if (!ReferenceEquals(_instance, s))
            {
                _instance = s;
                _phase = -1;
                _exitDialog = _rejected = _success = _wasTyping = false;
            }

            // The fake-loading strings are real game loc text — announce each phase once.
            float load = Clock(s, LoadClockField);
            int phase = load < 0.7f ? 0 : load < 2f ? 1 : load < 4f ? 2 : 3;
            if (phase != _phase)
            {
                if (phase == 0) Speech.Tts.Speak(GameText.T("Initializing..."));
                else if (phase == 1) Speech.Tts.Speak(GameText.T("Logging in..."));
                else if (phase == 2) Speech.Tts.Speak(GameText.T("Downloading task..."));
                _phase = phase;
            }

            AnnounceFlag(s, ExitDialogField, ref _exitDialog, "workhouse.exit.prompt");
            AnnounceFlag(s, RejectedField, ref _rejected, "workhouse.rejected");
            AnnounceFlag(s, SuccessField, ref _success, "workhouse.success");

            // When OUR focus leaves the fields (to receipt/submit/exit), release the game's field
            // the way a click would — otherwise typed characters would keep landing in it. Falling
            // edge only, so a mouse user's own field focus is never fought over.
            bool typing = Navigation.TextEntryFocused;
            if (_wasTyping && !typing && FocusedIndex(s) >= 0) ClearFieldFocus(s);
            _wasTyping = typing;
        }

        private static void AnnounceFlag(GClass50 s, FieldInfo field, ref bool prev, string locKey)
        {
            bool now = Flag(s, field);
            if (now && !prev) Speech.Tts.Speak(Loc.T(locKey));
            prev = now;
        }
    }
}
