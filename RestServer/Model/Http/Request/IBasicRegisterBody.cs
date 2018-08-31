using System;
using System.Collections.Generic;
using System.Text;

namespace RestServer.Model.Http.Request
{
    internal interface IBasicRegisterBody
    {
        string Email { get; set; }
        string Foto { get; set; }
    }
}
