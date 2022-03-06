using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Json.Serialization.NewtonsoftJson;

namespace BotCatMaxy.Services;
#nullable enable

public class DataService
{
    private readonly ILogger _log;
    private readonly IConfiguration _config;
    public readonly DocumentStore DataStore;

    public DataService(IConfiguration configuration, ILogger<DataService> logger)
    {
        _log = logger;
        _log.LogInformation("Starting", "Data:");
        _config = configuration;
        string connectionPath = _config["DataToken"];
        string certificatePath = _config["CertificatePath"];
        X509Certificate2? certificate = null;
        if (certificatePath != null)
            certificate = new X509Certificate2(certificatePath, configuration["CertificatePassword"]);
        DataStore = new DocumentStore()
        {
            Urls = new string[] { connectionPath },
            Certificate = certificate
        };
        DataStore.Conventions.RegisterAsyncIdConvention<UserInfractions>(
            (dbname, infractions) =>
                Task.FromResult($"{infractions.GuildId}/{infractions.userId}"));
        DataStore.Conventions.RegisterAsyncIdConvention<IGuildData>(
            (dbname, settings ) =>
                Task.FromResult($"f{settings.GuildId}s/{settings.GetType().Name}"));
        /*DataStore.Conventions.FindCollectionName = type =>
        {
            var guildData = type as IGuildData;
            if (guildData != null)
            {
                return guildData.GuildId.ToString();
            }

            string message = $"Type {type.Name} is not IGuildData";
            Console.WriteLine(message);
            throw new NotImplementedException(message);
        };*/
        DataStore.Conventions.Serialization = new NewtonsoftJsonSerializationConventions
        { 
            
        };
        DataStore.Initialize();
        _log.LogInformation("Data Started");
    }
    public IDocumentSession OpenSession(string database)
        => DataStore.OpenSession(database: database);

    public IDocumentSession OpenInfractionSession()
        => OpenSession("infractions");

    public IDocumentSession OpenSettingsSession()
        => OpenSession("settings");
}