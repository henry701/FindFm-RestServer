using System;
using NAudio.Flac;
using NAudio.Lame;
using NAudio.Vorbis;
using NAudio.Wave;
using System.Linq;
using System.IO;
using System.ComponentModel.DataAnnotations;
using MimeDetective.Extensions;

namespace FindFm.AudioHandler
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
                int? limitSeconds = Environment.GetEnvironmentVariable("FFM_MAX_SECONDS") == null ?
                    new int?() :
                    Convert.ToInt32(Environment.GetEnvironmentVariable("FFM_MAX_SECONDS"));

                string typeString = InferInformationString(sysin, mimeHint);
                Console.Error.WriteLine($"TypeString is {typeString}");

                AudioFormat audioFormat = AudioFormatFromString(typeString);
                Console.Error.WriteLine($"AudioFormat is {audioFormat}");

                sysin.Position = 0;
                WaveStream readerStream = ReaderForAudioFormat(sysin, audioFormat, limitSeconds, out int seconds);
                Console.Error.WriteLine($"ReaderStream is {readerStream}");

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

                Console.Error.WriteLine($"Setting Debug,Error,Message functions on fileWriter");

                fileWriter.SetDebugFunction(txt => Console.Error.WriteLine(txt));
                fileWriter.SetErrorFunction(txt => Console.Error.WriteLine(txt));
                fileWriter.SetMessageFunction(txt => Console.Error.WriteLine(txt));

                Console.Error.WriteLine($"Streaming operations now!");

                readerStream.CopyTo(fileWriter);
                fileWriter.Flush();
                fileWriter.Close();

                sysout.Close();

                return seconds;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Console.Error.Flush();
                return -1;
            }
        }

        private static string InferInformationString(Stream sysin, string mimeHint)
        {
            string typeString = HeyRed.Mime.MimeGuesser.GuessExtension(sysin);

            if (string.Equals(typeString, "bin", StringComparison.OrdinalIgnoreCase))
            {
                MimeDetective.FileType ft = sysin.GetFileType();
                if (ft != null)
                {
                    typeString = ft.Extension;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(mimeHint))
                    {
                        typeString = HeyRed.Mime.MimeTypesMap.GetExtension(mimeHint);
                        if (string.Equals(typeString, "bin", StringComparison.OrdinalIgnoreCase))
                        {
                            // TODO more guessing
                        }
                    }
                }
            }

            return typeString;
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

        private static WaveStream ReaderForAudioFormat
        (
            Stream fileInputStream,
            AudioFormat audioFormat,
            int? maxSeconds,
            out int seconds
        )
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
                        File.Delete(tempPath);
                    };
                    var tempFileStream = File.OpenWrite(tempPath);
                    fileInputStream.CopyTo(tempFileStream);
                    tempFileStream.Close();
                    Console.Error.WriteLine($"Creating MediaFoundationReader with file = {tempPath}");
                    readerStream = new MediaFoundationReader(tempPath);
                    break;
            }

            if (maxSeconds.HasValue)
            {
                readerStream = new CapSecondsByAverageStream(readerStream, maxSeconds.Value);
                seconds = Math.Min(maxSeconds.Value,
                    (int)(readerStream.Length / readerStream.WaveFormat.AverageBytesPerSecond));
            }
            else
            {
                seconds = (int) (readerStream.Length / readerStream.WaveFormat.AverageBytesPerSecond);
            }

            readerStream = NormalizeWaveFormat(readerStream);

            return readerStream;
        }

        private static WaveStream NormalizeWaveFormat(WaveStream readerStream)
        {
            WaveFormat waveFormat = readerStream.WaveFormat;
            if (waveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                ConvertToPcm(ref readerStream, ref waveFormat);
            }
            if (waveFormat.BitsPerSample != 16)
            {
                waveFormat = CreateSaneWaveFormat
                (
                    waveFormat.Encoding,
                    waveFormat.SampleRate,
                    waveFormat.Channels,
                    16
                );
                readerStream = new WaveProviderToWaveStream
                (
                    new MediaFoundationResampler(readerStream, waveFormat)
                );
            }
            if(waveFormat.SampleRate != 44_100)
            {
                waveFormat = CreateSaneWaveFormat
                (
                    waveFormat.Encoding,
                    44_100,
                    waveFormat.Channels,
                    waveFormat.BitsPerSample
                );
                readerStream = new WaveProviderToWaveStream
                (
                    new MediaFoundationResampler(readerStream, waveFormat)
                );
            }
            if (waveFormat.Channels != 2)
            {
                waveFormat = CreateSaneWaveFormat
                (
                    waveFormat.Encoding,
                    waveFormat.SampleRate,
                    2,
                    waveFormat.BitsPerSample
                );
                readerStream = new WaveProviderToWaveStream
                (
                    new MediaFoundationResampler(readerStream, waveFormat)
                );
            }
            if (!(readerStream is BlockAlignReductionStream))
            {
                readerStream = new BlockAlignReductionStream(readerStream);
            }
            return readerStream;
        }

        private static void ConvertToPcm(ref WaveStream readerStream, ref WaveFormat waveFormat)
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

        internal static WaveFormat CreateSaneWaveFormat
        (
            WaveFormatEncoding encoding,
            int sampleRate,
            int channels, 
            int bitsPerSample
        )
        {
            return WaveFormat.CreateCustomFormat
            (
                encoding,
                sampleRate,
                channels,
                (bitsPerSample * sampleRate * channels) / 8,
                (channels * bitsPerSample) / 8,
                bitsPerSample
            );
        }

        private class CapSecondsByAverageStream : WaveStream
        {
            private WaveStream ReaderStream { get; set; }
            public int MaxSeconds { get; private set; }
            private long MaxBytes { get; set; }

            public CapSecondsByAverageStream(WaveStream readerStream, int maxSeconds)
            {
                ReaderStream = readerStream;
                MaxSeconds = maxSeconds;
                MaxBytes = readerStream.WaveFormat.AverageBytesPerSecond * maxSeconds;
            }

            public override WaveFormat WaveFormat => ReaderStream.WaveFormat;

            public override long Length => Math.Min(ReaderStream.Length, MaxBytes);

            public override long Position { get => ReaderStream.Position; set => ReaderStream.Position = value; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                long left = MaxBytes - Position;
                if(left < count)
                {
                    count = (int) left;
                }
                return ReaderStream.Read(buffer, offset, count);
            }
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
        private int? providedLength;

        public WaveProviderToWaveStream(IWaveProvider source, int? providedLength = null)
        {
            this.source = source;
            this.providedLength = providedLength;
        }

        public override WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Don't know the real length of the source, just return a big number
        /// </summary>
        public override long Length => providedLength ?? int.MaxValue;

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
