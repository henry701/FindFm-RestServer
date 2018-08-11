using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestServer.Model.Config;
using RestServer.Util;

namespace RestServer.Controllers
{
    [Route("/example")]
    [Controller]
    internal sealed class ExampleController : ControllerBase
    {
        private readonly ILogger<ExampleController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;

        public ExampleController(MongoWrapper mongoWrapper, ServerInfo serverInfo, ILogger<ExampleController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(ExampleController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            return await Task.Run(() => new[] { "value1", "value2" });
        }

        [HttpGet("{id}")]
        public async Task<string> Get(int id)
        {
            return await Task.Run(() => $"GET value: {id}");
        }

        [HttpPost]
        public async Task<string> Post([FromBody]string value)
        {
            return await Task.Run(() => $"POST value: {value}");
        }

        [HttpPut("{id}")]
        public async void Put(int id, [FromBody]string value)
        {
            await Task.Run(() => Thread.Sleep(1000));
        }

        [HttpDelete("{id}")]
        public async void Delete(int id)
        {
            await Task.Run(() => Thread.Sleep(1000));
        }
    }
}