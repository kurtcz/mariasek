using System;

namespace Mariasek.Engine.New
{
    [Flags]
    public enum Hra
    {
        Hra = 1,
        Sedma = 2,
        Kilo = 4,
        SedmaProti = 8,
        KiloProti = 16,
        Betl = 32,
        Durch = 64
    }
}