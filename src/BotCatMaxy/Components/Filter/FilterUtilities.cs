using BotCatMaxy.Components.Logging;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BotCatMaxy.Components.Filter
{
    public static class FilterUtilities
    {
        readonly static char[] splitters = @"#.,/\|=_- ".ToCharArray();

        public static (BadWord word, int? index) CheckForBadWords(this string message, BadWord[] badWords)
        {
            if (badWords?.Length is null or 0) return (null, null);

            //Checks for bad words
            StringBuilder sb = new StringBuilder();
            foreach (char c in message)
            {
                switch (c)
                {
                    case '​': //zero width unicode needs to be removed
                        break;
                    case '@':
                    case '4':
                        sb.Append('a');
                        break;
                    case '¢':
                        sb.Append('c');
                        break;
                    case '3':
                        sb.Append('e');
                        break;
                    case '1':
                    case '!':
                        sb.Append('i');
                        break;
                    case '0':
                        sb.Append('o');
                        break;
                    case '5':
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
                if (badWord.PartOfWord)
                {
                    int index = strippedMessage.IndexOf(badWord.Word, StringComparison.InvariantCultureIgnoreCase);
                    if (index > -1)
                        return (badWord, index);
                }
                else
                    //If bad word is ignored inside of words
                    foreach (string word in messageParts)
                        if (word.Equals(badWord.Word, StringComparison.InvariantCultureIgnoreCase))
                            return (badWord, null);
            return (null, null);
        }

        public static async Task FilterPunish(this ICommandContext context, string reason, ModerationSettings modSettings, FilterSettings filterSettings, string badText, int? index = null, float warnSize = 0.5f)
        {
            await context.FilterPunish(context.User as IGuildUser, reason, modSettings, filterSettings, badText, index: index, delete: true, warnSize: warnSize);
        }

        public static async Task FilterPunish(this ICommandContext context, IGuildUser user, string reason, ModerationSettings modSettings, FilterSettings filterSettings, string badText, int? index = null, bool delete = true, float warnSize = 0.5f)
        {
            string content = context.Message.Content;
            if (badText != null) //will be null in case of reaction warn where reason speaks for itself
            {
                if (badText == content)
                {
                    content = $"**[{badText}]**";
                }
                else
                {
                    int badTextStart = index ?? content.IndexOf(badText);
                    int badTextEnd = badTextStart + badText.Length;
                    content = content.Insert(badTextStart, "**[");
                    content = content.Insert(badTextEnd + 3, "]**");
                }
            }
            else
                content = null;

            string jumpLink = await DiscordLogging.LogMessage(reason, context.Message, context.Guild, color: Color.Gold, authorOveride: user, textOverride: content);
            await user.Warn(warnSize, reason, context.Channel as ITextChannel, logLink: jumpLink);

            if (filterSettings?.announcementChannels?.Contains(context.Channel.Id) ?? false) //If this channel is an anouncement channel
                return;

            Task<IUserMessage> warnMessage = await NotifyPunish(context, user, reason, modSettings, content);

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

        public const string notifyInfoRegex = @"<@!?(\d+)> has been given their (\d+)\w+-?(?:\d+\w+)? infraction because of (.+)";

        public static async Task<Task<IUserMessage>> NotifyPunish(ICommandContext context, IGuildUser user, string reason, ModerationSettings settings, string highlight)
        {
            Task<IUserMessage> warnMessage = null;
            LogSettings logSettings = context.Guild.LoadFromFile<LogSettings>(false);

            string infractionAmount = user.LoadInfractions().Count.Suffix();

            var embed = new EmbedBuilder()
                .WithTitle($"Filter warning in {context.Guild.Name} for {reason.ToLower()}")
                .WithColor(Color.Gold)
                .WithGuildAsAuthor(context.Guild)
                .WithCurrentTimestamp();
            if (highlight != null) embed.WithDescription(highlight);
            await user.TryNotify(embed.Build());

            var messages = (await context.Channel.GetMessagesAsync(5).FlattenAsync()).Where(msg => msg.Author.Id == context.Client.CurrentUser.Id);
            //If need to go from "@person has been given their 3rd infractions because of fdsfd" to "@person has been given their 3rd-4th infractions because of fdsfd"
            if (messages.MatchInMessages(user.Id, out Match match, out IMessage message))
            {
                int oldInfraction = int.Parse(match.Groups[2].Value);
                await (message as IUserMessage).ModifyAsync(msg => msg.Content = $"{user.Mention} has been given their {oldInfraction.Suffix()}-{infractionAmount} infraction because of {reason}");
                return null;
            }
            else
            {
                //Public channel nonsense if someone want a public log (don't think this has been used since the old Vesteria Discord but backward compat)
                string toSay = $"{user.Mention} has been given their {infractionAmount} infraction because of {reason}";
                var pubLogChannel = await context.Guild.GetTextChannelAsync(logSettings?.pubLogChannel ?? 0);
                if (pubLogChannel != null)
                    warnMessage = pubLogChannel.SendMessageAsync(toSay);
                else
                    warnMessage = context.Channel.SendMessageAsync(toSay);
                return warnMessage;
            }
        }

        public static bool MatchInMessages(this IEnumerable<IMessage> messages, ulong userID, out Match match, out IMessage successMessage)
        {
            match = null;
            successMessage = null;
            if (!messages.Any()) return false;
            foreach (IMessage message in messages.ToArray().OrderBy(msg => msg.Timestamp))
            {
                var result = Regex.Match(message.Content, notifyInfoRegex);
                if (result.Success && result.Groups[1].Value == userID.ToString())
                {
                    successMessage = message;
                    match = result;
                    return true;
                }
            }

            return false;
        }
    }
}
