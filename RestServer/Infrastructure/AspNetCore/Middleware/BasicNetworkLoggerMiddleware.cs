using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Infrastructure.AspNetCore.Middleware
{
    internal sealed class BasicNetworkLoggerMiddleware
    {
        private ILogger<BasicNetworkLoggerMiddleware> Logger { get; set; }
        private RequestDelegate Next { get; set; }
        private MongoWrapper MongoWrapper { get; set; }

        public BasicNetworkLoggerMiddleware(RequestDelegate next, MongoWrapper mongoWrapper, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<BasicNetworkLoggerMiddleware>();
            Next = next;
            MongoWrapper = mongoWrapper;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var logBeforeTask = LogBefore(context);

            try
            {
                await Next(context);
            }
            finally
            {
                var logAfterTask = LogAfter(context);
                await logAfterTask.ContinueWith(task =>
                {
                    Logger.LogError(task.Exception, "NetworkLogger 'after' log failed!");
                },
                TaskContinuationOptions.OnlyOnFaulted);
            }

            await logBeforeTask.ContinueWith(task =>
            {
                Logger.LogError(task.Exception, "NetworkLogger 'before' log failed!");
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task LogBefore(HttpContext context)
        {
            var startDate = DateTime.UtcNow;
            var networkCollection = MongoWrapper.Database.GetCollection<NetworkEntry>(nameof(NetworkEntry));
            await networkCollection.InsertOneAsync
            (
                new NetworkEntry
                {
                    DateTime = startDate,
                    IPAddress = context.Connection.RemoteIpAddress,
                    Kind = NetworkEntryKind.Start,
                    _id = ObjectId.GenerateNewId(startDate)
                }
            );
        }

        private async Task LogAfter(HttpContext context)
        {
            var endDate = DateTime.UtcNow;
            var networkCollection = MongoWrapper.Database.GetCollection<NetworkEntry>(nameof(NetworkEntry));
            await networkCollection.InsertOneAsync
            (
                new NetworkEntry
                {
                    DateTime = endDate,
                    IPAddress = context.Connection.RemoteIpAddress,
                    Kind = NetworkEntryKind.End,
                    _id = ObjectId.GenerateNewId(endDate)
                }
            );
        }
    }
}
