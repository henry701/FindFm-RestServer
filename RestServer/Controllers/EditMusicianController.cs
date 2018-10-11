using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using RestServer.Model.Http.Request;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Controller]
    [Route("/editSelf/musician")]
    internal class EditMusicianController : EditUserControllerBase<EditMusicianRequest, Musician>
    {
        public EditMusicianController(MongoWrapper mongoWrapper, ILogger<EditMusicianController> logger) : base(mongoWrapper, logger)
        {

        }

        protected override async Task<Musician> BindUser(EditMusicianRequest requestBody, DateTime creationDate)
        {
            return await Task.Run(() => new Musician()
            {
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
                FullName = ValidationUtils.ValidateName(requestBody.NomeCompleto),
                Password = Encryption.Encrypt(ValidationUtils.ValidatePassword(requestBody.Senha)),
                PremiumLevel = PremiumLevel.None,
                Avatar = null,
                InstrumentSkills = requestBody.Instrumentos?.DefaultIfEmpty().Where(instr => instr != null).ToDictionary(instr => EnumExtensions.FromDisplayName<Skill>(instr.Nome), el => (SkillLevel)el.NivelHabilidade).ToHashSet(),
                Works = new HashSet<Work>(),
                Songs = new HashSet<Song>(),
                About = requestBody.Sobre,
            });
        }

        protected override Task<UpdateDefinition<Musician>> CreateUpdateDefinition(Musician oldUser, Musician newUser)
        {
            var userUpdateBuilder = new UpdateDefinitionBuilder<Musician>();
            var userUpdate = userUpdateBuilder
                .Set(u => u.Address, newUser.Address)
                .Set(u => u.Avatar, newUser.Avatar)
                .Set(u => u.StartDate, newUser.StartDate)
                .Set(u => u.Email, newUser.Email)
                .Set(u => u.FullName, newUser.FullName)
                .Set(u => u.Password, newUser.Password)
                .Set(u => u.Phone, newUser.Phone)
                .Set(u => u.Avatar, newUser.Avatar)
                .Set(u => u.About, newUser.About)
                .Set(u => u.InstrumentSkills, newUser.InstrumentSkills);
            return Task.FromResult(userUpdate);
        }
    }
}
