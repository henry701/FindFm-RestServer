using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Util.Extensions;

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
            if(context.Response.HasStarted)
            {
                Logger.LogWarning("Attempted to write Forbidden response, but response has already started");
                return;
            }
            if(context.Response.StatusCode == (int) HttpStatusCode.Forbidden)
            {
                await context.WriteResultAsync(new ForbiddenResult());
            }
            else if (context.Response.StatusCode == (int) HttpStatusCode.Unauthorized)
            {
                await context.WriteResultAsync(new UnauthorizedResult());
            }
        }
    }
}
