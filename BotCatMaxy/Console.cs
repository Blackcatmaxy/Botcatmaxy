/*
            case "messageowners":
                splitInput.RemoveAt(0);
                string message = "";
                foreach (string word in splitInput) {
                    message += " " + word;
                }
                List<SocketUser> owners = new List<SocketUser>();
                foreach (SocketGuild guild in client.Guilds) {
                    if (!owners.Contains(guild.Owner)) owners.Add(guild.Owner);
                }
                foreach (SocketUser owner in owners) {
                    owner.TryNotify(message);
                }
                await new LogMessage(LogSeverity.Info, "Console", "Messaged guild owners:" + message).Log();
                break;
            case "checktempbans":
                await TempActions.TempActChecker(client, true);
                await (new LogMessage(LogSeverity.Info, "Console", "Checked temp-actions")).Log();
                break;
            case "shutdown":
            case "shut down":
                await client.SetGameAsync("restarting");
                Environment.Exit(0);
                break;*/