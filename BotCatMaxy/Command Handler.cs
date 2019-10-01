using System;
using Discord;
using System.Collections.Generic;
using System.Text;
using Discord.WebSocket;
using Discord.Commands;
using System.Threading.Tasks;
using System.Reflection;
using BotCatMaxy;
using System.Text.RegularExpressions;

namespace BotCatMaxy {
    public class CommandHandler {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        //private SwearFilter filter;

        public CommandHandler(DiscordSocketClient client, CommandService commands) {
            _commands = commands;
            _client = client;

            _ = InstallCommandsAsync();
        }

        public async Task InstallCommandsAsync() {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            //Adds Emoji type reader
            _commands.AddTypeReader(typeof(Emoji), new EmojiTypeReader());

            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: null);
            await new LogMessage(LogSeverity.Info, "CMDs", "Commands set up").Log();
        }

        private async Task HandleCommandAsync(SocketMessage messageParam) {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
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
            var result = await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: null);

            //may clog up the request queue should a user spam a command.
            if (!result.IsSuccess && result.ErrorReason != "Unknown command.") {
                await context.Channel.SendMessageAsync(result.ErrorReason);
                await new LogMessage(LogSeverity.Warning, "Commands", result.ErrorReason).Log();
            }
        }
    }
}

namespace Discord.Commands {
    public class CanWarnAttribute : PreconditionAttribute {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) {
            //Makes sure it's in a server
            if (context.User is SocketGuildUser gUser) {
                // If this command was executed by a user with the appropriate role, return a success
                if (gUser.CanWarn())
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
            } else
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }
    public class HasAdminAttribute : PreconditionAttribute {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) {
            //Makes sure it's in a server
            if (context.User is SocketGuildUser gUser) {
                // If this command was executed by a user with administrator permission, return a success
                if (gUser.HasAdmin())
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You don't have the permissions to use this."));
            } else
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }

    public class EmojiTypeReader : TypeReader {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            string regex = @"<(a?):(\w+):(\d+)>";
            Match match = Regex.Match(input, regex); //Check if it's custom discord emoji
            if (match.Success) {
                return await Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "This is a custom emoji not a normal one, if you beleive they should work on this command make an issue on the GitHub over at !help"));
            }
            Emoji emoji = new Emoji(input);
            try {
                await context.Message.AddReactionAsync(emoji);
                await context.Message.RemoveReactionAsync(emoji, context.Client.CurrentUser);
            } catch {
                return await Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "That is not a valid emoji"));
            }
            return await Task.FromResult(TypeReaderResult.FromSuccess(emoji));
        }
    }

    public class RequireHierarchyAttribute : ParameterPreconditionAttribute {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            ParameterInfo parameter, object value, IServiceProvider services) {
            // Hierarchy is only available under the socket variant of the user.
            if (!(context.User is SocketGuildUser guildUser))
                return PreconditionResult.FromError("This command cannot be used outside of a guild");

            SocketGuildUser targetUser;
            switch (value) {
                case SocketGuildUser targetGuildUser:
                    targetUser = targetGuildUser;
                    break;
                case ulong userId:
                    targetUser = await context.Guild.GetUserAsync(userId).ConfigureAwait(false) as SocketGuildUser;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (targetUser == null)
                return PreconditionResult.FromError("Target user not found");

            if (guildUser.Hierarchy < targetUser.Hierarchy)
                return PreconditionResult.FromError("You cannot target anyone else whose roles are higher than yours");

            var currentUser = await context.Guild.GetCurrentUserAsync().ConfigureAwait(false) as SocketGuildUser;
            if (currentUser?.Hierarchy < targetUser.Hierarchy)
                return PreconditionResult.FromError("The bot's role is lower than the targeted user.");

            return PreconditionResult.FromSuccess();
        }
    }
}