using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy.Components.Filter
{
    public static class FilterUtilities
    {
        readonly static char[] splitters = @"#.,;/\|=_- ".ToCharArray();

        public static BadWord CheckForBadWords(this string message, BadWord[] badWords)
        {
            if (badWords.IsNullOrEmpty()) return null;

            //Checks for bad words
            StringBuilder sb = new StringBuilder();
            foreach (char c in message)
            {
                switch (c)
                {
                    case '@':
                    case '4':
                        sb.Append('a');
                        break;
                    case '8':
                        sb.Append('b');
                        break;
                    case '¢':
                        sb.Append('c');
                        break;
                    case '3':
                        sb.Append('e');
                        break;
                    case '!':
                        sb.Append('i');
                        break;
                    case '0':
                        sb.Append('o');
                        break;
                    case '$':
                        sb.Append('s');
                        break;
                    default:
                        if (!char.IsPunctuation(c) && !char.IsSymbol(c)) sb.Append(c);
                        break;
                }
            }

            string strippedMessage = sb.ToString();
            //splits string into words separated by space, '-' or '_'
            string[] messageParts = message.Split(splitters, StringSplitOptions.RemoveEmptyEntries);

            foreach (BadWord badWord in badWords)
            {
                if (badWord.partOfWord)
                {
                    if (strippedMessage.Contains(badWord.word, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return badWord;
                    }
                }
                else
                { //If bad word is ignored inside of words
                    foreach (string word in messageParts)
                    {
                        if (word.Equals(badWord.word, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return badWord;
                        }
                    }
                }
            }
            return null;
        }

        public static async Task FilterPunish(this SocketCommandContext context, string reason, ModerationSettings settings, float warnSize = 0.5f)
        {
            await context.FilterPunish(context.User as SocketGuildUser, reason, settings, delete: true, warnSize: warnSize);
        }

        public static async Task FilterPunish(this ICommandContext context, SocketGuildUser user, string reason, ModerationSettings settings, bool delete = true, float warnSize = 0.5f, string explicitInfo = "")
        {
            string jumpLink = await DiscordLogging.LogMessage(reason, context.Message, context.Guild, color: Color.Gold, authorOveride: user);
            await user.Warn(warnSize, reason, context.Channel as SocketTextChannel, logLink: jumpLink);
            LogSettings logSettings = context.Guild.LoadFromFile<LogSettings>(false);
            Task<IUserMessage> warnMessage = null;
            var pubLogChannel = await context.Guild.GetTextChannelAsync(logSettings?.pubLogChannel ?? 0);
            if (pubLogChannel != null)
            {
                warnMessage = pubLogChannel.SendMessageAsync($"{user.Mention} has been given their {user.LoadInfractions().Count.Suffix()} infraction because of {reason}");
            }
            else
            {
                if (settings?.anouncementChannels?.Contains(context.Channel.Id) ?? false) //If this channel is an anouncement channel
                    return;
                warnMessage = context.Channel.SendMessageAsync($"{user.Mention} has been given their {user.LoadInfractions().Count.Suffix()} infraction because of {reason}");
            }
            if (delete)
            {
                try
                {
                    DiscordLogging.deletedMessagesCache.Enqueue(context.Message.Id);
                    await context.Message.DeleteAsync();
                }
                catch (Exception e)
                {
                    await new LogMessage(LogSeverity.Warning, "Filter", "Error in removing message", e).Log();
                    await warnMessage?.Result?.ModifyAsync(msg => msg.Content += ", something went wrong removing the message.");
                }
            }
        }
    }
}
