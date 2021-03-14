using System;

namespace Mariasek.Engine
{
    [Flags]
    public enum Hra
    {
        Hra = 1,
        Sedma = 2,
        Kilo = 4,
        SedmaProti = 8,
        KiloProti = 0x10,
        Betl = 0x20,
        Durch = 0x40
    }
}