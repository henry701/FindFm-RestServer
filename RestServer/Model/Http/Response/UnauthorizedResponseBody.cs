namespace RestServer.Model.Http.Response
{
    public class UnauthorizedResponseBody : ResponseBody
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
