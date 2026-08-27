using System;
using Bentley.MstnPlatformNET;

namespace PipingDataExporter
{
    /// <summary>
    /// A failure the user can act on (no active model, unwritable output path, ...).
    /// Only <see cref="Exception.Message"/> is shown for these - no stack trace.
    /// </summary>
    internal sealed class KeyinCommandException : Exception
    {
        public KeyinCommandException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Entry-point plumbing shared by every key-in handler.
    /// </summary>
    internal static class KeyinCommand
    {
        /// <summary>
        /// Runs a key-in body with an outer catch-all.
        /// <para>
        /// A method invoked from a KeyinHandler is the top of the managed call stack - nothing native
        /// below it catches what escapes, so an unhandled exception takes the whole OpenPlant Modeler
        /// process down (logged only as CLR exception 0xe0434352, usually with no message). Every
        /// handler must funnel through here.
        /// </para>
        /// </summary>
        public static void Run(string commandTitle, Action body)
        {
            try
            {
                body();
            }
            catch (KeyinCommandException ex)
            {
                ReportError(commandTitle + " Failed", ex.Message);
            }
            catch (Exception ex)
            {
                ReportError(commandTitle + " Failed", ex.ToString());
            }
        }

        public static void ReportInfo(string title, string message)
        {
            Show(MessageType.Info, title, message, MessageAlert.None);
        }

        public static void ReportError(string title, string message)
        {
            Show(MessageType.Error, title, message, MessageAlert.Balloon);
        }

        private static void Show(MessageType type, string title, string message, MessageAlert alert)
        {
            MessageCenter.Instance.ShowMessage(type, title, message, alert);
        }
    }
}
