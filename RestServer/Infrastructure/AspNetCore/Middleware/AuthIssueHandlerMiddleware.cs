using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RestServer.Infrastructure.AspNetCore.Result;
using RestServer.Util.Extensions;

namespace RestServer.Infrastructure.AspNetCore.Middleware
{
    internal sealed class AuthIssueHandlerMiddleware
    {
        private ILogger<AuthIssueHandlerMiddleware> Logger { get; set; }
        private RequestDelegate Next { get; set; }

        public AuthIssueHandlerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<AuthIssueHandlerMiddleware>();
            Next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await Next(context);
            if(context.Response.StatusCode == (int) HttpStatusCode.Forbidden)
            {
                if (context.Response.HasStarted)
                {
                    Logger.LogWarning("Attempted to write Forbidden response, but response has already started");
                    return;
                }
                await context.WriteResultAsync(new ForbiddenResult());
            }
            else if (context.Response.StatusCode == (int) HttpStatusCode.Unauthorized)
            {
                if (context.Response.HasStarted)
                {
                    Logger.LogWarning("Attempted to write Forbidden response, but response has already started");
                    return;
                }
                await context.WriteResultAsync(new UnauthorizedResult());
            }
        }
    }
}
