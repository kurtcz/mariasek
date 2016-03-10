using System.Collections.Generic;

namespace Mariasek.Engine.New.Configuration
{
    public class ParameterConfigurationElementCollection : Dictionary<string, ParameterConfigurationElement>
    {
        public new ParameterConfigurationElement this[string key]
        {
            get
            {
                if(!base.ContainsKey(key))
                {
                    return null;
                }
                return base[key];
            }
            set
            {
                if(!base.ContainsKey(key))
                {
                    base.Add(key, value);
                }
                else
                {
                    base[key] = value;
                }
            }
        }

        public void Add(string key, string value)
        {
            base.Add(key, new ParameterConfigurationElement
            {
                Name = key,
                Value = value
            });
        }
    }
}
