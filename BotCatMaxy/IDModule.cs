using System.Threading.Tasks;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using BotCatMaxy.Data;
using Humanizer;
using Discord.WebSocket;
using Discord;
using System.Linq;
using BotCatMaxy;

namespace BotCatMaxy {
    [Group("ID")]
    public class IDCommands : ModuleBase<SocketCommandContext> {
        [RequireContext(ContextType.Guild)]
        [Command("dmwarns")]
        public async Task DMWarns(ulong ID, int amount = 99) {
            if (amount < 1) {
                await ReplyAsync("Why would you want to see that many infractions?");
                return;
            }
            string username = Context.Guild.GetUser(ID)?.NickOrUsername();
            if (username.IsNullOrEmpty()) username = Context.Client.GetUser(ID)?.Username;
            username = username?.StrippedOfPing();

            List<Infraction> infractions = ID.LoadInfractions(Context.Guild, false);
            if (!infractions.IsNullOrEmpty()) {
                try {
                    await Context.Message.Author.GetOrCreateDMChannelAsync().Result.SendMessageAsync(embed: infractions.GetEmbed(amount: amount));
                } catch {
                    await ReplyAsync($"{Context.Message.Author.Mention} something went wrong DMing you their infractions. Check your privacy settings");
                    return;
                }
            } else {
                await ReplyAsync($"{username ?? "They"} have no infractions");
                return;
            }
            string quantity = "infraction".ToQuantity(infractions.Count);
            if (!username.IsNullOrEmpty()) username += "'s";
            if (amount >= infractions.Count) await ReplyAsync($"DMed you {username ?? "their"} {quantity}");
            else await ReplyAsync($"DMed you {username ?? "their"} last {amount} out of {quantity}");
        }

        [RequireContext(ContextType.Guild)]
        [Command("warns"), Alias("infractions", "warnings")]
        public async Task Warns(ulong ID, int amount = 6) {
            if (amount < 1) {
                await ReplyAsync("Why would you want to see that many infractions?");
                return;
            }
            string username = Context.Guild.GetUser(ID)?.NickOrUsername();
            if (username.IsNullOrEmpty()) username = Context.Client.GetUser(ID)?.Username;
            username = username?.StrippedOfPing();

            List<Infraction> infractions = ID.LoadInfractions(Context.Guild, false);
            if (!infractions.IsNullOrEmpty()) {
                await ReplyAsync(embed: infractions.GetEmbed(amount: amount));
            } else {
                await ReplyAsync($"{username ?? "They"} have no infractions");
                return;
            }
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnUserAsync(ulong ID, [Remainder] string reason = "Unspecified") {
            SocketGuildUser gUser = Context.Guild.GetUser(ID);
            string jumpLink;
            if (gUser != null) {
                jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, gUser.Id, reason, Context.Message.GetJumpUrl());
                await gUser.Warn(1, reason, Context.Channel as SocketTextChannel, logLink: jumpLink);
                await ReplyAsync(gUser.Mention + " has gotten their " + gUser.LoadInfractions().Count.Suffix() + " infraction for " + reason);
                return;
            }
            jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, ID, reason, Context.Message.GetJumpUrl());
            await ID.Warn(1, reason, Context.Channel as SocketTextChannel, logLink: jumpLink);
            Context.Message.DeleteOrRespond(
                $"{Context.Client.GetUser(ID)?.Username.StrippedOfPing() ?? "They"} have gotten their {ID.LoadInfractions(Context.Guild).Count.Suffix()} infraction for {reason}", Context.Guild);
        }

        [Command("warn")]
        [CanWarn()]
        public async Task WarnWithSizeUserAsync(ulong ID, float size, [Remainder] string reason = "Unspecified") {
            SocketGuildUser gUser = Context.Guild.GetUser(ID);
            string jumpLink;
            if (gUser != null) {
                jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, gUser.Id, reason, Context.Message.GetJumpUrl());
                await gUser.Warn(size, reason, Context.Channel as SocketTextChannel, logLink: jumpLink);
                await ReplyAsync(gUser.Mention + " has gotten their " + gUser.LoadInfractions().Count.Suffix() + " infraction for " + reason);
                return;
            }
            jumpLink = Logging.LogWarn(Context.Guild, Context.Message.Author, ID, reason, Context.Message.GetJumpUrl());
            await ID.Warn(1, reason, Context.Channel as SocketTextChannel, logLink: jumpLink);
            Context.Message.DeleteOrRespond(
                $"{Context.Client.GetUser(ID)?.Username.StrippedOfPing() ?? "They"} have gotten their {ID.LoadInfractions(Context.Guild).Count.Suffix()} infraction for {reason}", Context.Guild);
        }

        [Command("ban")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban(ulong ID, [Remainder] string reason = "Unspecified") {
            TempActionList actions = Context.Guild.LoadFromFile<TempActionList>(false);
            if (actions?.tempBans?.Any(tempBan => tempBan.user == ID) ?? false) {
                actions.tempBans.Remove(actions.tempBans.First(tempban => tempban.user == ID));
            } else if (Context.Guild.GetBansAsync().Result.Any(ban => ban.User.Id == ID)) {
                await ReplyAsync("User has already been banned permanently");
                return;
            }
            Context.Client.GetUser(ID)?.TryNotify($"You have been banned in the {Context.Guild.Name} discord for {reason}");
            await Context.Guild.AddBanAsync(ID);
            Context.Message.DeleteOrRespond($"User has been banned for {reason}", Context.Guild);
        }
    }
}
