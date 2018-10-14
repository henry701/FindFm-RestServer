using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RestServer.Model.Http.Response;

namespace RestServer.Infrastructure.AspNetCore.Result
{
    internal class ForbiddenResult : IActionResult
    {
        private readonly ObjectResult result;

        public ForbiddenResult()
        {
            result = new ObjectResult(new ForbiddenResponseBody());
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = (int) HttpStatusCode.Forbidden;
            return result.ExecuteResultAsync(context);
        }
    }
}