﻿using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.User.Register
{
    internal abstract class RegisterUserControllerBase<TBody> : ControllerBase where TBody : IBasicRegisterBody
    {
        private readonly ILogger<RegisterUserControllerBase<TBody>> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly SmtpConfiguration SmtpConfiguration;
        private readonly TokenConfigurations TokenConfigurations;
        private readonly SigningConfigurations SigningConfigurations;

        public RegisterUserControllerBase(MongoWrapper mongoWrapper, SmtpConfiguration smtpConfiguration, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations, ILogger<RegisterUserControllerBase<TBody>> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RegisterUserControllerBase<TBody>)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            SmtpConfiguration = smtpConfiguration;
            TokenConfigurations = tokenConfigurations;
            SigningConfigurations = signingConfigurations;
        }

        [HttpPost]
        [AllowAnonymous] // No authorization required for Register Request, obviously
        public async Task<dynamic> Post([FromBody] TBody requestBody)
        {
            this.EnsureModelValidation();

            var userCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(Models.User));

            await RegistrarUtils.TestUserExists(this, userCollection, requestBody.Email);
            ResponseBody responseBody = new ResponseBody();

            DateTime creationDate = DateTime.UtcNow;

            var user = await BindUser(requestBody, creationDate);

            // 300MB file storage limit default
            user.FileBytesLimit = 300_000_000;

            var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);

            Task photoTask = this.UploadPhoto(requestBody.Foto, gridFsBucket, user, creationDate);

            var insertTask = userCollection.InsertOneAsync(user);

            try
            {
                await insertTask;
            }
            catch (Exception e)
            {
                Logger.LogError("Error while registering user", e);
                try
                {
                    await photoTask;
                    ObjectId? photoId = user?.Avatar?._id;
                    if(photoId.HasValue)
                    {
                        var deleteLingeringPhotoTask = gridFsBucket.DeleteAsync(photoId.Value);
                    }
                }
                catch
                {
                    // Ignore, no need to erase photo or log anything: User's insert has failed anyways.
                }
                throw;
            }

            // Não esperamos o e-mail ser enviado, apenas disparamos. Caso não tenha sido enviado vai ter um botão na home depois enchendo o saco pro usuário confirmar o e-mail dele.
            var sendEmailTask = EmailUtils.SendConfirmationEmail(MongoWrapper, SmtpConfiguration, user);

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

        protected abstract Task<Models.User> BindUser(TBody requestBody, DateTime creationDate);
    }
}