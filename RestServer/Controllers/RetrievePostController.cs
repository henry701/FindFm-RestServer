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
    [Route("/post/")]
    [Controller]
    internal sealed class RetrievePostController : ControllerBase
    {
        private readonly ILogger<RetrievePostController> Logger;
        private readonly MongoWrapper MongoWrapper;
   
        public RetrievePostController(MongoWrapper mongoWrapper, ILogger<RetrievePostController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrievePostController)} Constructor Invoked");
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

            var post = (await postCollection.FindAsync(postFilter, new FindOptions<Post>
            {
                Limit = 1,
            })).SingleOrDefault();

            if (post == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
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
                Message = "Post encontrado com sucesso!",
                Data = BuildPostResponse(post),
            };
        }

        [AllowAnonymous]
        [HttpGet("author/{id}")]
        public async Task<dynamic> GetByAuthorId(string id)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(p => p.Poster._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(postFilterBuilder)
            );

            var posts = await postCollection.FindAsync(postFilter, new FindOptions<Post>
            {
                AllowPartialResults = true,
            });

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Posts encontrados com sucesso!",
                Data = posts.ToList().Select(BuildPostResponse),
            };
        }

        private static dynamic BuildPostResponse(Post post)
        {
            return new
            {
                Titulo = post.Title,
                Descricao = post.Text,
                Autor = post.Poster,
                Likes = (int) post.Likes,
                Criacao = post.CreationDate,
                Midias = post.FileReferences.Select
                    (
                        fr => new
                        {
                            Id = fr._id.ToString(),
                            TipoMidia = fr.FileMetadata.FileType.GetAttribute<DisplayAttribute>().ShortName,
                        }
                   )
            };
        }
    }
}
 