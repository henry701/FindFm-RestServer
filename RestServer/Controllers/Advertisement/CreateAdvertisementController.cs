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

namespace RestServer.Controllers.Advertisement
{
    [Route("/ad/create")]
    [Controller]
    internal sealed class CreateAdvertisementController : ControllerBase
    {
        private readonly ILogger<CreateAdvertisementController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public CreateAdvertisementController(MongoWrapper mongoWrapper, ILogger<CreateAdvertisementController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(CreateAdvertisementController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }
        
        [HttpPost]
        public async Task<dynamic> Post([FromBody] CreateAdvertisementRequest requestBody)
        {
            var userId = new ObjectId(this.GetCurrentUserId());

            var userCollection = MongoWrapper.Database.GetCollection<Models.Contractor>(nameof(Models.User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Contractor>();
            var userFilter = userFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(u => u._id, userId)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.Contractor>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include(m => m.FileBytesLimit)
                .Include(m => m.FileBytesOccupied)
                .Include("_t");

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<Models.Contractor>
            {
                Limit = 1,
                AllowPartialResults = false,
                Projection = userProjection
            });

            Models.Contractor user = null;

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

            var postCollection = MongoWrapper.Database.GetCollection<Models.Advertisement>(nameof(Models.Advertisement));

            var creationDate = DateTime.UtcNow;

            var ad = new Models.Advertisement
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Title = requestBody.Titulo,
                Text = requestBody.Descricao,
                FileReferences = files.Select(f => f.Item1).ToList(),
                Position = new GeoJsonPoint<GeoJson3DGeographicCoordinates>(requestBody.Coordenada?.ToGeoJsonCoordinate()),
                Poster = user ?? (await userTask).Single(),
            };

            await postCollection.InsertOneAsync(ad);

            files.AsParallel().ForAll(async f => await f.Item2());

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = ad._id,
                Message = "Postagem criada com sucesso!",
                Success = true
            };
        }
    }
}