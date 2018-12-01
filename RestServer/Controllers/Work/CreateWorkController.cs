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

            var autorCollection = MongoWrapper.Database.GetCollection<Musician>(nameof(User));

            var autorFilterBuilder = new FilterDefinitionBuilder<Musician>();
            var autorFilter = autorFilterBuilder.And(
                GeneralUtils.NotDeactivated(autorFilterBuilder),
                autorFilterBuilder.Eq(u => u._id, userId)
            );

            var userTask = autorCollection.FindAsync(autorFilter, new FindOptions<Musician>
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

            var musicianFilterBuilder = new FilterDefinitionBuilder<Models.User>();
            var musicianFilter = musicianFilterBuilder.And(
                GeneralUtils.NotDeactivated(musicianFilterBuilder),
                musicianFilterBuilder.Eq(u => u._id, userId)
            );

            var musicianCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(User));

            List<(Models.User, Func<Task>)> musicians = new List<(Models.User, Func<Task>)>();
            Task<(Models.User, Func<Task>)> musiciansTask = Task.FromResult<(Models.User, Func<Task>)>((null, () => Task.CompletedTask));
            if (requestBody.Musicos != null)
            {
                foreach (UserModelRequest musicianRequest in requestBody.Musicos)
                {
                    if (musicianRequest.Id != null)
                    {
                        musicianFilterBuilder = new FilterDefinitionBuilder<Models.User>();
                        musicianFilter = musicianFilterBuilder.And
                        (
                            musicianFilterBuilder.Eq(u => u._id, new ObjectId(musicianRequest.Id)),
                            GeneralUtils.NotDeactivated(musicianFilterBuilder)
                        );

                        var musician = (await musicianCollection.FindAsync(musicianFilter, new FindOptions<Models.User>
                        {
                            Limit = 1,
                        })).SingleOrDefault();

                        Musician simpleMusician = new Musician
                        {
                            _id = musician._id,
                            FullName = musician.FullName,
                            Avatar = musician.Avatar,
                            About = musician.About
                        };                    

                        var (fileReference, expirer) = await musiciansTask;
                        musicians.Add((simpleMusician, expirer));
                    }
                }
            }
            //TODO: Buscar dentro do perfil, sei la
            var musicsCollection = MongoWrapper.Database.GetCollection<Models.Song>(nameof(Models.Song));

            List<(Models.Song, Func<Task>)> songList = new List<(Models.Song, Func<Task>)>();
            Task<(Models.Song, Func<Task>)> songListTask = Task.FromResult<(Models.Song, Func<Task>)>((null, () => Task.CompletedTask));
            if (requestBody.Musicos != null)
            {
                foreach (MusicRequest songRequest in requestBody.Musicas)
                {
                    if (songRequest.IdResource != null)
                    {
                        var songFilterBuilder = new FilterDefinitionBuilder<Models.Song>();
                        var songFilter = songFilterBuilder.And
                        (
                            songFilterBuilder.Eq(u => u._id, new ObjectId(songRequest.IdResource)),
                            GeneralUtils.NotDeactivated(songFilterBuilder)
                        );

                        var song = (await musicsCollection.FindAsync(songFilter, new FindOptions<Models.Song>
                        {
                            Limit = 1,
                        })).SingleOrDefault();

                        var (fileReference, expirer) = await songListTask;
                        songList.Add((song, expirer));
                    }
                }
            }
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

            var userUpdateBuilder = new UpdateDefinitionBuilder<Musician>();
            var userUpdate = userUpdateBuilder.AddToSet(w => w.Works, work);

            var updateResult = await autorCollection.UpdateOneAsync(autorFilter, userUpdate);

            // Consume the file tokens
            files.AsParallel().ForAll(async f => await f.Item2());

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
