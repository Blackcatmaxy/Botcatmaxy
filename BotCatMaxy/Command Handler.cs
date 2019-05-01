using System;
using Discord;
using System.Collections.Generic;
using System.Text;
using Discord.WebSocket;
using Discord.Commands;
using System.Threading.Tasks;
using System.Reflection;
using BotCatMaxy;

namespace BotCatMaxy {
    public class CommandHandler {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        //private SwearFilter filter;

        public CommandHandler(DiscordSocketClient client, CommandService commands) {
            _commands = commands;
            _client = client;
        }

        public async Task InstallCommandsAsync() {
            //filter = new SwearFilter();
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: null);
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

            // Optionally, we may inform the user if the command fails
            // to be executed; however, this may not always be desired,
            // as it may clog up the request queue should a user spam a
            // command.
             if (!result.IsSuccess && result.ErrorReason != "Unknown command.") {
                await context.Channel.SendMessageAsync(result.ErrorReason);
                Console.WriteLine(new LogMessage(LogSeverity.Error, "Commands", result.ErrorReason));
             }
        }
    }
}
/*
namespace Discord.Commands {
    // Inherit from PreconditionAttribute
    public class ModAttribute : CommandAttribute {
        public Task<PreconditionResult> NeedAdmin(ICommandContext context, CommandInfo command, IServiceProvider services) {
            // Check if this user is a Guild User, which is the only context where roles exist
            if (context.Guild == null) {
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
            }
            if (Utilities.HasAdmin(context.Message.Author as SocketGuildUser)) {
                return Task.FromResult(PreconditionResult.FromSuccess());
            } else {
                context.Channel.SendMessageAsync("You do not have the permission");
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
            }
        }
    }
}*/