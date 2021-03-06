﻿using System;
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

namespace RestServer.Controllers.User.Register
{
    [Route("/register/musician")]
    [Controller]
    internal sealed class RegisterMusicianController : RegisterUserControllerBase<RegisterMusicianRequest>
    {
        public RegisterMusicianController(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterMusicianController> logger) : base(mongoWrapper, smtpConfiguration, tokenConfigurations, signingConfigurations, logger)
        {

        }

        protected override async Task<Models.User> BindUser(RegisterMusicianRequest requestBody, DateTime creationDate)
        {
            return await Task.Run(() => new Musician()
            {
                _id = ObjectId.GenerateNewId(creationDate),
                StartDate = ValidationUtils.ValidateBornDate(requestBody.Nascimento),
                Email = ValidationUtils.ValidateEmail(requestBody.Email),
                EmailConfirmed = false,
                Address = new Address()
                {
                    City = requestBody.Cidade,
                    State = EnumExtensions.FromShortDisplayName<BrazilState>(requestBody.Uf),
                },
                Phone = ValidationUtils.ValidatePhoneNumber(requestBody.Telefone),
                Ip = TrackedEntity<IPAddress>.From(HttpContext.Connection.RemoteIpAddress, creationDate),
                TrackedPosition = TrackedEntity<GeoJsonPoint<GeoJson3DGeographicCoordinates>>.From(null, creationDate),
                FullName = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
                InstrumentSkills = requestBody.Instrumentos?.DefaultIfEmpty().Where(instr => instr != null).ToDictionary(instr => EnumExtensions.FromDisplayName<Skill>(instr.Nome), el => (SkillLevel)el.NivelHabilidade).ToHashSet(),
                Works = new HashSet<Models.Work>(),
                Songs = new HashSet<Models.Song>(),
                About = requestBody.Sobre,
                Visits = 0
            });
        }
    }
}