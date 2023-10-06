using System.Diagnostics;

namespace AgileDesign.Utilities
{
    public interface ILogger
    {
        /// <summary>
        ///   Uses AppDomain.CurrentDomain.FriendlyName by default
        /// </summary>
        string Name { get; set; }

        SourceSwitch Switch { get; set; }

        void TraceEvent
            (
            TraceEventType eventType,
            string message,
            params object[] args
            );

        void TraceInformation
            (
            string message,
            params object[] args
            );

        void TraceVerbose
            (
            string message,
            params object[] args
            );

        void Debug
            (
            string message,
            params object[] args
            );

        void TraceWarning
            (
            string message,
            params object[] args
            );

        void TraceError
            (
            string message,
            params object[] args
            );

        void AddTraceListener(TraceListener listener);
        void ClearListeners();
    }
}