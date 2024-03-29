﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace Mariasek.Engine.Logger
{
#if !PORTABLE
    [ExcludeFromCodeCoverage]
#endif
    public class DummyLogWrapper : ILog
    {
        public void Init()
        {
        }
        public void Debug(object message)
        {
#if DEBUG            
            System.Diagnostics.Debug.WriteLine(message);
#endif
        }
        public void Debug(object message, Exception exception)
        {
#if DEBUG            
            System.Diagnostics.Debug.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(exception.Message);
            System.Diagnostics.Debug.WriteLine(exception.StackTrace);
#endif
        }
        public void DebugFormat(string format, params object[] args)
        {
#if DEBUG            
            Debug(string.Format(format, args));
#endif
        }
        public void Error(object message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
        public void Error(object message, Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(exception.Message);
            System.Diagnostics.Debug.WriteLine(exception.StackTrace);
        }
        public void ErrorFormat(string format, params object[] args)
        {
            Error(string.Format(format, args));
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
#if DEBUG   
            System.Diagnostics.Debug.WriteLine(message);
#endif
        }
        public void Info(object message, Exception exception)
        {
#if DEBUG            
            System.Diagnostics.Debug.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(exception.Message);
            System.Diagnostics.Debug.WriteLine(exception.StackTrace);
#endif
        }
        public void InfoFormat(string format, params object[] args)
        {
#if DEBUG            
            Info(string.Format(format, args));
#endif
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
