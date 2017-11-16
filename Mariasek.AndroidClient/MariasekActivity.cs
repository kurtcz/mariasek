﻿using System;
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
               ScreenOrientation = ScreenOrientation.SensorLandscape,
               ConfigurationChanges = ConfigChanges.Orientation |
                                      ConfigChanges.ScreenSize |
        		                      ConfigChanges.KeyboardHidden |
        		                      ConfigChanges.Keyboard)]
    public class MariasekActivity : AndroidGameActivity, IEmailSender, IWebNavigate, IScreenManager
	{
        MariasekMonoGame g;

		protected override void OnCreate (Bundle bundle)
		{
            System.Diagnostics.Debug.WriteLine("OnCreate()");

            //handle unhandled exceptions from the UI thread
            AndroidEnvironment.UnhandledExceptionRaiser += OnUnhandledExceptionRaiser;
            //handle unhandled exceptions from background threads
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            try
            {
                base.OnCreate(bundle);


                // Create our OpenGL view, and display it
                g = new MariasekMonoGame(this, this, this);

                SetContentView(g.Services.GetService<View>());

                g.Run();
            }
            catch(Exception ex)
            {
                HandleException(ex);
            }
		}

        public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnConfigurationChanged()");
                base.OnConfigurationChanged(newConfig);

                //prevent the current game to be restarted due to changes defined in ActivityAttribute.ConfigurationChanges
                var view = g.Services.GetService<View>();
                SetContentView(view);
                view.KeepScreenOn = true;
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        protected override void OnStart()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnStart()");
                base.OnStart();
                g.OnStart();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        protected override void OnPause()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnPause()");
                base.OnPause();
                g.OnPaused();
            }
            catch(Exception ex)
            {
                HandleException(ex);
            }
        }

		protected override void OnResume()
		{
            try
            {
                System.Diagnostics.Debug.WriteLine("OnResume()");
                base.OnResume();
                g.OnResume();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
		}

        protected override void OnStop()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnStop()");
                base.OnStop();
                g.OnStop();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnDestroy()");
                base.OnDestroy();
                g.Dispose();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        protected override void OnRestart()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnRestart()");
                base.OnRestart();
                g.OnRestart();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

		protected override void OnSaveInstanceState (Bundle outState)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnSaveInstanceState()");
                base.OnSaveInstanceState(outState);
                g.OnSaveInstanceState();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {            
            System.Diagnostics.Debug.WriteLine("OnUnhandledException()");
            HandleException(args.ExceptionObject as Exception);
		}

		private void OnUnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("OnUnhandledExceptionRaiser()");
            HandleException(e.Exception);
		}

        private void HandleException(Exception ex)
        {
            var ae = ex as AggregateException;
            if (ae != null)
            {
                ex = ae.Flatten().InnerExceptions[0];
            }
            var msg = string.Format("{0}\n{1}\n{2}\n-\n{3}", ex?.Message, ex?.StackTrace,
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

        public void SetKeepScreenOnFlag(bool flag)
        {
            var view = g.Services.GetService<View>();

            view.KeepScreenOn = flag;
        }
    }
}


