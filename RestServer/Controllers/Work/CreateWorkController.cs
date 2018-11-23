using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;

namespace RestServer.Controllers.Work
{
    [Route("/work/create")]
    [Controller]
    internal sealed class CreateWorkController : ControllerBase
    {
        private readonly ILogger<CreateWorkController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public CreateWorkController(MongoWrapper mongoWrapper, ILogger<CreateWorkController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(CreateWorkController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpPost]
        public async Task<dynamic> Work([FromBody] CreateWorkRequest requestBody)
        {
            var userId = new ObjectId(this.GetCurrentUserId());

            var userCollection = MongoWrapper.Database.GetCollection<Musician>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Musician>();
            var userFilter = userFilterBuilder.And(
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(u => u._id, userId)
            );

            var userTask = userCollection.FindAsync(userFilter, new FindOptions<Musician>
            {
                AllowPartialResults = false,
                Projection = new ProjectionDefinitionBuilder<Musician>()
                    .Include(m => m.FileBytesLimit)
                    .Include(m => m.FileBytesOccupied)
                    .Include(m => m.FullName)
                    .Include("_t")
            });

            Models.User thisUser = null;

            List<(FileReference, Func<Task>)> files = new List<(FileReference, Func<Task>)>();
            Task<(FileReference, Func<Task>)> fileReferenceTask = Task.FromResult<(FileReference, Func<Task>)>((null, () => Task.CompletedTask));
            if (requestBody.Midias != null)
            {
                long totalSize = 0;
                foreach (MidiaRequest midiaRequest in requestBody.Midias)
                {
                    if (midiaRequest.Id != null)
                    {
                        fileReferenceTask = GeneralUtils.GetFileForReferenceToken
                        (
                            MongoWrapper,
                            midiaRequest.Id,
                            userId
                        );
                        var (fileReference, expirer) = await fileReferenceTask;
                        totalSize += fileReference.FileInfo.Size;
                        files.Add((fileReference, expirer));
                    }
                }
                thisUser = (await userTask).Single();
                GeneralUtils.CheckSizeForUser(totalSize, thisUser.FileBytesOccupied, thisUser.FileBytesLimit);
            }

            List<(Models.User, Func<Task>)> musicians = new List<(Models.User, Func<Task>)>();
            Task<(Models.User, Func<Task>)> musiciansTask = Task.FromResult<(Models.User, Func<Task>)>((null, () => Task.CompletedTask));
            if (requestBody.Musicos != null)
            {
                foreach (UserModelRequest musicianRequest in requestBody.Musicos)
                {
                    if (musicianRequest.Id != null)
                    {
                        userFilterBuilder = new FilterDefinitionBuilder<Musician>();
                        userFilter = userFilterBuilder.And
                        (
                            userFilterBuilder.Eq(u => u._id, new ObjectId(musicianRequest.Id)),
                            GeneralUtils.NotDeactivated(userFilterBuilder)
                        );

                        var musician = (await userCollection.FindAsync(userFilter, new FindOptions<Musician>
                        {
                            Limit = 1,
                        })).SingleOrDefault();

                        var (fileReference, expirer) = await musiciansTask;
                        musicians.Add((musician, expirer));
                    }
                }
            }

            var musicsCollection = MongoWrapper.Database.GetCollection<Models.Song>(nameof(Models.Song));

            List<(Models.Song, Func<Task>)> songList = new List<(Models.Song, Func<Task>)>();
            Task<(Models.Song, Func<Task>)> songListTask = Task.FromResult<(Models.Song, Func<Task>)>((null, () => Task.CompletedTask));
            if (requestBody.Musicos != null)
            {
                foreach (MusicRequest songRequest in requestBody.Musicas)
                {
                    if (songRequest.Id != null)
                    {
                        var songFilterBuilder = new FilterDefinitionBuilder<Models.Song>();
                        var songFilter = songFilterBuilder.And
                        (
                            songFilterBuilder.Eq(u => u._id, new ObjectId(songRequest.Id)),
                            GeneralUtils.NotDeactivated(songFilterBuilder)
                        );

                        var song = (await musicsCollection.FindAsync(songFilter, new FindOptions<Models.Song>
                        {
                            Limit = 1,
                        })).SingleOrDefault();

                        var (fileReference, expirer) = await musiciansTask;
                        songList.Add((song, expirer));
                    }
                }
            }

            var workCollection = MongoWrapper.Database.GetCollection<Models.Work>(nameof(Models.Work));

            var creationDate = DateTime.UtcNow;

            var work = new Models.Work
            {
                _id = ObjectId.GenerateNewId(creationDate),
                Name = requestBody.Nome,
                Description = requestBody.Descricao,
                FileReferences = files.Select(f => f.Item1).ToList(),
                Original = requestBody.Original,
                Songs = songList.Select(s => s.Item1).ToList(),
                RelatedMusicians = musicians.Select(m => m.Item1).ToList(),
            };

            await workCollection.InsertOneAsync(work);

            // Consume the file tokens
            files.AsParallel().ForAll(async f => await f.Item2());

            var userUpdateBuilder = new UpdateDefinitionBuilder<Musician>();
            var userUpdate = userUpdateBuilder.AddToSet(w => w.Works, work);

            var updateResult = await userCollection.UpdateOneAsync(userFilter, userUpdate);

            return new ResponseBody
            {
                Code = ResponseCode.GenericSuccess,
                Data = work._id,
                Message = "Trabalho criado com sucesso!",
                Success = true
            };
        }
    }
}
