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
using NLog;
using System.Threading;

namespace RestServer.Util
{
    internal static class EmailUtils
    {
        private static readonly ILogger LOGGER = LogManager.GetCurrentClassLogger();

        public static async Task SendConfirmationEmail(MongoWrapper mongoWrapper,
                                                       SmtpConfiguration smtpConfig,
                                                       User user)
        {
            var confirmationCollection = mongoWrapper.Database.GetCollection<ReferenceToken>(typeof(ReferenceToken).Name);

            var tokenTask = GeneralUtils.GenerateRandomBase64();

            string token = await tokenTask;

            ReferenceToken confirmation = new ReferenceToken()
            {
                UserId = user._id,
                TokenType = TokenType.Confirmation,
                _id = token,
            };

            var insertConfirmationTask = confirmationCollection.InsertOneAsync(confirmation);

            await insertConfirmationTask;

            await SendEmail
            (
                smtpConfig: smtpConfig,
                body: $"Seu código de confirmação FindFM: <b>{token}</b>",
                subject: "[FindFM] Confirmação de E-mail",
                encoding: Encoding.UTF8,
                from: new MailAddress(smtpConfig.Email, smtpConfig.DisplayName, Encoding.UTF8),
                to: new[] { new MailAddress(user.Email, user.FullName, Encoding.UTF8) }
            )
            .ContinueWith(t =>
            {
                LOGGER.Error(t.Exception, "Sending confirmation e-mail has failed!");
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }

        public static async Task SendPasswordRecoveryEmail(MongoWrapper mongoWrapper,
                                                           SmtpConfiguration smtpConfig,
                                                           User user)
        {
            var confirmationCollection = mongoWrapper.Database.GetCollection<ReferenceToken>(typeof(ReferenceToken).Name);

            var tokenTask = GeneralUtils.GenerateRandomBase64();

            string token = await tokenTask;

            ReferenceToken confirmation = new ReferenceToken()
            {
                UserId = user._id,
                TokenType = TokenType.PasswordRecovery,
                _id = token,
            };

            var insertConfirmationTask = confirmationCollection.InsertOneAsync(confirmation);

            await insertConfirmationTask;
            /*
            Deu isso aqui no SendEmail: Unexpected exception occured! System.Threading.Tasks.TaskCanceledException: A task was canceled.
            */
            await SendEmail
            (
                smtpConfig: smtpConfig,
                body: $"Você está recebendo este e-mail pois uma mudança de senha foi requisitada. Caso não tenha requisitado uma mudança de senha, ignore este e-mail.<br>Seu código de nova senha FindFM: <b>{token}</b>",
                subject: "[FindFM] Recuperação de senha - Código",
                encoding: Encoding.UTF8,
                from: new MailAddress(smtpConfig.Email, smtpConfig.DisplayName, Encoding.UTF8),
                to: new[] { new MailAddress(user.Email, user.FullName, Encoding.UTF8) }
            )
            .ContinueWith(t =>
            {
                LOGGER.Error(t.Exception, "Sending password recovery e-mail has failed!");
            },
            TaskContinuationOptions.OnlyOnFaulted);
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
                DeliveryFormat = SmtpDeliveryFormat.International,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential
                (
                    smtpConfig.Email,
                    smtpConfig.Password
                ),
            };

            client.ServicePoint.Expect100Continue = true;
            client.ServicePoint.UseNagleAlgorithm = false;

            MailMessage mailMessage = new MailMessage()
            {
                BodyEncoding = Encoding.UTF8,
                DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure,
                From = from,
                BodyTransferEncoding = System.Net.Mime.TransferEncoding.Base64,
                IsBodyHtml = true,
                Sender = from,
                Subject = subject,
                Priority = MailPriority.Normal,
                SubjectEncoding = Encoding.UTF8,
                Body = body
            };

            foreach (MailAddress address in to)
            {
                mailMessage.To.Add(address);
            }

            try
            {
                await client.SendMailAsync(mailMessage);
            }
            catch(Exception e)
            {
                LOGGER.Error(e, "Error while sending e-mail!");
                throw;
            }
        }
    }
}
