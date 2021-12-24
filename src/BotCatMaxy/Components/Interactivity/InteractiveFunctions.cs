using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Interactive;
#nullable enable
namespace BotCatMaxy.Components.Interactivity;

public static class InteractiveFunctions
{
    private static readonly Emoji _confirmEmoji = new("\U00002705");
    private static readonly Emoji _cancelEmoji = new("\U0000274c");

    /// <summary>
    /// Adds confirm and deny reactions to a message and then waits for the author to react.
    /// </summary>
    /// <param name="message">The message to check for reactions from</param>
    /// <param name="timeout">The time to wait before the methods returns a timeout result. Defaults to 2 minutes.</param>
    /// <returns>Returns true if didn't time out and confirm is selected</returns>
    public static async Task<bool> TryConfirmation(this InteractiveService service, IMessage message, TimeSpan? timeout = null)
    {
        await message.AddReactionAsync(_cancelEmoji);
        await message.AddReactionAsync(_confirmEmoji);
        var reaction = await service.NextReactionAsync(reaction
            => reaction.MessageId == message.Id && reaction.UserId == message.Author.Id, timeout: timeout ?? TimeSpan.FromMinutes(2));

        return (reaction.IsSuccess && Equals(reaction.Value.Emote, _confirmEmoji));
    }

    public static async Task<IGuild?> QueryMutualGuild(this InteractiveService service, ICommandContext context)
    {
        var mutualGuilds = (await context.Message.Author.GetMutualGuildsAsync(context.Client)).ToImmutableArray();

        var guildsEmbed = new EmbedBuilder();
        guildsEmbed.WithTitle("Reply with the number next to the guild you want to check the infractions from");

        for (var i = 0; i < mutualGuilds.Length; i++)
        {
            guildsEmbed.AddField($"[{i + 1}] {mutualGuilds[i].Name}", mutualGuilds[i].Id);
        }

        await context.Channel.SendMessageAsync(embed: guildsEmbed.Build());
        while (true)
        {
            Task<InteractiveResult<SocketMessage?>> task = service.NextMessageAsync(msg => msg.Channel.Id == context.Channel.Id, timeout: TimeSpan.FromMinutes(1));
            IMessage? message = (await task).Value;
            if (message?.Content is null or "cancel")
            {
                return null;
            }

            if (byte.TryParse(message.Content, out byte index) && index > 0)
                return mutualGuilds[index - 1];
            else
                await context.Channel.SendMessageAsync("Invalid number, please reply again with a valid number or ``cancel``");
        }
    }
}