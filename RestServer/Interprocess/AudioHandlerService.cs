using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RestServer.Interprocess
{
    internal static class AudioHandlerService
    {
        public static Stream ProcessAudio(Stream sourceAudio, string extension, int? cutSeconds, string title, string author)
        {
            var startInfo = new ProcessStartInfo("AudioHandler.exe");
            startInfo.Environment["FFM_ID3_AUTHOR"] = author;
            startInfo.Environment["FFM_ID3_TITLE"] = title;
            startInfo.Environment["FFM_EXTENSION"] = extension;
            var process = Process.Start(startInfo);
            sourceAudio.CopyTo(process.StandardInput.BaseStream);
            return process.StandardOutput.BaseStream;
        }
    }
}
