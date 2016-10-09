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
	[Activity (Label = "Mariášek", 
		MainLauncher = true,
		Icon = "@drawable/icon",
		Theme = "@style/Theme.Splash",
		AlwaysRetainTaskState = true,
		LaunchMode = LaunchMode.SingleInstance,
		ScreenOrientation = ScreenOrientation.Landscape,
		ConfigurationChanges = ConfigChanges.Orientation |
		ConfigChanges.KeyboardHidden |
		ConfigChanges.Keyboard)]
	public class MariasekActivity : AndroidGameActivity, IEmailSender
	{
        MariasekMonoGame g;

		protected override void OnCreate (Bundle bundle)
		{
            System.Diagnostics.Debug.WriteLine("OnCreate()");
			base.OnCreate (bundle);

            //Load: _counter = outState.GetInt ("click_count", 0);

            // Create our OpenGL view, and display it
			g = new MariasekMonoGame (this);
            SetContentView (g.Services.GetService<View>());
            g.Run();
		}

        protected override void OnRestart()
        {
            System.Diagnostics.Debug.WriteLine("OnRestart()");
            base.OnRestart();
            g.OnRestart();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            //Save: outState.PutInt ("click_count", _counter);
            g.OnSaveInstanceState();
            base.OnSaveInstanceState (outState);    
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
                var externalPath = global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
                var path = Path.Combine(externalPath, Path.GetFileName(attachment));
                var file = new Java.IO.File(path);
                var uri = Android.Net.Uri.FromFile(file);

                if(!File.Exists(attachment))
                {
                    continue;
                }
                File.Copy(attachment, path, true);
                file.SetReadable(true, false);
                uris.Add(uri);
            }
            email.PutParcelableArrayListExtra(Intent.ExtraStream, uris.ToArray());
            StartActivity(email);
        }
	}
}


