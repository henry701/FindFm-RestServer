using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
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
            var metaPhrase = await ComputePhraseForUser();

            var postsTask = FetchPosts(metaPhrase);
            var adsTask = FetchAds(metaPhrase);

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
                    anuncios = ads,
                }
            };
        }

        private async Task<IEnumerable<MetascoredPost>> FetchPosts(string metaPhrase)
        {
            var referenceDate = DateTime.UtcNow;

            var postCollection = MongoWrapper.Database.GetCollection<Models.Post>(nameof(Models.Post));

            var postProjectionBuilder = new ProjectionDefinitionBuilder<Models.Post>();
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

            var postFilterBuilder = new FilterDefinitionBuilder<Models.Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Gt
                (
                    post => post._id,
                    new ObjectId(referenceDate.Subtract(TimeSpan.FromDays(7)), 0, 0, 0)
                ),
                postFilterBuilder.Or
                (
                    postFilterBuilder.Text(metaPhrase, new TextSearchOptions
                    {
                        CaseSensitive = false,
                        DiacriticSensitive = false,
                    })
                    ,
                    postFilterBuilder.Exists(p => p._id)
                )
            );

            var postSortBuilder = new SortDefinitionBuilder<Models.Post>();
            var postSort = postSortBuilder.Combine
            (
                postSortBuilder.MetaTextScore(nameof(MetascoredPost.MetaScore).WithLowercaseFirstCharacter()),
                postSortBuilder.Descending(p => p._id)
            );

            var postsCursor = await postCollection.FindAsync(postFilter, new FindOptions<Models.Post, MetascoredPost>
            {
                AllowPartialResults = true,
                Limit = 10,
                Sort = postSort,
                Projection = postProjection
            });

            return postsCursor.ToList();
        }

        // TODO: Make it be just like post
        private async Task<IEnumerable<MetascoredAdvertisement>> FetchAds(string metaPhrase)
        {
            var adCollection = MongoWrapper.Database.GetCollection<Models.Advertisement>(nameof(Models.Advertisement));

            var adProjectionBuilder = new ProjectionDefinitionBuilder<Models.Advertisement>();
            var adProjection = adProjectionBuilder
                .MetaTextScore(nameof(MetascoredPost.MetaScore).WithLowercaseFirstCharacter())
                .Include(ad => ad._id)
                .Include(ad => ad.Title)
                .Include(ad => ad.Text)
                .Include(ad => ad.Poster)
                .Include(ad => ad.FileReference)
            ;

            var adFilterBuilder = new FilterDefinitionBuilder<Models.Advertisement>();
            adFilterBuilder.Text(metaPhrase, new TextSearchOptions
            {
                CaseSensitive = false,
                DiacriticSensitive = false,
            });
            var adFilter = adFilterBuilder.Gt
            (
                ad => ad._id,
                new ObjectId(DateTime.UtcNow.Subtract(TimeSpan.FromDays(32)), 0, 0, 0)
            );

            var adSortBuilder = new SortDefinitionBuilder<Models.Advertisement>();
            var adSort = adSortBuilder.Combine
            (
                adSortBuilder.MetaTextScore(nameof(MetascoredPost.MetaScore).WithLowercaseFirstCharacter())
            );

            var adsTask = await adCollection.FindAsync(adFilter, new FindOptions<Models.Advertisement, MetascoredAdvertisement>
            {
                AllowPartialResults = true,
                Limit = 2,
                Sort = adSort,
                Projection = adProjection
            });

            return adsTask.ToList();
        }

        private async Task<string> ComputePhraseForUser()
        {
            var userId = this.GetCurrentUserId();

            if(userId == null)
            {
                return "";
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
                return "";
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

            return phrase.ToString();
        }

        private class MetascoredPost : Models.Post
        {
            public double MetaScore { get; set; }
        }

        private class MetascoredAdvertisement : Models.Advertisement
        {
            public double MetaScore { get; set; }
        }
    }
}