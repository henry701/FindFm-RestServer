using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RestServer.Model.Http.Response;

namespace RestServer.Infrastructure.AspNetCore.Result
{
    internal class UnauthorizedResult : IActionResult
    {
        private readonly ObjectResult result;

        public UnauthorizedResult()
        {
            result = new ObjectResult(new UnauthorizedResponseBody());
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = (int) HttpStatusCode.Unauthorized;
            return result.ExecuteResultAsync(context);
        }
    }
}