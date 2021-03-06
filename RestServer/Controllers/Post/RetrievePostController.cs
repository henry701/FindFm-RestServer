﻿using System.ComponentModel.DataAnnotations;
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
using System;

namespace RestServer.Controllers.Post
{
    [Route("/post/")]
    [Controller]
    internal sealed class RetrievePostController : ControllerBase
    {
        private readonly ILogger<RetrievePostController> Logger;
        private readonly MongoWrapper MongoWrapper;
   
        public RetrievePostController(MongoWrapper mongoWrapper, ILogger<RetrievePostController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrievePostController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<dynamic> GetById(string id)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Models.Post>(nameof(Models.Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Models.Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(postFilterBuilder)
            );

            var post = (await postCollection.FindAsync(postFilter, new FindOptions<Models.Post>
            {
                Limit = 1,
            })).SingleOrDefault();

            if (post == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Post não encontrado!",
                };
            }

            Models.User user = await RetrieveAuthor(post);
            EnrichPostWithAuthor(post, user);

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Post encontrado com sucesso!",
                Data = ResponseMappingExtensions.BuildPostResponse(post),
            };
        }

        [AllowAnonymous]
        [HttpGet("author/{id}")]
        public async Task<dynamic> GetByAuthorId(string id)
        {
            var postCollection = MongoWrapper.Database.GetCollection<Models.Post>(nameof(Models.Post));

            var postFilterBuilder = new FilterDefinitionBuilder<Models.Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Eq(p => p.Poster._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(postFilterBuilder)
            );

            var postSortBuilder = new SortDefinitionBuilder<Models.Post>();
            var postSort = postSortBuilder.Descending(p => p._id);

            var posts = (await postCollection.FindAsync(postFilter, new FindOptions<Models.Post>
            {
                AllowPartialResults = true,
                Sort = postSort
            })).ToList();

            if (posts.Count > 0)
            {
                Models.User user = await RetrieveAuthor(posts.First());
                posts.ForEach(p => EnrichPostWithAuthor(p, user));

                return new ResponseBody
                {
                    Code = ResponseCode.GenericSuccess,
                    Success = true,
                    Message = "Posts encontrados com sucesso!",
                    Data = posts.Select(post => post.BuildPostResponse()),
                };
            }
            else
            {
                return new ResponseBody
                {
                    Code = ResponseCode.GenericSuccess,
                    Success = true,
                    Message = "Nenhum Post encontrado!",
                    Data = Array.Empty<Models.Post>(),
                };
            }
        }

        private void EnrichPostWithAuthor(Models.Post post, Models.User user)
        {
            if (user == null)
            {
                Logger.LogWarning("Post Author was not found! post id: {}, poster id: {}", post._id, post.Poster._id);
            }
            else
            {
                post.Poster = user;
            }
        }

        private async Task<Models.User> RetrieveAuthor(Models.Post post)
        {
            var userCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, post.Poster._id),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<Models.User>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include(m => m.Phone)
                .Include(m => m.StartDate)
                .Include(m => m.Email)
                .Include(m => m.Address)
                .Include("_t");

            return (await userCollection.FindAsync(userFilter, new FindOptions<Models.User>
            {
                Limit = 1,
                AllowPartialResults = true,
                Projection = userProjection
            })).SingleOrDefault();
        }
    }
}
 