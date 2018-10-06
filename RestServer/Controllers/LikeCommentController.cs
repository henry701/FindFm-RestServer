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
    [Route("/comment/like")]
    [Controller]
    internal sealed class LikeCommentController : ControllerBase
    {
        private readonly ILogger<LikeCommentController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public LikeCommentController(MongoWrapper mongoWrapper, ILogger<LikeCommentController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(LikeCommentController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpGet("{postId}/{commentId}")]
        public async Task<dynamic> LikeById(string postId, string commentId)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var commentFilterBuilder = new FilterDefinitionBuilder<Comment>();
            var commentFilter = commentFilterBuilder.Eq(c => c._id, new ObjectId(commentId));

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(u => u._id, new ObjectId(postId)),
                GeneralUtils.NotDeactivated(postFilterBuilder),
                postFilterBuilder.ElemMatch(u => u.Comments, commentFilter),
                GeneralUtils.NotDeactivated(postFilterBuilder, p => p.Comments)
            );

            ObjectId currentUserId = new ObjectId(this.GetCurrentUserId());

            var commentUpdateBuilder = new UpdateDefinitionBuilder<Post>();
            var commentUpdate = commentUpdateBuilder.AddToSet(p => p.Comments[-1].Likes, currentUserId);

            var updateResult = await postCollection.UpdateOneAsync(
                postFilter,
                commentUpdate
            );

            if (updateResult.MatchedCount == 0)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Comentário não encontrado!",
                };
            }

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Comentário Likeado com sucesso!",
            };
        }

        [HttpDelete("{postId}/{commentId}")]
        public async Task<dynamic> UnlikeById(string postId, string commentId)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(u => u._id, new ObjectId(postId)),
                GeneralUtils.NotDeactivated(postFilterBuilder),
                postFilterBuilder.Eq(u => u.Comments[-1]._id, new ObjectId(commentId)),
                GeneralUtils.NotDeactivated(postFilterBuilder, p => p.Comments[-1])
            );

            ObjectId currentUserId = new ObjectId(this.GetCurrentUserId());

            var commentUpdateBuilder = new UpdateDefinitionBuilder<Post>();
            var commentUpdate = commentUpdateBuilder.Pull(p => p.Comments[-1].Likes, currentUserId);

            var updateResult = await postCollection.UpdateOneAsync(
                postFilter,
                commentUpdate
            );

            if (updateResult.MatchedCount == 0)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Comentário não encontrado!",
                };
            }

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Comentário Unlikeado com sucesso!",
            };
        }
    }
}
