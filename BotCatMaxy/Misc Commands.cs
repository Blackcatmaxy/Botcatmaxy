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

namespace BotCatMaxy {
    public class MiscCommands : ModuleBase<SocketCommandContext> {
        [Command("help")]
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
        public async Task TestCommand([RequireHierarchy] SocketGuildUser other) {
            await ReplyAsync("Command success");
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
            foreach (SocketGuild guild in Context.Client.Guilds) {
                members += (ulong)guild.MemberCount;
                var collection = guild.GetInfractionsCollection(false);

                if (collection != null) {
                    using var cursor = collection.Find(new BsonDocument()).ToCursor();
                    foreach (var doc in cursor.ToList()) {
                        foreach (Infraction infraction in BsonSerializer.Deserialize<UserInfractions>(doc).infractions) {
                            if (DateTime.Now - infraction.time < TimeSpan.FromHours(24))
                                infractions24Hours++;
                            totalInfractons++;
                        }
                    }
                }
            }
            embed.AddField("Totals", $"{members} users || {totalInfractons} total infractions", true);
            embed.AddField("In the last 24 hours", $"{infractions24Hours} infractions given", true);
            embed.AddField("Uptime", (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).LimitedHumanize());
            await ReplyAsync(embed: embed.Build());
        }
    }
}
