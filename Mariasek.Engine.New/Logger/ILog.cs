using System;
namespace Mariasek.Engine.New.Logger
{
    public interface ILog
    {
        void Init();

        #region log4net.ILog members
        void Debug(object message);
        void Debug(object message, Exception exception);
        void DebugFormat(string format, params object[] args);
        void Error(object message);
        void Error(object message, Exception exception);
        void ErrorFormat(string format, params object[] args);
        void Fatal(object message);
        void Fatal(object message, Exception exception);
        void FatalFormat(string format, params object[] args);
        void Info(object message);
        void Info(object message, Exception exception);
        void InfoFormat(string format, params object[] args);
        void Warn(object message);
        void Warn(object message, Exception exception);
        void WarnFormat(string format, params object[] args);
        #endregion

        #region log4net.ILog extensions
        void Trace(object message);
        void Trace(object message, Exception exception);
        void TraceFormat(string format, params object[] args);
        void Verbose(object message);
        void Verbose(object message, Exception exception);
        void VerboseFormat(string format, params object[] args);
        #endregion
    }
}
