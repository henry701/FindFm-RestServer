using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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
        public async Task<dynamic> Get([FromRoute] ObjectId id)
        {
            var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
            var downloadStream = await gridFsBucket.OpenDownloadStreamAsync(id);
            var fileMetadata = downloadStream.FileInfo.Metadata.ToDictionary();
            string contentType = fileMetadata.GetValueOrDefault("content-type")?.ToString();
            if(String.IsNullOrWhiteSpace(contentType))
            {
                contentType = "application/octet-stream";
            }
            return new FileStreamResult(downloadStream, contentType);
        }
    }
}
