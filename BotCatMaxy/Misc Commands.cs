using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace BotCatMaxy {
    public class MiscCommands : ModuleBase<SocketCommandContext> {
        [Command("help")]
        public async Task Help() {
            var embed = new EmbedBuilder();
            embed.AddField("To see commands", "[Click here](https://github.com/Blackcatmaxy/Botcatmaxy/wiki)", true);
            embed.AddField("Report issues and contribute at", "[Click here for GitHub link](http://bot.blackcatmaxy.com)", true);
            await ReplyAsync(embed: embed.Build());
        }


        [Command("ping")]
        public async Task Ping() {
            await ReplyAsync("pong");
        }
    }
}
