using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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
        public async Task<dynamic> Get(ObjectId token)
        {
            // TODO
            return null;
        }
    }
}
