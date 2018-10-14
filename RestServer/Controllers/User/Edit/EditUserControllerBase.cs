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
using MongoDB.Driver.GridFS;

namespace RestServer.Controllers.User.Edit
{
    internal abstract class EditUserControllerBase<TBody, TEntity> : ControllerBase where TBody : IBasicRegisterBody where TEntity : Models.User
    {
        private readonly ILogger<EditUserControllerBase<TBody, TEntity>> Logger;
        private readonly MongoWrapper MongoWrapper;

        public EditUserControllerBase(MongoWrapper mongoWrapper, ILogger<EditUserControllerBase<TBody, TEntity>> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(EditUserControllerBase<TBody, TEntity>)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpPost]
        public async Task<dynamic> Post([FromBody] TBody requestBody)
        {
            var id = this.GetCurrentUserId();

            DateTime creationDate = DateTime.UtcNow;

            var newUserTask = BindUser(requestBody, creationDate);

            var userCollection = MongoWrapper.Database.GetCollection<TEntity>(nameof(Models.User));

            var userFilterBuilder = new FilterDefinitionBuilder<TEntity>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var oldUser = (await userCollection.FindAsync(userFilter)).SingleOrDefault();

            if (oldUser == null)
            {
                Logger.LogError("User with valid JWT id was not found in database! Id: {}", id);
                Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Seu usuário não foi encontrado!",
                };
            }

            var newUser = await newUserTask;

            var gridFsBucket = new GridFSBucket<ObjectId>(MongoWrapper.Database);
            await this.UploadPhoto(requestBody.Foto, gridFsBucket, newUser, creationDate);

            var userUpdate = await CreateUpdateDefinition(oldUser, newUser);

            if (oldUser.Email != oldUser.Email)
            {
                userUpdate = userUpdate.Set(u => u.EmailConfirmed, false);
            }

            var userUpdateTask = userCollection.UpdateOneAsync(userFilter, userUpdate);

            var postCollection = MongoWrapper.Database.GetCollection<Models.Post>(nameof(Models.Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Models.Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(p => p.Poster._id, oldUser._id),
                GeneralUtils.NotDeactivated(postFilterBuilder)
            );
            var postUpdateBuilder = new UpdateDefinitionBuilder<Models.Post>();
            var postUpdate = postUpdateBuilder.Set(p => p.Poster.Avatar, newUser.Avatar)
                                              .Set(p => p.Poster.FullName, newUser.FullName);

            var commentFilterBuilder = new FilterDefinitionBuilder<Comment>();
            var commentFilter = commentFilterBuilder.Eq(c => c._id, oldUser._id);
            var postCommentFilterBuilder = new FilterDefinitionBuilder<Models.Post>();
            var postCommentFilter = postFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(postFilterBuilder),
                postCommentFilterBuilder.ElemMatch(p => p.Comments, commentFilter)
            );
            var postCommentUpdateBuilder = new UpdateDefinitionBuilder<Models.Post>();
            var postCommentUpdate = postUpdateBuilder
                                             .Set(p => p.Comments[-1].Commenter.Avatar, newUser.Avatar)
                                             .Set(p => p.Comments[-1].Commenter.FullName, newUser.FullName);

            var postUpdateTask = postCollection.UpdateManyAsync(postFilter, postUpdate);

            await userUpdateTask;
            await postUpdateTask;

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Usuário editado com sucesso!",
            };
        }

        protected abstract Task<TEntity> BindUser(TBody requestBody, DateTime creationDate);

        protected abstract Task<UpdateDefinition<TEntity>> CreateUpdateDefinition(TEntity oldUser, TEntity newUser);
    }
}
 
 