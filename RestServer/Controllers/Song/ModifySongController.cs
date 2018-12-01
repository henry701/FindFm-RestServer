using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RestServer.Interprocess;
using System.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.IO;

namespace RestServer.Controllers.Song
{
    [Route("/song/modify")]
    [Controller]
    internal sealed class ModifySongController : ControllerBase
    {
        private readonly ILogger<ModifySongController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public ModifySongController(MongoWrapper mongoWrapper, ILogger<ModifySongController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(ModifySongController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }
        
        [HttpPost("{id}")]
        public async Task<dynamic> Post([FromBody] ModifySongRequest requestBody, [FromRoute] string id)
        {
            var userId = new ObjectId(this.GetCurrentUserId());
            var songId = new ObjectId(id);

            var userCollection = MongoWrapper.Database.GetCollection<Models.Musician>(nameof(Models.User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, userId),
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.ElemMatch(m => m.Songs, s => s._id == songId),
                GeneralUtils.NotDeactivated(userFilterBuilder, m => m.Songs)
            );

            var userUpdateBuilder = new UpdateDefinitionBuilder<Models.Musician>();
            var userUpdate = userUpdateBuilder
                .Set($"{nameof(Musician.Songs).WithLowercaseFirstCharacter()}.$.{nameof(Models.Song.RadioAuthorized).WithLowercaseFirstCharacter()}",
                    requestBody.AutorizadoRadio)
                .Set($"{nameof(Musician.Songs).WithLowercaseFirstCharacter()}.$.{nameof(Models.Song.Name).WithLowercaseFirstCharacter()}",
                    requestBody.Nome)
                .Set($"{nameof(Musician.Songs).WithLowercaseFirstCharacter()}.$.{nameof(Models.Song.Original).WithLowercaseFirstCharacter()}",
                    requestBody.Autoral);

            var updateResult = await userCollection.UpdateOneAsync(userFilter, userUpdate);

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Message = "Música atualizada com sucesso!",
                Success = true
            };
        }
    }
}