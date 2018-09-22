using System;
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

namespace RestServer.Controllers
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
            FileReference fileReference_Imagem = null;
            FileReference fileReference_Video = null;
            if (requestBody.imagemId != null)
            {
                fileReference_Imagem = await GeneralUtils.ConsumeReferenceTokenFile(
                    MongoWrapper,
                    requestBody.imagemId,
                    new ObjectId(this.GetCurrentUserId())
                );
            }

            if (requestBody.videoId != null)
            {
                fileReference_Video = await GeneralUtils.ConsumeReferenceTokenFile(
                    MongoWrapper,
                    requestBody.videoId,
                    new ObjectId(this.GetCurrentUserId())
                );
            }
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));
            
            var creationDate = DateTime.UtcNow;
            //TODO: settar o autor man
            var post = new Post
            {
                _id = ObjectId.GenerateNewId(),
                date = creationDate,
                Title = requestBody.Titulo,
                Text = requestBody.Descricao,
                FileReferences = new List<FileReference>()
                {
                     fileReference_Imagem,
                     fileReference_Video
                },

                Ip = HttpContext.Connection.RemoteIpAddress,
                Poster = null, // TODO, some fields from current user
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