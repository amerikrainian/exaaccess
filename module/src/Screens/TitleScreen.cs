using Echopunks.Game;
using Echopunks.Localization;
using Echopunks.UI;
using Echopunks.UI.Graph;

namespace Echopunks.Screens
{
    /// <summary>
    /// The first modeled screen: EXAPUNKS's title scene (deob GClass368 — typed via the remap
    /// pipeline). Its three mouse hotspots become buttons; activation calls the game's own screen
    /// push, byte-identical to the hotspot click. Labels are the game's hotspot LocStrings. The
    /// TRASH WORLD NEWS hotspot exists only once the "ghast-1" story beat is done — the same save
    /// check the game draws it with.
    /// </summary>
    public sealed class TitleScreen : Screen
    {
        public override string Key => "title";
        public override string ScreenName => Loc.T("screen.title");

        public override bool IsActive() => GameState.TopScreen() is GClass368;

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("title.computer"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("SAWAYAMA") + " " + GameText.T("Z7 TurboLance"),
                        kind: AnnouncementKinds.Label),
                },
                OnActivate = () => GameApi.PushScreen(new DesktopScreen()),
            });
            b.AddItem(ControlId.Structural("title.tablet"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("TEC") + " " + GameText.T("Constellation II"),
                        kind: AnnouncementKinds.Label),
                },
                OnActivate = () => GameApi.PushScreen(new ControlPanelScreen()),
            });
            if (NewsUnlocked())
            {
                b.AddItem(ControlId.Structural("title.news"), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => GameText.TSpeech("TRASH\nWORLD") + " " + GameText.T("NEWS"),
                            kind: AnnouncementKinds.Label),
                    },
                    OnActivate = () => GameApi.PushScreen(new GClass214(0)),
                });
            }
        }

        private static bool NewsUnlocked()
        {
            try
            {
                var gl = GameLogic.gameLogic_0;
                return gl != null && gl.saveData_0.method_13("ghast-1", 0);
            }
            catch { return false; }
        }
    }
}
