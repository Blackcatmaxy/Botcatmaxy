using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord.WebSocket;
using BotCatMaxy.Data;
using BotCatMaxy;
using System.Text;
using Discord;
using System.IO;
using BotCatMaxy.Settings;

namespace BotCatMaxy {
    public class TempActions {
        readonly DiscordSocketClient client;
        public TempActions(DiscordSocketClient client) {
            this.client = client;
            client.Ready += Timer;
        }

        public async Task TempBanChecker() {
            try {
                int unbannedPeople = 0;
                int bannedPeople = 0;
                int checkedGuilds = 0;
                foreach (SocketGuild guild in client.Guilds) {
                    string guildDir = guild.GetPath(false);
                    checkedGuilds++;
                    if (guildDir != null && Directory.Exists(guildDir) && File.Exists(guildDir + "/tempActions.json")) {
                        List<TempBan> tempBans = guild.LoadTempActions(false);
                        if (tempBans != null && tempBans.Count > 0) {
                            foreach (TempBan tempBan in tempBans) {
                                bannedPeople += tempBans.Count;
                                bool needSave = false;
                                if (DateTime.Now.Subtract(tempBan.dateBanned).Days >= tempBan.length) {
                                    await (client.GetUser(tempBan.personBanned) != null).AssertAsync("Tempbanned person doesn't exist");
                                    await (await guild.GetBanAsync(tempBan.personBanned) != null).AssertWarnAsync("Tempbanned person isn't banned");
                                    await guild.RemoveBanAsync(tempBan.personBanned);
                                    tempBans.Remove(tempBan);
                                    needSave = true;
                                    unbannedPeople++;
                                }
                                if (needSave) {
                                    tempBans.SaveTempBans(guild);
                                }
                            }
                        }
                    }
                }
                _ = new LogMessage(LogSeverity.Info, "TempAction", "Unbanned " + unbannedPeople + " people out of " + bannedPeople + " banned people in " + checkedGuilds + " guilds").Log();

            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unbanning someone", e).Log();
            }
        }

        public async Task Timer() {
            _ = TempBanChecker();

            await Task.Delay(3600000);
            _ = Timer();
        }
    }
}
