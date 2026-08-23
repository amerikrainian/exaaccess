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
        // ---- the file-values popup: Enter on a file row lists EVERY value, one row each (the
        // 60-value cap is the ROW's compromise; here you arrow at your own pace). Enter or
        // Backspace closes, focus back on the file row. ----

        private string _popupFile;
        private int _popupFileHost = -1; // ids repeat across hosts — the popup stays host-qualified
        private ControlId _popupOrigin; // the row that opened it (files stop OR a file window)

        private void OpenFilePopup(string id, int hostIndex)
            => OpenFilePopup(id, hostIndex, ControlId.Structural("ed.file." + hostIndex + "." + id));

        private void OpenFilePopup(string id, int hostIndex, ControlId origin)
        {
            _popupFile = id;
            _popupFileHost = hostIndex;
            _popupOrigin = origin;
            Navigation.FocusStop("filepop");
        }

        private void CloseFilePopup()
        {
            var origin = _popupOrigin;
            _popupFile = null;
            _popupFileHost = -1;
            _popupOrigin = null;
            if (origin != null) Navigation.FocusNode(origin);
        }

        private SimFile PopupFile()
            => _popupFile == null ? null : (FindFileAt(_popupFile, _popupFileHost) ?? FindFile(_popupFile));

        private bool BuildFilePopup(GraphBuilder b)
        {
            var file = PopupFile();
            if (file == null) return false;
            int count = 0;
            try { count = file.list_0.Count; } catch { }
            string fid = _popupFile;
            // No context, no position counts (user rule): entering announces the VALUE alone,
            // arrowing speaks each next value bare.
            b.BeginStop("filepop");
            int rows = Math.Max(1, count); // an empty file still gets its one "blank" row
            for (int i = 0; i < rows; i++)
            {
                int vi = i;
                b.AddItem(ControlId.Structural("ed.fpop." + i), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    SpeaksOwnPosition = true, // bare values — no "n of m" (user rule)
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => PopupValueAt(fid, vi), kind: AnnouncementKinds.Label),
                    },
                    OnActivate = CloseFilePopup,
                    OnSecondary = CloseFilePopup,
                });
            }
            return true;
        }

        private string PopupValueAt(string id, int i)
        {
            var file = PopupFile();
            if (file == null) return null;
            try
            {
                if (file.list_0.Count == 0) return Loc.T("text.blank"); // an empty file's one row
                return i < file.list_0.Count ? file.list_0[i].method_2(true) : null;
            }
            catch { return null; }
        }

        /// <summary>F2 steps the sim from anywhere on this screen (the game's own Tab-step is
        /// suppressed so Tab stays stop-navigation — see CLAUDE.md).</summary>
        public override System.Collections.Generic.IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction("ui.step", StepSim);
            yield return new ElementAction("ui.runto", RunToCaret);
            yield return new ElementAction("ui.runto.exa", RunToPinned);
            // Instance cycling exists only while ARMED (copies can't exist otherwise) —
            // while editing the chord stays the game's, untouched.
            var ed = Editor;
            bool armed = false;
            try { armed = ed != null && !Editing(ed); } catch { }
            if (armed)
            {
                yield return new ElementAction("ui.followNext", () => FollowInstance(1));
                yield return new ElementAction("ui.followPrev", () => FollowInstance(-1));
            }
            // Escape closes the popups (their game-side Escape is suppressed while
            // ModalCapturesEscape holds — see GameKeySuppression).
            if (_popupFile != null) yield return new ElementAction(ActionIds.Back, CloseFilePopup);
            if (_goalPopup) yield return new ElementAction(ActionIds.Back, CloseGoalPopup);
        }

        /// <summary>The popups are mod-side only — Escape must close THEM, not act in the game
        /// (reset-or-leave would close the whole task under the popup).</summary>
        public override bool ModalCapturesEscape => _popupFile != null || _goalPopup;
    }
}
