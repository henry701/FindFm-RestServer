using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/post/comment")]
    [Controller]
    internal sealed class CommentsController : ControllerBase
    {
        private readonly ILogger<CommentsController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public CommentsController(MongoWrapper mongoWrapper, ILogger<CommentsController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(CommentsController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpPost("{id}")]
        public async Task<dynamic> CommentById(string id, [FromBody] CreateCommentRequest requestBody)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(postFilterBuilder)
            );

            var userId = new ObjectId(this.GetCurrentUserId());

            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And(
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(user => user._id, userId)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<User>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include("_t");

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
                AllowPartialResults = false,
                Projection = userProjection
            });

            Comment newComment = new Comment
            {
                _id = ObjectId.GenerateNewId(),
                Commenter = (await userTask).Single(),
                Likes = new HashSet<ObjectId>(),
                Text = requestBody.Comentario
            };

            var postUpdateBuilder = new UpdateDefinitionBuilder<Post>();
            var postUpdate = postUpdateBuilder.Push(p => p.Comments, newComment);

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
                Message = "Comentário criado e vinculado com sucesso!",
                Data = newComment._id,
            };
        }
    }
}
