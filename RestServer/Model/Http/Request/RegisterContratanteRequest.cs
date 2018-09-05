using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class RegisterContractorRequest : IBasicRegisterBody
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

        public string NomeEstabelecimento { get; set; }
        public int CapacidadeLocal { get; set; }
        public DateTime Inauguracao { get; set; }
        [Required]
        public string Cidade { get; set; }
        [Required]
        public string Uf { get; set; }
        [Required]
        public string Endereco { get; set; }
        [Required]
        public int Numero { get; set; }
    }
}
