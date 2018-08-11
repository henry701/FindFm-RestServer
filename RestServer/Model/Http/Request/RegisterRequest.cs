using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    internal class RegisterRequest
    {
        [BindRequired]
        public string Email { get; set; }
    }
}