using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using System.Diagnostics.Contracts;
using System.Collections.Immutable;
using Discord.Addons.Interactive;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Reflection;
using Discord.WebSocket;
using Discord.Commands;
using Discord.Rest;
using System.Linq;
using BotCatMaxy;
using Discord;
using System;

namespace BotCatMaxy {
    public class CommandHandler {
        public readonly string[] ignoredCMDErrors = { "User not found.", "The input text has too few parameters.", "Invalid context for command; accepted contexts: Guild." };
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        public readonly IServiceProvider services;
        //private SwearFilter filter;

        public CommandHandler(DiscordSocketClient client, CommandService commands) {
            _commands = commands;
            _client = client;
            services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(new InteractiveService(client))
                .BuildServiceProvider();
            _ = InstallCommandsAsync();
        }

        public async Task InstallCommandsAsync() {
            try {
                // Hook the MessageReceived event into our command handler
                _client.MessageReceived += HandleCommandAsync;

                //Adds Emoji type reader
                _commands.AddTypeReader(typeof(Emoji), new EmojiTypeReader());
                _commands.AddTypeReader(typeof(UserRef), new UserRefTypeReader());
                _commands.AddTypeReader(typeof(IUser), new BetterUserTypeReader());

                // See Dependency Injection guide for more information.
                await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                                services: services);
                await new LogMessage(LogSeverity.Info, "CMDs", "Commands set up").Log();
            } catch (Exception e) {
                await new LogMessage(LogSeverity.Critical, "CMDs", "Commands set up failed", e).Log();
            }
        }

        private async Task HandleCommandAsync(SocketMessage messageParam) {
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
            var result = await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: services);

            //may clog up the request queue should a user spam a command.
            if (!result.IsSuccess && result.ErrorReason != "Unknown command.") {
                await context.Channel.SendMessageAsync(result.ErrorReason);
                if (ignoredCMDErrors.Contains(result.ErrorReason)) await new LogMessage(LogSeverity.Warning, "CMDs", result.ErrorReason).Log();
            }
        }
    }
}

namespace Discord.Commands {
    public class UserRef {
        public readonly SocketGuildUser gUser;
        public readonly SocketUser user;
        public readonly ulong ID;

        public UserRef(SocketGuildUser gUser) {
            Contract.Requires(gUser != null);
            this.gUser = gUser;
            user = gUser;
            ID = gUser.Id;
        }

        public UserRef(SocketUser user) {
            Contract.Requires(user != null);
            this.user = user;
            ID = user.Id;
        }

        public UserRef(ulong ID) => this.ID = ID;

        public UserRef(UserRef userRef, SocketGuild guild) {
            user = userRef.user;
            ID = userRef.ID;
            gUser = guild.GetUser(ID);
        }
    }

    public class UserRefTypeReader : TypeReader {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
            IReadOnlyCollection<IGuildUser> guildUsers = ImmutableArray.Create<IGuildUser>();
            SocketGuildUser gUserResult = null;
            SocketUser userResult = null;
            ulong? IDResult = null;

            if (context.Guild != null)
                guildUsers = await context.Guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false);

            //By Mention (1.0)
            if (MentionUtils.TryParseUser(input, out var id)) {
                if (context.Guild != null)
                    gUserResult = await context.Guild.GetUserAsync(id, CacheMode.AllowDownload) as SocketGuildUser;
                if (gUserResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(gUserResult));
                else
                    userResult = await context.Client.GetUserAsync(id, CacheMode.AllowDownload) as SocketUser;
                if (userResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(userResult));
                else
                    return TypeReaderResult.FromSuccess(new UserRef(id));
            }

            //By Id (0.9)
            if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id)) {
                if (context.Guild != null)
                    gUserResult = await context.Guild.GetUserAsync(id, CacheMode.AllowDownload) as SocketGuildUser;
                if (gUserResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(gUserResult));
                else
                    userResult = await context.Client.GetUserAsync(id, CacheMode.AllowDownload) as SocketUser; 
                if (userResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(userResult));
                else
                    return TypeReaderResult.FromSuccess(new UserRef(id));
            }

            return TypeReaderResult.FromError(CommandError.ObjectNotFound, "User not found.");
        }
    }

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

            if (guildUser.Hierarchy < targetUser.Hierarchy)
                return PreconditionResult.FromError("You cannot target anyone else whose roles are higher than yours");

            var currentUser = await context.Guild.GetCurrentUserAsync().ConfigureAwait(false) as SocketGuildUser;
            if (currentUser?.Hierarchy < targetUser.Hierarchy)
                return PreconditionResult.FromError("The bot's role is lower than the targeted user.");

            return PreconditionResult.FromSuccess();
        }
    }

    public class BetterUserTypeReader : UserTypeReader<IUser> {
        public override async Task<TypeReaderResult> ReadAsync(
            ICommandContext context,
            string input,
            IServiceProvider services) {
            var result = await base.ReadAsync(context, input, services);
            if (result.IsSuccess)
                return result;
            else {
                DiscordRestClient restClient = (context.Client as DiscordSocketClient).Rest;
                if (MentionUtils.TryParseUser(input, out var id)) {
                    RestUser user = await restClient.GetUserAsync(id);
                    if (user != null) return TypeReaderResult.FromSuccess(user);
                }
                if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id)) {
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