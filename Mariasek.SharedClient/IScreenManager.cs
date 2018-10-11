using System;
using Microsoft.Xna.Framework;

namespace Mariasek.SharedClient
{
    public interface IScreenManager
    {
        void SetKeepScreenOnFlag(bool flag);
        Rectangle Padding { get; set; }
    }
}
