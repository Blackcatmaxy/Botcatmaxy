using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCatMaxy.Settings;
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

namespace BotCatMaxy {
    public class MiscCommands : ModuleBase<SocketCommandContext> {
        [Command("help")]
        public async Task Help() {
            var embed = new EmbedBuilder();
            embed.AddField("To see commands", "[Click here](https://github.com/Blackcatmaxy/Botcatmaxy/wiki)", true);
            embed.AddField("Report issues and contribute at", "[Click here for GitHub link](http://bot.blackcatmaxy.com)", true);
            await ReplyAsync(embed: embed.Build());
        }

        [RequireOwner()]
        [Command("bottest")]
        public async Task TestCommand([Remainder] string s) {
            ModerationSettings modSettings = Context.Guild.LoadFromFile<ModerationSettings>();

            if (modSettings.allowedCaps > 0 && s.Length > 5) {
                uint amountCaps = 0;
                foreach (char c in s) {
                    if (char.IsUpper(c)) {
                        amountCaps++;
                    }
                }
                await ReplyAsync($"{(amountCaps / (float)s.Length) * 100} {modSettings.allowedCaps}, {(((amountCaps / (float)s.Length) * 100) >= modSettings.allowedCaps)}");
            }
        }
    }
}
