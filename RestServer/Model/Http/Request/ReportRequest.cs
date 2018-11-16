using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class ReportRequest
    {
        [Required]
        public string Id;
        [Required]
        public TipoDenuncia Tipo;
        [Required]
        public string Motivo;
        [Required]
        public string Contato;
    }

    internal enum TipoDenuncia
    {
        Post,
        Perfil,
        Anúncio,
        Música,
        Trabalho,
        Imagem,
        Vídeo,
        Comentário
    }
}
