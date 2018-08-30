using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class RegisterMusicianRequest
    {
        public string NomeUsuario { get; set; }
        public string Telefone { get; set; }
        public string Email { get; set; }
        public string Senha { get; set; }
        //Alterar para string
        public sbyte[] Foto { get; set; } // Java interop

        public string NomeCompleto { get; set; }
        public DateTime Nascimento { get; set; }
        public string Cidade { get; set; }
        public string Uf { get; set; }
        public IList<InstrumentRequest> Instrumentos { get; set; }
    }
}