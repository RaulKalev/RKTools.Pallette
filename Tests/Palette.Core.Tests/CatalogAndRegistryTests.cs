using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Palette.Core;
using Palette.Core.Commands;
using Palette.Core.Settings;
using Xunit;

namespace Palette.Core.Tests
{
    public class CatalogAndRegistryTests : TempFolderTest
    {
        private readonly PaletteCommandRegistry _registry = new PaletteCommandRegistry();
        private readonly SettingsService _settings;
        private readonly RecentCommandsService _recents;
        private readonly PaletteCatalog _catalog;

        public CatalogAndRegistryTests()
        {
            _settings = new SettingsService(Path.Combine(Folder, "settings.json"));
            _recents = new RecentCommandsService(Path.Combine(Folder, "recent.json"));
            _catalog = new PaletteCatalog(_registry, _settings, _recents);
            foreach (var n in new[] { "alpha", "bravo", "charlie", "delta", "echo" })
                _registry.Register(Make.Command("t." + n, char.ToUpper(n[0]) + n.Substring(1)));
        }

        private string[] Home(PaletteContext ctx = null) =>
            _catalog.BuildHome(ctx ?? PaletteContext.Empty).Select(e => e.Kind == EntryKind.Header ? "#" + e.Header : e.Command.Id).ToArray();

        [Fact]
        public void Home_ShowsFavoritesThenRecentThenRest()
        {
            _settings.SetFavorite("t.delta", true);
            _settings.SetFavorite("t.bravo", true);
            _recents.RecordUse("t.echo");
            _recents.RecordUse("t.delta"); // already a favourite: not repeated under Recent

            Assert.Equal(new[] { "#Favorites", "t.delta", "t.bravo", "#Recently used", "t.echo", "#Commands", "t.alpha", "t.charlie" }, Home());
        }

        [Fact]
        public void Home_FavoriteOrderIsTheUsersOrder()
        {
            _settings.SetFavorite("t.alpha", true);
            _settings.SetFavorite("t.bravo", true);
            _settings.MoveFavorite("t.bravo", -1);
            Assert.Equal(new[] { "t.bravo", "t.alpha" }, Home().Skip(1).Take(2).ToArray());
        }

        [Fact]
        public void Home_WithoutFavoritesOrRecent_IsPlainList()
        {
            var home = Home();
            Assert.DoesNotContain(home, h => h.StartsWith("#"));
            Assert.Equal(5, home.Length);
        }

        [Fact]
        public void Hidden_CommandsNeverAppear_InHomeOrSearch()
        {
            _settings.SetHidden("t.alpha", true);
            Assert.DoesNotContain("t.alpha", Home());
            Assert.Empty(_catalog.BuildSearch("alpha", PaletteContext.Empty));
        }

        [Fact]
        public void Recent_IsLimitedToRecentShown()
        {
            _settings.Current.RecentShown = 2;
            foreach (var n in new[] { "alpha", "bravo", "charlie", "delta" }) _recents.RecordUse("t." + n);
            var home = Home();
            Assert.Equal(new[] { "#Recently used", "t.delta", "t.charlie" }, home.Take(3).ToArray());
        }

        [Fact]
        public void CustomOrder_DrivesTheRest()
        {
            _settings.SetOrder(new[] { "t.echo", "t.delta" });
            Assert.Equal(new[] { "t.echo", "t.delta", "t.alpha", "t.bravo", "t.charlie" }, Home());
        }

        [Fact]
        public void UnavailableCommands_AreFilteredByContext()
        {
            _registry.Register(Make.Command("t.needsdoc", "Needs Document", available: c => c.HasDocument));
            Assert.DoesNotContain("t.needsdoc", Home(PaletteContext.Empty));
            Assert.Contains("t.needsdoc", Home(new PaletteContext(hasDocument: true)));
        }

