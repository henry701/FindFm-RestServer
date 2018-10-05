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

        [HttpGet("{id}")]
        public async Task<dynamic> LikeById(string id)
        {
            //TODO: nao esta achando o comentario, pq o comentario ta dentro do post
            var commentCollection = MongoWrapper.Database.GetCollection<Comment>(nameof(Comment));

            var commentFilterBuilder = new FilterDefinitionBuilder<Comment>();
            var commentFilter = commentFilterBuilder.And
            (
                commentFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(commentFilterBuilder)
            );

            ObjectId currentUserId = new ObjectId(this.GetCurrentUserId());

            var commentUpdateBuilder = new UpdateDefinitionBuilder<Comment>();
            var commentUpdate = commentUpdateBuilder.AddToSet(p => p.Likes, currentUserId);

            var updateResult = await commentCollection.UpdateOneAsync(
                commentFilter,
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

        [HttpDelete("{id}")]
        public async Task<dynamic> UnlikeById(string id)
        {
            //TODO: nao esta achando o comentario, pq o comentario ta dentro do post
            var commentCollection = MongoWrapper.Database.GetCollection<Comment>(nameof(Comment));

            var commentFilterBuilder = new FilterDefinitionBuilder<Comment>();
            var commentFilter = commentFilterBuilder.And
            (
                commentFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(commentFilterBuilder)
            );

            ObjectId currentUserId = new ObjectId(this.GetCurrentUserId());

            var commentUpdateBuilder = new UpdateDefinitionBuilder<Comment>();
            var commentUpdate = commentUpdateBuilder.Pull(p => p.Likes, currentUserId);

            var updateResult = await commentCollection.UpdateOneAsync(
                commentFilter,
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
