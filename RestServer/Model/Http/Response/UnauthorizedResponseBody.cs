namespace RestServer.Model.Http.Response
{
    internal class UnauthorizedResponseBody : ResponseBody
    {
        public UnauthorizedResponseBody()
        {
            Code = ResponseCode.IncorrectAuthenticationToken;
            Success = false;
            Message = "Autenticação Incorreta!";
            Data = null;
        }
    }
}
