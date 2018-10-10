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

            Task<FileReference> fileReferenceTask = Task.FromResult<FileReference>(null);

            if (requestBody.MusicaId != null)
            {
                fileReferenceTask = GeneralUtils.ConsumeReferenceTokenFile
                (
                    MongoWrapper,
                    requestBody.MusicaId,
                    new ObjectId(this.GetCurrentUserId())
                );
            }

            var userCollection = MongoWrapper.Database.GetCollection<Musician>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Musician>();
            var userFilter = userFilterBuilder.And(
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(u => u._id, userId)
            );

            var fileReference = await fileReferenceTask;

            var audioNormalizeTask = NormalizeAudio(fileReference, requestBody);

            await audioNormalizeTask;
            var audioReference = new AudioReference
            {
                _id = fileReference._id,
                DeactivationDate = fileReference.DeactivationDate,
                FileMetadata = new AudioMetadata(fileReference.FileMetadata)
            };

            var creationDate = DateTime.UtcNow;
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

            var userUpdateBuilder = new UpdateDefinitionBuilder<Musician>();
            var userUpdate = userUpdateBuilder.AddToSet(m => m.Songs, song);

            var updateResult = await userCollection.UpdateOneAsync(userFilter, userUpdate);

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = song._id,
                Message = "Música adicionada com sucesso!",
                Success = true
            };
        }

        private async Task NormalizeAudio(FileReference fileReference, CreateSongRequest requestBody)
        {
            var oldId = fileReference._id;
            var newId = ObjectId.GenerateNewId();

            var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
            var downloadStream = await gridFsBucket.OpenDownloadStreamAsync(oldId);

            Stream newAudio = AudioHandlerService.ProcessAudio
            (
                downloadStream,
                null,
                requestBody.ObraAutoral ? null : new int?(30),
                requestBody.Titulo,
                null
            );

            await gridFsBucket.UploadFromStreamAsync(fileReference._id, fileReference._id.ToString(), newAudio);

            var deleteOldTask = gridFsBucket.DeleteAsync(oldId);

            fileReference._id = newId;
        }
    }
}