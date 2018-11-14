using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.Other
{
    [Route("/geo")]
    [Controller]
    internal sealed class GeolocationController : ControllerBase
    {
        private readonly ILogger<GeolocationController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public GeolocationController(MongoWrapper mongoWrapper, ILogger<GeolocationController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(GeolocationController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpGet]
        public async Task<dynamic> Get()
        {


            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                // Message = "Geolocalização atualizada com sucesso!",
            };
        }
    }
}