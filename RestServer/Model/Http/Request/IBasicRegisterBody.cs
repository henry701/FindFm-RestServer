using System.ComponentModel.DataAnnotations;

namespace RestServer.Model.Http.Request
{
    internal interface IBasicRegisterBody
    {
        [Required]
        string Email { get; set; }
        string Foto { get; set; }
        string Sobre { get; set; }
    }
}
