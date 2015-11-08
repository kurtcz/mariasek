using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.TesterGUI
{
    public static class AppSettings
    {
        private static Configuration _config = ConfigurationManager.OpenExeConfiguration(Assembly.GetEntryAssembly().Location);

        public static bool GetBool(string key, bool defaultValue = default(bool))
        {
            try
            {
                return bool.Parse(_config.AppSettings.Settings[key].Value);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static int GetInt(string key, int defaultValue = default(int))
        {
            try
            {
                return int.Parse(_config.AppSettings.Settings[key].Value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static float GetFloat(string key, float defaultValue = default(float))
        {
            try
            {
                return float.Parse(_config.AppSettings.Settings[key].Value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static double GetDouble(string key, double defaultValue = default(double))
        {
            try
            {
                return double.Parse(_config.AppSettings.Settings[key].Value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static string GetString(string key, string defaultValue = default(string))
        {
            try
            {
                return _config.AppSettings.Settings[key].Value;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static T GetEnum<T>(string key, T defaultValue = default(T))
        {
            try
            {
                return (T)Enum.Parse(typeof(T), _config.AppSettings.Settings[key].Value);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static void SetInt(string key, int value)
        {
            if (_config.AppSettings.Settings[key] == null)
            {
                _config.AppSettings.Settings.Add(key, value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                _config.AppSettings.Settings[key].Value = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static void SetFloat(string key, float value)
        {
            if (_config.AppSettings.Settings[key] == null)
            {
                _config.AppSettings.Settings.Add(key, value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                _config.AppSettings.Settings[key].Value = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static void SetDouble(string key, double value)
        {
            if (_config.AppSettings.Settings[key] == null)
            {
                _config.AppSettings.Settings.Add(key, value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                _config.AppSettings.Settings[key].Value = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static void Set<T>(string key, T value)
        {
            if (_config.AppSettings.Settings[key] == null)
            {
                _config.AppSettings.Settings.Add(key, value.ToString());
            }
            else
            {
                _config.AppSettings.Settings[key].Value = value.ToString();
            }
        }

        public static void Open()
        {
            _config = ConfigurationManager.OpenExeConfiguration(Assembly.GetEntryAssembly().Location);            
        }

        public static void Save()
        {
            _config.Save(ConfigurationSaveMode.Modified);
        }
    }
}
