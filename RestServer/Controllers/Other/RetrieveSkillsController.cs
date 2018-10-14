using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using RestServer.Model.Http.Response;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.Other
{
    [Route("/instruments")]
    [Controller]
    internal sealed class RetrieveSkillsController : ControllerBase
    {
        private readonly ILogger<RetrieveSkillsController> Logger;

        public RetrieveSkillsController(ILogger<RetrieveSkillsController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveSkillsController)} Constructor Invoked");
        }

        [AllowAnonymous]
        public ResponseBody Get(string id)
        {
            var names = new List<string>();
            foreach(Skill skill in Enum.GetValues(typeof(Skill)))
            {
                var display = skill.GetAttribute<DisplayAttribute>();
                if(display == null)
                {
                    continue;
                }
                names.Add(display.Name);
            }
            return new ResponseBody()
            {
                Code = ResponseCode.GenericSuccess,
                Data = names,
                Message = null,
                Success = true
            };
        }
    }
}