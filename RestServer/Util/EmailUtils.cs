using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Models;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using RestServer.Model.Config;

namespace RestServer.Util
{
    internal static class EmailUtils
    {
        public static async Task SendConfirmationEmail(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfig, ServerInfo context, User user)
        {
            var confirmationCollection = mongoWrapper.Database.GetCollection<Confirmation>(typeof(Confirmation).Name);

            var tokenTask = GenerateRandomToken();

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

            string token = await tokenTask;

            Confirmation confirmation = new Confirmation()
            {
                User = user,
                _id = token
            };

            var insertConfirmationTask = confirmationCollection.InsertOneAsync(confirmation);

            MailMessage mailMessage = new MailMessage()
            {
                BodyEncoding = Encoding.UTF8,
                DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure,
                From = new MailAddress(smtpConfig.Email, smtpConfig.DisplayName, Encoding.UTF8),
                BodyTransferEncoding = System.Net.Mime.TransferEncoding.Base64,
                IsBodyHtml = true,
                Subject = "[FindFM] Confirmação de E-mail",
                Priority = MailPriority.Normal,
                SubjectEncoding = Encoding.UTF8,
                // TODO a better body builder, maybe even a template, or view???
                // AQUI É BODY BUILDER!!!
                Body = "<a href='" + context.HostUri.TrimEnd('/') + "/account/confirm/" + token + "'>Clique aqui para confirmar seu cadastro</a>"
            };

            mailMessage.To.Add(user.Email);

            await insertConfirmationTask;
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
