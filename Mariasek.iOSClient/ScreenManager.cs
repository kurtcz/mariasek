using Mariasek.SharedClient;
using UIKit;

namespace Mariasek.iOSClient
{
    public class ScreenUpdater : IScreenManager
    {

        public void SetKeepScreenOnFlag(bool flag)
        {
            UIApplication.SharedApplication.IdleTimerDisabled = flag;
        }
    }
}
