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
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/passwordRecovery")]
    [Controller]
    internal sealed class RecoverPasswordController : ControllerBase
    {
        private readonly ILogger<RecoverPasswordController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly SmtpConfiguration SmtpConfiguration;

        public RecoverPasswordController(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfiguration, ILogger<RecoverPasswordController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RecoverPasswordController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            SmtpConfiguration = smtpConfiguration;
        }

        [AllowAnonymous]
        [HttpGet("{token}")]
        public async Task<dynamic> Get(string token)
        {
            var tokenCollection = MongoWrapper.Database.GetCollection<ReferenceToken>(nameof(ReferenceToken));
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(Models.User));

            var randomPasswordTask = GeneralUtils.GenerateRandomString(
                10,
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_-".ToCharArray()
            );

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
        public async Task<dynamic> Post([FromBody] PasswordRecoveryRequest requestBody)
        {
            this.EnsureModelValidation();

            var collection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var projectionBuilder = new ProjectionDefinitionBuilder<User>();
            var projection = projectionBuilder
                             .Include(u => u._id)
                             .Include(u => u.Email)
                             .Include(u => u.FullName)
                             .Include("_t");

            var filterBuilder = new FilterDefinitionBuilder<User>();
            var filter = filterBuilder.And(
                filterBuilder.Eq(u => u.Email, requestBody.Email),
                filterBuilder.Not(
                    filterBuilder.Exists(u => u.DeactivationDate)
                )
            );

            var user = (await collection.FindAsync(filter, new FindOptions<User>
            {
                Limit = 1,
                Projection = projection,
            })).SingleOrDefault();

            await EmailUtils.SendPasswordRecoveryEmail(MongoWrapper, SmtpConfiguration, user);

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = null,
                Message = "Um código foi enviado para o e-mail! Este código permite a redefinição para uma senha gerada automaticamente.",
                Success = true,
            };
        }
    }
}