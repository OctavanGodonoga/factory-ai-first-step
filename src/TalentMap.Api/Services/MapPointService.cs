using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using TalentMap.Api.Models;
using TalentMap.Api.Repositories;

namespace TalentMap.Api.Services;

public class MapPointService : IMapPointService
{
    private readonly ILogger<MapPointService> _logger;
    private readonly IMapPointRepository _repository;
    private readonly IMoldovaBorderValidator _borderValidator;

    public MapPointService(
        ILogger<MapPointService> logger,
        IMapPointRepository repository,
        IMoldovaBorderValidator borderValidator)
    {
        _logger = logger;
        _repository = repository;
        _borderValidator = borderValidator;
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

    private void ValidateRequest(MapPointRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Numele este obligatoriu.");
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
