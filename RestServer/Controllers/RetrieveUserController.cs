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
    [Route("/account")]
    [Controller]
    internal sealed class RetrieveUserController : ControllerBase
    {
        private readonly ILogger<RetrieveUserController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public RetrieveUserController(MongoWrapper mongoWrapper, ILogger<RetrieveUserController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveUserController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<dynamic> Get(string id)
        {
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
            })).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Usuário não encontrado!";
                Response.StatusCode = (int) HttpStatusCode.NotFound;
                return responseBody;
            }

            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Message = "Usuário encontrado com sucesso!";
            responseBody.Data = user.BuildUserResponse();

            return responseBody;
        }

        [HttpGet("me")]
        public async Task<dynamic> Get()
        {
            var id = this.GetCurrentUserId();

            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
            })).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                Logger.LogError("User with valid JWT id was not found in database! Id: {}", id);
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Seu usuário não foi encontrado!";
                Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                return responseBody;
            }

            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Message = "Usuário encontrado com sucesso!";
            responseBody.Data = user.BuildUserResponse();

            return responseBody;
        }
    }
}
 