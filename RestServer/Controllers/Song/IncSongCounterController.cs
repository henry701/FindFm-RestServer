using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;
using System.Dynamic;
using System;

namespace RestServer.Controllers.Song
{
    [Route("/song/")]
    [Controller]
    internal sealed class IncSongCounterController : ControllerBase
    {
        private readonly ILogger<IncSongCounterController> Logger;
        private readonly MongoWrapper MongoWrapper;
   
        public IncSongCounterController(MongoWrapper mongoWrapper, ILogger<IncSongCounterController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(IncSongCounterController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpPost("{userId}/{songId}")]
        public async Task<dynamic> IncrementById(string userId, string songId)
        {
            var userCollection = MongoWrapper.Database.GetCollection<Models.Musician>(nameof(Models.User));

            var songFilterBuilder = new FilterDefinitionBuilder<Models.Song>();
            var songFilter = songFilterBuilder.And
            (
                songFilterBuilder.Eq(s => s._id, new ObjectId(songId)),
                GeneralUtils.NotDeactivated(songFilterBuilder)
            );

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(userId)),
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.ElemMatch(s => s.Songs, songFilter)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.Musician>();
            var userProjection = userProjectionBuilder
                .ElemMatch(m => m.Songs, songFilter);

            var userUpdateBuilder = new UpdateDefinitionBuilder<Models.Musician>();
            var userUpdate = userUpdateBuilder.Inc($"{nameof(Musician.Songs).WithLowercaseFirstCharacter()}.$.{nameof(Models.Song.TimesPlayed).WithLowercaseFirstCharacter()}", 1);

            var user = await userCollection.FindOneAndUpdateAsync(userFilter, userUpdate, new FindOneAndUpdateOptions<Models.Musician>
            {
                Projection = userProjection,
                ReturnDocument = ReturnDocument.Before,
            });

            if (user == null)
            {
                Response.StatusCode = (int) HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Música não encontrada!",
                };
            }

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Contador incrementado com sucesso!",
            };
        }
    }
}
 