using System;
using NAudio.Flac;
using NAudio.Lame;
using NAudio.Vorbis;
using NAudio.Wave;
using System.Linq;
using System.IO;
using System.ComponentModel.DataAnnotations;
using MimeDetective.Extensions;
using System.Threading;

namespace AudioHandler
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Stream sysin = new MemoryStream();
                Console.OpenStandardInput().CopyTo(sysin);

                Console.Error.WriteLine($"Copied STDIN, length {sysin.Length}");

                string id3Title = Environment.GetEnvironmentVariable("FFM_ID3_TITLE");
                string id3Author = Environment.GetEnvironmentVariable("FFM_ID3_AUTHOR");
                string mimeHint = Environment.GetEnvironmentVariable("FFM_MIME");

                string typeString = HeyRed.Mime.MimeGuesser.GuessExtension(sysin);
                if(string.Equals(typeString, "bin", StringComparison.OrdinalIgnoreCase))
                {
                    MimeDetective.FileType ft = sysin.GetFileType();
                    if(ft != null)
                    {
                        typeString = ft.Extension;
                    }
                    else
                    {
                        if(!string.IsNullOrWhiteSpace(mimeHint))
                        {
                            typeString = HeyRed.Mime.MimeTypesMap.GetExtension(mimeHint);
                            if(string.Equals(typeString, "bin", StringComparison.OrdinalIgnoreCase))
                            {
                                // TODO more guessing
                            }
                        }
                    }
                }
                Console.Error.WriteLine($"TypeString is {typeString}");

                AudioFormat audioFormat = AudioFormatFromString(typeString);
                Console.Error.WriteLine($"AudioFormat is {audioFormat}");

                sysin.Position = 0;
                WaveStream readerStream = ReaderForAudioFormat(sysin, audioFormat);
                Console.Error.WriteLine($"ReaderStream is {readerStream} with length {readerStream.Length} and position {readerStream.Position}");

                Stream sysout = Console.OpenStandardOutput();

                var fileWriter = new LameMP3FileWriter
                (
                    sysout,
                    readerStream.WaveFormat,
                    320,
                    new ID3TagData
                    {
                        Title = id3Title,
                        Artist = id3Author,
                    }
                );

                fileWriter.SetDebugFunction(txt => Console.Error.WriteLine(txt));
                fileWriter.SetErrorFunction(txt => Console.Error.WriteLine(txt));
                fileWriter.SetMessageFunction(txt => Console.Error.WriteLine(txt));

                Console.Error.WriteLine($"Streaming operations now!");

                readerStream.CopyTo(fileWriter);
                fileWriter.Flush();
                fileWriter.Close();

                sysout.Close();

                return 0;
            }
            catch(Exception e)
            {
                Console.Error.WriteLine(e);
                Console.Error.Flush();
                return -1;
            }
        }

        private static AudioFormat AudioFormatFromString(string extensionOrMime)
        {
            string str = extensionOrMime.ToLower();
            AudioFormat fmt = EnumExtensions.FromShortDisplayName<AudioFormat>(str);
            if(fmt == 0)
            {
                fmt = EnumExtensions.FromDisplayName<AudioFormat>(str);
            }
            return fmt;
        }

        private static WaveStream ReaderForAudioFormat(Stream fileInputStream, AudioFormat audioFormat)
        {
            WaveStream readerStream;
            switch (audioFormat)
            {
                case AudioFormat.Mp3:
                    readerStream = new Mp3FileReader(fileInputStream);
                    break;
                case AudioFormat.Wave:
                    readerStream = new WaveFileReader(fileInputStream);
                    break;
                case AudioFormat.Aiff:
                    readerStream = new AiffFileReader(fileInputStream);
                    break;
                case AudioFormat.Flac:
                    readerStream = new FlacReader(fileInputStream);
                    break;
                case AudioFormat.OggVorbis:
                    readerStream = new VorbisWaveReader(fileInputStream);
                    break;
                case AudioFormat.Other:
                default:
                    var tempPath = Path.GetTempFileName();
                    AppDomain.CurrentDomain.ProcessExit += (o, e) =>
                    {
                        //File.Delete(tempPath);
                    };
                    var tempFileStream = File.OpenWrite(tempPath);
                    fileInputStream.CopyTo(tempFileStream);
                    tempFileStream.Close();
                    Console.Error.WriteLine($"Creating MediaFoundationReader with file = {tempPath}");
                    readerStream = new MediaFoundationReader(tempPath);
                    break;
            }

            readerStream = ConvertToMp3LameConvertible(readerStream);

            return readerStream;
        }

        private static WaveStream ConvertToMp3LameConvertible(WaveStream readerStream)
        {
            WaveFormat waveFormat = readerStream.WaveFormat;
            if (waveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                waveFormat = WaveFormat.CreateCustomFormat
                (
                    WaveFormatEncoding.Pcm,
                    waveFormat.SampleRate,
                    waveFormat.Channels,
                    waveFormat.AverageBytesPerSecond,
                    waveFormat.BlockAlign,
                    waveFormat.BitsPerSample
                );
                readerStream = new WaveFormatConversionStream(waveFormat, readerStream);
                readerStream = new BlockAlignReductionStream(readerStream);
            }
            if (waveFormat.BitsPerSample != 16)
            {
                waveFormat = WaveFormat.CreateCustomFormat
                (
                    waveFormat.Encoding,
                    waveFormat.SampleRate,
                    waveFormat.Channels,
                    (16 * waveFormat.SampleRate * waveFormat.Channels) / 8,
                    (waveFormat.Channels * 16) / 8,
                    16
                );
                readerStream = new WaveProviderToWaveStream
                (
                    new MediaFoundationResampler(readerStream, waveFormat)
                );
            }
            if(!(readerStream is BlockAlignReductionStream))
            {
                readerStream = new BlockAlignReductionStream(readerStream);
            }
            return readerStream;
        }
    }

    internal enum AudioFormat
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

    internal static class EnumExtensions
    {
        public static TAttribute GetAttribute<TAttribute>(this Enum enumValue) where TAttribute : Attribute
        {
            Type enumType = enumValue.GetType();
            Type attributeType = typeof(TAttribute);
            return enumType.GetMember(Enum.GetName(enumType, enumValue)).First().GetCustomAttributes(attributeType, true).OfType<TAttribute>().SingleOrDefault();
        }

        public static TEnum FromDisplayName<TEnum>(string displayName) where TEnum : Enum
        {
            Type enumType = typeof(TEnum);
            Array enumValues = enumType.GetEnumValues();
            foreach (object enumVal in enumValues)
            {
                TEnum enumInstance = (TEnum)enumVal;
                DisplayAttribute displayAttr = enumInstance.GetAttribute<DisplayAttribute>();
                if (displayAttr == null)
                {
                    continue;
                }
                if (string.Equals(displayAttr.Name, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return enumInstance;
                }
            }
            return default;
        }

        public static TEnum FromShortDisplayName<TEnum>(string displayName) where TEnum : Enum
        {
            Type enumType = typeof(TEnum);
            Array enumValues = enumType.GetEnumValues();
            foreach (object enumVal in enumValues)
            {
                TEnum enumInstance = (TEnum)enumVal;
                DisplayAttribute displayAttr = enumInstance.GetAttribute<DisplayAttribute>();
                if (displayAttr == null)
                {
                    continue;
                }
                if (string.Equals(displayAttr.ShortName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return enumInstance;
                }
            }
            return default;
        }
    }

    public class WaveProviderToWaveStream : WaveStream
    {
        private readonly IWaveProvider source;
        private long position;

        public WaveProviderToWaveStream(IWaveProvider source)
        {
            this.source = source;
        }

        public override WaveFormat WaveFormat
        {
            get { return source.WaveFormat; }
        }

        /// <summary>
        /// Don't know the real length of the source, just return a big number
        /// </summary>
        public override long Length
        {
            get { return Int32.MaxValue; }
        }

        public override long Position
        {
            get
            {
                // we'll just return the number of bytes read so far
                return position;
            }
            set
            {
                // can't set position on the source
                // n.b. could alternatively ignore this
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            position += read;
            return read;
        }
    }
}
