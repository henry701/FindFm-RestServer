using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Models;
using MongoDB.Driver;
using RestServer.Http.Request;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;

namespace RestServer.Controllers
{
    [Route("/register")]
    [Controller]
    internal sealed class RegisterController : ControllerBase
    {
        private readonly ILogger<LoginController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;

        public RegisterController(MongoWrapper mongoWrapper, ServerInfo serverInfo, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<LoginController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(LoginController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
        }

        [HttpPost]
        [AllowAnonymous] // No authorization required for Login Request, obviously
        public async Task<dynamic> Post([FromBody] RegisterRequest requestBody)
        {
            var userCollection = MongoWrapper.Database.GetCollection<User>(typeof(User).Name);

            var filterBuilder = new FilterDefinitionBuilder<User>();
            var filter = filterBuilder.Eq((User u) => u.Email, requestBody.Email);
            var existingUser = (await userCollection.FindAsync(filter)).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (existingUser != null)
            {
                responseBody.Code = ResponseCode.AlreadyExists;
                responseBody.Success = false;
                responseBody.Message = "Usuário com este e-mail já existe!";
                Response.StatusCode = (int) HttpStatusCode.Conflict;
                return responseBody;
            }

            // User newUser = requestBody.User;

            // TODO: Check if password is strong enough

            // newUser.IsConfirmed = false;

            // TODO: Insert user and pic in DB
            // await userCollection.InsertOneAsync(existingUser);

            responseBody.Message = "Regisro efetuado com sucesso! Confirme seu e-mail para continuar.";
            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Data = null;
            Response.StatusCode = (int) HttpStatusCode.OK;
            return responseBody;
        }
    }
}