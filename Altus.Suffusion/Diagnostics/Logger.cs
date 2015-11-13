using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Diagnostics
{
    public static partial class Logger
    {
        static EventLog _eventLog;

        public const string LOG_SOURCE = "Agent Service";
        public const string LOG_NAME = "CSGO";

        static Logger()
        {
            TraceSwitch ts = new TraceSwitch("TraceLevelSwitch", "Determines the tracing level to log/display");
            TraceLevel = ts.Level;

            if (!EventLog.Exists("CSGO"))
            {
                EventLog.CreateEventSource(LOG_SOURCE, LOG_NAME);
            }

            try
            {
                _eventLog = new EventLog();
                _eventLog.Source = LOG_SOURCE;
                _eventLog.Log = LOG_NAME;
            }
            catch { }
            try
            {
                _eventLog.MaximumKilobytes = 200 * 1024;
                _eventLog.ModifyOverflowPolicy(OverflowAction.OverwriteAsNeeded, 0);
            }
            catch
            {
            }
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new EventLogTraceListener(_eventLog)); // writes to EventLog
            Trace.Listeners.Add(new ConsoleTraceListener(true)); // writes to Console window
            Trace.Listeners.Add(new DefaultTraceListener()); // writes to Output window
        }

        public static TraceLevel TraceLevel { get; set; }

        public static void Log(string message)
        {
            if (TraceLevel < TraceLevel.Verbose) return;
            lock (_eventLog)
            {
                Trace.WriteLine(message);
            }
        }

        public static void Log(Exception exception)
        {
            Log(exception, "An error treated as Verbose Information occurred.");
        }

        public static void Log(Exception exception, string headerMessage)
        {
            if (TraceLevel < TraceLevel.Verbose) return;
            Exception inner = exception;
            lock (_eventLog)
            {
                while (inner != null)
                {
                    string message = String.Format("{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                        headerMessage,
                        exception.Source,
                        exception.Message,
                        exception.StackTrace);
                    Log(message);
                    inner = inner.InnerException;
                }
            }
        }

        public static void LogInfo(string message, params object[] args)
        {
            if (TraceLevel < TraceLevel.Info) return;
            lock (_eventLog)
            {
                Trace.TraceInformation(string.Format(message, args));
            }
        }

        public static void LogInfo(Exception exception)
        {
            Log(exception, "An error treated as Information occurred.");
        }

        public static void LogInfo(Exception exception, string headerMessage)
        {
            if (TraceLevel < TraceLevel.Info) return;
            Exception inner = exception;
            lock (_eventLog)
            {
                while (inner != null)
                {
                    Trace.TraceInformation("{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                        headerMessage,
                        exception.Source,
                        exception.Message,
                        exception.StackTrace);
                    inner = inner.InnerException;
                }
            }
        }


        public static void LogWarn(string message, params object[] args)
        {
            if (TraceLevel < TraceLevel.Warning) return;
            lock (_eventLog)
            {
                Trace.TraceWarning(string.Format(message, args));
            }
        }

        public static void LogWarn(Exception exception)
        {
            LogWarn(exception, "An error treated as Warning occurred.");
        }

        public static void LogWarn(Exception exception, string headerMessage)
        {
            if (TraceLevel < TraceLevel.Warning) return;
            Exception inner = exception;
            lock (_eventLog)
            {
                while (inner != null)
                {
                    Trace.TraceWarning("{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                        headerMessage,
                        exception.Source,
                        exception.Message,
                        exception.StackTrace);
                    inner = inner.InnerException;
                }
            }
        }



        public static void LogError(string message, params object[] args)
        {
            if (TraceLevel < TraceLevel.Error) return;
            lock (_eventLog)
            {
                Trace.TraceError(string.Format(message, args));
            }
        }

        public static void LogError(Exception exception)
        {
            LogError(exception, "An Error occurred.");
        }

        public static void LogError(Exception exception, string headerMessage)
        {
            if (TraceLevel < TraceLevel.Error) return;
            Exception inner = exception;
            lock (_eventLog)
            {
                while (inner != null)
                {
                    Trace.TraceError("{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                        headerMessage,
                        exception.Source,
                        exception.Message,
                        exception.StackTrace);
                    inner = inner.InnerException;
                }
            }
        }
    }
}
