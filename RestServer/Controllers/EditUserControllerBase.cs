using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;
using System.Dynamic;
using RestServer.Model.Http.Request;
using System;

namespace RestServer.Controllers
{
    internal abstract class EditUserControllerBase<TBody> : ControllerBase where TBody : IBasicRegisterBody
    {
        private readonly ILogger<EditUserControllerBase<TBody>> Logger;
        private readonly MongoWrapper MongoWrapper;

        public EditUserControllerBase(MongoWrapper mongoWrapper, ILogger<EditUserControllerBase<TBody>> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(EditUserControllerBase<TBody>)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpPost("me")]
        public async Task<dynamic> Post(TBody requestBody)
        {
            var id = this.GetCurrentUserId();

            DateTime creationDate = DateTime.UtcNow;

            var newUserTask = this.BindUser(requestBody, creationDate);

            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var newUser = await newUserTask;

            var userUpdateBuilder = new UpdateDefinitionBuilder<User>();
            var userUpdate = userUpdateBuilder
                .Set(u => u.Address, newUser.Address)
                .Set(u => u.Email, newUser.Email)
                .Set(u => u.EmailConfirmed, false)
                .Set(u => u.FullName, newUser.FullName)
                .Set(u => u.Password, newUser.Password)
                .Set(u => u.Phone, newUser.Phone)
                .Set(u => u.Avatar, newUser.Avatar);

            var user = await userCollection.FindOneAndUpdateAsync(userFilter, userUpdate, new FindOneAndUpdateOptions<User>
            {
                ReturnDocument = ReturnDocument.Before
            });

            var responseBody = new ResponseBody();

            if (user == null)
            {
                Logger.LogError("User with valid JWT id was not found in database! Id: {}", id);
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Seu usuário não foi encontrado!";
                Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                return responseBody;
            }

            // TODO: Editar todos os posts daquele usuário com o nome e avatar novo

            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Message = "Usuário editado com sucesso!";

            return responseBody;
        }

        protected abstract Task<User> BindUser(TBody requestBody, DateTime creationDate);
    }
}
 