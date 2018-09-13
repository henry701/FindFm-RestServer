using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    /// <summary>
    /// The request to be made when password recovery is desired
    /// </summary>
    [BindRequired]
    internal class PasswordRecoveryRequest
    {
        /// <summary>
        /// The e-mail for the <see cref="Models.User"/> account
        /// </summary>
        [Required]
        public string Email { get; set; }
    }
}
