#if DEBUG
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Echopunks.Dev
{
    /// <summary>
    /// The /screenshot endpoint's engine: captures OUR OWN game window via PrintWindow with
    /// PW_RENDERFULLCONTENT, which reads the DWM-composited surface — no focus change, no
    /// foreground requirement, works while occluded (not while minimized). Never bring the window
    /// to the front for a capture; the dev driver runs alongside whatever else the user is doing.
    /// PNGs land under %LOCALAPPDATA%\Echopunks\screenshots. DEBUG-only, like all dev tooling.
    /// </summary>
    internal static class Screenshot
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

        private const uint PwRenderFullContent = 2;

        /// <summary>Capture the game window to a timestamped PNG; returns a report line with the
        /// path, or an error line. Safe from any thread.</summary>
        public static string Capture()
        {
            try
            {
                IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd == IntPtr.Zero) return "[no window] the game window does not exist yet\n";

                Rect r;
                if (!GetWindowRect(hwnd, out r)) return "[error] GetWindowRect failed\n";
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                if (w <= 0 || h <= 0) return "[error] window is minimized or zero-sized (" + w + "x" + h + ")\n";

                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Echopunks", "screenshots");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");

                using (var bmp = new Bitmap(w, h))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        bool ok;
                        try { ok = PrintWindow(hwnd, hdc, PwRenderFullContent); }
                        finally { g.ReleaseHdc(hdc); }
                        if (!ok) return "[error] PrintWindow failed\n";
                    }
                    bmp.Save(path, ImageFormat.Png);
                }
                return path + "\n";
            }
            catch (Exception ex)
            {
                return "[error] " + ex.Message + "\n";
            }
        }
    }
}
#endif
