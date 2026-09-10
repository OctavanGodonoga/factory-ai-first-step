using MongoDB.Driver.GeoJsonObjectModel;
using TalentMap.Api.Models;

namespace TalentMap.Api.Repositories;

public interface IMapPointRepository
{
    Task<MapPoint?> FindByIdAsync(string id);

    Task<MapPoint> InsertAsync(MapPoint point);

    Task<bool> ReplaceAsync(string id, MapPoint point);

    Task<IReadOnlyList<MapPoint>> FindWithinAsync(GeoJsonPolygon<GeoJson2DGeographicCoordinates> polygon);

    Task<IReadOnlyList<MapPointCluster>> FindClusteredAsync(
        GeoJsonPolygon<GeoJson2DGeographicCoordinates> polygon,
        (double MinLon, double MinLat, double MaxLon, double MaxLat) bounds,
        int gridSize);
}
