using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Windows;
using Palette.Core;
using Palette.Core.Commands;

namespace Palette.Revit
{
    /// <summary>
    /// Finds the buttons other RK Tools plugins put on the "RK Tools" ribbon tab so they can be launched from Palette
    /// without a manifest. The id of a Revit ribbon button is exactly what <c>RevitCommandId.LookupCommandId</c> accepts.
    /// </summary>
    internal static class RibbonScanner
    {
        public const string SourceName = "ribbon:RK Tools";

        public static List<PaletteCommand> Scan(string tabName, string ownPanelName)
        {
            var result = new List<PaletteCommand>();
            try
            {
                var ribbon = ComponentManager.Ribbon;
                if (ribbon == null) return result;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var tab in ribbon.Tabs)
                {
                    if (!string.Equals(tab.Title, tabName, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var panel in tab.Panels)
                    {
                        var panelTitle = panel.Source?.Title;
                        if (string.Equals(panelTitle, ownPanelName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (panel.Source == null) continue;
                        Collect(panel.Source.Items, panelTitle, result, seen);
                    }
                }
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Scanning the RK Tools ribbon failed", ex);
            }
            return result;
        }

        private static object ImageOf(RibbonItem item)
        {
            var button = item as RibbonButton;
            return button?.Image ?? button?.LargeImage;
        }

        private static void Collect(System.Collections.IEnumerable items, string panelTitle, List<PaletteCommand> result, HashSet<string> seen)
        {
            if (items == null) return;
            foreach (var obj in items)
            {
                try
                {
                    var item = obj as RibbonItem;
                    if (item == null) continue;
                    var row = item as RibbonRowPanel;
                    if (row != null) { Collect(row.Items, panelTitle, result, seen); continue; }
                    var split = item as RibbonSplitButton;
                    if (split != null) { Collect(split.Items, panelTitle, result, seen); continue; }
                    var list = item as RibbonList;
                    if (list != null) { Collect(list.Items, panelTitle, result, seen); continue; }

                    var id = item.Id;
                    var text = (item.Text ?? item.Name ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                    if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(id)) continue;
                    if (!id.StartsWith("CustomCtrl_", StringComparison.Ordinal)) continue;
                    if (!seen.Add(id)) continue;

                    var action = PostCommandAction.ForCommandId(id);
                    result.Add(new PaletteCommand(
                        id: "ribbon:" + id,
                        name: text,
                        action: action,
                        category: string.IsNullOrWhiteSpace(panelTitle) ? "RK Tools" : panelTitle.Replace("\r", " ").Replace("\n", " ").Trim(),
                        description: "RK Tools ribbon command",
                        keywords: new[] { "rk", "tools" },
                        iconKind: "Puzzle",
                        source: SourceName,
                        isAvailable: ctx => ctx.IsPostable(action.AvailabilityKey),
                        iconImage: () => ImageOf(item)));
                }
                catch (Exception ex)
                {
                    PaletteLog.Error("Reading a ribbon item failed", ex);
                }
            }
        }
    }
}
