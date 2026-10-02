using System;
using System.Collections.Generic;
using System.Linq;

namespace Palette.Core.Commands
{
    /// <summary>A registered command plus its pre-normalised search fields (computed once, not per keystroke).</summary>
    public sealed class IndexedCommand
    {
        internal IndexedCommand(PaletteCommand command)
        {
            Command = command;
            Name = TextNormalizer.Normalize(command.Name);
            NameWords = TextNormalizer.Words(Name);
            Initials = new string(NameWords.Where(w => w.Length > 0).Select(w => w[0]).ToArray());
            Category = TextNormalizer.Normalize(command.Category);
            Aliases = command.Aliases.Select(TextNormalizer.Normalize).ToArray();
            Keywords = command.Keywords.Select(TextNormalizer.Normalize).ToArray();
            Description = TextNormalizer.Normalize(command.Description);
        }

        public PaletteCommand Command { get; }
        public string Name { get; }
        public string[] NameWords { get; }
        public string Initials { get; }
        public string Category { get; }
        public string[] Aliases { get; }
        public string[] Keywords { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Central store of everything Palette can launch. Commands can come from several sources (built-in Revit commands,
    /// manifests, other plugins); the UI only ever talks to the registry. Thread-safe; readers get an immutable
    /// snapshot that is rebuilt only when the registry changes.
    /// </summary>
    public sealed class PaletteCommandRegistry
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, IndexedCommand> _byId = new Dictionary<string, IndexedCommand>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<IndexedCommand> _snapshot = new IndexedCommand[0];
        private bool _dirty;
        private int _version;

        /// <summary>Raised after a registration change. May be raised on any thread.</summary>
        public event EventHandler Changed;

        /// <summary>Incremented on every change; lets callers cache derived data.</summary>
        public int Version
        {
            get { lock (_lock) return _version; }
        }

        /// <summary>Registers a command; an existing command with the same id is replaced.</summary>
        public void Register(PaletteCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var indexed = new IndexedCommand(command);
            lock (_lock)
            {
                _byId[command.Id] = indexed;
                _dirty = true;
                _version++;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Registers several commands, logging (not throwing) for any that are invalid.</summary>
        public int RegisterRange(IEnumerable<PaletteCommand> commands)
        {
            var count = 0;
            foreach (var c in commands)
            {
                try { Register(c); count++; }
                catch (Exception ex) { PaletteLog.Error("Command registration failed", ex); }
            }
            return count;
        }

        public bool Unregister(string id)
        {
            bool removed;
            lock (_lock)
            {
                removed = id != null && _byId.Remove(id);
                if (removed) { _dirty = true; _version++; }
            }
            if (removed) Changed?.Invoke(this, EventArgs.Empty);
            return removed;
        }

        /// <summary>Removes every command registered by one source (used when a manifest file changes).</summary>
        public int UnregisterSource(string source)
        {
            int removed;
            lock (_lock)
            {
                var ids = _byId.Values.Where(c => string.Equals(c.Command.Source, source, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Command.Id).ToList();
                foreach (var id in ids) _byId.Remove(id);
                removed = ids.Count;
                if (removed > 0) { _dirty = true; _version++; }
            }
            if (removed > 0) Changed?.Invoke(this, EventArgs.Empty);
            return removed;
        }

        public PaletteCommand Get(string id)
        {
            lock (_lock)
            {
                IndexedCommand c;
                return id != null && _byId.TryGetValue(id, out c) ? c.Command : null;
            }
        }

        public bool Contains(string id) => Get(id) != null;

        /// <summary>All commands in a stable order (category, then name).</summary>
        public IReadOnlyList<IndexedCommand> Snapshot()
        {
            lock (_lock)
            {
                if (_dirty)
                {
                    _snapshot = _byId.Values
                        .OrderBy(c => c.Command.Category, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(c => c.Command.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    _dirty = false;
                }
                return _snapshot;
            }
        }

        public IEnumerable<PaletteCommand> All => Snapshot().Select(c => c.Command);

        public int Count
        {
            get { lock (_lock) return _byId.Count; }
        }
    }
}
