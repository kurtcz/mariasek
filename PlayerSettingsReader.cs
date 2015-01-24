using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Configuration = Mariasek.Engine.New.Configuration;

namespace Mariasek.WinSettings
{
    public class PlayerSettingsReader : Configuration.IPlayerSettingsReader
    {
        public Configuration.PlayersConfigurationSection ReadSettings()
        {
            var playersSettings = ConfigurationManager.GetSection("players") as PlayersConfigurationSection;

            throw new NotImplementedException();
        }
    }
}
