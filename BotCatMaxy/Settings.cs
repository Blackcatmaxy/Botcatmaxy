using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy;
using BotCatMaxy.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Humanizer;
using Discord.Addons.Interactive;
using BotCatMaxy.Models;

namespace BotCatMaxy
{
    //I want to move away from vague files like settings since conflicts are annoying
    public class SettingsModule : InteractiveBase<SocketCommandContext>
    {
        [Command("Settings Info")]
        [RequireContext(ContextType.Guild)]
        public async Task SettingsInfo()
        {
            var embed = new EmbedBuilder();
            var guildDir = Context.Guild.GetCollection(false);
            if (guildDir == null)
            {
                embed.AddField("Storage", "Not created yet", true);
            }
            else if (guildDir.CollectionNamespace.CollectionName == Context.Guild.OwnerId.ToString())
            {
                embed.AddField("Storage", "Using Owner's ID", true);
            }
            else if (guildDir.CollectionNamespace.CollectionName == Context.Guild.Id.ToString())
            {
                embed.AddField("Storage", "Using Guild ID", true);
            }

            await ReplyAsync(embed: embed.Build());
        }

        [Command("toggleserverstorage", RunMode = RunMode.Async)]
        [HasAdmin]
        public async Task ToggleServerIDUse()
        {
            await ReplyAsync("This is a legacy feature, if you want this done now contact blackcatmaxy@gmail.com with your guild invite and your username so I can get back to you");
        }

        [Command("allowwarn"), Alias("allowtowarn")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
        public async Task AddWarnRole(SocketRole role)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (!settings.ableToWarn.Contains(role.Id))
            {
                settings.ableToWarn.Add(role.Id);
            }
            else
            {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can already warn people");
            }

            settings.SaveToFile();

            await ReplyAsync("People with the role \"" + role.Name + "\" can now warn people");
        }

        [Command("setmaxpunishment"), Alias("setmaxpunish", "maxpunishmentset")]
        [RequireContext(ContextType.Guild), HasAdmin()]
        public async Task SetMaxPunishment(string length)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (length == "none")
            { //Maybe add more options that mean none
                if (settings.maxTempAction == null)
                    await ReplyAsync("The maximum temp punishment is already none");
                else
                {
                    settings.maxTempAction = null;
                    settings.SaveToFile();
                    await ReplyAsync("The maximum temp punishment is now none");
                }
                return;
            }
            TimeSpan? span = length.ToTime();
            if (span != null)
            {
                if (span == settings.maxTempAction)
                {
                    await ReplyAsync("The maximum temp punishment is already " + ((TimeSpan)span).LimitedHumanize(4));
                }
                else
                {
                    settings.maxTempAction = span;
                    settings.SaveToFile();
                    await ReplyAsync("The maximum temp punishment is now " + ((TimeSpan)span).LimitedHumanize(4));
                }
            }
            else
            {
                await ReplyAsync("Your time is incorrectly setup");
            }
        }

        [Command("setmutedrole"), Alias("mutedroleset")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
        public async Task SetMutedRole(SocketRole role)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings == null)
            {
                settings = new ModerationSettings();
            }
            if (settings.mutedRole != role.Id)
            {
                settings.mutedRole = role.Id;
            }
            else
            {
                _ = ReplyAsync("The role \"" + role.Name + "\" is already the muted role");
                return;
            }

            settings.SaveToFile();

            await ReplyAsync("The role \"" + role.Name + "\" is now the muted role");
        }

        [Command("removewarnability")]
        [RequireContext(ContextType.Guild)]
        [HasAdmin]
        public async Task RemoveWarnRole(SocketRole role)
        {
            if (!((SocketGuildUser)Context.User).HasAdmin())
            {
                await ReplyAsync("You do have administrator permissions");
                return;
            }

            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);

            if (settings == null)
            {
                settings = new ModerationSettings();
            }
            if (settings.ableToWarn.Contains(role.Id))
            {
                settings.ableToWarn.Remove(role.Id);
            }
            else
            {
                _ = ReplyAsync("People with the role \"" + role.Name + "\" can't already warn people");
            }
            settings.SaveToFile();

            _ = ReplyAsync("People with the role \"" + role.Name + "\" can now no longer warn people");
        }
    }


}
