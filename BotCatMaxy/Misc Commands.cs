using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using Newtonsoft.Json;
using System.Text;
using System.Linq;
using BotCatMaxy;
using System.IO;
using Discord;
using System;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Diagnostics;
using Discord.Addons.Preconditions;
using Humanizer;
using Discord.Rest;
using Discord.Addons.Preconditions;
using BotCatMaxy.Cache;
using BotCatMaxy.Models;
using System.Net.Sockets;

namespace BotCatMaxy
{
    public class MiscCommands : ModuleBase<SocketCommandContext>
    {
        public object TempActs { get; private set; }
        private readonly CommandService _service;

        public MiscCommands(CommandService service)
        {
            _service = service;
        }

        [Command("help"), Alias("botinfo", "commands")]
        [Summary("View Botcatmxy resources.")]
        public async Task Help()
        {
            var embed = new EmbedBuilder
            {
                Description = "Say !dmhelp in the bot's dms for a full list of commands you can use."
            };
            embed.AddField("To see commands", "[Click here](https://github.com/Blackcatmaxy/Botcatmaxy/wiki)", true);
            embed.AddField("Report issues and contribute at", "[Click here for GitHub link](http://bot.blackcatmaxy.com)", true);
            await ReplyAsync(embed: embed.Build());
        }

        [Command("dmhelp"), Alias("dmbotinfo", "dmcommands")]
        [Summary("DM's a list of commands you can use.")]
        [RequireContext(ContextType.DM)]
        public async Task DMHelp()
        {
            EmbedBuilder extraHelpEmbed = new EmbedBuilder();
            extraHelpEmbed.AddField("Wiki", "[Click Here](https://github.com/Blackcatmaxy/Botcatmaxy/wiki)", true);
            extraHelpEmbed.AddField("Submit bugs, enhancements, and contribute", "[Click Here](http://bot.blackcatmaxy.com)", true);

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = "Commands",
                Description = "You are viewing all commands you have permission to use."
            };

            IReadOnlyCollection<SocketGuild> guilds = Context.User.MutualGuilds;

            foreach (ModuleInfo module in _service.Modules)
            {
                string description = "";
                foreach (CommandInfo command in module.Commands)
                {
                    bool isAllowed = false;

                    foreach (SocketGuild guild in guilds)
                    {
                        WriteableCommandContext tmpCtx = new WriteableCommandContext
                        {
                            Client = Context.Client,
                            Message = Context.Message,
                            Guild = guild,
                            Channel = (IMessageChannel)guild.Channels.First(),
                            User = guild.GetUser(Context.User.Id)
                        };

                        PreconditionResult check = await command.CheckPreconditionsAsync(tmpCtx);

                        if (check.IsSuccess)
                        {
                            isAllowed = true;
                            break;
                        }
                    }

                    if (isAllowed)
                    {
                        description += $"!{command.Aliases[0]}\n";
                    }
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    embed.AddField(new EmbedFieldBuilder
                    {
                        Name = module.Name,
                        Value = description
                    }); ;
                }
            }

            await Context.User.SendMessageAsync(embed: extraHelpEmbed.Build());
            await Context.User.SendMessageAsync(embed: embed.Build());
        }

        [Command("dmhelp"), Alias("dmbotinfo", "dmcommands")]
        [Summary("DM's a list of commands you can use.")]
        [RequireContext(ContextType.DM)]
        public async Task DMHelp(string commandName)
        {
            SearchResult res = _service.Search(Context, commandName);

            if (!res.IsSuccess)
            {
                await ReplyAsync($"`!{commandName}` isn't a command.");
                return;
            }

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = "Commands",
                Description = $"Viewing search results you can use for `!{commandName}`."
            };

            IReadOnlyCollection<SocketGuild> guilds = Context.User.MutualGuilds;

            foreach (CommandMatch match in res.Commands)
            {
                CommandInfo command = match.Command;
                bool isAllowed = false;

                foreach (SocketGuild guild in guilds)
                {
                    WriteableCommandContext tmpCtx = new WriteableCommandContext
                    {
                        Client = Context.Client,
                        Message = Context.Message,
                        Guild = guild,
                        Channel = (IMessageChannel)guild.Channels.First(),
                        User = guild.GetUser(Context.User.Id)
                    };

                    PreconditionResult check = await command.CheckPreconditionsAsync(tmpCtx);

                    if (check.IsSuccess)
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (isAllowed)
                {
                    string args = "";

                    foreach (ParameterInfo param in command.Parameters)
                    {
                        args += $"[{param.Name}] ";
                    }

                    embed.AddField(new EmbedFieldBuilder
                    {
                        Name = $"!{command.Aliases[0]} {args}",
                        Value = command.Summary ?? "*No description.*"
                    });
                }
            }

            await Context.User.SendMessageAsync(embed: embed.Build());
        }

