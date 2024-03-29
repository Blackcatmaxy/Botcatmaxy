﻿using BotCatMaxy;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Components.Interactivity;

[Name("Report")]
public class ReportModule : InteractiveModule
{
    public ReportModule(IServiceProvider service) : base(service)
    {
    }

    [Command("report", RunMode = RunMode.Async)]
    [Summary("Create a new report.")]
    [RequireContext(ContextType.DM)]
    public async Task<CommandResult> Report()
    {
        if (Context is not SocketCommandContext socketContext)
            throw new NotImplementedException("Command not ready for tests.");

        var queryGuild = await QueryMutualGuild();
        if (queryGuild == null)
            return CommandResult.FromError("You have timed out or canceled.");

        var guild = queryGuild as SocketGuild ?? socketContext.Client.GetGuild(queryGuild.Id);
        ReportSettings settings = guild.LoadFromFile<ReportSettings>(false);
        if (settings?.channelID == null || guild.GetChannel(settings.channelID ?? 0) == null)
            return CommandResult.FromError("This guild does not currently have reporting set up, command canceled");

        SocketGuildUser gUser = guild.GetUser(Context.Message.Author.Id);
        if (settings.requiredRole != null && !(gUser.RoleIDs().Contains(settings.requiredRole.Value) || gUser.GuildPermissions.Administrator))
            return CommandResult.FromError("You are missing required role for reporting");

        if (settings.cooldown != null)
        {
            int messageAmount = 100;
            List<IMessage> messages = await Context.Channel.GetMessagesAsync(messageAmount).Flatten()
                                                   .OrderBy(msg => msg.CreatedAt).ToListAsync();
            while (messages.Last().CreatedAt.Offset > settings.cooldown.Value)
            {
                _ = ReplyAsync("Downloading more messages");
                messageAmount += 100;
                messages = await Context.Channel.GetMessagesAsync(messageAmount).Flatten()
                                        .OrderBy(msg => msg.CreatedAt).ToListAsync();
            }
            foreach (var message in messages)
            {
                TimeSpan timeAgo = message.GetTimeAgo();
                if (!message.Author.IsBot || message.Content != "Report has been sent")
                    continue;

                if (timeAgo > settings.cooldown.Value) break;
                return CommandResult.FromError(
                    $"You need to wait the full {settings.cooldown.Value.Humanize()}, {timeAgo.Humanize()} have passed from {message.GetJumpUrl()}");
            }
        }

        await ReplyAsync("Please reply with what you want to report");
        var reportMsg = await Interactivity.NextMessageAsync(
            msg => msg.Channel.Id == Context.Channel.Id, timeout: TimeSpan.FromMinutes(5));
        if (!reportMsg.IsSuccess)
            return CommandResult.FromError("Report cancelled.");

        var embed = new EmbedBuilder();
        embed.WithAuthor(Context.Message.Author);
        embed.WithTitle("Report");
        embed.WithDescription(reportMsg.Value.Content);
        embed.WithFooter("User ID: " + Context.Message.Author.Id);
        embed.WithCurrentTimestamp();
        string links = "";
        if (reportMsg.Value.Attachments?.Count is not null or 0)
            links = reportMsg.Value.Attachments.Select(attachment => attachment.ProxyUrl).ListItems(" ");
        var channel = guild.GetTextChannel(settings.channelID.Value);
        await channel.SendMessageAsync(embed: embed.Build());
        if (!string.IsNullOrEmpty(links))
            await channel.SendMessageAsync("The message above had these attachments: " + links);

        return CommandResult.FromSuccess("Report has been sent");
    }

    [Command("setreportchannel")]
    [Summary("Sets the channel user reports will be sent in.")]
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
    [Summary("Sets the cooldown in between reports.")]
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
                ReplyAsync("Time is invalid, if you intend to remove cooldown instead use ``none``");
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
    [Summary("Sets a role that is required to create reports.")]
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
    [Summary("Sets a role that is required to create reports. Supply `none` to remove.")]
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