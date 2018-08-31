using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class RegisterContractorRequest : IBasicRegisterBody
    {
        public string NomeUsuario { get; set; }
        public string Telefone { get; set; }
        public string Email { get; set; }
        public string Senha { get; set; }
        public sbyte[] Foto { get; set; } // Java interop

        public string NomeEstabelecimento { get; set; }
        public int CapacidadeLocal { get; set; }
        public DateTime Inauguracao { get; set; }
        public string Cidade { get; set; }
        public string Uf { get; set; }
        public string Endereco { get; set; }
        public int Numero { get; set; }
    }
}
