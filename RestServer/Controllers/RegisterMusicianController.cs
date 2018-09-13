using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/register/musician")]
    [Controller]
    internal sealed class RegisterMusicianController : RegisterControllerBase<RegisterMusicianRequest>
    {
        public RegisterMusicianController(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterMusicianController> logger) : base(mongoWrapper, smtpConfiguration, tokenConfigurations, signingConfigurations, logger)
        {

        }

        protected override async Task<User> BindUser(RegisterMusicianRequest requestBody, DateTime creationDate)
        {
            return await Task.Run(() => new Musician()
            {
                _id = ObjectId.GenerateNewId(creationDate),
                StartDate = ValidationUtils.ValidateBornDate(requestBody.Nascimento),
                Email = ValidationUtils.ValidateEmail(requestBody.Email),
                IsConfirmed = false,
                Address = new Address()
                {
                    City = requestBody.Cidade,
                    State = EnumExtensions.FromShortDisplayName<BrazilState>(requestBody.Uf),
                    // TODO: Musico nao passa endereço full pq? @Bruno
                    // Road = requestBody.Endereco,
                    // Numeration = requestBody.NumeroEndereco,
                },
                Phone = ValidationUtils.ValidatePhoneNumber(ParsingUtils.ParsePhoneNumber(requestBody.Telefone)),
                Ip = TrackedEntity<IPAddress>.From(HttpContext.Connection.RemoteIpAddress, creationDate),
                Position = TrackedEntity<GeoJsonPoint<GeoJson2DGeographicCoordinates>>.From(null, creationDate),
                FullName = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                UserName = requestBody.NomeUsuario,
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
                InstrumentSkills = requestBody.Instrumentos?.DefaultIfEmpty().Where(instr => instr != null).ToDictionary(instr => EnumExtensions.FromDisplayName<Skill>(instr.Nome), el => (SkillLevel)el.NivelHabilidade).ToHashSet(),
                Works = new HashSet<Work>(),
                Songs = new HashSet<Song>(),
            });
        }
    }
}