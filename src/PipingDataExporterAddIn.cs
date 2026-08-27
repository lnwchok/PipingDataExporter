using System;
using Bentley.MstnPlatformNET;

namespace PipingDataExporter
{
    /// <summary>
    /// AddIn entry point. MicroStation instantiates this on load.
    /// <para>
    /// <c>MdlTaskID</c> MUST equal the built DLL's filename (see &lt;AssemblyName&gt; in the .csproj).
    /// If they diverge the AddIn silently never loads.
    /// </para>
    /// </summary>
    [AddIn(MdlTaskID = "PipingDataExporter")]
    public sealed class PipingDataExporterAddIn : AddIn
    {
        public PipingDataExporterAddIn(IntPtr mdlDescriptor) : base(mdlDescriptor)
        {
        }

        protected override int Run(string[] commandArgs)
        {
            KeyinCommand.Run("AddIn Load", () =>
                KeyinCommand.ReportInfo("AddIn Loaded!", "Custom C# AddIn successfully initialized."));

            return 0;
        }
    }
}
