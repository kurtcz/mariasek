using System.Configuration;

namespace Mariasek.WinSettings
{
    public class PlayerConfigurationElement : ParameterConfigurationElement
    {
        [ConfigurationProperty("name")]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("type")]
        public string Type
        {
            get { return (string)this["type"]; }
            set { this["type"] = value; }
        }

        [ConfigurationProperty("assembly")]
        public string Assembly
        {
            get { return (string)this["assembly"]; }
            set { this["assembly"] = value; }
        }


        [ConfigurationProperty("", IsRequired = false, IsKey = false, IsDefaultCollection = true)]
        public ParameterConfigurationElementCollection Parameters
        {
            get { return (ParameterConfigurationElementCollection)base[""]; }
            set { base[""] = value; }
        }
    }
}
