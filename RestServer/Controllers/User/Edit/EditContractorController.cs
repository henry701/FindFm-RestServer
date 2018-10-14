using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using RestServer.Model.Http.Request;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.User.Edit
{
    [Controller]
    [Route("/editSelf/contractor")]
    internal class EditContractorController : EditUserControllerBase<EditContractorRequest, Contractor>
    {
        public EditContractorController(MongoWrapper mongoWrapper, ILogger<EditContractorController> logger) : base(mongoWrapper, logger)
        {

        }

        protected override async Task<Contractor> BindUser(EditContractorRequest requestBody, DateTime creationDate)
        {
            return await Task.Run(() => new Contractor()
            {
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
                FullName = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
                About = requestBody.Sobre,
            });
        }

        protected override Task<UpdateDefinition<Contractor>> CreateUpdateDefinition(Contractor oldUser, Contractor newUser)
        {
            var userUpdateBuilder = new UpdateDefinitionBuilder<Contractor>();
            var userUpdate = userUpdateBuilder
                .Set(u => u.Address, newUser.Address)
                .Set(u => u.Avatar, newUser.Avatar)
                .Set(u => u.StartDate, newUser.StartDate)
                .Set(u => u.Email, newUser.Email)
                .Set(u => u.FullName, newUser.FullName)
                .Set(u => u.Password, newUser.Password)
                .Set(u => u.Phone, newUser.Phone)
                .Set(u => u.Avatar, newUser.Avatar)
                .Set(u => u.About, newUser.About);
            return Task.FromResult(userUpdate);
        }
    }
}
