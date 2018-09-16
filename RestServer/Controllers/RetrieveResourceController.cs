using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.GridFS;
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
            var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
            var downloadStream = await gridFsBucket.OpenDownloadStreamAsync(new ObjectId(id));
            var fileMetadata = BsonSerializer.Deserialize<FileMetadata>(downloadStream.FileInfo.Metadata);
            string contentType = string.IsNullOrWhiteSpace(fileMetadata.ContentType) ? "application/octet-stream" : fileMetadata.ContentType;
            return new FileStreamResult(downloadStream, contentType);
        }
    }
}
