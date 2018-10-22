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

namespace RestServer.Controllers.User
{
    [Route("/account/search")]
    [Controller]
    internal sealed class SearchUserController : ControllerBase
    {
        private readonly ILogger<SearchUserController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public SearchUserController(MongoWrapper mongoWrapper, ILogger<SearchUserController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(SearchUserController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<dynamic> Get([FromQuery] string search)
        {
            var userCollection = MongoWrapper.Database.GetCollection<MetascoredUser>(nameof(Models.User));

            var userProjectionBuilder = new ProjectionDefinitionBuilder<MetascoredUser>();
            var userProjection = userProjectionBuilder
                .MetaTextScore(nameof(MetascoredUser.MetaScore).WithLowercaseFirstCharacter())
                .Include(user => user._id)
                .Include(user => user.FullName)
                .Include(user => user.Avatar);

            var userFilterBuilder = new FilterDefinitionBuilder<MetascoredUser>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Text(search, new TextSearchOptions
                {
                    CaseSensitive = false,
                    DiacriticSensitive = false,
                }),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var userSortBuilder = new SortDefinitionBuilder<MetascoredUser>();
            var userSort = userSortBuilder.MetaTextScore(nameof(MetascoredUser.MetaScore).WithLowercaseFirstCharacter());

            var users = (await userCollection.FindAsync(userFilter, new FindOptions<MetascoredUser>
            {
                Sort = userSort,
                Limit = 50,
                AllowPartialResults = true,
                Projection = userProjection,
            }));

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Data = users.ToEnumerable().Select(u => u.BuildUserResponse()),
                Message = "Usuários encontrado com sucesso!",
            };
        }

        private class MetascoredUser : Models.User
        {
            public double MetaScore { get; set; }
        }
    }
}
 