using System;
using NAudio.Flac;
using NAudio.Lame;
using NAudio.Vorbis;
using NAudio.Wave;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel.DataAnnotations;

namespace AudioHandler
{
    class Program
    {
        static void Main(string[] args)
        {
            string id3Title = Environment.GetEnvironmentVariable("FFM_ID3_TITLE");
            string id3Author = Environment.GetEnvironmentVariable("FFM_ID3_AUTHOR");
            string extension = Environment.GetEnvironmentVariable("FFM_EXTENSION");

            AudioFormat audioFormat = AudioFormatFromExtension(extension);

            WaveStream readerStream = ReaderForAudioFormat(Console.OpenStandardInput(), audioFormat);

            var fileWriter = new LameMP3FileWriter(Console.OpenStandardOutput(), readerStream.WaveFormat, 320, new ID3TagData
            {
                Title = id3Title,
                Artist = id3Author,
            });

            readerStream.CopyTo(fileWriter);
        }

        private static WaveStream ReaderForAudioFormat(Stream file, AudioFormat audioFormat)
        {
            WaveStream readerStream;
            switch (audioFormat)
            {
                case AudioFormat.Mp3:
                    readerStream = new Mp3FileReader(file);
                    break;
                case AudioFormat.Wave:
                    readerStream = new WaveFileReader(file);
                    if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm && readerStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                    {
                        readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
                        readerStream = new BlockAlignReductionStream(readerStream);
                    }
                    break;
                case AudioFormat.Aiff:
                    readerStream = new AiffFileReader(file);
                    break;
                case AudioFormat.Flac:
                    readerStream = new FlacReader(file);
                    break;
                case AudioFormat.OggVorbis:
                    readerStream = new VorbisWaveReader(file);
                    break;
                case AudioFormat.Other:
                default:
                    var tempPath = Path.GetTempFileName();
                    File.WriteAllBytes(tempPath, new BinaryReader(file).ReadBytes((int) file.Length));
                    readerStream = new MediaFoundationReader(tempPath);
                    break;
            }

            return readerStream;
        }

        private static AudioFormat AudioFormatFromExtension(string fileName)
        {
            // TODO
            return AudioFormat.Other;
        }

        enum AudioFormat
        {
            Other = 0,
            [Display(ShortName = "wav")]
            Wave = 1,
            [Display(ShortName = "mp3")]
            Mp3 = 2,
            [Display(ShortName = "aif")]
            Aiff = 3,
            [Display(ShortName = "ogg")]
            OggVorbis = 4,
            [Display(ShortName = "flac")]
            Flac = 5,
        }
    }
}
