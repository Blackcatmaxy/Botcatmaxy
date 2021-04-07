using Discord;
using Humanizer;
using MongoDB.Driver;
using Serilog;
using System;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public static class ExceptionLogging
    {
        public static ILogger logger;

        public static async Task Log(this LogMessage message)
        {
            System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace();
            string finalMessage = message.Source.PadRight(8) + message.Message;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                    if (message.Exception != null) logger.Fatal(message.Exception, finalMessage);
                    else logger.Fatal(finalMessage);
                    break;
                case LogSeverity.Error:
                    if (message.Exception != null) logger.Error(message.Exception, finalMessage);
                    else logger.Error(finalMessage);
                    break;
                case LogSeverity.Warning:
                    if (message.Exception != null) logger.Warning(message.Exception, finalMessage);
                    else logger.Warning(finalMessage);
                    break;
                case LogSeverity.Info:
                    logger.Information(finalMessage);
                    break;
                case LogSeverity.Verbose:
                    logger.Verbose(finalMessage);
                    break;
                case LogSeverity.Debug:
                    logger.Debug(finalMessage);
                    break;
            }
            if (message.Severity <= LogSeverity.Error || (string.IsNullOrEmpty(message.Source) && string.IsNullOrEmpty(message.Message)))
            { //If severity is Critical or Error
                var errorEmbed = new EmbedBuilder();
                errorEmbed.WithAuthor(BotInfo.user);
                errorEmbed.WithTitle(message.Source);
                errorEmbed.AddField(message.Severity.ToString(), message.Message.ToString().Truncate(1020));
                errorEmbed.WithCurrentTimestamp();

                if (message.Exception != null)
                    errorEmbed.AddField("Exception", message.Exception.ToString().Truncate(1020));
                else
                    errorEmbed.AddField("Trace", trace.ToString().Truncate(1020));
                await BotInfo.logChannel.SendMessageAsync(embed: errorEmbed.Build());
            }
            if (message.Exception != null || message.Severity <= LogSeverity.Error) Console.WriteLine($"Stacktrace:\n{trace}");
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
