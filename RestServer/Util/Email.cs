using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Models;
using RestServer.Model.Config;

namespace RestServer.Util
{
    internal static class EmailUtils
    {
        public static void SendConfirmationEmail(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfig, ServerInfo context, User user)
        {
            var collectionConfirmações = mongoWrapper.Database.GetCollection<Confirmation>(typeof(Confirmation).Name);

            string token = GenerateRandomToken();

            SmtpClient client = new SmtpClient
            {
                Port = smtpConfig.Port,
                Host = smtpConfig.Host,
                EnableSsl = true,
                Timeout = smtpConfig.Timeout,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpConfig.Email, smtpConfig.Password)
            };

            Confirmation confirmation = new Confirmation()
            {
                User = user,
                Token = token
            };

            collectionConfirmações.InsertOne(confirmation);

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
                Body = "<a href='" + context.HostUri.TrimEnd('/') + "/account/confirm/" + token + "'>Clique aqui para confirmar seu cadastro</a>"
            };

            mailMessage.To.Add(user.Email);

            client.Send(mailMessage);
        }

        private static string GenerateRandomToken()
        {
            byte[] tokenBytes = new byte[512];
            new RNGCryptoServiceProvider().GetBytes(tokenBytes);
            string token = Convert.ToBase64String(tokenBytes);
            return token;
        }
    }
}
