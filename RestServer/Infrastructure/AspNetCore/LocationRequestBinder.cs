using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RestServer.Model.Http.Request;

namespace RestServer.Infrastructure.AspNetCore
{
    internal class LocationRequestBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var inputStream = bindingContext.HttpContext.Request.Body;
            var binaryReader = new BinaryReader(inputStream);

            var location = new LocationRequest
            {
                Latitude = binaryReader.ReadDouble(),
                Longitude = binaryReader.ReadDouble()
            };

            try
            {
                location.VerticalAccuracy = binaryReader.ReadDouble();
                location.HorizontalAccuracy = binaryReader.ReadDouble();

                location.Altitude = binaryReader.ReadDouble();
                location.Speed = binaryReader.ReadDouble();

                location.Course = binaryReader.ReadDouble();
            }
            catch(EndOfStreamException)
            {
                // Optional elements, no big deal
            }

            bindingContext.Result = ModelBindingResult.Success(location);

            return Task.CompletedTask;
        }
    }
}