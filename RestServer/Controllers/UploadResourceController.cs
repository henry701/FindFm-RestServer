﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RestServer.Exceptions;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/upload")]
    [Controller]
    internal sealed class UploadResourceController : ControllerBase
    {
        private readonly ILogger<UploadResourceController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public UploadResourceController(MongoWrapper mongoWrapper, ILogger<UploadResourceController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(UploadResourceController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpPut]
        [RequestSizeLimit(100_000_000)]
        public async Task<dynamic> Put()
        {
            var contentType = Request.Headers["Content-Type"];

            FileType? fileType = FileTypeFromMime(contentType);
            if(!fileType.HasValue)
            {
                throw new ValidationException("Tipo de arquivo desconhecido! Tipo: " + contentType);
            }

            var generatedFileId = ObjectId.GenerateNewId();

            var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);

            var uploadTask = gridFsBucket.UploadFromStreamAsync(generatedFileId, generatedFileId.ToString(), Request.Body, new GridFSUploadOptions
            {
                 Metadata = new FileMetadata
                 {
                     ContentType = contentType,
                     FileType = fileType.Value
                 }.ToBsonDocument()
            });

            var id = this.GetCurrentUserId();

            var confirmationCollection = MongoWrapper.Database.GetCollection<ReferenceToken>(typeof(ReferenceToken).Name);

            var generatedToken = await GeneralUtils.GenerateRandomBase64(256);

            var token = new DataReferenceToken<ObjectId>()
            {
                UserId = new ObjectId(id),
                TokenType = TokenType.FileUpload,
                _id = generatedToken,
                AdditionalData = generatedFileId,
                // Auto-Deletion allowed 1 day from now. TODO: Implement job that does it
                // TODO: How to check for FileReference? Can't delete file if it is already used,
                // in those cases we should delete only the token!
                DeactivationDate = DateTime.UtcNow + TimeSpan.FromDays(1)
            };

            var insertTokenTask = confirmationCollection.InsertOneAsync(token);

            using (var session = await MongoWrapper.MongoClient.StartSessionAsync())
            {
                session.StartTransaction();

                try
                {
                    await insertTokenTask;
                    await uploadTask;
                }
                catch (Exception e)
                {
                    Logger.LogError("Error while uploading user resource", e);
                    await session.AbortTransactionAsync();
                    throw;
                }

                await session.CommitTransactionAsync();
            }

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = generatedToken,
                Message = "Arquivo enviado com sucesso!",
                Success = true
            };
        }

        private static FileType? FileTypeFromMime(string contentType)
        {
            if(contentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {
                return FileType.Image;
            }
            if (contentType.StartsWith("audio", StringComparison.OrdinalIgnoreCase))
            {
                return FileType.Audio;
            }
            if (contentType.StartsWith("video", StringComparison.OrdinalIgnoreCase))
            {
                return FileType.Video;
            }
            return null;
        }
    }
}