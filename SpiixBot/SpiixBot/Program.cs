using SpiixBot.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Diagnostics;
using SpiixBot.Spotify;
using SpiixBot.Spotify.Models;
using System.Collections.Generic;
using System.Web;
using Victoria;
using Victoria.Enums;
using System.Linq;
using Victoria.Decoder;
using SpiixBot.Util.Decoder;
using SpiixBot.Util.Encoder;
using SpiixBot.Util;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System.IO;

namespace SpiixBot
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().StartAsync().GetAwaiter().GetResult();
        }

        public async Task StartAsync()
        {
            IServiceCollection services = ConfigureServices();
            IServiceProvider provider = services.BuildServiceProvider();
            IConfiguration configuration = provider.GetService<IConfiguration>();

            if (configuration["DISCORD_BOT_TOKEN"] == "" ||
                configuration["SPOTIFY_CLIENT_ID"] == "" ||
                configuration["SPOTIFY_SECRET"] == "")
            {
                throw new Exception("Some required environment variables are not set.");
                return;
            }

            var client = provider.GetRequiredService<DiscordSocketClient>();

            string token = configuration["DISCORD_BOT_TOKEN"];
            await client.LoginAsync(TokenType.Bot, token);

            await client.StartAsync();

            var commands = provider.GetRequiredService<CommandService>();
            await commands.AddModulesAsync(Assembly.GetExecutingAssembly(), provider);

            bool hasSetup = false;

            client.Ready += async () =>
            {
                LavaConfig lavaConfig = provider.GetRequiredService<LavaConfig>();

                LavaNode lavaNode = provider.GetRequiredService<LavaNode>();
                while (!lavaNode.IsConnected)
                {
                    await lavaNode.ConnectAsync();
                    if (!lavaNode.IsConnected) await Task.Delay(1000);
                    if (lavaNode.IsConnected) Console.WriteLine("LavaLink successfully connected");
                }

                if (!hasSetup)
                {
                    provider.GetRequiredService<CommandHandlingService>().Start();

                    Console.WriteLine("Bot is ready!");
                    Console.WriteLine($"- Username: {client.CurrentUser.Username}");
                    Console.WriteLine($"- Id: {client.CurrentUser.Id}");
                }

                hasSetup = true;
                
            };
            client.Log += (LogMessage message) =>
            {
                Console.WriteLine(message.ToString());
                return Task.CompletedTask;
            };

            await Task.Delay(-1);
        }


        private IServiceCollection ConfigureServices()
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true)
                .AddEnvironmentVariables()
                .Build();

            return new ServiceCollection()
                .AddSingleton(provider => config)
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    MessageCacheSize = 500,
                    LogLevel = Discord.LogSeverity.Verbose
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    CaseSensitiveCommands = false,
                    IgnoreExtraArgs = false
                }))
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<AudioService>()
                .AddSingleton<YoutubeService>()
                .AddSingleton<SpotifyService>()
                .AddLavaNode(x => 
                {
                    x.SelfDeaf = false;

                    string port = Environment.GetEnvironmentVariable("LAVALINK_PORT");
                    if (port != null) x.Port = ushort.Parse(port);  // Default port is 2333

                    x.Hostname = Environment.GetEnvironmentVariable("LAVALINK_HOSTNAME") ?? "127.0.0.1";  // Needs to be the name of the docker service if using docker
                });
        }
    }
}
