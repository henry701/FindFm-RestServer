﻿using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal class MusicRequest
    {
        [Required]
        public string IdResource { get; set; }
    }
}
