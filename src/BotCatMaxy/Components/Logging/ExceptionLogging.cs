using Discord;
using Humanizer;
using MongoDB.Driver;
using Serilog.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BotCatMaxy.Components.Logging;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Logger = Serilog.Log;

namespace BotCatMaxy
{
    public static class ExceptionLogging
    {
        [Obsolete("Use LogSeverity.Log or LogExceptionAsync instead")]
        public static Task Log(this LogMessage message)
        {
            var severity = message.Severity;
            var source = message.Source;
            var content = message.Message;
            var exception = message.Exception;
            if (exception != null) 
                return severity.LogExceptionAsync(source, content, exception);
            severity.Log(source, content);
            return Task.CompletedTask;
        }

        public static void Log(this LogSeverity severity, string source, string message)
        {
            source += ':';
            string content = source.PadRight(9) + message;
            Logger.Write(severity.SwitchToEventLevel(), content);
        }

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
        
        public static Task LogExceptionAsync(this LogSeverity severity, string source, string message, Exception exception, EmbedBuilder errorEmbed = null)
        {
            Task task = null;
            if (BotInfo.LogChannel != null)
            {
                errorEmbed ??= new EmbedBuilder()
                    .WithAuthor(BotInfo.User)
                    .WithTitle(source)
                    .AddField(severity.ToString(), message.Truncate(2000))
                    .AddField("Exception", exception.ToString().Truncate(2000))
                    .WithCurrentTimestamp();
                
                task = BotInfo.LogChannel.SendMessageAsync(embed: errorEmbed.Build());
            }

            severity.Log(source, message);
            if (exception != null)
                Console.WriteLine(exception.ToString());
            
            return task ?? Task.CompletedTask;
        }

        public static async Task AssertAsync(this bool assertion, string message = "Assertion failed")
        {
            if (assertion == false)
            {
                await Log(new LogMessage(LogSeverity.Error, "Assert", message));
            }
        }

        public static async Task AssertWarnAsync(this bool assertion, string message = "Assertion failed")
        {
            if (assertion == false)
            {
                await Log(new LogMessage(LogSeverity.Warning, "Assert", message));
            }
        }

        public static async Task LogFilterError(this Exception exception, string type, IGuild guild)
        {
            await new LogMessage(LogSeverity.Error, "Filter", $"Something went wrong with the {type} filter in {guild.Name} guild ({guild.Id}) owned by {guild.OwnerId}", exception).Log();
        }

        //just to save code
        public static async Task<string> Describe(this IGuild guild) => $"{guild.Name} ({guild.Id}) owned by {(await guild.GetOwnerAsync()).Describe()}";
        public static string Describe(this IUser user) => $"{user.Username}#{user.Discriminator} ({user.Id})";
    }
}