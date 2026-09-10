using TalentMap.Api.Models;

namespace TalentMap.Api.Services;

public interface IMapPointService
{
    Task<MapPoint> CreateAsync(MapPointRequest request);

    Task<MapPoint?> UpdateAsync(string id, MapPointRequest request);
}
