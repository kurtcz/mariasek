using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net.Configuration;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.TesterGUI
{
    public class SmtpCredentialsSection : ConfigurationSection
    {
        [ConfigurationProperty("userName", DefaultValue = "")]
        public string UserName
        {
            get { return (string)this["userName"]; }
            set { this["userName"] = value; }
        }

        [ConfigurationProperty("password", DefaultValue = "")]
        public string Password { 
            get { return (string)this["password"]; }
            set { this["password"] = value; }
        }
    }

    public static class MailSettings
    {
        private static Configuration _config = ConfigurationManager.OpenExeConfiguration(Assembly.GetEntryAssembly().Location);
        private static MailSettingsSectionGroup mailSettings = _config.GetSectionGroup("system.net/mailSettings") as MailSettingsSectionGroup;
        private static SmtpCredentialsSection smtpCredentials = _config.GetSection("smtpCredentials") as SmtpCredentialsSection;

        public static string Host
        {
            get { return mailSettings.Smtp.Network.Host; }
        }

        public static int Port
        {
            get { return mailSettings.Smtp.Network.Port; }
        }

        public static bool DefaultCredentials
        {
            get { return mailSettings.Smtp.Network.DefaultCredentials; }
        }

        public static bool EnableSsl
        {
            get { return mailSettings.Smtp.Network.EnableSsl; }
        }

        public static string Email
        {
            get { return mailSettings.Smtp.From; }
            set { mailSettings.Smtp.From = value; }
        }

        public static string UserName
        {
            get { return smtpCredentials.UserName; }
            set { smtpCredentials.UserName = value; }
        }

        public static string Password
        {
            get { return smtpCredentials.Password; }
            set { smtpCredentials.Password = value; }
        }

        public static void Open()
        {
            _config = ConfigurationManager.OpenExeConfiguration(Assembly.GetEntryAssembly().Location);
            mailSettings = _config.GetSectionGroup("system.net/mailSettings") as MailSettingsSectionGroup;
            smtpCredentials = _config.GetSection("smtpCredentials") as SmtpCredentialsSection;
            if (!smtpCredentials.SectionInformation.IsProtected)
            {
                smtpCredentials.SectionInformation.ProtectSection("RSAProtectedConfigurationProvider");
                smtpCredentials.SectionInformation.ForceSave = true;
            }
        }
        public static void Save()
        {
            _config.Save(ConfigurationSaveMode.Modified);
        }
    }
}
