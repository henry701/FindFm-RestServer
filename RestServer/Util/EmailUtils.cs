using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Models;
using RestServer.Model.Config;

namespace RestServer.Util
{
    internal static class EmailUtils
    {
        public static async Task SendConfirmationEmail(MongoWrapper mongoWrapper,
                                                       SmtpConfiguration smtpConfig,
                                                       User user)
        {
            var confirmationCollection = mongoWrapper.Database.GetCollection<ReferenceToken>(typeof(ReferenceToken).Name);

            var tokenTask = GenerateRandomToken();

            string token = await tokenTask;

            ReferenceToken confirmation = new ReferenceToken()
            {
                User = user,
                TokenType = TokenType.Confirmation,
                _id = token,
            };

            var insertConfirmationTask = confirmationCollection.InsertOneAsync(confirmation);

            await insertConfirmationTask;

            await SendEmail(smtpConfig: smtpConfig,
                            body: $"Seu código de confirmação FindFM: <b>{token}</b>",
                            subject: "[FindFM] Confirmação de E-mail",
                            encoding: Encoding.UTF8,
                            from: new MailAddress(smtpConfig.Email, smtpConfig.DisplayName, Encoding.UTF8),
                            to: new[] { new MailAddress(user.Email, user.FullName, Encoding.UTF8) });
        }

        public static async Task SendPasswordRecoveryEmail(MongoWrapper mongoWrapper,
                                                           SmtpConfiguration smtpConfig,
                                                           User user)
        {
            var confirmationCollection = mongoWrapper.Database.GetCollection<ReferenceToken>(typeof(ReferenceToken).Name);

            var tokenTask = GenerateRandomToken();

            string token = await tokenTask;

            ReferenceToken confirmation = new ReferenceToken()
            {
                User = user,
                TokenType = TokenType.PasswordRecovery,
                _id = token,
            };

            var insertConfirmationTask = confirmationCollection.InsertOneAsync(confirmation);

            await insertConfirmationTask;

            await SendEmail(smtpConfig: smtpConfig,
                            body: $"Você está recebendo este e-mail pois uma mudança de senha foi requisitada. Caso não tenha requisitado uma mudança de senha, ignore este e-mail.<br>Seu código de nova senha FindFM: <b>{token}</b>",
                            subject: "[FindFM] Confirmação de E-mail",
                            encoding: Encoding.UTF8,
                            from: new MailAddress(smtpConfig.Email, smtpConfig.DisplayName, Encoding.UTF8),
                            to: new[] { new MailAddress(user.Email, user.FullName, Encoding.UTF8) });
        }

        public static async Task SendEmail(SmtpConfiguration smtpConfig,
                                           string body,
                                           string subject,
                                           Encoding encoding,
                                           MailAddress from,
                                           IEnumerable<MailAddress> to)
        {
            SmtpClient client = new SmtpClient
            {
                Port = smtpConfig.Port,
                Host = smtpConfig.Host,
                EnableSsl = true,
                Timeout = smtpConfig.Timeout,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpConfig.Email, smtpConfig.Password),
            };

            MailMessage mailMessage = new MailMessage()
            {
                BodyEncoding = Encoding.UTF8,
                DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure,
                From = new MailAddress(smtpConfig.Email, smtpConfig.DisplayName, Encoding.UTF8),
                BodyTransferEncoding = System.Net.Mime.TransferEncoding.Base64,
                IsBodyHtml = true,
                Subject = subject,
                Priority = MailPriority.Normal,
                SubjectEncoding = Encoding.UTF8,
                Body = body
            };

            foreach (MailAddress address in to)
            {
                mailMessage.To.Add(address);
            }

            await client.SendMailAsync(mailMessage);
        }

        private static async Task<string> GenerateRandomToken()
        {
            byte[] tokenBytes = new byte[512];
            await Task.Run(() => new RNGCryptoServiceProvider().GetBytes(tokenBytes));
            string token = Convert.ToBase64String(tokenBytes);
            return token;
        }
    }
}
