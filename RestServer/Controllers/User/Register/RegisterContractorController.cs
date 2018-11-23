using System;
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
    [Route("/register/contractor")]
    [Controller]
    internal sealed class RegisterContractorController : RegisterUserControllerBase<RegisterContractorRequest>
    {
        public RegisterContractorController(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterContractorController> logger) : base(mongoWrapper, smtpConfiguration, tokenConfigurations, signingConfigurations, logger)
        {

        }

        protected override async Task<Models.User> BindUser(RegisterContractorRequest requestBody, DateTime creationDate)
        {
            return await Task.Run(() => new Contractor()
            {
                _id = ObjectId.GenerateNewId(creationDate),
                StartDate = ValidationUtils.ValidateStartDate(requestBody.Inauguracao),
                Email = ValidationUtils.ValidateEmail(requestBody.Email),
                EmailConfirmed = false,
                Address = new Address()
                {
                    City = requestBody.Cidade,
                    State = EnumExtensions.FromShortDisplayName<BrazilState>(requestBody.Uf),
                    Road = requestBody.Endereco,
                    Numeration = requestBody.Numero,
                },
                Phone = ValidationUtils.ValidatePhoneNumber(requestBody.Telefone),
                Ip = TrackedEntity<IPAddress>.From(HttpContext.Connection.RemoteIpAddress, creationDate),
                TrackedPosition = TrackedEntity<GeoJsonPoint<GeoJson3DGeographicCoordinates>>.From(null, creationDate),
                FullName = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
                About = requestBody.Sobre,
                Visits = 0
            });
        }
    }
}