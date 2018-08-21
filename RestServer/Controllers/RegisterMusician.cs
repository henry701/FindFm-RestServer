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
        private readonly TokenConfigurations TokenConfigurations;
        private readonly SigningConfigurations SigningConfigurations;

        public RegisterMusicianController(MongoWrapper mongoWrapper, ServerInfo serverInfo, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterMusicianController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RegisterMusicianController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
            SmtpConfiguration = smtpConfiguration;
            TokenConfigurations = tokenConfigurations;
            SigningConfigurations = signingConfigurations;
        }

        [HttpPost]
        [AllowAnonymous] // No authorization required for Register Request, obviously
        public async Task<dynamic> Post([FromBody] RegisterMusicianRequest requestBody)
        {
            this.EnsureModelValidation();

            var userCollection = MongoWrapper.Database.GetCollection<User>(typeof(User).Name);

            var filterBuilder = new FilterDefinitionBuilder<User>();
            var filter = filterBuilder.Eq((User u) => u.Email, requestBody.Email);
            var existingUserCount = (await userCollection.CountDocumentsAsync(filter));

            var responseBody = new ResponseBody();

            if (existingUserCount > 0)
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
                var photoStream = ImageUtils.ToStream(photo);
                var fileId = ObjectId.GenerateNewId();
                musician.ImageReference = fileId.ToString();
                var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
                photoTask = gridFsBucket.UploadFromStreamAsync(fileId, fileId.ToString(), photoStream);
                var streamCloseTask = photoTask.ContinueWith(tsk => photoStream.Close(), TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                musician.ImageReference = null;
                photoTask = Task.CompletedTask;
            }

            var insertTask = userCollection.InsertOneAsync(musician);

            try
            {
                Task.WaitAll(insertTask, photoTask);
            }
            catch(Exception e)
            {
                Logger.LogError("Error while registering user", e);
                if(insertTask.IsFaulted && !photoTask.IsFaulted)
                {
                    // TODO: Erase photo
                }
                else if(!insertTask.IsFaulted && photoTask.IsFaulted)
                {
                    // TODO: Erase from MongoDB
                }
            }

            // Não esperamos o e-mail ser enviado, apenas disparamos. Caso não tenha sido enviado vai ter um botão na home depois enchendo o saco pro usuário confirmar o e-mail dele.
            var sendEmailTask = EmailUtils.SendConfirmationEmail(MongoWrapper, SmtpConfiguration, ServerInfo, musician);

            var (creationDate, expiryDate, token) = AuthenticationUtils.GenerateJwtTokenForUser(musician._id.ToString(), musician.Email, TokenConfigurations, SigningConfigurations);

            responseBody.Message = "Registro efetuado com sucesso. Não se esqueça de confirmar seu e-mail!";
            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Data = new
            {
                tokenData = new
                {
                    created = creationDate,
                    expiration = expiryDate,
                    accessToken = token,
                }
            };
            Response.StatusCode = (int) HttpStatusCode.OK;
            return responseBody;
        }
    }
}