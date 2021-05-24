using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using Discord;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using MongoDB.Driver;

namespace BotCatMaxy.Components.Logging
{
    public class BotInfo : InitializedService
    {
        public static SocketTextChannel LogChannel { get; private set; }
        public static ISelfUser User { get; private set; }
        private DiscordSocketClient _client;
        private string[] _args;

        public BotInfo(DiscordSocketClient client, string[] args)
        {
            client.Ready += Ready;
            _client = client;
            _args = args;
        }
        
        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            //Gets build date
            const string BuildVersionMetadataPrefix = "+build";
            var buildDate = new DateTime();
            var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
                    {
                        buildDate = result.ToUniversalTime();
                    }
                }
            }

            string version = _args.ElementAtOrDefault(0) ?? "unknown";
            await new LogMessage(LogSeverity.Info, "Main", $"Starting with version {version}, built " +
                                                     $"{buildDate.ToShortDateString()}, {(DateTime.UtcNow - buildDate).LimitedHumanize()} ago").Log();        
        }
        
        private async Task Ready()
        {
            _client.Ready -= Ready;
            
            SocketGuild guild = _client.GetGuild(285529027383525376);
            LogChannel = guild.GetTextChannel(593128958552309761);
            User = _client.CurrentUser;
            
            await new LogMessage(LogSeverity.Info, "Mongo", $"Connected to cluster {DataManipulator.dbClient.Cluster.ClusterId} " +
                                                            $"with {(await DataManipulator.dbClient.ListDatabasesAsync()).ToList().Count} databases").Log();
        }
    }
}