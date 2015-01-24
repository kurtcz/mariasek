using System.Configuration;

namespace Mariasek.WinSettings
{
    public class PlayersConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("player1")]
        public PlayerConfigurationElement Player1
        {
            get { return (PlayerConfigurationElement)this["player1"]; }
            set { this["player1"] = value; }
        }

        [ConfigurationProperty("player2")]
        public PlayerConfigurationElement Player2
        {
            get { return (PlayerConfigurationElement)this["player2"]; }
            set { this["player2"] = value; }
        }

        [ConfigurationProperty("player3")]
        public PlayerConfigurationElement Player3
        {
            get { return (PlayerConfigurationElement)this["player3"]; }
            set { this["player3"] = value; }
        }
    }
}
