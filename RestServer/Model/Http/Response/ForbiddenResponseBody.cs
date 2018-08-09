namespace RestServer.Model.Http.Response
{
    public class ForbiddenResponseBody : ResponseBody
    {
        public ForbiddenResponseBody()
        {
            Code = ResponseCode.RequiresAuthentication;
            Success = false;
            Message = "Essa operação requer autenticação!";
            Data = null;
        }
    }
}
