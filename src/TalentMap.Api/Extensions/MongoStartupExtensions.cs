using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TalentMap.Api.Extensions;

public static class MongoStartupExtensions
{
    public static void LogMongoRegistration(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var client = app.Services.GetRequiredService<IMongoClient>();
        var database = app.Services.GetRequiredService<IMongoDatabase>();

        var mongoHost = string.Join(",", client.Settings.Servers.Select(server => $"{server.Host}:{server.Port}"));
        logger.LogInformation(
            "MongoDB client registered. Host: {MongoHost}, Database: {DatabaseName}",
            mongoHost,
            database.DatabaseNamespace.DatabaseName);
    }

    public static async Task CheckMongoConnectivityAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var client = app.Services.GetRequiredService<IMongoClient>();
        var database = app.Services.GetRequiredService<IMongoDatabase>();

        var mongoHost = string.Join(",", client.Settings.Servers.Select(server => $"{server.Host}:{server.Port}"));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            stopwatch.Stop();
            logger.LogInformation(
                "MongoDB startup ping succeeded in {ElapsedMs} ms. Host: {MongoHost}, Database: {DatabaseName}",
                stopwatch.ElapsedMilliseconds,
                mongoHost,
                database.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning(
                ex,
                "MongoDB startup ping failed after {ElapsedMs} ms. Host: {MongoHost}, Database: {DatabaseName}, ExceptionType: {ExceptionType}",
                stopwatch.ElapsedMilliseconds,
                mongoHost,
                database.DatabaseNamespace.DatabaseName,
                ex.GetType().Name);
        }
    }
}
