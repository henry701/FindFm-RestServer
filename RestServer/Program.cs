using System;
using System.Linq;
using RestServer.Model.Config;
using RestServer.Util;
using NLog;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Infrastructure.Shell;
using NLog.Web;
using Microsoft.Extensions.Logging;
using LiterCast;
using RestServer.ShellSupport;
using System.Threading;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using LiterCast.AudioSources;
using System.Collections.Generic;
using Models;
using RestServer.Controllers.Other;
using RestServer.Util.Extensions;
using Newtonsoft.Json;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace RestServer
{
    public sealed class Program
    {
        private static readonly NLog.ILogger LOGGER = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Invokes <see cref="WrappedMainAsync"/> inside a Try block for error handling and logging at Fatal level
        /// </summary>
        /// <param name="args">The arguments received via command line.</param>
        /// <returns>The exit code for the program</returns>
        public static async Task Main(string[] args)
        {
            try
            {
                int exitCode = await WrappedMain(args);
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                LOGGER.Fatal(e);
                if (Environment.UserInteractive)
                {
                    Console.Beep();
                    Console.Out.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                }
                Environment.Exit(Environment.ExitCode != 0 ? Environment.ExitCode : -1);
            }
        }

        /// <summary>
        /// The actual main logic of the application, which loads the configuration, creates the
        /// connection with MongoDB, and calls <see cref="StartServerAsync(SharedData)"/> to start the server.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task<int> WrappedMain(string[] args)
        {
            LOGGER.Info("Starting the Application");

            string configPath = args.ElementAtOrDefault(0);
            ServerConfiguration config = GeneralUtils.ReadConfiguration(configPath);
            string hostUri = $"http://{config.Listening.Address}:{config.Listening.Port}{config.Listening.BasePath}";
            var serverInfo = new ServerInfo(configPath, hostUri);

            MongoWrapper mongoWrapper = CreateMongoWrapper(config.Mongodb);

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            LOGGER.Info("Entering main program logic");

            await StartServer(config, serverInfo, mongoWrapper);

            LOGGER.Info("Exiting program");

            return 0;
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            LOGGER.Error(e.Exception, "Unhandled Task exception!");
        }

        /// <summary>
        /// Creates the <see cref="MongoWrapper"/> using the provided configuration.
        /// </summary>
        /// <param name="config">The configuration parameters for MongoDB.</param>
        /// <returns>The wrapper class for MongoDB</returns>
        private static MongoWrapper CreateMongoWrapper(MongodbConfiguration config)
        {
            LOGGER.Info("Connecting to MongoDB...");
            MongoWrapper mongoWrapper;
            try
            {
                mongoWrapper = new MongoWrapper(config.MongoConnectionString, config.MongoDatabase);
            }
            catch (Exception e)
            {
                throw new ApplicationException("Error while connecting to MongoDB", e);
            }
            return mongoWrapper;
        }

        /// <summary>
        /// Method that contains initialization for all the pieces of the server application.
        /// </summary>
        /// <param name="sharedData"></param>
        private static async Task StartServer(ServerConfiguration serverConfig, ServerInfo serverInfo, MongoWrapper mongoWrapper)
        {
            var serverController = new ServerController(
                configurationReloader: () =>
                {
                    serverConfig = GeneralUtils.ReadConfiguration(serverInfo.ConfigurationPath);
                }
            );

            (Task hostTask, IWebHost host) = StartHost(serverConfig, serverInfo, mongoWrapper);

            (Task radioTask, RadioCastServer radioCastServer) = StartRadio(serverConfig.Radio);

            (Task shellTask, FmShell.Shell shell) = StartShell(serverConfig, serverInfo, serverController, radioCastServer);

            Task radioFeeder = StartRadioFeeder(radioCastServer, mongoWrapper);

            await AwaitTermination(shellTask, shell, hostTask, host, radioFeeder);

            radioCastServer.Dispose();
        }

        private static Task StartRadioFeeder(RadioCastServer radioCastServer, MongoWrapper mongoWrapper)
        {
            var userCollection = mongoWrapper.Database.GetCollection<Models.Musician>(nameof(Models.User));

            var songSortBuilder = new SortDefinitionBuilder<ProjectedMusicianSong>();
            var songSort = songSortBuilder
                .Descending(nameof(ProjectedMusicianSong.Score));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();
            var userFilter = userFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.AnyEq("_t", nameof(Models.Musician))
            );

            var songFilterBuilder = new FilterDefinitionBuilder<ProjectedMusicianSong>();
            var songFilter = songFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(songFilterBuilder, s => s.Song),
                songFilterBuilder.Eq(s => s.Song.RadioAuthorized, true),
                songFilterBuilder.Eq(s => s.Song.Original, true)
            );

            var projectionBuilder = new ProjectionDefinitionBuilder<Models.Musician>();
            var projection = projectionBuilder.Include(m => m.FullName).Include(m => m.Songs);

            var fsBucket = new GridFSBucket<ObjectId>(mongoWrapper.Database);

            var trackHistory = new List<(IAudioSource, ProjectedMusicianSong)>();

            var onTrackChangedTE = new ManualResetEvent(true);

            radioCastServer.OnTrackChanged += async (s, e) =>
            {
                List<(IAudioSource, ProjectedMusicianSong)> myTrackHistory;
                lock (trackHistory)
                {
                    myTrackHistory = trackHistory.ToList();
                }

                RadioInfoController.CurrentSong = myTrackHistory.Where(th => th.Item1.Equals(e.NewTrack)).Select(th => th.Item2).LastOrDefault();
                LOGGER.Info("Now playing: {}", JsonConvert.SerializeObject(RadioInfoController.CurrentSong));

                onTrackChangedTE.Set();

                if (e.OldTrack == null)
                {
                    return;
                }

                var oldTrack = myTrackHistory.Where(th => th.Item1.Equals(e.OldTrack)).Select(th => th.Item2).LastOrDefault();
                var oldMusicianId = oldTrack._id;
                var oldTrackId = oldTrack.Song._id;

                var musSongFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();

                var musSongFilter = musSongFilterBuilder.And
                (
                    musSongFilterBuilder.Eq(m => m._id, oldMusicianId),
                    musSongFilterBuilder.ElemMatch(m => m.Songs, sg => sg._id == oldTrackId)
                );
                var musSongUpdate = new UpdateDefinitionBuilder<Models.Musician>()
                    .Inc($"{nameof(Models.Musician.Songs)}.$.{nameof(Song.TimesPlayedRadio)}", 1);

                await userCollection.UpdateOneAsync(musSongFilter, musSongUpdate);

                // Remove oldest, only keep 5 in history
                if (myTrackHistory.Count > 5)
                {
                    lock (trackHistory)
                    {
                        trackHistory.RemoveAt(0);
                    }
                }
            };

            return Task.Run(async () =>
            {
                while (true)
                {
                    List<(IAudioSource, ProjectedMusicianSong)> myTrackHistory;
                    lock (trackHistory)
                    {
                        myTrackHistory = trackHistory.ToList();
                    }

                    var lookupStageRandomArr = $@"
                    {{
                        $lookup:
                        {{
                            from: ""randomNumbers"",
                            pipeline:
                            [
                                {{ $sample: {{ size: 2 }} }}
                            ],
                            as: ""RandomArr""
                        }}
                    }}
                    ";

                    // OK Vezes totais que a música foi tocada * 0.5
                    // OK Vezes totais que a música foi tocada na rádio * -1
                    // OK Se música está presente na lista das últimas 5 tocadas, -100 
                    // OK Se o autor da música está presente na lista das últimas 5 tocadas, -50
                    // OK Pontuação aleatória para cada música, entre - 10 e + 10
                    // OK Número de dias desde o cadastramento da música * -1 
                    // OK   Há uma chance de 5% de multiplicar a pontuação resultante por -1 (efeito nostalgia)

                    var scoreStage = $@"
                    {{
                        $addFields:
                        {{
                            ""Score"":
                            {{
                                $add:
                                [
                                    {{
                                        $multiply: [ ""$Song.timesPlayed"", 0.5 ]
                                    }},
                                    {{
                                        $multiply: [ ""$Song.timesPlayedRadio"", -1 ]
                                    }},
                                    {{
                                        $cond:
                                        {{
                                            if:
                                            {{
                                                $in: [ ""$_id"", [ {myTrackHistory.Select(th => $"ObjectId(\"{th.Item2._id.ToString()}\")").DefaultIfEmpty("").Aggregate((s1, s2) => $"{s1.TrimEnd(',')},{s2.TrimEnd(',')},").TrimEnd(',')} ] ]
                                            }},
                                            then: -50,
                                            else: 0
                                        }}
                                    }},
                                    {{
                                        $cond:
                                        {{
                                            if:
                                            {{
                                                $in: [ ""$Song._id"", [ {myTrackHistory.Select(th => $"ObjectId(\"{th.Item2.Song._id.ToString()}\")").DefaultIfEmpty("").Aggregate((s1, s2) => $"{s1.TrimEnd(',')},{s2.TrimEnd(',')},").TrimEnd(',')} ] ]
                                            }},
                                            then: -100,
                                            else: 0
                                        }}
                                    }},
                                    {{
                                        $add:
                                        [ 
                                            {{
                                                $multiply:
                                                [
                                                    {{ $toDecimal: {{ $arrayElemAt: [""$RandomArr.decimal"", 0] }} }},
                                                    21
                                                ]
                                            }}, 
                                            -10
                                        ]
                                    }},
                                    {{
                                        $multiply:
                                        [
                                            {{
                                                $divide:
                                                [
                                                    {{
                                                        $subtract:
                                                        [
                                                            {{ $toDate: ""{DateTime.UtcNow.ToString("o")}"" }},
                                                            {{ $toDate: ""$Song._id"" }}
                                                        ]
                                                    }},
                                                    NumberLong(""86400000"")
                                                ]
                                            }},
                                            {{
                                                $cond:
                                                {{
                                                    if:
                                                    {{
                                                        $gt:
                                                        [
                                                            {{ $toDecimal: {{ $arrayElemAt: [""$RandomArr.decimal"", 1] }} }},
                                                            NumberDecimal(""0.1"")
                                                        ]
                                                    }},
                                                    then: -1,
                                                    else: 1
                                                }}
                                            }}
                                        ]
                                    }}
                                ]
                            }}
                        }}
                    }}";

                    LOGGER.Info("Score stage generated MongoDB query: {}", scoreStage);

                    var pipeline = PipelineDefinitionBuilder
                    .For<Models.Musician>()
                    .Match(userFilter)
                    .Unwind(m => m.Songs, new AggregateUnwindOptions<Models.Musician>
                    {
                        PreserveNullAndEmptyArrays = false,
                        IncludeArrayIndex = null,
                    })
                    .Project(m => new ProjectedMusicianSong
                    {
                        _id = m._id,
                        Song = (Song) m.Songs,
                        Score = 1,
                    })
                    .Match(songFilter)
                    .AppendStage<Musician, ProjectedMusicianSong, ProjectedMusicianSong>(lookupStageRandomArr)
                    .AppendStage<Musician, ProjectedMusicianSong, ProjectedMusicianSong>(scoreStage)
                    .Sort(songSort)
                    .Limit(1);

                    if(LOGGER.IsDebugEnabled)
                    {
                        LOGGER.Debug("Pipeline generated MongoDB query for song: {}", pipeline.ToString());
                    }

                    var findTask = userCollection.AggregateAsync(pipeline, new AggregateOptions
                    {
                        AllowDiskUse = true,
                        BatchSize = 1,
                        UseCursor = true,
                        Comment = "Radio Aggregate Query",
                        TranslationOptions = new ExpressionTranslationOptions
                        {
                            StringTranslationMode = AggregateStringTranslationMode.CodePoints
                        }
                    });

                    var findResult = await findTask;
                    var firstSong = findResult.SingleOrDefault();
                    // If no songs, wait a minute before checking again
                    // Mostly not to strain the CPU on development environments
                    if (firstSong?.Song == null)
                    {
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                        continue;
                    }
                    LOGGER.Info("Next selected song: {}", JsonConvert.SerializeObject(firstSong));
                    var audioRef = firstSong.Song.AudioReference;
                    var gridId = audioRef._id;
                    var fileStreamTask = fsBucket.OpenDownloadStreamAsync(gridId, new GridFSDownloadOptions
                    {
                        Seekable = true,
                        CheckMD5 = false,
                    });
                    var audioSource = new Mp3FileAudioSource(await fileStreamTask, firstSong.Song.Name);
                    // Wait for the radio to need more songs before we add the track we have on our hands
                    while (radioCastServer.TrackCount > 1)
                    {
                        onTrackChangedTE.WaitOne();
                        onTrackChangedTE.Reset();
                    }
                    lock (trackHistory)
                    {
                        trackHistory.Add((audioSource, firstSong));
                    }
                    radioCastServer.AddTrack(audioSource);
                }
            });
        }

        private static (Task radioTask, RadioCastServer radioCastServer) StartRadio(RadioCasterConfiguration radioCfg)
        {
            var radioCastServer = new RadioCastServer(new IPEndPoint(IPAddress.Parse(radioCfg.Address), radioCfg.Port), new RadioInfo(radioCfg.IcyMetadataInterval));
            var radioTask = radioCastServer.Start();
            return (radioTask, radioCastServer);
        }

        private static async Task AwaitTermination(Task shellTask, FmShell.Shell shell, Task hostTask, IWebHost host, Task radioFeeder)
        {
            Task.WaitAny(shellTask, hostTask, radioFeeder);

            if (shellTask.IsCompleted)
            {
                LOGGER.Info("Shell has been shutdown. Shutting down the Host as well. Timeout: 1 minute");
                var beforeStop = DateTime.Now;
                await host.StopAsync(TimeSpan.FromMinutes(1));
                var afterStop = DateTime.Now;
                LOGGER.Info("Host has been stopped. Time taken: {}", afterStop - beforeStop);
                if (shellTask.Exception?.InnerExceptions?.Count > 0)
                {
                    throw shellTask.Exception;
                }
            }
            else if (hostTask.IsCompleted)
            {
                LOGGER.Info("Host has been shutdown. Shutting down the Shell as well.");
                shell.Stop();
                LOGGER.Info("Shell has been stopped.");
                if (hostTask.Exception?.InnerExceptions?.Count > 0)
                {
                    throw hostTask.Exception;
                }
            }
            else if (radioFeeder.IsCompleted)
            {
                LOGGER.Info("Radio has been shutdown. Shutting down server and shell as well.");
                await host.StopAsync(TimeSpan.FromMinutes(1));
                shell.Stop();
                if (radioFeeder.Exception?.InnerExceptions?.Count > 0)
                {
                    throw radioFeeder.Exception;
                }
            }
        }

        private static (Task task, IWebHost host) StartHost(ServerConfiguration serverConfig, ServerInfo serverInfo, MongoWrapper mongoWrapper)
        {
            IWebHost host = BuildHost(serverConfig, serverInfo, mongoWrapper);
            LOGGER.Info("Starting HTTP Host...");
            var hostTask = host.RunAsync();
            LOGGER.Info("HTTP host is running on {}", serverInfo.HostUri);
            return (hostTask, host);
        }

        private static (Task task, FmShell.Shell shell) StartShell(ServerConfiguration serverConfig, ServerInfo serverInfo, ServerController serverController, RadioCastServer radioCastServer)
        {
            LOGGER.Info("Starting Shell...");
            var shell = new FmShell.Shell(new ShellMethods(serverInfo, serverController, radioCastServer), serverConfig.ConsolePrompt, "FindFM", ConsoleColor.Black, ConsoleColor.Green);
            var shellTask = new Task(() => shell.Start());
            shellTask.Start();
            return (shellTask, shell);
        }

        private static IWebHost BuildHost(ServerConfiguration serverConfig, ServerInfo serverInfo, MongoWrapper mongoWrapper)
        {
            return new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseWebRoot(Path.Combine(Directory.GetCurrentDirectory(), "webroot"))
                .SuppressStatusMessages(true)
                .UseEnvironment(EnvironmentName.Development)
                .CaptureStartupErrors(false)
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                })
                .UseNLog(NLogAspNetCoreOptions.Default)
                .UseKestrel((builderContext, options) =>
                {
                    // Configure Kestrel Options and Builder Context here
                    options.AddServerHeader = false;
                    // Set because Kestrel sometimes does sync writes, unfortunately
                    options.AllowSynchronousIO = true;
                    options.ApplicationSchedulingMode = SchedulingMode.ThreadPool;
                    options.Listen(IPAddress.Parse(serverConfig.Listening.Address), serverConfig.Listening.Port, listenOptions =>
                    {
                        // Configure Listen Options here
                        listenOptions.NoDelay = true;
                    });
                })
                .UseLibuv(options =>
                {
                    options.ThreadCount = Environment.ProcessorCount * 2;
                })
                .ConfigureServices(services =>
                {
                    // Configure dependency injection (services) for the Startup class here
                    services.AddSingleton(mongoWrapper);
                    services.AddSingleton(serverInfo);
                    // We act as a cache for the ServerConfiguration object, invoking this func on every request for it.
                    services.AddScoped(provider => serverConfig);
                    services.AddScoped(provider => provider.GetRequiredService<ServerConfiguration>().Smtp);
                    // services.AddScoped(provider => provider.GetRequiredService<ServerConfiguration>().XX);
                })
                .UseStartup<RestServerStartup>()
                .Build();
        }

        internal class ProjectedMusicianSong
        {
            public ObjectId _id { get; set; }
            public Song Song { get; set; }
            public decimal Score { get; set; }
        }
    }
}