using Discord;
using Discord.Commands;
using SpiixBot.Attributes;
using SpiixBot.Services;
using SpiixBot.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Modules
{
    [Group("help")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private CommandService _commandService;
        private CommandHandlingService _commandHandlingService;

        public string Prefix => _commandHandlingService.Prefix;

        public HelpModule(CommandService commandService, CommandHandlingService commandHandlingService)
        {
            _commandService = commandService;
            _commandHandlingService = commandHandlingService;
        }

        private bool HasAnyHiddenAttribute(CommandInfo command)
        {
            if (command.Attributes.Any(attr => attr is HiddenAttribute))
                return true;

            return HasAnyHiddenAttribute(command.Module);
        }

        private bool HasAnyHiddenAttribute(ModuleInfo module)
        {
            if (module == null) 
                return false;

            if (module.Attributes.Any(attr => attr is HiddenAttribute))
                return true;

            return HasAnyHiddenAttribute(module.Parent);
        }

        public async Task SendModuleHelp(ModuleInfo module)
        {
            //await ReplyAsync($"Module\n{module.Name}");

            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle($"Help | {module.Name}")
                //.WithDescription($"**Prefix:** `{Prefix}`")
                .WithColor(0x4298f5);

            List<string> commandStrings = new List<string>();
            List<List<string>> paginated = new List<List<string>>();

            foreach (CommandInfo command in module.Commands)
            {
                if (HasAnyHiddenAttribute(command))
                    continue;

                List<string> parameters = new List<string>();

                foreach (ParameterInfo param in command.Parameters)
                {
                    string colon = param.IsRemainder ? ":" : "";
                    string defaultValue = param.DefaultValue == null || param.DefaultValue.ToString() == "" ? "\"\"" : param.DefaultValue.ToString();
                    string part = param.IsOptional ? $"[{colon}{param.Name} = {defaultValue}]" : $"<{colon}{param.Name}>";

                    parameters.Add(part);
                }

                string paramString = "";
                if (parameters.Count > 0)
                    paramString = " " + string.Join(' ', parameters);

                string desc = command.Summary ?? command.Remarks ?? "No description.";
                string commandString = $"`{Prefix}{command.Name}{paramString}` - {desc}";

                if (commandStrings.Sum(s => s.Length) + commandString.Length > 1000)
                {
                    paginated.Add(commandStrings);
                    commandStrings = new List<string>();
                }

                commandStrings.Add(commandString);
            }

            foreach (ModuleInfo subModule in module.Submodules)
            {
                if (HasAnyHiddenAttribute(subModule))
                    continue;

                string moduleDesc = subModule.Summary ?? subModule.Remarks ?? "No description.";
                string moduleCommandString = $"`{Prefix}{subModule.Group} <subcommand>` - {moduleDesc}";

                if (commandStrings.Sum(s => s.Length) + moduleCommandString.Length > 1000)
                {
                    paginated.Add(commandStrings);
                    commandStrings = new List<string>();
                }

                commandStrings.Add(moduleCommandString);
            }

            if (commandStrings.Count != 0) 
            {
                paginated.Add(commandStrings);
            }

            if (paginated.Count == 0)
            {
                var commandField = new EmbedFieldBuilder()
                    .WithName("Commands")
                    .WithValue("No commands.")
                    .WithIsInline(false);
                builder.AddField(commandField);
            }
            else if (paginated.Count == 1)
            {
                var commandField = new EmbedFieldBuilder()
                    .WithName($"Commands ({paginated[0].Count})")
                    .WithValue(string.Join("\n", paginated[0]))
                    .WithIsInline(false);
                builder.AddField(commandField);
            }
            else
            {
                for (int i = 0; i < paginated.Count; i++)
                {
                    List<string> cmdParts = paginated[i];

                    var commandField = new EmbedFieldBuilder()
                        .WithName($"Commands ({cmdParts.Count}) [{i + 1}]")
                        .WithValue(string.Join("\n", cmdParts))
                        .WithIsInline(false);
                    builder.AddField(commandField);
                }
            }

            if (paginated.Count != 0)
            {
                builder.Fields.Last().Value += $"\n\n`<arg>` means the argument is required\n`[arg]` means it is optional";
                builder.Fields.Last().Value += "\n\nUse `!help [command]` for more info about a command.";
            }

            await ReplyAsync(embed: builder.Build());
        }

        public async Task SendCommandHelp(CommandInfo command)
        {
            //await ReplyAsync($"Command\n{command.Name}\n```\n{command.Remarks ?? "None"}``````\n{command.Summary ?? "None"}```");

            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle($"Help | {command.Name}")
                //.WithDescription($"**Prefix:** `{Prefix}`")
                .WithColor(0x4298f5);

            string desc = command.Summary ?? command.Remarks ?? "No description found.";

            var descBuilder = new EmbedFieldBuilder()
                .WithName("Description")
                .WithValue(desc)
                .WithIsInline(false);
            builder.AddField(descBuilder);

            //Parameter stuff
            List<string> parameters = new List<string>();
            List<string> parametersSummary = new List<string>();

            foreach (ParameterInfo param in command.Parameters)
            {
                string colon = param.IsRemainder ? ":" : "";
                string defaultValue = param.DefaultValue == null || param.DefaultValue.ToString() == "" ? "\"\"" : param.DefaultValue.ToString();
                string part = param.IsOptional ? $"[{colon}{param.Name} = {defaultValue}]" : $"<{colon}{param.Name}>";

                parameters.Add(part);

                if (param.Summary != null)
                    parametersSummary.Add($"> `{part}` - {param.Summary}");
            }

            string paramString = "";
            if (parameters.Count > 0)
                paramString = " " + string.Join(' ', parameters);

            string paramInfo = string.Join("\n", parametersSummary);

            var usageBuilder = new EmbedFieldBuilder()
                .WithName("Usage")
                .WithValue($"`{Prefix}{command.Aliases.First()}{paramString}`")
                .WithIsInline(false);
            builder.AddField(usageBuilder);

            if (paramInfo != "")
            {
                var paramDescBuilder = new EmbedFieldBuilder()
                    .WithName("Argument Descriptions:")
                    .WithValue(paramInfo)
                    .WithIsInline(false);
                builder.AddField(paramDescBuilder);
            }
                
            List<string> aliases = new List<string>();
            foreach (string alias in command.Aliases)
            {
                string actual = alias.Split(' ').Last();
                if (actual == command.Name || aliases.Contains(actual))
                    continue;

                aliases.Add(actual);
            }

            if (aliases.Count != 0)
            {
                var aliasBuilder = new EmbedFieldBuilder()
                    .WithName("Aliases")
                    .WithValue(string.Join(", ", aliases.Select(a => $"`{a}`")))
                    .WithIsInline(false);
                builder.AddField(aliasBuilder);
            }   

            builder.Fields.Last().Value += $"\n\n`<arg>` means the argument is required\n`[arg]` means it is optional";

            if (command.Module.Group != null)
            {
                builder.Fields.Last().Value += $"\n\n_This command is a subcommand of `{command.Module.Group}`_";
            }

            await ReplyAsync(embed: builder.Build());
        }

        public async Task SendGroupHelp(ModuleInfo module)
        {
            //await ReplyAsync($"Group\n{module.Group}");

            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle($"Help | {module.Group}")
                //.WithDescription($"**Prefix:** `{Prefix}`")
                .WithColor(0x4298f5);

            string desc = module.Summary ?? module.Remarks ?? "No description found.";

            var descBuilder = new EmbedFieldBuilder()
                .WithName("Description")
                .WithValue(desc)
                .WithIsInline(false);
            builder.AddField(descBuilder);

            CommandInfo defaultCommand = null;
            defaultCommand = module.Commands.FirstOrDefault(cmd => char.IsUpper(cmd.Name[0]) && !HasAnyHiddenAttribute(cmd));

            if (defaultCommand != null)
            {
                //Parameter stuff
                List<string> parameters = new List<string>();
                List<string> parametersSummary = new List<string>();

                foreach (ParameterInfo param in defaultCommand.Parameters)
                {
                    string colon = param.IsRemainder ? ":" : "";
                    string defaultValue = param.DefaultValue == null || param.DefaultValue.ToString() == "" ? "\"\"" : param.DefaultValue.ToString();
                    string part = param.IsOptional ? $"[{colon}{param.Name} = {defaultValue}]" : $"<{colon}{param.Name}>";

                    parameters.Add(part);

                    if (param.Summary != null)
                        parametersSummary.Add($"> `{part}` - {param.Summary}");
                }

                string paramString = "";
                if (parameters.Count > 0)
                    paramString = " " + string.Join(' ', parameters);

                string paramInfo = string.Join("\n", parametersSummary);

                string defaultDesc = defaultCommand.Summary ?? defaultCommand.Remarks;
                if (defaultDesc != null)
                    defaultDesc = " - " + defaultDesc;

                var usageBuilder = new EmbedFieldBuilder()
                    .WithName("Usage")
                    .WithValue($"`{Prefix}{defaultCommand.Aliases.First()}{paramString}`{defaultDesc}\n`{Prefix}{defaultCommand.Aliases.First()} <subcommand>`{paramInfo}")
                    .WithIsInline(false);
                builder.AddField(usageBuilder);

                if (paramInfo != "")
                {
                    var paramDescBuilder = new EmbedFieldBuilder()
                        .WithName("Argument Descriptions:")
                        .WithValue(paramInfo)
                        .WithIsInline(false);
                    builder.AddField(paramDescBuilder);
                }
            }
            else
            {
                var usageBuilder = new EmbedFieldBuilder()
                    .WithName("Usage")
                    .WithValue($"`{Prefix}{module.Aliases.First()} <subcommand>`")
                    .WithIsInline(false);
                builder.AddField(usageBuilder);
            }

            List<string> subCommands = new List<string>();

            foreach (CommandInfo command in module.Commands)
            {
                if (char.IsUpper(command.Name[0]))
                {
                    if (module.Commands.Count == 1 && module.Submodules.Count == 0)
                        break;

                    if (HasAnyHiddenAttribute(command))
                        continue;

                    continue;
                }

                subCommands.Add($"`{command.Name}`");
            }

            foreach (ModuleInfo subModule in module.Submodules)
            {
                if (HasAnyHiddenAttribute(subModule))
                    continue;

                subCommands.Add($"`{subModule.Group}`");
            }

            if (subCommands.Count != 0)
            {
                var commandField = new EmbedFieldBuilder()
                    .WithName("Subcommands")
                    .WithValue(string.Join(", ", subCommands))
                    .WithIsInline(false);
                builder.AddField(commandField);
            }

            if (defaultCommand != null)
                builder.Fields.Last().Value += $"\n\n`<arg>` means the argument is required\n`[arg]` means it is optional";

            await ReplyAsync(embed: builder.Build());
        }

        public async Task SendBotHelp2()
        {
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle("Help")
                //.WithDescription($"**Prefix:** `{Prefix}`")
                .WithColor(0x4298f5);

            Dictionary<string, CommandInfo> noCategoryCommands = new Dictionary<string, CommandInfo>();
            Dictionary<string, List<string>> modules = new Dictionary<string, List<string>>();
            
            foreach (CommandInfo command in _commandService.Commands)
            {

            }
        }

        public async Task SendBotHelp()
        {
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle("Help")
                //.WithDescription($"**Prefix:** `{Prefix}`")
                .WithColor(0x4298f5);

            ModuleInfo[] modules = _commandService.Modules.ToArray();
            Dictionary<string, List<ModuleInfo>> extraCommands = new Dictionary<string, List<ModuleInfo>>();

            List<ModuleInfo> toReiterate = new List<ModuleInfo>();

            foreach (ModuleInfo module in _commandService.Modules)
            {
                if (module.Parent != null && module.Parent.Group == null && module.Parent.Parent == null)
                {
                    List<ModuleInfo> existing;
                    if (!extraCommands.TryGetValue(module.Parent.Name, out existing))
                    {
                        existing = new List<ModuleInfo>();
                    }

                    existing.Add(module);
                    extraCommands[module.Parent.Name] = existing;
                }
            }

            var noCategoryCommands = new Dictionary<string, string>();
            var usedModuleCommands = new List<string>();

            if (modules.Length == 0)
            {
                builder.Description = "\n\nNo commands found.";
            }
            else
            {
                for (int i = 0; i < modules.Length; i++)
                {
                    ModuleInfo module = modules[i];

                    if (module.Group != null && (module.Parent == null || module.Parent.Group == null) && !HasAnyHiddenAttribute(module))
                    {
                        noCategoryCommands[module.Name] = $"`{module.Name}`";
                        continue;
                    }

                    int cmdAmount = module.Commands.Count;

                    string fmt = string.Join(", ", module.Commands.Where(cmd => !HasAnyHiddenAttribute(cmd)).Select(cmd => $"`{cmd.Name}`"));
                    if (fmt == "") continue;

                    if (extraCommands.TryGetValue(module.Name, out List<ModuleInfo> extras))
                    {
                        ModuleInfo[] extrasFiltered = extras.Where(cmd => !HasAnyHiddenAttribute(cmd)).ToArray();
                        
                        if (extrasFiltered.Length > 0)
                            fmt += $", {string.Join(", ", extrasFiltered.Select(cmd => $"`{cmd.Name}`"))}";

                        usedModuleCommands.AddRange(extras.Select(cmd => cmd.Name));
                    }

                    EmbedFieldBuilder fieldBuilder = new EmbedFieldBuilder()
                        .WithName($"{module.Name} ({cmdAmount})")
                        .WithValue(fmt)
                        .WithIsInline(false);
                    builder.AddField(fieldBuilder);
                }
            }

            if (noCategoryCommands.Count > 0)
            {
                EmbedFieldBuilder fieldBuilder = new EmbedFieldBuilder()
                        .WithName($"No Category ({noCategoryCommands.Count})")
                        .WithValue(string.Join(", ", noCategoryCommands.Where(pair => !usedModuleCommands.Contains(pair.Key)).Select(pair => pair.Value)))
                        .WithIsInline(false);
                builder.AddField(fieldBuilder);
            }

            if (builder.Fields.Count > 0)
                builder.Fields.Last().Value += $"\n\nUse `{Prefix}help [command]` for more info about a command.\nUse `{Prefix}help [category]` for more info about a category.";
            else
                builder.Description = "\n\nNo commands found.";

            await ReplyAsync(embed: builder.Build());
        }

        [Command]
        public async Task HelpCommand([Remainder]string commandOrModule = null)
        {
            if (commandOrModule != null)
            {
                if (commandOrModule.StartsWith(Prefix))
                    commandOrModule = commandOrModule[1..];

                foreach (CommandInfo command in _commandService.Commands)
                {
                    if (HasAnyHiddenAttribute(command))
                        continue;

                    foreach (string alias in command.Aliases)
                    {
                        if (string.Equals(alias, commandOrModule, StringComparison.OrdinalIgnoreCase))
                        {
                            if (command.Module.Group != null)
                            {
                                if (command.Module.Aliases.Any(a => string.Equals(a, alias, StringComparison.OrdinalIgnoreCase)))
                                {
                                    await SendGroupHelp(command.Module);
                                    return;
                                }
                            }

                            await SendCommandHelp(command);
                            return;
                        }
                    }
                }

                foreach (ModuleInfo module in _commandService.Modules)
                {
                    if (HasAnyHiddenAttribute(module))
                        continue;

                    if (module.Group == null)
                    {
                        if (string.Equals(module.Name, commandOrModule, StringComparison.OrdinalIgnoreCase))
                        {
                            await SendModuleHelp(module);
                            return;
                        }
                    }
                    else
                    {
                        foreach (string alias in module.Aliases)
                        {
                            if (string.Equals(alias, commandOrModule, StringComparison.OrdinalIgnoreCase))
                            {
                                await SendGroupHelp(module);
                                return;
                            }
                        }
                    } 
                }

                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Could not find the specified command or module."));
                return;
            }

            await SendBotHelp();
        }
    }
}
