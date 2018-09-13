using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Driver;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;

namespace RestServer.Controllers
{
    [Route("/passwordRecovery")]
    [Controller]
    internal sealed class RecoverPasswordController : ControllerBase
    {
        private readonly ILogger<RecoverPasswordController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;

        public RecoverPasswordController(MongoWrapper mongoWrapper, ServerInfo serverInfo, ILogger<RecoverPasswordController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RecoverPasswordController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
        }

        [AllowAnonymous]
        [HttpGet("{token}")]
        public async Task<dynamic> Get(string token)
        {
            var tokenCollection = MongoWrapper.Database.GetCollection<ReferenceToken>(nameof(ReferenceToken));
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(Models.User));

            var randomPasswordTask = GeneralUtils.GenerateRandomString(10, new char[] { 'a' });

            var currentTime = DateTime.UtcNow;

            var confirmationFilterBuilder = new FilterDefinitionBuilder<ReferenceToken>();
            var confirmationFilter = confirmationFilterBuilder.And
            (
                confirmationFilterBuilder.Eq(conf => conf._id, token),
                confirmationFilterBuilder.Eq(conf => conf.TokenType, TokenType.PasswordRecovery),
                confirmationFilterBuilder.Not(
                    confirmationFilterBuilder.Exists(conf => conf.DeactivationDate)
                )
            );

            var confirmationUpdateBuilder = new UpdateDefinitionBuilder<ReferenceToken>();
            var confirmationUpdate = confirmationUpdateBuilder.Set(conf => conf.DeactivationDate, currentTime);

            var oldConfirmation = await tokenCollection.FindOneAndUpdateAsync(confirmationFilter, confirmationUpdate, new FindOneAndUpdateOptions<ReferenceToken>()
            {
                ReturnDocument = ReturnDocument.Before
            });

            if (oldConfirmation == null)
            {
                HttpContext.Response.StatusCode = (int) HttpStatusCode.NotFound;
                return new ResponseBody()
                {
                    Code = ResponseCode.NotFound,
                    Data = null,
                    Message = "O token especificado não existe ou já foi expirado!",
                    Success = false
                };
            }

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(user => user._id, oldConfirmation.User._id),
                userFilterBuilder.Not(
                    userFilterBuilder.Exists(user => user.DeactivationDate)
                )
            );

            var randomPassword = await randomPasswordTask;
            var randomPassEncrypted = Encryption.Encrypt(randomPassword);

            var userUpdateBuilder = new UpdateDefinitionBuilder<User>();
            var userUpdate = userUpdateBuilder.Set(user => user.Password, randomPassEncrypted);

            await userCollection.UpdateOneAsync(userFilter, userUpdate);

            return new ResponseBody()
            {
                Code = ResponseCode.GenericSuccess,
                Data = randomPassword,
                Message = "Uma nova senha foi gerada!",
                Success = true
            };
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<dynamic> Post([FromBody] PasswordRecoveryRequest value)
        {
            return await Task.Run(() => $"POST value: {value}");
        }
    }
}