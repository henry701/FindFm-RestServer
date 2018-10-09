using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class EditMusicianRequest : IBasicRegisterBody
    {
        public PhoneNumber Telefone { get; set; }
        
        public string Email { get; set; }
        
        public string Senha { get; set; }

        public string Foto { get; set; }
        
        public string NomeCompleto { get; set; }
        
        public DateTime Nascimento { get; set; }
        
        public string Cidade { get; set; }
        
        public string Uf { get; set; }

        public IList<InstrumentRequest> Instrumentos { get; set; }
    }
}