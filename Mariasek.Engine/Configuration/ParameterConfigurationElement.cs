
namespace Mariasek.Engine.Configuration
{
    public class ParameterConfigurationElement
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public static ParameterConfigurationElement Empty = new ParameterConfigurationElement();
    }
}
