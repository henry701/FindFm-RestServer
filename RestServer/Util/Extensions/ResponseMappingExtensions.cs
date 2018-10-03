using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Linq;
using System.Text;
using Models;

namespace RestServer.Util.Extensions
{
    internal static class ResponseMappingExtensions
    {
        public static dynamic BuildPostResponse(this Post post)
        {
            return new
            {
                Titulo = post.Title,
                Descricao = post.Text,
                Autor = post.Poster.BuildUserObject(),
                UsuarioLikes = post.Likes,
                Criacao = post._id.CreationTime,
                Midias = post.FileReferences.Select
                    (
                        fr => new
                        {
                            Id = fr._id.ToString(),
                            TipoMidia = fr.FileMetadata.FileType.GetAttribute<DisplayAttribute>().ShortName,
                        }
                   )
            };
        }

        public static dynamic BuildUserObject(this User user)
        {
            dynamic userObj = new ExpandoObject();
            userObj.usuario = new ExpandoObject();
            userObj.usuario.id = user._id;
            if (user.Address != null)
            {
                userObj.usuario.endereco = new
                {
                    estado = EnumExtensions.GetAttribute<DisplayAttribute>(user.Address.State).Name,
                    rua = user.Address.Road,
                    numero = user.Address.Numeration,
                    cep = user.Address.ZipCode,
                    cidade = user.Address.City
                };
            }
            userObj.usuario.date = user.StartDate;
            userObj.usuario.avatar = user.Avatar;
            userObj.usuario.email = user.Email;
            userObj.usuario.fullName = user.FullName;
            userObj.usuario.telefone = user.Phone;
            userObj.usuario.Kind = user.Kind;

            if (user is Musician musician)
            {
                IncrementMusicianObject(musician, userObj);
            }
            return userObj;
        }

        private static void IncrementMusicianObject(Musician musician, dynamic userObj)
        {
            userObj.usuario.musicas = musician.Songs?.Where(s => s != null).Select(song => new
            {
                nome = song.Name,
                idResource = song.AudioReference._id,
                duracao = song.DurationSeconds,
                autoral = song.Original,
                autorizadoRadio = song.RadioAuthorized
            });
            userObj.usuario.habilidades = musician.InstrumentSkills?.ToDictionary(kv => EnumExtensions.GetAttribute<DisplayAttribute>(kv.Key).Name, kv => (int)kv.Value);
        }
    }
}
