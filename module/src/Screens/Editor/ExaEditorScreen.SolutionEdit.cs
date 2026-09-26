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
        // ---- solution-name editing + Create New EXA (task 7) ----

        private static bool NameArmed(EditorScreen e)
        {
            try { return NameActiveField != null && (bool)NameActiveField.GetValue(e); }
            catch { return false; }
        }

        private static void ArmName(EditorScreen e)
        {
            if (e == null || !Editing(e) || NameArmed(e)) return;
            try
            {
                Invoke(CloseFieldsMethod, e);
                NameActiveField?.SetValue(e, true);
                GClass288.smethod_1(); // prime the text-input state, like the game's click
            }
            catch (Exception ex) { Log.Error("[editor] name arm failed", ex); }
        }

        private static void CommitName(EditorScreen e)
        {
            if (e == null || !NameArmed(e)) return;
            try
            {
                NameActiveField?.SetValue(e, false);
                Invoke(SnapshotMethod, e); // the game's own commit path (Enter/click-away)
            }
            catch (Exception ex) { Log.Error("[editor] name commit failed", ex); }
        }

        // Mirror of the Create New EXA handler (button / native Ctrl+Enter): create, snapshot,
        // rebuild, then focus the new EXA's code — landing our focus on the code stop with it.
        private void CreateExa()
        {
            var e = Editor;
            if (e == null || !Editing(e)) return;
            try
            {
                var sim = TheSim(e);
                if (sim != null && (sim.bool_4
                    || e.solution_0.list_0.Count >= sim.dictionary_0[e.method_24()].int_0
                    || e.solution_0.list_0.Count >= sim.int_6))
                {
                    Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true);
                    return;
                }
                var id = e.solution_0.method_2();
                Invoke(DirtyMethod, e);
                Invoke(SnapshotMethod, e);
                Invoke(RebuildMethod, e);
                FocusQueueField?.SetValue(e, (Maybe<GStruct9>)new GStruct9(id, (GEnum13)1));
                FocusExaMethod?.Invoke(e, new object[] { id, true, true });
                Navigation.FocusStop("code");
            }
            catch (Exception ex) { Log.Error("[editor] create EXA failed", ex); }
        }
    }
}
