using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestServer.Model.Config;
using RestServer.Util;

namespace RestServer.Controllers
{
    [Route("/")]
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

        [HttpGet]
        public IEnumerable<string> Get()
        {
            Logger.LogTrace("GET TRIGGER");
            return new[] { "value1", "value2" };
        }

        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        [HttpPost]
        public void Post([FromBody]string value)
        {

        }

        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {

        }

        [HttpDelete("{id}")]
        public void Delete(int id)
        {

        }

    }
}