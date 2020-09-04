using BotCatMaxy;
using BotCatMaxy.Models;
using BotCatMaxy.TypeReaders;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Reflection;
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
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        public readonly IServiceProvider services;

        public CommandHandler(DiscordSocketClient client, CommandService commands)
        {
            _commands = commands;
            _client = client;
            services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(new InteractiveService(client))
                .BuildServiceProvider();
            _ = InstallCommandsAsync();
        }

        public async Task InstallCommandsAsync()
        {
            try
            {
                // Hook the MessageReceived event into our command handler
                _client.MessageReceived += HandleCommandAsync;

                //Post Execution handling
                _commands.Log += ExceptionLogging.Log;
                _commands.CommandExecuted += CommandExecuted;

                //Adds custom type readers
                _commands.AddTypeReader(typeof(Emoji), new EmojiTypeReader());
                _commands.AddTypeReader(typeof(UserRef), new UserRefTypeReader());
                _commands.AddTypeReader(typeof(IUser), new BetterUserTypeReader());

                // See Dependency Injection guide for more information.
                await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                                services: services);
                await new LogMessage(LogSeverity.Info, "CMDs", "Commands set up").Log();
            }
            catch (Exception e)
            {
                await new LogMessage(LogSeverity.Critical, "CMDs", "Commands set up failed", e).Log();
            }
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            SocketUserMessage message = messageParam as SocketUserMessage;
            if (message == null)
                return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

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

        private async Task CommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
            {
                await context.Channel.SendMessageAsync(result.ErrorReason.Truncate(1500));
                if (result.Error != null && (result.Error == CommandError.Exception || result.Error == CommandError.Unsuccessful))
                {
                    string message = $"Command !{command.Value?.Name} in";
                    if (context.Guild != null)
                    {
                        message += $" {await context.Guild.Describe()} owned by {(await context.Guild.GetOwnerAsync()).Describe()}";
                    }
                    else
                    {
                        message += $" {context.User.Describe()} DMs";
                    }
                    message += $" used as \"{context.Message}\" encountered: result type \"{result.GetType().Name}\", \"{result.ErrorReason}\"";
                    await new LogMessage(LogSeverity.Error, "CMDs", message, (result as ExecuteResult?)?.Exception).Log();
                }
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
            if (context.User is SocketGuildUser gUser)
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
            if (context.User is SocketGuildUser gUser)
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
            // Hierarchy is only available under the socket variant of the user.
            if (!(context.User is SocketGuildUser guildUser))
                return PreconditionResult.FromError("This command cannot be used outside of a guild");
            var targetUser = value switch
            {
                UserRef userRef => userRef.gUser as SocketGuildUser,
                SocketGuildUser targetGuildUser => targetGuildUser,
                ulong userId => await context.Guild.GetUserAsync(userId).ConfigureAwait(false) as SocketGuildUser,
                _ => throw new ArgumentOutOfRangeException("Unkown Type used in parameter that requires hierarchy"),
            };
            if (targetUser == null)
                if (value is UserRef)
                    return PreconditionResult.FromSuccess();
                else
                    return PreconditionResult.FromError("Target user not found");

            if (guildUser.Hierarchy <= targetUser.Hierarchy)
                return PreconditionResult.FromError("You cannot target anyone else whose roles are higher than yours");

            var currentUser = await context.Guild.GetCurrentUserAsync().ConfigureAwait(false) as SocketGuildUser;
            if (currentUser?.Hierarchy < targetUser.Hierarchy)
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