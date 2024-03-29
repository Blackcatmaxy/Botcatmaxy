﻿using Discord;
using Humanizer;
using System;
using System.Threading.Tasks;
using Serilog.Events;
using Logger = Serilog.Log;

namespace BotCatMaxy.Services.Logging;

public static class ExceptionLogging
{
    /// <summary>
    /// Calls <see cref="SendExceptionAsync"/> if there's an exception, otherwise calls <see cref="Log(Discord.LogMessage)"/> 
    /// </summary>
    [Obsolete("Use LogSeverity.Log or LogExceptionAsync instead")]
    public static Task Log(this LogMessage message)
    {
        var severity = message.Severity;
        var source = message.Source;
        var content = message.Message;
        var exception = message.Exception;
        if (exception != null)
            return severity.SendExceptionAsync(source, content, exception);
        severity.Log(source, content);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Formats a source and message into log content
    /// </summary>
    /// <param name="severity">The importance of the message</param>
    /// <param name="source">Where it came from, max 7 chars</param>
    /// <param name="message">Main content written</param>
    private static string FormatSourcedMessage(string source, string message)
    {
        source += ':';
        return source.PadRight(9) + message;
    }

    /// <summary>
    /// Logs the message with the source and severity to the selected logger
    /// </summary>
    /// <param name="logger">The selected logger</param>
    /// <param name="severity">The importance of the message</param>
    /// <param name="source">Where it came from, max 7 chars</param>
    /// <param name="message">Main content written</param>
    public static void Log(this Serilog.ILogger logger, LogEventLevel severity, string source, string message)
        => logger.Write(severity, FormatSourcedMessage(source, message));

    /// <summary>
    /// Logs the message with the source and severity to the static Serilog logger
    /// </summary>
    /// <param name="severity">The importance of the message</param>
    /// <param name="source">Where it came from, max 7 chars</param>
    /// <param name="message">Main content written</param>
    public static void Log(this LogSeverity severity, string source, string message)
        => Logger.Logger.Log(severity.SwitchToEventLevel(), source, message);

    /// <summary>
    /// Switches Discord <see cref="LogSeverity"/> to Serilog <see cref="LogEventLevel"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if new definition was added and therefore wouldn't meet any existing case</exception>
    public static LogEventLevel SwitchToEventLevel(this LogSeverity severity)
        => severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };

    /// <summary>
    /// Directly formats an exception and message to be logged to Serilog WITHOUT sending to Discord
    /// </summary>
    /// <param name="severity"></param>
    /// <param name="source"></param>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public static void LogException(this LogSeverity severity, string source, string message, Exception exception)
    {
        severity.Log(source, $"{message}\n{exception}");
    }
        
    /// <summary>
    /// Formats an embed to be sent in Discord if bot is connected, and then calls Serilog
    /// </summary>
    /// <param name="severity">The importance of the message</param>
    /// <param name="source">Where it came from, max 7 chars</param>
    /// <param name="message">Main content written</param>
    /// <param name="exception"><b>Required</b>, the exception to be logged</param>
    /// <param name="errorEmbed">Optional override for Discord embed</param>
    public static async Task SendExceptionAsync(this LogSeverity severity, string source, string message, Exception exception, EmbedBuilder errorEmbed = null)
    {
        Task task = null;
        if (BotInfo.LogChannel != null)
        {
            errorEmbed ??= new EmbedBuilder()
                           .WithAuthor(BotInfo.User)
                           .WithTitle(source)
                           .AddField(severity.ToString(), message.Truncate(1024))
                           .AddField("Exception", exception.ToString().Truncate(1024))
                           .WithCurrentTimestamp();

            task = BotInfo.LogChannel.SendMessageAsync(embed: errorEmbed.Build());
        }

        LogException(severity, source, message, exception);
        try
        {
            if (task != null)
                await task;
        }
        catch (Exception e)
        {
            LogException(severity, "Logging", "Something went wrong sending an exception to Discord:", exception);
        }
    }

    public static async Task LogFilterError(this Exception exception, string type, IGuild guild)
    {
        await new LogMessage(LogSeverity.Error, "Filter", $"Something went wrong with the {type} filter in {guild.Name} guild ({guild.Id}) owned by {guild.OwnerId}", exception).Log();
    }

    /// <param name="markdown">String to surround info with to generate markdown, default empty</param>
    /// <returns>String with all generic information available from <see cref="IGuild"/></returns>
    public static async Task<string> Describe(this IGuild guild, string markdown = "")
    {
        return $"{markdown}{guild.Name} ({guild.Id}){markdown} owned by {markdown}{(await guild.GetOwnerAsync()).Describe()}{markdown}";
    }

    /// <returns>String with all generic information available from <see cref="IUser"/></returns>
    public static string Describe(this IUser user)
    {
        var nickname = string.Empty;
        if (user is IGuildUser guildUser && !string.IsNullOrWhiteSpace(nickname))
            nickname = $" aka {guildUser.Nickname}";
        return $"{user.Username}#{user.Discriminator}{nickname} ({user.Id.ToString()})";
    }
}