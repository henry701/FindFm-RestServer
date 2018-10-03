using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/post/like")]
    [Controller]
    internal sealed class LikePostController : ControllerBase
    {
        private readonly ILogger<LikePostController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public LikePostController(MongoWrapper mongoWrapper, ILogger<LikePostController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(LikePostController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpGet("{id}")]
        public async Task<dynamic> LikeById(string id)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(postFilterBuilder)
            );

            ObjectId currentUserId = new ObjectId(this.GetCurrentUserId());

            var postUpdateBuilder = new UpdateDefinitionBuilder<Post>();
            var postUpdate = postUpdateBuilder.AddToSet(p => p.Likes, currentUserId);

            var updateResult = await postCollection.UpdateOneAsync(
                postFilter,
                postUpdate
            );

            if (updateResult.MatchedCount == 0)
            {
                Response.StatusCode = (int) HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Post não encontrado!",
                };
            }

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Post Likeado com sucesso!",
            };
        }
    }
}
