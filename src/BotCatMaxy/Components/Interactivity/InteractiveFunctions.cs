using System;
using System.Threading.Tasks;
using Discord;
using Fergun.Interactive;

namespace BotCatMaxy.Components.Interactivity
{
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
    }
}