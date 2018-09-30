using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal class CreateSongRequest
    {
        [Required]
        public string Titulo { get; set; }
        [Required]
        public string Descricao { get; set; }
        [Required]
        public string MusicaId { get; set; }
        [Required]
        public bool PermitidoRadio { get; set; }
        [Required]
        public bool ObraAutoral { get; set; }
    }
}
