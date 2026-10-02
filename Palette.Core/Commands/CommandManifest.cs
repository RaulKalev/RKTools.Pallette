using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Palette.Core.Commands
{
    /// <summary>One command declared in a manifest file (see README, "Adding your own commands").</summary>
    public sealed class ManifestCommand
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> Keywords { get; set; }
        public List<string> Aliases { get; set; }

        /// <summary>Material Design icon name, e.g. "Cctv".</summary>
        public string Icon { get; set; }

        /// <summary>
        /// A Revit command id string resolved with <c>RevitCommandId.LookupCommandId</c> when launched, e.g. the id of a
        /// plugin's ribbon button.
        /// </summary>
        public string RevitCommandId { get; set; }

        /// <summary>Name of a <c>PostableCommand</c> enum value, e.g. "ManageLinks".</summary>
        public string PostableCommand { get; set; }
    }

    public sealed class ManifestFile
    {
        /// <summary>Optional display name of the plugin that owns the commands.</summary>
        public string Source { get; set; }

        public List<ManifestCommand> Commands { get; set; } = new List<ManifestCommand>();

        [JsonIgnore]
        public string FilePath { get; set; }
    }

    public sealed class ManifestLoadResult
    {
        public List<ManifestFile> Files { get; } = new List<ManifestFile>();
        public List<string> Errors { get; } = new List<string>();
    }

    /// <summary>
    /// Reads *.palette.json manifests from a folder. Manifests are the decoupled way for another plugin to join Palette: it
    /// drops a small JSON file and never references Palette's assembly. Invalid files or commands are skipped and reported,
    /// never thrown.
    /// </summary>
    public static class ManifestLoader
    {
        public const string FilePattern = "*.palette.json";

        /// <summary>Cheap change detector (file names, sizes, timestamps) so a re-scan only happens after an edit.</summary>
        public static string Signature(IEnumerable<string> folders)
        {
            try
            {
                var parts = new List<string>();
                foreach (var folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (var f in Directory.GetFiles(folder, FilePattern).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        var info = new FileInfo(f);
                        parts.Add(f + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks);
                    }
                }
                return string.Join(";", parts);
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Scanning manifest folders failed", ex);
                return "";
            }
        }

        public static ManifestLoadResult Load(IEnumerable<string> folders)
        {
            var result = new ManifestLoadResult();
            foreach (var folder in folders)
            {
                string[] files;
                try
                {
                    if (!Directory.Exists(folder)) continue;
                    files = Directory.GetFiles(folder, FilePattern);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(folder + ": " + ex.Message);
                    continue;
                }

                foreach (var path in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var file = JsonConvert.DeserializeObject<ManifestFile>(File.ReadAllText(path));
                        if (file == null || file.Commands == null) throw new JsonException("No \"commands\" array.");
                        file.FilePath = path;
                        file.Commands = file.Commands.Where(c => Validate(c, path, result.Errors)).ToList();
                        result.Files.Add(file);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(Path.GetFileName(path) + ": " + ex.Message);
                    }
                }
            }
            return result;
        }

        private static bool Validate(ManifestCommand c, string path, List<string> errors)
        {
            var file = Path.GetFileName(path);
            if (c == null) return false;
            if (string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name))
            {
                errors.Add(file + ": a command needs \"id\" and \"name\".");
                return false;
            }
            if (string.IsNullOrWhiteSpace(c.RevitCommandId) == string.IsNullOrWhiteSpace(c.PostableCommand))
            {
                errors.Add(file + ": command \"" + c.Id + "\" needs exactly one of \"revitCommandId\" or \"postableCommand\".");
                return false;
            }
            return true;
        }
    }
}
