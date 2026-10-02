using System.Linq;
using Palette.Core;
using Palette.Core.Commands;
using Xunit;

namespace Palette.Core.Tests
{
    public class SearchTests
    {
        private readonly PaletteCommandRegistry _registry = new PaletteCommandRegistry();
        private readonly SearchService _search = new SearchService();

        public SearchTests()
        {
            _registry.Register(Make.Command("camera.fov", "Camera FOV", "Security", new[] { "camera", "cctv", "fov", "coverage" }, new[] { "fov" }));
            _registry.Register(Make.Command("panel.schematics", "Panel Schematics", "Electrical"));
            _registry.Register(Make.Command("panel.edit", "Edit Panel", "Electrical"));
            _registry.Register(Make.Command("pulse.panel", "Pulse — Analyse Panel", "Fire Alarm"));
            _registry.Register(Make.Command("revit.pdf", "Export PDF", "Revit", new[] { "print", "sheets" }));
            _registry.Register(Make.Command("revit.links", "Manage Links", "Revit", new[] { "link", "rvt" }));
            _registry.Register(Make.Command("revit.vg", "Visibility/Graphics", "Views", new[] { "vg", "visibility" }));
            _registry.Register(Make.Command("eule.ohk", "Õhk Calculator", "EULE"));
        }

        private string[] Ids(string query) =>
            _search.Search(query, _registry.Snapshot()).Select(r => r.Command.Id).ToArray();

        [Fact]
        public void Prefix_FindsCameraFov() => Assert.Equal("camera.fov", Ids("cam").First());

        [Fact]
        public void Panel_ReturnsAllThreePanelCommands()
        {
            var ids = Ids("panel");
            Assert.Contains("panel.schematics", ids);
            Assert.Contains("panel.edit", ids);
            Assert.Contains("pulse.panel", ids);
            Assert.Equal("panel.schematics", ids.First()); // starts-with beats word-start
        }

        [Fact]
        public void ExactName_BeatsEverything() => Assert.Equal("revit.links", Ids("manage links").First());

        [Fact]
        public void Keyword_Matches() => Assert.Equal("camera.fov", Ids("cctv").First());

        [Fact]
        public void KeywordNotInName_FindsCommand() => Assert.Contains("revit.pdf", Ids("sheets"));

        [Fact]
        public void Alias_RanksLikeName() => Assert.Equal("camera.fov", Ids("fov").First());

        [Fact]
        public void Category_Matches() => Assert.Contains("camera.fov", Ids("security"));

        [Fact]
        public void WordStartInsideName_Matches() => Assert.Contains("pulse.panel", Ids("analyse"));

        [Fact]
        public void Initials_Match() => Assert.Equal("camera.fov", Ids("cf").First());

        [Fact]
        public void LooseSubsequence_Matches() => Assert.Contains("revit.vg", Ids("vsb"));

        [Fact]
        public void MultipleWords_AllMustMatch()
        {
            Assert.Equal(new[] { "pulse.panel" }, Ids("pulse panel"));
            Assert.Empty(Ids("pulse camera"));
        }

        [Fact]
        public void Diacritics_AreFolded() => Assert.Equal("eule.ohk", Ids("ohk").First());

        [Fact]
        public void CaseIsIgnored() => Assert.Equal("camera.fov", Ids("CAMERA fov").First());

        [Fact]
        public void NoMatch_ReturnsEmpty() => Assert.Empty(Ids("zzzz"));

        [Fact]
        public void EmptyQuery_ReturnsEmpty() => Assert.Empty(Ids("   "));

        [Fact]
        public void Boost_OnlyReordersInsideTheSameBand()
        {
            var results = _search.Search("panel", _registry.Snapshot(), c => c.Id == "pulse.panel" ? SearchService.MaxBoost : 0);
            Assert.Equal("panel.schematics", results[0].Command.Id);
        }

        [Fact]
        public void Boost_BreaksTiesInsideABand()
        {
            // "Edit Panel" and "Pulse - Analyse Panel" are both word-start matches; the boosted one comes first.
            var ids = _search.Search("panel", _registry.Snapshot(), c => c.Id == "pulse.panel" ? 10 : 0).Select(r => r.Command.Id).ToList();
            Assert.True(ids.IndexOf("pulse.panel") < ids.IndexOf("panel.edit"));
        }
    }
}
