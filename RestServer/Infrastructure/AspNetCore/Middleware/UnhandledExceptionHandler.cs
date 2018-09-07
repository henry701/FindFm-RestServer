using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestServer.Model.Http.Response;
using Util.Extensions;

namespace RestServer.Infrastructure.AspNetCore.Middleware
{
    public sealed class UnhandledExceptionHandler
    {
        private ILogger<UnhandledExceptionHandler> Logger { get; set; }
        private RequestDelegate Next { get; set; }

        public UnhandledExceptionHandler(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<UnhandledExceptionHandler>();
            Next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await Next(context);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Unexpected exception occured!");
                if (context.Response.HasStarted)
                {
                    return;
                }
                ResponseBody errorBody = new ResponseBody()
                {
                    Success = false,
                    Code = ResponseCode.GenericFailure,
                    Message = "Erro interno. Por favor, contate o suporte e passe o seguinte: " + context.TraceIdentifier,
                    Data = context.TraceIdentifier
                };
                context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                await context.WriteResultAsync(new ObjectResult(errorBody));
            }
        }
    }
}
