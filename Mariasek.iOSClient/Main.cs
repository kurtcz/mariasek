#region Using Statements
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mariasek.SharedClient;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

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
        private static EmailSender emailSender = null;
        private static ScreenManager screenManager = null;
        internal static void RunGame()
        {
			var storageAccessor = new StorageAccessor();
            var navigator = new WebNavigate();

            screenManager = new ScreenManager();
            emailSender = new EmailSender();

            game = new MariasekMonoGame(emailSender, navigator, screenManager, storageAccessor);

			var gameController = game.Services.GetService(typeof(UIViewController)) as UIViewController;
			emailSender.GameController = gameController;

			game.Run();
            AmendScreenManagerPadding();    //SafeAreInsets on iPhone X is now populated
#if !__IOS__ && !__TVOS__
            game.Dispose();
#endif
        }

        public override void DidChangeStatusBarOrientation(UIApplication application, UIInterfaceOrientation oldStatusBarOrientation)
        {
            AmendScreenManagerPadding();
        }

        private static void AmendScreenManagerPadding()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            {
                var insets = UIApplication.SharedApplication.KeyWindow.SafeAreaInsets;
                var scale = UIScreen.MainScreen.Scale;

                insets.Bottom = 0;  //ignore bottom inset area as we want to draw content into it
                screenManager.Padding = new Rectangle((int)(insets.Left * scale), 
                                                      (int)(insets.Top * scale), 
                                                      (int)((insets.Right - insets.Left) * scale), 
                                                      (int)((insets.Bottom - (int)insets.Top) * scale));
                game.OnOrientationChanged();
            }
        }
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
#if !MONOMAC && !__IOS__ && !__TVOS__
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
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
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

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("OnUnobservedTaskException()");
            HandleException(args.Exception as Exception);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("OnUnhandledException()");
            HandleException(args.ExceptionObject as Exception);
        }

        private void HandleException(Exception ex)
        {
            try
            {
                var ae = ex as AggregateException;
                if (ae != null)
                {
                    ex = ae.Flatten().InnerExceptions[0];
                }
                MainScene mainScene = null;
                try
                {
                    mainScene = game?.MainScene;
                }
                catch
                {
                }
                if (mainScene != null)
                {
                    mainScene.GameException(this, new Engine.New.GameExceptionEventArgs() { e = ex });
                }
                else
                {
                    var subject = $"Mariášek crash report v{MariasekMonoGame.Version} ({MariasekMonoGame.Platform})";
                    var msg = string.Format("{0}\n{1}\n{2}\n-\n{3}", ex?.Message, ex?.StackTrace,
                                            mainScene?.g?.DebugString?.ToString() ?? string.Empty,
                                            mainScene?.g?.BiddingDebugInfo?.ToString() ?? string.Empty);

                    emailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, subject, msg, new string[0]);
                }
            }
            catch
            {
            }
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

