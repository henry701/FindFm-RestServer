using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Driver;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;

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
            Task<IAsyncCursor<MetascoredPost>> postsTask = FetchPosts();
            Task<IAsyncCursor<MetascoredAdvertisement>> adsTask = FetchAds();

            var posts = await postsTask;
            var ads = await adsTask;

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Success = true,
                Message = "Feed atualizado com sucesso!",
                Data = new
                {
                    postagens = posts,
                    anuncios = ads,
                }
            };
        }

        private Task<IAsyncCursor<MetascoredPost>> FetchPosts()
        {
            var postCollection = MongoWrapper.Database.GetCollection<Post>(nameof(Post));

            var postProjectionBuilder = new ProjectionDefinitionBuilder<Post>();
            var postProjection = postProjectionBuilder
                .MetaTextScore(nameof(MetascoredPost.MetaScore))
                .Include(post => post._id)
                .Include(post => post.Title)
                .Include(post => post.Text)
                .Include(post => post.Poster)
                .Include(post => post.Likes)
                .Include(post => post.FileReferences)
                .Include(post => post.Comments)
            ;

            var postFilterBuilder = new FilterDefinitionBuilder<Post>();
            postFilterBuilder.Text("metaDadosTODO", new TextSearchOptions
            {
                CaseSensitive = false,
                DiacriticSensitive = false,
            });
            var postFilter = postFilterBuilder.Gt(
                post => post._id.CreationTime,
                DateTime.UtcNow.Subtract(TimeSpan.FromDays(7))
            );

            var postSortBuilder = new SortDefinitionBuilder<Post>();
            var postSort = postSortBuilder.Combine(
                postSortBuilder.MetaTextScore(nameof(MetascoredPost.MetaScore))
            );

            var postsTask = postCollection.FindAsync(postFilter, new FindOptions<Post, MetascoredPost>
            {
                AllowPartialResults = true,
                Limit = 10,
                Sort = postSort,
                Projection = postProjection
            });
            return postsTask;
        }

        private Task<IAsyncCursor<MetascoredAdvertisement>> FetchAds()
        {
            var adCollection = MongoWrapper.Database.GetCollection<Advertisement>(nameof(Advertisement));

            var adProjectionBuilder = new ProjectionDefinitionBuilder<Advertisement>();
            var adProjection = adProjectionBuilder
                .MetaTextScore(nameof(MetascoredPost.MetaScore))
                .Include(ad => ad._id)
                .Include(ad => ad.Title)
                .Include(ad => ad.Text)
                .Include(ad => ad.Poster)
                .Include(ad => ad.FileReference)
            ;

            var adFilterBuilder = new FilterDefinitionBuilder<Advertisement>();
            adFilterBuilder.Text("metaDadosTODO", new TextSearchOptions
            {
                CaseSensitive = false,
                DiacriticSensitive = false,
            });
            var adFilter = adFilterBuilder.Gt(
                ad => ad._id.CreationTime,
                DateTime.UtcNow.Subtract(TimeSpan.FromDays(16))
            );

            var adSortBuilder = new SortDefinitionBuilder<Advertisement>();
            var adSort = adSortBuilder.Combine(
                adSortBuilder.MetaTextScore(nameof(MetascoredPost.MetaScore))
            );

            var adsTask = adCollection.FindAsync(adFilter, new FindOptions<Advertisement, MetascoredAdvertisement>
            {
                AllowPartialResults = true,
                Limit = 1,
                Sort = adSort,
                Projection = adProjection
            });
            return adsTask;
        }

        private class MetascoredPost : Post
        {
            public int MetaScore { get; set; }
        }

        private class MetascoredAdvertisement : Advertisement
        {
            public int MetaScore { get; set; }
        }
    }
}