﻿using System;
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
            catch(Exception e)
            {
                LOGGER.Fatal(e);
                if(Environment.UserInteractive)
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
        /// Method that contains the <see cref="NancyHost"/> initialization and starts the <see cref="FmShell.Shell"/>.
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

            var songSortBuilder = new SortDefinitionBuilder<Models.Song>();
            var songSort = songSortBuilder.Ascending(s => s.TimesPlayedRadio).Descending(s => s.TimesPlayed);

            var userFilterBuilder = new FilterDefinitionBuilder<Models.Musician>();
            var userFilter = userFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.AnyEq("_t", nameof(Models.Musician))
            );

            var songFilterBuilder = new FilterDefinitionBuilder<Models.Song>();
            var songFilter = songFilterBuilder.And
            (
                GeneralUtils.NotDeactivated(songFilterBuilder),
                songFilterBuilder.Eq(s => s.RadioAuthorized, true),
                songFilterBuilder.Eq(s => s.Original, true)
            );

            var projectionBuilder = new ProjectionDefinitionBuilder<Models.Musician>();
            var projection = projectionBuilder.Include(m => m.FullName).Include(m => m.Songs);

            var pipeline = PipelineDefinitionBuilder
                .For<Models.Musician>()
                .Match(userFilter)
                .Unwind(m => m.Songs, new AggregateUnwindOptions<Models.Musician>
                {
                    PreserveNullAndEmptyArrays = false,
                    IncludeArrayIndex = null,
                })
                // Cast because is unwinded, single object now
                .ReplaceRoot(m => (Models.Song) m.Songs)
                .Match(songFilter)
                .Sort(songSort)
                .Limit(1);

            var fsBucket = new GridFSBucket<ObjectId>(mongoWrapper.Database);

            return Task.Run(async () =>
            {
                while (true)
                {
                    var findTask = userCollection.AggregateAsync(pipeline, new AggregateOptions
                    {
                        AllowDiskUse = false,
                        UseCursor = false,
                    });

                    var findResult = await findTask;
                    var firstSong = await findResult.SingleOrDefaultAsync();
                    // If no songs, wait a minute before checking again
                    // Mostly not to strain the CPU on development environments
                    if(firstSong == null)
                    {
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                        continue;
                    }
                    var audioRef = firstSong.AudioReference;
                    var gridId = audioRef._id;
                    var fileStreamTask = fsBucket.OpenDownloadStreamAsync(gridId, new GridFSDownloadOptions
                    {
                        Seekable = true,
                        CheckMD5 = false,
                    });
                    // Wait for the radio to need more songs before we add the track
                    SpinWait.SpinUntil(() => radioCastServer.TrackCount <= 1);
                    var audioSource = new FileAudioSource(await fileStreamTask, firstSong.Name);
                    radioCastServer.AddTrack(audioSource);
                    // TODO: Increment played count by 1 here and persist to MongoDB, or (better): 
                    // expose events on RadioCastServer and callback here
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
    }
}
