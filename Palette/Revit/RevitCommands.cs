using System;
using Autodesk.Revit.UI;
using Palette.Core.Commands;

namespace Palette.Revit
{
    /// <summary>
    /// Execution context handed to commands: proof that the call happens inside a valid Revit API context
    /// (an ExternalEvent handler), carrying the <see cref="UIApplication"/> to use.
    /// </summary>
    public sealed class RevitExecutionContext : IExecutionContext
    {
        public RevitExecutionContext(UIApplication uiApplication)
        {
            UiApplication = uiApplication;
        }

        public UIApplication UiApplication { get; }
    }

    /// <summary>A command that can report, from inside the Revit API context, whether it can run right now.</summary>
    internal interface IPostableAction
    {
        /// <summary>Key under which <see cref="PaletteContext.IsPostable"/> reports availability.</summary>
        string AvailabilityKey { get; }

        bool CanPost(UIApplication app);
    }

    /// <summary>
    /// Runs a command through the supported <see cref="UIApplication.PostCommand"/> route: either a built-in
    /// <see cref="PostableCommand"/> (by enum name, so one build works on every Revit version that has it) or any
    /// command Revit knows by id string (<see cref="RevitCommandId.LookupCommandId"/>, e.g. a plugin's ribbon button).
    ///
    /// Revit API restriction: PostCommand only works inside a valid API context, so this is only ever called from the
    /// ExternalEvent executor, never from the WPF window. Palette never instantiates another plugin's IExternalCommand.
    /// </summary>
    internal sealed class PostCommandAction : ICommandAction, IPostableAction
    {
        private readonly string _postableName;
        private readonly string _commandIdString;
        private RevitCommandId _resolved;

        private PostCommandAction(string postableName, string commandIdString)
        {
            _postableName = postableName;
            _commandIdString = commandIdString;
        }

        public static PostCommandAction ForPostable(string postableCommandName) => new PostCommandAction(postableCommandName, null);

        public static PostCommandAction ForCommandId(string revitCommandId) => new PostCommandAction(null, revitCommandId);

        public string AvailabilityKey => _postableName != null ? "postable:" + _postableName : "id:" + _commandIdString;

        public bool CanPost(UIApplication app)
        {
            try
            {
                var id = Resolve();
                return id != null && app.CanPostCommand(id);
            }
            catch
            {
                return false;
            }
        }

        public void Execute(IExecutionContext context)
        {
            var app = (context as RevitExecutionContext)?.UiApplication;
            if (app == null) throw new InvalidOperationException("No Revit API context available.");

            var id = Resolve();
            if (id == null) throw new InvalidOperationException("Revit does not know this command (it may not exist in this Revit version, or its add-in is not loaded).");
            if (!app.CanPostCommand(id)) throw new InvalidOperationException("Revit cannot run this command right now (for example no project is open, or another command is active).");
            app.PostCommand(id);
        }

        private RevitCommandId Resolve()
        {
            if (_resolved != null) return _resolved;
            RevitCommandId id = null;
            if (_postableName != null)
            {
                PostableCommand postable;
                if (Enum.TryParse(_postableName, out postable)) id = RevitCommandId.LookupPostableCommandId(postable);
            }
            else if (!string.IsNullOrWhiteSpace(_commandIdString))
            {
                id = RevitCommandId.LookupCommandId(_commandIdString);
            }
            // Cache only successful lookups: a plugin's command id can appear after Palette started.
            _resolved = id;
            return id;
        }
    }

    /// <summary>Runs a delegate with the <see cref="UIApplication"/>; used by plugins that register through <see cref="PaletteApi"/>.</summary>
    internal sealed class RevitDelegateAction : ICommandAction
    {
        private readonly Action<UIApplication> _run;

        public RevitDelegateAction(Action<UIApplication> run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public void Execute(IExecutionContext context)
        {
            var app = (context as RevitExecutionContext)?.UiApplication;
            if (app == null) throw new InvalidOperationException("No Revit API context available.");
            _run(app);
        }
    }
}
