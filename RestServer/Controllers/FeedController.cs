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
                    postagens = posts.Select(RetrievePostController.BuildPostResponse),
                    anuncios = ads,
                }
            };
        }

        private async Task<IEnumerable<MetascoredPost>> FetchPosts()
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
                post => post._id,
                new ObjectId(DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)), 0, 0, 0)
            );

            var postSortBuilder = new SortDefinitionBuilder<Post>();
            var postSort = postSortBuilder.Combine(
                postSortBuilder.MetaTextScore(nameof(MetascoredPost.MetaScore))
            );

            var postsTask = await postCollection.FindAsync(postFilter, new FindOptions<Post, MetascoredPost>
            {
                AllowPartialResults = true,
                Limit = 10,
                Sort = postSort,
                Projection = postProjection
            });
            return postsTask.ToEnumerable();
        }

        private async Task<IEnumerable<MetascoredAdvertisement>> FetchAds()
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
                ad => ad._id,
                new ObjectId(DateTime.UtcNow.Subtract(TimeSpan.FromDays(16)), 0, 0, 0)
            );

            var adSortBuilder = new SortDefinitionBuilder<Advertisement>();
            var adSort = adSortBuilder.Combine(
                adSortBuilder.MetaTextScore(nameof(MetascoredPost.MetaScore))
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