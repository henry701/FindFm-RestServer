using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using static RestServer.Program;

namespace RestServer.Controllers.Other
{
    [Route("/radioInfo")]
    [Controller]
    internal sealed class RadioInfoController : ControllerBase
    {
        internal static ProjectedMusicianSong CurrentSong { get; set; }
        private readonly ILogger<RadioInfoController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public RadioInfoController(MongoWrapper mongoWrapper, ILogger<RadioInfoController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RadioInfoController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<dynamic> Get()
        {
            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Message = "Here's the song!",
                Success = true,
                Data = CurrentSong
            };
        }
    }
}