using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RestServer.Infrastructure.AspNetCore;
using RestServer.Model.Http.Response;

namespace RestServer.Util
{
    internal static class RegistrarUtils
    {
        public static async Task TestUserExists(this ControllerBase controller, IMongoCollection<User> userCollection, string email)
        {
            var filterBuilder = new FilterDefinitionBuilder<User>();
            var filter = filterBuilder.And(
                filterBuilder.Eq(u => u.Email, email),
                filterBuilder.Not(
                    filterBuilder.Exists(u => u.DeactivationDate)
                )
            );
            var existingUserCount = (await userCollection.CountDocumentsAsync(filter));

            if (existingUserCount > 0)
            {
                throw new ResultException
                (
                    new ObjectResult
                    (
                        new ResponseBody()
                        {
                            Code = ResponseCode.AlreadyExists,
                            Success = false,
                            Message = "Usuário com este e-mail já existe!",
                        }
                    ),
                    (int) HttpStatusCode.Conflict
                );
            }
        }

        public static async Task UploadPhoto(this ControllerBase controller, string foto, GridFSBucket<ObjectId> gridFsBucket, User user, DateTime creationDate)
        {
            if(String.IsNullOrWhiteSpace(foto))
            {
                user.Avatar = null;
                return;
            }
            var photo = ImageUtils.FromBytes(Convert.FromBase64String(foto));
            if (photo == null)
            {
                controller.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                throw new ResultException
                (
                    new ObjectResult
                    (
                        new ResponseBody()
                        {
                            Code = ResponseCode.InvalidImage,
                            Success = false,
                            Message = "A imagem enviada é inválida!",
                        }
                    )
                );
            }
            photo = ImageUtils.GuaranteeMaxSize(photo, 1000);
            var photoStream = ImageUtils.ToStream(photo);
            var fileId = ObjectId.GenerateNewId(creationDate);
            var metadata = new ImageMetadata
            (
                new MediaMetadata()
                {
                    ContentType = "image/jpeg",
                    MediaType = MediaType.Image
                }
            );
            user.Avatar = new ImageReference()
            {
                _id = fileId,
                MediaMetadata = metadata
            };
            Task photoTask = gridFsBucket.UploadFromStreamAsync(
                fileId,
                fileId.ToString(),
                photoStream,
                new GridFSUploadOptions()
                {
                    Metadata = metadata.ToBsonDocument()
                }
            );
            var streamCloseTask = photoTask.ContinueWith(tsk => photoStream.Close(), TaskContinuationOptions.ExecuteSynchronously);
            await photoTask;
        }
    }
}
