using API.Configurations;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Threading.Tasks;

namespace API.Services
{
    public class EmailService : IEmailSender
    {
        private readonly SMTPSettings _smtpSettings;

        public EmailService(IOptions<SMTPSettings> smtpSettings)
        {
            _smtpSettings = smtpSettings.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_smtpSettings.SenderName, _smtpSettings.SenderEmail));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = subject;
                message.Body = new TextPart(htmlMessage);

                using(var client = new SmtpClient())
                {
                    await client.ConnectAsync(_smtpSettings.Server, _smtpSettings.Port);
                    await client.AuthenticateAsync(_smtpSettings.UserName, _smtpSettings.Password);
                    
                    await client.SendAsync(message);

                    await client.DisconnectAsync(true);
                }
            }
            catch (System.Exception)
            {
                throw;
            }
        }
    }
}
