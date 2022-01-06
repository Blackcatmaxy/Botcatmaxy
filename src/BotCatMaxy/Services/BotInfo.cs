using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Serilog;

using ILogger = Serilog.ILogger;

namespace BotCatMaxy.Services
{
    public class BotInfo : DiscordClientService
    {
        public static SocketTextChannel LogChannel { get; private set; }
        public static ISelfUser User { get; private set; }
        private readonly IConfiguration _configuration;
        private readonly DiscordSocketClient _client;
        private readonly ILogger _logger;

        public BotInfo(DiscordSocketClient client, ILogger<BotInfo> logger, IConfiguration configuration) : base(client, logger)
        {
            _client = client;
            _configuration = configuration;
            _logger = Log.ForContext("Source", "BotInfo");
        }

        protected override async Task ExecuteAsync(CancellationToken ctx)
        {
            //Gets build date
            const string buildVersionMetadataPrefix = "+build";
            var buildDate = new DateTime();
            var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(buildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value.Substring(index + buildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var result))
                    {
                        buildDate = result.ToUniversalTime();
                    }
                }
            }

            string version = _configuration["Version"] ?? "unknown";
            string shortDate = buildDate.ToShortDateString();
            string timeAgo = (DateTime.UtcNow - buildDate).LimitedHumanize();
            _logger.Debug("Starting with version {Version}, built {ShortDate}, {TimeAgo} ago",
                version, shortDate, timeAgo);

            await _client.WaitForReadyAsync(ctx);
            try
            {
                SocketGuild guild = _client.GetGuild(ulong.Parse(_configuration["LogGuild"]));
                LogChannel = guild.GetTextChannel(ulong.Parse(_configuration["ExceptionLogChannel"]));
            }
            catch
            {
                _logger.Warning("Exception log channel is not set, is the configuration `BotCatMaxy.ini` set up?");
            }

            User = _client.CurrentUser;

            int databaseCount = (await DataManipulator.dbClient.ListDatabasesAsync(ctx)).ToList(ctx).Count;
            LogSeverity.Info.Log("Mongo",$"Connected to cluster {DataManipulator.dbClient.Cluster.ClusterId} with {databaseCount} databases");
        }
    }
}