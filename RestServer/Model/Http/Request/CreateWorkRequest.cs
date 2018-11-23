using Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace RestServer.Model.Http.Request
{
    class CreateWorkRequest
    {
        public string Nome { get; set; }
        public string Descricao { get; set; }
        public bool Original { get; set; }
        public IList<MidiaRequest> Midias { get; set; }
        public IList<MusicRequest> Musicas { get; set; }
        public IList<UserModelRequest> Musicos { get; set; }
    }
}
