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
        public static async Task<int> Main(string[] args)
        {
            try
            {
                return await WrappedMain(args);
            }
            catch(Exception e)
            {
                LOGGER.Fatal(e);
                return Environment.ExitCode != 0 ? Environment.ExitCode : -1;
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

            LOGGER.Info("Entering main program logic");            

            await StartServer(config, serverInfo, mongoWrapper);

            LOGGER.Info("Exiting program");

            return 0;
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

            (Task shellTask, FmShell.Shell shell) = StartShell(serverConfig, serverInfo, serverController);
            (Task hostTask, IWebHost host) = StartHost(serverConfig, serverInfo, mongoWrapper);

            Task.WaitAny(shellTask, hostTask);

            if(shellTask.IsCompleted)
            {
                LOGGER.Info("Shell has been shutdown. Shutting down the Host as well. Timeout: 1 minute");
                var beforeStop = DateTime.Now;
                await host.StopAsync(TimeSpan.FromMinutes(1));
                var afterStop = DateTime.Now;
                LOGGER.Info("Host has been stopped. Time taken: {}", afterStop - beforeStop);
            }
            else if (hostTask.IsCompleted)
            {
                LOGGER.Info("Host has been shutdown. Shutting down the Shell as well.");
                shell.Stop();
                LOGGER.Info("Shell has been stopped.");
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

        private static (Task task, FmShell.Shell shell) StartShell(ServerConfiguration serverConfig, ServerInfo serverInfo, ServerController serverController)
        {
            LOGGER.Info("Starting Shell...");
            var shell = new FmShell.Shell(new Shell.ShellMethods(serverInfo, serverController), serverConfig.ConsolePrompt, "FindFM", ConsoleColor.Black, ConsoleColor.Green);
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
                    services.Add(new ServiceDescriptor(typeof(MongoWrapper), mongoWrapper));
                    services.Add(new ServiceDescriptor(typeof(ServerInfo), serverInfo));
                    // We act as a cache for the ServerConfiguration object, invoking this func on every request for it.
                    services.Add(new ServiceDescriptor(typeof(ServerConfiguration),
                        provider => serverConfig,
                        ServiceLifetime.Transient)
                    );
                })
                .UseStartup<RestServerStartup>()
                .Build();
        }
    }
}
