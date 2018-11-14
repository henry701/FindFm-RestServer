using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using GeoCoordinatePortable;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal class CreatePostRequest
    {
        [Required]
        public string Titulo { get; set; }
        [Required]
        public string Descricao { get; set; }
        [Required]
        public List<MidiaRequest> Midias { get; set; }

        public GeoCoordinate Coordenada { get; set; }
    }
}
