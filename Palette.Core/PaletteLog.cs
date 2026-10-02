using System;
using System.IO;

namespace Palette.Core
{
    /// <summary>
    /// Minimal best-effort file logger. Writes to %LocalAppData%\Palette\palette.log and never throws.
    /// Used for startup, settings, registration and execution failures; never for every keystroke.
    /// </summary>
    public static class PaletteLog
    {
        private static readonly object Lock = new object();

        /// <summary>Log file location. Overridable so tests never write to the user's real log.</summary>
        public static string LogFilePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Palette", "palette.log");

        public static void Info(string message) => Write("INFO ", message);
        public static void Warn(string message) => Write("WARN ", message);

        public static void Error(string message, Exception ex = null) =>
            Write("ERROR", ex == null ? message : message + " :: " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace);

        private static void Write(string level, string message)
        {
            try
            {
                lock (Lock)
                {
                    var path = LogFilePath;
                    var dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024) fi.Delete();

                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine);
                }
            }
            catch
            {
                // logging must never break the plugin
            }
        }
    }
}
