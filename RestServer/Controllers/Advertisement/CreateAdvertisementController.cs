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
                userFilterBuilder.Eq(user => user._id, userId)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.Contractor>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include("_t");

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<Models.Contractor>
            {
                Limit = 1,
                AllowPartialResults = false,
                Projection = userProjection
            });

            List<FileReference> files = new List<FileReference>();
            Task<FileReference> fileReference = Task.FromResult<FileReference>(null);
            foreach (MidiaRequest midiaRequest in requestBody.Midias)
            {
                if (midiaRequest.Id != null)
                {
                    fileReference = GeneralUtils.ConsumeReferenceTokenFile(
                        MongoWrapper,
                        midiaRequest.Id,
                        new ObjectId(this.GetCurrentUserId())
                    );
                    files.Add(await fileReference);
                }
            }

            var postCollection = MongoWrapper.Database.GetCollection<Models.Advertisement>(nameof(Models.Advertisement));

            var creationDate = DateTime.UtcNow;

            var ad = new Models.Advertisement
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Title = requestBody.Titulo,
                Text = requestBody.Descricao,
                FileReferences = files,
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