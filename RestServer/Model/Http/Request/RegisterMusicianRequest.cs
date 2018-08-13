using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal class RegisterMusicianRequest
    {
        public string NomeUsuario { get; set; }
        public bool Confirmado { get; set; }
        public bool Premium { get; set; }
        public string Telefone { get; set; }
        public string Email { get; set; }
        public string Senha { get; set; }
        public byte[] Foto { get; set; }

        public string NomeCompleto { get; set; }
        public DateTime Nascimento { get; set; }
        public string Cidade { get; set; }
        public string Uf { get; set; }
        public IList<Instrument> Instrumentos { get; set; }
    }
}