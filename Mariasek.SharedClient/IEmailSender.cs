using System;
using System.Collections.Generic;
using System.Text;

namespace Mariasek.SharedClient
{
    public interface IEmailSender
    {
        void SendEmail(string[] recipients, string subject, string body, string[] attachements);
    }
}
