using Discord;
using Discord.WebSocket;
using System.Threading;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public class StatusManager
    {
        private DiscordSocketClient client;
        private Timer timer;
        private readonly string version;
        private ushort statusPos = 0;

        public StatusManager(DiscordSocketClient client, string version)
        {
            this.client = client;
            this.version = version;
            client.Ready += ReadyHandler;
        }

        public async Task ReadyHandler()
        {
            client.Ready -= ReadyHandler;
            await new LogMessage(LogSeverity.Info, "Status", "Statuses are running").Log();
            timer = new Timer(async (_) => await CheckStatus());
            timer.Change(0, 30000);
        }

        public async Task CheckStatus()
        {
            string status = null;
            switch (statusPos)
            {
                case 0:
                    status = $"version {version}";
                    statusPos++;
                    break;
                case 1:
                    status = "with info at https://bot.blackcatmaxy.com";
                    statusPos++;
                    break;
                case 2:
                    status = "Donate at https://donate.blackcatmaxy.com to help keep the bot running";
                    statusPos = 0;
                    break;
                default:
                    await new LogMessage(LogSeverity.Error, "Status", "Reached invalid status").Log();
                    break;
            }
            await client.SetGameAsync(status);
        }
    }
}
