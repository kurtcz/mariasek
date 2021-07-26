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
using Android.Support.V4.Content;

using Microsoft.Xna.Framework;
using Mariasek.SharedClient;
using Android;
using System.Diagnostics;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.OS.Storage;

namespace Mariasek.AndroidClient
{
    [Activity(Name = "com.tnemec.mariasek.android.MariasekActivity",
               Label = "Mariášek",
               MainLauncher = true,
               Icon = "@mipmap/ic_launcher",
               Theme = "@style/Theme.Splash",
               AlwaysRetainTaskState = true,
               LaunchMode = LaunchMode.SingleInstance,
               ScreenOrientation = ScreenOrientation.SensorLandscape,
               ConfigurationChanges = ConfigChanges.Orientation |
                                      ConfigChanges.ScreenSize |
                                      ConfigChanges.KeyboardHidden |
                                      ConfigChanges.Keyboard)]
    public class MariasekActivity :
        AndroidGameActivity, IEmailSender, IWebNavigate, IScreenManager, IStorageAccessor, IOnApplyWindowInsetsListener
    {
        MariasekMonoGame g;
        int storageAccessRequestCode;

        public Rectangle Padding { get; set; }
        private Stopwatch sw = new Stopwatch();

        protected override void OnCreate(Bundle bundle)
        {
            System.Diagnostics.Debug.WriteLine("OnCreate()");
            sw.Start();

            //handle unobserved task exceptions
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            //handle unhandled exceptions from the UI thread
            AndroidEnvironment.UnhandledExceptionRaiser += OnUnhandledExceptionRaiser;
            //handle unhandled exceptions from background threads
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            //handle unhandled Java thread exceptions
            Java.Lang.Thread.DefaultUncaughtExceptionHandler = new UncaughtExceptionEventHandler(this);
            try
            {
                base.OnCreate(bundle);

                // Create our OpenGL view, and display it
                g = new MariasekMonoGame(this, this, this, this);
                var view = g.Services.GetService<View>();
                if ((int)Android.OS.Build.VERSION.SdkInt >= 28)
                {
                    ViewCompat.SetOnApplyWindowInsetsListener(view, this);
                    Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
                    //Window.AddFlags(WindowManagerFlags.TranslucentStatus);                    
                }
                //if ((int)Android.OS.Build.VERSION.SdkInt >= 29)
                //{
                //    var storageManager = (StorageManager)ApplicationContext.GetSystemService(StorageService);
                //    var storageVolume = storageManager.GetStorageVolume(Android.OS.Environment.ExternalStorageDirectory);

                //    if (storageVolume != null)
                //    {
                //        //Nasledujici metoda zrejme zatim neni implementovana do Xamarin Android SDK 10 ?
                //        //var intent = storageVolume.CreateOpenDocumentTreeIntent();
                //        //Nasledujici alternativni metoda nefunguje s adresarem "Mariasek", cili je nepouzitelna
                //        var intent = storageVolume.CreateAccessIntent("DCIM");
                //        StartActivityForResult(intent, 111);
                //    }
                //}
                if (g.Settings.ShowStatusBar)
                {
                    view.SystemUiVisibility = 0;
                }
                else
                {
                    view.SystemUiVisibility = (StatusBarVisibility)(SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutFullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen | SystemUiFlags.ImmersiveSticky);
                }
                SetContentView(view);
                //g.ExitedEventHandler += ExitedEventHandler;

                sw.Stop();
                System.Diagnostics.Debug.WriteLine("OnCreate sw {0}", sw.ElapsedMilliseconds);
                g.Run();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);

            var view = g.Services.GetService<View>();

            if (g.Settings.ShowStatusBar)
            {
                view.SystemUiVisibility = 0;
            }
            else
            {
                view.SystemUiVisibility = (StatusBarVisibility)(SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutFullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen | SystemUiFlags.ImmersiveSticky);
            }
            if (g.GraphicsDevice != null)
            {
                g.SetupScaleMatrices();
            }
        }

        public bool CheckStorageAccess()
        {
            var permission = Manifest.Permission.WriteExternalStorage;

            return ContextCompat.CheckSelfPermission(this, permission) == Permission.Granted;
        }

        public void GetStorageAccess(bool force = false)
        {
            if ((int)Android.OS.Build.VERSION.SdkInt >= 29 && !force)
            {
                return;
            }
            var permission = Manifest.Permission.WriteExternalStorage;

            if (!CheckStorageAccess())
            {
                ActivityCompat.RequestPermissions(this, new[] { permission }, ++storageAccessRequestCode);
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
                g.OnOrientationChanged();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        //private void ExitedEventHandler(object sender, EventArgs e)
        //{
        //    Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
        //}

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
            catch (Exception ex)
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

        protected override void OnSaveInstanceState(Bundle outState)
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

        private void OnUnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnUnhandledExceptionRaiser()");
            HandleException(e.Exception);
        }

        public void HandleException(Exception ex)
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
                    mainScene = g?.MainScene;
                }
                catch
                {
                }
                if (mainScene != null)
                {
                    mainScene.GameException(this, new Engine.GameExceptionEventArgs() { e = ex });
                }
                else
                {
                    var subject = $"Mariášek crash report v{MariasekMonoGame.Version} ({MariasekMonoGame.Platform})";
                    var msg = string.Format("{0}\nUnhandled exception\n{1}\n{2}\n{3}\n-\n{4}",
                                            subject, ex?.Message, ex?.StackTrace,
                                            mainScene?.g?.DebugString?.ToString() ?? string.Empty,
                                            mainScene?.g?.BiddingDebugInfo?.ToString() ?? string.Empty);

                    SendEmail(new[] { "mariasek.app@gmail.com" }, subject, msg, new string[0]);
                }
            }
            catch
            {
            }
        }

        private Intent[] GetEmailApps()
        {
            var result = new List<Intent>();
            try
            {
                //Intent that only email apps can handle:
                var email = new Intent(Intent.ActionSendto);
                email.SetData(Android.Net.Uri.Parse("mailto:"));
                email.PutExtra(Android.Content.Intent.ExtraEmail, "");
                email.PutExtra(Android.Content.Intent.ExtraSubject, "");

                var packageManager = Application.Context.PackageManager;
                var emailApps = packageManager.QueryIntentActivities(email, PackageInfoFlags.MatchDefaultOnly);//(PackageInfoFlags)0x20000); //PackageInfoFlags.MatchAll == 0x20000

                foreach (var resolveInfo in emailApps)
                {
                    var packageName = resolveInfo.ActivityInfo.PackageName;
                    var app = packageManager.GetLaunchIntentForPackage(packageName);

                    result.Add(app);
                }
            }
            catch
            {
                //fetching email apps failed
            }
            return result.ToArray();
        }

        public void SendEmail(string[] recipients, string subject, string body, string[] attachments)
        {
            var email = new Intent(Android.Content.Intent.ActionSendMultiple);
            var uris = new List<Android.Net.Uri>();

            try
            {
                email.SetType("text/plain");
                email.PutExtra(Android.Content.Intent.ExtraEmail, recipients);
                email.PutExtra(Android.Content.Intent.ExtraSubject, subject);
                email.PutExtra(Android.Content.Intent.ExtraText, body);
                foreach (var attachment in attachments)
                {
                    //copy attachment to external storage where an email application can have access to it
                    //var externalPath = Path.Combine(global::Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
                    //var externalPath = Android.App.Application.Context.GetExternalFilesDir(null).Path;
                    //var path = Path.Combine(externalPath, Path.GetFileName(attachment));
                    //var file = new Java.IO.File(path);
                    var file = new Java.IO.File(attachment);

                    if (!File.Exists(attachment))
                    {
                        continue;
                    }
                    //if (path != attachment)
                    //{
                    //    MainScene.CreateDirectoryForFilePath(path);
                    //    File.Copy(attachment, path, true);
                    //}
                    file.SetReadable(true, false);
                    if ((int)Build.VERSION.SdkInt < 24)
                    {
                        var uri = Android.Net.Uri.FromFile(file);
                        uris.Add(uri);
                    }
                    else
                    {
                        var uri = FileProvider.GetUriForFile(Application.Context, Application.Context.PackageName + ".fileprovider", file);
                        uris.Add(uri);
                    }
                }
                email.PutParcelableArrayListExtra(Intent.ExtraStream, uris.ToArray());

                var chooser = Intent.CreateChooser(email, "Jakou aplikací odeslat email?");
                var emailApps = GetEmailApps();
                if (emailApps.Length > 0)
                {
                    chooser.PutExtra(Intent.ExtraInitialIntents, emailApps);
                }
                StartActivity(chooser);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cannot send email: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void Navigate(string url)
        {
            var browser = new Intent(Intent.ActionView);

            browser.SetData(Android.Net.Uri.Parse(url));
            browser.SetFlags(ActivityFlags.NoHistory | ActivityFlags.NewTask | ActivityFlags.MultipleTask);
            StartActivity(browser);
        }

        public void SetKeepScreenOnFlag(bool flag)
        {
            var view = g.Services.GetService<View>();

            view.KeepScreenOn = flag;
        }

        //tento kod bude fungovat s Xamarin.Android.Support.v4 v29.0.0.0+
        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            var cutout = insets.DisplayCutout;

            if (cutout != null)
            {
                Padding = new Rectangle(cutout.SafeInsetLeft, cutout.SafeInsetTop, cutout.SafeInsetRight, cutout.SafeInsetBottom);

                if (g.GraphicsDevice != null)
                {
                    g.OnOrientationChanged();
                }
            }
            return insets;
        }
    }

    public class UncaughtExceptionEventHandler : Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler
    {
        private MariasekActivity _activity;

        public UncaughtExceptionEventHandler(MariasekActivity activity)
        {
            _activity = activity;
        }

        public void UncaughtException(Java.Lang.Thread t, Java.Lang.Throwable e)
        {
            _activity.HandleException(e);
        }
    }
}

