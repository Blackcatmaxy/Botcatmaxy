using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    //I want to move away from vague files like settings since conflicts are annoying
    [Name("Settings")]
    public class SettingsModule : ModuleBase<ICommandContext>
    {
        [Command("Settings Info")]
        [Summary("View settings.")]
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

        [Command("toggleserverstorage")]
        [Summary("Legacy feature. Run for instruction on how to enable.")]
        [HasAdmin]
        public async Task<RuntimeResult> ToggleServerIDUse()
        {
            return CommandResult.FromSuccess(
                "This is a legacy feature, if you want this done now contact blackcatmaxy@gmail.com with your guild invite and your username so I can get back to you");
        }

        [Command("allowwarn"), Alias("allowtowarn")]
        [Summary("Sets which role is allowed to warn other users.")]
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
                await ReplyAsync("People with the role \"" + role.Name + "\" can already warn people");
                return;
            }

            settings.SaveToFile();

            await ReplyAsync("People with the role \"" + role.Name + "\" can now warn people");
        }

        [Command("setmaxpunishment"), Alias("setmaxpunish", "maxpunishmentset")]
        [Summary("Sets the max length a temporary punishment can last.")]
        [RequireContext(ContextType.Guild), HasAdmin()]
        public async Task SetMaxPunishment(string length)
        {
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(true);
            if (length == "none")
            { //Maybe add more options that mean none
                if (settings.maxTempAction == null)
                {
                    await ReplyAsync("The maximum temp punishment is already none");
                    return;
                }
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
        [Summary("Sets the muted role of the server.")]
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
        [Summary("Disables a role's ability to warn.")]
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
                await ReplyAsync("People with the role \"" + role.Name + "\" can't already warn people");
                return;
            }
            settings.SaveToFile();

            await ReplyAsync("People with the role \"" + role.Name + "\" can now no longer warn people");
        }

        [Command("AddPermission")]
        public Task<RuntimeResult> AddPermission(CommandInfo[] commands, IRole role)
        {
            var permissions = Context.Guild.LoadFromFile<CommandPermissions>(true);
            var commandNames = commands.Select(command => command.Name).ToArray();
            
            //Check if any already set
            foreach (string name in commandNames)
            {
                if (permissions.map.TryGetValue(name, out List<ulong> roles) && (roles?.Contains(role.Id) ?? false)) 
                    return Task.FromResult<RuntimeResult>(CommandResult
                        .FromError($"`!{name}` already has permissions set to this role."));
            }

            foreach (var name in commandNames)
            {
                if (permissions.map.TryGetValue(name, out List<ulong> roles))
                {
                    roles.Add(role.Id);
                    permissions.map[name] = roles;
                }
                else
                    permissions.map[name] = new List<ulong> {role.Id};
            }
            permissions.SaveToFile();
            
            commandNames = commandNames.Select(name => $"`!{name}`").ToArray();
            return Task.FromResult<RuntimeResult>(CommandResult
                .FromSuccess($"Added role requirement of `{role.Name}` to {string.Join(", ", commandNames)}."));
        }
        
        [Command("RemovePermission")]
        public Task<RuntimeResult> RemovePermission(CommandInfo[] commands, IRole role)
        {
            var permissions = Context.Guild.LoadFromFile<CommandPermissions>(false);
            if (permissions == null) 
                return  Task.FromResult<RuntimeResult>(CommandResult.FromError("Permissions not set."));
            
            var commandNames = commands.Select(command => command.Name).ToArray();
            
            //Check if any already set
            foreach (string name in commandNames)
            {
                //If value is is not in dict or roleID not in list
                if (!permissions.map.TryGetValue(name, out List<ulong> roles) || (!roles?.Contains(role.Id) ?? false)) 
                    return Task.FromResult(CommandResult
                        .FromError($"`!{name}` already doesn't have permissions set to this role.") as RuntimeResult);
            }

            foreach (var name in commandNames)
            {
                var roles = permissions.map[name];
                roles.Remove(role.Id);
                permissions.map[name] = roles;
            }
            permissions.SaveToFile();
            
            commandNames = commandNames.Select(name => $"`!{name}`").ToArray();
            return Task.FromResult(CommandResult
                .FromSuccess($"Removed role requirement of `{role.Name}` to {string.Join(", ", commandNames)}.") as RuntimeResult);
        }
    }
}