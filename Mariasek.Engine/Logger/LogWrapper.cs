#if !PORTABLE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace Mariasek.Engine.Logger
{
    [ExcludeFromCodeCoverage]
    public class LogWrapper : ILog
    {
        private readonly Type _type;
        private readonly log4net.ILog _log;

        public LogWrapper(Type type)
        {
            _type = type;
            _log = log4net.LogManager.GetLogger(type);
        }

        public void Init()
        {
            log4net.GlobalContext.Properties["ProgramDataPath"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var configFile = new FileInfo(Process.GetCurrentProcess().MainModule.FileName + ".config");

            log4net.Config.XmlConfigurator.ConfigureAndWatch(configFile);
        }

        public void Debug(object message, Exception exception)
        {
            if (_log != null) _log.Debug(message, exception);
        }

        public void Debug(object message)
        {
            _log.Debug(message);
        }

        public void DebugFormat(string format, params object[] args)
        {
            _log.DebugFormat(format, args);
        }

        public void Error(object message, Exception exception)
        {
            _log.Error(message, exception);
        }

        public void Error(object message)
        {
            _log.Error(message);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            _log.ErrorFormat(format, args);
        }

        public void Fatal(object message, Exception exception)
        {
            _log.Fatal(message, exception);
        }

        public void Fatal(object message)
        {
            _log.Fatal(message);
        }

        public void FatalFormat(string format, params object[] args)
        {
            _log.FatalFormat(format, args);
        }

        public void Info(object message, Exception exception)
        {
            _log.Info(message, exception);
        }

        public void Info(object message)
        {
            _log.Info(message);
        }

        public void InfoFormat(string format, params object[] args)
        {
            _log.InfoFormat(format, args);
        }

        public void Warn(object message, Exception exception)
        {
            _log.Warn(message, exception);
        }

        public void Warn(object message)
        {
            _log.Warn(message);
        }

        public void WarnFormat(string format, params object[] args)
        {
            _log.WarnFormat(format, args);
        }

        public void Trace(object message, Exception exception)
        {
            _log.Logger.Log(_type, log4net.Core.Level.Trace, message, exception);
        }

        public void Trace(object message)
        {
            Trace(message, null);
        }

        public void TraceFormat(string format, params object[] args)
        {
            var message = string.Format(format, args);

            Trace(message);
        }

        public void Verbose(object message, Exception exception)
        {
            _log.Logger.Log(_type, log4net.Core.Level.Verbose, message, exception);
        }

        public void Verbose(object message)
        {
            Verbose(message, null);
        }

        public void VerboseFormat(string format, params object[] args)
        {
            var message = string.Format(format, args);

            Verbose(message);
        }
    }
}

#endif