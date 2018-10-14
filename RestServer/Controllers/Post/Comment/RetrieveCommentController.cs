using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;
using System.Dynamic;

namespace RestServer.Controllers
{
    [Route("/post/comment")]
    [Controller]
    internal sealed class RetrieveCommentController : ControllerBase
    {
        private readonly ILogger<RetrieveCommentController> Logger;
        private readonly MongoWrapper MongoWrapper;
   
        public RetrieveCommentController(MongoWrapper mongoWrapper, ILogger<RetrieveCommentController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveCommentController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<dynamic> GetById(string id)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(postFilterBuilder)
            );

            var postCommentProjectionBuilder = new ProjectionDefinitionBuilder<Post>();
            var postCommentProjection = postCommentProjectionBuilder.Include(p => p.Comments);

            var post = (await postCollection.FindAsync(postFilter, new FindOptions<Post>
            {
                Limit = 1,
                Projection = postCommentProjection
            })).SingleOrDefault();

            if (post == null)
            {
                Response.StatusCode = (int) HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Comment não encontrado!",
                };
            }

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Comentários do post retornados com sucesso!",
                Data = post.Comments.Select(c => c.BuildCommentResponse()),
            };
        }
    }
}
 