        [Command("checkperms")]
        [Summary("Check if the required permissions are given.")]
        [RequireUserPermission(GuildPermission.BanMembers, Group = "Permission")]
        [RequireOwner(Group = "Permission")]
        public async Task CheckPerms()
        {
            GuildPermissions perms = Context.Guild.CurrentUser.GuildPermissions;
            var embed = new EmbedBuilder();
            embed.AddField("Manage roles", perms.ManageRoles, true);
            embed.AddField("Manage messages", perms.ManageMessages, true);
            embed.AddField("Kick", perms.KickMembers, true);
            embed.AddField("Ban", perms.BanMembers, true);
            await ReplyAsync(embed: embed.Build());
        }

        /*[RequireOwner()]
        [Command("bottest")]
        public async Task TestCommand() {
            await ReplyAsync($"Your message had {Context.Message.Content.Count(c => c == '\n')}");
        }*/

        public void ErrorTest()
        {
            throw new InvalidOperationException();
        }

        [RequireOwner]
        [Command("stats")]
        [Summary("View bot statistics.")]
        [Alias("statistics")]
        public async Task Statistics()
        {
            var embed = new EmbedBuilder();
            embed.WithTitle("Statistics");
            embed.AddField($"Part of", $"{Context.Client.Guilds.Count} discord guilds", true);
            ulong infractions24Hours = 0;
            ulong totalInfractons = 0;
            ulong members = 0;
            uint tempBannedPeople = 0;
            uint tempMutedPeople = 0;
            foreach (SocketGuild guild in Context.Client.Guilds)
            {
                members += (ulong)guild.MemberCount;
                var collection = guild.GetInfractionsCollection(false);

                if (collection != null)
                {
                    using var cursor = collection.Find(new BsonDocument()).ToCursor();
                    foreach (var doc in cursor.ToList())
                    {
                        foreach (Infraction infraction in BsonSerializer.Deserialize<UserInfractions>(doc).infractions)
                        {
                            if (DateTime.UtcNow - infraction.time < TimeSpan.FromHours(24))
                                infractions24Hours++;
                            totalInfractons++;
                        }
                    }
                }

                TempActionList tempActionList = guild.LoadFromFile<TempActionList>(false);
                tempBannedPeople += (uint)(tempActionList?.tempBans?.Count ?? 0);
                tempMutedPeople += (uint)(tempActionList?.tempMutes?.Count ?? 0);
            }
            embed.AddField("Totals", $"{members} users || {totalInfractons} total infractions", true);
            embed.AddField("In the last 24 hours", $"{infractions24Hours} infractions given", true);
            embed.AddField("Temp Actions", $"{tempMutedPeople} tempmuted, {tempBannedPeople} tempbanned", true);
            embed.AddField("Uptime", (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).LimitedHumanize());
            await ReplyAsync(embed: embed.Build());
        }

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

        [Command("tempactexectime")]
        [Summary("View temp action check execution times.")]
        [RequireOwner]
        public async Task DisplayTempActionTimes()
        {
            var embed = new EmbedBuilder();
            embed.WithTitle($"Temp Action Check Execution Times (last check {DateTime.UtcNow.Subtract(TempActions.cachedInfo.lastCheck).Humanize(2)} ago)");
            embed.AddField("Times", TempActions.cachedInfo.checkExecutionTimes.Select(timeSpan => timeSpan.Humanize(2)).Reverse().ListItems("\n"));
            await ReplyAsync(embed: embed.Build());
        }

