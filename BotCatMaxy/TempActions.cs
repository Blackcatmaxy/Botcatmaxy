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
            client.Ready += Ready;
        }

        public async Task Ready() {
            _ = Timer();
        }

        public static async Task TempBanChecker(DiscordSocketClient client) {
            try {
                int unbannedPeople = 0;
                int bannedPeople = 0;
                int checkedGuilds = 0;
                foreach (SocketGuild guild in client.Guilds) {
                    string guildDir = guild.GetPath(false);
                    checkedGuilds++;
                    if (guildDir != null && Directory.Exists(guildDir) && File.Exists(guildDir + "/tempActions.json")) {
                        List<TempBan> tempBans = guild.LoadFromFile<List<TempBan>>("tempActions.json");
                        List<TempBan> editedBans = new List<TempBan>(tempBans);
                        if (tempBans != null && tempBans.Count > 0) {
                            bannedPeople += tempBans.Count;

                            foreach (TempBan tempBan in tempBans) {
                                try {
                                    if (client.GetUser(tempBan.user) == null) {
                                        _ = new LogMessage(LogSeverity.Warning, "TempAction", "User is null").Log();
                                    /*} else if (!guild.ContainsBan(tempBan.user)) {
                                        _ = new LogMessage(LogSeverity.Warning, "TempAction", "Tempbanned person isn't banned").Log();
                                        editedBans.Remove(tempBan);*/
                                    } else if (DateTime.Now >= tempBan.timeUnbanned) {
                                        _ = guild.RemoveBanAsync(tempBan.user);
                                        editedBans.Remove(tempBan);
                                        Logging.LogEndTempAct(guild, guild.GetUser(tempBan.user), "ban", tempBan.reason, (tempBan.timeUnbanned - tempBan.dateBanned));
                                        unbannedPeople++;
                                    }
                                } catch (Exception e) {
                                    _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unbanning someone, continuing", e).Log();
                                }
                                
                            }

                            if (editedBans != tempBans) {
                                editedBans.SaveToFile("tempActions.json", guild);
                            }
                        }
                    }
                }
                _ = (checkedGuilds > 0).AssertWarnAsync("Checked 0 guilds for tempbans?");
                _ = new LogMessage(LogSeverity.Debug, "TempAction", "Unbanned " + unbannedPeople + " people out of " + bannedPeople + " banned people").Log();

            } catch (Exception e) {
                _ = new LogMessage(LogSeverity.Error, "TempAction", "Something went wrong unbanning someone", e).Log();
            }
        }

        public async Task Timer() {
            _ = TempBanChecker(client);

            await Task.Delay(600000);
            _ = Timer();
        }
    }
}
