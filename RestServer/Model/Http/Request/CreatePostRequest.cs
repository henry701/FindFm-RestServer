using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RestServer.Model.Http.Request
{
    [BindRequired]
    internal class CreatePostRequest
    {
        //TODO: Mudar pra esse formato
        //{"descricao":"valor do aluguel do mês","midias":[{"contentType":"img/jpeg","id":"asdasdasdasd"},{"contentType":"mus/mp3","id":"asdasdasd"},{"contentType":"video/mp4","id":"asdasdasdasd"}]}

        [Required]
        public string Titulo { get; set; }
        [Required]
        public string Descricao { get; set; }
        [Required]
        public string ImagemId { get; set; }
        [Required]
        public string VideoId { get; set; }
        [Required]
        public string AudioId { get; set; }

    }
}
