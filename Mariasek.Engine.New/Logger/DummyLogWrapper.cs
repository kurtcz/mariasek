using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New.Logger
{
    public class DummyLogWrapper : ILog
    {
        public void Init()
        {
        }
        public void Debug(object message)
        {
        }
        public void Debug(object message, Exception exception)
        {
        }
        public void DebugFormat(string format, params object[] args)
        {
        }
        public void Error(object message)
        {
        }
        public void Error(object message, Exception exception)
        {
        }
        public void ErrorFormat(string format, params object[] args)
        {
        }
        public void Fatal(object message)
        {
        }
        public void Fatal(object message, Exception exception)
        {
        }
        public void FatalFormat(string format, params object[] args)
        {
        }
        public void Info(object message)
        {
        }
        public void Info(object message, Exception exception)
        {
        }
        public void InfoFormat(string format, params object[] args)
        {
        }
        public void Warn(object message)
        {
        }
        public void Warn(object message, Exception exception)
        {
        }
        public void WarnFormat(string format, params object[] args)
        {
        }
        public void Trace(object message)
        {
        }
        public void Trace(object message, Exception exception)
        {
        }
        public void TraceFormat(string format, params object[] args)
        {
        }
        public void Verbose(object message)
        {
        }
        public void Verbose(object message, Exception exception)
        {
        }
        public void VerboseFormat(string format, params object[] args)
        {
        }
    }
}
