using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

namespace RestServer.Controllers
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
        public async Task<dynamic> Get()
        {
            var postsTask = FetchPosts();
            var adsTask = FetchAds();

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

        private async Task<IEnumerable<MetascoredPost>> FetchPosts()
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var postProjectionBuilder = new ProjectionDefinitionBuilder<Post>();
            var postProjection = postProjectionBuilder
                .MetaTextScore(FirstCharacterToLower(nameof(MetascoredPost.MetaScore)))
                .Include(post => post._id)
                .Include(post => post.Title)
                .Include(post => post.Text)
                .Include(post => post.Poster)
                .Include(post => post.Likes)
                .Include(post => post.FileReferences)
                .Include(post => post.Comments)
            ;

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            var postFilter = postFilterBuilder.And
            (
                postFilterBuilder.Gt
                (
                    post => post._id,
                    // Only from the most recent seven days
                    new ObjectId(DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)), 0, 0, 0)
                ),
                postFilterBuilder.Text("postão", new TextSearchOptions
                {
                    CaseSensitive = false,
                    DiacriticSensitive = false,
                })
            );

            var postSortBuilder = new SortDefinitionBuilder<Post>();
            var postSort = postSortBuilder.Combine(
                postSortBuilder.Descending(p => p._id),
                postSortBuilder.MetaTextScore(FirstCharacterToLower(nameof(MetascoredPost.MetaScore)))
            );

            var postsTask = await postCollection.FindAsync(postFilter, new FindOptions<Post, MetascoredPost>
            {
                AllowPartialResults = true,
                Limit = 10,
                Sort = postSort,
                Projection = postProjection
            });

            var postList = postsTask.ToList();

            if (postList.Count > 0)
            {               
                postList.ForEach(async p => await GetPostAuthorAsync(p));

                return postList;
            }

            return postsTask.ToEnumerable();
        }

        private async Task<User> RetrieveAuthor(Post post)
        {
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, post.Poster._id),
                GeneralUtils.NotDeactivated(userFilterBuilder)
            );

            var userProjectionBuilder = new ProjectionDefinitionBuilder<User>();
            var userProjection = userProjectionBuilder
                .Include(m => m._id)
                .Include(m => m.FullName)
                .Include(m => m.Avatar)
                .Include(m => m.Phone)
                .Include(m => m.StartDate)
                .Include(m => m.Email)
                .Include(m => m.Address)
                .Include("_t");

            return (await userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
                AllowPartialResults = true,
                Projection = userProjection
            })).SingleOrDefault();
        }

        private async Task<IEnumerable<MetascoredAdvertisement>> FetchAds()
        {
            var adCollection = MongoWrapper.Database.GetCollection<Advertisement>(nameof(Advertisement));

            var adProjectionBuilder = new ProjectionDefinitionBuilder<Advertisement>();
            var adProjection = adProjectionBuilder
                .MetaTextScore(FirstCharacterToLower(nameof(MetascoredPost.MetaScore)))
                .Include(ad => ad._id)
                .Include(ad => ad.Title)
                .Include(ad => ad.Text)
                .Include(ad => ad.Poster)
                .Include(ad => ad.FileReference)
            ;

            var adFilterBuilder = new FilterDefinitionBuilder<Advertisement>();
            adFilterBuilder.Text("anunciozão", new TextSearchOptions
            {
                CaseSensitive = false,
                DiacriticSensitive = false,
            });
            var adFilter = adFilterBuilder.Gt(
                ad => ad._id,
                new ObjectId(DateTime.UtcNow.Subtract(TimeSpan.FromDays(16)), 0, 0, 0)
            );

            var adSortBuilder = new SortDefinitionBuilder<Advertisement>();
            var adSort = adSortBuilder.Combine(
                adSortBuilder.MetaTextScore(FirstCharacterToLower(nameof(MetascoredPost.MetaScore)))
            );

            var adsTask = await adCollection.FindAsync(adFilter, new FindOptions<Advertisement, MetascoredAdvertisement>
            {
                AllowPartialResults = true,
                Limit = 1,
                Sort = adSort,
                Projection = adProjection
            });
            return adsTask.ToEnumerable();
        }

        private async Task GetPostAuthorAsync(Post post)
        {
            User user = await RetrieveAuthor(post);
            if (user == null)
            {
                Logger.LogWarning("Post Author was not found! post id: {}, poster id: {}", post._id, post.Poster._id);
            }
            else
            {
                post.Poster = user;
            }
        }

        public static string FirstCharacterToLower(string str)
        {
            if (string.IsNullOrEmpty(str) || char.IsLower(str, 0))
            {
                return str;
            }

            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }

        private class MetascoredPost : Post
        {
            public double MetaScore { get; set; }
        }

        private class MetascoredAdvertisement : Advertisement
        {
            public double MetaScore { get; set; }
        }
    }
}