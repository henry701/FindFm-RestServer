using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace RestServer.Interprocess
{
    internal static class AudioHandlerService
    {
        private static readonly ILogger LOGGER = LogManager.GetCurrentClassLogger();

        public static async Task<Stream> ProcessAudio(Stream sourceAudio, int? cutSeconds, string title, string author, string mimeHint = null)
        {
            var procDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FindFm-AudioHandler");

            var startInfo = new ProcessStartInfo(Path.Combine(procDir, "FindFm_AudioHandler.exe"))
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = procDir
            };

            startInfo.Environment["FFM_ID3_AUTHOR"] = author;
            startInfo.Environment["FFM_ID3_TITLE"] = title;
            startInfo.Environment["FFM_MIMEHINT"] = mimeHint;

            using (var process = Process.Start(startInfo))
            {
                await sourceAudio.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();

                var outStream = new MemoryStream();

                // Either the timeout or the closing of sysout, whichever happens first
                Task.WaitAny
                (
                    Task.Run(() => process.WaitForExit(120000)),
                    process.StandardOutput.BaseStream.CopyToAsync(outStream)
                );

                if (!process.HasExited)
                {
                    process.Kill();
                    LOGGER.Error("Interprocess exit timeout. Process error stream: {}", await process.StandardError.ReadToEndAsync());
                    throw new ApplicationException("Erro ao normalizar áudio!");
                }

                if (process.ExitCode != 0)
                {
                    LOGGER.Error("Interprocess exit code expected 0, but was: {}. Process error stream: {}", process.ExitCode, await process.StandardError.ReadToEndAsync());
                    throw new ApplicationException("Erro ao normalizar áudio!");
                }

                LOGGER.Debug("Interprocess error stream: {}", await process.StandardError.ReadToEndAsync());

                outStream.Position = 0;
                return outStream;
            }
        }
    }
}
