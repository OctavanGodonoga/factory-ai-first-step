using System.Diagnostics;
using MongoDB.Driver;
using TalentMap.Api.Models;

namespace TalentMap.Api.Repositories;

public class MongoMapPointRepository : IMapPointRepository
{
    private readonly ILogger<MongoMapPointRepository> _logger;
    private readonly IMongoCollection<MapPoint> _collection;

    public MongoMapPointRepository(ILogger<MongoMapPointRepository> logger, IMongoDatabase database)
    {
        _logger = logger;
        _collection = database.GetCollection<MapPoint>(MapPoint.CollectionName);
    }

    public async Task<MapPoint?> FindByIdAsync(string id)
    {
        return await _collection.Find(Builders<MapPoint>.Filter.Eq(p => p.Id, id)).FirstOrDefaultAsync();
    }

    public async Task<MapPoint> InsertAsync(MapPoint point)
    {
        var stopwatch = Stopwatch.StartNew();

        await _collection.InsertOneAsync(point);

        stopwatch.Stop();
        _logger.LogInformation(
            "Inserted map point. Id: {Id}, ElapsedMs: {ElapsedMs}",
            point.Id,
            stopwatch.ElapsedMilliseconds);

        return point;
    }

    public async Task<bool> ReplaceAsync(string id, MapPoint point)
    {
        var stopwatch = Stopwatch.StartNew();

        var filter = Builders<MapPoint>.Filter.Eq(p => p.Id, id);
        var result = await _collection.ReplaceOneAsync(filter, point);

        stopwatch.Stop();

        if (result.MatchedCount == 0)
        {
            _logger.LogWarning(
                "Replace failed, map point not found. Id: {Id}, ElapsedMs: {ElapsedMs}",
                id,
                stopwatch.ElapsedMilliseconds);
            return false;
        }

        _logger.LogInformation(
            "Replaced map point. Id: {Id}, ElapsedMs: {ElapsedMs}",
            id,
            stopwatch.ElapsedMilliseconds);

        return true;
    }
}
