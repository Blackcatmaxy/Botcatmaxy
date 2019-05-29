using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace BotCatMaxy {
    public class MiscCommands : ModuleBase<SocketCommandContext> {
        [Command("commands")]
        public async Task ListCommands() {
            await ReplyAsync("View commands here https://docs.google.com/document/d/1uVYHX9WEe2aRy2QbzMIwHMHthxJsViqu5Ah-yFKCANc/edit?usp=sharing");
        }

        [Command("help")]
        public async Task Help() {
            var embed = new EmbedBuilder();
            embed.AddField("To see commands", "[Click here](https://docs.google.com/document/d/1uVYHX9WEe2aRy2QbzMIwHMHthxJsViqu5Ah-yFKCANc/edit?usp=sharing)", true);
            embed.AddField("Report issues and contribute at", "[Click here for GitHub link](http://bot.blackcatmaxy.com)", true);
            await ReplyAsync(embed: embed.Build());
        }


        [Command("ping")]
        public async Task Ping() {
            await ReplyAsync("pong");
        }
    }
}
