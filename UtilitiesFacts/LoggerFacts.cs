using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using AgileDesign.Utilities;
using Xunit;

namespace UtilitiesFacts
{
    public class LoggerFacts
    {
        [Fact, MethodImpl(MethodImplOptions.NoInlining)]
        public void ExecutingMethodName()
        {
            PrivateExecutingMethod();
        }

        private void PrivateExecutingMethod()
        {
            Assert.Equal(
                NameOf.Method(() => PrivateExecutingMethod())
                , Logger.ExecutingMethodName());

            Assert.Equal(
                NameOf.Method(() => ExecutingMethodName())
                , Logger.ExecutingPublicMethodName());
        }

        /// <remarks>
        /// Intermittent test
        /// </remarks>
        [Fact]
        public void TraceEvent()
        {
            Logger.Instance = new Logger();
            string expectedLoggerName = Logger.DefaultSourceName;

            Assert.Equal(expectedLoggerName, Logger.Instance.Name);

            var stringWriter = new StringWriter();
            //Default trace listener writes to Output Window in debug mode
            Logger.Instance.AddTraceListener(new TextWriterTraceListener(stringWriter));
            Logger.Instance.AddTraceListener(new ConsoleTraceListener());

            Logger.Instance.TraceEvent(TraceEventType.Error, "test 2");
            WaitTillAsyncCallCompletes(stringWriter);
            Assert.True(stringWriter.ToString().EndsWith("test 2\r\n"));
            //This logger works with default Switch too, but Level is Information by default
            Logger.Instance.Switch = new SourceSwitch("TestSwitch")
                                {
                                    Level = SourceLevels.Verbose
                                };

            Logger.Instance.TraceEvent(TraceEventType.Verbose, "test {0}", "message");

            WaitTillAsyncCallCompletes(stringWriter);

            Assert.Contains(
                string.Format
                (
                    "{0} Verbose: {1} : test message"
                    , expectedLoggerName
                    , short.Parse(NameOf.Method(() => TraceEvent())
                        .GetHashCode().ToString().Right(4))
                )
                , stringWriter.ToString());
        }

        private void WaitTillAsyncCallCompletes(StringWriter stringWriter)
        {
            Contract.Requires(stringWriter != null);
            do
            {
                Thread.Sleep(15);

            } while (string.IsNullOrEmpty(stringWriter.ToString()));
        }

    }
}
