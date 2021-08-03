using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Components.CommandHandling;
using Interactivity;
using Interactivity.Confirmation;

namespace BotCatMaxy
{
    //I want to move away from vague files like settings since conflicts are annoying
    [Name("Settings")]
    public class SettingsModule : InteractiveModule
    {
        public PermissionService PermissionService { get; set; }
     
        public SettingsModule(IServiceProvider service) : base(service)
        {
        }
        
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
        public async Task<RuntimeResult> AddPermissionAsync(IRole role, string node)
        {
            var permissions = Context.Guild.LoadFromFile<CommandPermissions>(true);
            //Prompt user with info about new system before enabling when disabled
            if (permissions.enabled == false && Interactivity != null)
            {
                var pageBuilder = new PageBuilder()
                    .WithTitle("Are you sure?")
                    .WithDescription("Adding a new permission would enable the new advanced permission system, " +
                                     "'restricted' commands will no longer permit users based on Discord permissions. " +
                                     "Are you sure you're ready for this change to take effect?");
                var confirmation = new ConfirmationBuilder()
                    .WithContent(pageBuilder)
                    .WithConfirmEmote(new Emoji("\U00002705"))
                    .WithDeclineEmote(new Emoji("\U0000274c"));
                var result = await Interactivity.SendConfirmationAsync(confirmation.Build(), Context.Channel,
                    TimeSpan.FromMinutes(3));
                //if not success
                if (result.Value == false)
                    return CommandResult.FromSuccess("Command cancelled");
                await ReplyAsync("Advanced permission system activated.");
                permissions.enabled = true;
            }

            if (permissions.RoleHasValue(role.Id, node))
                return CommandResult.FromError($"Role `{role.Name}` already has permissions set to this role.");

            string verifyResult = PermissionService.TryVerifyNode(node);
            if (verifyResult != null)
                return CommandResult.FromError(verifyResult);

            permissions.AddNodeToRole(role.Id, node);
            permissions.SaveToFile();

            return CommandResult.FromSuccess($"Added node `{node}` to `{role.Name}`.");
        }

        [Command("RemovePermission")]
        [DynamicPermission("Permissions.Nodes.Remove")]
        public Task<RuntimeResult> RemovePermission(IRole role, string node)
        {
            var permissions = Context.Guild.LoadFromFile<CommandPermissions>(false);
            if (permissions == null) 
                return Task.FromResult<RuntimeResult>(CommandResult.FromError("Permissions not set."));

            //If value is is not in dict or roleID not in list
            if (!permissions.RoleHasValue(role.Id, node)) 
                return Task.FromResult<RuntimeResult>(CommandResult.FromError($"Role `{role.Name}` already doesn't have permissions set to node `{node}`."));

            var nodes = permissions.Map[role.Id];
            if (nodes.Count > 1)
            {
                nodes.Remove(node);
                permissions.Map[role.Id] = nodes;
            }
            else
            {
                permissions.Map.Remove(role.Id);
            }

            permissions.SaveToFile();
            return Task.FromResult<RuntimeResult>(CommandResult.FromSuccess($"Removed node `{node}` from role `{role.Name}`."));
        }
    }
}