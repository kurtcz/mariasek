using System;
using System.Reflection;
using System.Text;
using System.Xml;
using System.IO;

// 3rd party libs
using log4net.Config;
using log4net.DateFormatter;

namespace Logger
{
	/// <summary>
	/// Summary description for LoggerSetupHelper.
	/// </summary>
    public class LoggerSetup
    {
        static LoggerSetup()
        {
            // Best possible guess...
            LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "log");
        }

        public enum FileMode { CreateNewOrTruncate, Append };

        private static readonly string[] DefaultFileName = new string[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"LoggerSetup.xml")
        };

        /// <summary>
        /// Used during process initialization to decide where logs will be placed.
        /// </summary>
        public static string LogFolder
        {
            get;
            set;
        }

        static void Init(string[] anExternalFileConfigNameList, string resourceName)
        {
            Stream dataSourceStream = null;
            var resourceAssembly = Assembly.GetExecutingAssembly();
            _setupLoggerFile = new XmlDocument();
            if( (anExternalFileConfigNameList != null) && (anExternalFileConfigNameList.Length > 0) )
            {
                foreach(var fileName in anExternalFileConfigNameList )
                {
                    if (!File.Exists(fileName)) continue;
                    dataSourceStream = new FileStream(fileName, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read);
                    break;
                }
            }
			if(dataSourceStream==null)
			{
				if(File.Exists( DefaultFileName[0] ))
				{
					dataSourceStream = new FileStream(DefaultFileName[0], System.IO.FileMode.Open, FileAccess.Read, FileShare.Read);
				}
			}
            if(dataSourceStream==null && resourceName != null)
            {
                dataSourceStream = resourceAssembly.GetManifestResourceStream(resourceName);
                if (dataSourceStream == null)
                    throw new InvalidOperationException(String.Format("Unable to load embedded resource ({0})", resourceName));
            }

            if (dataSourceStream == null)
                throw new InvalidOperationException("No valid logging configuration has been found!");
            _setupLoggerFile.Load(dataSourceStream);
        }

