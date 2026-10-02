using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Palette.Core.Commands;
using Palette.Revit;

namespace Palette
{
    /// <summary>
    /// In-process entry point for plugins that want to appear in Palette.
    ///
    /// Two ways to integrate, from loosest to tightest coupling:
    ///  1. A manifest file (<c>%AppData%\Palette\Commands\*.palette.json</c>): no reference to Palette at all; Palette
    ///     launches the plugin's ribbon command through Revit's own <c>PostCommand</c>. Prefer this.
    ///  2. This API, when the plugin can reference Palette.dll and both are loaded into the same context. Registration is
    ///     safe at any time (also before Palette's own startup); the delegate runs later inside a valid Revit API
    ///     context (ExternalEvent), so it may use the UIApplication freely, including opening transactions.
    ///
    /// Palette never calls another plugin's IExternalCommand.Execute itself.
    /// </summary>
    public static class PaletteApi
    {
        /// <summary>Registers (or replaces) a command that runs <paramref name="execute"/> inside a Revit API context.</summary>
        public static void Register(
            string id,
            string name,
            Action<UIApplication> execute,
            string category = null,
            string description = null,
            IEnumerable<string> keywords = null,
            IEnumerable<string> aliases = null,
            string iconKind = null,
            string source = null,
            Func<PaletteContext, bool> isAvailable = null)
        {
            PaletteHost.Registry.Register(new PaletteCommand(id, name, new RevitDelegateAction(execute), category, description,
                keywords, aliases, iconKind, source ?? "api", isAvailable));
        }

        /// <summary>
        /// Registers a command that launches an existing Revit command (for example your own ribbon button) with
        /// <c>PostCommand</c>. <paramref name="revitCommandId"/> is the string accepted by
        /// <c>RevitCommandId.LookupCommandId</c>.
        /// </summary>
        public static void RegisterRevitCommand(
            string id,
            string name,
            string revitCommandId,
            string category = null,
            string description = null,
            IEnumerable<string> keywords = null,
            IEnumerable<string> aliases = null,
            string iconKind = null,
            string source = null)
        {
            var action = PostCommandAction.ForCommandId(revitCommandId);
            PaletteHost.Registry.Register(new PaletteCommand(id, name, action, category, description, keywords, aliases,
                iconKind, source ?? "api", ctx => ctx.IsPostable(action.AvailabilityKey)));
        }

        /// <summary>Removes a command previously registered with this id.</summary>
        public static bool Unregister(string id) => PaletteHost.Registry.Unregister(id);
    }
}
