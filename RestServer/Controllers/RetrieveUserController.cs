using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;
using System.Dynamic;

namespace RestServer.Controllers
{
    [Route("/account")]
    [Controller]
    internal sealed class RetrieveUserController : ControllerBase
    {
        private readonly ILogger<RetrieveUserController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;

        public RetrieveUserController(MongoWrapper mongoWrapper, ILogger<RetrieveUserController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveUserController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<dynamic> Get(string id)
        {
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
            })).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Usuário não encontrado!";
                Response.StatusCode = (int) HttpStatusCode.NotFound;
                return responseBody;
            }

            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Message = "Usuário encontrado com sucesso!";
            responseBody.Data = BuildUserObject(user);

            return responseBody;
        }

        private static dynamic BuildUserObject(User user)
        {
            dynamic userObj = new ExpandoObject();
            userObj.usuario = new ExpandoObject();
            userObj.usuario.endereco = new
                {
                    estado = EnumExtensions.GetAttribute<DisplayAttribute>(user.Address.State).Name,
                    rua = user.Address.Road,
                    numero = user.Address.Numeration,
                    cep = user.Address.ZipCode,
                    cidade = user.Address.City
                };
            userObj.usuario.date = user.StartDate;
            userObj.usuario.avatar = user.Avatar;
            userObj.usuario.email = user.Email;
            userObj.usuario.fullName = user.FullName;
            userObj.usuario.telefone = user.Phone;
            userObj.usuario.Kind = user.Kind;

            if (user is Musician musician)
            {
                IncrementMusicianObject(musician, userObj);
            }
            return userObj;
        }

        private static void IncrementMusicianObject(Musician musician, dynamic userObj)
        {
            //Da um erro mesmo quando o cara nao tem musicas
            /*
             RuntimeBinderException: '<>f__AnonymousType5<<>f__AnonymousType6<<>f__AnonymousType7<string,string,string,string>,Models.ImageReference,string,string,Models.PhoneNumber,string>>' 
             does not contain a definition for 'musicas'
             */
             
            userObj.usuario.musicas = musician.Songs?.Where(s => s != null).Select(song => new
            {
                nome = song.Name,
                idResource = song.AudioReference._id,
                duracao = song.DurationSeconds,
                autoral = song.Original,
                autorizadoRadio = song.RadioAuthorized
            });
            userObj.usuario.habilidades = musician.InstrumentSkills?.ToDictionary(kv => EnumExtensions.GetAttribute<DisplayAttribute>(kv.Key).Name, kv => (int)kv.Value);
        }

        [HttpGet("me")]
        public async Task<dynamic> Get()
        {
            var id = this.GetCurrentUserId();

            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
            })).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                Logger.LogError("User with valid JWT id was not found in database! Id: {}", id);
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Seu usuário não foi encontrado!";
                Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                return responseBody;
            }

            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Message = "Usuário encontrado com sucesso!";
            responseBody.Data = responseBody.Data = BuildUserObject(user);

            return responseBody;
        }
    }
}
 