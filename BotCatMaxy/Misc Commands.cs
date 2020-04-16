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
using Humanizer;
using Discord.Rest;

namespace BotCatMaxy {
    public class MiscCommands : ModuleBase<SocketCommandContext> {
        [Command("help"), Alias("botinfo", "commands")]
        public async Task Help() {
            var embed = new EmbedBuilder();
            embed.AddField("To see commands", "[Click here](https://github.com/Blackcatmaxy/Botcatmaxy/wiki)", true);
            embed.AddField("Report issues and contribute at", "[Click here for GitHub link](http://bot.blackcatmaxy.com)", true);
            await ReplyAsync(embed: embed.Build());
        }

        [Command("checkperms")]
        [RequireUserPermission(GuildPermission.BanMembers, Group = "Permission")]
        [RequireOwner(Group = "Permission")]
        public async Task CheckPerms() {
            GuildPermissions perms = Context.Guild.CurrentUser.GuildPermissions;
            var embed = new EmbedBuilder();
            embed.AddField("Manage roles", perms.ManageRoles, true);
            embed.AddField("Manage messages", perms.ManageMessages, true);
            embed.AddField("Kick", perms.KickMembers, true);
            embed.AddField("Ban", perms.BanMembers, true);
            await ReplyAsync(embed: embed.Build());
        }

        [RequireOwner()]
        [Command("bottest")]
        public async Task TestCommand() {
            ErrorTest();
        }

        public void ErrorTest() {
            throw new InvalidOperationException();
        }

        [RequireOwner]
        [Command("stats")]
        [Alias("statistics")]
        public async Task Statistics() {
            var embed = new EmbedBuilder();
            embed.WithTitle("Statistics");
            embed.AddField($"Part of", $"{Context.Client.Guilds.Count} discord guilds", true);
            ulong infractions24Hours = 0;
            ulong totalInfractons = 0;
            ulong members = 0;
            uint tempBannedPeople = 0;
            uint tempMutedPeople = 0;
            foreach (SocketGuild guild in Context.Client.Guilds) {
                members += (ulong)guild.MemberCount;
                var collection = guild.GetInfractionsCollection(false);

                if (collection != null) {
                    using var cursor = collection.Find(new BsonDocument()).ToCursor();
                    foreach (var doc in cursor.ToList()) {
                        foreach (Infraction infraction in BsonSerializer.Deserialize<UserInfractions>(doc).infractions) {
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
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task SetSlowMode(int time) {
            (Context.Channel as SocketTextChannel).ModifyAsync(channel => channel.SlowModeInterval = time);
            await ReplyAsync($"Set channel slowmode to {time} seconds");
        }

        [Command("tempactexectime")]
        [RequireOwner]
        public async Task DisplayTempActionTimes() {
            var embed = new EmbedBuilder();
            embed.WithTitle("Temp Action Check Execution Times");
            embed.AddField("Times", TempActions.checkExecutionTimes.Select(timeSpan => timeSpan.Humanize(2)).Reverse().ListItems("\n"));
            await ReplyAsync(embed: embed.Build());
        }

        [Command("Checktempacts")]
        [RequireOwner]
        public async Task ActSanityCheck() {
            List<TypedTempAct> tempActsToEnd = new List<TypedTempAct>();
            RequestOptions requestOptions = RequestOptions.Default;
            requestOptions.RetryMode = RetryMode.AlwaysRetry;
            foreach (SocketGuild sockGuild in Context.Client.Guilds) {
                TempActionList actions = sockGuild.LoadFromFile<TempActionList>(false);
                if (actions != null) {
                    if (!actions.tempBans.IsNullOrEmpty()) {
                        var bans = await sockGuild.GetBansAsync(requestOptions);
                        foreach (TempAct tempBan in actions.tempBans) {
                            RestBan ban = bans.FirstOrDefault(tBan => tBan.User.Id == tempBan.user);
                            if (DateTime.UtcNow >= tempBan.dateBanned.Add(tempBan.length)) {
                                tempActsToEnd.Add(new TypedTempAct(tempBan, TempActs.TempBan));
                            }
                        }
                    }

                    ModerationSettings settings = sockGuild.LoadFromFile<ModerationSettings>();
                    if (settings != null && sockGuild.GetRole(settings.mutedRole) != null && actions.tempMutes.NotEmpty()) {
                        SocketRole mutedRole = sockGuild.GetRole(settings.mutedRole);
                        foreach (TempAct tempMute in actions.tempMutes) {
                            if (DateTime.UtcNow >= tempMute.dateBanned.Add(tempMute.length)) { //Normal mute end
                                tempActsToEnd.Add(new TypedTempAct(tempMute, TempActs.TempMute));
                            }
                        }
                    }
                }
            }
            if (tempActsToEnd.Count == 0) {
                await ReplyAsync("No acts should've ended already");
                return;
            }

            var embed = new EmbedBuilder();
            embed.Title = $"{tempActsToEnd.Count} tempacts should've ended (longest one ended ago is {tempActsToEnd.Select(tempAct => DateTime.UtcNow.Subtract(tempAct.End)).Max().Humanize(2)}";
            foreach (TypedTempAct tempAct in tempActsToEnd) {
                embed.AddField($"{tempAct.type} started on {tempAct.dateBanned.ToShortDateString()} for {tempAct.length.LimitedHumanize()}", $"Should've ended {DateTime.UtcNow.Subtract(tempAct.End).LimitedHumanize()}");
            }
            await ReplyAsync(embed: embed.Build());
        }

        [Command("CheckCache")]
        [HasAdmin]
        public async Task CheckCache() {
            string modSettings;
            if (Context.Guild.GetFromCache<ModerationSettings>(out _, out _) != null)
                modSettings = "In cache";
            else {
                if (Context.Guild.LoadFromFile<ModerationSettings>(false) != null) {
                    if (Context.Guild.GetFromCache<ModerationSettings>(out _, out _) != null)
                        modSettings = "Loaded into cache";
                    else
                        modSettings = "Cache failed";
                } else
                    modSettings = "Not set";
            }

            string logSettings;
            if (Context.Guild.GetFromCache<LogSettings>(out _, out _) != null)
                logSettings = "In cache";
            else {
                if (Context.Guild.LoadFromFile<LogSettings>(false) != null) {
                    if (Context.Guild.GetFromCache<LogSettings>(out _, out _) != null)
                        logSettings = "Loaded into cache";
                    else
                        logSettings = "Cache failed";
                } else
                    logSettings = "Not set";
            }

            EmbedBuilder embed = new EmbedBuilder();
            embed.WithColor(Color.LighterGrey);
            embed.AddField("Moderation settings", modSettings, true);
            embed.AddField("Logging settings", logSettings, true);
            await ReplyAsync(embed: embed.Build());
        }
    }
}
