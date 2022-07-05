using Microsoft.Extensions.Configuration;
using SpiixBot.Spotify;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Services
{
    public class SpotifyService
    {
        public SpotifyClient Client;

        public SpotifyService(IConfiguration config)
        {
            Client = new SpotifyClient(config["SPOTIFY_CLIENT_ID"], config["SPOTIFY_SECRET"]);
        }
    }
}
