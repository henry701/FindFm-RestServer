using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Config;
using RestServer.Model.Http.Request;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers.Other
{
    [Route("/geo")]
    [Controller]
    internal sealed class GeolocationController : ControllerBase
    {
        private readonly ILogger<GeolocationController> Logger;
        private readonly MongoWrapper MongoWrapper;

        public GeolocationController(MongoWrapper mongoWrapper, ILogger<GeolocationController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(GeolocationController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
        }

        [HttpPut]
        public void Put(LocationRequest locationRequest)
        {
            var userId = new ObjectId(this.GetCurrentUserId());

            var userCollection = MongoWrapper.Database.GetCollection<Models.User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<Models.User>();
            var userFilter = userFilterBuilder.And(
                GeneralUtils.NotDeactivated(userFilterBuilder),
                userFilterBuilder.Eq(u => u._id, userId)
            );

            var userUpdateBuilder = new UpdateDefinitionBuilder<Models.User>();
            var userUpdate = userUpdateBuilder.Set
            (
                u => u.TrackedPosition,
                TrackedEntity<GeoJsonPoint<GeoJson3DGeographicCoordinates>>.From
                (
                    new GeoJsonPoint<GeoJson3DGeographicCoordinates>
                    (
                        new GeoJson3DGeographicCoordinates(locationRequest.Latitude, locationRequest.Longitude, locationRequest.Altitude)
                    )
                )
            );

            // Do not await the update before returning, only log in case of error
            var updateResult = userCollection
                .UpdateOneAsync(userFilter, userUpdate)
                .ContinueWith
                (
                    t => Logger.LogError(t.Exception, "Error while updating user location for user {}", userId),
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
                );

            Response.StatusCode = (int) HttpStatusCode.OK;
        }
    }
}