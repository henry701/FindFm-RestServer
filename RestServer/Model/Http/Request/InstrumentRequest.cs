using System;
using System.Collections.Generic;
using System.Text;

namespace RestServer.Model.Http.Request
{
    internal sealed class InstrumentRequest
    {
        public string Nome { get; set; }
        public int NivelHabilidade { get; set; }
    }
}
