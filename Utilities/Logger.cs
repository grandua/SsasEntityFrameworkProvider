using System;
using System.Diagnostics;
using System.Reflection;

namespace AgileDesign.Utilities
{
    /// <remarks>
    /// Logger follows Registry design pattern
    /// </remarks>
#if ! DEBUG
    [DebuggerStepThrough]
#endif
    public class Logger : ILogger
    {
        static Logger()
        {
            AddConsoleListener();
        }

        [Conditional("DEBUG")]
        static void AddConsoleListener()
        {
            Instance.AddTraceListener(new ConsoleTraceListener());
        }

        const string ComponentsDelimiter = "/";
        static readonly object locker = new object();

        static ILogger instance;
        TraceSource loggerImpl;

        public static ILogger Instance
        {
            get { return Init.InitIfNullLocking<Logger, ILogger>(ref instance, locker); }
            set
            {
                lock (locker)
                {
                    instance = value;
                }
            }
        }

        string name = DefaultSourceName;
        public const string DefaultSourceName = "SsasEntityFrameworkProvider";

        public string Name
        {
            get { return name; }
            set
            {
                if (loggerImpl != null)
                {
                    throw new InvalidOperationException
                        ("Cannot change Name after Logger has been initialized");
                }
                name = value;
            }
        }

        ///<summary>
        ///  Uses 'EntryAssemblyName/CallingAssemblyName' as a name of your trace source
        ///</summary>
        ///<value>
        ///  Instance of TraceSource configured with a name of your app
        ///</value>
        TraceSource LoggerImpl
        {
            get 
            {
                return loggerImpl
                       ?? ( loggerImpl = new TraceSource(Name, SourceLevels.Information) );
            }
        }

        SourceSwitch ILogger.Switch
        {
            get { return LoggerImpl.Switch; }
            set { LoggerImpl.Switch = value; }
        }
        public static SourceSwitch Switch
        {
            get { return Instance.Switch; }
            set { Instance.Switch = value; }
        }

        /// <returns>
        ///   EntryAssembly(exe)/CallingAssembly
        /// </returns>
        public static string GetEntryAndCallingAssemblies()
        {
            string result = GetEntryAssemblyName();
            result += ComponentsDelimiter + GetCallingAssemblyName();
            return result;
        }

        public static string GetEntryAssemblyName()
        {
            string result = "";
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                result = entryAssembly.GetName().Name;
            }
            return result;
        }

        public static string GetCallingAssemblyName()
        {
            var methodInfo = ExecutingMethod();
            var assembly = methodInfo.DeclaringType == null
                               ? methodInfo.Module.Assembly
                               : methodInfo.DeclaringType.Assembly;

            return assembly.GetName().Name;
        }

        void ILogger.TraceEvent
            (
                TraceEventType eventType,
                string message,
                params object[] args
            )
        {
            int eventId = GetEventIdFromCallerMethod();
            if (LoggerImpl.Listeners.Count == 0)
            {
                //do not remove a call to LoggerImpl, it must happen on initial thread to init Name properly
                return;
            }
            Action logAction = () =>
            {
                lock (locker)
                {
                    //avoid simultaneous write to the same file / log
                    WriteToConsole(eventType, eventId, message, args);
                    LoggerImpl.TraceEvent(eventType, eventId, message, args);
                }
            };

            logAction.BeginInvoke(null, null);
        }
        public static void TraceEvent
            (
                TraceEventType eventType,
                string message,
                params object[] args
            )
        {
            Instance.TraceEvent(eventType, message, args);
        }

        [Conditional("DEBUG")]
        void WriteToConsole
            (
                TraceEventType eventType,
                int eventId,
                string message,
                object[] args
            )
        {
            Console.Write("eventId='{0}', ", eventId);
            Console.Write("eventType='{0}', ", eventType);
            Console.WriteLine("{0}, ", DateTime.Now.ToString());
            Console.WriteLine(message, args);
            Console.WriteLine();
        }

        void ILogger.TraceInformation
            (
                string message,
                params object[] args
            )
        {
            TraceEvent(TraceEventType.Information, message, args);
        }
        public static void TraceInformation
            (
                string message,
                params object[] args
            )
        {
            Instance.TraceInformation(message, args);
        }

        void ILogger.TraceVerbose
            (
                string message,
                params object[] args
            )
        {
            TraceEvent(TraceEventType.Verbose, message, args);
        }
        public static void TraceVerbose
            (
                string message,
                params object[] args
            )
        {
            Instance.TraceVerbose(message, args);
        }

        void ILogger.Debug
            (
                string message,
                params object[] args
            )
        {
            DebugConditional(message, args);
        }
        public static void Debug
            (
                string message,
                params object[] args
            )
        {
            Instance.Debug(message, args);
        }

        [Conditional("DEBUG")]
        void DebugConditional
            (
                string message,
                object[] args
            )
        {
            TraceEvent(TraceEventType.Verbose, message, args);
        }

        void ILogger.TraceWarning
            (
                string message,
                params object[] args
            )
        {
            TraceEvent(TraceEventType.Warning, message, args);
        }
        public static void TraceWarning
            (
                string message,
                params object[] args
            )
        {
            Instance.TraceWarning(message, args);
        }

        void ILogger.TraceError
            (
                string message,
                params object[] args
            )
        {
            TraceEvent(TraceEventType.Error, message, args);
        }
        public static void TraceError
            (
                string message,
                params object[] args
            )
        {
            Instance.TraceError(message, args);
        }

        void ILogger.AddTraceListener(TraceListener listener)
        {
            LoggerImpl.Listeners.Add(listener);
        }
        public static void AddTraceListener(TraceListener listener)
        {
            Instance.AddTraceListener(listener);
        }

        void ILogger.ClearListeners()
        {
            LoggerImpl.Listeners.Clear();
        }
        public static void ClearListeners()
        {
            Instance.ClearListeners();
        }

        protected virtual short GetEventIdFromCallerMethod()
        {
            return short.Parse(ExecutingPublicMethodName().GetHashCode().ToString().Right(4));
        }

        public static string ExecutingMethodName()
        {
            var executingMethod = ExecutingMethod();
            return (executingMethod == null)
                       ? ""
                       : executingMethod.Name;
        }

        static MethodBase ExecutingMethod()
        {
            var trace = new StackTrace(false);
            for (int i = 0; i < trace.FrameCount; ++i)
            {
                var method = trace.GetFrame(i).GetMethod();
                if (method.DeclaringType != typeof(Logger))
                {
                    return method;
                }
            }
            return null;
        }

        public static string ExecutingPublicMethodName()
        {
            var executingMethod = ExecutingPublicMethod();
            return (executingMethod == null)
                       ? ""
                       : executingMethod.Name;
        }

        static MethodBase ExecutingPublicMethod()
        {
            var trace = new StackTrace(false);
            for (int i = 0; i < trace.FrameCount; ++i)
            {
                MethodBase method = trace.GetFrame(i).GetMethod();
                if (method.DeclaringType != typeof(Logger)
                    && method.IsPublic)
                {
                    return method;
                }
            }
            return null;
        }

    }
}