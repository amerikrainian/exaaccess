using System;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    /// <summary>
    /// The TEC Constellation II control panel (game type ControlPanelScreen — one game screen, but
    /// THREE mod screens keyed to its private page field, so page flips announce as screen changes
    /// and each page keeps its own focus memory). All widgets mirror the decompiled originals:
    /// radios write the config cell plus the same apply call the game's button makes; sliders write
    /// the cell plus the live mixer value; key bindings push the game's own capture subscreen.
    ///
    /// LABELS COME FROM THE GAME (GameText — live language, all six the game ships); our own locale
    /// carries only what the game has no string for: hints, role words, the capture prompt, and the
    /// gamepad binding names (baked into art in the game). The game's native mouse handling and
    /// Escape behavior keep working untouched throughout.
    /// </summary>
    public abstract class ControlPanelPageScreen : Screen
    {
        private readonly int _page;

        protected ControlPanelPageScreen(int page)
        {
            _page = page;
            Wrap = true; // small closed screens: Tab cycles tabs → content → back → tabs
        }

        public override bool IsActive()
            => ControlPanelApi.PanelInstance != null && ControlPanelApi.Page == _page;

        // Covered by the key-capture overlay (or a page flip): keep focus/tab memory.
        public override bool KeepStateOnPop => true;

        // ---- shared widget declarations ----

        protected static void Button(GraphBuilder b, string id, Func<string> label, string hintKey, Action activate)
        {
            var parts = new System.Collections.Generic.List<NodeAnnouncement>
            {
                new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
            };
            if (hintKey != null) parts.Add(new NodeAnnouncement(() => Loc.T(hintKey), kind: AnnouncementKinds.Tooltip));
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = parts,
                OnActivate = activate,
            });
        }

        /// <summary>One option of a mutually-exclusive pair/list (the panel's standard widget).</summary>
        protected static void Radio(GraphBuilder b, string id, Func<string> label,
            Func<bool> selected, Action activate, Func<bool> enabled = null)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new[]
                {
                    new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => Safe(selected) ? Loc.T("value.selected") : null,
                        live: true, kind: AnnouncementKinds.Selected),
                    new NodeAnnouncement(() => enabled == null || Safe(enabled) ? null : Loc.T("value.unavailable"),
                        kind: AnnouncementKinds.Enabled),
                },
                StateText = () => Safe(selected) ? Loc.T("value.selected") : null,
                OnActivate = () =>
                {
                    if (enabled != null && !Safe(enabled)) { Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true); return; }
                    activate();
                },
            });
        }

        /// <summary>A labeled row holding a pair of radio options (the panel's visual layout: label
        /// left, two buttons right — modeled as a context wrapping a two-item row). Label and option
        /// texts are GAME loc keys.</summary>
        protected static void RadioRow(GraphBuilder b, string rowKey, string gameLabelKey,
            string idA, string gameOptKeyA, Func<bool> selA, Action actA,
            string idB, string gameOptKeyB, Func<bool> selB, Action actB,
            Func<bool> enabled = null)
        {
            b.PushContext(GameText.TSpeech(gameLabelKey));
            b.StartRow(rowKey);
            Radio(b, idA, () => GameText.TSpeech(gameOptKeyA), selA, actA, enabled);
            Radio(b, idB, () => GameText.TSpeech(gameOptKeyB), selB, actB, enabled);
            b.EndRow();
            b.PopContext();
        }

        /// <summary>A 0..1 volume slider over a config cell, mirroring the live mixer value. The
        /// label is a GAME loc key.</summary>
        protected static void VolumeSlider(GraphBuilder b, string id, string gameLabelKey, string configKey)
        {
            Func<float> value = () => ControlPanelApi.GetCell(configKey)?.GetFloat() ?? 0f;
            Func<string> percent = () => Loc.T("value.percent", new { value = Math.Round(value() * 100f) });
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T(gameLabelKey), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(percent, kind: AnnouncementKinds.Value),
                },
                StateText = percent,
                OnAdjust = (sign, large) =>
                {
                    var cell = ControlPanelApi.GetCell(configKey);
                    if (cell == null) return;
                    float step = large ? 0.1f : 0.05f;
                    float v = Math.Max(0f, Math.Min(1f, cell.GetFloat() + sign * step));
                    cell.Set(v);
                    ControlPanelApi.SetLiveVolume(configKey, v); // the game's slider writes both
                },
            });
        }

        protected static void TextRow(GraphBuilder b, string id, Func<string> label, Func<string> value, string hintKey = null)
        {
            var parts = new System.Collections.Generic.List<NodeAnnouncement>
            {
                new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                new NodeAnnouncement(value, kind: AnnouncementKinds.Value),
            };
            if (hintKey != null) parts.Add(new NodeAnnouncement(() => Loc.T(hintKey), kind: AnnouncementKinds.Tooltip));
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = parts,
            });
        }

        /// <summary>The tab strip (one horizontal row; activating a tab switches the panel's tab
        /// field, which rebuilds the content stop in place).</summary>
        protected static void Tabs(GraphBuilder b, params (string id, Func<string> label, int index)[] tabs)
        {
            b.BeginStop("tabs");
            b.StartRow();
            foreach (var t in tabs)
            {
                int index = t.index;
                var label = t.label;
                b.AddItem(ControlId.Structural(t.id), new NodeVtable
                {
                    ControlType = ControlTypes.Tab,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => ControlPanelApi.Tab == index ? Loc.T("value.selected") : null,
                            live: true, kind: AnnouncementKinds.Selected),
                    },
                    StateText = () => ControlPanelApi.Tab == index ? Loc.T("value.selected") : null,
                    OnActivate = () => ControlPanelApi.SetTab(index),
                });
            }
            b.EndRow();
        }

        protected static void BackButton(GraphBuilder b)
        {
            b.BeginStop("back");
            Button(b, "panel.back", () => GameText.T("Back"), "panel.back.hint", () => ControlPanelApi.SetPage(0));
        }

        private static bool Safe(Func<bool> f) { try { return f(); } catch { return false; } }
    }

    /// <summary>Page 0: the panel's home — Options / Controls / Exit, the clock, and Close.</summary>
    public sealed class ControlPanelHomeScreen : ControlPanelPageScreen
    {
        public ControlPanelHomeScreen() : base(0) { }
        public override string Key => "panel.home";
        public override string ScreenName => Loc.T("screen.controlpanel");

        public override void Build(GraphBuilder b)
        {
            Button(b, "panel.options", () => GameText.T("Options"), "panel.options.hint", () => ControlPanelApi.SetPage(1));
            Button(b, "panel.controls", () => GameText.T("Controls"), "panel.controls.hint", () => ControlPanelApi.SetPage(2));
            Button(b, "panel.exit", () => GameText.T("Exit Game"), "panel.exit.hint", () => GameApi.QuitGame());
            Button(b, "panel.close", () => Loc.T("panel.close"), "panel.close.hint", () => GameApi.PopScreen());
            TextRow(b, "panel.clock", () => Loc.T("panel.clock"), () => DateTime.Now.ToString("h:mm tt"));
        }
    }

    /// <summary>Page 1: Options — Display / Sound / Interface / Network tabs.</summary>
    public sealed class ControlPanelOptionsScreen : ControlPanelPageScreen
    {
        public ControlPanelOptionsScreen() : base(1) { }
        public override string Key => "panel.options";
        public override string ScreenName => GameText.T("Options");

        public override void Build(GraphBuilder b)
        {
            Tabs(b,
                ("tab.display", () => GameText.T("Display"), 0),
                ("tab.sound", () => GameText.T("Sound"), 1),
                ("tab.interface", () => GameText.T("Interface"), 2),
                ("tab.network", () => GameText.T("Network"), 3));

            b.BeginStop("content");
            switch (ControlPanelApi.Tab)
            {
                case 0: BuildDisplay(b); break;
                case 1: BuildSound(b); break;
                case 2: BuildInterface(b); break;
                case 3: BuildNetwork(b); break;
            }
            BackButton(b);
        }

        private static void BuildDisplay(GraphBuilder b)
        {
            Func<bool> fullscreen = () => ControlPanelApi.GetCell("Fullscreen")?.GetBool() ?? true;
            RadioRow(b, "opts", "Display Mode",
                "disp.fullscreen", "Fullscreen", fullscreen, () => ControlPanelApi.SetFullscreen(true),
                "disp.windowed", "Windowed", () => !fullscreen(), () => ControlPanelApi.SetFullscreen(false));

            // Window sizes: selectable only while windowed and only when they fit the desktop.
            b.PushContext(GameText.T("Window Size"));
            var resolutions = ControlPanelApi.Resolutions();
            for (int i = 0; i < resolutions.Count; i += 2)
            {
                b.StartRow("res");
                for (int j = i; j < Math.Min(i + 2, resolutions.Count); j++)
                {
                    var res = resolutions[j].Key;
                    string label = resolutions[j].Value;
                    Radio(b, "res." + label,
                        () => label,
                        () => !fullscreen() && Equals(ControlPanelApi.CurrentResolution(), res),
                        () => ControlPanelApi.SetResolution(res),
                        () => !fullscreen() && ControlPanelApi.ResolutionFits(res));
                }
                b.EndRow();
            }
            b.PopContext();

            Func<bool> capable = () => ControlPanelApi.Is4KCapable;
            Func<bool> low = () => ControlPanelApi.GetCell("ForceLowQualityTextures")?.GetBool() ?? false;
            RadioRow(b, "opts", "Display Quality",
                "qual.high", "High (4K)", () => capable() && !low(), () => ControlPanelApi.SetQualityLow(false),
                "qual.low", "Low (2K)", () => low() || !capable(), () => ControlPanelApi.SetQualityLow(true),
                capable);
        }

        private static void BuildSound(GraphBuilder b)
        {
            VolumeSlider(b, "vol.sfx", "SFX Volume", "Volume.Sound");
            VolumeSlider(b, "vol.voice", "Voiceover Volume", "Volume.Voice");
            VolumeSlider(b, "vol.music", "Music Volume", "Volume.Music");
        }

        private static void BuildInterface(GraphBuilder b)
        {
            TextRow(b, "iface.hostname", () => GameText.T("Hostname"),
                () => ControlPanelApi.Hostname() ?? Loc.T("value.unavailable"), "hostname.hint");

            Cell("FilterProfanity", out var profanity);
            RadioRow(b, "opts", "Profanity",
                "prof.show", "Show", () => !profanity(), () => Set("FilterProfanity", false),
                "prof.hide", "Hide", profanity, () => Set("FilterProfanity", true));

            Cell("UseSoftwareCursor", out var software);
            RadioRow(b, "opts", "Mouse Cursor",
                "cur.hw", "Hardware", () => !software(), () => Set("UseSoftwareCursor", false),
                "cur.sw", "Software", software, () => Set("UseSoftwareCursor", true));

            Cell("UseLargeFonts", out var larger);
            RadioRow(b, "opts", "Code Font Size",
                "font.normal", "Normal", () => !larger(), () => { Set("UseLargeFonts", false); ControlPanelApi.SetLiveLargeFonts(false); },
                "font.larger", "Larger", larger, () => { Set("UseLargeFonts", true); ControlPanelApi.SetLiveLargeFonts(true); });

            Cell("EnableCrtDistortion", out var crt);
            RadioRow(b, "opts", "HACK\\*MATCH\nCRT Effect",
                "crt.normal", "Normal", crt, () => Set("EnableCrtDistortion", true),
                "crt.off", "No Distortion", () => !crt(), () => Set("EnableCrtDistortion", false));
        }

        private static void BuildNetwork(GraphBuilder b)
        {
            Cell("EnableHistograms", out var histograms);
            RadioRow(b, "opts", "Histograms",
                "hist.show", "Show", histograms, () => Set("EnableHistograms", true),
                "hist.hide", "Hide", () => !histograms(), () => Set("EnableHistograms", false));

            Cell("EnableLeaderboards", out var boards);
            RadioRow(b, "opts", "Leaderboards",
                "lead.show", "Show", boards, () => Set("EnableLeaderboards", true),
                "lead.hide", "Hide", () => !boards(), () => Set("EnableLeaderboards", false));

            Cell("ShowTopPercentile", out var top);
            RadioRow(b, "opts", "Top Percentile",
                "top.show", "Show", () => boards() && top(), () => Set("ShowTopPercentile", true),
                "top.hide", "Hide", () => !top() || !boards(), () => Set("ShowTopPercentile", false),
                boards);

            Cell("ShowTenthPercentile", out var tenth);
            RadioRow(b, "opts", "Tenth Percentile",
                "tenth.show", "Show", () => boards() && tenth(), () => Set("ShowTenthPercentile", true),
                "tenth.hide", "Hide", () => !tenth() || !boards(), () => Set("ShowTenthPercentile", false),
                boards);

            Cell("EnableMultiplayer", out var multi);
            RadioRow(b, "opts", "Multiplayer",
                "mp.on", "Enable", multi, () => Set("EnableMultiplayer", true),
                "mp.off", "Disable", () => !multi(), () => Set("EnableMultiplayer", false));
        }

        private static void Cell(string key, out Func<bool> value)
        {
            value = () => ControlPanelApi.GetCell(key)?.GetBool() ?? false;
        }

        private static void Set(string key, bool v) => ControlPanelApi.GetCell(key)?.Set(v);
    }

    /// <summary>Page 2: Controls — the Redshift gamepad key bindings + the keyboard reference.</summary>
    public sealed class ControlPanelControlsScreen : ControlPanelPageScreen
    {
        public ControlPanelControlsScreen() : base(2) { }
        public override string Key => "panel.controls";
        public override string ScreenName => GameText.T("Controls");

        private ControlPanelApi.Cell _pendingCapture;

        // The gamepad art labels these visually; the game has no strings for them — ours (ui.json).
        private static readonly (string id, string labelKey, string configKey)[] Bindings =
        {
            ("bind.up", "bind.up", "KeyMapping.Up"),
            ("bind.down", "bind.down", "KeyMapping.Down"),
            ("bind.left", "bind.left", "KeyMapping.Left"),
            ("bind.right", "bind.right", "KeyMapping.Right"),
            ("bind.x", "bind.x", "KeyMapping.X"),
            ("bind.y", "bind.y", "KeyMapping.Y"),
            ("bind.z", "bind.z", "KeyMapping.Z"),
            ("bind.start", "bind.start", "KeyMapping.Start"),
        };

        // The reference table IS game text (both columns; the game picks the platform variant in
        // code — these are the Windows strings, which are also the loc keys).
        private static readonly (string id, string labelKey, string valueKey)[] Reference =
        {
            ("ref.sim", "Reset / Pause / Step / Run / Fast", "Escape / F3 / Tab / F4 / F5"),
            ("ref.runto", "Run to Instruction (Any EXA / This EXA)", "Alt + Click / Shift + Alt + Click"),
            ("ref.goal", "Show Goal", "F1"),
            ("ref.exawin", "Previous / Next EXA Window", "Ctrl + Up / Ctrl + Down"),
            ("ref.newexa", "Create New EXA", "Ctrl + Enter"),
            ("ref.clipboard", "Cut / Copy / Paste", "Ctrl + X / Ctrl + C / Ctrl + V"),
            ("ref.undo", "Undo / Redo", "Ctrl + Z / Ctrl + Y"),
        };

        public override void Build(GraphBuilder b)
        {
            Tabs(b,
                ("tab.redshift", () => GameText.TSpeech("Redshift / HACK\\*MATCH"), 0),
                ("tab.othercontrols", () => GameText.T("Other Controls"), 1));

            b.BeginStop("content");
            if (ControlPanelApi.Tab == 0)
            {
                b.PushContext(Loc.T("controls.gamepad"));
                foreach (var (id, labelKey, configKey) in Bindings)
                {
                    string key = configKey;
                    string label = labelKey;
                    b.AddItem(ControlId.Structural(id), new NodeVtable
                    {
                        ControlType = ControlTypes.Button,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => Loc.T(label), kind: AnnouncementKinds.Label),
                            new NodeAnnouncement(() => ControlPanelApi.BindingKeyName(ControlPanelApi.GetCell(key)),
                                live: true, kind: AnnouncementKinds.Value),
                            new NodeAnnouncement(() => Loc.T("bind.hint"), kind: AnnouncementKinds.Tooltip),
                        },
                        OnActivate = () => _pendingCapture = ControlPanelApi.GetCell(key),
                    });
                }
                b.PopContext();
            }
            else
            {
                b.PushContext(Loc.T("controls.reference"));
                foreach (var (id, labelKey, valueKey) in Reference)
                {
                    string lk = labelKey, vk = valueKey;
                    TextRow(b, id, () => GameText.TSpeech(lk), () => GameText.TSpeech(vk));
                }
                b.PopContext();
            }
            BackButton(b);
        }

        // The game's capture screen binds the first HELD key it polls — pushing it while our
        // activating Enter is still down would instantly bind Return. Defer until all keys are up.
        public override void OnUpdate()
        {
            if (_pendingCapture == null || Input.SdlKeyboard.AnyKeyHeld) return;
            var cell = _pendingCapture;
            _pendingCapture = null;
            ControlPanelApi.PushKeyCapture(cell);
        }
    }

    /// <summary>The game's key-capture overlay (deob GClass21): announce the prompt and stand our
    /// input down — every key must reach the game's raw capture (Escape cancels natively).</summary>
    public sealed class GameKeyCaptureScreen : Screen
    {
        public override string Key => "panel.keycapture";
        public override int Layer => 10;
        public override string ScreenName => Loc.T("capture.prompt");
        public override bool CapturesRawInput => true;

        public override bool IsActive()
        {
            var top = GameState.TopScreen();
            var t = ControlPanelApi.KeyCaptureType;
            return top != null && t != null && top.GetType() == t;
        }
    }
}
