using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class RegisterMusicianRequest : IBasicRegisterBody
    {
        [Required]
        public PhoneNumber Telefone { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Senha { get; set; }

        public string Foto { get; set; }

        public string Sobre { get; set; }

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