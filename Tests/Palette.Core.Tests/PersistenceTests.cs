using System;
using System.IO;
using System.Linq;
using Palette.Core.Settings;
using Xunit;

namespace Palette.Core.Tests
{
    public class PersistenceTests : TempFolderTest
    {
        private string SettingsPath => Path.Combine(Folder, "settings.json");

        [Fact]
        public void Favorites_PersistAcrossInstances_InOrder()
        {
            var a = new SettingsService(SettingsPath);
            a.Load();
            a.SetFavorite("b", true);
            a.SetFavorite("a", true);
            a.SetFavorite("c", true);
            a.MoveFavorite("c", -2);

            var b = new SettingsService(SettingsPath);
            b.Load();
            Assert.Equal(new[] { "c", "b", "a" }, b.Current.Favorites.ToArray());
        }

        [Fact]
        public void Unfavorite_Persists()
        {
            var a = new SettingsService(SettingsPath);
            a.SetFavorite("x", true);
            a.SetFavorite("x", false);
            var b = new SettingsService(SettingsPath);
            b.Load();
            Assert.False(b.IsFavorite("x"));
        }

        [Fact]
        public void Hidden_Persists_AndHidingRemovesFavorite()
        {
            var a = new SettingsService(SettingsPath);
            a.SetFavorite("x", true);
            a.SetHidden("x", true);
            var b = new SettingsService(SettingsPath);
            b.Load();
            Assert.True(b.IsHidden("x"));
            Assert.False(b.IsFavorite("x"));
        }

        [Fact]
        public void Order_Persists()
        {
            var a = new SettingsService(SettingsPath);
            a.SetOrder(new[] { "z", "y", "x" });
            a.MoveInOrder(a.Current.Order, "x", -1);
            var b = new SettingsService(SettingsPath);
            b.Load();
            Assert.Equal(new[] { "z", "x", "y" }, b.Current.Order.ToArray());
        }

        [Fact]
        public void MalformedFile_FallsBackToDefaults_AndIsKeptAside()
        {
            File.WriteAllText(SettingsPath, "{ this is not json");
            var s = new SettingsService(SettingsPath);
            s.Load();
            Assert.NotNull(s.LoadError);
            Assert.Empty(s.Current.Favorites);
            Assert.Contains(Directory.GetFiles(Folder), f => Path.GetFileName(f).StartsWith("settings.json.bad-"));
            Assert.Equal("{ this is not json", File.ReadAllText(SettingsPath)); // not overwritten by loading
        }

        [Fact]
        public void WrongShapes_AreTolerated()
        {
            File.WriteAllText(SettingsPath, "{\"version\":99,\"favorites\":[\"a\",null,\"A\",\"\",\"b\"],\"unknown\":{\"x\":1},\"recentShown\":500}");
            var s = new SettingsService(SettingsPath);
            s.Load();
            Assert.Equal(new[] { "a", "b" }, s.Current.Favorites.ToArray());
            Assert.Equal(20, s.Current.RecentShown);
        }

        [Fact]
        public void EmptyOrMissingFile_IsDefaults()
        {
            var s = new SettingsService(SettingsPath);
            s.Load();
            Assert.Null(s.LoadError);
            File.WriteAllText(SettingsPath, "");
            s.Load();
            Assert.Null(s.LoadError);
            Assert.Empty(s.Current.Favorites);
        }

        [Fact]
        public void Recent_PersistsNewestFirst_WithCounts_AndIsCapped()
        {
            var path = Path.Combine(Folder, "recent.json");
            var r = new RecentCommandsService(path);
            var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 20; i++) r.RecordUse("c" + i, t.AddMinutes(i));
            r.RecordUse("c19", t.AddMinutes(30));

            var again = new RecentCommandsService(path);
            again.Load();
            Assert.Equal(RecentCommandsService.MaxStored, again.Entries.Count);
            Assert.Equal("c19", again.RecentIds(1)[0]);
            Assert.Equal(2, again.UseCount("c19"));
            Assert.Equal(new[] { "c19", "c18", "c17" }, again.RecentIds(3).ToArray());
        }

        [Fact]
        public void Recent_MalformedFile_StartsEmpty()
        {
            var path = Path.Combine(Folder, "recent.json");
            File.WriteAllText(path, "[[[");
            var r = new RecentCommandsService(path);
            r.Load();
            Assert.Empty(r.Entries);
            r.RecordUse("a");
            Assert.Single(r.Entries);
        }
    }
}
