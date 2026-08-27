using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.Elements;
using Bentley.GeometryNET;

namespace PipingDataExporter
{
    /// <summary>
    /// <c>PipingDataExporter DrawLine</c> - sanity check that the AddIn loaded and can write to the
    /// active model. Scaffolding; not part of the export feature.
    /// </summary>
    public static class DrawLineCommand
    {
        private const string CommandTitle = "Draw Line";

        private static readonly DPoint3d StartPoint = new DPoint3d(0, 0, 0);
        private static readonly DPoint3d EndPoint = new DPoint3d(5000, 5000, 0);

        /// <summary>Key-in handler. Referenced by name from Commands.xml.</summary>
        public static void DrawLine(string unparsed)
        {
            KeyinCommand.Run(CommandTitle, () =>
            {
                DgnModel activeModel = ActiveSession.RequireModel();

                var line = new LineElement(activeModel, null, new DSegment3d(StartPoint, EndPoint));
                line.AddToModel();

                KeyinCommand.ReportInfo("Line Drawn!", "3D Test line created in model.");
            });
        }
    }
}
