using System.Diagnostics;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using TalentMap.Api.Models;
using TileCoordinate = NetTopologySuite.IO.VectorTiles.Tiles.Tile;

namespace TalentMap.Api.Services;

public class VectorTileEncoder : IVectorTileEncoder
{
    private const string LayerName = "mappoints";

    private static readonly GeometryFactory GeometryFactory = new();

    private readonly ILogger<VectorTileEncoder> _logger;

    public VectorTileEncoder(ILogger<VectorTileEncoder> logger)
    {
        _logger = logger;
    }

    public byte[] Encode(IReadOnlyList<MapPoint> points, int z, int x, int y)
    {
        _logger.LogDebug(
            "Encode called. Z: {Z}, X: {X}, Y: {Y}, PointCount: {PointCount}",
            z,
            x,
            y,
            points.Count);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tile = new TileCoordinate(x, y, z);
            var vectorTile = new VectorTile { TileId = tile.Id };
            var layer = new Layer { Name = LayerName };

            foreach (var point in points)
            {
                var geometry = GeometryFactory.CreatePoint(
                    new Coordinate(point.Location.Coordinates.Longitude, point.Location.Coordinates.Latitude));

                var attributes = new AttributesTable
                {
                    { "id", point.Id ?? string.Empty },
                    { "type", point.Type },
                    { "status", point.Status },
                };

                layer.Features.Add(new Feature(geometry, attributes));
            }

            vectorTile.Layers.Add(layer);

            using var stream = new MemoryStream();
            vectorTile.Write(stream, MapboxTileWriter.DefaultMinLinealExtent, MapboxTileWriter.DefaultMinPolygonalExtent);
            var bytes = stream.ToArray();

            stopwatch.Stop();
            _logger.LogInformation(
                "Encode succeeded. Z: {Z}, X: {X}, Y: {Y}, PointCount: {PointCount}, ByteSize: {ByteSize}, ElapsedMs: {ElapsedMs}",
                z,
                x,
                y,
                points.Count,
                bytes.Length,
                stopwatch.ElapsedMilliseconds);

            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encode failed. Z: {Z}, X: {X}, Y: {Y}", z, x, y);
            throw;
        }
    }
}
