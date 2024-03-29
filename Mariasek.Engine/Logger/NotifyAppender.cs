﻿#if !PORTABLE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using log4net.Appender;
using log4net.Core;
using System.Diagnostics.CodeAnalysis;

namespace Mariasek.Engine.Logger
{
    /// <summary>
    /// The appender we are going to bind to for our logging.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class NotifyAppender : AppenderSkeleton, INotifyPropertyChanged
    {
        #region Members and events
        private string _notification;
        private event PropertyChangedEventHandler _propertyChanged;

        public NotifyAppender()
        { }

        public static NotifyAppender Instance
        {
            get
            {
                foreach (log4net.ILog log in log4net.LogManager.GetCurrentLoggers())
                {
                    foreach (IAppender appender in log.Logger.Repository.GetAppenders())
                    {
                        if (appender is NotifyAppender)
                        {
                            return appender as NotifyAppender;
                        }
                    }
                }
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged
        {
            add { _propertyChanged += value; }
            remove { _propertyChanged -= value; }
        }
        #endregion

        /// <summary>
        /// Get or set the notification message.
        /// </summary>
        public string Notification
        {
            get
            {
                return _notification;
            }
            set
            {
                if (_notification != value)
                {
                    _notification = value;
                    OnChange();
                }
            }
        }

        /// <summary>
        /// Raise the change notification.
        /// </summary>
        private void OnChange()
        {
            PropertyChangedEventHandler handler = _propertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(string.Empty));
            }
        }

        /// <summary>
        /// Append the log information to the notification.
        /// </summary>
        /// <param name="loggingEvent">The log event.</param>
        protected override void Append(LoggingEvent loggingEvent)
        {
            StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
            Layout.Format(writer, loggingEvent);
            Notification += writer.ToString();
        }
    }
}

#endif