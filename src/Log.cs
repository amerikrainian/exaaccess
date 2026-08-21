using System;
using System.IO;

namespace ExaAccess
{
    /// <summary>
    /// The mod's logger. Unlike the WotR sibling (which routes through Unity's Debug log into
    /// Player.log), EXAPUNKS has no engine log surface we can borrow, so we write our own file under
    /// %LOCALAPPDATA%\ExaAccess\ and mirror to any attached console (a normal GUI launch has none;
    /// Console.WriteLine is then a harmless no-op). A screen-reader user can point support at one
    /// predictable path. Thread-safe: the game hooks (main thread) and the dev HTTP server (its own
    /// thread) both log.
    /// </summary>
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;

        public static string Path => _path;

        public static void Init()
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExaAccess");
                Directory.CreateDirectory(dir);
                _path = System.IO.Path.Combine(dir, "exaaccess.log");
                // Fresh file each launch — one session per log keeps it readable.
                File.WriteAllText(_path, "ExaAccess log — " + DateTime.Now.ToString("s") + Environment.NewLine);
            }
            catch { _path = null; }
        }

        public static void Info(string message) => Write("INFO ", message);
        public static void Warning(string message) => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex) => Write("ERROR", message + Environment.NewLine + ex);

        private static void Write(string level, string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + level + " " + message;
            lock (Gate)
            {
                try { Console.WriteLine(line); } catch { }
                if (_path == null) return;
                try { File.AppendAllText(_path, line + Environment.NewLine); } catch { }
            }
        }
    }
}
