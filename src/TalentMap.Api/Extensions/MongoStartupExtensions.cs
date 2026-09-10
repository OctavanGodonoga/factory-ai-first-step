using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using TalentMap.Api.Models;

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

    public static async Task EnsureMapPointIndexesAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var database = app.Services.GetRequiredService<IMongoDatabase>();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var collection = database.GetCollection<MapPoint>(MapPoint.CollectionName);
            var indexKeys = Builders<MapPoint>.IndexKeys.Geo2DSphere(p => p.Location);
            var indexName = await collection.Indexes.CreateOneAsync(new CreateIndexModel<MapPoint>(indexKeys));

            stopwatch.Stop();
            logger.LogInformation(
                "2dsphere index ensured on {CollectionName}.location in {ElapsedMs} ms. Index name: {IndexName}",
                MapPoint.CollectionName,
                stopwatch.ElapsedMilliseconds,
                indexName);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning(
                ex,
                "Failed to ensure 2dsphere index on {CollectionName}.location after {ElapsedMs} ms. ExceptionType: {ExceptionType}",
                MapPoint.CollectionName,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().Name);
        }
    }
}
