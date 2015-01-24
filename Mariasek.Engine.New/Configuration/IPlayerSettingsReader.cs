using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New.Configuration
{
    public interface IPlayerSettingsReader
    {
        PlayersConfigurationSection ReadSettings();
    }
}
