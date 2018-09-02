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
    internal abstract class RegisterControllerBase<TBody> : ControllerBase where TBody : IBasicRegisterBody
    {
        private readonly ILogger<RegisterControllerBase<TBody>> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;
        private readonly SmtpConfiguration SmtpConfiguration;
        private readonly TokenConfigurations TokenConfigurations;
        private readonly SigningConfigurations SigningConfigurations;

        public RegisterControllerBase(MongoWrapper mongoWrapper, ServerInfo serverInfo, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterControllerBase<TBody>> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RegisterControllerBase<TBody>)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
            SmtpConfiguration = smtpConfiguration;
            TokenConfigurations = tokenConfigurations;
            SigningConfigurations = signingConfigurations;
        }

        [HttpPost]
        [AllowAnonymous] // No authorization required for Register Request, obviously
        public async Task<dynamic> Post([FromBody] TBody requestBody)
        {
            this.EnsureModelValidation();

            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            await RegistrarUtils.TestUserExists(this, userCollection, requestBody.Email);
            ResponseBody responseBody = new ResponseBody();

            DateTime creationDate = DateTime.UtcNow;

            var user = await BindUser(requestBody, creationDate);

            using (var session = await MongoWrapper.MongoClient.StartSessionAsync())
            {
                session.StartTransaction();

                Task photoTask = this.UploadPhoto(requestBody.Foto, new GridFSBucket<ObjectId>(MongoWrapper.Database), user, creationDate);

                var insertTask = userCollection.InsertOneAsync(session, user);

                try
                {
                    Task.WaitAll(insertTask, photoTask);
                }
                catch (Exception e)
                {
                    Logger.LogError("Error while registering user", e);
                    await session.AbortTransactionAsync();
                    throw;
                }

                await session.CommitTransactionAsync();
            }

            // Não esperamos o e-mail ser enviado, apenas disparamos. Caso não tenha sido enviado vai ter um botão na home depois enchendo o saco pro usuário confirmar o e-mail dele.
            var sendEmailTask = EmailUtils.SendConfirmationEmail(MongoWrapper, SmtpConfiguration, ServerInfo, user);

            var (tokenCreationDate, tokenExpiryDate, token) = await AuthenticationUtils.GenerateJwtTokenForUser(user._id.ToString(), TokenConfigurations, SigningConfigurations);

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

        protected abstract Task<User> BindUser(TBody requestBody, DateTime creationDate);
    }
}