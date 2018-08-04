using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Models;
using MongoDB.Driver;
using RestServer.Http.Request;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;

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
        public dynamic Post([FromBody] LoginRequest requestBody)
        {
            var collection = MongoWrapper.Database.GetCollection<User>(typeof(User).Name);

            var filterBuilder = new FilterDefinitionBuilder<User>();
            var filter = filterBuilder.Eq((User u) => u.Email, requestBody.Email);
            var user = collection.Find(filter).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Usuário não encontrado!";
                Response.StatusCode = (int) HttpStatusCode.Unauthorized;
                return responseBody;
            }

            bool passwordMatches = Encryption.Compare(requestBody.Senha, user.Senha);

            if (!passwordMatches)
            {
                responseBody.Message = "Senha incorreta!";
                responseBody.Code = ResponseCode.IncorrectPassword;
                responseBody.Success = false;
                Response.StatusCode = (int) HttpStatusCode.Unauthorized;
                return responseBody;
            }

            if (!user.IsConfirmed)
            {
                responseBody.Message = "Seu e-mail não foi confirmado!";
                responseBody.Code = ResponseCode.UnconfirmedEmail;
                responseBody.Success = false;
                Response.StatusCode = (int) HttpStatusCode.Unauthorized;
                return responseBody;
            }

            ClaimsIdentity identity = new ClaimsIdentity
            (
                new GenericIdentity(user._id.ToString(), "Login"),
                new[]
                {
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                    new Claim(JwtRegisteredClaimNames.UniqueName, user._id.ToString())
                }
            );

            DateTime dataCriacao = DateTime.Now;
            DateTime dataExpiracao = dataCriacao + TimeSpan.FromSeconds(TokenConfigurations.Seconds);

            var handler = new JwtSecurityTokenHandler();
            var securityToken = handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = TokenConfigurations.Issuer,
                Audience = TokenConfigurations.Audience,
                SigningCredentials = SigningConfigurations.SigningCredentials,
                Subject = identity,
                NotBefore = dataCriacao,
                Expires = dataExpiracao
            });

            var token = handler.WriteToken(securityToken);

            responseBody.Message = "Login efetuado com sucesso!";
            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Data = new {
                tokenData = new
                {
                    created = dataCriacao,
                    expiration = dataExpiracao,
                    accessToken = token,
                }
            };
            Response.StatusCode = (int) HttpStatusCode.OK;
            return responseBody;
        }
    }
}