using System;
using System.Collections.Generic;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    /// <summary>
    /// ПАСЬЯНС — the in-fiction solitaire (SolitaireScreen, name-preserved), a 100% mouse
    /// screen natively (press = grab, release = drop-or-silently-snap-back; the game reads
    /// only Escape here). 36 cards: suits 0=spades 1=hearts 2=diamonds 3=clubs (0,3 black;
    /// 1,2 red — screenshot-verified 2026-08-23), ranks 6-10 number / 11-14 face
    /// (jack/queen/king/ace). Rules per the game's own instructions page: numbers stack
    /// descending alternating color, faces stack by suit in any order, both move as stacks;
    /// a completed 4-face stack LOCKS its column (a goal); the free cell holds one card;
    /// win = 4 number runs + 4 locked face stacks.
    ///
    /// The BOARD is a raw-wired grid: left/right = adjacent column at the same depth
    /// (clamped to its height), up/down = within a column, locked/empty columns are single
    /// nodes. Moves are a SELECTION model, not a drag: Enter picks up (validated by the
    /// game's own movability predicate), Enter on a destination drops (validated by the
    /// game's own drop predicate, re-parented by the same public call the mouse path uses,
    /// same sounds) — atomic, so the game's per-frame lock/win logic never sees a detached
    /// card, and the mouse keeps working in parallel. Illegal moves are SPOKEN — the
    /// mouse's silent snap-back, made audible. Backspace puts the selection back (nothing
    /// was detached). Board state lives on GameLogic and persists across visits; the win
    /// marks the campaign task complete via the game's own save call.
    /// </summary>
    public sealed class SolitaireGameScreen : Screen
    {
        public SolitaireGameScreen() { Wrap = true; }

        public override string Key => "solitaire";
        public override string ScreenName => Loc.T("screen.solitaire");
        public override bool IsActive() => GameState.TopScreen() is SolitaireScreen;
        public override object InitialFocusStop => "board";

        // The screen's private half: the two rules predicates (movability / drop legality),
        // the buttons' click effects, the instructions-view flag and the Steam win count.
        private static readonly MethodInfo CanPickMethod = Deobf.Method(typeof(SolitaireScreen), "method_5");
        private static readonly MethodInfo CanDropMethod = Deobf.Method(typeof(SolitaireScreen), "method_6");
        private static readonly MethodInfo NewGameMethod = Deobf.Method(typeof(SolitaireScreen), "method_3");
        private static readonly FieldInfo InstructionsField = Deobf.Field(typeof(SolitaireScreen), "bool_0");
        private static readonly FieldInfo WinCountField = Deobf.Field(typeof(SolitaireScreen), "int_0");

        // The four paragraphs of the game's instructions page — loc KEYS are the literals
        // (asterisks = the page's bold markup, stripped for speech).
        private static readonly string[] InstructionKeys =
        {
            "To win, sort the dealt cards into four completed stacks of number cards\nand four completed stacks of face cards.",
            "Number cards are stacked by *alternating color* and *decreasing value*,\nand can be moved together as a stack of any size.",
            "Face cards are stacked *by suit* in *any order*, and can also be moved as a stack. However, a completed stack of face cards placed directly on the board will become immovable.",
            "The *free cell* in the top-right corner of the board can store a single card of any type.",
        };

        private static SolitaireScreen Game => GameState.TopScreen() as SolitaireScreen;
        private static SolitaireState State
        {
            get { try { return GameLogic.gameLogic_0.solitaireState_0; } catch { return null; } }
        }

        private static int Phase(SolitaireState st)
        {
            try { return (int)st.genum157_0; } catch { return -1; }
        }

        private static bool InstructionsOpen(SolitaireScreen s)
        {
            try { return InstructionsField != null && (bool)InstructionsField.GetValue(s); }
            catch { return false; }
        }

        // ---- card speech ----

        private static string SuitName(SolitaireItem card)
        {
            switch ((int)card.genum148_0)
            {
                case 0: return Loc.T("card.spades");
                case 1: return Loc.T("card.hearts");
                case 2: return Loc.T("card.diamonds");
                default: return Loc.T("card.clubs");
            }
        }

        private static string RankName(SolitaireItem card)
        {
            switch (card.int_0)
            {
                case 11: return Loc.T("card.jack");
                case 12: return Loc.T("card.queen");
                case 13: return Loc.T("card.king");
                case 14: return Loc.T("card.ace");
                default: return card.int_0.ToString();
            }
        }

        private static string CardName(SolitaireItem card)
            => card == null ? null : Loc.T("card.of", new { rank = RankName(card), suit = SuitName(card) });

        // ---- typed views (all re-resolved live — the board changes under us) ----

        private static List<SolitaireItem> ColumnCards(SolitaireState st, int col)
        {
            var list = new List<SolitaireItem>();
            try { foreach (var c in st.solitaireItem_1[col].method_9()) list.Add(c); }
            catch { }
            return list;
        }

        private static SolitaireItem CardAt(int col, int depth)
        {
            var st = State;
            if (st == null) return null;
            var cards = ColumnCards(st, col);
            return depth < cards.Count ? cards[depth] : null;
        }

        private static SolitaireItem CellCard(SolitaireState st)
        {
            try
            {
                var top = st.solitaireItem_0.method_7();
                return top.genum143_0 == 0 ? top : null;
            }
            catch { return null; }
        }

        private static bool ColumnLocked(SolitaireState st, int col)
        {
            try { return st.bool_0[col]; } catch { return false; }
        }

        // ---- build ----

        public override void Build(GraphBuilder b)
        {
            var s = Game;
            var st = State;
            if (s == null || st == null) return;
            if (InstructionsOpen(s))
            {
                BuildInstructions(b, s);
                return;
            }
            BuildBoard(b, st);
            BuildCell(b, st);
            BuildButtons(b, s);
        }

        private void BuildBoard(GraphBuilder b, SolitaireState st)
        {
            b.BeginStop("board");
            b.PushContext(Loc.T("solitaire.board"), positions: false);
            int cols = st.solitaireItem_1.Length;
            var ids = new List<ControlId>[cols];
            for (int col = 0; col < cols; col++)
            {
                ids[col] = new List<ControlId>();
                int c = col;
                if (ColumnLocked(st, col))
                {
                    var id = ControlId.Structural("sol.lock." + col);
                    ids[col].Add(id);
                    b.AddNode(id, new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        SpeaksOwnPosition = true,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => LockedText(c), kind: AnnouncementKinds.Label),
                        },
                        OnActivate = () => ActivateColumn(c, -1),
                        OnSecondary = CancelGrab,
                    });
                    continue;
                }
                var cards = ColumnCards(st, col);
                if (cards.Count == 0)
                {
                    var id = ControlId.Structural("sol.empty." + col);
                    ids[col].Add(id);
                    b.AddNode(id, new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        SpeaksOwnPosition = true,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => Loc.T("solitaire.column.empty", new { n = c + 1 }),
                                kind: AnnouncementKinds.Label),
                        },
                        OnActivate = () => ActivateColumn(c, -1),
                        OnSecondary = CancelGrab,
                    });
                    continue;
                }
                for (int d = 0; d < cards.Count; d++)
                {
                    int depth = d;
                    var id = ControlId.Structural("sol.c" + col + "." + d);
                    ids[col].Add(id);
                    b.AddNode(id, new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        SpeaksOwnPosition = true, // bare cards — position is the navigation
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => CardName(CardAt(c, depth)), live: true,
                                kind: AnnouncementKinds.Label),
                        },
                        OnActivate = () => ActivateColumn(c, depth),
                        OnSecondary = CancelGrab,
                    });
                }
            }
            // Up/down walk a column (down = toward the top of the stack, the way the pile
            // grows on screen); left/right jump columns at the same depth, clamped.
            for (int col = 0; col < cols; col++)
            {
                for (int d = 0; d < ids[col].Count; d++)
                {
                    if (d > 0) b.Connect(ids[col][d], GraphDir.Up, ids[col][d - 1]);
                    if (d < ids[col].Count - 1) b.Connect(ids[col][d], GraphDir.Down, ids[col][d + 1]);
                    if (col > 0)
                        b.Connect(ids[col][d], GraphDir.Left, ids[col - 1][Math.Min(d, ids[col - 1].Count - 1)]);
                    if (col < cols - 1)
                        b.Connect(ids[col][d], GraphDir.Right, ids[col + 1][Math.Min(d, ids[col + 1].Count - 1)]);
                }
            }
            b.PopContext();
        }

        private void BuildCell(GraphBuilder b, SolitaireState st)
        {
            b.BeginStop("cell");
            b.AddItem(ControlId.Structural("sol.cell"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("solitaire.cell"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() =>
                    {
                        var cur = State;
                        var card = cur == null ? null : CellCard(cur);
                        return card == null ? Loc.T("solitaire.cell.empty") : CardName(card);
                    }, live: true, kind: AnnouncementKinds.Value),
                },
                OnActivate = ActivateCell,
                OnSecondary = CancelGrab,
            });
        }

        private void BuildButtons(GraphBuilder b, SolitaireScreen s)
        {
            b.BeginStop("buttons");
            b.AddItem(ControlId.Structural("sol.wins"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("WIN COUNT"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => WinCount().ToString(), live: true, kind: AnnouncementKinds.Value),
                },
            });
            b.AddItem(ControlId.Structural("sol.instructions"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("INSTRUCTIONS"), kind: AnnouncementKinds.Label),
                },
                OnActivate = () => SetInstructions(true), // the game's button just flips the flag
            });
            b.AddItem(ControlId.Structural("sol.newgame"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("NEW GAME"), kind: AnnouncementKinds.Label),
                },
                // The game's click path exactly: fresh state + autosave. The deal watch
                // speaks "Dealing." as confirmation.
                OnActivate = () => { try { NewGameMethod?.Invoke(Game, null); _grab = null; } catch (Exception ex) { Log.Error("[solitaire] new game failed", ex); } },
            });
        }

        private void BuildInstructions(GraphBuilder b, SolitaireScreen s)
        {
            b.BeginStop("instructions");
            b.PushContext(GameText.T("INSTRUCTIONS"), positions: false);
            for (int i = 0; i < InstructionKeys.Length; i++)
            {
                int n = i;
                b.AddItem(ControlId.Structural("sol.ins." + i), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => GameText.TSpeech(InstructionKeys[n]).Replace("*", ""),
                            kind: AnnouncementKinds.Label),
                    },
                });
            }
            b.AddItem(ControlId.Structural("sol.return"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("RETURN TO GAME"), kind: AnnouncementKinds.Label),
                },
                OnActivate = () => SetInstructions(false), // Escape does the same, natively
            });
            b.PopContext();
        }

        // ---- interaction: the selection model ----

        private SolitaireItem _grab; // never detached — the move is one atomic re-parent

        private void ActivateColumn(int col, int depth)
        {
            var s = Game;
            var st = State;
            if (s == null || st == null || Phase(st) != 1)
            {
                Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true);
                return;
            }
            var card = depth >= 0 ? CardAt(col, depth) : null;
            if (_grab != null && ReferenceEquals(card, _grab)) { CancelGrab(); return; }
            if (_grab == null)
            {
                if (card == null || ColumnLocked(st, col)) // the game's pick loop skips locked columns
                {
                    Speech.Tts.Speak(Loc.T("solitaire.blocked"), interrupt: true);
                    return;
                }
                TryPick(s, card);
                return;
            }
            SolitaireItem top;
            try { top = st.solitaireItem_1[col].method_7(); } // the anchor itself when empty
            catch { return; }
            TryDrop(s, top, Loc.T(top.genum143_0 == 0 ? "solitaire.dest.card" : "solitaire.dest.empty",
                new { dest = top.genum143_0 == 0 ? CardName(top) : (col + 1).ToString() }));
        }

        private void ActivateCell()
        {
            var s = Game;
            var st = State;
            if (s == null || st == null || Phase(st) != 1)
            {
                Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true);
                return;
            }
            var card = CellCard(st);
            if (_grab != null && ReferenceEquals(card, _grab)) { CancelGrab(); return; }
            if (_grab == null)
            {
                if (card == null)
                {
                    Speech.Tts.Speak(Loc.T("solitaire.blocked"), interrupt: true);
                    return;
                }
                TryPick(s, card);
                return;
            }
            SolitaireItem top;
            try { top = st.solitaireItem_0.method_7(); } // occupied cell = its card; method_6 rejects it
            catch { return; }
            // No move announce: the focused cell's LIVE value re-announces the arrival itself
            // (user rule 2026-08-23).
            TryDrop(s, top, null);
        }

        private void TryPick(SolitaireScreen s, SolitaireItem card)
        {
            bool ok = false;
            try { ok = CanPickMethod != null && (bool)CanPickMethod.Invoke(s, new object[] { card }); }
            catch (Exception ex) { Log.Error("[solitaire] pick check failed", ex); }
            if (!ok)
            {
                Speech.Tts.Speak(Loc.T("solitaire.blocked"), interrupt: true);
                return;
            }
            _grab = card;
            int riding = 0;
            try { foreach (var _ in card.method_9()) riding++; } catch { }
            riding = Math.Max(0, riding - 1);
            try { GClass45.soundsNamespace_0.sound_12.smethod_1(1f); } catch { }
            Speech.Tts.Speak(riding == 0
                ? Loc.T("solitaire.picked", new { card = CardName(card) })
                : Loc.T("solitaire.picked.stack", new { card = CardName(card), n = riding }),
                interrupt: true);
        }

        private void TryDrop(SolitaireScreen s, SolitaireItem targetTop, string destSpeech)
        {
            var grab = _grab;
            if (grab == null || targetTop == null) return;
            bool ok = false;
            try
            {
                // Re-check movability first — the mouse may have restacked things since the
                // pick (the game's drag doesn't need this; its card is detached).
                ok = CanPickMethod != null && (bool)CanPickMethod.Invoke(s, new object[] { grab })
                    && CanDropMethod != null && (bool)CanDropMethod.Invoke(s, new object[] { grab, targetTop });
            }
            catch (Exception ex) { Log.Error("[solitaire] drop check failed", ex); }
            if (!ok)
            {
                Speech.Tts.Speak(Loc.T("solitaire.illegal"), interrupt: true);
                return;
            }
            try
            {
                grab.method_4(targetTop); // the drop handler's own re-parent call
                GClass45.soundsNamespace_0.sound_13.smethod_1(1f);
            }
            catch (Exception ex)
            {
                Log.Error("[solitaire] move failed", ex);
                return;
            }
            _grab = null;
            if (destSpeech != null)
                Speech.Tts.Speak(Loc.T("solitaire.moved", new { card = CardName(grab), dest = destSpeech }),
                    interrupt: true);
        }

        private void CancelGrab()
        {
            if (_grab == null) return;
            _grab = null;
            Speech.Tts.Speak(Loc.T("solitaire.putback"), interrupt: true);
        }

        // ---- watches ----

        private object _lastState;
        private int _lastPhase = -1;
        private bool[] _lastLocks;

        public override void OnUpdate()
        {
            var st = State;
            var s = Game;
            if (st == null || s == null) return;
            if (!ReferenceEquals(st, _lastState))
            {
                _lastState = st;
                _lastPhase = -1;
                _lastLocks = null;
                _grab = null;
            }
            int phase = Phase(st);
            if (phase != _lastPhase)
            {
                if (phase == 0 && _lastPhase != 0) Speech.Tts.Speak(Loc.T("solitaire.dealing"));
                else if (phase == 1 && _lastPhase == 0) Speech.Tts.Speak(Loc.T("solitaire.dealt"));
                else if (phase == 2 && _lastPhase == 1) AnnounceWin();
                _lastPhase = phase;
            }
            try
            {
                var locks = st.bool_0;
                if (_lastLocks != null && locks.Length == _lastLocks.Length)
                    for (int i = 0; i < locks.Length; i++)
                        if (locks[i] && !_lastLocks[i])
                            Speech.Tts.Speak(Loc.T("solitaire.locked.announce", new { suit = LockedSuit(st, i) }));
                _lastLocks = (bool[])locks.Clone();
            }
            catch { }
            if (_grab != null && phase != 1) _grab = null;
        }

        private void AnnounceWin()
        {
            int n = WinCount();
            Speech.Tts.Speak(n > 0
                ? Loc.T("solitaire.won", new { n })
                : Loc.T("solitaire.won.plain"));
        }

        private int WinCount()
        {
            try { return WinCountField != null ? (int)WinCountField.GetValue(Game) : 0; }
            catch { return 0; }
        }

        private static string LockedText(int col)
        {
            var st = State;
            if (st == null) return null;
            return Loc.T("solitaire.column.locked", new { suit = LockedSuit(st, col) });
        }

        private static string LockedSuit(SolitaireState st, int col)
        {
            try
            {
                foreach (var c in st.solitaireItem_1[col].method_9()) return SuitName(c);
            }
            catch { }
            return "";
        }

        private static void SetInstructions(bool open)
        {
            try { InstructionsField?.SetValue(Game, open); }
            catch (Exception ex) { Log.Error("[solitaire] instructions toggle failed", ex); }
        }
    }
}
