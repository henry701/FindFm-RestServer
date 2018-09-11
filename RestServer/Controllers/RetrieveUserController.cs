using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using RestServer.Util;
using RestServer.Util.Extensions;

namespace RestServer.Controllers
{
    [Route("/account")]
    [Controller]
    internal sealed class RetrieveUserController : ControllerBase
    {
        private readonly ILogger<RetrieveUserController> Logger;
        private readonly MongoWrapper MongoWrapper;
        private readonly ServerInfo ServerInfo;

        public RetrieveUserController(MongoWrapper mongoWrapper, ServerInfo serverInfo, ILogger<RetrieveUserController> logger)
        {
            Logger = logger;
            Logger.LogTrace($"{nameof(RetrieveUserController)} Constructor Invoked");
            MongoWrapper = mongoWrapper;
            ServerInfo = serverInfo;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<dynamic> Get(string id)
        {
            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                userFilterBuilder.Not(
                    userFilterBuilder.Exists(u => u.DeactivationDate)
                )
            );

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
            })).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Usuário não encontrado!";
                Response.StatusCode = (int) HttpStatusCode.NotFound;
                return responseBody;
            }

            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Message = "Usuário encontrado com sucesso!";
            responseBody.Data = BuildUserObject(user);

            return responseBody;
        }

        private static dynamic BuildUserObject(User user)
        {
            if(user is Musician musician)
            {
                return BuildMusicianObject(musician);
            }
            return new
            {
                usuario = new
                {
                    endereco = new
                    {
                        estado = EnumExtensions.GetAttribute<DisplayAttribute>(user.Address.State).Name,
                        rua = user.Address.Road,
                        numero = user.Address.Numeration,
                        cep = user.Address.ZipCode,
                    },
                    avatar = user.Avatar,
                    email = user.Email,
                    nomeCompleto = user.FullName,
                    telefone = user.Phone,
                    tipo = user.Kind,
                },
            };
        }

        private static dynamic BuildMusicianObject(Musician musician)
        {
            return new
            {
                usuario = new
                {
                    endereco = new
                    {
                        estado = EnumExtensions.GetAttribute<DisplayAttribute>(musician.Address.State).Name,
                        rua = musician.Address.Road,
                        numero = musician.Address.Numeration,
                        cep = musician.Address.ZipCode,
                    },
                    avatar = musician.Avatar,
                    email = musician.Email,
                    nomeCompleto = musician.FullName,
                    telefone = musician.Phone,
                    tipo = musician.Kind,
                    // -sep,
                    musicas = musician.Songs.Select(song => new
                    {
                        nome = song.Name,
                        idResource = song.AudioReference._id,
                        duracao = song.DurationSeconds,
                        autoral = song.Original,
                        autorizadoRadio = song.RadioAuthorized
                    }),
                    habilidades = musician.InstrumentSkills.ToDictionary(kv => EnumExtensions.GetAttribute<DisplayAttribute>(kv.Key).Name, kv => (int) kv.Value)
                },
            };
        }

        [HttpGet("me")]
        public async Task<dynamic> Get()
        {
            var id = this.GetCurrentUserId();

            var userCollection = MongoWrapper.Database.GetCollection<User>(nameof(User));

            var userFilterBuilder = new FilterDefinitionBuilder<User>();
            var userFilter = userFilterBuilder.And
            (
                userFilterBuilder.Eq(u => u._id, new ObjectId(id)),
                userFilterBuilder.Not(
                    userFilterBuilder.Exists(u => u.DeactivationDate)
                )
            );

            var user = (await userCollection.FindAsync(userFilter, new FindOptions<User>
            {
                Limit = 1,
            })).SingleOrDefault();

            var responseBody = new ResponseBody();

            if (user == null)
            {
                // TODO: LOG
                responseBody.Code = ResponseCode.NotFound;
                responseBody.Success = false;
                responseBody.Message = "Seu usuário não foi encontrado!";
                Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                return responseBody;
            }

            responseBody.Code = ResponseCode.GenericSuccess;
            responseBody.Success = true;
            responseBody.Message = "Usuário encontrado com sucesso!";
            responseBody.Data = responseBody.Data = BuildUserObject(user);

            return responseBody;
        }
    }
}