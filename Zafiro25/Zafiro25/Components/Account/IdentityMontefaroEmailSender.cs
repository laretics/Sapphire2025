using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;

using Zafiro25.Data;
namespace Zafiro25.Components.Account
{
    public class MontefaroCredentials
    {
		public string server { get; set; } = string.Empty;
		public string dnsFrom { get; set; } = string.Empty;
		public int port {  get; set; }
        public string user { get; set; }= string.Empty;
        public string password { get; set; } = string.Empty;        
    }
	public class IdentityMontefaroEmailSender:IEmailSender<SFMUser>
	{
		private readonly IEmailSender mvarSender;
		public IdentityMontefaroEmailSender(MontefaroCredentials credentials)
		{
			this.mvarSender = new MontefaroEmailSender(credentials);
		}

		public async Task SendConfirmationLinkAsync(SFMUser user, string email, string confirmationLink)
		{
			string auxTexto = string.Format("Por favor, confirme su cuenta haciendo click <a href='{0}'>aquí</a>.", confirmationLink);
			await mvarSender.SendEmailAsync(email, "Zafiro (Confirmación de correo)", auxTexto);
		}
		public async Task SendPasswordResetLinkAsync(SFMUser user, string email, string resetLink)
		{
			await mvarSender.SendEmailAsync(email, "Zafiro (Restablecimiento de contraseña)", string.Format("Por favor, restaure su contraseña haciendo click <a href='{0}'>aquí</a>.", resetLink));

		}
		public async Task SendPasswordResetCodeAsync(SFMUser user, string email, string resetCode)
		{
			await mvarSender.SendEmailAsync(email, "Zafiro (Restablecimiento de contraseña)", string.Format("Por favor, restaure su contraseña utilizando este código: {0}", resetCode));
		}
	}

	public class MontefaroEmailSender : IEmailSender
	{
        private MontefaroCredentials mvarCredentials;
        public MontefaroEmailSender(MontefaroCredentials credentials)
        {
            mvarCredentials = credentials;
			// Habilitar el registro de la actividad SMTP
			var listener = new TextWriterTraceListener(Console.Out);
			Trace.Listeners.Add(listener);
			Trace.AutoFlush = true;
		}

		public Task SendEmailAsync(string email, string subject, string htmlMessage)
		{
            SmtpClient auxClient = new SmtpClient(mvarCredentials.server,mvarCredentials.port)
            {
				Credentials = new NetworkCredential(mvarCredentials.user, mvarCredentials.password),
				EnableSsl = true
			};
            MailMessage auxMessage = new MailMessage()
            {   
				From = new MailAddress( mvarCredentials.dnsFrom),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true				
            };
			auxMessage.To.Add(email);
			return auxClient.SendMailAsync(auxMessage);            
		}
	}
}
