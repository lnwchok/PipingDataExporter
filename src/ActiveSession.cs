using Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;

namespace PipingDataExporter
{
    /// <summary>
    /// Access to what the host currently has open, with the "nothing is open" case turned into a
    /// message the user can act on instead of a NullReferenceException stack trace.
    /// </summary>
    internal static class ActiveSession
    {
        public static DgnModel RequireModel()
        {
            DgnModel model = Session.Instance.GetActiveDgnModel();
            if (model == null)
            {
                throw new KeyinCommandException("No active model. Open a DGN and activate the model you want to use, then run the key-in again.");
            }

            return model;
        }

        public static DgnFile RequireFile()
        {
            DgnFile file = Session.Instance.GetActiveDgnFile();
            if (file == null)
            {
                throw new KeyinCommandException("No active DGN file. Open a DGN, then run the key-in again.");
            }

            return file;
        }
    }
}
