using System.Configuration;

namespace Mariasek.WinSettings
{
    public class ParameterConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("name")]
        public virtual string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("value")]
        public virtual string Value
        {
            get { return (string)this["value"]; }
            set { this["value"] = value; }
        }
    }
}
