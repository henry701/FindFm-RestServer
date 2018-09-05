using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class InstrumentRequest
    {
        [Required]
        public string Nome { get; set; }
        [Required]
        public int NivelHabilidade { get; set; }
    }
}
