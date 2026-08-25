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
                        if (exa == null) return null;
                        string nm = exa.string_0;
                        // Armed: the label names the FOLLOWED instance ("XA:1") — the mode
                        // must be audible, never guessed.
                        if (!Editing(Editor))
                        {
                            var f = FollowedExa(exa);
                            if (f != null) nm = f.string_0;
                        }
                        return Loc.T("editor.code", new { exa = nm });
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
            // While the sim is ARMED the game's caret is FROZEN (the edit widget only runs in
            // edit mode — every arrow would be a no-move and re-announce the same line): the
            // virtual read cursor takes the keys instead.
            bool armed = false;
            try { armed = e.method_0(); } catch { }
            if (armed)
            {
                NarrateVirtual(e);
                return;
            }
            _virtLine = -1; // back in edit mode: the real caret resumes; the next arm re-snaps
            _followedEntity = -1; // copies are gone with the run — follow the original again
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

        // ---- the virtual read cursor (armed sim only): a mod-side line index over the
        // EXECUTING listing — SimExa.method_9(), the exact text the sim window draws, one line
        // per instruction index — because the real caret is frozen mid-run. Vertical keys move
        // and speak lines (PgUp/PgDn ±10, Home/End first/last), the game's highlighted current
        // instruction (int_0) is marked, and entry snaps to it. Read-only by nature; horizontal
        // keys mean nothing here. ----

        private int _virtLine = -1;
        private int _virtExa = int.MinValue;

        // ---- the FOLLOWED instance (armed only): which live EXA of the focused program the
        // code view tracks — the original by default, a REPL copy after Enter on its window
        // row or Ctrl+Left/Right cycling. Audible everywhere it matters: the code node's
        // label carries the name, switching announces it, Shift+Enter pins it. Falls back to
        // the original the moment the instance dies or the program changes. ----
        private int _followedEntity = -1; // EntityID number; -1 = the original

        private SimExa FollowedExa(SolutionExa program)
        {
            try
            {
                int s = program.method_0();
                if (_followedEntity >= 0)
                {
                    var exa = FindExaByEntity(_followedEntity);
                    if (exa != null && exa.maybe_2.method_0() && exa.maybe_2.method_2().method_0() == s
                        && Mine(exa))
                        return exa;
                    _followedEntity = -1; // died, or a different program — back to the original
                }
                return FindExa(s);
            }
            catch { return null; }
        }

        /// <summary>The LISTING line of an EXA's pending instruction — the line the game's
        /// window highlights. int_0 (the instruction counter) drifts off the listing when
        /// EmptyLines are consumed, so the instruction's own line annotation is the
        /// truth (the step narration's source).</summary>
        private static int CurrentListingLine(SimExa exa)
        {
            try
            {
                var instr = exa.method_10();
                if (instr.maybe_0.method_0()) return instr.maybe_0.method_2();
            }
            catch { }
            try { return exa.int_0; } catch { return -1; }
        }

        /// <summary>The viewing player's own EXA? In battle the OPPONENT's solution numbers
        /// its programs like ours, so EVERY instance walk that matches by solution number
        /// must also check the team — an unfiltered match counted the enemy's EXA as an
        /// instance of YOUR program and its internal name leaked into the current markers
        /// ("current, XA, ALPHA" — user report, 2026-08-25). Fails closed: no leak even if
        /// the team read ever breaks.</summary>
        private static bool Mine(SimExa exa)
        {
            try
            {
                var e = Editor;
                return e != null && exa.team_0 == e.method_24();
            }
            catch { return false; }
        }

        /// <summary>How many live EXAs run this program (1 = just the original).</summary>
        private int LiveInstanceCount(SolutionExa program)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null) return 0;
                int s = program.method_0(), n = 0;
                foreach (var entity in sim.list_1)
                {
                    var x = entity as SimExa;
                    if (x != null && x.maybe_2.method_0() && x.maybe_2.method_2().method_0() == s
                        && Mine(x)) n++;
                }
                return n;
            }
            catch { return 0; }
        }

        /// <summary>", current" when this listing line is a live instance's current
        /// instruction — naming the instance(s) whenever the program has more than one
        /// alive (user spec 2026-08-23: one EXA = bare "current"; several =
        /// "current, XA:1").</summary>
        private string CurrentMarker(SolutionExa program, int line)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null) return null;
                int s = program.method_0();
                var here = new System.Collections.Generic.List<string>();
                int total = 0;
                foreach (var entity in sim.list_1)
                {
                    var x = entity as SimExa;
                    if (x == null || !x.maybe_2.method_0()) continue;
                    if (x.maybe_2.method_2().method_0() != s || !Mine(x)) continue;
                    total++;
                    if (CurrentListingLine(x) == line) here.Add(x.string_0);
                }
                if (here.Count == 0) return null;
                string marker = ", " + Loc.T("editor.line.current");
                return total <= 1 ? marker : marker + ", " + string.Join(", ", here.ToArray());
            }
            catch { return null; }
        }

        private void NarrateVirtual(EditorScreen e)
        {
            _pendingMove = MoveNone; // never let a stale edit-mode edge re-land later
            var exa = TargetCodeExa(e);
            if (exa == null) return;
            string text = null;
            int current = -1;
            try
            {
                var simExa = FollowedExa(exa);
                if (simExa != null)
                {
                    text = simExa.method_9();
                    current = CurrentListingLine(simExa);
                }
                else
                {
                    // The EXA died mid-run — the listing still reads, nothing is current.
                    text = exa.gclass276_0.gclass286_1.string_0;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(text)) return;

            // Chorded arrows are COMMANDS (Alt = instance switch, Ctrl = region nav) — the
            // read cursor moves on BARE keys only, or the chord lands on the executing
            // line and instantly walks off its own landing (bit Alt+Up/Down, 2026-08-23).
            bool chorded = Input.SdlKeyboard.CtrlHeld || Input.SdlKeyboard.AltHeld
                || Input.SdlKeyboard.ShiftHeld;
            int edgeIdx = -1;
            for (int i = 0; i < CaretKeys.Length; i++)
            {
                bool held = Input.SdlKeyboard.Held(CaretKeys[i]);
                if (held && !_caretKeyWas[i] && !chorded) edgeIdx = i;
                _caretKeyWas[i] = held;
            }

            int lineCount = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') lineCount++;
            if (_virtExa != exa.method_0() || _virtLine < 0)
            {
                _virtExa = exa.method_0();
                _virtLine = current >= 0 ? current : 0;
            }
            if (_virtLine >= lineCount) _virtLine = lineCount - 1;
            if (edgeIdx < 0) return;

            int line = _virtLine;
            if (edgeIdx == 0 || edgeIdx == 4) line--;                 // Up / KP_8
            else if (edgeIdx == 1 || edgeIdx == 5) line++;            // Down / KP_2
            else if (edgeIdx == 2) line -= 10;                        // PageUp
            else if (edgeIdx == 3) line += 10;                        // PageDown
            else if (edgeIdx == 10 || edgeIdx == 12) line = 0;        // Home / KP_7
            else if (edgeIdx == 11 || edgeIdx == 13) line = lineCount - 1; // End / KP_1
            else return;
            _virtLine = Math.Max(0, Math.Min(lineCount - 1, line));

            int start = CaretText.OffsetOfLine(text, _virtLine);
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            string lineText = end > start ? text.Substring(start, end - start) : Loc.T("text.blank");
            string marker = CurrentMarker(exa, _virtLine);
            if (marker != null) lineText += marker;
            Speech.Tts.Speak(lineText, interrupt: true);
        }


        /// <summary>The text of the line the caret sits on ("blank" for an empty line).</summary>
        private string CurrentLineText()
        {
            var e = Editor;
            var exa = TargetCodeExa(e); // arming is queued; read the target
            if (exa == null) return null;
            try
            {
                // ARMED: the landing must speak the line the arrows will move FROM — the
                // virtual read cursor over the EXECUTING listing (snapped to the current
                // instruction when unset, mirroring NarrateVirtual) — never the frozen edit
                // caret, which lives in another region entirely ("the lines don't follow",
                // 2026-08-23).
                if (!Editing(e))
                {
                    string listing = null;
                    int current = -1;
                    var simExa = FollowedExa(exa);
                    if (simExa != null)
                    {
                        listing = simExa.method_9();
                        current = CurrentListingLine(simExa);
                    }
                    else
                    {
                        listing = exa.gclass276_0.gclass286_1.string_0;
                    }
                    if (!string.IsNullOrEmpty(listing))
                    {
                        int lineCount = 1;
                        for (int i = 0; i < listing.Length; i++)
                            if (listing[i] == '\n') lineCount++;
                        if (_virtExa != exa.method_0() || _virtLine < 0)
                        {
                            _virtExa = exa.method_0();
                            _virtLine = current >= 0 ? current : 0;
                        }
                        if (_virtLine >= lineCount) _virtLine = lineCount - 1;
                        int vstart = CaretText.OffsetOfLine(listing, _virtLine);
                        int vend = listing.IndexOf('\n', vstart);
                        if (vend < 0) vend = listing.Length;
                        string vline = vend > vstart
                            ? listing.Substring(vstart, vend - vstart)
                            : Loc.T("text.blank");
                        string vmark = CurrentMarker(exa, _virtLine);
                        if (vmark != null) vline += vmark;
                        return vline;
                    }
                }
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
