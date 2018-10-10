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

namespace RestServer.Controllers
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

            var userCollection = MongoWrapper.Database.GetCollection<Contractor>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Contractor>();
            var userFilter = userFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(user => user._id, userId)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Contractor>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include("_t");

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<Contractor>
            {
                Limit = 1,
                AllowPartialResults = false,
                Projection = userProjection
            });

            Task<FileReference> fileReferenceTask = Task.FromResult<FileReference>(null);
            if (requestBody.MidiaId != null)
            {
                fileReferenceTask = GeneralUtils.ConsumeReferenceTokenFile
                (
                    MongoWrapper,
                    requestBody.MidiaId,
                    new ObjectId(this.GetCurrentUserId())
                );
            }

            var postCollection = MongoWrapper.Database.GetCollection<Advertisement>(nameof(Advertisement));

            var creationDate = DateTime.UtcNow;

            var ad = new Advertisement
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Title = requestBody.Titulo,
                Text = requestBody.Descricao,
                FileReference = await fileReferenceTask,
                Poster = (await userTask).Single()
            };

            await postCollection.InsertOneAsync(ad);

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