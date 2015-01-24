using System.Configuration;

namespace Mariasek.WinSettings
{
    public class ParameterConfigurationElementCollection : ConfigurationElementCollection
    {
        private const string _ElementName = "parameter";

        protected override ConfigurationElement CreateNewElement()
        {
            return new ParameterConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ParameterConfigurationElement)element).Name;
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.BasicMap; }
        }

        protected override string ElementName
        {
            get { return _ElementName; }
        }

        protected override bool IsElementName(string elementName)
        {
            if (string.IsNullOrWhiteSpace(elementName) || elementName != _ElementName)
                return false;
            return true;
        }

        //public ParameterConfigurationElement this[int index]
        //{
        //    get { return (ParameterConfigurationElement)BaseGet(index); }
        //    set
        //    {
        //        if (BaseGet(index) != null)
        //            BaseRemoveAt(index);
        //        BaseAdd(index, value);
        //    }
        //}

        public ParameterConfigurationElement this[string key]
        {
            get { return (ParameterConfigurationElement)BaseGet(key); }
            set
            {
                var index = -1; //append by default
                var tempElement = (ParameterConfigurationElement)BaseGet(key);
                if (tempElement != null)
                {
                    index = BaseIndexOf(tempElement);
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }

        public void Add(string key, string value)
        {
            BaseAdd(-1, new ParameterConfigurationElement
            {
                Name = key,
                Value = value
            });
        }

        //public string[] AllKeys
        //{
        //    get { return BaseGetAllKeys().Select(i => (string)i).ToArray(); }
        //}
    }
}
