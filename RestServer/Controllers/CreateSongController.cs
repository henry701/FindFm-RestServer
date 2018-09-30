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

namespace RestServer.Controllers
{
    [Route("/song/create")]
    [Controller]
    internal sealed class CreateSongController : ControllerBase
    {
        private readonly ILogger<CreateSongController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public CreateSongController(MongoWrapper mongoWrapper, ILogger<CreateSongController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(CreateSongController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }
        
        [HttpPost]
        public async Task<dynamic> Post([FromBody] CreateSongRequest requestBody)
        {
            var userId = new ObjectId(this.GetCurrentUserId());

            var userCollection = MongoWrapper.Database.GetCollection<Musician>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Musician>();
            var userFilter = userFilterBuilder.And(
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(user => user._id, userId)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Musician>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include("_t");

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<Musician>
            {
                Limit = 1,
                AllowPartialResults = false,
                Projection = userProjection
            });

            Task<FileReference> fileReferenceTask = Task.FromResult<FileReference>(null);

            if (requestBody.MusicaId != null)
            {
                fileReferenceTask = GeneralUtils.ConsumeReferenceTokenFile(
                    MongoWrapper,
                    requestBody.MusicaId,
                    new ObjectId(this.GetCurrentUserId())
                );
            }

            var songCollection = MongoWrapper.Database.GetCollection<Song>(nameof(Song));

            var creationDate = DateTime.UtcNow;

            var fileReference = await fileReferenceTask;
            var audioReference = new AudioReference
            {
                _id = fileReference._id,
                DeactivationDate = fileReference.DeactivationDate,
                FileMetadata = new AudioMetadata(fileReference.FileMetadata)
            };

            var song = new Song
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Name = requestBody.Titulo,
                RadioAuthorized = requestBody.PermitidoRadio,
                Original = requestBody.ObraAutoral,
                AudioReference = audioReference,
                DurationSeconds = 99, // TODO - Parte mais difícil desse proj é provavelmente a porra do parse de música
                TimesPlayed = 0,
                TimesPlayedRadio = 0,
            };

            await songCollection.InsertOneAsync(song);

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = song._id,
                Message = "Música criada com sucesso!",
                Success = true
            };
        }
    }
}