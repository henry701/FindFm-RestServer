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
using MongoDB.Driver.GeoJsonObjectModel;
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

            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

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

            if(requestBody.Instrumentos == null)
            {
                requestBody.Instrumentos = new List<InstrumentRequest>();
            }

            DateTime creationDate = DateTime.UtcNow;

            var musician = new Musician()
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Born = ValidationUtils.ValidateBornDate(requestBody.Nascimento),
                Email = ValidationUtils.ValidateEmail(requestBody.Email),
                IsConfirmed = false,
                Ip = TrackedEntity<IPAddress>.From(HttpContext.Connection.RemoteIpAddress, creationDate),
                Position = TrackedEntity<GeoJsonPoint<GeoJson2DGeographicCoordinates>>.From(null, creationDate),
                Name = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
                InstrumentSkills = requestBody.Instrumentos.DefaultIfEmpty().Where(instr => instr != null).ToDictionary(instr => SkillFromAppName(instr.Nome)).Select(keyPair => KeyValuePair.Create(keyPair.Key, (SkillLevel)keyPair.Value.NivelHabilidade)).ToDictionary(k => k.Key, k => k.Value)
            };

            Task photoTask;
            if (requestBody.Foto != null)
            {
                var photo = ImageUtils.FromBytes(Array.ConvertAll(requestBody.Foto, (sb) => (byte)sb));
                if(photo == null)
                {
                    responseBody.Code = ResponseCode.InvalidImage;
                    responseBody.Success = false;
                    responseBody.Message = "A imagem enviada é inválida!";
                    Response.StatusCode = (int) HttpStatusCode.BadRequest;
                    return responseBody;
                }
                photo = ImageUtils.GuaranteeMaxSize(photo, 1000);
                var photoStream = ImageUtils.ToStream(photo);
                var fileId = ObjectId.GenerateNewId();
                musician.Avatar = new ImageReference()
                {
                    _id = ObjectId.GenerateNewId(creationDate),
                    MediaMetadata = new ImageMetadata()
                    {
                        MediaType = MediaType.Image,
                        ContentType = "TODO"
                    }
                };
                var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
                photoTask = gridFsBucket.UploadFromStreamAsync(
                    fileId, 
                    fileId.ToString(), 
                    photoStream,
                    new GridFSUploadOptions()
                    {
                        Metadata = new BsonDocument
                        (
                            new Dictionary<String, Object>
                            {
                                ["content-type"] = "image/jpeg"
                            }
                        )
                    }
                );
                var streamCloseTask = photoTask.ContinueWith(tsk => photoStream.Close(), TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                musician.Avatar = null;
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
                    // TODO: Erase photo from MongoDB
                }
                else if(!insertTask.IsFaulted && photoTask.IsFaulted)
                {
                    // TODO: Erase entity from MongoDB
                }
            }

            // Não esperamos o e-mail ser enviado, apenas disparamos. Caso não tenha sido enviado vai ter um botão na home depois enchendo o saco pro usuário confirmar o e-mail dele.
            var sendEmailTask = EmailUtils.SendConfirmationEmail(MongoWrapper, SmtpConfiguration, ServerInfo, musician);

            var (tokenCreationDate, tokenExpiryDate, token) = AuthenticationUtils.GenerateJwtTokenForUser(musician._id.ToString(), musician.Email, TokenConfigurations, SigningConfigurations);

            responseBody.Message = "Registro efetuado com sucesso. Não se esqueça de confirmar seu e-mail!";
            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Data = new
            {
                tokenData = new
                {
                    created = tokenCreationDate,
                    expiration = tokenExpiryDate,
                    accessToken = token,
                }
            };
            Response.StatusCode = (int) HttpStatusCode.OK;
            return responseBody;
        }

        private Skill SkillFromAppName(string name)
        {
            switch(name)
            {
                case "":
                    return Skill.Bass;
            }
            throw new ValidationException("Habilidade inválida!");
        }
    }
}