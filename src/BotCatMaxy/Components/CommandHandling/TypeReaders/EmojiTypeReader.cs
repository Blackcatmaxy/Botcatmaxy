using Discord;
using Discord.Commands;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BotCatMaxy.TypeReaders
{
    public class EmojiTypeReader : TypeReader
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            string regex = @"<(a?):(\w+):(\d+)>";
            Match match = Regex.Match(input, regex); //Check if it's custom discord emoji
            if (match.Success)
            {
                return await Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "This is a custom emoji not a normal one, if you believe they should work on this command make an issue on the GitHub over at !help"));
            }
            Emoji emoji = new Emoji(input);
            try
            {
                await context.Message.AddReactionAsync(emoji);
                await context.Message.RemoveReactionAsync(emoji, context.Client.CurrentUser);
            }
            catch
            {
                return await Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "That is not a valid emoji"));
            }
            return await Task.FromResult(TypeReaderResult.FromSuccess(emoji));
        }
    }
}
