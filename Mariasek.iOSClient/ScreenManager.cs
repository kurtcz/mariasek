using Mariasek.SharedClient;
using Microsoft.Xna.Framework;
using UIKit;

namespace Mariasek.iOSClient
{
    public class ScreenManager : IScreenManager
    {
        public Rectangle Padding { get; set; }

        public void SetKeepScreenOnFlag(bool flag)
        {
            UIApplication.SharedApplication.IdleTimerDisabled = flag;
        }
    }
}
