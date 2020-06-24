using Discord.Addons.Interactive;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;
using BotCatMaxy.Data;
using System.Linq;
using Discord;
using System;
using Humanizer;
using BotCatMaxy;

public class ReportModule : InteractiveBase<SocketCommandContext>
{
    [Command("report", RunMode = RunMode.Async)]
    [RequireContext(ContextType.DM)]
    public async Task Report()
    {
        try
        {
            var guildsEmbed = new EmbedBuilder();
            guildsEmbed.WithTitle("Reply with the the number next to the guild you want to make the report in");
            var mutualGuilds = Context.User.MutualGuilds.ToArray();
            for (int i = 0; i < Context.User.MutualGuilds.Count; i++)
            {
                guildsEmbed.AddField($"[{i + 1}] {mutualGuilds[i].Name} discord", mutualGuilds[i].Id);
            }
            await ReplyAsync(embed: guildsEmbed.Build());
            SocketGuild guild;
            while (true)
            {
                SocketMessage message = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                if (message == null || message.Content == "cancel")
                {
                    await ReplyAsync("You have timed out or canceled");
                    return;
                }
                try
                {
                    guild = mutualGuilds[ushort.Parse(message.Content) - 1];
                    break;
                }
                catch
                {
                    await ReplyAsync("Invalid number, please reply again with a valid number or ``cancel``");
                }
            }

            ReportSettings settings = guild.LoadFromFile<ReportSettings>(false);
            if (settings?.channelID == null || guild.GetChannel(settings.channelID ?? 0) == null)
            {
                await ReplyAsync("This guild does not currently have reporting set up, command canceled");
                return;
            }
            SocketGuildUser gUser = guild.GetUser(Context.Message.Author.Id);
            if (settings.requiredRole != null && !(gUser.RoleIDs().Contains(settings.requiredRole.Value) || gUser.GuildPermissions.Administrator))
            {
                await ReplyAsync("You are missing required role for reporting");
                return;
            }

            if (settings.cooldown != null)
            {
                int messageAmount = 100;
                var messages = await Context.Channel.GetMessagesAsync(messageAmount).Flatten().ToListAsync();
                messages.OrderBy(msg => msg.CreatedAt);
                while (messages.Last().CreatedAt.Offset > settings.cooldown.Value)
                {
                    _ = ReplyAsync("Downloading more messages");
                    messageAmount += 100;
                    messages = await Context.Channel.GetMessagesAsync(messageAmount).Flatten().ToListAsync();
                    messages.OrderBy(msg => msg.Timestamp.Offset);
                }
                foreach (IMessage message in messages)
                {
                    TimeSpan timeAgo = message.GetTimeAgo();
                    if (message.Author.IsBot && message.Content == "Report has been sent")
                    {
                        if (timeAgo > settings.cooldown.Value) break;
                        else
                        {
                            await ReplyAsync($"You need to wait the full {settings.cooldown.Value.Humanize()}, {timeAgo.Humanize()} have passed from {message.GetJumpUrl()}");
                            return;
                        }
                    }
                }
            }

            await ReplyAsync("Please reply with what you want to report");
            var reportMsg = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
            if (reportMsg == null) ReplyAsync("Report aborted");

            var embed = new EmbedBuilder();
            embed.WithAuthor(Context.Message.Author);
            embed.WithTitle("Report");
            embed.WithDescription(reportMsg.Content);
            embed.WithFooter("User ID: " + Context.Message.Author.Id);
            embed.WithCurrentTimestamp();
            string links = "";
            if (reportMsg.Attachments.NotEmpty())
                links = reportMsg.Attachments.Select(attachment => attachment.ProxyUrl).ListItems(" ");
            var channel = guild.GetTextChannel(settings.channelID.Value);
            await channel.SendMessageAsync(embed: embed.Build());
            if (!string.IsNullOrEmpty(links))
                await channel.SendMessageAsync("The message above had these attachments: " + links);

            await ReplyAsync("Report has been sent");
        }
        catch (Exception e)
        {
            ReplyAsync("Error: " + e);
        }
    }

    [Command("setreportchannel")]
    [HasAdmin]
    public async Task SetReportChannel()
    {
        try
        {
            ReportSettings settings = Context.Guild.LoadFromFile<ReportSettings>(true);
            if (settings.channelID == Context.Channel.Id) ReplyAsync("Reporting is already set to log here");
            else
            {
                settings.channelID = Context.Channel.Id;
                settings.SaveToFile();
                ReplyAsync("Reporting is now set to this channel");
            }
        }
        catch (Exception e)
        {
            ReplyAsync("Error: " + e);
        }
    }

    [Command("setreportcooldown")]
    [HasAdmin]
    public async Task SetReportCooldown(string time)
    {
        try
        {
            ReportSettings settings;
            if (time == "none")
            {
                settings = Context.Guild.LoadFromFile<ReportSettings>(false);
                if (settings?.cooldown == null)
                {
                    ReplyAsync("Either reports or cooldown are already turned off");
                    return;
                }
                settings.cooldown = null;
            }
            settings = Context.Guild.LoadFromFile<ReportSettings>(true);
            TimeSpan? cooldown = time.ToTime();
            if (cooldown == null)
            {
                ReplyAsync("Time is invalid, if you intend to remove cooldon instead use ``none``");
                return;
            }
            if (settings.cooldown == cooldown) ReplyAsync("Cooldown is already set to value");
            else
            {
                settings.cooldown = cooldown;
                settings.SaveToFile();
                ReplyAsync($"Cooldown is now set to {cooldown.Value.Humanize()}");
            }
        }
        catch (Exception e)
        {
            ReplyAsync("Error: " + e);
        }
    }

    [Command("setreportrole"), HasAdmin]
    public async Task SetReportRole(SocketRole role)
    {
        ReportSettings settings = Context.Guild.LoadFromFile<ReportSettings>();
        if (settings.requiredRole == role.Id) ReplyAsync("Reporting is already set to that role");
        else
        {
            settings.requiredRole = role.Id;
            settings.SaveToFile();
            ReplyAsync($"Reporting now requires {role.Name} role");
        }
    }

    [Command("setreportrole"), HasAdmin]
    public async Task SetReportRole(string value)
    {
        value.ToLower();
        if (!(value == "null" || value == "none"))
        {
            await ReplyAsync("Invalid role");
            return;
        }
        ReportSettings settings = Context.Guild.LoadFromFile<ReportSettings>();
        if (settings.requiredRole == null) ReplyAsync("Reporting is already set to not need a role");
        else
        {
            settings.requiredRole = null;
            settings.SaveToFile();
            ReplyAsync($"Reporting now doesn't require a role");
        }
    }
}
