using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class RegisterContractorRequest : IBasicRegisterBody
    {
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
        public int CapacidadeLocal { get; set; }
        [Required]
        public DateTime Inauguracao { get; set; }
        [Required]
        public string Cidade { get; set; }
        [Required]
        public string Uf { get; set; }
        [Required]
        public string Endereco { get; set; }
        [Required]
        public string Numero { get; set; }
    }
}
