using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New.Logger
{
    public static class LogManager
    {
        public static ILog GetLogger(Type type)
        {
            return new DummyLogWrapper();
        }
    }
}
