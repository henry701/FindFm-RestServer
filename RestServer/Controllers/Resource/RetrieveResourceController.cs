using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.GridFS;
using RestServer.Controllers.User.Register;
using RestServer.Model.Http.Response;
using RestServer.Util;

namespace RestServer.Controllers.Resource
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
            Request.Headers.TryGetValue("If-None-Match", out StringValues clientEtag);
            string clientEtagStr = clientEtag.Count > 0 ? clientEtag.ToString() : null;
            try
            {
                var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
                var downloadStream = await gridFsBucket.OpenDownloadStreamAsync
                (
                    new ObjectId(id),
                    new GridFSDownloadOptions
                    {
                        Seekable = true,
                        CheckMD5 = false
                    }
                );
                if (clientEtagStr != null)
                {
                    if (downloadStream.FileInfo.MD5.Equals(clientEtagStr.Trim('"'), StringComparison.Ordinal))
                    {
                        Response.StatusCode = (int) HttpStatusCode.NotModified;
                        return new EmptyResult();
                    }
                }

                var fileMetadata = BsonSerializer.Deserialize<FileMetadata>(downloadStream.FileInfo.Metadata);
                string contentType = string.IsNullOrWhiteSpace(fileMetadata.ContentType)
                                     ? "application/octet-stream" : fileMetadata.ContentType;
                Response.GetTypedHeaders().CacheControl = CacheControlHeaderValue.Parse(CacheControlHeaderValue.NoCacheString);
                return new FileStreamResult(downloadStream, contentType)
                {
                    EnableRangeProcessing = true,
                    EntityTag = new EntityTagHeaderValue("\"" + downloadStream.FileInfo.MD5 + "\"")
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
