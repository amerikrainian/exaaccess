using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    public sealed partial class ExaEditorScreen
    {
        // ---- the CODE stop: one caret-owning node mirroring the game's focused EXA editor.
        // Tab in = the game's real code focus arms and you are typing (arrows/Home/End/Enter/
        // Delete/clipboard all native); Tab/Shift+Tab out = disarmed, back to browsing. The
        // game's own Ctrl+Up/Down switches which EXA is focused; the node follows. ----

        private static readonly FieldInfo FocusField = Deobf.Field(typeof(EditorScreen), "maybe_3");
        private static readonly FieldInfo FocusQueueField = Deobf.Field(typeof(EditorScreen), "maybe_4");
        private static readonly MethodInfo FocusExaMethod = Deobf.Method(typeof(EditorScreen), "method_58");
        private static readonly FieldInfo CaretField = Deobf.Field(typeof(CodeEditorWidget), "int_1");
        private static readonly FieldInfo AnchorField = Deobf.Field(typeof(CodeEditorWidget), "int_0");

        /// <summary>The SolutionExa whose CODE the game currently focuses, else null.</summary>
        private static SolutionExa FocusedCodeExa(EditorScreen e)
        {
            return CodeExaFrom(e, FocusField);
        }

        /// <summary>The announce-time target: the applied focus, else the QUEUED one (focus changes
        /// apply a frame late), else the first EXA.</summary>
        private static SolutionExa TargetCodeExa(EditorScreen e)
        {
            return FocusedCodeExa(e) ?? CodeExaFrom(e, FocusQueueField) ?? FirstExa(e);
        }

        private static SolutionExa CodeExaFrom(EditorScreen e, FieldInfo field)
        {
            try
            {
                var focus = (Maybe<GStruct9>)field.GetValue(e);
                if (!focus.method_0() || focus.method_2().genum13_0 != (GEnum13)1) return null;
                var id = focus.method_2().entityID_0;
                foreach (var exa in e.solution_0.list_0)
                    if (EntityID.Exa(exa.method_0()) == id) return exa;
            }
            catch { }
            return null;
        }

        // ---- problems: every compile error as its own row, "Line n: message" (EXA-prefixed
        // when the solution has several), Enter = jump the caret to that line. The rows read
        // the WRITTEN pass (the code the user navigates); no errors — or a running sim, which
        // only ever runs clean code — means no stop at all. ----

        private void BuildProblems(GraphBuilder b, EditorScreen e)
        {
            if (!Editing(e)) return;
            var labels = new System.Collections.Generic.List<string>();
            var exaNums = new System.Collections.Generic.List<int>();
            var errLines = new System.Collections.Generic.List<int>();
            try
            {
                bool multi = e.solution_0.list_0.Count > 1;
                foreach (var exa in e.solution_0.list_0)
                    foreach (var err in exa.gclass276_0.gclass286_0.list_1)
                    {
                        string message = GameText.Speech(err.string_0);
                        labels.Add(multi
                            ? Loc.T("editor.problem.exa",
                                new { exa = exa.string_0, line = err.int_0 + 1, message })
                            : Loc.T("editor.problem", new { line = err.int_0 + 1, message }));
                        exaNums.Add(exa.method_0());
                        errLines.Add(err.int_0);
                    }
            }
            catch { }
            if (labels.Count == 0) return;
            b.BeginStop("problems");
            b.PushContext(Loc.T("editor.problems"));
            for (int r = 0; r < labels.Count; r++)
            {
                string label = labels[r];
                int exaNum = exaNums[r], line = errLines[r];
                b.AddItem(ControlId.Structural("ed.prob." + r), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => label, kind: AnnouncementKinds.Label),
                    },
                    OnActivate = () => JumpToProblem(exaNum, line),
                });
            }
            b.PopContext();
        }

        private void JumpToProblem(int exaNumber, int line)
        {
            var e = Editor;
            var exa = SolutionExaOf(exaNumber);
            if (e == null || exa == null) return;
            ArmCodeFor(e, exa);
            try
            {
                int offset = CaretText.OffsetOfLine(exa.string_1 ?? string.Empty, line);
                CaretField.SetValue(exa.codeEditorWidget_0, offset);
                AnchorField.SetValue(exa.codeEditorWidget_0, offset);
            }
            catch (Exception ex) { Log.Error("[editor] problem jump failed", ex); }
            Navigation.FocusStop("code");
        }

        private void BuildCode(GraphBuilder b, EditorScreen e)
        {
            if (e.solution_0.list_0.Count == 0) return;
            b.BeginStop("code");
            b.AddItem(ControlId.Structural("ed.code"), new NodeVtable
            {
                ControlType = ControlTypes.TextField,
                Announcements = new[]
                {
                    new NodeAnnouncement(() =>
                    {
                        var exa = TargetCodeExa(Editor);
                        return exa == null ? null : Loc.T("editor.code", new { exa = exa.string_0 });
                    }, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => CurrentLineText(), kind: AnnouncementKinds.Value),
                },
                TextEntry = true,
                TextEntryCaret = true,
                TextEchoCaps = false, // the widget uppercases every insert
                TextValue = () =>
                {
                    var exa = FocusedCodeExa(Editor);
                    return exa == null ? null : exa.string_1;
                },
                // The one code node fronts whichever EXA the game focuses — the echo must
                // re-baseline on the game's Ctrl+Up/Down switch, not diff XA's code against XB's.
                TextIdentity = () =>
                {
                    var exa = FocusedCodeExa(Editor);
                    return exa == null ? null : (object)exa.method_0();
                },
                OnSelect = () => ArmCode(Editor),
            });
        }

        private static SolutionExa FirstExa(EditorScreen e)
        {
            try { return e != null && e.solution_0.list_0.Count > 0 ? e.solution_0.list_0[0] : null; }
            catch { return null; }
        }

        // The EXA last armed at the code stop (tracked by NarrateCaret while focused): Tab away
        // and back re-arms IT, not the first EXA — the game's Ctrl+Up/Down choice survives.
        private int _lastCodeExa = int.MinValue;

        private void ArmCode(EditorScreen e)
        {
            if (e == null || FocusedCodeExa(e) != null) return; // already armed
            ArmCodeFor(e, LastOrFirstExa(e));
        }

        private SolutionExa LastOrFirstExa(EditorScreen e)
        {
            try
            {
                if (_lastCodeExa != int.MinValue)
                    foreach (var exa in e.solution_0.list_0)
                        if (exa.method_0() == _lastCodeExa) return exa;
            }
            catch { }
            return FirstExa(e); // never armed yet, or that EXA was deleted
        }

        private static void ArmCodeFor(EditorScreen e, SolutionExa exa)
        {
            if (e == null || exa == null) return;
            try
            {
                var id = EntityID.Exa(exa.method_0());
                // The game QUEUES focus changes (maybe_4, applied next frame); method_58 is only
                // the scroll-into-view half — both mirror the game's own Ctrl+Up/Down path.
                FocusQueueField?.SetValue(e, (Maybe<GStruct9>)new GStruct9(id, (GEnum13)1));
                FocusExaMethod?.Invoke(e, new object[] { id, true, true });
            }
            catch (Exception ex) { Log.Error("[editor] code arm failed", ex); }
        }

        private static void DisarmCode(EditorScreen e)
        {
            try { FocusField?.SetValue(e, (Maybe<GStruct9>)GStruct10.gstruct10_0); }
            catch (Exception ex) { Log.Error("[editor] code disarm failed", ex); }
        }

        // ---- caret narration (task 4B): while the code stop is focused, vertical caret moves
        // speak the line landed on, horizontal moves speak the character at the caret, and the
        // game's Ctrl+Up/Down EXA switch announces the new window. Text CHANGES are the typing
        // echo's job — they only re-baseline here, never double-speak. ----

        private int _caretExa = int.MinValue;
        private int _caretOffset = -1, _caretLine = -1;
        private string _caretText;

        // Edge detection for the caret keys. Two jobs: (1) a press that had nowhere to go
        // (Home at the line start, Up on the first line) re-announces one tick later (user
        // request, 2026-08-22); (2) the edge's KIND classifies the caret move it causes —
        // the game applies the move a frame after we see the press, and a fast tap releases
        // the key before then, so held-state classification mis-reads a vertical move as a
        // word jump ("LINK" instead of "LINK 800" — user report). Grouped by index range.
        private static readonly int[] CaretKeys =
        {
            (int)Input.Scancode.Up, (int)Input.Scancode.Down,
            (int)Input.Scancode.PageUp, (int)Input.Scancode.PageDown,
            96, 90,  // KP_8 / KP_2                                    [0-5]  vertical
            (int)Input.Scancode.Left, (int)Input.Scancode.Right,
            92, 94,  // KP_4 / KP_6                                    [6-9]  horizontal
            (int)Input.Scancode.Home, (int)Input.Scancode.End,
            95, 89,  // KP_7 / KP_1                                    [10-13] home/end
        };
        private readonly bool[] _caretKeyWas = new bool[CaretKeys.Length];

        private const int MoveNone = 0, MoveVertical = 1, MoveHorizontal = 2, MoveHomeEnd = 3,
            MoveWord = 4; // horizontal with Ctrl at edge time — a word jump
        private int _pendingMove = MoveNone; // the un-consumed key edge's kind
        private int _pendingAge;             // ticks since that edge fired

        private void NarrateCaret(EditorScreen e, bool baseline)
        {
            var exa = FocusedCodeExa(e);
            if (exa == null) return;
            int caret, exaNum;
            string text;
            try
            {
                caret = (int)CaretField.GetValue(exa.codeEditorWidget_0);
                text = exa.string_1 ?? string.Empty;
                exaNum = exa.method_0();
            }
            catch { return; }
            if (caret > text.Length) caret = text.Length;
            int line = LineIndex(text, caret);

            // Fresh press edges this frame (typematic repeat doesn't re-edge — one physical
            // press, one announce). An edge becomes the PENDING move kind: the caret delta
            // it causes shows up a tick later and consumes it for classification; a pending
            // edge that never produces a delta becomes the re-land announce.
            int edge = MoveNone;
            for (int i = 0; i < CaretKeys.Length; i++)
            {
                bool held = Input.SdlKeyboard.Held(CaretKeys[i]);
                if (held && !_caretKeyWas[i])
                    edge = i < 6 ? MoveVertical
                        : i < 10 ? (Input.SdlKeyboard.CtrlHeld ? MoveWord : MoveHorizontal)
                        : MoveHomeEnd;
                _caretKeyWas[i] = held;
            }
            if (edge != MoveNone) { _pendingMove = edge; _pendingAge = 0; }
            else if (_pendingMove != MoveNone) _pendingAge++;

            if (exaNum != _caretExa)
            {
                // First arm is silent (the landing announce covered it); a real EXA switch
                // (the game's Ctrl+Up/Down) announces the new window + its current line.
                if (!baseline && _caretExa != int.MinValue)
                    Speech.Tts.Speak(Loc.T("editor.code", new { exa = exa.string_0 })
                        + ", " + (CurrentLineText() ?? ""), interrupt: true);
                _pendingMove = MoveNone; // this edge's outcome is handled
            }
            else if (text != _caretText)
            {
                // An edit: the typing echo spoke it; just re-baseline below.
                _pendingMove = MoveNone;
            }
            else if (line != _caretLine || caret != _caretOffset)
            {
                // Screen-reader convention (user rules, 2026-08-22): ONLY vertical moves read the
                // full line. Horizontal moves speak the character (a line boundary lands on the
                // newline and says just that); larger horizontal jumps (Ctrl word moves) speak
                // the word landed on; Home/End are caret PLACEMENTS speaking the character.
                // The move type is the KEY EDGE that caused this delta (a fast tap is released
                // by now — held state alone mis-classifies); held state is the fallback for
                // deltas with no recorded edge.
                int kind = _pendingMove;
                _pendingMove = MoveNone;
                bool vertical = kind == MoveVertical ||
                    (kind == MoveNone && (
                        Input.SdlKeyboard.Held((int)Input.Scancode.Up)
                        || Input.SdlKeyboard.Held((int)Input.Scancode.Down)
                        || Input.SdlKeyboard.Held((int)Input.Scancode.PageUp)
                        || Input.SdlKeyboard.Held((int)Input.Scancode.PageDown)
                        || Input.SdlKeyboard.Held(96) || Input.SdlKeyboard.Held(90))); // KP_8 / KP_2
                bool homeEnd = kind == MoveHomeEnd ||
                    (kind == MoveNone && (
                        Input.SdlKeyboard.Held((int)Input.Scancode.Home)
                        || Input.SdlKeyboard.Held((int)Input.Scancode.End)
                        || Input.SdlKeyboard.Held(95) || Input.SdlKeyboard.Held(89))); // KP_7 / KP_1
                if (Input.SdlKeyboard.ShiftHeld)
                {
                    // Shift = the game's native selection. Speak the TRUE delta — the span
                    // between the old and new caret — + "selected" ("unselected" when the
                    // caret moved back toward the anchor). This holds for vertical moves too:
                    // Shift+Up from mid-line selects tail-of-upper + head-of-lower (" EOF
                    // TJMP"), not the whole line — speaking the line would misreport the
                    // clipboard (user report, 2026-08-22).
                    int anchor = caret;
                    try { anchor = (int)AnchorField.GetValue(exa.codeEditorWidget_0); } catch { }
                    bool shrank = Math.Abs(caret - anchor) < Math.Abs(_caretOffset - anchor);
                    int at = Math.Min(caret, _caretOffset);
                    int len = Math.Abs(caret - _caretOffset);
                    string sel = len > 0 && at >= 0 && at + len <= text.Length
                        ? text.Substring(at, len) : null;
                    if (sel != null && sel.Length == 1) sel = CaretText.CharAt(sel, 0);
                    if (sel != null)
                        Speech.Tts.Speak(Loc.T(shrank ? "text.unselected" : "text.selected",
                            new { text = sel }), interrupt: true);
                }
                else if (vertical)
                    // Line-index change NOT required: Up on the first line (or Down on the
                    // last) clamps the caret to the line's start/end WITHIN the same line —
                    // a vertical intent still reads the line (user report, 2026-08-22).
                    Speech.Tts.Speak(CurrentLineText(), interrupt: true);
                else if (homeEnd || (kind != MoveWord && Math.Abs(caret - _caretOffset) == 1))
                    Speech.Tts.Speak(CaretText.CharAt(text, caret), interrupt: true);
                else
                    Speech.Tts.Speak(CaretText.WordAt(text, caret), interrupt: true);
            }
            else if (!baseline && _pendingMove != MoveNone && _pendingAge >= 1)
            {
                // The key fired a tick ago and no caret delta followed — it had nowhere to
                // go (Home already at the line start, Up on the first line, Down on the
                // last): re-announce as if landed anew — vertical keys the line, word jumps
                // the word, the rest the character (user request; Ctrl+Left at the text
                // start repeats "LINK", never its first letter). The one-tick wait is what
                // separates this from a real move whose delta arrives a frame behind the press.
                if (_pendingMove == MoveVertical) Speech.Tts.Speak(CurrentLineText(), interrupt: true);
                else if (_pendingMove == MoveWord) Speech.Tts.Speak(CaretText.WordAt(text, caret), interrupt: true);
                else Speech.Tts.Speak(CaretText.CharAt(text, caret), interrupt: true);
                _pendingMove = MoveNone;
            }

            _caretExa = exaNum;
            _caretOffset = caret;
            _caretLine = line;
            _caretText = text;
            _lastCodeExa = exaNum; // survives Tab-away/back: re-arm this EXA, not the first
        }

        private static int LineIndex(string text, int caret) => CaretText.LineIndex(text, caret);


        /// <summary>The text of the line the caret sits on ("blank" for an empty line).</summary>
        private static string CurrentLineText()
        {
            var e = Editor;
            var exa = TargetCodeExa(e); // arming is queued; read the target
            if (exa == null) return null;
            try
            {
                int caret = (int)CaretField.GetValue(exa.codeEditorWidget_0);
                string text = exa.string_1 ?? string.Empty;
                if (caret > text.Length) caret = text.Length;
                int start = text.LastIndexOf('\n', Math.Max(0, caret - 1)) + 1;
                int end = text.IndexOf('\n', caret);
                if (end < 0) end = text.Length;
                string line = start <= end ? text.Substring(start, end - start) : string.Empty;
                return line.Length == 0 ? Loc.T("text.blank") : line;
            }
            catch { return null; }
        }
    }
}
