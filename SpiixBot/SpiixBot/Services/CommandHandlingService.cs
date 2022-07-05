using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Services
{
    public class CommandHandlingService
    {
        public string Prefix { get; set; } = "!";

        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        public List<CommandInfo> NotSubCommands { get; } = new List<CommandInfo>();
        public List<ModuleInfo> NotSubGroupModules { get; } = new List<ModuleInfo>();

        public CommandHandlingService(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider, IConfiguration config)
        {
            _client = client;
            _commands = commands;
            _serviceProvider = serviceProvider;
            _config = config;

            string newPrefix = config["PREFIX"];
            if (newPrefix != null)
            {
                Prefix = newPrefix;
            }

            Console.WriteLine("Prefix: " + Prefix);
        }

        public void Start()
        {
            foreach (ModuleInfo module in _commands.Modules)
            {
                if (module.Group != null && (module.Parent == null || module.Parent.Group == null))
                    NotSubGroupModules.Add(module);
            }

            _client.MessageReceived += OnMessageReceivedAsync;
        }

        public void Stop()
        {
            _client.MessageReceived -= OnMessageReceivedAsync;
        }

        private async Task OnMessageReceivedAsync(SocketMessage messageParam)
        {
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            var context = new SocketCommandContext(_client, message);

            int argPos = 0;

            if (!(message.HasStringPrefix(Prefix, ref argPos) ||
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
            {
                //Console.WriteLine($"Message not recognized as command: {message.Content}");
                return;
            }

            IResult result = await _commands.ExecuteAsync(context, argPos, _serviceProvider);

            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case CommandError.UnknownCommand:
                    case CommandError.UnmetPrecondition:
                        break;

                    case CommandError.Exception:
                        if (result is ExecuteResult exeRes)
                        {
                            Console.WriteLine(exeRes.Exception.ToString());
                            //Console.WriteLine(exeRes.Exception.StackTrace);

                            await context.Channel.SendMessageAsync("```\n" + exeRes.Exception.ToString().Replace("`", "\\`") + "```");
                        }
                        break;

                    default:
                        await context.Channel.SendMessageAsync("```\n" + result.ToString() + "```");
                        break;
                }
            }
        }
    }
}
