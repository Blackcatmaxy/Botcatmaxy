using BotCatMaxy.Cache;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Components.CommandHandling;
using BotCatMaxy.Services.TempActions;
using BotCatMaxy.Startup;

namespace BotCatMaxy
{
    public class MiscCommands : ModuleBase<SocketCommandContext>
    {
        private const string GITHUB = "https://github.com/Blackcatmaxy/Botcatmaxy";
        private readonly CommandService _service;

        public MiscCommands(CommandService service)
        {
            _service = service;
        }

        [Command("help"), Alias("botinfo", "commands")]
        [Summary("View Botcatmaxy resources.")]
        public async Task Help()
        {
            var embed = new EmbedBuilder
            {
                Description = "Say !dmhelp in the bot's direct messages for a full list of commands you can use."
            };
            embed.AddField("To see commands", $"[Click Here]({GITHUB}/wiki)", true);
            embed.AddField("Report issues and contribute at", $"[Click Here]({GITHUB})", true);
            await ReplyAsync(embed: embed.Build());
        }

        private EmbedFieldBuilder MakeCommandField(CommandInfo command)
        {
            string args = "";
            foreach (ParameterInfo param in command.Parameters)
            {
                args += $"[{param.Name}] ";
            }

            const string GUILDMESSSAGE = "in guilds only";
            string context = null;

            if (command.Preconditions.Any(attribute => attribute is HasAdminAttribute))
                context = $"{GUILDMESSSAGE} \n**Requires administrator permission**";
            else if (command.Preconditions.Any(attribute => attribute is CanWarnAttribute))
                context = $"{GUILDMESSSAGE} \n**Requires ability to warn**";
            else
            {
                var permissionAttribute = command.Preconditions
                    .FirstOrDefault(attribute => attribute is RequireUserPermissionAttribute) as RequireUserPermissionAttribute;
                if (permissionAttribute?.GuildPermission != null)
                    context = $"{GUILDMESSSAGE} \n**Requires {permissionAttribute.GuildPermission.Value.Humanize(LetterCasing.LowerCase)} permission**";
            }

            if (context == null)
            {
                var contextAttribute = command.Preconditions
                    .FirstOrDefault(attribute => attribute is RequireContextAttribute) as RequireContextAttribute;
                context = contextAttribute?.Contexts switch
                {
                    ContextType.Guild => GUILDMESSSAGE,
                    ContextType.DM => "in DMs only",
                    _ => "anywhere",
                };
            }

            string description = command.Summary ?? "*No description.*";
            description += $"\nUseable {context}";

            return new EmbedFieldBuilder
            {
                Name = $"!{command.Aliases[0]} {args}",
                Value = description
            };
        }

        [Command("dmhelp"), Alias("dmbotinfo", "dmcommands", "commandlist", "listcommands")]
        [Summary("Direct messagess a full list of commands you can use.")]
        [RequireContext(ContextType.DM)]
        public async Task DMHelp()
        {
            EmbedBuilder extraHelpEmbed = new EmbedBuilder();
            extraHelpEmbed.AddField("Wiki", $"[Click Here]({GITHUB}/wiki)", true);
            extraHelpEmbed.AddField("Submit bugs, enhancements, and contribute", $"[Click Here]({GITHUB})", true);
            await Context.User.SendMessageAsync(embed: extraHelpEmbed.Build());
            IUserMessage msg = await Context.User.SendMessageAsync("Fetching commands...");

            List<ICommandContext> contexts = new() { Context };
            foreach (SocketGuild guild in Context.User.MutualGuilds)
            {
                var channel = guild.Channels.First(channel => channel is IMessageChannel) as IMessageChannel;

                contexts.Add(new WriteableCommandContext
                {
                    Client = Context.Client,
                    Message = Context.Message,
                    Guild = guild,
                    Channel = channel,
                    User = guild.GetUser(Context.User.Id)
                });
            }

            foreach (ModuleInfo module in _service.Modules)
            {
                EmbedBuilder embed = new()
                {
                    Title = module.Name
                };

                foreach (CommandInfo command in module.Commands)
                {
                    bool isAllowed = false;

                    foreach (ICommandContext ctx in contexts)
                    {
                        PreconditionResult check = await command.CheckPreconditionsAsync(ctx);

                        if (check.IsSuccess)
                        {
                            isAllowed = true;
                            break;
                        }
                    }

                    if (isAllowed)
                    {
                        embed.AddField(MakeCommandField(command));
                    }
                }

                if (embed.Fields.Count > 0)
                {
                    await ReplyAsync(embed: embed.Build());
                }
            }

            msg.DeleteAsync();
            await ReplyAsync("These are all the commands you have permissions to use");
        }

