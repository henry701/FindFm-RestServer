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
            Thread.Sleep(2000);
            Environment.Exit(-5);
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

            (Task radioTask, RadioCastServer radioCastServer) = StartRadio();

            (Task shellTask, FmShell.Shell shell) = StartShell(serverConfig, serverInfo, serverController, radioCastServer);

            Task radioFeeder = StartRadioFeeder(radioCastServer, mongoWrapper);

            await AwaitTermination(shellTask, shell, hostTask, host);

            radioCastServer.Dispose();
        }

        private static Task StartRadioFeeder(RadioCastServer radioCastServer, MongoWrapper mongoWrapper)
        {
            var songCollection = mongoWrapper.Database.GetCollection<Models.Song>(nameof(Models.Song));
            var sortBuilder = new SortDefinitionBuilder<Models.Song>();
            var sort = sortBuilder.Ascending(song => song.TimesPlayed);
            var filterBuilder = new FilterDefinitionBuilder<Models.Song>();
            var filter = filterBuilder.And
            (
                filterBuilder.Not(filterBuilder.Exists(song => song.DeactivationDate)),
                filterBuilder.Eq(song => song.RadioAuthorized, true),
                filterBuilder.Eq(song => song.Original, true)
            );
            var fsBucket = new GridFSBucket<ObjectId>(mongoWrapper.Database);
            return Task.Run(async () =>
            {
                while (true)
                {
                    var findTask = songCollection.FindAsync(filter, new FindOptions<Models.Song>
                    {
                        AllowPartialResults = true,
                        Sort = sort,
                        Limit = 1
                    });
                    var findResult = await findTask;
                    var firstSong = await findResult.FirstOrDefaultAsync();
                    // If no songs, wait a minute before checking again
                    if(firstSong == null)
                    {
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                        continue;
                    }
                    var audioRef = firstSong.AudioReference;
                    var gridId = audioRef._id;
                    var fileStreamTask = fsBucket.OpenDownloadStreamAsync(gridId);
                    // Wait for the radio to need more songs before we add the track
                    SpinWait.SpinUntil(() => radioCastServer.TrackCount <= 1);
                    radioCastServer.AddTrack(new FileAudioSource(await fileStreamTask, firstSong.Name));
                    // TODO: Increment played count by 1 here, or expose events on RadioCastServer and callback here
                }
            });
        }

        private static (Task radioTask, RadioCastServer radioCastServer) StartRadio()
        {
            var radioCastServer = new RadioCastServer(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8081), new RadioInfo());
            var radioTask = radioCastServer.Start();
            return (radioTask, radioCastServer);
        }

        private static async Task AwaitTermination(Task shellTask, FmShell.Shell shell, Task hostTask, IWebHost host)
        {
            Task.WaitAny(shellTask, hostTask);

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
