using System.Text.Json;

namespace TalentMap.Api.Services;

public class MoldovaBorderValidator : IMoldovaBorderValidator
{
    private readonly ILogger<MoldovaBorderValidator> _logger;
    private readonly IReadOnlyList<(double Longitude, double Latitude)> _borderRing;

    public MoldovaBorderValidator(ILogger<MoldovaBorderValidator> logger)
    {
        _logger = logger;

        var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "moldova-border.geojson");

        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);

            var ring = document.RootElement
                .GetProperty("geometry")
                .GetProperty("coordinates")
                .EnumerateArray()
                .First();

            var points = new List<(double Longitude, double Latitude)>(ring.GetArrayLength());
            foreach (var point in ring.EnumerateArray())
            {
                var coordinates = point.EnumerateArray().ToArray();
                points.Add((coordinates[0].GetDouble(), coordinates[1].GetDouble()));
            }

            _borderRing = points;

            _logger.LogInformation(
                "Loaded Moldova border polygon. PointCount: {PointCount}, FilePath: {FilePath}",
                _borderRing.Count,
                filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load or parse Moldova border polygon from {FilePath}", filePath);
            throw;
        }
    }

    public bool IsInside(double longitude, double latitude)
    {
        var isInside = false;
        var pointCount = _borderRing.Count;

        for (int i = 0, j = pointCount - 1; i < pointCount; j = i++)
        {
            var (xi, yi) = _borderRing[i];
            var (xj, yj) = _borderRing[j];

            var intersects = ((yi > latitude) != (yj > latitude)) &&
                (longitude < ((xj - xi) * (latitude - yi) / (yj - yi)) + xi);

            if (intersects)
            {
                isInside = !isInside;
            }
        }

        _logger.LogDebug(
            "IsInside check. Longitude: {Longitude}, Latitude: {Latitude}, Result: {IsInside}",
            longitude,
            latitude,
            isInside);

        return isInside;
    }
}
