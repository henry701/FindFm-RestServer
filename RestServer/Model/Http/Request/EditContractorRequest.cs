using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class EditContractorRequest : IBasicRegisterBody
    {
        public PhoneNumber Telefone { get; set; }

        public string Email { get; set; }

        public string Senha { get; set; }

        public string Foto { get; set; }

        public string Sobre { get; set; }

        public string NomeCompleto { get; set; }

        public int CapacidadeLocal { get; set; }

        public DateTime Inauguracao { get; set; }

        public string Cidade { get; set; }

        public string Uf { get; set; }

        public string Endereco { get; set; }

        public string Numero { get; set; }
    }
}
