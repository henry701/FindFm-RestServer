﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal class CreatePostRequest
    {
        [Required]
        public string Titulo { get; set; }
        [Required]
        public string Descricao { get; set; }
        [Required]
        public string ImagemId { get; set; }
        [Required]
        public string VideoId { get; set; }
        [Required]
        public string AudioId { get; set; }

    }
}
