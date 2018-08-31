using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Models;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Http.Response;

namespace RestServer.Util.Extensions
{
    internal static class UserExtensions
    {
        public static void ValidateUserIsConfirmed(this User user)
        {
            if (!user.IsConfirmed)
            {
                throw new ResultException
                (
                    new ObjectResult
                    (
                        new ResponseBody
                        {
                            Message = "Seu e-mail não foi confirmado!",
                            Code = ResponseCode.UnconfirmedEmail,
                            Success = false,
                        }
                    ),
                    (int) HttpStatusCode.Unauthorized
                );
            }
        }
    }
}
