using System;
using System.Collections.Generic;

namespace Palette.Core.Commands
{
    /// <summary>
    /// Marker for "whatever the host needs to run a command". The Revit host passes an object that carries the
    /// <c>UIApplication</c>; Core never sees the Revit API. Commands are only ever executed while the host holds a
    /// valid Revit API context (inside an ExternalEvent), see <c>Palette.Revit.CommandExecutor</c>.
    /// </summary>
    public interface IExecutionContext
    {
    }

    /// <summary>What a command does. Discovery (metadata) and execution are deliberately separate.</summary>
    public interface ICommandAction
    {
        /// <summary>Runs the command. Throwing is allowed; the host logs it and shows a concise message.</summary>
        void Execute(IExecutionContext context);
    }

    /// <summary>Adapts a delegate to <see cref="ICommandAction"/>.</summary>
    public sealed class DelegateAction : ICommandAction
    {
        private readonly Action<IExecutionContext> _run;

        public DelegateAction(Action<IExecutionContext> run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public void Execute(IExecutionContext context) => _run(context);
    }

    /// <summary>
    /// A command Palette can list and launch. Immutable: the registry caches a search index at registration time.
    /// </summary>
    public sealed class PaletteCommand
    {
        public PaletteCommand(
            string id,
            string name,
            ICommandAction action,
            string category = null,
            string description = null,
            IEnumerable<string> keywords = null,
            IEnumerable<string> aliases = null,
            string iconKind = null,
            string source = null,
            Func<PaletteContext, bool> isAvailable = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A command needs an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A command needs a name.", nameof(name));
            Id = id.Trim();
            Name = name.Trim();
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            Description = description ?? "";
            Keywords = Clean(keywords);
            Aliases = Clean(aliases);
            IconKind = iconKind;
            Source = source ?? "";
            Availability = isAvailable;
        }

        /// <summary>Stable unique id, e.g. "camera.fov" or "revit.ManageLinks". Persisted in settings.</summary>
        public string Id { get; }

        public string Name { get; }
        public string Category { get; }
        public string Description { get; }

        /// <summary>Extra search words ("cctv", "coverage").</summary>
        public IReadOnlyList<string> Keywords { get; }

        /// <summary>Short alternative names / abbreviations ("fov"). Ranked like a name match.</summary>
        public IReadOnlyList<string> Aliases { get; }

        /// <summary>Material Design icon name (MaterialDesignThemes PackIconKind), resolved lazily by the UI.</summary>
        public string IconKind { get; }

        /// <summary>Who registered the command ("revit", "palette", a manifest file, a plugin name).</summary>
        public string Source { get; }

        public ICommandAction Action { get; }

        /// <summary>Optional availability rule. Null means always available.</summary>
        public Func<PaletteContext, bool> Availability { get; }

        /// <summary>Evaluates <see cref="Availability"/>; a throwing rule counts as unavailable.</summary>
        public bool IsAvailable(PaletteContext context)
        {
            if (Availability == null) return true;
            try { return Availability(context ?? PaletteContext.Empty); }
            catch (Exception ex)
            {
                PaletteLog.Error("Availability check failed for " + Id, ex);
                return false;
            }
        }

        public void Execute(IExecutionContext context) => Action.Execute(context);

        public override string ToString() => Id + " (" + Name + ")";

        private static IReadOnlyList<string> Clean(IEnumerable<string> values)
        {
            var list = new List<string>();
            if (values == null) return list;
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v)) list.Add(v.Trim());
            return list;
        }
    }
}
