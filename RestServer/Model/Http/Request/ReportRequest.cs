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
        Publicação,
        Comentário,
        Anúncio,
        Perfil,
        Música,
        Trabalho,
        Arquivo
    }
}