//        static LoggerSetup()
//        {
//            Init(DefaultFileName, null);
//        }

        private static void MakeSetup(XmlDocument aConfigData) 
        {
            var configElement = aConfigData.DocumentElement;
            XmlConfigurator.Configure(configElement);
        }

        public static void SimpleSetup() 
        {
            SimpleSetup(DefaultFileName[0]);
        }
        public static void SimpleSetup(string aLogConfigFile) 
        {
            Init(new[] { aLogConfigFile }, null);
			AddRootAppender("ConsoleAppender");
			MakeSetup(_setupLoggerFile);
        }

        public static void TraceFileResourceSetup(string aLogFileName, string resourceName)
        {
            TraceFileSetup(aLogFileName, FileMode.CreateNewOrTruncate, new string[] { }, resourceName, true);
        }
	    public static void TraceFileSetup(string aFileName) 
        {
            TraceFileSetup( aFileName, FileMode.CreateNewOrTruncate);
        }
        public static void TraceFileSetup(string aLogFileName, string aLogConfigFile) 
        {
            TraceFileSetup( aLogFileName, FileMode.CreateNewOrTruncate, new[] { aLogConfigFile });
        }
        public static void TraceFileSetup(string aLogFileName, string[] aLoggerConfigFileList) 
        {
            TraceFileSetup( aLogFileName, FileMode.CreateNewOrTruncate, aLoggerConfigFileList);
        }
        public static void TraceFileSetupWithoutConsole(string aLogFileName, string[] aLoggerConfigFileList) 
        {
            TraceFileSetup( aLogFileName, FileMode.CreateNewOrTruncate, aLoggerConfigFileList, null, false);
        }

        public static void TraceFileAppendSetup(string aLogFileName) 
        {
            TraceFileSetup( aLogFileName, FileMode.Append);
        }
        public static void TraceFileAppendSetup(string aLogFileName, string aLogConfigFile) 
        {
            TraceFileSetup( aLogFileName, FileMode.Append, new string[] { aLogConfigFile });
        }
        public static void TraceFileAppendSetup(string aLogFileName, string[] aLoggerConfigFileList) 
        {
            TraceFileSetup( aLogFileName, FileMode.Append, aLoggerConfigFileList);
        }
        public static void TraceFileAppendSetupWithoutConsole(
            string aLogFileName, 
            string[] aLoggerConfigFileList) 
        {
            TraceFileSetup( aLogFileName, FileMode.Append, aLoggerConfigFileList, null, false);
        }

        public static void TraceFileSetup(string aLogFileName, FileMode aFileMode)
        {
            TraceFileSetup(aLogFileName, aFileMode, new string[] { aLogFileName+".local.xml" });
        }
        public static void TraceFileSetup(
            string aLogFileName,
            FileMode aFileMode,
            string[] aLoggerConfigFileList)
        {
            TraceFileSetup(aLogFileName, aFileMode, aLoggerConfigFileList, null, true);
        }
        
        public static void TraceFileSetup(string aLogFileName, FileMode aFileMode, string[] aLoggerConfigFileList, string resourceName, bool aUseConsoleAppender)
        {
            Init(aLoggerConfigFileList, resourceName);

            var formater = new AbsoluteTimeDateFormatter();
            var formatedHeaderString = new StringBuilder("[Header - Application started at ");
            var formatedHeaderStringWriter = new StringWriter(formatedHeaderString);
			var now = DateTime.UtcNow;

            formatedHeaderString.Append(now.ToString("dd-MMM-yyyy HH:mm:ss,fff") + " local/");
			formater.FormatDate(now.ToUniversalTime(), formatedHeaderStringWriter);
            formatedHeaderString.Append(" UTC]\r\n");

            ChangeAttrValue(new[] {"descendant::param[@value='RollingLogFile.txt']"}, "value", aLogFileName.Replace("\\","\\\\"));
            ChangeAttrValue(new[] {"descendant::appender[@name='RollingLogFileAppender']", "descendant::param[@name='Header']"}, "value", formatedHeaderString.ToString());

            if( aFileMode == FileMode.Append ) 
            {
                ChangeAttrValue(new[] {"descendant::appender[@name='RollingLogFileAppender']", "descendant::param[@name='AppendToFile']"}, "value", true.ToString());
            }

            AddRootAppender("RollingLogFileAppender");
            if(aUseConsoleAppender)
            {
                AddRootAppender("ConsoleAppender");
                AddRootAppender("TraceAppender");
                AddRootAppender("NotifyAppender");
            }

			MakeSetup(_setupLoggerFile);
        }

        private static void AddRootAppender(string anAppenderName) 
        {
            // find appender root element
            var rootDocElement = _setupLoggerFile.DocumentElement;

            if (rootDocElement==null) throw new ApplicationException("Internal logger error");

			// check if the appender is defined in the config file
			var xAppenderDefinition = rootDocElement.SelectSingleNode("descendant::appender[@name='" + anAppenderName + "']");
			if(xAppenderDefinition == null) return;

            var rootAppenderElement= rootDocElement.SelectSingleNode("descendant::root");

            // create new appender
            XmlNode newAppenderNode =  _setupLoggerFile.CreateDocumentFragment();
            newAppenderNode.InnerXml = "<appender-ref ref=\""+anAppenderName+"\" />";
			
            // extend config file
            rootAppenderElement.AppendChild(newAppenderNode);
        }

        private static void ChangeAttrValue(XmlNode anAttrNode,
            string anAttrName, 
            string aNewAttrValue) 
        {
            if (anAttrNode == null) return;
            // replace attr value with new one
            var attrCollection = anAttrNode.Attributes;
            var attrValue = attrCollection.GetNamedItem(anAttrName);
            attrValue.Value = aNewAttrValue;
        }

        private static void ChangeAttrValue(string[] anAttrLookupPathPattern, 
            string anAttrName,
            string aNewAttrValue) 
        {
            //start with doc. root
            XmlNode root = null;
            XmlNode sectionNode = _setupLoggerFile.DocumentElement;

			// traverse to node
			foreach (var pathPattern in anAttrLookupPathPattern)
			{
			    if (sectionNode == null) continue;
			    root = sectionNode;
			    sectionNode = root.SelectSingleNode(pathPattern);
			}

			// change attribute
			ChangeAttrValue(sectionNode, anAttrName, aNewAttrValue);
        }

        private static XmlDocument _setupLoggerFile = null;
    }
}
