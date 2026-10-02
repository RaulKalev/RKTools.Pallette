using System;
using System.Collections.Concurrent;
using Autodesk.Revit.UI;
using Palette.Core;
using Palette.Core.Commands;

namespace Palette.Revit
{
    /// <summary>
    /// Runs Palette commands inside a valid Revit API context.
    ///
    /// Revit API restriction: the Palette window is a modeless WPF window, so its events arrive on the UI thread but
    /// outside any API context, where almost every Revit call (PostCommand included) is illegal. The window therefore
    /// only enqueues the chosen command and raises this ExternalEvent; Revit calls <see cref="Execute"/> when it is safe,
    /// handing over the <see cref="UIApplication"/>. The event must be created inside an API context (add-in startup).
    /// </summary>
    internal sealed class CommandExecutor : IExternalEventHandler
    {
        private readonly ConcurrentQueue<PaletteCommand> _queue = new ConcurrentQueue<PaletteCommand>();
        private readonly ExternalEvent _event;

        public CommandExecutor()
        {
            _event = ExternalEvent.Create(this);
        }

        public void Enqueue(PaletteCommand command)
        {
            _queue.Enqueue(command);
            try
            {
                _event.Raise();
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Raising the Palette ExternalEvent failed", ex);
            }
        }

        public void Execute(UIApplication app)
        {
            PaletteCommand command;
            while (_queue.TryDequeue(out command))
            {
                try
                {
                    command.Execute(new RevitExecutionContext(app));
                }
                catch (Exception ex)
                {
                    // A broken command must never take Palette or Revit down: log it and tell the user briefly.
                    PaletteLog.Error("Command failed: " + command.Id, ex);
                    TryShowError(command, ex);
                }
            }
        }

        public string GetName() => "Palette command executor";

        private static void TryShowError(PaletteCommand command, Exception ex)
        {
            try
            {
                var dialog = new TaskDialog("Palette")
                {
                    MainInstruction = "“" + command.Name + "” could not be run.",
                    MainContent = ex.Message,
                    CommonButtons = TaskDialogCommonButtons.Close
                };
                dialog.Show();
            }
            catch (Exception inner)
            {
                PaletteLog.Error("Showing the command error failed", inner);
            }
        }
    }
}
