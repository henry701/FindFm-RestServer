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
using MongoDB.Driver.GeoJsonObjectModel;

namespace RestServer.Controllers.Post
{
    [Route("/post/create")]
    [Controller]
    internal sealed class CreatePostController : ControllerBase
    {
        private readonly ILogger<CreatePostController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public CreatePostController(MongoWrapper mongoWrapper, ILogger<CreatePostController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(CreatePostController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }
        
        [HttpPost]
        public async Task<dynamic> Post([FromBody] CreatePostRequest requestBody)
        {
            var userId = new ObjectId(this.GetCurrentUserId());

            var userCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(Models.User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.User>();
            var userFilter = userFilterBuilder.And(
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(u => u._id, userId)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.User>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include(m => m.FileBytesLimit)
                .Include(m => m.FileBytesOccupied)
                .Include("_t");

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<Models.User>
            {
                Limit = 1,
                AllowPartialResults = false,
                Projection = userProjection
            });

            Models.User user = null;

            List<(FileReference, Func<Task>)> files = new List<(FileReference, Func<Task>)>();
            Task<(FileReference, Func<Task>)> fileReferenceTask = Task.FromResult<(FileReference, Func<Task>)>((null, () => Task.CompletedTask));
            if (requestBody.Midias != null)
            {
                long totalSize = 0;
                foreach (MidiaRequest midiaRequest in requestBody.Midias)
                {
                    if (midiaRequest.Id != null)
                    {
                        fileReferenceTask = GeneralUtils.GetFileForReferenceToken
                        (
                            MongoWrapper,
                            midiaRequest.Id,
                            userId
                        );
                        var (fileReference, expirer) = await fileReferenceTask;
                        totalSize += fileReference.FileInfo.Size;
                        files.Add((fileReference, expirer));
                    }
                }
                user = (await userTask).Single();
                GeneralUtils.CheckSizeForUser(totalSize, user.FileBytesOccupied, user.FileBytesLimit);
            }

            var postCollection = MongoWrapper.Database.GetCollection<Models.Post>(nameof(Models.Post));

            var creationDate = DateTime.UtcNow;

            var post = new Models.Post
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Title = requestBody.Titulo,
                Text = requestBody.Descricao,
                Comments = new List<Models.Comment>(),
                Likes = new HashSet<ObjectId>(),
                FileReferences = files.Select(f => f.Item1).ToList(),
                Ip = HttpContext.Connection.RemoteIpAddress,
                Poster = user ?? (await userTask).Single(),
                Position = requestBody.Coordenada == null ? null : new GeoJsonPoint<GeoJson3DGeographicCoordinates>(requestBody.Coordenada.ToGeoJsonCoordinate())
            };

            await postCollection.InsertOneAsync(post);

            // Consume the file tokens
            files.AsParallel().ForAll(async f => await f.Item2());

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = post._id,
                Message = "Postagem criada com sucesso!",
                Success = true
            };
        }
    }
}