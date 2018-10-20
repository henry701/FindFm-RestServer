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
using System;

namespace RestServer.Controllers.Advertisement
{
    [Route("/ad/")]
    [Controller]
    internal sealed class RetrieveAdController : ControllerBase
    {
        private readonly ILogger<RetrieveAdController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public RetrieveAdController(MongoWrapper mongoWrapper, ILogger<RetrieveAdController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveAdController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<dynamic> GetById(string id)
        {
            var adCollection = MongoWrapper.Database.GetCollection<Models.Advertisement>(nameof(Models.Advertisement));

            var AdvertisementFilterBuilder = new FilterDefinitionBuilder<Models.Advertisement>();
            var AdvertisementFilter = AdvertisementFilterBuilder.And
            (
                AdvertisementFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(AdvertisementFilterBuilder)
            );

            var Advertisement = (await adCollection.FindAsync(AdvertisementFilter, new FindOptions<Models.Advertisement>
            {
                Limit = 1,
            })).SingleOrDefault();

            if (Advertisement == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new ResponseBody
                {
                    Code = ResponseCode.NotFound,
                    Success = false,
                    Message = "Anúncio não encontrado!",
                };
            }

            Models.User user = await RetrieveAuthor(Advertisement);
            EnrichAdvertisementWithAuthor(Advertisement, user);

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Advertisement encontrado com sucesso!",
                Data = ResponseMappingExtensions.BuildAdResponse(Advertisement),
            };
        }

        [AllowAnonymous]
        [HttpGet("author/{id}")]
        public async Task<dynamic> GetByAuthorId(string id)
        {
            var adCollection = MongoWrapper.Database.GetCollection<Models.Advertisement>(nameof(Models.Advertisement));

            var AdvertisementFilterBuilder = new FilterDefinitionBuilder<Models.Advertisement>();
            var AdvertisementFilter = AdvertisementFilterBuilder.And
            (
                AdvertisementFilterBuilder.Eq(p => p.Poster._id, new ObjectId(id)),
                GeneralUtils.NotDeactivated(AdvertisementFilterBuilder)
            );

            var AdvertisementSortBuilder = new SortDefinitionBuilder<Models.Advertisement>();
            var AdvertisementSort = AdvertisementSortBuilder.Descending(p => p._id);

            var Advertisements = (await adCollection.FindAsync(AdvertisementFilter, new FindOptions<Models.Advertisement>
            {
                AllowPartialResults = true,
                Sort = AdvertisementSort
            })).ToList();

            if (Advertisements.Count > 0)
            {
                Models.User user = await RetrieveAuthor(Advertisements.First());
                Advertisements.ForEach(p => EnrichAdvertisementWithAuthor(p, user));

                return new ResponseBody
                {
                    Code = ResponseCode.GenericSuccess,
                    Success = true,
                    Message = "Advertisements encontrados com sucesso!",
                    Data = Advertisements.Select(Advertisement => Advertisement.BuildAdResponse()),
                };
            }
            else
            {
                return new ResponseBody
                {
                    Code = ResponseCode.GenericSuccess,
                    Success = true,
                    Message = "Nenhum Advertisement encontrado!",
                    Data = Array.Empty<Models.Advertisement>(),
                };
            }
        }

        private void EnrichAdvertisementWithAuthor(Models.Advertisement Advertisement, Models.User user)
        {
            if (user == null)
            {
                Logger.LogWarning("Advertisement Author was not found! Advertisement id: {}, Poster id: {}", Advertisement._id, Advertisement.Poster._id);
            }
            else
            {
                Advertisement.Poster = (Contractor) user;
            }
        }

        private async Task<Models.User> RetrieveAuthor(Models.Advertisement Advertisement)
        {
            var userCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, Advertisement.Poster._id),
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
