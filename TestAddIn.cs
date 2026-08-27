using System;
using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.Elements;
using Bentley.GeometryNET;
using Bentley.MstnPlatformNET;

namespace PipingDataExporter
{
    // MdlTaskID MUST match your DLL filename (PipingDataExporter)
    [AddIn(MdlTaskID = "PipingDataExporter")]
    public sealed class TestAddIn : AddIn
    {
        public TestAddIn(IntPtr mdlDescriptor) : base(mdlDescriptor)
        {
        }

        protected override int Run(string[] commandArgs)
        {
            MessageCenter.Instance.ShowMessage(
                MessageType.Info,
                "AddIn Loaded!",
                "Custom C# AddIn successfully initialized.",
                MessageAlert.None
            );
            return 0;
        }
    }

    public class OpenPlantTestCommands
    {
        public static void DrawLine(string unparsed)
        {
            DgnModel activeModel = Session.Instance.GetActiveDgnModel();

            DPoint3d startPt = new DPoint3d(0, 0, 0);
            DPoint3d endPt = new DPoint3d(5000, 5000, 0);

            DSegment3d segment = new DSegment3d(startPt, endPt);
            LineElement line = new LineElement(activeModel, null, segment);

            line.AddToModel();

            MessageCenter.Instance.ShowMessage(
                MessageType.Info,
                "Line Drawn!",
                "3D Test line created in model.",
                MessageAlert.None
            );
        }
    }
}