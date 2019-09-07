using System.Threading.Tasks;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using BotCatMaxy.Data;
using Humanizer;
using Discord.Addons.Preconditions;

namespace BotCatMaxy {
    [Group("ID")]
    public class IDCommands : ModuleBase<SocketCommandContext> {
        [Ratelimit(3, 5, Measure.Minutes, ErrorMessage = "You have used this command too much, calm down")]
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
    }
}
