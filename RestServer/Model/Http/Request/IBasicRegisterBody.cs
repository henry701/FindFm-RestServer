using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    internal interface IBasicRegisterBody
    {
        [Required]
        string Email { get; set; }
        string Foto { get; set; }
    }
}
