using System;
using System.IO;
using Newtonsoft.Json;

namespace Palette.Core.Settings
{
    /// <summary>
    /// Reads and writes one small JSON file safely: writes go to a temp file that then replaces the original
    /// (a crash mid-write cannot truncate the real file), and a file that cannot be parsed is kept aside as
    /// "*.bad-yyyyMMdd-HHmmss" instead of being overwritten or deleted.
    /// </summary>
    internal static class JsonFileStore
    {
        /// <summary>Loads <typeparamref name="T"/>; returns null for "no file" and for an unreadable file (after logging).</summary>
        public static T Load<T>(string path, out string error) where T : class
        {
            error = null;
            try
            {
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) return null;
                var value = JsonConvert.DeserializeObject<T>(text);
                if (value == null) throw new JsonException("The file does not contain a settings object.");
                return value;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                PaletteLog.Error("Could not read " + path + "; using defaults and keeping a copy of the file", ex);
                KeepAside(path);
                return null;
            }
        }

        public static bool TrySave<T>(string path, T value)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(value, Formatting.Indented));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Could not save " + path, ex);
                return false;
            }
        }

        private static void KeepAside(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                File.Copy(path, path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true);
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Could not keep a copy of the unreadable file " + path, ex);
            }
        }
    }
}
