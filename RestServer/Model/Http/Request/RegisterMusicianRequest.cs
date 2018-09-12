﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class RegisterMusicianRequest : IBasicRegisterBody
    {
        [Required]
        public string NomeUsuario { get; set; }
        [Required]
        public string Telefone { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Senha { get; set; }

        public string Foto { get; set; }

        [Required]
        public string NomeCompleto { get; set; }
        [Required]
        public DateTime Nascimento { get; set; }
        [Required]
        public string Cidade { get; set; }
        [Required]
        public string Uf { get; set; }
        public IList<InstrumentRequest> Instrumentos { get; set; }
    }
}