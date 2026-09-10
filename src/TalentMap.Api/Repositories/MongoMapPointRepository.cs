using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using TalentMap.Api.Models;

namespace TalentMap.Api.Repositories;

public class MongoMapPointRepository : IMapPointRepository
{
    private const int MaxTileResults = 5000;
    private static readonly TimeSpan TileQueryTimeout = TimeSpan.FromSeconds(10);

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

    public async Task<IReadOnlyList<MapPoint>> FindWithinAsync(GeoJsonPolygon<GeoJson2DGeographicCoordinates> polygon)
    {
        var stopwatch = Stopwatch.StartNew();

        var filter = Builders<MapPoint>.Filter.GeoWithin(p => p.Location, polygon);
        var results = await _collection
            .Find(filter, new FindOptions { MaxTime = TileQueryTimeout })
            .Limit(MaxTileResults)
            .ToListAsync();

        stopwatch.Stop();

        var coordinates = polygon.Coordinates.Exterior.Positions;
        _logger.LogDebug(
            "FindWithinAsync polygon bounding box. MinLon: {MinLon}, MinLat: {MinLat}, MaxLon: {MaxLon}, MaxLat: {MaxLat}",
            coordinates[0].Longitude,
            coordinates[0].Latitude,
            coordinates[2].Longitude,
            coordinates[2].Latitude);

        if (results.Count >= MaxTileResults)
        {
            _logger.LogWarning(
                "FindWithinAsync hit the result cap. MaxTileResults: {MaxTileResults}",
                MaxTileResults);
        }

        _logger.LogInformation(
            "FindWithinAsync completed. ResultCount: {ResultCount}, ElapsedMs: {ElapsedMs}",
            results.Count,
            stopwatch.ElapsedMilliseconds);

        return results;
    }

    public async Task<IReadOnlyList<MapPointCluster>> FindClusteredAsync(
        GeoJsonPolygon<GeoJson2DGeographicCoordinates> polygon,
        (double MinLon, double MinLat, double MaxLon, double MaxLat) bounds,
        int gridSize)
    {
        var stopwatch = Stopwatch.StartNew();

        var cellWidth = (bounds.MaxLon - bounds.MinLon) / gridSize;
        var cellHeight = (bounds.MaxLat - bounds.MinLat) / gridSize;

        var filter = Builders<MapPoint>.Filter.GeoWithin(p => p.Location, polygon);

        var groupStage = new BsonDocument("$group", new BsonDocument
        {
            {
                "_id", new BsonDocument
                {
                    {
                        "cellX", new BsonDocument("$floor", new BsonDocument("$divide", new BsonArray
                        {
                            new BsonDocument("$subtract", new BsonArray
                            {
                                new BsonDocument("$arrayElemAt", new BsonArray { "$location.coordinates", 0 }),
                                bounds.MinLon,
                            }),
                            cellWidth,
                        }))
                    },
                    {
                        "cellY", new BsonDocument("$floor", new BsonDocument("$divide", new BsonArray
                        {
                            new BsonDocument("$subtract", new BsonArray
                            {
                                new BsonDocument("$arrayElemAt", new BsonArray { "$location.coordinates", 1 }),
                                bounds.MinLat,
                            }),
                            cellHeight,
                        }))
                    },
                }
            },
            { "count", new BsonDocument("$sum", 1) },
            {
                "avgLon",
                new BsonDocument("$avg", new BsonDocument("$arrayElemAt", new BsonArray { "$location.coordinates", 0 }))
            },
            {
                "avgLat",
                new BsonDocument("$avg", new BsonDocument("$arrayElemAt", new BsonArray { "$location.coordinates", 1 }))
            },
        });

        var pipeline = new EmptyPipelineDefinition<MapPoint>()
            .Match(filter)
            .AppendStage<MapPoint, MapPoint, BsonDocument>(groupStage);

        try
        {
            var documents = await _collection.Aggregate(pipeline, new AggregateOptions { MaxTime = TileQueryTimeout })
                .ToListAsync();

            var clusters = documents
                .Select(doc => new MapPointCluster(
                    doc["avgLon"].ToDouble(),
                    doc["avgLat"].ToDouble(),
                    doc["count"].ToInt32()))
                .ToList();

            stopwatch.Stop();
            _logger.LogInformation(
                "FindClusteredAsync completed. ClusterCount: {ClusterCount}, ElapsedMs: {ElapsedMs}",
                clusters.Count,
                stopwatch.ElapsedMilliseconds);

            return clusters;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "FindClusteredAsync failed. MinLon: {MinLon}, MinLat: {MinLat}, MaxLon: {MaxLon}, MaxLat: {MaxLat}, GridSize: {GridSize}",
                bounds.MinLon,
                bounds.MinLat,
                bounds.MaxLon,
                bounds.MaxLat,
                gridSize);
            throw;
        }
    }
}