        [Command("Checktempacts")]
        [Summary("Checks to see if any tempacts should've ended but didn't.")]
        [RequireOwner]
        public async Task ActSanityCheck()
        {
            List<TypedTempAct> tempActsToEnd = new List<TypedTempAct>();
            RequestOptions requestOptions = RequestOptions.Default;
            requestOptions.RetryMode = RetryMode.AlwaysRetry;
            foreach (SocketGuild sockGuild in Context.Client.Guilds)
            {
                TempActionList actions = sockGuild.LoadFromFile<TempActionList>(false);
                if (actions != null)
                {
                    if (!actions.tempBans.IsNullOrEmpty())
                    {
                        foreach (TempAct tempBan in actions.tempBans)
                        {
                            if (DateTime.UtcNow >= tempBan.End)
                            {
                                tempActsToEnd.Add(new TypedTempAct(tempBan, TempActType.TempBan));
                            }
                        }
                    }

                    ModerationSettings settings = sockGuild.LoadFromFile<ModerationSettings>();
                    if (settings != null && sockGuild.GetRole(settings.mutedRole) != null && actions.tempMutes.NotEmpty())
                    {
                        foreach (TempAct tempMute in actions.tempMutes)
                        {
                            if (DateTime.UtcNow >= tempMute.End)
                            { //Normal mute end
                                tempActsToEnd.Add(new TypedTempAct(tempMute, TempActType.TempMute));
                            }
                        }
                    }
                }
            }
            if (tempActsToEnd.Count == 0)
            {
                await ReplyAsync("No acts should've ended already");
                return;
            }

            var embed = new EmbedBuilder();
            embed.Title = $"{tempActsToEnd.Count} tempacts should've ended (longest one ended ago is {TimeSpan.FromMilliseconds(tempActsToEnd.Select(tempAct => DateTime.UtcNow.Subtract(tempAct.End).TotalMilliseconds).Max()).Humanize(2)}";
            foreach (TypedTempAct tempAct in tempActsToEnd)
            {
                embed.AddField($"{tempAct.type} started on {tempAct.dateBanned.ToShortTimeString()} {tempAct.dateBanned.ToShortDateString()} for {tempAct.length.LimitedHumanize()}",
                    $"Should've ended {DateTime.UtcNow.Subtract(tempAct.End).LimitedHumanize()}");
            }
            await ReplyAsync(embed: embed.Build());
            if (tempActsToEnd.Any(tempAct => DateTime.UtcNow.Subtract(tempAct.End).CompareTo(TimeSpan.Zero) < 0))
                await ReplyAsync("Note: some of the values seem broken");
        }

        [Command("CheckCache")]
        [Summary("Check cache info.")]
        [HasAdmin]
        public async Task CheckCache()
        {
            string modSettings;
            if (Context.Guild.GetFromCache<ModerationSettings>(out _, out _) != null)
                modSettings = "In cache";
            else
            {
                if (Context.Guild.LoadFromFile<ModerationSettings>(false) != null)
                {
                    if (Context.Guild.GetFromCache<ModerationSettings>(out _, out _) != null)
                        modSettings = "Loaded into cache";
                    else
                        modSettings = "Cache failed";
                }
                else
                    modSettings = "Not set";
            }

            string logSettings;
            if (Context.Guild.GetFromCache<LogSettings>(out _, out _) != null)
                logSettings = "In cache";
            else
            {
                if (Context.Guild.LoadFromFile<LogSettings>(false) != null)
                {
                    if (Context.Guild.GetFromCache<LogSettings>(out _, out _) != null)
                        logSettings = "Loaded into cache";
                    else
                        logSettings = "Cache failed";
                }
                else
                    logSettings = "Not set";
            }

            EmbedBuilder embed = new EmbedBuilder();
            embed.WithColor(Color.LighterGrey);
            embed.AddField("Moderation settings", modSettings, true);
            embed.AddField("Logging settings", logSettings, true);
            await ReplyAsync(embed: embed.Build());
        }

        [Command("verboseactcheck")]
        [Summary("Check temp acts.")]
        [RequireOwner]
        public async Task VerboseActCheck()
        {
            await TempActions.CheckTempActs(Context.Client, true);
            await ReplyAsync("Checked temp acts. Info is in console");
        }

        [Command("info")]
        [Summary("Information about the bot.")]
        public async Task InfoCommandAsync()
        {
            await ReplyAsync($"Botcatmaxy is a public, open-source bot written and maintained by Blackcatmaxy with info at https://github.com/Blackcatmaxy/Botcatmaxy/ (use '{Context.Client.CurrentUser.Mention} help' for direct link to commands wiki)");
        }

        [Command("resetcache")]
        [Summary("Resets the cache.")]
        [HasAdmin]
        public async Task ResetCacheCommad()
        {
            GuildSettings guild = SettingsCache.guildSettings.FirstOrDefault(g => g.ID == Context.Guild.Id);
            if (guild == null)
            {
                await ReplyAsync("No settings loaded to clear");
                return;
            }
            SettingsCache.guildSettings.Remove(guild);
            await ReplyAsync("Cache cleared for this server's data");
        }

        [Command("globalresetcache")]
        [Summary("Resets the cache from all guilds.")]
        [RequireOwner]
        public async Task ResetGlobalCacheCommad()
        {
            SettingsCache.guildSettings = new HashSet<GuildSettings>();
            await ReplyAsync("All data cleared from cache");
        }
    }
}