        [Command("describecommand"), Alias("describecmd", "dc", "commanddescribe")]
        [Summary("Find info on a command.")]
        [Priority(10)]
        public async Task DescribeCMDAsync(CommandInfo[] commands)
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = "Commands",
                Description = $"Viewing search results you can use for `!{commands[0].Name}`."
            };

            foreach (CommandInfo command in commands.Take(25))
            {
                embed.AddField(MakeCommandField(command));
            }

            await ReplyAsync(embed: embed.Build());
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

#if DEBUG
        [Command("throw")]
        [RequireOwner]
        public Task ExceptionTestCommandAsync()
        {
            throw new InvalidOperationException("This is an error test");
            ExceptionTestMethod();
            return Task.CompletedTask;
        }

        private static void ExceptionTestMethod()
        {
            throw new InvalidEnumArgumentException("This is what shouldn't be reached");
        }
#endif
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
                            if (DateTime.UtcNow - infraction.Time < TimeSpan.FromHours(24))
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

        [Command("tempactexectime")]
        [Summary("View temp action check execution times.")]
        [RequireOwner]
        public async Task DisplayTempActionTimes()
        {
            var embed = new EmbedBuilder();
            embed.WithTitle($"Temp Action Check Execution Times (last check {DateTime.UtcNow.Subtract(CachedInfo.LastCheck).Humanize(2)} ago)");
            embed.AddField("Times", CachedInfo.CheckExecutionTimes.Select(timeSpan => timeSpan.Humanize(2)).Reverse().ListItems("\n"));
            await ReplyAsync(embed: embed.Build());
        }

        [Command("Checktempacts")]
        [Summary("Checks to see if any tempacts should've ended but didn't.")]
        [RequireOwner]
        public async Task ActSanityCheck()
        {
            var tempActsToEnd = new List<ITempAction>();
            RequestOptions requestOptions = new RequestOptions() { RetryMode = RetryMode.AlwaysRetry };
            foreach (SocketGuild sockGuild in Context.Client.Guilds)
            {
                TempActionList actions = sockGuild.LoadFromFile<TempActionList>(false);
                if (actions != null)
                {
                    if (actions.tempBans?.Count is not (null or 0))
                    {
                        tempActsToEnd.AddRange(actions.tempBans.
                                                       Where(action => (action as ITempAction).ShouldEnd));
                    }

                    ModerationSettings settings = sockGuild.LoadFromFile<ModerationSettings>();
                    if (settings is not null && sockGuild.GetRole(settings.mutedRole) != null
                                             && actions.tempMutes?.Count is not (null or 0))
                    {
                        tempActsToEnd.AddRange(actions.tempMutes
                                                      .Where(action => (action as ITempAction).ShouldEnd));
                    }
                }
            }
            if (tempActsToEnd.Count == 0)
            {
                await ReplyAsync("No acts should've ended already");
                return;
            }

            var embed = new EmbedBuilder();

            // Don't ask me what exactly or why this does, it just does
            var longestTimeAgo = TimeSpan.FromMilliseconds(tempActsToEnd.Select(tempAct
                              => DateTime.UtcNow.Subtract(tempAct.EndTime).TotalMilliseconds).Max());
            embed.Title = $"{tempActsToEnd.Count} tempacts should've ended (longest one ended ago is {longestTimeAgo.Humanize(2)}";
            foreach (ITempAction tempAct in tempActsToEnd)
            {
                embed.AddField($"{tempAct.GetType()} started on {tempAct.Start.ToShortTimeString()} for {tempAct.Length.LimitedHumanize()}",
                    $"Should've ended {DateTime.UtcNow.Subtract(tempAct.EndTime).LimitedHumanize()}");
            }
            await ReplyAsync(embed: embed.Build());
            if (tempActsToEnd.Any(tempAct => DateTime.UtcNow.Subtract(tempAct.EndTime).CompareTo(TimeSpan.Zero) < 0))
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

        [Command("info")]
        [Summary("Information about the bot.")]
        public async Task InfoCommandAsync()
        {
            await ReplyAsync($"Botcatmaxy is a public, open-source bot written and maintained by Blackcatmaxy with info at https://github.com/Blackcatmaxy/Botcatmaxy/ (use '{Context.Client.CurrentUser.Mention} help' for direct link to commands wiki)");
        }

        [Command("resetcache")]
        [Summary("Resets the cache.")]
        [HasAdmin]
        public async Task ResetCacheCommand()
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
        public async Task ResetGlobalCacheCommand()
        {
            SettingsCache.guildSettings = new HashSet<GuildSettings>();
            await ReplyAsync("All data cleared from cache");
        }
    }
}