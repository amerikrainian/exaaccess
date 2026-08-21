using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExaAccess.Game
{
    /// <summary>
    /// Resolved access to EXAPUNKS's control panel and its settings, from the decompile of
    /// ControlPanelScreen/GClass17/GClass52/GClass21 (see CLAUDE.md). Design facts:
    ///
    ///  • The panel is ONE game screen with a private page enum (0 home / 1 options / 2 controls) and
    ///    a tab int — pure per-frame state the mod can read AND write (the game's own buttons do
    ///    nothing more than assign them).
    ///  • Every setting is a GClass52&lt;T&gt; config cell on a settings object hanging off GameLogic.
    ///    Each cell stores its CONFIG KEY ("Fullscreen", "Volume.Music", "KeyMapping.X", …) in its one
    ///    string field, so cells resolve BY NAME — no ordinals at all.
    ///  • A cell is get/set only (no observers): where the game's widget has an apply side effect
    ///    (fullscreen switch, live mixer volume, live font size), the mod mirrors that exact call.
    ///  • The key-capture subscreen binds the first HELD key it polls, so it must only be pushed once
    ///    all keys are up (the caller owns that dance).
    ///
    /// Everything resolves lazily with cross-checks and degrades to no-ops, loudly.
    /// </summary>
    internal static class ControlPanelApi
    {
        // ---- the panel screen: page + tab state ----

        private static Type _panelType;
        private static FieldInfo _pageField;
        private static FieldInfo _tabField;
        private static bool _panelResolved;

        public static Type PanelType
        {
            get { ResolvePanel(); return _panelType; }
        }

        /// <summary>The live panel instance if it's anywhere on the game's screen stack, else null.</summary>
        public static object PanelInstance
        {
            get
            {
                ResolvePanel();
                if (_panelType == null) return null;
                var stack = GameState.ScreenStack();
                if (stack == null) return null;
                for (int i = stack.Count - 1; i >= 0; i--)
                    if (stack[i] != null && stack[i].GetType() == _panelType) return stack[i];
                return null;
            }
        }

        /// <summary>0 = home, 1 = Options, 2 = Controls; -1 when the panel isn't up / unresolved.</summary>
        public static int Page
        {
            get
            {
                var p = PanelInstance;
                if (p == null || _pageField == null) return -1;
                try { return Convert.ToInt32(_pageField.GetValue(p)); } catch { return -1; }
            }
        }

        public static int Tab
        {
            get
            {
                var p = PanelInstance;
                if (p == null || _tabField == null) return -1;
                try { return (int)_tabField.GetValue(p); } catch { return -1; }
            }
        }

        /// <summary>Navigate the panel — exactly what the game's page buttons do (assign the fields;
        /// page changes reset the tab like the game's handlers).</summary>
        public static void SetPage(int page)
        {
            var p = PanelInstance;
            if (p == null || _pageField == null || _tabField == null) return;
            try
            {
                _pageField.SetValue(p, Enum.ToObject(_pageField.FieldType, page));
                _tabField.SetValue(p, 0);
            }
            catch (Exception ex) { Log.Error("[panel] SetPage failed", ex); }
        }

        public static void SetTab(int tab)
        {
            var p = PanelInstance;
            if (p == null || _tabField == null) return;
            try { _tabField.SetValue(p, tab); }
            catch (Exception ex) { Log.Error("[panel] SetTab failed", ex); }
        }

        private static void ResolvePanel()
        {
            if (_panelResolved) return;
            _panelResolved = true;
            var asm = GameState.GameAssembly;
            _panelType = asm?.GetType("ControlPanelScreen"); // real name preserved
            if (_panelType == null) { Log.Error("[panel] ControlPanelScreen type not found."); return; }

            // Page: the unique instance field typed as one of the panel's own nested enums.
            // Tab: the unique instance Int32 field.
            foreach (var f in MemberResolver.FieldsInTokenOrder(_panelType))
            {
                if (f.IsStatic) continue;
                if (f.FieldType.IsEnum && f.FieldType.IsNested && f.FieldType.DeclaringType == _panelType)
                    _pageField = _pageField == null ? f : _pageField; // first (unique in practice)
                else if (f.FieldType == typeof(int))
                    _tabField = _tabField == null ? f : _tabField;
            }
            if (_pageField == null || _tabField == null)
                Log.Error("[panel] page/tab fields unresolved (page=" + (_pageField != null) + ", tab=" + (_tabField != null) + ").");
            else
                Log.Info("[panel] resolved: page=" + _pageField.Name + " tab=" + _tabField.Name);
        }

        // ---- settings cells (GClass52<T> on the GClass17 settings object) ----

        /// <summary>One resolved config cell: typed get/set via the cell's own methods.</summary>
        internal sealed class Cell
        {
            public readonly object Instance;
            private readonly MethodInfo _get;
            private readonly MethodInfo _set;

            public Cell(object instance)
            {
                Instance = instance;
                var t = instance.GetType();
                var valueType = t.GetGenericArguments()[0];
                // Getter: the unique ()->T. Setter: the unique (T)->void (method_2; method_1 is
                // ()->void so it can't collide). Unique or nothing — never a maybe-wrong member.
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 0 && m.ReturnType == valueType)
                        _get = _get == null ? m : null;
                    else if (ps.Length == 1 && ps[0].ParameterType == valueType && m.ReturnType == typeof(void))
                        _set = _set == null ? m : null;
                }
            }

            public bool Usable => _get != null && _set != null;
            public object Get() { try { return _get.Invoke(Instance, null); } catch { return null; } }
            public void Set(object value)
            {
                try { _set.Invoke(Instance, new[] { value }); }
                catch (Exception ex) { Log.Error("[panel] cell set failed", ex); }
            }
            public bool GetBool() => Get() is bool b && b;
            public float GetFloat() => Get() is float f ? f : 0f;
            public int GetKeycode() { try { return Convert.ToInt32(Get()); } catch { return 0; } }
        }

        private static Dictionary<string, Cell> _cells;
        private static Type _bindingCellRuntimeType; // GClass52<keycode-enum>, for the capture screen
        private static Type _keycodeEnumType;

        /// <summary>The cell for a config key ("Fullscreen", "Volume.Music", "KeyMapping.X", …), or
        /// null (logged once at resolve).</summary>
        public static Cell GetCell(string configKey)
        {
            ResolveCells();
            Cell c;
            return _cells != null && _cells.TryGetValue(configKey, out c) ? c : null;
        }

        private static void ResolveCells()
        {
            if (_cells != null) return;
            _cells = new Dictionary<string, Cell>();
            try
            {
                var gl = GameState.Instance;
                if (gl == null) return;

                // The settings object: the unique GameLogic field whose type carries a horde (20+) of
                // fields sharing one generic typedef — the GClass52<> cell family.
                object host = null;
                Type cellTypedef = null;
                foreach (var f in MemberResolver.FieldsInTokenOrder(gl.GetType()))
                {
                    if (f.IsStatic) continue;
                    var counts = new Dictionary<Type, int>();
                    foreach (var hf in f.FieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (!hf.FieldType.IsGenericType) continue;
                        var def = hf.FieldType.GetGenericTypeDefinition();
                        counts[def] = counts.TryGetValue(def, out int n) ? n + 1 : 1;
                    }
                    foreach (var kv in counts)
                        if (kv.Value >= 20)
                        {
                            host = f.GetValue(gl);
                            cellTypedef = kv.Key;
                            break;
                        }
                    if (host != null) break;
                }
                if (host == null) { Log.Error("[panel] settings object not found on GameLogic."); return; }

                // Each cell names itself: its unique string field is the config key.
                foreach (var hf in host.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!hf.FieldType.IsGenericType || hf.FieldType.GetGenericTypeDefinition() != cellTypedef) continue;
                    object cellObj = hf.GetValue(host);
                    if (cellObj == null) continue;
                    string key = CellKey(cellObj);
                    if (key == null) continue;
                    var cell = new Cell(cellObj);
                    if (!cell.Usable) continue;
                    _cells[key] = cell;
                    var arg = hf.FieldType.GetGenericArguments()[0];
                    if (arg.IsEnum && key.StartsWith("KeyMapping.", StringComparison.Ordinal))
                    {
                        _bindingCellRuntimeType = hf.FieldType;
                        _keycodeEnumType = arg;
                    }
                }
                Log.Info("[panel] " + _cells.Count + " settings cells resolved by config key.");
            }
            catch (Exception ex) { Log.Error("[panel] cell resolution failed", ex); }
        }

        private static string CellKey(object cellObj)
        {
            FieldInfo stringField = null;
            foreach (var f in cellObj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(string)) continue;
                if (stringField != null) return null; // not unique — shape changed
                stringField = f;
            }
            return stringField?.GetValue(cellObj) as string;
        }

        // ---- apply side effects the game's widgets perform beyond the cell write ----

        private static MethodInfo _applyFullscreen, _windowedAllowed, _resolutionFits, _applyQuality, _is4K;
        private static bool _opsResolved;

        private static void ResolveOps()
        {
            if (_opsResolved) return;
            _opsResolved = true;
            var gl = GameState.Instance;
            if (gl == null) return;
            var t = gl.GetType();
            var index2 = GameState.GameAssembly.GetType("Index2");
            _is4K = GameApi.ResolveInstanceMethod(t, 0, "display-4k-capable", typeof(bool));
            _applyFullscreen = GameApi.ResolveInstanceMethod(t, 40, "apply-fullscreen", typeof(void), typeof(bool));
            _windowedAllowed = GameApi.ResolveInstanceMethod(t, 41, "windowed-allowed", typeof(bool));
            if (index2 != null)
                _resolutionFits = GameApi.ResolveInstanceMethod(t, 42, "resolution-fits", typeof(bool), index2);
            _applyQuality = GameApi.ResolveInstanceMethod(t, 43, "apply-quality", typeof(void), typeof(bool));
        }

        private static object Invoke(MethodInfo m, params object[] args)
        {
            var gl = GameState.Instance;
            if (gl == null || m == null) return null;
            try { return m.Invoke(gl, args); }
            catch (Exception ex) { Log.Error("[panel] " + m.Name + " failed", ex); return null; }
        }

        /// <summary>Set + apply display mode — the game's Fullscreen/Windowed buttons (cell write via
        /// the button helper, then the apply call).</summary>
        public static void SetFullscreen(bool fullscreen)
        {
            ResolveOps();
            GetCell("Fullscreen")?.Set(fullscreen);
            Invoke(_applyFullscreen, fullscreen);
        }

        public static bool IsWindowedAllowed { get { ResolveOps(); return Invoke(_windowedAllowed) is bool b && b; } }
        public static bool Is4KCapable { get { ResolveOps(); return Invoke(_is4K) is bool b && b; } }

        /// <summary>Set display quality — the game's High(4K)/Low(2K) buttons call this alone (it owns
        /// its own cell write). true = low.</summary>
        public static void SetQualityLow(bool low)
        {
            ResolveOps();
            Invoke(_applyQuality, low);
        }

        public static bool ResolutionFits(object index2)
        {
            ResolveOps();
            return Invoke(_resolutionFits, index2) is bool b && b;
        }

        // ---- window resolutions ----

        private static FieldInfo _resolutionList;      // static Index2[] on GameLogic
        private static FieldInfo _recreateWindowFlag;  // GameLogic.bool_0 (first instance bool)
        private static MethodInfo _getResolution, _setResolution; // on the settings object
        private static object _settingsHost;
        private static bool _resResolved;

        private static void ResolveResolutionApi()
        {
            if (_resResolved) return;
            _resResolved = true;
            var gl = GameState.Instance;
            var index2 = GameState.GameAssembly.GetType("Index2");
            if (gl == null || index2 == null) return;
            var t = gl.GetType();

            foreach (var f in MemberResolver.FieldsInTokenOrder(t))
            {
                if (f.IsStatic && f.FieldType.IsArray && f.FieldType.GetElementType() == index2 && _resolutionList == null)
                    _resolutionList = f;
                if (!f.IsStatic && f.FieldType == typeof(bool) && _recreateWindowFlag == null)
                    _recreateWindowFlag = f; // de4dot bool_0: the first instance bool — the recreate-window flag
            }

            // The settings object's resolution get/set: the GameLogic field whose type carries a
            // unique ()->Index2 and a unique (Index2)->void.
            foreach (var f in MemberResolver.FieldsInTokenOrder(t))
            {
                if (f.IsStatic) continue;
                var ft = f.FieldType;
                var m2 = UniqueMethod(ft, typeof(void), index2);
                var m1 = UniqueMethod(ft, index2);
                if (m1 != null && m2 != null)
                {
                    _settingsHost = f.GetValue(gl);
                    _getResolution = m1;
                    _setResolution = m2;
                    break;
                }
            }
            if (_resolutionList == null || _setResolution == null || _recreateWindowFlag == null)
                Log.Warning("[panel] resolution API partial (list=" + (_resolutionList != null)
                    + ", set=" + (_setResolution != null) + ", flag=" + (_recreateWindowFlag != null) + ").");
        }

        private static MethodInfo UniqueMethod(Type t, Type ret, params Type[] ps)
        {
            MethodInfo unique = null;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.ReturnType != ret) continue;
                var mp = m.GetParameters();
                if (mp.Length != ps.Length) continue;
                bool ok = true;
                for (int i = 0; i < mp.Length; i++) if (mp[i].ParameterType != ps[i]) { ok = false; break; }
                if (!ok) continue;
                if (unique != null) return null;
                unique = m;
            }
            return unique;
        }

        /// <summary>The game's windowed-size choices, as (object Index2, "1920 x 1080") pairs.</summary>
        public static List<KeyValuePair<object, string>> Resolutions()
        {
            ResolveResolutionApi();
            var result = new List<KeyValuePair<object, string>>();
            var arr = _resolutionList?.GetValue(null) as Array;
            if (arr == null) return result;
            foreach (var item in arr)
                result.Add(new KeyValuePair<object, string>(item, Index2Text(item)));
            return result;
        }

        public static object CurrentResolution()
        {
            ResolveResolutionApi();
            if (_getResolution == null || _settingsHost == null) return null;
            try { return _getResolution.Invoke(_settingsHost, null); } catch { return null; }
        }

        public static void SetResolution(object index2)
        {
            ResolveResolutionApi();
            if (_setResolution == null || _settingsHost == null) return;
            try
            {
                _setResolution.Invoke(_settingsHost, new[] { index2 });
                var gl = GameState.Instance;
                if (gl != null && _recreateWindowFlag != null) _recreateWindowFlag.SetValue(gl, true);
            }
            catch (Exception ex) { Log.Error("[panel] SetResolution failed", ex); }
        }

        /// <summary>"1920 x 1080" from an Index2 (its two int fields, token order = width, height).</summary>
        public static string Index2Text(object index2)
        {
            if (index2 == null) return "";
            var fs = MemberResolver.FieldsInTokenOrder(index2.GetType());
            int w = 0, h = 0, seen = 0;
            foreach (var f in fs)
            {
                if (f.IsStatic || f.FieldType != typeof(int)) continue;
                if (seen == 0) w = (int)f.GetValue(index2);
                else if (seen == 1) h = (int)f.GetValue(index2);
                seen++;
            }
            return w + " x " + h;
        }

        // ---- live-audio / live-font statics (deob GClass1) ----

        private static Type _liveStateType;
        private static FieldInfo[] _liveFloats;
        private static FieldInfo _liveLargeFonts;
        private static bool _liveResolved;

        private static void ResolveLiveState()
        {
            if (_liveResolved) return;
            _liveResolved = true;
            var asm = GameState.GameAssembly;
            var anchor = asm?.GetType("ReliableRandom"); // real name — GClass1's distinctive static
            if (anchor == null) return;
            foreach (var t in asm.ManifestModule.GetTypes())
            {
                if (!(t.IsAbstract && t.IsSealed)) continue; // static classes only
                FieldInfo anchorField = null;
                var floats = new List<FieldInfo>();
                var bools = new List<FieldInfo>();
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (f.FieldType == anchor) anchorField = f;
                    else if (f.FieldType == typeof(float)) floats.Add(f);
                    else if (f.FieldType == typeof(bool)) bools.Add(f);
                }
                if (anchorField != null && floats.Count >= 4 && bools.Count >= 6)
                {
                    _liveStateType = t;
                    floats.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
                    bools.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
                    _liveFloats = floats.ToArray();   // [0]=music, [1]=sfx, [2]=voice (deob float_0/1/2)
                    _liveLargeFonts = bools.Count > 2 ? bools[2] : null; // deob bool_2 (live large-fonts)
                    Log.Info("[panel] live-state statics resolved: " + t.Name);
                    return;
                }
            }
            Log.Warning("[panel] live-state statics not found — volume/font changes apply on next use only.");
        }

        /// <summary>Mirror a volume cell write into the live mixer value, as the game's slider does.
        /// Config keys map: Volume.Music → float[0], Volume.Sound → float[1], Volume.Voice → float[2].</summary>
        public static void SetLiveVolume(string configKey, float value)
        {
            ResolveLiveState();
            if (_liveFloats == null || _liveFloats.Length < 3) return;
            int idx = configKey == "Volume.Music" ? 0 : configKey == "Volume.Sound" ? 1 : configKey == "Volume.Voice" ? 2 : -1;
            if (idx < 0) return;
            try { _liveFloats[idx].SetValue(null, value); } catch { }
        }

        /// <summary>Mirror the large-fonts cell into the live flag, as the game's buttons do.</summary>
        public static void SetLiveLargeFonts(bool larger)
        {
            ResolveLiveState();
            try { _liveLargeFonts?.SetValue(null, larger); } catch { }
        }

        // ---- hostname (read-only until the text-entry port) ----

        private static MethodInfo _hostnameGet;
        private static object _saveData;
        private static bool _hostnameResolved;

        public static string Hostname()
        {
            if (!_hostnameResolved)
            {
                _hostnameResolved = true;
                var gl = GameState.Instance;
                var saveDataType = GameState.GameAssembly?.GetType("SaveData");
                if (gl != null && saveDataType != null)
                {
                    foreach (var f in MemberResolver.FieldsInTokenOrder(gl.GetType()))
                        if (!f.IsStatic && f.FieldType == saveDataType) { _saveData = f.GetValue(gl); break; }
                    if (_saveData != null)
                        _hostnameGet = GameApi.ResolveInstanceMethod(saveDataType, 32, "hostname-get", typeof(string));
                }
            }
            if (_saveData == null || _hostnameGet == null) return null;
            try { return _hostnameGet.Invoke(_saveData, null) as string; } catch { return null; }
        }

        // ---- the key-capture subscreen (deob GClass21) ----

        private static Type _captureType;
        private static bool _captureResolved;

        /// <summary>The game's key-capture screen type: the IScreen whose single constructor takes one
        /// binding cell and whose only instance field stores it.</summary>
        public static Type KeyCaptureType
        {
            get
            {
                if (_captureResolved) return _captureType;
                _captureResolved = true;
                ResolveCells();
                var asm = GameState.GameAssembly;
                var iScreen = asm?.GetType("IScreen");
                if (iScreen == null || _bindingCellRuntimeType == null) return null;
                foreach (var t in asm.ManifestModule.GetTypes())
                {
                    if (t.IsInterface || t.IsAbstract || !iScreen.IsAssignableFrom(t)) continue;
                    var ctors = t.GetConstructors();
                    if (ctors.Length != 1) continue;
                    var ps = ctors[0].GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != _bindingCellRuntimeType) continue;
                    _captureType = t;
                    Log.Info("[panel] key-capture screen resolved: " + t.Name);
                    break;
                }
                if (_captureType == null) Log.Error("[panel] key-capture screen type not found by shape.");
                return _captureType;
            }
        }

        /// <summary>Push the game's key-capture screen for a binding cell. The CALLER must ensure no
        /// key is held (the capture binds the first held key it polls).</summary>
        public static bool PushKeyCapture(Cell bindingCell)
        {
            var t = KeyCaptureType;
            if (t == null || bindingCell == null) return false;
            return GameApi.PushScreen(t, bindingCell.Instance);
        }

        /// <summary>The display name of a binding cell's current key ("J", "Return", …).</summary>
        public static string BindingKeyName(Cell bindingCell)
        {
            if (bindingCell == null) return "";
            return SdlNative.GetKeyName(bindingCell.GetKeycode());
        }
    }
}
