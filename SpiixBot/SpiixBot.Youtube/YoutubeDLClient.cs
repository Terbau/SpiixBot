using SpiixBot.Youtube.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Youtube
{
    public class YoutubeDLClient
    {
        public DefaultArguments DefaultArguments { get; set; }

        public YoutubeDLClient()
        {
            DefaultArguments = new DefaultArguments();
        }

        public YoutubeDLClient(DefaultArguments defaultArguments)
        {
            DefaultArguments = defaultArguments;
        }

        public string[] RunProcessCommand(string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "youtube-dl.exe";
                process.StartInfo.Arguments = DefaultArguments.GetAsArguments() + " " + arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                return output.Split(Environment.NewLine.ToCharArray());
            }
        }

        public async Task<string[]> RunProcessCommandAsync(string arguments, YoutubeRunMode runMode)
        {
            if (runMode is YoutubeRunMode.Async)
            {
                string[] output = await Task.Run(() => RunProcessCommand(arguments));
                return output;
            }
            else
            {
                return RunProcessCommand(arguments);
            }
        }

        public async Task<Video> GetVideoInfoByYTDL(string keyword, YoutubeRunMode runMode = YoutubeRunMode.Async)
        {
            // Don't really need to get description here but it dont care to test it without.
            string arguments = $"--no-playlist --get-title --get-id --get-thumbnail --get-description --get-duration ytsearch:\"{keyword}\"";
            string[] output = await RunProcessCommandAsync(arguments, runMode);

            var video = new Video(output[0], output[1], output[2], output[output.Length - 2]);

            return video;
        }
    }
}
