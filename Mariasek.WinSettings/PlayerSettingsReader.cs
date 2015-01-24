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
        private Configuration.PlayersConfigurationSection Convert(PlayersConfigurationSection section)
        {
            return new Configuration.PlayersConfigurationSection
            {
                Player1 = Convert(section.Player1),
                Player2 = Convert(section.Player2),
                Player3 = Convert(section.Player3)
            };
        }

        private Configuration.PlayerConfigurationElement Convert(PlayerConfigurationElement element)
        {
            return new Configuration.PlayerConfigurationElement
            {
                Name = element.Name,
                Assembly = element.Assembly,
                Type = element.Type,
                Parameters = Convert(element.Parameters)
            };
        }

        private Configuration.ParameterConfigurationElementCollection Convert(ParameterConfigurationElementCollection collection)
        {
            var result = new Configuration.ParameterConfigurationElementCollection();

            foreach(var element in collection)
            {
                var parameter = element as ParameterConfigurationElement;
                
                result.Add(parameter.Name, parameter.Value);
            }

            return result;
        }

        public Configuration.PlayersConfigurationSection ReadSettings()
        {
            var playersSettings = ConfigurationManager.GetSection("players") as PlayersConfigurationSection;

            return Convert(playersSettings);
        }
    }
}
