using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using TalentMap.Api.Models;
using TalentMap.Api.Repositories;

namespace TalentMap.Api.Services;

public class MapPointService : IMapPointService
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;

    private readonly ILogger<MapPointService> _logger;
    private readonly IMapPointRepository _repository;
    private readonly IMoldovaBorderValidator _borderValidator;
    private readonly IVectorTileEncoder _vectorTileEncoder;

    public MapPointService(
        ILogger<MapPointService> logger,
        IMapPointRepository repository,
        IMoldovaBorderValidator borderValidator,
        IVectorTileEncoder vectorTileEncoder)
    {
        _logger = logger;
        _repository = repository;
        _borderValidator = borderValidator;
        _vectorTileEncoder = vectorTileEncoder;
    }

    public async Task<MapPoint> CreateAsync(MapPointRequest request)
    {
        _logger.LogInformation(
            "CreateAsync called. Name: {Name}, Longitude: {Longitude}, Latitude: {Latitude}",
            request.Name,
            request.Longitude,
            request.Latitude);

        ValidateRequest(request);

        var point = new MapPoint
        {
            Name = request.Name,
            Description = request.Description,
            Location = BuildLocation(request),
        };

        var created = await _repository.InsertAsync(point);

        _logger.LogInformation("CreateAsync succeeded. Id: {Id}", created.Id);

        return created;
    }

    public async Task<MapPoint?> UpdateAsync(string id, MapPointRequest request)
    {
        _logger.LogInformation(
            "UpdateAsync called. Id: {Id}, Name: {Name}, Longitude: {Longitude}, Latitude: {Latitude}",
            id,
            request.Name,
            request.Longitude,
            request.Latitude);

        if (!ObjectId.TryParse(id, out _))
        {
            _logger.LogWarning("UpdateAsync received an invalid ObjectId. Id: {Id}", id);
            return null;
        }

        var existing = await _repository.FindByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        ValidateRequest(request);

        var point = new MapPoint
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            Location = BuildLocation(request),
            CreatedAt = existing.CreatedAt,
        };

        var replaced = await _repository.ReplaceAsync(id, point);
        if (!replaced)
        {
            return null;
        }

        _logger.LogInformation("UpdateAsync succeeded. Id: {Id}", id);

        return point;
    }

    public async Task<MapPointDto?> GetByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            _logger.LogWarning("GetByIdAsync received an invalid ObjectId. Id: {Id}", id);
            return null;
        }

        var point = await _repository.FindByIdAsync(id);
        if (point is null)
        {
            _logger.LogInformation("GetByIdAsync: point not found. Id: {Id}", id);
            return null;
        }

        _logger.LogDebug("GetByIdAsync: point {Id} found", id);

        return ToDto(point);
    }

    public async Task<IReadOnlyList<MapPointDto>> GetByTileAsync(int z, int x, int y)
    {
        _logger.LogInformation("GetByTileAsync called. Z: {Z}, X: {X}, Y: {Y}", z, x, y);

        var polygon = ResolveTilePolygon(nameof(GetByTileAsync), z, x, y);

        var points = await _repository.FindWithinAsync(polygon);
        var result = points.Select(ToDto).ToList();

        _logger.LogInformation(
            "GetByTileAsync succeeded. Z: {Z}, X: {X}, Y: {Y}, PointCount: {PointCount}",
            z,
            x,
            y,
            result.Count);

        return result;
    }

    public async Task<byte[]> GetTileMvtAsync(int z, int x, int y)
    {
        _logger.LogInformation("GetTileMvtAsync called. Z: {Z}, X: {X}, Y: {Y}", z, x, y);

        var polygon = ResolveTilePolygon(nameof(GetTileMvtAsync), z, x, y);

        var points = await _repository.FindWithinAsync(polygon);
        var bytes = _vectorTileEncoder.Encode(points, z, x, y);

        _logger.LogInformation(
            "GetTileMvtAsync succeeded. Z: {Z}, X: {X}, Y: {Y}, PointCount: {PointCount}, ByteSize: {ByteSize}",
            z,
            x,
            y,
            points.Count,
            bytes.Length);

        return bytes;
    }

    private GeoJsonPolygon<GeoJson2DGeographicCoordinates> ResolveTilePolygon(string callerName, int z, int x, int y)
    {
        try
        {
            return TileGeometry.ToPolygon(z, x, y);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(
                "{CallerName} received invalid tile coordinates. Z: {Z}, X: {X}, Y: {Y}, Message: {Message}",
                callerName,
                z,
                x,
                y,
                ex.Message);
            throw new MapPointValidationException(new List<string> { ex.Message });
        }
    }

    private static MapPointDto ToDto(MapPoint point)
    {
        return new MapPointDto
        {
            Id = point.Id ?? string.Empty,
            Name = point.Name,
            Description = point.Description,
            Longitude = point.Location.Coordinates.Longitude,
            Latitude = point.Location.Coordinates.Latitude,
        };
    }

    private void ValidateRequest(MapPointRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Numele este obligatoriu.");
        }
        else if (request.Name.Length > MaxNameLength)
        {
            errors.Add($"Numele nu poate depăși {MaxNameLength} de caractere.");
        }

        if (request.Description is { Length: > MaxDescriptionLength })
        {
            errors.Add($"Descrierea nu poate depăși {MaxDescriptionLength} de caractere.");
        }

        if (request.Longitude is < -180 or > 180)
        {
            errors.Add("Longitudinea trebuie să fie între -180 și 180.");
        }

        if (request.Latitude is < -90 or > 90)
        {
            errors.Add("Latitudinea trebuie să fie între -90 și 90.");
        }

        if (!_borderValidator.IsInside(request.Longitude, request.Latitude))
        {
            errors.Add("Coordonatele trebuie să fie în interiorul Moldovei.");
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed. Errors: {Errors}", string.Join("; ", errors));
            throw new MapPointValidationException(errors);
        }
    }

    private static GeoJsonPoint<GeoJson2DGeographicCoordinates> BuildLocation(MapPointRequest request)
    {
        return new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
            new GeoJson2DGeographicCoordinates(request.Longitude, request.Latitude));
    }
}
