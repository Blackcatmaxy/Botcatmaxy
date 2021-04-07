using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy.Components.Settings
{
    [Name("Slowmode")]
    [RequireUserPermission(ChannelPermission.ManageChannels)]
    [RequireBotPermission(ChannelPermission.ManageChannels)]
    public class SlowmodeCommands : ModuleBase<ICommandContext>
    {
        [Command("setslowmode"), Alias("setcooldown", "slowmodeset")]
        [Summary("Sets this channel's slowmode.")]
        public async Task SetSlowMode(int time)
        {
            await (Context.Channel as SocketTextChannel).ModifyAsync(channel => channel.SlowModeInterval = time);
            await ReplyAsync($"Set channel slowmode to {time} seconds");
        }

        [Command("setslowmode"), Alias("setcooldown", "slowmodeset")]
        [Summary("Sets this channel's slowmode.")]
        public async Task SetSlowMode(TimeSpan time)
        {
            if (time.TotalSeconds % 1 != 0)
            {
                await ReplyAsync("Can't set slowmode precision for less than a second");
                return;
            }
            await (Context.Channel as SocketTextChannel).ModifyAsync(channel => channel.SlowModeInterval = time.Seconds);
            await ReplyAsync($"Set channel slowmode to {time.LimitedHumanize()}");
        }

        [Command("dynamicslowmode"), Alias("ds"), Priority(5)]
        [Summary("Set the factor of dynamic slowmode.")]
        [HasAdmin]
        public async Task DynamicSlowmode(double factor)
        {
            if (factor < 0)
            {
                await ReplyAsync("Why would you need a factor that low?");
                return;
            }

            SocketTextChannel channel = Context.Channel as SocketTextChannel;
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            string channelId = Convert.ToString(channel.Id);

            if (factor == 0)
            {
                settings.dynamicSlowmode.Remove(channelId);
                await ReplyAsync(channel.Mention + " no longer has dynamic slowmode.");
            }
            else
            {
                if (!settings.dynamicSlowmode.ContainsKey(channelId))
                {
                    settings.dynamicSlowmode.Add(channelId, factor);
                }
                else
                {
                    if (settings.dynamicSlowmode[channelId] == factor)
                    {
                        await ReplyAsync(channel.Mention + " already has a dynamic slowmode with a factor of " + factor + ".");
                        return;
                    }

                    settings.dynamicSlowmode[channelId] = factor;
                }

                await ReplyAsync(channel.Mention + " now has a dynamic slowmode with a factor of " + factor + ".");
            }

            settings.SaveToFile();
        }

        readonly string[] disableStrings = { "null", "off", "none" };

        [Command("dynamicslowmode"), Alias("ds")]
        [Summary("Set the factor of dynamic slowmode. Pass `null` or `off` to disable.")]
        [HasAdmin]
        public async Task DynamicSlowmode(string disable)
        {
            if (disableStrings.Contains(disable.ToLowerInvariant()))
            {
                await ReplyAsync("Input not understood");
                return;
            }

            SocketTextChannel channel = Context.Channel as SocketTextChannel;
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
            string channelId = Convert.ToString(channel.Id);

            if (settings?.dynamicSlowmode == null || !settings.dynamicSlowmode.ContainsKey(channelId))
            {
                await ReplyAsync(channel.Mention + " doesn't have dynamic slowmode.");
                return;
            }

            settings.dynamicSlowmode.Remove(channelId);
            await ReplyAsync(channel.Mention + " no longer has dynamic slowmode.");
            settings.SaveToFile();
        }
    }
}
