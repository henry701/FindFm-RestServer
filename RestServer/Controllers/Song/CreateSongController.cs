﻿using System;
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

            var userCollection = MongoWrapper.Database.GetCollection<Models.Musician>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();
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
            var song = new Models.Song
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

            var userUpdateBuilder = new UpdateDefinitionBuilder<Models.Musician>();
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

            Stream newAudio = await AudioHandlerService.ProcessAudio
            (
                downloadStream,
                requestBody.ObraAutoral ? null : new int?(30),
                requestBody.Titulo,
                "TODOAutor",
                fileReference.FileMetadata.ContentType
            );

            fileReference._id = newId;
            if (fileReference.FileMetadata == null)
            {
                fileReference.FileMetadata = new AudioMetadata();
            }
            fileReference.FileMetadata.ContentType = "audio/mpeg";
            fileReference.FileMetadata.FileType = FileType.Audio;

            var uploadStream = await gridFsBucket.OpenUploadStreamAsync
            (
                newId,
                newId.ToString(),
                new GridFSUploadOptions
                {
                    Metadata = fileReference.FileMetadata.ToBsonDocument(),
                }
            );

            await newAudio.CopyToAsync(uploadStream);

            await uploadStream.CloseAsync();

            var deleteOldTask = gridFsBucket.DeleteAsync(oldId);
        }
    }
}