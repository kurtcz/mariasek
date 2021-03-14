
namespace Mariasek.Engine.Configuration
{
    public class PlayerConfigurationElement
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Assembly { get; set; }
        public ParameterConfigurationElementCollection Parameters { get; set; }
    }
}
