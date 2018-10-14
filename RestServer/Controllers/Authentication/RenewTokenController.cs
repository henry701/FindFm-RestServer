using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Util;
using RestServer.Model.Http.Response;
using System.Net;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.Authentication
{
    internal sealed class RenewTokenController : ControllerBase
    {
        private readonly ILogger<RenewTokenController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly TokenConfigurations TokenConfigurations;
        private readonly SigningConfigurations SigningConfigurations;

        public RenewTokenController(MongoWrapper mongoWrapper, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RenewTokenController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RenewTokenController)} Constructor Invoked");
            TokenConfigurations = tokenConfigurations;
            SigningConfigurations = signingConfigurations;
            MongoWrapper = mongoWrapper;
        }

        [HttpGet("/auth/renew")]
        public async Task<dynamic> Get()
        {
            var userId = this.GetCurrentUserId();

            var (creationDate, expiryDate, token) = await AuthenticationUtils.GenerateJwtTokenForUser(userId, TokenConfigurations, SigningConfigurations);

            Response.StatusCode = (int) HttpStatusCode.OK;

            return new ResponseBody()
            {
                Message = "Token renovado com sucesso!",
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Data = new
                {
                    tokenData = new
                    {
                        created = creationDate,
                        expiration = expiryDate,
                        accessToken = token,
                    }
                }
            };
        }
    }
}
