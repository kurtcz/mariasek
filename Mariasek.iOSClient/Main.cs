#region Using Statements
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mariasek.SharedClient;

#if MONOMAC
using MonoMac.AppKit;
using MonoMac.Foundation;

#elif __IOS__ || __TVOS__
using Foundation;
using UIKit;
#endif
#endregion

namespace Mariasek.iOSClient
{
    #if __IOS__ || __TVOS__
    [Register("AppDelegate")]
	class Program : UIApplicationDelegate
    
#else
    static class Program
    #endif
    {
        private static MariasekMonoGame game;
		private static Program instance = new Program();

        internal static void RunGame()
        {
			var emailSender = new EmailSender ();
			game = new MariasekMonoGame(emailSender);

			var gameController = game.Services.GetService(typeof(UIViewController)) as UIViewController;
			emailSender.GameController = gameController;

			game.Run();
            #if !__IOS__  && !__TVOS__
            game.Dispose();
            #endif
        }
			
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        #if !MONOMAC && !__IOS__  && !__TVOS__
        [STAThread]
        #endif
        static void Main(string[] args)
        {
            #if MONOMAC
            NSApplication.Init ();

            using (var p = new NSAutoreleasePool ()) {
                NSApplication.SharedApplication.Delegate = new AppDelegate();
                NSApplication.Main(args);
            }
            #elif __IOS__ || __TVOS__
            UIApplication.Main(args, null, "AppDelegate");
            #else
            RunGame();
            #endif
        }

        #if __IOS__ || __TVOS__
        public override void FinishedLaunching(UIApplication app)
        {
			app.IdleTimerDisabled = true; //stop iOS from stalling our game
            RunGame();
        }

		public override void WillEnterForeground(UIApplication app)
		{
			app.IdleTimerDisabled = true; //stop iOS from stalling our game
		}

		public override void DidEnterBackground(UIApplication app)
		{
			app.IdleTimerDisabled = false; //conserve battery when sleeping
		}
        #endif

        public override void WillTerminate(UIApplication application)
        {
            game.Dispose();
        }
    }

    #if MONOMAC
    class AppDelegate : NSApplicationDelegate
    {
        public override void FinishedLaunching (MonoMac.Foundation.NSObject notification)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (object sender, ResolveEventArgs a) =>  {
                if (a.Name.StartsWith("MonoMac")) {
                    return typeof(MonoMac.AppKit.AppKitFramework).Assembly;
                }
                return null;
            };
            Program.RunGame();
        }

        public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender)
        {
            return true;
        }
    }  
    #endif
}

