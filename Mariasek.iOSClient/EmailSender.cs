﻿using System;
using System.IO;

using Foundation;
using MessageUI;
using UIKit;

using Mariasek.SharedClient;

namespace Mariasek.iOSClient
{
	public class EmailSender : IEmailSender
	{
		UIViewController _gameController;
		public UIViewController GameController { get { return _gameController; } set { _gameController = value; } }

		public void SendEmail(string[] recipients, string subject, string body, string[] attachments)
		{
			var nso = new NSObject();

			nso.InvokeOnMainThread(() =>
			{
				if (MFMailComposeViewController.CanSendMail)
				{
					var mailController = new MFMailComposeViewController();
					mailController.SetToRecipients(new string[] { "mariasek.app@gmail.com" });
                    mailController.SetSubject(subject);
					mailController.SetMessageBody(body, false);
					if (attachments != null)
					{
						foreach (var attachment in attachments)
						{
							var data = NSData.FromFile(attachment);
							if (data != null)
							{
								mailController.AddAttachmentData(data, "text/plain", Path.GetFileName(attachment));
							}
						}
					}
					mailController.Finished += (object s, MFComposeResultEventArgs args) =>
					{
						//Console.WriteLine (args.Result.ToString ());
						args.Controller.DismissViewController(true, null);
					};

					_gameController.PresentViewController(mailController, true, null);
				}
			});
		}
	}
}

