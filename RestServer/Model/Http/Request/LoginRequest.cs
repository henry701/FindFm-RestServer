using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Http.Request
{
    /// <summary>
    /// Request model for REST Login Request
    /// </summary>
    [BindRequired]
    internal sealed class LoginRequest
    {
        /// <summary>
        /// The e-mail for the <see cref="Models.User"/> account
        /// </summary>
        [BindRequired]
        public string Email { get; set; }
        /// <summary>
        /// The password for the <see cref="Models.User"/> account
        /// </summary>
        [BindRequired]
        public string Password { get; set; }
    }
}