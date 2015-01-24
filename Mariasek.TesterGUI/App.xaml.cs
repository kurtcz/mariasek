using System;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
//using log4net;
using Mariasek.Engine.New.Logger;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            //Application.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(OnDispatcherUnhandledException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);
        }

        /// <summary>
        /// Gets called after an unhandled exception occurs on any thread
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            UnhandledException((Exception) e.ExceptionObject);
        }

        /// <summary>
        /// Gets called after an unhandled exception occurs on the main thread
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            UnhandledException(e.Exception);
#if DEBUG
            e.Handled = false;
#endif
        }

        private void UnhandledException(Exception e)
        {
            var sb = new StringBuilder();
            var inner = false;

            sb.Append(string.Format("\nUnhandled exception: {0}\n", e.Message));
            for (var ex = e; ex != null; ex = ex.InnerException)
            {
                if (inner)
                {
                    sb.Append(string.Format("Inner exception: {0}\n", ex.Message));
                }
                sb.Append(ex.StackTrace);
                sb.Append("\n");
                inner = true;
            }
            _log.ErrorFormat(sb.ToString());
        }
    }
}
