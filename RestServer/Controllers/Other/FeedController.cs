using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.Other
{
    [Route("/feed")]
    [Controller]
    internal sealed class FeedController : ControllerBase
    {
        private readonly ILogger<FeedController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public FeedController(MongoWrapper mongoWrapper, ILogger<FeedController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(FeedController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<dynamic> Get()
        {
            var (metaPhrase, trackedPositionEntity) = await ComputeDataForUser();

            // Ignore location in calculation if it's older than one day
            if(trackedPositionEntity?.ModifiedDate - DateTime.UtcNow > TimeSpan.FromDays(1))
            {
                trackedPositionEntity = null;
            }

            var postsTask = FetchPosts(metaPhrase, trackedPositionEntity?.Entity);
            var adsTask = FetchAds(metaPhrase, trackedPositionEntity?.Entity);

            var posts = await postsTask;
            var ads = await adsTask;

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Feed atualizado com sucesso!",
                Data = new
                {
                    postagens = posts.Select(post => post.BuildPostResponse()),
                    anuncios = ads.Select(ad => ad.BuildAdResponse()),
                }
            };
        }

        private async Task<IEnumerable<T>> FetchEntities<T>
        (
            string metaPhrase,
            ProjectionDefinition<T> projection,
            IMongoCollection<T> collection,
            double daysTolerance,
            int limit,
            GeoJsonPoint<GeoJson3DGeographicCoordinates> targetPosition = null
        ) where T : IIdentifiable<ObjectId>, ILocatable, IMetaScored
        {
            var referenceDate = DateTime.UtcNow;

            var filterBuilder = new FilterDefinitionBuilder<T>();
            var filter = filterBuilder.And
            (
                filterBuilder.Gt
                (
                    post => post._id,
                    new ObjectId(referenceDate.Subtract(TimeSpan.FromDays(daysTolerance)), 0, 0, 0)
                ),
                filterBuilder.Or
                (
                    filterBuilder.Text(metaPhrase, new TextSearchOptions
                    {
                        CaseSensitive = false,
                        DiacriticSensitive = false,
                    })
                    ,
                    filterBuilder.Exists(p => p._id)
                )
            );

            var sortBuilder = new SortDefinitionBuilder<T>();
            var sort = sortBuilder.Combine
            (
                sortBuilder.MetaTextScore("metaScore"),
                sortBuilder.Descending(p => p._id)
            );

            var cursor = await collection.FindAsync(filter, new FindOptions<T>
            {
                AllowPartialResults = true,
                Limit = limit,
                Sort = sort,
                Projection = projection
            });

            var enumerable = cursor.ToEnumerable();

            if (targetPosition != null)
            {
                enumerable = enumerable.OrderBy
                (
                    item =>
                        item.Position.Coordinates.ToGeoCoordinate().GetDistanceTo(targetPosition.Coordinates.ToGeoCoordinate())
                        -
                        Math.Pow(item.MetaScore, 2)
                );
            }

            return enumerable;
        }

        private async Task<IEnumerable<MetascoredPost>> FetchPosts(string metaPhrase, GeoJsonPoint<GeoJson3DGeographicCoordinates> position = null)
        {
            var postProjectionBuilder = new ProjectionDefinitionBuilder<MetascoredPost>();
            var postProjection = postProjectionBuilder
                .MetaTextScore(nameof(MetascoredPost.MetaScore).WithLowercaseFirstCharacter())
                .Include(post => post._id)
                .Include(post => post.Title)
                .Include(post => post.Text)
                .Include(post => post.Poster)
                .Include(post => post.Likes)
                .Include(post => post.FileReferences)
                .Include(post => post.Comments)
            ;

            return await FetchEntities(metaPhrase, postProjection, MongoWrapper.Database.GetCollection<MetascoredPost>(nameof(Models.Post)), 30, 10, position);
        }

        private async Task<IEnumerable<MetascoredAdvertisement>> FetchAds(string metaPhrase, GeoJsonPoint<GeoJson3DGeographicCoordinates> position = null)
        {
            var adProjectionBuilder = new ProjectionDefinitionBuilder<MetascoredAdvertisement>();
            var adProjection = adProjectionBuilder
                .MetaTextScore(nameof(MetascoredAdvertisement.MetaScore).WithLowercaseFirstCharacter())
                .Include(ad => ad._id)
                .Include(ad => ad.Title)
                .Include(ad => ad.Text)
                .Include(ad => ad.Poster)
                .Include(ad => ad.FileReferences)
            ;

            return await FetchEntities(metaPhrase, adProjection, MongoWrapper.Database.GetCollection<MetascoredAdvertisement>(nameof(Models.Advertisement)), 100, 5, position);
        }

        private async Task<(string, TrackedEntity<GeoJsonPoint<GeoJson3DGeographicCoordinates>>)> ComputeDataForUser()
        {
            var userId = this.GetCurrentUserId();

            if(userId == null)
            {
                return ("", null);
            }

            var userCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(Models.User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.User>();
            var userFilter = userFilterBuilder.Eq(u => u._id, new ObjectId(userId));

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<Models.User>
            {
                AllowPartialResults = true,
                Limit = 1
            })).SingleOrDefault();

            if(user == null)
            {
                return ("", null);
            }

            var phrase = string.Empty;

            if (!string.IsNullOrWhiteSpace(user.About))
            {
                phrase += user.About.Replace("\"", "");
            }

            if (user is Musician musician)
            {
                phrase += musician.InstrumentSkills?.Select
                (
                    kvp => kvp.Key.GetAttribute<DisplayAttribute>().Name
                )
                .Aggregate
                (
                    string.Empty,
                    (s1, s2) => s1 + " " + s2
                );
            }

            return (phrase, user.TrackedPosition);
        }

        private class MetascoredPost : Models.Post, IMetaScored
        {
            public double MetaScore { get; set; }
        }

        private class MetascoredAdvertisement : Models.Advertisement, IMetaScored
        {
            public double MetaScore { get; set; }
        }

        private interface IMetaScored
        {
            double MetaScore { get; }
        }
    }
}