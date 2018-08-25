using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
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

        [HttpGet("/account/confirm/{token}")]
        public async Task<dynamic> Get(string token)
        {
            var confirmationCollection = MongoWrapper.Database.GetCollection<Confirmation>(nameof(Confirmation));
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(Models.User));

            var currentTime = DateTime.UtcNow;

            var filterBuilder = new FilterDefinitionBuilder<Confirmation>();
            var filter = filterBuilder.And(
                filterBuilder.Eq(conf => conf._id, token),
                filterBuilder.Lt(conf => conf.DeactivationDate, currentTime)
            );

            var updateBuilder = new UpdateDefinitionBuilder<Confirmation>();
            var update = updateBuilder.Set(conf => conf.DeactivationDate, currentTime);

            var oldConfirmation = await confirmationCollection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<Confirmation>()
            {
                ReturnDocument = ReturnDocument.Before
            });

            if(oldConfirmation == null)
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

            // TODO: Validar usuário de fato

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
