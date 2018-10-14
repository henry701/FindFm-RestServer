using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Driver;
using RestServer.Model.Http.Response;
using RestServer.Util;

namespace RestServer.Controllers.Authentication
{
    internal sealed class ConfirmEmailController : ControllerBase
    {
        private readonly MongoWrapper MongoWrapper;
        private readonly ILogger<ConfirmEmailController> Logger;

        public ConfirmEmailController(MongoWrapper mongoWrapper, ILogger<ConfirmEmailController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(ConfirmEmailController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet("/account/confirm/{token}")]
        public async Task<dynamic> Get(string token)
        {
            var tokenCollection = MongoWrapper.Database.GetCollection<ReferenceToken>(nameof(ReferenceToken));
            var userCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(Models.User));

            var currentTime = DateTime.UtcNow;

            var confirmationFilterBuilder = new FilterDefinitionBuilder<ReferenceToken>();
            var confirmationFilter = confirmationFilterBuilder.And
            (
                confirmationFilterBuilder.Eq(conf => conf._id, token),
                confirmationFilterBuilder.Eq(conf => conf.TokenType, TokenType.Confirmation),
                GeneralUtils.NotDeactivated(confirmationFilterBuilder, currentTime)
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

            var userFilterBuilder = new FilterDefinitionBuilder<Models.User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(user => user._id, oldConfirmation.UserId),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var userUpdateBuilder = new UpdateDefinitionBuilder<Models.User>();
            var userUpdate = userUpdateBuilder.Set(user => user.EmailConfirmed, true);

            await userCollection.UpdateOneAsync(userFilter, userUpdate);

            return new ResponseBody()
            {
                Code = ResponseCode.GenericSuccess,
                Data = null,
                Message = "E-mail validado com sucesso!",
                Success = true
            };
        }
    }
}
