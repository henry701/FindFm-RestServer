﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal class CreateSongRequest
    {
        [Required]
        public string Nome { get; set; }
        [Required]
        public string IdResource { get; set; }
        [Required]
        public bool AutorizadoRadio { get; set; }
        [Required]
        public bool Autoral { get; set; }
    }
}
