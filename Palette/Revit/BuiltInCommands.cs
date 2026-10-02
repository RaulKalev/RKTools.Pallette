using System.Collections.Generic;
using Palette.Core.Commands;

namespace Palette.Revit
{
    /// <summary>
    /// Native Revit commands offered out of the box. Each is a <c>PostableCommand</c> that Revit itself lets an add-in post, so
    /// execution is the supported <c>PostCommand</c> route. Names are resolved at run time: a command missing from the
    /// running Revit version (or not postable right now) is simply hidden from the list.
    /// </summary>
    internal static class BuiltInCommands
    {
        private struct Def
        {
            public string Postable, Name, Icon, Category, Description;
            public string[] Keywords;

            public Def(string postable, string name, string icon, string category, string description, params string[] keywords)
            {
                Postable = postable;
                Name = name;
                Icon = icon;
                Category = category;
                Description = description;
                Keywords = keywords;
            }
        }

        private static readonly Def[] Definitions =
        {
            new Def("ManageLinks", "Manage Links", "LinkVariant", "Revit", "Link, reload and unload Revit, CAD and IFC links", "link", "rvt", "cad", "ifc", "reload", "unload"),
            new Def("LinkRevit", "Link Revit", "LinkPlus", "Revit", "Link another Revit model", "link", "rvt", "add"),
            new Def("LinkCAD", "Link CAD", "FileCadBox", "Revit", "Link a DWG or DXF file", "dwg", "dxf", "link", "autocad"),
            new Def("LinkIFC", "Link IFC", "CubeOutline", "Revit", "Link an IFC model", "ifc", "link"),
            new Def("VisibilityOrGraphics", "Visibility/Graphics", "EyeOutline", "Views", "Visibility and graphic overrides for the active view", "vg", "vv", "visibility", "override", "graphics"),
            new Def("Filters", "Filters", "FilterOutline", "Views", "View filters", "filter", "override"),
            new Def("ObjectStyles", "Object Styles", "ShapeOutline", "Revit", "Line weights, colours and patterns per category", "line", "weight", "style"),
            new Def("ManageViewTemplates", "Manage View Templates", "ViewDashboardOutline", "Views", "View templates", "template", "view"),
            new Def("Default3DView", "Default 3D View", "Rotate3d", "Views", "Open the default 3D view", "3d", "isometric", "view"),
            new Def("NewSheet", "New Sheet", "FilePlusOutline", "Documentation", "Create a sheet", "sheet", "drawing", "titleblock"),
            new Def("Print", "Print", "PrinterOutline", "Documentation", "Print dialog", "print", "plot"),
            new Def("ExportPDF", "Export PDF", "FilePdfBox", "Documentation", "Export views and sheets to PDF", "pdf", "export", "print", "sheets"),
            new Def("ExportCADFormatsDWG", "Export DWG", "FileExportOutline", "Documentation", "Export views and sheets to DWG", "dwg", "autocad", "export", "cad"),
            new Def("ExportIFC", "Export IFC", "CubeSend", "Coordination", "Export the model to IFC", "ifc", "export", "bim"),
            new Def("SynchronizeNow", "Synchronize with Central", "Sync", "Coordination", "Synchronize the central model now", "sync", "central", "workshare", "save"),
            new Def("Worksets", "Worksets", "LayersOutline", "Coordination", "Worksets dialog", "workset", "workshare"),
            new Def("DesignOptions", "Design Options", "CallSplit", "Coordination", "Design options", "option", "alternative"),
            new Def("Phases", "Phasing", "TimelineClockOutline", "Coordination", "Project phases", "phase", "demolish", "existing"),
            new Def("ProjectParameters", "Project Parameters", "FormatListBulletedSquare", "Revit", "Project parameters", "parameter", "schedule"),
            new Def("SharedParameters", "Shared Parameters", "ShareVariantOutline", "Revit", "Shared parameter files", "parameter", "shared", "txt"),
            new Def("ProjectInformation", "Project Information", "InformationOutline", "Revit", "Project name, number and other information", "project", "info"),
            new Def("ProjectUnits", "Project Units", "RulerSquare", "Revit", "Units and number formats", "units", "format", "mm"),
            new Def("Materials", "Materials", "Texture", "Revit", "Materials browser", "material"),
            new Def("PurgeUnused", "Purge Unused", "Broom", "Revit", "Remove unused families and types", "purge", "clean", "cleanup", "unused"),
            new Def("TransferProjectStandards", "Transfer Project Standards", "SwapHorizontal", "Revit", "Copy settings between projects", "transfer", "standards", "copy"),
            new Def("SelectById", "Select by ID", "CursorDefaultClickOutline", "Revit", "Select an element by its element id", "id", "element", "select")
        };

        public static IEnumerable<PaletteCommand> Create()
        {
            foreach (var d in Definitions)
            {
                var action = PostCommandAction.ForPostable(d.Postable);
                yield return new PaletteCommand(
                    id: "revit." + d.Postable,
                    name: d.Name,
                    action: action,
                    category: d.Category,
                    description: d.Description,
                    keywords: d.Keywords,
                    iconKind: d.Icon,
                    source: "revit",
                    isAvailable: ctx => ctx.IsPostable(action.AvailabilityKey));
            }
        }
    }
}
