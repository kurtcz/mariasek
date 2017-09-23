using System;
using Foundation;
using Mariasek.SharedClient;
using UIKit;

namespace Mariasek.iOSClient
{
    public class WebNavigate : IWebNavigate
    {
        public void Navigate(string url)
        {
            UIApplication.SharedApplication.OpenUrl(new NSUrl(url));
        }
    }
}
