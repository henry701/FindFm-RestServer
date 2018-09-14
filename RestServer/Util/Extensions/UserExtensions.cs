using System.Net;
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
            if (!user.EmailConfirmed)
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
