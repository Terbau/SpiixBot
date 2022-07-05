using Microsoft.Extensions.Configuration;
using SpiixBot.Youtube;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Services
{
    public class YoutubeService
    {
        public YoutubeClient Client;

        public YoutubeService(IConfiguration config)
        {
            Client = new YoutubeClient(config["YOUTUBE_API_KEY"]);
        }
    }
}
