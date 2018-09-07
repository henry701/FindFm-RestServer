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
    [Route("/register/contractor")]
    [Controller]
    internal sealed class RegisterContractorController : RegisterControllerBase<RegisterContractorRequest>
    {
        public RegisterContractorController(MongoWrapper mongoWrapper, ServerInfo serverInfo, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterContractorController> logger) : base(mongoWrapper, serverInfo, smtpConfiguration, tokenConfigurations, signingConfigurations, logger)
        {

        }

        protected override async Task<User> BindUser(RegisterContractorRequest requestBody, DateTime creationDate)
        {
            return await Task.Run(() => new Contractor()
            {
                _id = ObjectId.GenerateNewId(creationDate),
                StartDate = ValidationUtils.ValidateStartDate(requestBody.Inauguracao),
                Email = ValidationUtils.ValidateEmail(requestBody.Email),
                IsConfirmed = false,
                Address = new Address()
                {
                    City = requestBody.Cidade,
                    State = EnumExtensions.FromShortDisplayName<BrazilState>(requestBody.Uf),
                    Road = requestBody.Endereco,
                    Numeration = requestBody.Numero,
                },
                Phone = ValidationUtils.ValidatePhoneNumber(ParsingUtils.ParsePhoneNumber(requestBody.Telefone)),
                Ip = TrackedEntity<IPAddress>.From(HttpContext.Connection.RemoteIpAddress, creationDate),
                Position = TrackedEntity<GeoJsonPoint<GeoJson2DGeographicCoordinates>>.From(null, creationDate),
                UserName = requestBody.NomeUsuario,
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
            });
        }
    }
}