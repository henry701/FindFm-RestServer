﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Util;
using RestServer.Model.Http.Response;
using System.Net;

namespace RestServer.Controllers
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
            string userId = User.Identities.First(claimIdent => claimIdent.AuthenticationType == "Login").Name;

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
