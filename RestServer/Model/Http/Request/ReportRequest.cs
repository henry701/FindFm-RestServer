using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal sealed class ReportRequest
    {
        [Required]
        public string Id { get; set; }
        [Required]
        public TipoDenuncia Tipo { get; set; }
        [Required]
        public string Motivo { get; set; }
        [Required]
        public string Contato { get; set; }
    }

    internal enum TipoDenuncia
    {
        Publicação,
        Comentário,
        Anúncio,
        Perfil,
        Música,
        Trabalho,
        Arquivo
    }
}
