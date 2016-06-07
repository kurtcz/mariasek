using System;
using System.Configuration;
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
	[Activity (Label = "Mariasek", 
		MainLauncher = true,
		Icon = "@drawable/icon",
		Theme = "@style/Theme.Splash",
		AlwaysRetainTaskState = true,
		LaunchMode = LaunchMode.SingleInstance,
		ScreenOrientation = ScreenOrientation.Landscape,
		ConfigurationChanges = ConfigChanges.Orientation |
		ConfigChanges.KeyboardHidden |
		ConfigChanges.Keyboard)]
	public class MariasekActivity : AndroidGameActivity
	{
        MariasekMonoGame g;

		protected override void OnCreate (Bundle bundle)
		{
            System.Diagnostics.Debug.WriteLine("OnCreate()");
			base.OnCreate (bundle);

			// Create our OpenGL view, and display it
			g = new MariasekMonoGame ();
            SetContentView (g.Services.GetService<View>());
            g.Run();
		}

        protected override void OnRestart()
        {
            System.Diagnostics.Debug.WriteLine("OnRestart()");
            base.OnRestart();
            g.OnRestart();
        }

        protected override void OnResume()
        {
            System.Diagnostics.Debug.WriteLine("OnResume()");
            base.OnResume();
            g.OnRestart();
        }
    }
}


