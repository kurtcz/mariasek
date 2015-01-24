﻿using System;
using System.Configuration;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
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
		LaunchMode = Android.Content.PM.LaunchMode.SingleInstance,
		//ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait,
		ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape,
		//ScreenOrientation = Android.Content.PM.ScreenOrientation.Sensor,
		ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation |
		Android.Content.PM.ConfigChanges.KeyboardHidden |
		Android.Content.PM.ConfigChanges.Keyboard)]
	public class MariasekActivity : AndroidGameActivity
	{
        MariasekMonoGame g;

		protected override void OnCreate (Bundle bundle)
		{
            System.Diagnostics.Debug.WriteLine("OnCreate()");
			base.OnCreate (bundle);

			// Create our OpenGL view, and display it
			MariasekMonoGame.Activity = this;
			g = new MariasekMonoGame ();
			SetContentView (g.Window);
            g.Run();
		}

        protected override void OnRestart()
        {
            System.Diagnostics.Debug.WriteLine("OnRestart()");
            base.OnRestart();
            g.OnRestart();
        }

//        protected override void OnResume()
//        {
//            //SetContentView (g.Window);
//            //SetContentView ((View)g.Services.GetService(typeof(View)));
//            base.OnResume();
//        }
	}
}


