using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public static class NotifyUtilities
    {
        /// <summary>
        /// Embeds a DM to a user about an action with a reason
        /// </summary>
        public static async Task Notify(this IUser user, string action, string reason, IGuild guild, IUser author = null, Color color = default, string appealLink = "")
        {
            var newReason = reason;

            if (color == default) color = Color.LightGrey;
            var embed = new EmbedBuilder()
                .AddField($"You have been {action}", reason)
                .WithCurrentTimestamp()
                .WithGuildAsAuthor(guild)
                .WithColor(color);

            if (!appealLink.IsNullOrEmpty())
                embed.AddField("Appeal", $"**[Click here]({appealLink})** if you'd like to appeal.");

            if (author != null) embed.WithFooter($"Done by {author.Username}#{author.Discriminator}", author.GetAvatarUrl());
            await user.TryNotify(embed.Build());
        }

        /// <summary>
        /// Sends a simple message to a user's DMs
        /// </summary>
        /// <returns>if the message succeeded or not</returns>
        public static async Task<bool> TryNotify(this IUser user, string message)
        {
            try
            {
                var sentMessage = await user?.SendMessageAsync(message);
                if (sentMessage == null) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Embeds a message to a user's DMs
        /// </summary>
        /// <returns>if the message succeeded or not</returns>
        public static async Task<bool> TryNotify(this IUser user, Embed embed)
        {
            try
            {
                var sentMessage = await user?.SendMessageAsync(embed: embed);
                if (sentMessage == null) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///Sets the author field of the <seealso cref="EmbedBuilder"/> using the supplied guild info
        /// </summary>
        public static EmbedBuilder WithGuildAsAuthor(this EmbedBuilder embed, IGuild guild)
            => embed.WithAuthor(guild.Name, guild.IconUrl);

        /// <summary>
        ///Sets the footer field of the <seealso cref="EmbedBuilder"/> using the supplied guild info
        /// </summary>
        public static EmbedBuilder WithGuildAsFooter(this EmbedBuilder embed, IGuild guild, string extra = null)
        {
            string text = guild.Name;
            if (extra != null) text += $" • {extra}";
            return embed.WithFooter(text, guild.IconUrl);
        }
    }
}
