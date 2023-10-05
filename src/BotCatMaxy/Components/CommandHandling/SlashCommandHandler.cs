using Discord;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Components.CommandHandling;
using BotCatMaxy.Services.Logging;
using Discord.Addons.Hosting;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using ExecuteResult = Discord.Interactions.ExecuteResult;
using IResult = Discord.Interactions.IResult;

namespace BotCatMaxy.Startup
{
    public class SlashCommandHandler : DiscordClientService
    {
        private readonly IDiscordClient _client;
        private readonly InteractionService _interaction;
        private readonly IServiceProvider _services;

        public SlashCommandHandler(IDiscordClient client, ILogger<SlashCommandHandler> logger,
            IServiceProvider services, InteractionService interaction)
            : base(client as DiscordSocketClient, logger)
        {
            _client = client;
            _services = services;
            _interaction = interaction;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
            => InitializeAsync(cancellationToken);

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (_client is DiscordSocketClient socketClient)
            {
                socketClient.Ready += async () =>
                {
#if DEBUG
                    await _interaction.RegisterCommandsToGuildAsync(285529027383525376);
#else
                    await interactionService.RegisterCommandsGloballyAsync();
#endif
                    LogSeverity.Info.Log("CMDs", "Registered commands");
                };
                // Hook the MessageReceived event into our command handler
                socketClient.InteractionCreated += async (x) =>
                {
                    var ctx = new SocketInteractionContext(socketClient, x);
                    await _interaction.ExecuteCommandAsync(ctx, _services);
                };
            };

            _interaction.SlashCommandExecuted += CommandExecuted;

            // See Dependency Injection guide for more information.
            await _interaction.AddModulesAsync(assembly: Assembly.GetAssembly(typeof(Program)),
                services: _services);
            int commands = _interaction.SlashCommands.Count + _interaction.ModalCommands.Count;
            await new LogMessage(LogSeverity.Info, "CMDs", $"{commands} slash commands set up").Log();
        }

        const string permissionRegex = @"(.+) requires (.+) permission (.+)\.";

        private async Task CommandExecuted(SlashCommandInfo slashCommandInfo, IInteractionContext context, IResult result)
        {
            if (result is InteractionResult commandResult)
            {
                if (!string.IsNullOrEmpty(result.ErrorReason))
                {
                    if (context.Interaction.HasResponded)
                        await context.Interaction.ModifyOriginalResponseAsync(properties =>
                        {
                            properties.Content = commandResult.ErrorReason;
                            properties.Embed = commandResult.Embed;
                        });
                    else
                    {
                        await context.Interaction.RespondAsync(commandResult.ErrorReason, embed: commandResult.Embed);
                    }
                }
                return;
            }

            if (result.IsSuccess)
            {
                return;
            }

            string message = null;
            //Idea is to override missing perm messages like "User requires guild permission BanMembers." to be more readable
            //like "You need server permission ban members."
            if (result.Error == InteractionCommandError.UnmetPrecondition)
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
            message ??= result.ErrorReason;
            if (context.Interaction.HasResponded)
                await context.Interaction.FollowupAsync(message);
            else
                await context.Interaction.RespondAsync(message, ephemeral: true);

            //Debug info
            if (result.Error is InteractionCommandError.Exception or InteractionCommandError.Unsuccessful)
                await LogCommandException(slashCommandInfo, context, result);
        }

        private async Task LogCommandException(Optional<SlashCommandInfo> command, IInteractionContext context, IResult result)
        {
            //To make any null variables clear and stand out if they shouldn't normally be null
            const string nullIndicator = "NULL_MESSAGE";
            var executeResult = result as ExecuteResult?;
            var exception = executeResult?.Exception;
            
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

            IUserMessage invocation = await context.Interaction.GetOriginalResponseAsync();
            if (context.Guild is not null)
                errorEmbed.AddField("Guild", $"{context.Guild.Name}\n({context.Guild.Id})", true);
            
            errorEmbed.AddField($"Message Content {invocation?.Id.ToString() ?? nullIndicator}",
                $"```{invocation?.Content ?? nullIndicator}```[Jump to Invocation]({invocation?.GetJumpUrl()})");
            errorEmbed.AddField("Exception", $"```{exception}```");
            await LogSeverity.Error.SendExceptionAsync("Command", logMessage, exception, errorEmbed);
        }
    }
}