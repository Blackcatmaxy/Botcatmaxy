using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;
using Discord.Addons.Interactive;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Moderation;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using System.Linq;
using BotCatMaxy;
using Humanizer;
using Discord;
using System;

namespace BotCatMaxy {
    public class DiscordModModule : InteractiveBase<SocketCommandContext> {
        [RequireContext(ContextType.Guild)]
        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserAsync([RequireHierarchy] SocketGuildUser user, [Remainder] string reason = "Unspecified") {
            string jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, user.Id, reason, Context.Message.GetJumpUrl());
            await user.Warn(1, reason, Context.Channel as SocketTextChannel, logLink: jumpLink);

            Context.Message.DeleteOrRespond($"{user.Mention} has gotten their {user.LoadInfractions().Count.Suffix()} infraction for {reason}", Context.Guild);
        }

        [RequireContext(ContextType.Guild)]
        [Command("warn")]
        [CanWarn()]
        public async Task WarnWithSizeUserAsync([RequireHierarchy] SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            string jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, user.Id, reason, Context.Message.GetJumpUrl());
            await user.Warn(size, reason, Context.Channel as SocketTextChannel, logLink: jumpLink);

            Context.Message.DeleteOrRespond($"{user.Mention} has gotten their {user.LoadInfractions().Count.Suffix()} infraction for {reason}", Context.Guild);
        }

        [Command("dmwarns", RunMode = RunMode.Async)]
        [RequireContext(ContextType.DM, ErrorMessage = "This command now only works in the bot's DMs")]
        [Alias("dminfractions", "dmwarnings", "warns", "infractions", "warnings")]
        public async Task DMUserWarnsAsync(IUser user = null, int amount = 50) {
            if (amount < 1) {
                await ReplyAsync("Why would you want to see that many infractions?");
                return;
            }
            var mutualGuilds = Context.Message.Author.MutualGuilds.ToArray();
            if (user == null) {
                user = Context.Message.Author;
            } else {
                SocketUser sUser = user as SocketUser ?? Context.Client.GetUser(user.Id);
                if (sUser == null) {
                    await ReplyAsync("User exists but isn't cached and won't work with this command");
                    return;
                }
                mutualGuilds = sUser.MutualGuilds.Intersect(mutualGuilds).ToArray();
                if (mutualGuilds.Length == 0) {
                    await ReplyAsync("You do not share any guilds that I moderate with that user");
                    return;
                }
            }
            
            var guildsEmbed = new EmbedBuilder();
            guildsEmbed.WithTitle("Reply with the the number next to the guild you want to check the infractions from");
            
            for (int i = 0; i < mutualGuilds.Length; i++) { 
                guildsEmbed.AddField($"[{i + 1}] {mutualGuilds[i].Name} discord", mutualGuilds[i].Id);
            }
            await ReplyAsync(embed: guildsEmbed.Build());
            SocketGuild guild;
            while (true) {
                SocketMessage message = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                if (message == null || message.Content == "cancel") {
                    await ReplyAsync("You have timed out or canceled");
                    return;
                }
                try {
                    guild = mutualGuilds[ushort.Parse(message.Content) - 1];
                    break;
                } catch {
                    await ReplyAsync("Invalid number, please reply again with a valid number or ``cancel``");
                }
            }
            SocketGuildUser gUser = guild.GetUser(user.Id);
            string username;
            if (!gUser.Nickname.IsNullOrEmpty()) username = gUser.Nickname.StrippedOfPing();
            else username = user.Username.StrippedOfPing();

            List<Infraction> infractions = gUser.LoadInfractions(false);
            if (!infractions.IsNullOrEmpty()) {
                await ReplyAsync($"Here are {username}'s {((amount < infractions.Count) ? $"last {amount} out of " : "")}{"infraction".ToQuantity(infractions.Count)}",
                embed: infractions.GetEmbed(gUser, amount: amount));
            } else {
                await ReplyAsync($"{gUser.NickOrUsername()} has no infractions");
                return;
            }
        }

        [Command("warns")]
        [RequireContext(ContextType.Guild)]
        [Alias("infractions", "warnings")]
        public async Task CheckUserWarnsAsync(SocketGuildUser user = null, int amount = 5) {
            if (user == null) {
                user = Context.Message.Author as SocketGuildUser;
            }
            if (!(Context.Message.Author as SocketGuildUser).CanWarn()) {
                await ReplyAsync("To avoid flood only people who can warn can use this command. Please use !dmwarns instead");
                return;
            }

            List<Infraction> infractions = user.LoadInfractions(false);
            if (infractions.IsNullOrEmpty()) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} has no infractions");
                return;
            }
            await ReplyAsync(embed: infractions.GetEmbed(user, amount: amount, showLinks: true));
        }

        [Command("removewarn")]
        [Alias("warnremove", "removewarning")]
        [HasAdmin()]
        public async Task RemoveWarnAsync([RequireHierarchy] SocketGuildUser user, int index) {
            List<Infraction> infractions = user.LoadInfractions();
            if (infractions.IsNullOrEmpty()) {
                await ReplyAsync("Infractions are null");
                return;
            }
            if (infractions.Count < index || index <= 0) {
                await ReplyAsync("Invalid infraction number");
                return;
            }
            string reason = infractions[index - 1].reason;
            infractions.RemoveAt(index - 1);

            user.SaveInfractions(infractions);
            user.TryNotify($"Your {index.Ordinalize()} warning in {Context.Guild.Name} discord for {reason} has been removed");
            await ReplyAsync("Removed " + user.Mention + "'s warning for " + reason);
        }

        [Command("kickwarn")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn([RequireHierarchy] SocketGuildUser user, [Remainder] string reason = "Unspecified") {
            await user.Warn(1, reason, Context.Channel as SocketTextChannel, "Discord");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await user.KickAsync(reason);
            Context.Message.DeleteOrRespond($"{user.Mention} has been kicked for {reason} ", Context.Guild);
        }

        [Command("kickwarn")]
        [Alias("warnkick", "warnandkick", "kickandwarn")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAndWarn([RequireHierarchy] SocketGuildUser user, float size, [Remainder] string reason = "Unspecified") {
            await user.Warn(size, reason, Context.Channel as SocketTextChannel, "Discord");

            _ = user.Notify("kicked", reason, Context.Guild, Context.Message.Author);
            await user.KickAsync(reason);
            Context.Message.DeleteOrRespond($"{user.Mention} has been kicked for {reason} ", Context.Guild);
        }

        [Command("tempban")]
        [Alias("tban", "temp-ban")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempBanUser([RequireHierarchy] SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }
            if (!(Context.Message.Author as SocketGuildUser).HasAdmin()) {
                ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                if (settings?.maxTempAction != null && amount > settings.maxTempAction) {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            TempAct oldAct = actions.tempBans.FirstOrDefault(tempMute => tempMute.user == user.Id);
            if (oldAct != null) {
                if (!(Context.Message.Author as SocketGuildUser).HasAdmin() && (oldAct.length - (DateTime.Now - oldAct.dateBanned)) >= amount) {
                    await ReplyAsync($"{Context.User.Mention} please contact your admin(s) in order to shorten length of a punishment");
                    return;
                }
                IUserMessage query = await ReplyAsync(
                    $"{user.NickOrUsername().StrippedOfPing()} is already temp-banned for {oldAct.length.LimitedHumanize()} ({(oldAct.length - (DateTime.Now - oldAct.dateBanned)).LimitedHumanize()} left), reply with !confirm within 2 minutes to confirm you want to change the length");
                SocketMessage nextMessage = await NextMessageAsync(timeout: TimeSpan.FromMinutes(2));
                if (nextMessage?.Content?.ToLower() == "!confirm") {
                    _ = query.DeleteAsync();
                    _ = nextMessage.DeleteAsync();
                    actions.tempBans.Remove(oldAct);
                    actions.SaveToFile();
                } else {
                    _ = query.DeleteAsync();
                    if (nextMessage != null) _ = nextMessage.DeleteAsync();
                    await ReplyAsync("Command canceled");
                    return;
                }
            }
            await user.TempBan(amount.Value, reason, Context, actions);
            Context.Message.DeleteOrRespond($"Temporarily banned {user.Mention} for {amount.Value.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempbanwarn")]
        [Alias("tbanwarn", "temp-banwarn", "tempbanandwarn", "tbw")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempBanWarnUser([RequireHierarchy] SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }
            if (!(Context.Message.Author as SocketGuildUser).HasAdmin()) {
                ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                if (settings?.maxTempAction != null && amount > settings.maxTempAction) {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            await user.Warn(1, reason, Context.Channel as SocketTextChannel, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempBans.Any(tempBan => tempBan.user == user.Id)) {
                Context.Message.DeleteOrRespond($"{user.NickOrUsername().StrippedOfPing()} is already temp-banned (the warn did go through)", Context.Guild);
                return;
            }
            await user.TempBan(amount.Value, reason, Context, actions);
            Context.Message.DeleteOrRespond($"Temporarily banned {user.Mention} for {amount.Value.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempbanwarn")]
        [Alias("tbanwarn", "temp-banwarn", "tempbanwarn", "warntempban", "tbw")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempBanWarnUser([RequireHierarchy] SocketGuildUser user, string time, float size, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-ban for less than a minute");
                return;
            }
            if (!(Context.Message.Author as SocketGuildUser).HasAdmin()) {
                ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
                if (settings?.maxTempAction != null && amount > settings.maxTempAction) {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            await user.Warn(size, reason, Context.Channel as SocketTextChannel, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempBans.Any(tempBan => tempBan.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-banned (the warn did go through)");
                return;
            }
            await user.TempBan(amount.Value, reason, Context, actions);
            Context.Message.DeleteOrRespond($"Temporarily banned {user.Mention} for {amount.Value.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempmute", RunMode = RunMode.Async)]
        [Alias("tmute", "temp-mute")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteUser([RequireHierarchy] SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (!(Context.Message.Author as SocketGuildUser).HasAdmin()) {
                if (settings?.maxTempAction != null && amount > settings.maxTempAction) {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null) {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            TempAct oldAct = actions.tempMutes.FirstOrDefault(tempMute => tempMute.user == user.Id);
            if (oldAct != null) {
                if (!(Context.Message.Author as SocketGuildUser).HasAdmin() && (oldAct.length - (DateTime.Now - oldAct.dateBanned)) >= amount) {
                    await ReplyAsync($"{Context.User.Mention} please contact your admin(s) in order to shorten length of a punishment");
                    return;
                }
                IUserMessage query = await ReplyAsync(
                    $"{user.NickOrUsername().StrippedOfPing()} is already temp-muted for {oldAct.length.LimitedHumanize()} ({(oldAct.length - (DateTime.Now - oldAct.dateBanned)).LimitedHumanize()} left), reply with !confirm within 2 minutes to confirm you want to change the length");
                SocketMessage nextMessage = await NextMessageAsync(timeout: TimeSpan.FromMinutes(2));
                if (nextMessage?.Content?.ToLower() == "!confirm") {
                    _ = query.DeleteAsync();
                    _ = nextMessage.DeleteAsync();
                    actions.tempMutes.Remove(oldAct);
                    actions.SaveToFile();
                } else {
                    _ = query.DeleteAsync();
                    if (nextMessage != null) _ = nextMessage.DeleteAsync();
                    await ReplyAsync("Command canceled");
                    return;
                }
            }

            await user.TempMute(amount.Value, reason, Context, settings, actions);
            Context.Message.DeleteOrRespond($"Temporarily muted {user.Mention} for {amount.Value.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempmutewarn")]
        [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn", "tmw")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteWarnUser([RequireHierarchy] SocketGuildUser user, string time, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (!(Context.Message.Author as SocketGuildUser).HasAdmin()) {
                if (settings?.maxTempAction != null && amount > settings.maxTempAction) {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null) {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            await user.Warn(1, reason, Context.Channel as SocketTextChannel, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempMutes.Any(tempMute => tempMute.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-muted, (the warn did go through)");
                return;
            }

            await user.TempMute(amount.Value, reason, Context, settings, actions);
            Context.Message.DeleteOrRespond($"Temporarily muted {user.Mention} for {amount.Value.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("tempmutewarn")]
        [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn", "tmw")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task TempMuteWarnUser([RequireHierarchy] SocketGuildUser user, string time, float size, [Remainder] string reason) {
            var amount = time.ToTime();
            if (amount == null) {
                await ReplyAsync($"Unable to parse '{time}', be careful with decimals");
                return;
            }
            if (amount.Value.TotalMinutes < 1) {
                await ReplyAsync("Can't temp-mute for less than a minute");
                return;
            }
            ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>();
            if (!(Context.Message.Author as SocketGuildUser).HasAdmin()) {
                if (settings?.maxTempAction != null && amount > settings.maxTempAction) {
                    await ReplyAsync("You are not allowed to punish for that long");
                    return;
                }
            }
            if (settings == null || settings.mutedRole == 0 || Context.Guild.GetRole(settings.mutedRole) == null) {
                await ReplyAsync("Muted role is null or invalid");
                return;
            }
            await user.Warn(size, reason, Context.Channel as SocketTextChannel, "Discord");
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(true);
            if (actions.tempMutes.Any(tempMute => tempMute.user == user.Id)) {
                await ReplyAsync($"{user.NickOrUsername().StrippedOfPing()} is already temp-muted, (the warn did go through)");
                return;
            }

            await user.TempMute(amount.Value, reason, Context, settings, actions);
            Context.Message.DeleteOrRespond($"Temporarily muted {user.Mention} for {amount.Value.LimitedHumanize(3)} because of {reason}", Context.Guild);
        }

        [Command("ban")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban(SocketUser user, [Remainder] string reason = "Unspecified") {
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(false);
            if (actions?.tempBans?.Any(tempBan => tempBan.user == user.Id) ?? false) {
                actions.tempBans.Remove(actions.tempBans.First(tempban => tempban.user == user.Id));
            } else if (Context.Guild.GetBansAsync().Result.Any(ban => ban.User.Id == user.Id)) {
                await ReplyAsync("User has already been banned permanently");
                return;
            }
            user.TryNotify($"You have been banned in the {Context.Guild.Name} discord for {reason}");
            await Context.Guild.AddBanAsync(user);
            Context.Message.DeleteOrRespond($"User has been banned for {reason}", Context.Guild);
        }
    }
}