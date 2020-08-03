using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy.Components.Settings
{
    [Name("Slowmode")]
    public class SlowmodeCommands : ModuleBase
    {
        [Command("setslowmode"), Alias("setcooldown", "slowmodeset")]
        [Summary("Sets this channel's slowmode.")]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task SetSlowMode(int time)
        {
            await (Context.Channel as SocketTextChannel).ModifyAsync(channel => channel.SlowModeInterval = time);
            await ReplyAsync($"Set channel slowmode to {time} seconds");
        }

        [Command("setslowmode"), Alias("setcooldown", "slowmodeset")]
        [Summary("Sets this channel's slowmode.")]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task SetSlowMode(string time)
        {
            var amount = time.ToTime();
            if (amount == null)
            {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalSeconds % 1 != 0)
            {
                await ReplyAsync("Can't set slowmode precision for less than a second");
                return;
            }
            await (Context.Channel as SocketTextChannel).ModifyAsync(channel => channel.SlowModeInterval = (int)amount.Value.TotalSeconds);
            await ReplyAsync($"Set channel slowmode to {amount.Value.LimitedHumanize()}");
        }

        [Command("dynamicslowmode"), Alias("ds"), Priority(5)]
        [Summary("Set the factor of dynamic slowmode.")]
        [HasAdmin]
        public async Task DynamicSlowmode(double factor)
        {
            SocketTextChannel channel = Context.Channel as SocketTextChannel;
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (factor <= 0)
            {
                await ReplyAsync("Why would you need a factor that low?");
                return;
            }

            if (settings.dynamicSlowmode[channel.Id] == factor)
            {
                await ReplyAsync(channel.Mention + " already has a dynamic slowmode with a factor of " + factor + ".");
                return;
            }
            else
            {
                if (factor == 0)
                {
                    settings.dynamicSlowmode.Remove(channel.Id);
                    await ReplyAsync(channel.Mention + " no longer has dynamic slowmode.");
                }
                else
                {
                    if (!settings.dynamicSlowmode.ContainsKey(channel.Id))
                    {
                        settings.dynamicSlowmode.Add(channel.Id, factor);
                    }
                    else
                    {
                        settings.dynamicSlowmode[channel.Id] = factor;
                    }

                    await ReplyAsync(channel.Mention + " now has a dynamic slowmode with a factor of " + factor + ".");
                }

                settings.SaveToFile();
            }
        }

        [Command("dynamicslowmode"), Alias("ds")]
        [Summary("Set the factor of dynamic slowmode. Pass `null` or `off` to disable.")]
        [HasAdmin]
        public async Task DynamicSlowmode(string disable)
        {
            SocketTextChannel channel = Context.Channel as SocketTextChannel;
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings.dynamicSlowmode[channel.Id] == null)
            {
                await ReplyAsync(channel.Mention + " doesn't have dynamic slowmode.");
                return;
            }
            else if (disable == "null" || disable == "off")
            {
                settings.dynamicSlowmode.Remove(channel.Id);
                await ReplyAsync(channel.Mention + " no longer has dynamic slowmode.");

                settings.SaveToFile();
            }
        }
    }
}
