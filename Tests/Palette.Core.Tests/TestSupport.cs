using System;
using System.IO;
using Palette.Core;
using Palette.Core.Commands;

namespace Palette.Core.Tests
{
    /// <summary>A scratch folder per test class instance; the log is redirected so tests never touch the user's real files.</summary>
    public abstract class TempFolderTest : IDisposable
    {
        protected TempFolderTest()
        {
            Folder = Path.Combine(Path.GetTempPath(), "PaletteTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
            PaletteLog.LogFilePath = Path.Combine(Folder, "palette.log");
        }

        protected string Folder { get; }

        public void Dispose()
        {
            try { Directory.Delete(Folder, true); } catch { /* best effort */ }
        }
    }

    internal static class Make
    {
        public static readonly ICommandAction Nothing = new DelegateAction(c => { });

        public static PaletteCommand Command(string id, string name, string category = "Revit", string[] keywords = null,
            string[] aliases = null, string description = null, Func<PaletteContext, bool> available = null) =>
            new PaletteCommand(id, name, Nothing, category, description, keywords, aliases, null, "test", available);
    }
}
