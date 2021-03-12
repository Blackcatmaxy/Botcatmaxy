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
using System.Threading.Tasks;

namespace BotCatMaxy.Startup
{
    public class CommandHandler
    {
        /*public readonly HashSet<string> ignoredCMDErrors = new HashSet<string>() { "User not found.",
                            "The input text has too few parameters.", "Invalid context for command; accepted contexts: Guild.",
                            "User requires guild permission BanMembers.", "This command now only works in the bot's DMs", "Failed to parse Int32.",
                            "User requires guild permission KickMembers.", "Bot requires guild permission ManageRoles.",
                            "Command can only be run by the owner of the bot.", "You don't have the permissions to use this.",
                            "User requires channel permission ManageMessages.", "Failed to parse UInt32." };*/
        private readonly IDiscordClient _client;
        private readonly CommandService _commands;
        public readonly IServiceProvider services;

        public CommandHandler(IDiscordClient client, CommandService commands)
        {
            _commands = commands;
            _client = client;
            var serviceBuilder = new ServiceCollection()
                .AddSingleton(_client);
            if (client is DiscordSocketClient socketClient)
                serviceBuilder.AddSingleton(new InteractivityService(socketClient, TimeSpan.FromMinutes(3)));
            services = serviceBuilder.BuildServiceProvider();
            _ = InstallCommandsAsync();
        }

        private async Task InstallCommandsAsync()
        {
            try
            {
                if (_client is DiscordSocketClient socketClient)
                    // Hook the MessageReceived event into our command handler
                    socketClient.MessageReceived += HandleCommandAsync;

                //Exception and Post Execution handling
                _commands.Log += ExceptionLogging.Log;
                _commands.CommandExecuted += CommandExecuted;

                //Adds custom type readers
                _commands.AddTypeReader(typeof(Emoji), new EmojiTypeReader());
                _commands.AddTypeReader(typeof(UserRef), new UserRefTypeReader());
                _commands.AddTypeReader(typeof(IUser), new BetterUserTypeReader());
                _commands.AddTypeReader(typeof(TimeSpan), new TimeSpanTypeReader(), true);

                // See Dependency Injection guide for more information.
                await _commands.AddModulesAsync(assembly: Assembly.GetAssembly(typeof(MainClass)),
                                                services: services);
                //await new LogMessage(LogSeverity.Info, "CMDs", "Commands set up").Log();
            }
            catch (Exception e)
            {
                await new LogMessage(LogSeverity.Critical, "CMDs", "Commands set up failed", e).Log();
            }
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
                services: services);
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
                    string user = (match.Groups[1].Value == "Bot") ? "I" : "You";
                    //If guild permission, say "server" permission, else say "channel" permission
                    string area = (match.Groups[2].Value == "guild") ? "server" : "channel";
                    message = $"{user} need {area} permission {match.Groups[3].Value.Humanize(LetterCasing.LowerCase)}.";
                }
            }

            //Try to use above override, otherwise use provided error reason from event
            await context.Channel.SendMessageAsync(message ?? result.ErrorReason);

            //Debug info
            if (result.Error != null && (result.Error == CommandError.Exception || result.Error == CommandError.Unsuccessful))
            {
                string logMessage = $"Command !{command.Value?.Name} in";
                if (context.Guild != null)
                {
                    logMessage += $" {await context.Guild.Describe()} owned by {(await context.Guild.GetOwnerAsync()).Describe()}";
                }
                else
                {
                    logMessage += $" {context.User.Describe()} DMs";
                }
                logMessage += $" used as \"{context.Message}\" encountered: result type \"{result.GetType().Name}\", \"{result.ErrorReason}\"";
                await new LogMessage(LogSeverity.Error, "CMDs", logMessage, (result as ExecuteResult?)?.Exception).Log();
            }
        }
    }
}

namespace Discord.Commands
{
    public class CanWarnAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is IGuildUser gUser)
            {
                // If this command was executed by a user with the appropriate role, return a success
                if (gUser.CanWarn())
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
            }
            else
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }

    public class HasAdminAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            //Makes sure it's in a server
            if (context.User is IGuildUser gUser)
            {
                // If this command was executed by a user with administrator permission, return a success
                if (gUser.HasAdmin())
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
            }
            else
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }

    public class RequireHierarchyAttribute : ParameterPreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            ParameterInfo parameter, object value, IServiceProvider services)
        {
            if (context.User is not IGuildUser guildUser)
                return PreconditionResult.FromError("This command cannot be used outside of a guild");
            var targetUser = value switch
            {
                UserRef userRef => userRef.GuildUser,
                IGuildUser targetGuildUser => targetGuildUser,
                ulong userId => await context.Guild.GetUserAsync(userId),
                _ => throw new ArgumentOutOfRangeException("Unknown Type used in parameter that requires hierarchy"),
            };
            if (targetUser == null)
                if (value is UserRef)
                    return PreconditionResult.FromSuccess();
                else
                    return PreconditionResult.FromError("Target user not found");

            if (guildUser.GetHierarchy() <= targetUser.GetHierarchy())
                return PreconditionResult.FromError("You cannot target anyone else whose roles are higher than yours");

            var currentUser = await context.Guild.GetCurrentUserAsync();
            if (currentUser?.GetHierarchy() < targetUser.GetHierarchy())
                return PreconditionResult.FromError("The bot's role is lower than the targeted user.");

            return PreconditionResult.FromSuccess();
        }
    }

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

    public class WriteableCommandContext : ICommandContext
    {
        public IDiscordClient Client { get; set; }
        public IGuild Guild { get; set; }
        public IMessageChannel Channel { get; set; }
        public IUser User { get; set; }
        public IUserMessage Message { get; set; }

        public bool IsPrivate => Channel is IPrivateChannel;
    }
}