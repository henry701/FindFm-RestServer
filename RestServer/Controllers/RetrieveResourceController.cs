using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.GridFS;
using RestServer.Model.Http.Response;
using RestServer.Util;

namespace RestServer.Controllers
{
    internal sealed class RetrieveResourceController : ControllerBase
    {
        private readonly ILogger<RegisterMusicianController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public RetrieveResourceController(MongoWrapper mongoWrapper, ILogger<RegisterMusicianController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveResourceController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpGet("/resource/{id}")]
        [AllowAnonymous]
        public async Task<dynamic> Get([FromRoute] string id)
        {
            try
            {
                var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
                var downloadStream = await gridFsBucket.OpenDownloadStreamAsync(
                    new ObjectId(id),
                    new GridFSDownloadOptions
                    {
                        Seekable = true,
                        CheckMD5 = false
                    }
                );
                var fileMetadata = BsonSerializer.Deserialize<FileMetadata>(downloadStream.FileInfo.Metadata);
                string contentType = string.IsNullOrWhiteSpace(fileMetadata.ContentType) ? "application/octet-stream" : fileMetadata.ContentType;
                return new FileStreamResult(downloadStream, contentType)
                {
                    EnableRangeProcessing = true,
                    EntityTag = Microsoft.Net.Http.Headers.EntityTagHeaderValue.Parse(downloadStream.FileInfo.MD5)
                };
            }
            catch(GridFSFileNotFoundException)
            {
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Message = "Arquivo não foi encontrado!",
                    Success = false
                };
            }
        }
    }
}
