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
                Id = post._id,
                Titulo = post.Title,
                Descricao = post.Text,
                Autor = post.Poster.BuildUserResponse(),
                UsuarioLikes = post.Likes,
                Criacao = post._id.CreationTime,
                Midias = post.FileReferences.Select
                (
                    fr => new
                    {
                        Id = fr._id.ToString(),
                        TipoMidia = fr.FileInfo.FileMetadata.FileType.GetAttribute<DisplayAttribute>().ShortName,
                    }
                ),
               Comentarios = post.Comments.Select(BuildCommentResponse)
            };
        }

        public static dynamic BuildAdResponse(this Advertisement ad)
        {
            return new
            {
                Id = ad._id,
                Titulo = ad.Title,
                Descricao = ad.Text,
                Autor = ad.Poster.BuildUserResponse(),
                Criacao = ad._id.CreationTime,
                Midias = ad.FileReferences.Select
                (
                    fr => fr == null ? null : new
                    {
                        Id = fr._id.ToString(),
                        TipoMidia = fr.FileInfo.FileMetadata.FileType.GetAttribute<DisplayAttribute>().ShortName,
                    }
                )
            };
        }

        public static dynamic BuildCommentResponse(this Comment comentario)
        {
            return new
            {
                Comentador = new
                {
                    NomeCompleto = comentario.Commenter.FullName,
                    Id = comentario.Commenter._id,
                    FotoID = comentario.Commenter.Avatar?._id
                },
                Comentario = comentario.Text,
                DataComentario = comentario._id.CreationTime,
#pragma warning disable
                Likes = comentario.Likes,
#pragma warning restore
                Id = comentario._id
            };
        }

        public static dynamic BuildUserResponse(this User user)
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
            userObj.usuario.sobre = user.About;
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
            userObj.usuario.trabalhos = musician.Works?.Where(w => w != null).Select(work => new
            {
                id = work._id.ToString(),
                nome = work.Name,
                descricao = work.Description,
                original = work.Original,
                midias = work.FileReferences.Select
                (
                    fr => new
                    {
                        Id = fr._id.ToString(),
                        TipoMidia = fr.FileInfo.FileMetadata.FileType.GetAttribute<DisplayAttribute>().ShortName,
                    }
                ),
                musicas = work.Songs?.Where(s => s != null).Select(song => new
                    {
                        nome = song.Name,
                        idResource = song.AudioReference._id,
                        duracao = song.DurationSeconds,
                        autoral = song.Original,
                        autorizadoRadio = song.RadioAuthorized
                    }),
                musicos = work.RelatedMusicians.Select
                (
                    musico => new
                    {
                        Id = musico._id.ToString(),
                        Avatar = musico.Avatar,
                        FullName = musico.FullName,
                        Sobre = musico.About
        }
                ),
            });
            userObj.usuario.habilidades = musician.InstrumentSkills?.ToDictionary(kv => EnumExtensions.GetAttribute<DisplayAttribute>(kv.Key).Name, kv => (int)kv.Value);
        }
    }
}
