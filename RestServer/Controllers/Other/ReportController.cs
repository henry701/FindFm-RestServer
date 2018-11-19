using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.Other
{
    [Route("/report")]
    [Controller]
    internal sealed class ReportController : ControllerBase
    {
        private readonly ILogger<ReportController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly SmtpConfiguration SmtpConfiguration;

        public ReportController(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfiguration, ILogger<ReportController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(ReportController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            SmtpConfiguration = smtpConfiguration;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<dynamic> Post([FromBody] ReportRequest requestBody)
        {
            var itemTask = TipoQuery.QueryItem(new ObjectId(requestBody.Id), requestBody.Tipo, MongoWrapper);

            var typeName = Enum.GetName(typeof(TipoDenuncia), requestBody.Tipo);

            var emailTitle = $"[Denúncia] {typeName} - {requestBody.Motivo}";

            var emailBody = $"Uma denúncia foi realizada por um usuário da plataforma.<br>" +
                $"<b>Motivo:</b> <pre>{requestBody.Motivo}</pre><br>" +
                $"<b>Informações de Contato:</b> <pre>{requestBody.Contato}</pre><br>" +
                $"<b>Tipo da Entidade:</b> <i>{typeName}</i><br>" +
                $"<b>Dump da Entidade:</b> <pre>" +
                $"{JsonConvert.SerializeObject(await itemTask, Formatting.Indented,new JsonConverter[] { new StringEnumConverter() })}" +
                $"</pre><br>" +
                $"<b></b>";

            var address = new MailAddress(SmtpConfiguration.Email, SmtpConfiguration.DisplayName, Encoding.UTF8);

            await EmailUtils.SendEmail
            (
                smtpConfig: SmtpConfiguration,
                body: emailBody,
                subject: emailTitle,
                encoding: Encoding.UTF8,
                from: address,
                to: new [] { address }
            );

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Message = "Denúncia enviada com sucesso!",
                Success = true,
            };
        }
    }

    internal static class TipoQuery
    {
        private static readonly
            IReadOnlyDictionary<TipoDenuncia, (string, Func<ObjectId, FilterDefinition<dynamic>>)>
            queryMapping = new
            Dictionary<TipoDenuncia, (string, Func<ObjectId, FilterDefinition<dynamic>>)>()
        {
            {
                TipoDenuncia.Publicação,
                (
                    nameof(Post),
                    (Func<ObjectId, FilterDefinition<dynamic>>)
                        (eid => new FilterDefinitionBuilder<dynamic>().Eq("_id", eid))
                )
            },
            {
                TipoDenuncia.Comentário,
                (
                    nameof(Post),
                    (Func<ObjectId, FilterDefinition<dynamic>>)
                        (eid => new FilterDefinitionBuilder<dynamic>().Eq
                            ($"{nameof(Models.Post.Comments).WithLowercaseFirstCharacter()}._id", eid))
                )
            },
            {
                TipoDenuncia.Anúncio,
                (
                    nameof(Advertisement),
                    (Func<ObjectId, FilterDefinition<dynamic>>)
                        (eid => new FilterDefinitionBuilder<dynamic>().Eq("_id", eid))
                )
            },
            {
                TipoDenuncia.Perfil,
                (
                    nameof(User),
                    (Func<ObjectId, FilterDefinition<dynamic>>)
                        (eid => new FilterDefinitionBuilder<dynamic>().Eq("_id", eid))
                )
            },
            {
                TipoDenuncia.Música,
                (
                    nameof(User),
                    (Func<ObjectId, FilterDefinition<dynamic>>)
                        (eid => new FilterDefinitionBuilder<dynamic>().Eq
                            ($"{nameof(Models.Musician.Songs).WithLowercaseFirstCharacter()}._id", eid))
                )
            },
            {
                TipoDenuncia.Trabalho,
                (
                    nameof(User),
                    (Func<ObjectId, FilterDefinition<dynamic>>)
                        (eid => new FilterDefinitionBuilder<dynamic>().Eq
                            ($"{nameof(Models.Musician.Works).WithLowercaseFirstCharacter()}._id", eid))
                )
            }
        };

        public static async Task<dynamic> QueryItem(ObjectId entityId, TipoDenuncia denuncia, MongoWrapper mongoWrapper)
        {
            queryMapping.TryGetValue(denuncia, out var tup);
            if (tup.Item1 == null)
            {
                var bucket = new GridFSBucket<ObjectId>(mongoWrapper.Database);
                return bucket.Find((Expression<Func<GridFSFileInfo<ObjectId>, bool>>)(f => f.Id == entityId)).Single();
            }
            var collection = mongoWrapper.Database.GetCollection<dynamic>(tup.Item1);
            var findCursorTask = collection.FindAsync
            (
                tup.Item2.Invoke(entityId),
                new FindOptions<dynamic>
                {
                    AllowPartialResults = false,
                    Limit = 1
                }
            );
            return (await findCursorTask).Single();
        }
    }
}