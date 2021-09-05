using BotCatMaxy;
using BotCatMaxy.Models;
using BotCatMaxy.TypeReaders;
using Discord;
using Interactivity;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BotCatMaxy.Components.Logging;

namespace BotCatMaxy.Startup
{
    public class CommandHandler : DiscordClientService
    {
        /*public readonly HashSet<string> ignoredCMDErrors = new HashSet<string>() { "User not found.",
                            "The input text has too few parameters.", "Invalid context for command; accepted contexts: Guild.",
                            "User requires guild permission BanMembers.", "This command now only works in the bot's DMs", "Failed to parse Int32.",
                            "User requires guild permission KickMembers.", "Bot requires guild permission ManageRoles.",
                            "Command can only be run by the owner of the bot.", "You don't have the permissions to use this.",
                            "User requires channel permission ManageMessages.", "Failed to parse UInt32." };*/
        private readonly IDiscordClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public CommandHandler(IDiscordClient client, ILogger<CommandHandler> logger,  IServiceProvider services, CommandService commandService) 
            : base(client as DiscordSocketClient, logger)
        {
            _commands = commandService;
            _client = client;
            _services = services;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
            => InitializeAsync(cancellationToken);

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (_client is DiscordSocketClient socketClient)
                // Hook the MessageReceived event into our command handler
                socketClient.MessageReceived += HandleCommandAsync;

            //Exception and Post Execution handling
            _commands.CommandExecuted += CommandExecuted;

            //Adds custom type readers
            _commands.AddTypeReader(typeof(Emoji), new EmojiTypeReader());
            _commands.AddTypeReader(typeof(UserRef), new UserRefTypeReader());
            _commands.AddTypeReader(typeof(IUser), new BetterUserTypeReader());
            _commands.AddTypeReader(typeof(TimeSpan), new TimeSpanTypeReader(), true);
            _commands.AddTypeReader(typeof(CommandInfo[]), new CommandTypeReader());

            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetAssembly(typeof(Program)),
                services: _services);
            await new LogMessage(LogSeverity.Info, "CMDs", "Commands set up").Log();
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
            => await ExecuteCommand(messageParam);

        public async Task ExecuteCommand(IMessage messageParam, ICommandContext context = null)
        {
            // Don't process the command if it was a system message
            if (messageParam is not IUserMessage message)
                return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref argPos) ||
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message and assume if no mock context then use Socket
            context ??= new SocketCommandContext((DiscordSocketClient)_client, (SocketUserMessage)message);

            //var res = filter.CheckMessage(message, context);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.

            // Keep in mind that result does not indicate a return value
            // rather an object stating if the command executed successfully.
            await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: _services);
        }

        const string permissionRegex = @"(.+) requires (.+) permission (.+)\.";

        private async Task CommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result is CommandResult)
            {
                if (!string.IsNullOrEmpty(result.ErrorReason))
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                return;
            }

            if (result.IsSuccess || result.Error == CommandError.UnknownCommand)
            {
                return;
            }

            string message = null;
            //Idea is to override missing perm messages like "User requires guild permission BanMembers." to be more readable
            //like "You need server permission ban members."
            if (result.Error == CommandError.UnmetPrecondition)
            {
                var match = Regex.Match(result.ErrorReason, permissionRegex);
                if (match.Success)
                {
                    //If Bot, say "I", else say "You"
                    string user = match.Groups[1].Value == "Bot" ? "I" : "You";
                    //If guild permission, say "server" permission, else say "channel" permission
                    string area = match.Groups[2].Value == "guild" ? "server" : "channel";
                    message = $"{user} need {area} permission {match.Groups[3].Value.Humanize(LetterCasing.LowerCase)}.";
                }
            }

            //Try to use above override, otherwise use provided error reason from event
            await context.Channel.SendMessageAsync(message ?? result.ErrorReason);

            //Debug info
            if (result.Error is CommandError.Exception or CommandError.Unsuccessful)
                await LogCommandException(command, context, (result as ExecuteResult?)?.Exception, result);
        }

        private async Task LogCommandException(Optional<CommandInfo> command, ICommandContext context, Exception exception, IResult result)
        {
            //To make any null variables clear and stand out if they shouldn't normally be null
            const string nullIndicator = "NULL_MESSAGE";
            string logMessage = $"Command `!{command.Value?.Name ?? nullIndicator}` in";

            if (context.Guild != null)
                logMessage += $" {await context.Guild.Describe("`")}";
            else
                logMessage += $" `{context.User.Describe()}` DMs";

            logMessage += $" encountered: result type `{result.GetType().Name}` with reason: `{result.ErrorReason}`";

            var errorEmbed = new EmbedBuilder()
            {
                Title = $"New Command Exception",
                Timestamp = DateTime.Now,
                Description = logMessage.Truncate(1500)
            };

            errorEmbed.AddField("Executor", $"{context.User.Username}#{context.User.Discriminator}\n({context.User.Id})", true);
            errorEmbed.AddField("Channel", context.Guild is null ? "Direct Messages" : $"{context.Channel.Name}\n({context.Channel.Id})", true);

            if (context.Guild is not null)
                errorEmbed.AddField("Guild", $"{context.Guild.Name}\n({context.Guild.Id})", true);

            errorEmbed.AddField($"Message Content {context.Message?.Id.ToString() ?? nullIndicator}",
                $"```{context.Message?.Content ?? nullIndicator}```[Jump to Invocation]({context.Message.GetJumpUrl()})");
            errorEmbed.AddField("Exception", $"```{exception}```");
            await LogSeverity.Error.LogExceptionAsync("Command", logMessage, exception, errorEmbed);
        }
    }
}

namespace Discord.Commands
{
    public class BetterUserTypeReader : UserTypeReader<IUser>
    {
        public override async Task<TypeReaderResult> ReadAsync(
            ICommandContext context,
            string input,
            IServiceProvider services)
        {
            var result = await base.ReadAsync(context, input, services);
            if (result.IsSuccess)
                return result;
            else
            {
                DiscordRestClient restClient = (context.Client as DiscordSocketClient).Rest;
                if (MentionUtils.TryParseUser(input, out var id))
                {
                    RestUser user = await restClient.GetUserAsync(id);
                    if (user != null) return TypeReaderResult.FromSuccess(user);
                }
                if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                {
                    RestUser user = await restClient.GetUserAsync(id);
                    if (user != null) return TypeReaderResult.FromSuccess(user);
                }
                return TypeReaderResult.FromError(CommandError.ObjectNotFound, "User not found.");
            }
            /*
            if (svc != null) {
                var game = svc.GetGameFromChannel(context.Channel);
                if (game != null) {
                    var player = game.Players.SingleOrDefault(p => p.User.Id == user.Id);
                    return (player != null)
                        ? TypeReaderResult.FromSuccess(player)
                        : TypeReaderResult.FromError(CommandError.ObjectNotFound, "Specified user not a player in this game.");
                }
                return TypeReaderResult.FromError(CommandError.ObjectNotFound, "No game going on.");
            }
            return TypeReaderResult.FromError(CommandError.ObjectNotFound, "Game service not found.");*/
        }
    }
}