using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Palette.Core;
using Palette.Revit;

namespace Palette
{
    /// <summary>
    /// The ribbon button, and the command a user assigns a Revit keyboard shortcut to. It only opens the Palette window:
    /// the window is modeless, so this returns immediately and the chosen command is run later by the ExternalEvent
    /// executor. No transaction is used here.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class OpenPaletteCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                PaletteHost.ShowPalette(commandData.Application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Opening Palette failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
