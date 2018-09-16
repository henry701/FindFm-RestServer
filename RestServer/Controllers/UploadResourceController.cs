using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using RestServer.Exceptions;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;

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
        public async Task<dynamic> Get()
        {
            var contentType = Request.Headers["Content-Type"];

            FileType fileType;
            switch(contentType)
            {
                case "image/jpeg":
                    fileType = FileType.Image;
                    break;
                case "audio/mpeg":
                    fileType = FileType.Audio;
                    break;
                case "video/mpeg":
                    fileType = FileType.Video;
                    break;
                default:
                    throw new ValidationException("Tipo de arquivo desconhecido! Tipo: " + contentType);
            }

            var generatedId = ObjectId.GenerateNewId();

            var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);

            await gridFsBucket.UploadFromStreamAsync(generatedId, generatedId.ToString(), Request.Body, new GridFSUploadOptions
            {
                 Metadata = new FileMetadata
                 {
                     ContentType = contentType,
                     FileType = fileType
                 }.ToBsonDocument()
            });

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = generatedId,
                Message = "Arquivo enviado com sucesso!",
                Success = true
            };
        }
    }
}