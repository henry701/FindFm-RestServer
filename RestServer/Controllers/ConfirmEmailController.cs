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

namespace RestServer.Controllers
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
            var confirmationCollection = MongoWrapper.Database.GetCollection<Confirmation>(nameof(Confirmation));
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(Models.User));

            var currentTime = DateTime.UtcNow;

            var confirmationFilterBuilder = new FilterDefinitionBuilder<Confirmation>();
            var confirmationFilter = confirmationFilterBuilder.And
            (
                confirmationFilterBuilder.Eq(conf => conf._id, token),
                confirmationFilterBuilder.Not(
                    confirmationFilterBuilder.Exists(conf => conf.DeactivationDate)
                )
            );

            var confirmationUpdateBuilder = new UpdateDefinitionBuilder<Confirmation>();
            var confirmationUpdate = confirmationUpdateBuilder.Set(conf => conf.DeactivationDate, currentTime);

            var oldConfirmation = await confirmationCollection.FindOneAndUpdateAsync(confirmationFilter, confirmationUpdate, new FindOneAndUpdateOptions<Confirmation>()
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
                userFilterBuilder.Lt(user => user.DeactivationDate, currentTime)
            );

            var userUpdateBuilder = new UpdateDefinitionBuilder<User>();
            var userUpdate = userUpdateBuilder.Set(user => user.IsConfirmed, true);

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
