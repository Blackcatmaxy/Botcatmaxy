using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.WebSocket;
using Humanizer;
using System;
using System.Threading;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Microsoft.Extensions.Logging;
#nullable enable
namespace BotCatMaxy.Services.Logging;

public class ThreadHandler : DiscordClientService
{
    private readonly DiscordSocketClient _client;
    public ThreadHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger) : base(client, logger)
    {
        _client = client;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.ThreadCreated += HandleNew;
        _client.ThreadDeleted += HandleDelete;
        _client.ThreadUpdated += HandleUpdate;

        LogSeverity.Info.Log("Logs", "Thread logging set up");
        return Task.CompletedTask;
    }

    public Task HandleNew(SocketThreadChannel socketThreadChannel)
    {
        LogSeverity.Info.Log("logs", "thead create");
        Task.Run(() => LogNew(socketThreadChannel));
        return Task.CompletedTask;
    }

    private Task HandleDelete(Cacheable<SocketThreadChannel, ulong> cacheable)
    {
        Task.Run(() => LogDelete(cacheable));
        return Task.CompletedTask;
    }

    private Task HandleUpdate(Cacheable<SocketThreadChannel, ulong> cacheable, SocketThreadChannel socketThreadChannel)
    {
        Task.Run(() => LogEdit(cacheable, socketThreadChannel));
        return Task.CompletedTask;
    }
    private async Task LogNew(SocketThreadChannel threadChannel)
    {
        try
        {
            SocketGuild guild = threadChannel.Guild;
            LogSettings? settings = guild.LoadFromFile<LogSettings>(false);

            ITextChannel logChannel = guild.GetTextChannel(settings?.logChannel ?? 0);
            if (logChannel == null || !settings!.logThreads)
                return;

            EmbedBuilder embed = new EmbedBuilder()
                                 .WithAuthor(threadChannel.Owner)
                                 .WithFooter($"User ID:{threadChannel.Owner.Id}")
                                 .WithTitle("Thread Created")
                                 .AddField("Thread", threadChannel.Mention, true)
                                 // This ugliness because can't mention SocketGuildChannel?
                                 .AddField("Parent Channel", $"<#{threadChannel.ParentChannel.Id.ToString()}>", true)
                                 .WithTimestamp(threadChannel.CreatedAt)
                                 .WithColor(Color.DarkTeal);

            await logChannel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception exception)
        {
            await new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
        }
    }

    public async Task LogEdit(Cacheable<SocketThreadChannel, ulong> cacheable, SocketThreadChannel threadChannel)
    {
        try
        {
            SocketGuild guild = threadChannel.Guild;
            LogSettings? settings = guild.LoadFromFile<LogSettings>(false);

            ITextChannel logChannel = guild.GetTextChannel(settings?.logChannel ?? 0);
            if (logChannel == null || !settings!.logThreads)
                return;

            SocketThreadChannel oldThreadChannel = await cacheable.GetOrDownloadAsync();

            if (oldThreadChannel.Name == threadChannel.Name)
                return;

            EmbedBuilder embed = new EmbedBuilder()
                                 .WithFooter($"User ID:{threadChannel.Owner.Id}")
                                 .WithTitle("Thread Name Updated")
                                 .WithDescription($"Changed from `{oldThreadChannel.Name}` to `{threadChannel.Name}`")
                                 .AddField("Thread", threadChannel.Mention, true)
                                 // This ugliness because can't mention SocketGuildChannel?
                                 .AddField("Parent Channel", $"<#{threadChannel.ParentChannel.Id.ToString()}>", true)
                                 .WithTimestamp(DateTimeOffset.UtcNow)
                                 .WithColor(Color.DarkOrange);

            await logChannel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception exception)
        {
            await new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
        }
    }

    private async Task LogDelete(Cacheable<SocketThreadChannel, ulong> cachedChannel)
    {
        try
        {
            SocketThreadChannel threadChannel = await cachedChannel.GetOrDownloadAsync();
            SocketGuild guild = threadChannel.Guild;
            LogSettings? settings = guild.LoadFromFile<LogSettings>(false);

            ITextChannel logChannel = guild.GetTextChannel(settings?.logChannel ?? 0);
            if (logChannel == null || !settings!.logThreads)
                return;

            EmbedBuilder embed = new EmbedBuilder()
                                 .WithFooter($"User ID:{threadChannel.Owner.Id}")
                                 .WithTitle("Thread Deleted")
                                 .AddField("Thread Name", threadChannel.Name, true)
                                 .AddField("Thread", threadChannel.Mention, true)
                                 // This ugliness because can't mention SocketGuildChannel?
                                 .AddField("Parent Channel", $"<#{threadChannel.ParentChannel.Id.ToString()}>", true)
                                 .WithTimestamp(DateTimeOffset.UtcNow)
                                 .WithColor(Color.DarkMagenta);

            await logChannel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception exception)
        {
            await new LogMessage(LogSeverity.Error, "Logging", exception.Message, exception).Log();
        }
    }
}