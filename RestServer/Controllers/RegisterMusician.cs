using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RestServer.Exceptions;
using RestServer.Http.Request;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/register/musician")]
    [Controller]
    internal sealed class RegisterMusicianController : ControllerBase
    {
        private readonly ILogger<RegisterMusicianController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;
        private readonly SmtpConfiguration SmtpConfiguration;

        public RegisterMusicianController(MongoWrapper mongoWrapper, ServerInfo serverInfo, SmtpConfiguration smtpConfiguration, ILogger<RegisterMusicianController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RegisterMusicianController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
            SmtpConfiguration = smtpConfiguration;
        }

        [HttpPost]
        [AllowAnonymous] // No authorization required for Register Request, obviously
        public async Task<dynamic> Post([FromBody] RegisterMusicianRequest requestBody)
        {
            this.EnsureModelValidation();

            var userCollection = MongoWrapper.Database.GetCollection<User>(typeof(User).Name);

            var filterBuilder = new FilterDefinitionBuilder<User>();
            var filter = filterBuilder.Eq((User u) => u.Email, requestBody.Email);
            var existingUser = (await userCollection.FindAsync(filter)).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (existingUser != null)
            {
                responseBody.Code = ResponseCode.AlreadyExists;
                responseBody.Success = false;
                responseBody.Message = "Usuário com este e-mail já existe!";
                Response.StatusCode = (int) HttpStatusCode.Conflict;
                return responseBody;
            }

            var musician = new Musician()
            {
                _id = ObjectId.GenerateNewId(),
                Born = ValidationUtils.ValidateBornDate(requestBody.Nascimento),
                Email = ValidationUtils.ValidateEmail(requestBody.Email),
                IsConfirmed = false,
                LastIp = HttpContext.Connection.RemoteIpAddress,
                LastPosition = null,
                Name = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                ImageReference = null,
                Instruments = requestBody.Instrumentos.Select(
                    instr =>
                        instr == null ? null : 
                        new Instrument()
                        {
                            SkillLevel = (SkillLevel) instr.NivelHabilidade,
                            Name = instr.Nome,
                        })
                        .ToList()
            };

            Task photoTask;
            if (requestBody.Foto != null)
            {
                var photo = ImageUtils.FromBytes(Array.ConvertAll(requestBody.Foto, (sb) => (byte)sb));
                photo = ImageUtils.GuaranteeMaxSize(photo, 1000);
                using (var photoStream = ImageUtils.ToStream(photo))
                {
                    var fileId = ObjectId.GenerateNewId();
                    musician.ImageReference = fileId.ToString();
                    var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
                    photoTask = gridFsBucket.UploadFromStreamAsync(fileId, fileId.ToString(), photoStream);
                }
            }
            else
            {
                musician.ImageReference = null;
                photoTask = Task.CompletedTask;
            }

            var insertTask = userCollection.InsertOneAsync(musician);

            // TODO: Limpar foto se insert do Mongo falhar, e vice-versa
            Task.WaitAll(insertTask, photoTask);

            // Não esperamos o e-mail ser enviado, caso não tenha sido enviado vai ter um botão na home depois
            var sendEmailTask = EmailUtils.SendConfirmationEmail(MongoWrapper, SmtpConfiguration, ServerInfo, musician);

            responseBody.Message = "Registro efetuado com sucesso. Não se esqueça de confirmar seu e-mail!";
            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Data = null;
            Response.StatusCode = (int) HttpStatusCode.OK;
            return responseBody;
        }
    }
}