        [Fact]
        public void ContextAware_CommandAppearsOnlyWithMatchingSelection()
        {
            _registry.Register(Make.Command("t.panel", "Open Panel Schematic", available: c => c.IsCategorySelected("Electrical Equipment")));
            Assert.Empty(_catalog.BuildSearch("panel", new PaletteContext(hasDocument: true)));
            Assert.Single(_catalog.BuildSearch("panel", new PaletteContext(hasDocument: true, selectedCategories: new[] { "electrical equipment" })));
        }

        [Fact]
        public void ThrowingAvailabilityRule_IsTreatedAsUnavailable()
        {
            _registry.Register(Make.Command("t.bad", "Bad Rule", available: c => { throw new InvalidOperationException(); }));
            Assert.DoesNotContain("t.bad", Home());
        }

        [Fact]
        public void FavoritesOfMissingCommands_AreKeptButNotShown()
        {
            _settings.SetFavorite("plugin.not.loaded", true);
            Assert.DoesNotContain("plugin.not.loaded", Home());
            Assert.True(_settings.IsFavorite("plugin.not.loaded"));
        }

        [Fact]
        public void Registry_ReplacesById_AndUnregistersBySource()
        {
            _registry.Register(Make.Command("t.alpha", "Alpha Renamed"));
            Assert.Equal("Alpha Renamed", _registry.Get("t.alpha").Name);
            Assert.Equal(5, _registry.Count);
            Assert.Equal(5, _registry.UnregisterSource("test"));
            Assert.Equal(0, _registry.Count);
        }

        [Fact]
        public void Registry_RangeSkipsInvalidCommands_WithoutThrowing()
        {
            var ok = _registry.RegisterRange(new[] { Make.Command("t.new", "New"), null });
            Assert.Equal(1, ok);
        }

        [Fact]
        public void Registry_VersionAndChangedEvent()
        {
            var fired = 0;
            _registry.Changed += (s, e) => fired++;
            var v = _registry.Version;
            _registry.Register(Make.Command("t.x", "X"));
            Assert.Equal(v + 1, _registry.Version);
            Assert.Equal(1, fired);
        }

        [Fact]
        public void Command_RequiresIdAndName()
        {
            Assert.Throws<ArgumentException>(() => Make.Command("", "x"));
            Assert.Throws<ArgumentException>(() => Make.Command("x", " "));
        }

        [Fact]
        public void Manifest_LoadsValidCommands_ReportsInvalidOnes_AndSkipsBrokenFiles()
        {
            var dir = Path.Combine(Folder, "m");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "camera.palette.json"), JsonConvert.SerializeObject(new
            {
                source = "Camera FOV",
                commands = new object[]
                {
                    new { id = "camera.fov", name = "Camera FOV", category = "Security", keywords = new[] { "cctv" }, revitCommandId = "CustomCtrl_%x" },
                    new { id = "bad", name = "Missing target" },
                    new { id = "both", name = "Both", revitCommandId = "a", postableCommand = "b" }
                }
            }));
            File.WriteAllText(Path.Combine(dir, "broken.palette.json"), "{ nope");
            File.WriteAllText(Path.Combine(dir, "ignored.json"), "{}");

            var result = ManifestLoader.Load(new[] { dir, Path.Combine(Folder, "does-not-exist") });
            Assert.Single(result.Files);
            Assert.Equal("camera.fov", result.Files[0].Commands.Single().Id);
            Assert.Equal(3, result.Errors.Count);
        }

        [Fact]
        public void Manifest_SignatureChangesWhenAFileChanges()
        {
            var dir = Path.Combine(Folder, "m2");
            Directory.CreateDirectory(dir);
            var before = ManifestLoader.Signature(new[] { dir });
            File.WriteAllText(Path.Combine(dir, "a.palette.json"), "{\"commands\":[]}");
            var after = ManifestLoader.Signature(new[] { dir });
            Assert.NotEqual(before, after);
            Assert.Equal(after, ManifestLoader.Signature(new[] { dir }));
        }
    }
}
