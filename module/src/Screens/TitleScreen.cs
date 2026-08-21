using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    /// <summary>
    /// The first modeled screen: EXAPUNKS's title scene (deob `GClass368` — see CLAUDE.md "Title
    /// screen decoded"). Visually it's a dark room with three MOUSE hotspots and no keyboard
    /// affordance at all; this screen turns them into a two-item menu:
    ///
    ///   • the Sawayama Z7 computer  → pushes DesktopScreen (starts the game)
    ///   • the TEC Constellation II  → pushes ControlPanelScreen (options; the game itself also opens
    ///     this on Escape, which keeps working — we don't swallow keys)
    ///
    /// Activation calls the game's own screen push (GameApi) — byte-identical to the hotspot click.
    /// The third hotspot (TRASH WORLD NEWS) appears only with story progress and needs save-data
    /// reads; it joins when those land (roadmap).
    ///
    /// The game type is obfuscated, so it resolves BY SHAPE, not name: the title screen is the only
    /// IScreen with the hotspot-helper signature bool(float, Texture, Vector2, LocString, LocString,
    /// *, Vector2, Bounds2) — all parameter types keep their real names.
    /// </summary>
    public sealed class TitleScreen : Screen
    {
        private static Type _gameType;
        private static bool _resolved;

        public override string Key => "title";
        public override string ScreenName => Loc.T("screen.title");

        public override bool IsActive()
        {
            var top = GameState.TopScreen();
            return top != null && top.GetType() == GameType;
        }

        public override void Build(GraphBuilder b)
        {
            // Labels are the game's own hotspot LocStrings (each drawn as name + subtitle); only the
            // hints are ours — the game offers no explanation of what the hotspots do.
            b.AddItem(ControlId.Structural("title.computer"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("SAWAYAMA") + " " + GameText.T("Z7 TurboLance"),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => Loc.T("title.computer.hint"), kind: AnnouncementKinds.Tooltip),
                },
                OnActivate = () => GameApi.PushScreen(GameState.GameAssembly.GetType("DesktopScreen")),
            });
            b.AddItem(ControlId.Structural("title.tablet"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("TEC") + " " + GameText.T("Constellation II"),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => Loc.T("title.tablet.hint"), kind: AnnouncementKinds.Tooltip),
                },
                OnActivate = () => GameApi.PushScreen(GameState.GameAssembly.GetType("ControlPanelScreen")),
            });
        }

        private static Type GameType
        {
            get
            {
                if (_resolved) return _gameType;
                _resolved = true;
                try { _gameType = ResolveByShape(); }
                catch (Exception ex) { Log.Error("[title] resolution failed", ex); }
                if (_gameType == null)
                    Log.Error("[title] title-screen type not found by shape — game updated? Screen stays inactive.");
                else
                    Log.Info("[title] game type resolved: " + _gameType.Name);
                return _gameType;
            }
        }

        private static Type ResolveByShape()
        {
            var asm = GameState.GameAssembly;
            if (asm == null) return null;
            var iScreen = asm.GetType("IScreen");
            var texture = asm.GetType("Texture");
            var vector2 = asm.GetType("Vector2");
            var locString = asm.GetType("LocString");
            var bounds2 = asm.GetType("Bounds2");
            if (iScreen == null || texture == null || vector2 == null || locString == null || bounds2 == null)
                return null;

            foreach (var t in asm.ManifestModule.GetTypes())
            {
                if (t.IsInterface || t.IsAbstract || !iScreen.IsAssignableFrom(t)) continue;
                foreach (var m in t.GetMethods(MemberResolver.AllDeclared))
                {
                    if (m.ReturnType != typeof(bool)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 8
                        && ps[0].ParameterType == typeof(float)
                        && ps[1].ParameterType == texture
                        && ps[2].ParameterType == vector2
                        && ps[3].ParameterType == locString
                        && ps[4].ParameterType == locString
                        && ps[6].ParameterType == vector2
                        && ps[7].ParameterType == bounds2)
                        return t;
                }
            }
            return null;
        }
    }
}
