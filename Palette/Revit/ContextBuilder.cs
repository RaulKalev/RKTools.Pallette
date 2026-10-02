using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Palette.Core;
using Palette.Core.Commands;

namespace Palette.Revit
{
    /// <summary>
    /// Captures the Revit state Palette needs (active document/view, selection, which commands are postable) while a
    /// valid API context is available. Cheap by design: a handful of API calls, no element collection.
    /// </summary>
    internal static class ContextBuilder
    {
        private const int MaxSelectionInspected = 200;

        public static PaletteContext Build(UIApplication app, PaletteCommandRegistry registry)
        {
            try
            {
                var uidoc = app.ActiveUIDocument;
                var doc = uidoc?.Document;

                string viewType = "";
                var selectedCount = 0;
                var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (doc != null)
                {
                    try { viewType = doc.ActiveView?.ViewType.ToString() ?? ""; }
                    catch { /* no active view */ }

                    var ids = uidoc.Selection.GetElementIds();
                    selectedCount = ids.Count;
                    foreach (var id in ids.Take(MaxSelectionInspected))
                    {
                        var name = doc.GetElement(id)?.Category?.Name;
                        if (!string.IsNullOrEmpty(name)) categories.Add(name);
                    }
                }

                var postable = new List<string>();
                foreach (var command in registry.All)
                {
                    var action = command.Action as IPostableAction;
                    if (action != null && action.CanPost(app)) postable.Add(action.AvailabilityKey);
                }

                return new PaletteContext(
                    hasDocument: doc != null,
                    isFamilyDocument: doc != null && doc.IsFamilyDocument,
                    documentTitle: doc?.Title,
                    activeViewType: viewType,
                    selectionCount: selectedCount,
                    selectedCategories: categories,
                    postableCommands: postable);
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Reading the Revit context failed; Palette opens with an empty context", ex);
                return PaletteContext.Empty;
            }
        }
    }
}
