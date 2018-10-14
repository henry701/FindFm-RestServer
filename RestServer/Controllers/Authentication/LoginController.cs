using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Driver;
using RestServer.Http.Request;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/login")]
    [Controller]
    internal sealed class LoginController : ControllerBase
    {
        private readonly ILogger<LoginController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;
        private readonly TokenConfigurations TokenConfigurations;
        private readonly SigningConfigurations SigningConfigurations;

        public LoginController(MongoWrapper mongoWrapper, ServerInfo serverInfo, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<LoginController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(LoginController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
            TokenConfigurations = tokenConfigurations;
            SigningConfigurations = signingConfigurations;
        }

        [HttpPost]
        [AllowAnonymous] // No authorization required for Login Request, obviously
        public async Task<dynamic> Post([FromBody] LoginRequest requestBody)
        {
            this.EnsureModelValidation();

            var collection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var projectionBuilder = new ProjectionDefinitionBuilder<User>();
            var projection = projectionBuilder
                             .Include(u => u.Password)
                             .Include(u => u.Avatar)
                             .Include(u => u.FullName)
                             .Include("_t");

            var filterBuilder = new FilterDefinitionBuilder<User>();
            var filter = filterBuilder.And(
                filterBuilder.Eq(u => u.Email, requestBody.Email),
                GeneralUtils.NotDeactivated(filterBuilder)
            );

            var user = (await collection.FindAsync(filter, new FindOptions<User>
            {
                Limit = 1,
                Projection = projection,
            })).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Usuário não encontrado!";
                Response.StatusCode = (int) HttpStatusCode.Unauthorized;
                return responseBody;
            }

            var passwordMatchesTask = Task.Run(() => Encryption.Compare(requestBody.Password, user.Password));

            var (creationDate, expiryDate, token) = await AuthenticationUtils.GenerateJwtTokenForUser(user._id.ToString(), TokenConfigurations, SigningConfigurations);
        
            bool passwordMatches = await passwordMatchesTask;
            if (!passwordMatches)
            {
                responseBody.Message = "Senha incorreta!";
                responseBody.Code = ResponseCode.IncorrectPassword;
                responseBody.Success = false;
                Response.StatusCode = (int) HttpStatusCode.Unauthorized;
                return responseBody;
            }

            responseBody.Message = "Login efetuado com sucesso!";
            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Data = new
            {
                user = new
                {
                    user.Kind,
                    user.Avatar,
                    user.FullName,
                },
                tokenData = new
                {
                    created = creationDate,
                    expiration = expiryDate,
                    accessToken = token,
                }
            };
            Response.StatusCode = (int) HttpStatusCode.OK;
            return responseBody;
        }
    }
}