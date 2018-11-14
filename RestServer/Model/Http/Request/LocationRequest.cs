using GeoCoordinatePortable;
using Microsoft.AspNetCore.Mvc;
using RestServer.Infrastructure.AspNetCore;

namespace RestServer.Model.Http.Request
{
    [ModelBinder(BinderType = typeof(LocationRequestBinder))]
    internal class LocationRequest : GeoCoordinate
    {
        
    }
}
