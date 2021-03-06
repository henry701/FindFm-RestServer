﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RestServer.Exceptions;
using RestServer.Model.Http.Response;
using RestServer.Util.Extensions;

namespace RestServer.Infrastructure.AspNetCore.Middleware
{
    internal sealed class ApplicationExceptionHandler
    {
        private RequestDelegate Next { get; set; }

        public ApplicationExceptionHandler(RequestDelegate next)
        {
            Next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await Next(context);
            }
            // TODO: Exception Middleware clauses by class ^-^
            catch (ApplicationException exception)
            {
                if (context.Response.HasStarted)
                {
                    return;
                }
                if (exception is ResultException resultException)
                {
                    context.Response.StatusCode = resultException.HttpCode ?? context.Response.StatusCode;
                    await context.WriteResultAsync(resultException.Result);
                    return;
                }
                if (exception is ValidationException validationException)
                {
                    context.Response.StatusCode = 422; // Unprocessable Entity - Specific form of Bad Request
                    ResponseBody errorBody = new ResponseBody()
                    {
                        Success = false,
                        Code = ResponseCode.ValidationFailure,
                        Message = validationException.Message,
                    };
                    await context.WriteResultAsync(new ObjectResult(errorBody));
                    return;
                }
                if(exception is UserLimitException userLimitException)
                {
                    context.Response.StatusCode = 422; // Unprocessable Entity - Specific form of Bad Request
                    ResponseBody errorBody = new ResponseBody()
                    {
                        Success = false,
                        Code = ResponseCode.GenericFailure,
                        Message = userLimitException.Message,
                    };
                    await context.WriteResultAsync(new ObjectResult(errorBody));
                    return;
                }
                throw;
            }
        }
    }
}
