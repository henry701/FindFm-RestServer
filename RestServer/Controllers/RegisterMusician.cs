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
    internal sealed class RegisterMusicianController : RegisterControllerBase<RegisterMusicianRequest>
    {
        public RegisterMusicianController(MongoWrapper mongoWrapper, ServerInfo serverInfo, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterMusicianController> logger) : base(mongoWrapper, serverInfo, smtpConfiguration, tokenConfigurations, signingConfigurations, logger)
        {

        }

        protected override async Task<User> BindUser(RegisterMusicianRequest requestBody, DateTime creationDate)
        {
            if (requestBody.Instrumentos == null)
            {
                requestBody.Instrumentos = new List<InstrumentRequest>();
            }

            return await Task.Run(() => new Musician()
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Born = ValidationUtils.ValidateBornDate(requestBody.Nascimento),
                Email = ValidationUtils.ValidateEmail(requestBody.Email),
                IsConfirmed = false,
                City = requestBody.Cidade,
                Address = new Address()
                {
                    City = requestBody.Cidade,
                    State = EnumExtensions.FromShortDisplayName<BrazilState>(requestBody.Uf),
                },
                Phone = ValidationUtils.ValidatePhoneNumber(ParsingUtils.ParsePhoneNumber(requestBody.Telefone)),
                Ip = TrackedEntity<IPAddress>.From(HttpContext.Connection.RemoteIpAddress, creationDate),
                Position = TrackedEntity<GeoJsonPoint<GeoJson2DGeographicCoordinates>>.From(null, creationDate),
                FullName = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                UserName = requestBody.NomeUsuario,
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
                //Isso da exception no mongo
                //InstrumentSkills = requestBody.Instrumentos.DefaultIfEmpty().Where(instr => instr != null).ToDictionary(instr => SkillFromAppName(instr.Nome)).Select(keyPair => KeyValuePair.Create(keyPair.Key, (SkillLevel)keyPair.Value.NivelHabilidade)).ToDictionary(k => k.Key, k => k.Value)
            });
        }
    }
}