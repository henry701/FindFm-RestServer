﻿using System;
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
                userFilterBuilder.Eq(user => user._id, userId)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.User>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include("_t");

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<Models.User>
            {
                Limit = 1,
                AllowPartialResults = false,
                Projection = userProjection
            });

            Task<FileReference> fileReference_Imagem = Task.FromResult<FileReference>(null);
            if (requestBody.ImagemId != null)
            {
                fileReference_Imagem = GeneralUtils.ConsumeReferenceTokenFile(
                    MongoWrapper,
                    requestBody.ImagemId,
                    new ObjectId(this.GetCurrentUserId())
                );
            }

            Task<FileReference> fileReference_Video = Task.FromResult<FileReference>(null);
            if (requestBody.VideoId != null)
            {
                fileReference_Video = GeneralUtils.ConsumeReferenceTokenFile(
                    MongoWrapper,
                    requestBody.VideoId,
                    new ObjectId(this.GetCurrentUserId())
                );
            }

            Task<FileReference> fileReference_Audio = Task.FromResult<FileReference>(null);
            if (requestBody.AudioId != null)
            {
                fileReference_Audio = GeneralUtils.ConsumeReferenceTokenFile(
                    MongoWrapper,
                    requestBody.AudioId,
                    new ObjectId(this.GetCurrentUserId())
                );
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
                FileReferences = new FileReference[]
                {
                     await fileReference_Imagem,
                     await fileReference_Video,
                     await fileReference_Audio
                }.Where(fr => fr != null).ToList(),
                Ip = HttpContext.Connection.RemoteIpAddress,
                Poster = (await userTask).Single()
            };

            await postCollection.InsertOneAsync(post);

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