using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using Microsoft.Xna.Framework;
using Mariasek.SharedClient;

namespace Mariasek.AndroidClient
{
	[Activity (Name = "com.tnemec.mariasek.android.MariasekActivity",
               Label = "Mariášek", 
               MainLauncher = true,
               Icon = "@drawable/icon",
               Theme = "@style/Theme.Splash",
               AlwaysRetainTaskState = true,
               LaunchMode = LaunchMode.SingleInstance,
               ScreenOrientation = ScreenOrientation.ReverseLandscape,
               ConfigurationChanges = ConfigChanges.Orientation |
                                      ConfigChanges.ScreenSize |
        		                      ConfigChanges.KeyboardHidden |
        		                      ConfigChanges.Keyboard)]
	public class MariasekActivity : AndroidGameActivity, IEmailSender, IWebNavigate
	{
        MariasekMonoGame g;

		protected override void OnCreate (Bundle bundle)
		{
            System.Diagnostics.Debug.WriteLine("OnCreate()");
			base.OnCreate (bundle);

            //handle unhandled exceptions from the UI thread
            AndroidEnvironment.UnhandledExceptionRaiser += OnUnhandledExceptionRaiser;
            //handle unhandled exceptions from background threads
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Create our OpenGL view, and display it
			g = new MariasekMonoGame (this, this);
            SetContentView (g.Services.GetService<View>());
            g.Run();
		}

        public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
        {
            System.Diagnostics.Debug.WriteLine("OnConfigurationChanged()");
            base.OnConfigurationChanged(newConfig);

            //prevent the current game to be restarted due to changes defined in ActivityAttribute.ConfigurationChanges
            SetContentView(g.Services.GetService<View>());
        }

        protected override void OnStart()
        {
            System.Diagnostics.Debug.WriteLine("OnStart()");
            base.OnStart();
        }

        protected override void OnPause()
        {
            System.Diagnostics.Debug.WriteLine("OnPause()");
            base.OnPause();
            g.OnPaused();
        }

		protected override void OnResume()
		{
			System.Diagnostics.Debug.WriteLine("OnResume()");
            base.OnResume();
            g.OnResume();
		}

        protected override void OnStop()
        {
            System.Diagnostics.Debug.WriteLine("OnStop()");
            base.OnStop();
            g.OnStop();
        }

        protected override void OnDestroy()
        {
            System.Diagnostics.Debug.WriteLine("OnDestroy()");
            base.OnDestroy();
            g.Dispose();
        }

        protected override void OnRestart()
        {
            System.Diagnostics.Debug.WriteLine("OnRestart()");
            base.OnRestart();
            g.OnRestart();
        }

		protected override void OnSaveInstanceState (Bundle outState)
        {
            System.Diagnostics.Debug.WriteLine("OnSaveInstanceState()");
            base.OnSaveInstanceState (outState);    
            g.OnSaveInstanceState();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {            
            System.Diagnostics.Debug.WriteLine("OnUnhandledException()");
            var ex = args.ExceptionObject as Exception;
			var ae = ex as AggregateException;
			if (ae != null)
			{
				ex = ae.Flatten().InnerExceptions[0];
			}
            var msg = string.Format("{0}\n{1}\n{2}\n-\n{3}", ex.Message, ex.StackTrace, 
                                    g?.MainScene?.g?.DebugString?.ToString() ?? string.Empty,
                                    g?.MainScene?.g?.BiddingDebugInfo?.ToString() ?? string.Empty);

			SendEmail(new[] { "mariasek.app@gmail.com" }, "Mariasek crash report", msg, new string[0]);
		}

		private void OnUnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("OnUnhandledExceptionRaiser()");
			var ex = e.Exception;
			var ae = ex as AggregateException;
			if (ae != null)
			{
				ex = ae.Flatten().InnerExceptions[0];
			}
            var msg = string.Format("{0}\n{1}\n{2}\n-\n{3}", ex.Message, ex.StackTrace, 
                                    g?.MainScene?.g?.DebugString?.ToString() ?? string.Empty,
                                    g?.MainScene?.g?.BiddingDebugInfo?.ToString() ?? string.Empty);

            SendEmail(new[] { "mariasek.app@gmail.com" }, "Mariasek crash report", msg, new string[0]);
		}

		public void SendEmail(string[] recipients, string subject, string body, string[] attachments)
        {
            var email = new Intent(Android.Content.Intent.ActionSendMultiple);
            var uris = new List<Android.Net.Uri>();

            email.SetType("text/plain");
            email.PutExtra(Android.Content.Intent.ExtraEmail, recipients);
            email.PutExtra(Android.Content.Intent.ExtraSubject, subject);
            email.PutExtra(Android.Content.Intent.ExtraText, body);
            foreach (var attachment in attachments)
            {
                //copy attachment to external storage where an email application can have access to it
                var externalPath = Path.Combine(global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
                var path = Path.Combine(externalPath, Path.GetFileName(attachment));
                var file = new Java.IO.File(path);
                var uri = Android.Net.Uri.FromFile(file);

                if(!File.Exists(attachment))
                {
                    continue;
                }
                if (path != attachment)
                {
                    MainScene.CreateDirectoryForFilePath(path);
                    File.Copy(attachment, path, true);
                }
                file.SetReadable(true, false);
                uris.Add(uri);
            }
            email.PutParcelableArrayListExtra(Intent.ExtraStream, uris.ToArray());
            try
            {
                StartActivity(email);
            }
            catch
            {
                StartActivity(Intent.CreateChooser(email, "Jakou aplikací odeslat email?"));
            }
        }

        public void Navigate(string url)
        {
            var browser = new Intent(Intent.ActionView);

            browser.SetData(Android.Net.Uri.Parse(url));
			StartActivity(browser);
        }
	}
}


