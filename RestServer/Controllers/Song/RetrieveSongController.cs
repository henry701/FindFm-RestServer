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
    internal sealed class RetrieveSongController : ControllerBase
    {
        private readonly ILogger<RetrieveSongController> Logger;
        private readonly MongoWrapper MongoWrapper;
   
        public RetrieveSongController(MongoWrapper mongoWrapper, ILogger<RetrieveSongController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveSongController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet("{userId}/{songId}")]
        public async Task<dynamic> GetById(string userId, string songId)
        {
            var userCollection = MongoWrapper.Database.GetCollection<Models.Musician>(nameof(Models.User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(userId)),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var songFilterBuilder = new FilterDefinitionBuilder<Models.Song>();
            var songFilter = songFilterBuilder.And
            (
                songFilterBuilder.Eq(s => s._id, new ObjectId(songId)),
                GeneralUtils.NotDeactivated(songFilterBuilder)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.Musician>();
            var userProjection = userProjectionBuilder
                .ElemMatch(m => m.Songs, songFilter);

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<Models.Musician>
            {
                Limit = 1,
                Projection = userProjection,
            })).SingleOrDefault();

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

            var song = user.Songs?.SingleOrDefault();

            if (song == default)
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
                Message = "Música encontrada com sucesso!",
                Data = ResponseMappingExtensions.BuildSongResponse(song),
            };
        }

        [AllowAnonymous]
        [HttpGet("{userId}")]
        public async Task<dynamic> GetByAuthorId(string userId)
        {
            var userCollection = MongoWrapper.Database.GetCollection<Models.Musician>(nameof(Models.User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(userId)),
                GeneralUtils.NotDeactivated(userFilterBuilder),
                GeneralUtils.NotDeactivated(userFilterBuilder, m => m.Songs)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.Musician>();
            var userProjection = userProjectionBuilder
                .Include(m => m.Songs);

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<Models.Musician>
            {
                Limit = 1,
            })).SingleOrDefault();

            if (user == null)
            {
                Response.StatusCode = (int) HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Usuário não encontrado!",
                };
            }

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Músicas encontradas com sucesso!",
                Data = user.Songs.Select(ResponseMappingExtensions.BuildSongResponse),
            };
        }
    }
}
 