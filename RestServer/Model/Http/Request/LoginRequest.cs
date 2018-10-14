using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
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
        [Required]
        public string Email { get; set; }
        /// <summary>
        /// The password for the <see cref="Models.User"/> account
        /// </summary>
        [Required]
        public string Password { get; set; }
    }
}